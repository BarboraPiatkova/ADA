using System.Diagnostics;
using AdaPlatform.Domain.Network;
using AdaPlatform.Domain.Raw;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdaPlatform.Infrastructure.Reconstruction;

/// <summary>
/// Derives trips, stop visits and door counts from the raw UCP events, one source file
/// (vehicle-day) at a time, with <see cref="UcpTripReconstructor"/>.
///
/// Recomputable: a file's reconstructed trips are deleted and written again in one
/// transaction, so re-running after a rule change replaces them, and the raw layer is never
/// touched. Reference data the log reveals and the timetable lacks (stops, lines, patterns,
/// blocks) is added; existing entries are only filled in (a missing name or position),
/// never overwritten.
/// </summary>
public sealed class TripReconstruction(AppDbContext db, IOptions<ReconstructionOptions> options)
{
    /// <param name="onlyNew">
    /// Only files that have no reconstructed trips yet (after an import). Otherwise every UCP
    /// file is reconstructed again.
    /// </param>
    public async Task<ReconstructionReport> ReconstructAsync(bool onlyNew = false, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var report = new ReconstructionReport();
        var reconstructor = new UcpTripReconstructor(options.Value);

        var query = db.SourceFiles.AsNoTracking().Where(f => f.Format == SourceFormat.UcpLog);
        if (onlyNew)
        {
            query = query.Where(f => !db.Trips.Any(t => t.SourceFileId == f.Id));
        }
        var files = await query.OrderBy(f => f.VehicleId).ThenBy(f => f.ServiceDate)
            .Select(f => new { f.Id, f.VehicleId })
            .ToListAsync(ct);

        var stops = await db.Stops.AsNoTracking().ToDictionaryAsync(s => s.Code, ct);
        var lines = (await db.Lines.Select(l => l.Id).ToListAsync(ct)).ToHashSet();
        var patternHasStops = await db.Patterns.Select(p => new { p.Code, HasStops = p.Stops.Any() })
            .ToDictionaryAsync(p => p.Code, p => p.HasStops, ct);
        report.BrokenPatternStopListsCleared = await ClearBrokenStopListsAsync(patternHasStops, ct);
        var blocks = (await db.Blocks.Select(b => b.Code).ToListAsync(ct)).ToHashSet();
        var devices = await db.CountingDevices.AsNoTracking()
            .ToDictionaryAsync(d => (d.VehicleId, d.DeviceNumber), d => d.Id, ct);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var events = await db.DeviceEvents.AsNoTracking()
                .Where(e => e.SourceFileId == file.Id)
                .OrderBy(e => e.LineNumber)
                .ToListAsync(ct);
            var day = reconstructor.Reconstruct(events,
                number => devices.TryGetValue((file.VehicleId, number), out var id) ? id : null);

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            report.TripsReplaced += await db.Trips.Where(t => t.SourceFileId == file.Id).ExecuteDeleteAsync(ct);

            AddStops(day, stops, report);
            AddPatterns(day, lines, patternHasStops, report);
            foreach (var block in day.Blocks.Where(blocks.Add))
            {
                db.Blocks.Add(new Block { Code = block });
                report.NewBlocks++;
            }
            foreach (var trip in day.Trips)
            {
                trip.SourceFileId = file.Id;
                // Keep the reference only if it exists (a trip start without a line can't create its pattern).
                if (trip.PatternCode is { } code && !patternHasStops.ContainsKey(code))
                {
                    trip.PatternCode = null;
                }
            }
            db.Trips.AddRange(day.Trips);

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            db.ChangeTracker.Clear();

            report.Files++;
            report.Stats.Add(day.Stats);
        }

        report.Elapsed = stopwatch.Elapsed;
        return report;
    }

    /// <summary>
    /// Stop lists taken from a trip whose log repeated calls back and forth (before the reconstructor
    /// recognised doors opening before the arrival) are dropped, so a clean trip fills them again.
    /// </summary>
    private async Task<int> ClearBrokenStopListsAsync(Dictionary<int, bool> patternHasStops, CancellationToken ct)
    {
        var lists = (await db.PatternStops.AsNoTracking().Select(ps => new { ps.PatternCode, ps.Sequence, ps.StopCode }).ToListAsync(ct))
            .GroupBy(ps => ps.PatternCode)
            .Where(g => UcpTripReconstructor.RepeatedCalls(g.OrderBy(ps => ps.Sequence).Select(ps => ps.StopCode).ToList()) >= 2)
            .Select(g => g.Key)
            .ToList();
        foreach (var code in lists)
        {
            await db.PatternStops.Where(ps => ps.PatternCode == code).ExecuteDeleteAsync(ct);
            patternHasStops[code] = false;
        }
        return lists.Count;
    }

    private void AddStops(ReconstructedDay day, Dictionary<int, Stop> known, ReconstructionReport report)
    {
        foreach (var (code, seen) in day.Stops)
        {
            if (!known.TryGetValue(code, out var stop))
            {
                db.Stops.Add(seen);
                known[code] = seen;
                report.NewStops++;
                continue;
            }

            var fillName = stop.Name.Length == 0 && seen.Name.Length > 0;
            var fillPosition = stop.Latitude is null && seen.Latitude is not null;
            if (fillName || fillPosition)
            {
                if (fillName) stop.Name = seen.Name;
                if (fillPosition) (stop.Latitude, stop.Longitude) = (seen.Latitude, seen.Longitude);
                db.Stops.Update(stop);
            }
        }
    }

    private void AddPatterns(ReconstructedDay day, HashSet<int> lines, Dictionary<int, bool> hasStops, ReconstructionReport report)
    {
        foreach (var (code, sighting) in day.Patterns)
        {
            if (!hasStops.ContainsKey(code))
            {
                if (lines.Add(sighting.Pattern.LineId))
                {
                    db.Lines.Add(new Line { Id = sighting.Pattern.LineId });
                    report.NewLines++;
                }
                db.Patterns.Add(sighting.Pattern);
                hasStops[code] = false;
                report.NewPatterns++;
            }

            if (!hasStops[code] && sighting.StopCodes is { } stopCodes)
            {
                db.PatternStops.AddRange(stopCodes.Select((stop, i) => new PatternStop { PatternCode = code, Sequence = i + 1, StopCode = stop }));
                hasStops[code] = true;
            }
        }
    }
}

public sealed class ReconstructionReport
{
    public int Files { get; set; }
    public int TripsReplaced { get; set; }
    public int NewStops { get; set; }
    public int NewLines { get; set; }

    /// <summary>Pattern stop lists with calls repeated back and forth, dropped to be filled again.</summary>
    public int BrokenPatternStopListsCleared { get; set; }
    public int NewPatterns { get; set; }
    public int NewBlocks { get; set; }
    public ReconstructionStats Stats { get; } = new();
    public TimeSpan Elapsed { get; set; }

    public override string ToString() => $"""
        Trip reconstruction ({Elapsed:mm\:ss}): {Files} files, {TripsReplaced} earlier trips replaced
          new stops {NewStops}, lines {NewLines}, patterns {NewPatterns}, blocks {NewBlocks}
          broken pattern stop lists cleared {BrokenPatternStopListsCleared}
        {Stats}
        """;
}
