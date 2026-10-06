using System.Diagnostics;
using AdaPlatform.Domain.Operations;
using AdaPlatform.Infrastructure.Import.Epcomp;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Import.Transportella;

/// <summary>
/// Loads Transportella's per-stop operations into <see cref="RecordedCall"/>, from whichever
/// <see cref="ITransportellaStatisticsSource"/> the deployment has. Safe to re-run: rows whose external
/// id is already stored are skipped, so a nightly run only adds what is new. Saved in batches, each
/// in its own transaction, so a large dump can be interrupted and resumed.
/// </summary>
public sealed class TransportellaStatisticsImporter(AppDbContext db)
{
    private const int BatchSize = 5000;

    /// <param name="from">First service day to take (by trip start); null = from the beginning.</param>
    /// <param name="to">Last service day to take; null = up to the end.</param>
    public async Task<TransportellaImportReport> ImportAsync(
        ITransportellaStatisticsSource source, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var report = new TransportellaImportReport { Source = source.Kind };

        // Known ids of this source, sorted for binary search (8 bytes each; millions fit).
        var known = await db.RecordedCalls.Where(c => c.Source == source.Kind)
            .Select(c => c.ExternalId).OrderBy(id => id).ToArrayAsync(ct);
        var after = known.Length > 0 ? known[^1] : 0;
        // Statistics ids are the table's primary key, unique already; report ids are hashes and a
        // report could list a trip twice, so only those are checked within the run.
        HashSet<long>? inThisRun = source.Kind != RecordedCallSource.TransportellaStatistics ? [] : null;

        // Sources that name stops without numbers get the station from the operator's stop list, by name
        // (accents and spacing aside). A name used by more than one station stays unmatched.
        var stationByName = (await db.Stops.AsNoTracking().Select(s => new { s.Code, s.Name }).ToListAsync(ct))
            .Where(s => s.Name.Length > 0)
            .GroupBy(s => EpcompStationsImporter.Fold(s.Name))
            .Where(g => g.Select(s => s.Code / 100).Distinct().Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().Code / 100);

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        var batch = new List<RecordedCall>(BatchSize);
        try
        {
            await foreach (var call in source.ReadAsync(after, ct))
            {
                report.Read++;
                var day = DateOnly.FromDateTime(call.TripStart);
                if ((from is { } f && day < f) || (to is { } t && day > t))
                {
                    report.OutsidePeriod++;
                    continue;
                }
                if (Array.BinarySearch(known, call.ExternalId) >= 0 || inThisRun?.Add(call.ExternalId) == false)
                {
                    report.AlreadyImported++;
                    continue;
                }

                if (call.StationId == 0 && call.StopName is { } stopName)
                {
                    if (stationByName.TryGetValue(EpcompStationsImporter.Fold(stopName), out var station))
                    {
                        call.StationId = station;
                    }
                    else
                    {
                        report.UnknownStops.Add(stopName);
                    }
                }

                batch.Add(call);
                if (batch.Count == BatchSize)
                {
                    await SaveAsync(batch, report, ct);
                }
            }
            await SaveAsync(batch, report, ct);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        report.Elapsed = stopwatch.Elapsed;
        return report;
    }

    private async Task SaveAsync(List<RecordedCall> batch, TransportellaImportReport report, CancellationToken ct)
    {
        if (batch.Count == 0)
        {
            return;
        }
        db.RecordedCalls.AddRange(batch);
        db.ChangeTracker.DetectChanges();
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        report.Imported += batch.Count;
        report.WithoutActualTimes += batch.Count(c => c.ActualArrival is null && c.ActualDeparture is null);
        batch.Clear();
    }
}

public sealed class TransportellaImportReport
{
    public RecordedCallSource Source { get; set; }
    public long Read { get; set; }
    public long Imported { get; set; }
    public long AlreadyImported { get; set; }
    public long OutsidePeriod { get; set; }
    public long WithoutActualTimes { get; set; }
    public TimeSpan Elapsed { get; set; }

    /// <summary>Stop names no station of the stop list matches (sources without stop numbers).</summary>
    public HashSet<string> UnknownStops { get; } = [];

    public override string ToString() => $"""
        Transportella statistics import ({Source}, {Elapsed:hh\:mm\:ss}): {Read} rows read
          imported {Imported} (without actual times {WithoutActualTimes}), already imported {AlreadyImported}, outside the period {OutsidePeriod}
        """ + (UnknownStops.Count == 0 ? "" : $"\n  {UnknownStops.Count} stop names not in the stop list, e.g. {string.Join(", ", UnknownStops.Order().Take(8))}");
}
