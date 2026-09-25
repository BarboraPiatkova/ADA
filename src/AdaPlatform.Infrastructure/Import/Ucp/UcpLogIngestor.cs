using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using AdaPlatform.Domain.Fleet;
using AdaPlatform.Domain.Raw;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Import.Ucp;

/// <summary>
/// Loads UCP logs into the raw layer: one <see cref="SourceFile"/> per log and one
/// <see cref="DeviceEvent"/> per line, plus the vehicles and counting devices they reveal.
///
/// Idempotent: files are recognised by content hash, so re-running over the same folder or
/// archive only adds what's new. Each file is saved in its own transaction — a crash
/// mid-run leaves whole files ingested or absent, never half a file.
/// </summary>
public sealed class UcpLogIngestor(AppDbContext db)
{
    /// <summary>Ingests every <c>APC_*.csv</c> under a folder or inside a .zip (recursively).</summary>
    public async Task<UcpIngestReport> IngestAsync(string path, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var report = new UcpIngestReport();

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        var knownHashes = (await db.SourceFiles.Select(f => f.Sha256).ToListAsync(ct)).ToHashSet();
        var knownVehicles = await db.Vehicles.AsNoTracking().ToDictionaryAsync(v => v.Id, ct);
        var devices = new Dictionary<(int Vehicle, int Device), DeviceSighting>();

        foreach (var log in EnumerateLogs(path))
        {
            ct.ThrowIfCancellationRequested();
            report.FilesSeen++;

            if (!UcpLogParser.TryParseFileName(log.FileName, out var vehicleId, out var serviceDate))
            {
                report.FilesSkippedUnrecognised++;
                continue;
            }

            var bytes = log.Read();
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!knownHashes.Add(sha256))
            {
                report.FilesSkippedDuplicate++;
                continue;
            }

            using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
            var parsed = UcpLogParser.Parse(reader, vehicleId);

            UpsertVehicle(vehicleId, parsed.VehicleInfo, knownVehicles, report);

            var sourceFile = new SourceFile
            {
                SourcePath = log.SourcePath,
                FileName = log.FileName,
                Format = SourceFormat.UcpLog,
                VehicleId = vehicleId,
                ServiceDate = serviceDate,
                Sha256 = sha256,
                SizeBytes = bytes.LongLength,
                LineCount = parsed.LineCount,
                MalformedLineCount = parsed.MalformedLineCount,
                // Stored without a time zone (see AppDbContext); this column is UTC by convention.
                ImportedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            };
            db.SourceFiles.Add(sourceFile);
            foreach (var deviceEvent in parsed.Events)
            {
                deviceEvent.SourceFile = sourceFile;
                if (deviceEvent.DeviceNumber > 0)
                {
                    RecordSighting(devices, deviceEvent);
                }
            }
            db.DeviceEvents.AddRange(parsed.Events);

            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            report.FilesIngested++;
            report.Events += parsed.Events.Count;
            report.MalformedLines += parsed.MalformedLineCount;
            if (parsed.MalformedLineCount > 0)
            {
                report.FilesWithMalformedLines++;
            }
        }

        report.NewCountingDevices = await SaveDevicesAsync(devices, ct);
        db.ChangeTracker.AutoDetectChangesEnabled = true;

        report.Elapsed = stopwatch.Elapsed;
        return report;
    }

    private void UpsertVehicle(int vehicleId, Dictionary<string, string>? info, Dictionary<int, Vehicle> known, UcpIngestReport report)
    {
        var depot = info?.GetValueOrDefault("vozovna");
        var traction = info?.GetValueOrDefault("trakce");
        var model = info?.GetValueOrDefault("typ");

        if (!known.TryGetValue(vehicleId, out var vehicle))
        {
            vehicle = new Vehicle { Id = vehicleId, Depot = depot, Traction = traction, Model = model };
            db.Vehicles.Add(vehicle);
            known[vehicleId] = vehicle;
            report.NewVehicles++;
            return;
        }

        // Fill in what an earlier source (e.g. legacy ADA) didn't know; never overwrite.
        if ((vehicle.Depot is null && depot is not null) || (vehicle.Traction is null && traction is not null) || (vehicle.Model is null && model is not null))
        {
            vehicle.Depot ??= depot;
            vehicle.Traction ??= traction;
            vehicle.Model ??= model;
            db.Vehicles.Update(vehicle);
        }
    }

    private static void RecordSighting(Dictionary<(int, int), DeviceSighting> devices, DeviceEvent e)
    {
        var key = (e.VehicleId, e.DeviceNumber);
        if (!devices.TryGetValue(key, out var sighting))
        {
            devices[key] = sighting = new DeviceSighting(e.Time);
        }
        if (e.Time < sighting.First) sighting.First = e.Time;
        if (e.Time >= sighting.Last) sighting.Last = e.Time;
        if (e.FirmwareVersion is not null && e.Time >= sighting.FirmwareSeenAt)
        {
            sighting.Firmware = e.FirmwareVersion;
            sighting.FirmwareSeenAt = e.Time;
        }
    }

    private async Task<int> SaveDevicesAsync(Dictionary<(int Vehicle, int Device), DeviceSighting> sightings, CancellationToken ct)
    {
        if (sightings.Count == 0)
        {
            return 0;
        }

        db.ChangeTracker.AutoDetectChangesEnabled = true;
        var existing = await db.CountingDevices.ToDictionaryAsync(d => (d.VehicleId, d.DeviceNumber), ct);
        var added = 0;
        foreach (var ((vehicleId, deviceNumber), sighting) in sightings)
        {
            if (existing.TryGetValue((vehicleId, deviceNumber), out var device))
            {
                if (sighting.First < device.FirstSeenAt) device.FirstSeenAt = sighting.First;
                if (sighting.Last > device.LastSeenAt)
                {
                    device.LastSeenAt = sighting.Last;
                    device.FirmwareVersion = sighting.Firmware ?? device.FirmwareVersion;
                }
            }
            else
            {
                db.CountingDevices.Add(new CountingDevice
                {
                    VehicleId = vehicleId,
                    DeviceNumber = deviceNumber,
                    FirmwareVersion = sighting.Firmware,
                    FirstSeenAt = sighting.First,
                    LastSeenAt = sighting.Last,
                });
                added++;
            }
        }
        await db.SaveChangesAsync(ct);
        return added;
    }

    private static IEnumerable<LogEntry> EnumerateLogs(string path)
    {
        if (File.Exists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var entry in EnumerateZip(path))
            {
                yield return entry;
            }
            yield break;
        }

        if (!Directory.Exists(path))
        {
            throw new InvalidOperationException($"'{path}' is neither a folder nor a .zip archive.");
        }

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var entry in EnumerateZip(file))
                {
                    yield return entry;
                }
            }
            else if (file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                var captured = file;
                yield return new LogEntry(captured, Path.GetFileName(captured), () => File.ReadAllBytes(captured));
            }
        }
    }

    private static IEnumerable<LogEntry> EnumerateZip(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries.Where(e => e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            var captured = entry;
            yield return new LogEntry($"{Path.GetFileName(zipPath)}!{captured.FullName}", captured.Name, () =>
            {
                using var stream = captured.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return buffer.ToArray();
            });
        }
    }

    private sealed record LogEntry(string SourcePath, string FileName, Func<byte[]> Read);

    private sealed class DeviceSighting(DateTime time)
    {
        public DateTime First { get; set; } = time;
        public DateTime Last { get; set; } = time;
        public string? Firmware { get; set; }
        public DateTime FirmwareSeenAt { get; set; } = DateTime.MinValue;
    }
}

public sealed class UcpIngestReport
{
    public int FilesSeen { get; set; }
    public int FilesIngested { get; set; }
    public int FilesSkippedDuplicate { get; set; }
    public int FilesSkippedUnrecognised { get; set; }
    public int FilesWithMalformedLines { get; set; }
    public int Events { get; set; }
    public int MalformedLines { get; set; }
    public int NewVehicles { get; set; }
    public int NewCountingDevices { get; set; }
    public TimeSpan Elapsed { get; set; }

    public override string ToString() => $"""
        UCP log ingestion ({Elapsed:mm\:ss}):
          files seen {FilesSeen}: ingested {FilesIngested}, already ingested {FilesSkippedDuplicate}, unrecognised name {FilesSkippedUnrecognised}
          events {Events}, malformed lines {MalformedLines} (in {FilesWithMalformedLines} files)
          new vehicles {NewVehicles}, new counting devices {NewCountingDevices}
        """;
}
