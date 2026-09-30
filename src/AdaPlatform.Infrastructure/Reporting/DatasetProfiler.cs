using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AdaPlatform.Domain.Quality;
using AdaPlatform.Domain.Raw;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// Computes the descriptive statistics of the ingested data that the thesis cites, and
/// writes them as a Markdown report (Czech, for the thesis text) plus CSV files (for charts).
///
/// Every figure has a stable ID (F1, F2, …) and states which data it was computed from and
/// how, so each number in the thesis traces back to a query over identified source files.
/// Re-running on the same data gives the same numbers; the dataset fingerprint proves it's
/// the same data.
/// </summary>
public sealed class DatasetProfiler(AppDbContext db, IOptions<HealthThresholds> options)
{
    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");

    public async Task<ProfileResult> ProfileAsync(string outputFolder, string codeVersion, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputFolder);
        var md = new StringBuilder();
        var csvFiles = new List<string>();

        void Csv(string name, string header, IEnumerable<string> rows)
        {
            var path = Path.Combine(outputFolder, name);
            File.WriteAllLines(path, rows.Prepend(header), new UTF8Encoding(false));
            csvFiles.Add(name);
        }

        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));

        // ---------- Raw UCP logs ----------
        var files = await db.SourceFiles.AsNoTracking()
            .Where(f => f.Format == SourceFormat.UcpLog)
            .Select(f => new { f.Sha256, f.SourcePath, f.VehicleId, f.ServiceDate, f.SizeBytes, f.LineCount, f.MalformedLineCount })
            .ToListAsync(ct);

        var fingerprint = files.Count == 0 ? "—" : Convert.ToHexStringLower(
            SHA256.HashData(Encoding.ASCII.GetBytes(string.Join('\n', files.Select(f => f.Sha256).Order(StringComparer.Ordinal)))))[..16];
        var archives = files.Select(f => f.SourcePath.Split('!')[0]).Distinct().Order().ToList();

        md.AppendLine("# Profil datové sady");
        md.AppendLine();
        md.AppendLine($"Vygenerováno {DateTime.Now:yyyy-MM-dd HH:mm} příkazem `AdaPlatform.Cli profile` z databáze platformy, verze kódu `{codeVersion}`.");
        md.AppendLine("Každý údaj má identifikátor (F1, F2, …) a uvádí, z jakých dat a jak byl spočten. V textu práce stačí odkázat na identifikátor, např. „(vlastní analýza, F4)“.");
        md.AppendLine();

        md.AppendLine("## Surové záznamy jednotek UCP (DPMB)");
        md.AppendLine();
        md.AppendLine($"**Zdroj:** denní logy palubního počítače pro jednotky UCP-01/UCP-02 (`APC_<vůz>.<datum>.csv`) z archivu {string.Join(", ", archives.Select(a => $"`{a}`"))}. " +
                      $"Otisk datové sady (SHA-256 přes seřazené otisky souborů, prvních 16 znaků): `{fingerprint}`.");
        md.AppendLine();

        if (files.Count > 0)
        {
            var from = files.Min(f => f.ServiceDate);
            var to = files.Max(f => f.ServiceDate);
            var malformedFiles = files.Count(f => f.MalformedLineCount > 0);

            md.AppendLine("### F1 Rozsah datové sady");
            md.AppendLine();
            md.AppendLine("| Ukazatel | Hodnota |");
            md.AppendLine("| --- | --- |");
            md.AppendLine($"| Období | {from:d. M. yyyy} – {to:d. M. yyyy} ({to.DayNumber - from.DayNumber + 1} dní) |");
            md.AppendLine($"| Vozidla | {N(files.Select(f => f.VehicleId).Distinct().Count())} |");
            md.AppendLine($"| Soubory (vůz × den) | {N(files.Count)} |");
            md.AppendLine($"| Zprávy (neprázdné platné řádky) | {N(await db.DeviceEvents.LongCountAsync(ct))} |");
            md.AppendLine($"| Řádky souborů celkem, vč. prázdných | {N(files.Sum(f => (long)f.LineCount))} |");
            md.AppendLine($"| Objem | {(files.Sum(f => f.SizeBytes) / 1024.0 / 1024.0).ToString("N1", Cs)} MiB |");
            md.AppendLine($"| Poškozené řádky | {N(files.Sum(f => f.MalformedLineCount))} v {N(malformedFiles)} souborech |");
            md.AppendLine();
            md.AppendLine("*Výpočet:* součty přes tabulku `SourceFiles`. Poškozený řádek = nemá 12 sloupců nebo neplatný čas; ve všech zjištěných případech jde o úseky bajtů NUL, typické pro výpadek napájení během zápisu.");
            md.AppendLine();
        }

        var byType = await db.DeviceEvents.AsNoTracking()
            .GroupBy(e => new { e.Code, e.Type })
            .Select(g => new { g.Key.Code, g.Key.Type, Count = g.LongCount() })
            .ToListAsync(ct);
        var totalEvents = byType.Sum(t => t.Count);

        md.AppendLine("### F2 Typy zpráv");
        md.AppendLine();
        md.AppendLine("| Kód | Typ | Počet | Podíl |");
        md.AppendLine("| --- | --- | ---: | ---: |");
        foreach (var t in byType.OrderByDescending(t => t.Count))
        {
            md.AppendLine($"| {t.Code} | {t.Type} | {N(t.Count)} | {Pct(t.Count, totalEvents)} |");
        }
        md.AppendLine();
        md.AppendLine("*Výpočet:* počet řádků `DeviceEvents` podle kódu zprávy. Význam kódů byl odvozen z parseru aplikace ADA a ověřen na datech; veřejná specifikace formátu neexistuje.");
        md.AppendLine();
        Csv("f2-typy-zprav.csv", "kod,typ,pocet", byType.OrderBy(t => t.Code).Select(t => $"{t.Code},{t.Type},{t.Count}"));

        // ---------- Counting devices ----------
        var devices = await db.CountingDevices.AsNoTracking()
            .Select(d => new { d.VehicleId, d.DeviceNumber, d.FirmwareVersion })
            .ToListAsync(ct);

        md.AppendLine("### F3 Sčítací jednotky");
        md.AppendLine();
        var vehiclesInLogs = files.Select(f => f.VehicleId).Distinct().Count();
        var vehiclesWithDevices = devices.Select(d => d.VehicleId).Distinct().Count();
        md.AppendLine($"V datech se ohlásilo **{N(devices.Count)} sčítacích jednotek** (zpravidla jedna nad každými dveřmi) na {N(vehiclesWithDevices)} vozidlech; " +
                      $"{N(vehiclesInLogs - vehiclesWithDevices)} vozidla s logem neohlásila žádnou jednotku.");
        md.AppendLine();
        md.AppendLine("| Jednotek na vozidle | Vozidel |");
        md.AppendLine("| ---: | ---: |");
        var perVehicle = devices.GroupBy(d => d.VehicleId).GroupBy(g => g.Count()).OrderBy(g => g.Key).ToList();
        foreach (var g in perVehicle)
        {
            md.AppendLine($"| {g.Key} | {g.Count()} |");
        }
        md.AppendLine();
        md.AppendLine("| Verze firmware | Jednotek |");
        md.AppendLine("| --- | ---: |");
        foreach (var g in devices.GroupBy(d => d.FirmwareVersion ?? "(neohlášena)").OrderByDescending(g => g.Count()))
        {
            md.AppendLine($"| {g.Key} | {g.Count()} |");
        }
        md.AppendLine();
        md.AppendLine("*Výpočet:* tabulka `CountingDevices` — jednotka = dvojice (vůz, adresa zařízení) s alespoň jednou zprávou; firmware = poslední verze hlášená ve zprávě stavu (kód 5).");
        md.AppendLine();
        Csv("f3-jednotky-na-vozidle.csv", "jednotek_na_vozidle,vozidel", perVehicle.Select(g => $"{g.Key},{g.Count()}"));

        // ---------- Fault signals ----------
        var heartbeats = await db.DeviceEvents.AsNoTracking()
            .Where(e => e.Type == DeviceEventType.Heartbeat)
            .GroupBy(e => new { e.Alive, e.StatusRegister })
            .Select(g => new { g.Key.Alive, g.Key.StatusRegister, Count = g.LongCount() })
            .ToListAsync(ct);
        var heartbeatTotal = heartbeats.Sum(h => h.Count);
        var notAlive = heartbeats.Where(h => h.Alive == false).Sum(h => h.Count);

        md.AppendLine("### F4 Zprávy o stavu jednotek (kód 5)");
        md.AppendLine();
        md.AppendLine($"Ze {N(heartbeatTotal)} zpráv o stavu hlásí **{N(notAlive)} ({Pct(notAlive, heartbeatTotal)}) jednotku jako neživou** (`alive=false`).");
        md.AppendLine();
        md.AppendLine("| alive | statusReg | Počet |");
        md.AppendLine("| --- | --- | ---: |");
        foreach (var h in heartbeats.OrderByDescending(h => h.Count))
        {
            md.AppendLine($"| {h.Alive?.ToString() ?? "—"} | {h.StatusRegister ?? "—"} | {N(h.Count)} |");
        }
        md.AppendLine();
        md.AppendLine("*Výpočet:* `DeviceEvents` typu Heartbeat podle polí `alive` a `statusReg`. Význam bitů `statusReg` zatím není ověřen s výrobcem.");
        md.AppendLine();

        var restartsPerDeviceDay = await db.DeviceEvents.AsNoTracking()
            .Where(e => e.Type == DeviceEventType.DeviceRestart)
            .GroupBy(e => new { e.VehicleId, e.DeviceNumber, Day = e.Time.Date })
            .Select(g => new { g.Key.VehicleId, g.Key.DeviceNumber, g.Key.Day, Count = g.Count() })
            .ToListAsync(ct);
        var activeDeviceDays = await db.DeviceEvents.AsNoTracking()
            .Where(e => e.Type == DeviceEventType.CountingStopped)
            .GroupBy(e => new { e.VehicleId, e.DeviceNumber, Day = e.Time.Date })
            .Select(g => g.Key)
            .CountAsync(ct);
        var restartCounts = restartsPerDeviceDay.Select(r => r.Count).Order().ToList();

        var tripStarts = byType.Where(t => t.Type == DeviceEventType.TripStart).Sum(t => t.Count);
        md.AppendLine("### F5 Restarty jednotek (kód 12)");
        md.AppendLine();
        if (restartCounts.Count > 0)
        {
            md.AppendLine($"Celkem **{N(restartCounts.Sum())} restartů**, zaznamenaných v {N(restartCounts.Count)} kombinacích jednotka × den (jednotky sčítaly v {N(activeDeviceDays)} kombinacích). " +
                          $"Počet restartů jedné jednotky za den: medián {Quantile(restartCounts, 0.5)}, 95. percentil {Quantile(restartCounts, 0.95)}, maximum {restartCounts[^1]}. " +
                          $"Pro srovnání: v datech je {N(tripStarts)} začátků jízd.");
            md.AppendLine();
            md.AppendLine("**Restart sám o sobě není porucha.** Restarty jsou rovnoměrně rozložené přes téměř všechny jednotky a typicky po sobě následují po 30–60 minutách, tedy zhruba s rytmem jízd; pravděpodobně jde o běžné nulování čítače. Jako signál poruchy mají smysl až odchylky od tohoto rytmu (např. maximum za den výše) — ověřit význam s výrobcem.");
        }
        md.AppendLine();
        md.AppendLine("*Výpočet:* `DeviceEvents` typu DeviceRestart seskupené podle (vůz, jednotka, kalendářní den). Kombinace „jednotka sčítala“ = alespoň jedna zpráva kódu 11 v daném dni. Odstupy restartů: rozdíl časů po sobě jdoucích restartů téže jednotky (samostatný dotaz, viz poznámky k analýze).");
        md.AppendLine();
        Csv("f5-restarty-jednotka-den.csv", "vuz,jednotka,den,restartu",
            restartsPerDeviceDay.OrderBy(r => r.VehicleId).ThenBy(r => r.DeviceNumber).ThenBy(r => r.Day)
                .Select(r => $"{r.VehicleId},{r.DeviceNumber},{r.Day:yyyy-MM-dd},{r.Count}"));

        var summaries = await db.DeviceEvents.AsNoTracking()
            .Where(e => e.Type == DeviceEventType.StopSummary)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.LongCount(),
                Flagged = g.LongCount(e => e.InvalidDevices != null),
                Negative = g.LongCount(e => e.OnBoard < 0),
            })
            .SingleOrDefaultAsync(ct);

        md.AppendLine("### F6 Zastavení s příznakem chyby a záporná obsazenost");
        md.AppendLine();
        if (summaries is not null)
        {
            md.AppendLine("| Ukazatel | Počet | Podíl ze zastavení |");
            md.AppendLine("| --- | ---: | ---: |");
            md.AppendLine($"| Zastavení celkem (zpráva kódu 15) | {N(summaries.Total)} | 100 % |");
            md.AppendLine($"| s jednotkou označenou jako chybná (`chyba`) | {N(summaries.Flagged)} | {Pct(summaries.Flagged, summaries.Total)} |");
            md.AppendLine($"| se zápornou obsazeností vozu po zastávce | {N(summaries.Negative)} | {Pct(summaries.Negative, summaries.Total)} |");
        }
        md.AppendLine();
        md.AppendLine("*Výpočet:* `DeviceEvents` typu StopSummary; obsazenost = sloupec 10 logu (počet cestujících ve voze po zastávce, jak jej vede palubní počítač). Záporná hodnota je fyzikálně nemožná a signalizuje kumulovanou chybu sčítání.");
        md.AppendLine();

        // Door counts: the in/out values of messages 10/11/12 are running counter readings,
        // so passengers at a stop = stop reading − start reading (DoorStopPairing, F11).
        var pairing = new DoorStopPairing();
        var perDoor = new Dictionary<(int VehicleId, int DeviceNumber), (int In, int Out, int Stops)>();
        var perVehicleDay = new Dictionary<(int VehicleId, DateTime Day), (int In, int Out)>();
        var perStopEvent = new Dictionary<(int VehicleId, DateTime Time), (int DeltaIn, int DeltaOut, int ReadingIn, int ReadingOut)>();
        await foreach (var c in DoorStopPairing.StreamAsync(db, pairing, ct: ct))
        {
            var door = perDoor.GetValueOrDefault((c.VehicleId, c.DeviceNumber));
            perDoor[(c.VehicleId, c.DeviceNumber)] = (door.In + c.Boardings, door.Out + c.Alightings, door.Stops + 1);
            var day = perVehicleDay.GetValueOrDefault((c.VehicleId, c.Time.Date));
            perVehicleDay[(c.VehicleId, c.Time.Date)] = (day.In + c.Boardings, day.Out + c.Alightings);
            var stop = perStopEvent.GetValueOrDefault((c.VehicleId, c.Time));
            perStopEvent[(c.VehicleId, c.Time)] = (stop.DeltaIn + c.Boardings, stop.DeltaOut + c.Alightings, stop.ReadingIn + c.ReadingBoardings, stop.ReadingOut + c.ReadingAlightings);
        }
        var deviceTotals = perDoor.Select(kv => new { kv.Key.VehicleId, kv.Key.DeviceNumber, kv.Value.In, kv.Value.Out, kv.Value.Stops }).ToList();

        var silent = deviceTotals.Where(d => QualityMetrics.IsSilent(d.Stops, d.In, d.Out)).ToList();
        var devicesPerVehicle = deviceTotals.CountBy(d => d.VehicleId).ToDictionary();
        var silentVehicles = silent.GroupBy(d => d.VehicleId)
            .Where(g => g.Count() == devicesPerVehicle[g.Key])
            .Select(g => g.Key).ToList();

        md.AppendLine("### F7 Jednotky, které za celé období nenapočítaly nikoho");
        md.AppendLine();
        md.AppendLine($"**{N(silent.Count)} z {N(deviceTotals.Count)} jednotek**, které alespoň jednou dokončily sčítání na zastávce, mělo za celé období nulový součet nástupů i výstupů, přestože odesílaly zprávy o ukončení sčítání" +
                      (silentVehicles.Count switch
                      {
                          0 => ".",
                          1 => $"; u vozidla {silentVehicles[0]} takto mlčely všechny jednotky.",
                          _ => $"; u vozidel {string.Join(", ", silentVehicles)} takto mlčely všechny jednotky.",
                      }));
        md.AppendLine();
        md.AppendLine("*Výpočet:* součet počtů jednotky na zastávkách (rozdíl stavu čítače mezi zprávou kódu 10 a 11, viz F11) podle (vůz, jednotka).");
        md.AppendLine();
        Csv("f7-soucty-jednotek.csv", "vuz,jednotka,nastupy,vystupy,zastaveni",
            deviceTotals.OrderBy(d => d.VehicleId).ThenBy(d => d.DeviceNumber)
                .Select(d => $"{d.VehicleId},{d.DeviceNumber},{d.In},{d.Out},{d.Stops}"));

        var vehicleDays = perVehicleDay.Select(kv => new { kv.Key.VehicleId, kv.Key.Day, kv.Value.In, kv.Value.Out }).ToList();
        // The same minimum as the health report, so F8 and the UI judge balance alike.
        var minPassengers = options.Value.MinPassengersForBalance;
        var traction = await db.Vehicles.AsNoTracking().ToDictionaryAsync(v => v.Id, v => v.Traction ?? "(neuvedeno)", ct);
        var balance = vehicleDays
            .Select(v => new { v.VehicleId, v.Day, v.In, v.Out, Imbalance = QualityMetrics.Imbalance(v.In, v.Out, minPassengers), Traction = traction.GetValueOrDefault(v.VehicleId, "(neuvedeno)") })
            .Where(v => v.Imbalance is not null)
            .Select(v => new { v.VehicleId, v.Day, v.In, v.Out, Imbalance = v.Imbalance!.Value, v.Traction })
            .OrderBy(v => v.Imbalance).ToList();

        md.AppendLine("### F8 Bilance nástupů a výstupů vozidla za den");
        md.AppendLine();
        if (balance.Count > 0)
        {
            var imbalances = balance.Select(b => b.Imbalance).ToList();
            var moreBoardings = balance.Count(b => b.In > b.Out);
            md.AppendLine($"Za den by počet nástupů a výstupů vozidla měl přibližně souhlasit (kdo nastoupí, také vystoupí). Pro {N(balance.Count)} kombinací vůz × den s alespoň {minPassengers} napočítanými cestujícími je relativní nesoulad |nástupy − výstupy| / (nástupy + výstupy): " +
                          $"medián {Share(Quantile(imbalances, 0.5))}, 90. percentil {Share(Quantile(imbalances, 0.9))}, maximum {Share(imbalances[^1])}.");
            md.AppendLine();
            var share = moreBoardings / (double)balance.Count;
            md.AppendLine(share switch
            {
                > 0.65 => $"**Převažují nástupy ({Pct(moreBoardings, balance.Count)} kombinací má nástupů více než výstupů)** — jednotky soustavně podhodnocují výstupy, nebo nadhodnocují nástupy.",
                < 0.35 => $"**Převažují výstupy (nástupů více má jen {Pct(moreBoardings, balance.Count)} kombinací)** — jednotky soustavně podhodnocují nástupy, nebo nadhodnocují výstupy.",
                _ => $"Směr nesouladu je celkově smíšený (nástupů více než výstupů má {Pct(moreBoardings, balance.Count)} kombinací); podle trakce se ale liší:",
            });
            md.AppendLine();
            md.AppendLine("| Trakce | Vůz × den | Medián nesouladu | Nástupů více než výstupů |");
            md.AppendLine("| --- | ---: | ---: | ---: |");
            foreach (var g in balance.GroupBy(b => b.Traction).OrderBy(g => g.Key))
            {
                var sorted = g.Select(b => b.Imbalance).Order().ToList();
                md.AppendLine($"| {g.Key} | {N(g.Count())} | {Share(Quantile(sorted, 0.5))} | {Pct(g.Count(b => b.In > b.Out), g.Count())} |");
            }
        }
        md.AppendLine();
        md.AppendLine("*Výpočet:* součet počtů na zastávkách (rozdíl stavu čítače mezi zprávou kódu 10 a 11, viz F11) podle (vůz, kalendářní den); trakce z tabulky `Vehicles` (zpráva kódu 2). Nesoulad jednotlivých dveří není sám o sobě chybou (cestující mohou nastupovat jinými dveřmi, než vystupují); kontrola proto probíhá za celé vozidlo. U spřažených tramvají může část nesouladu vznikat tím, že cestující nastoupí do jednoho vozu a vystoupí z druhého — každý vůz má vlastní log.");
        md.AppendLine();
        Csv("f8-bilance-vuz-den.csv", "vuz,den,nastupy,vystupy,nesoulad",
            balance.Select(b => $"{b.VehicleId},{b.Day:yyyy-MM-dd},{b.In},{b.Out},{b.Imbalance.ToString("0.0000", CultureInfo.InvariantCulture)}"));

        var stopSummaries = await db.DeviceEvents.AsNoTracking()
            .Where(e => e.Type == DeviceEventType.StopSummary)
            .Select(e => new { e.VehicleId, e.Time, In = e.Boardings ?? 0, Out = e.Alightings ?? 0 })
            .ToListAsync(ct);
        var comparable = stopSummaries.Where(s => perStopEvent.ContainsKey((s.VehicleId, s.Time))).ToList();
        var matchesDifference = comparable.Count(s => perStopEvent[(s.VehicleId, s.Time)] is var p && p.DeltaIn == s.In && p.DeltaOut == s.Out);
        var matchesReading = comparable.Count(s => perStopEvent[(s.VehicleId, s.Time)] is var p && p.ReadingIn == s.In && p.ReadingOut == s.Out);

        md.AppendLine("### F11 Význam hodnot `in`/`out` ve zprávách jednotek");
        md.AppendLine();
        md.AppendLine("**Hodnoty `in`/`out` ve zprávách kódů 10, 11 a 12 jsou průběžné stavy čítače jednotky, nikoli počty za zastávku.** Počet cestujících na zastávce je rozdíl stavu při ukončení (11) a při zahájení (10) sčítání. Doklad z dat:");
        md.AppendLine();
        md.AppendLine("| Pozorování | Počet | Podíl |");
        md.AppendLine("| --- | ---: | ---: |");
        md.AppendLine($"| Zahájení (10), které opakuje stav z předchozího ukončení (11) | {N(pairing.StartsRepeatingPreviousStop)} z {N(pairing.Starts)} | {Pct(pairing.StartsRepeatingPreviousStop, pairing.Starts)} |");
        md.AppendLine($"| Ukončení (11) přímo po zahájení, se stavem ≥ stavu při zahájení | {N(pairing.PairedStops)} z {N(pairing.PairedStops + pairing.DecreasingStops)} | {Pct(pairing.PairedStops, pairing.PairedStops + pairing.DecreasingStops)} |");
        md.AppendLine($"| Ukončení bez předchozího zahájení (např. po restartu) — počet nelze určit | {N(pairing.UnpairedStops)} z {N(pairing.Stops)} | {Pct(pairing.UnpairedStops, pairing.Stops)} |");
        md.AppendLine($"| Souhrn vozu (15) = součet **rozdílů** přes jednotky | {N(matchesDifference)} z {N(comparable.Count)} | {Pct(matchesDifference, comparable.Count)} |");
        md.AppendLine($"| Souhrn vozu (15) = součet **stavů** při ukončení | {N(matchesReading)} z {N(comparable.Count)} | {Pct(matchesReading, comparable.Count)} |");
        md.AppendLine();
        md.AppendLine("*Výpočet:* zprávy 10/11/12 každé jednotky seřazené podle času (`DoorStopPairing`); souhrn vozu porovnán se součtem přes jednotky ukončené ve stejném okamžiku. Důsledek: prostý součet hodnot ze zpráv 11 počet cestujících mnohonásobně nadhodnocuje; tento report i platforma proto pracují s rozdílem.");
        md.AppendLine();

        var gps = await db.DeviceEvents.AsNoTracking().GroupBy(_ => 1)
            .Select(g => new { Total = g.LongCount(), NoFix = g.LongCount(e => e.Latitude == null) })
            .SingleOrDefaultAsync(ct);
        if (gps is not null)
        {
            md.AppendLine("### F9 Poloha GPS");
            md.AppendLine();
            md.AppendLine($"{N(gps.NoFix)} z {N(gps.Total)} zpráv ({Pct(gps.NoFix, gps.Total)}) nemá platnou polohu GPS (log zapisuje `0.000000 , 0.000000`).");
            md.AppendLine();
            md.AppendLine("*Výpočet:* `DeviceEvents` s prázdnou zeměpisnou šířkou.");
            md.AppendLine();
        }

        // ---------- Legacy ADA ----------
        var legacyTrips = await db.Trips.AsNoTracking().Where(t => t.SourceFileId == null)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), From = g.Min(t => t.StartTime), To = g.Max(t => t.StartTime), Invalid = g.Count(t => !t.IsValid) })
            .SingleOrDefaultAsync(ct);
        if (legacyTrips is not null)
        {
            var legacyVisits = await db.StopVisits.CountAsync(s => s.Trip.SourceFileId == null, ct);
            var negativeTrips = await db.Trips.CountAsync(t => t.SourceFileId == null && t.StopVisits.Any(s => s.Occupancy < 0), ct);
            var minOccupancy = await db.StopVisits.Where(s => s.Trip.SourceFileId == null).MinAsync(s => s.Occupancy, ct);
            var legacyFaults = await db.DeviceFaults.CountAsync(f => f.Source == FaultSource.LegacyAda, ct);
            var legacyVehicles = await db.Trips.Where(t => t.SourceFileId == null).Select(t => t.VehicleId).Distinct().ToListAsync(ct);
            var unknownCapacity = await db.Vehicles.CountAsync(v => legacyVehicles.Contains(v.Id) && (v.SeatingCapacity == null || v.StandingCapacity == null), ct);

            md.AppendLine("## Databáze aplikace ADA (legacy)");
            md.AppendLine();
            md.AppendLine("**Zdroj:** databáze `ADA.dbFile` stávající desktopové aplikace ADA (SQLite), importovaná příkazem `import-ada`. Obsahuje již zpracovaná data — jízdy a zastavení se součty za celé vozidlo, bez údajů jednotlivých dveří a bez surových zpráv.");
            md.AppendLine();
            md.AppendLine("### F10 Rozsah a kvalita dat ADA");
            md.AppendLine();
            md.AppendLine("| Ukazatel | Hodnota |");
            md.AppendLine("| --- | --- |");
            md.AppendLine($"| Období | {legacyTrips.From:d. M. yyyy} – {legacyTrips.To:d. M. yyyy} |");
            md.AppendLine($"| Vozidla | {N(legacyVehicles.Count)} |");
            md.AppendLine($"| Jízdy | {N(legacyTrips.Count)} |");
            md.AppendLine($"| Zastavení | {N(legacyVisits)} |");
            md.AppendLine($"| Jízdy označené jako nevalidní | {N(legacyTrips.Invalid)} ({Pct(legacyTrips.Invalid, legacyTrips.Count)}); záznamů o chybě jednotky: {N(legacyFaults)} |");
            md.AppendLine($"| Jízdy, kde obsazenost klesne pod nulu | {N(negativeTrips)} ({Pct(negativeTrips, legacyTrips.Count)}), minimum {minOccupancy} |");
            md.AppendLine($"| Vozidla bez vyplněné kapacity | {N(unknownCapacity)} z {N(legacyVehicles.Count)} |");
            md.AppendLine();
            md.AppendLine("*Výpočet:* tabulky `Trips`, `StopVisits`, `DeviceFaults` a `Vehicles` omezené na záznamy bez vazby na surový soubor (`SourceFileId` je prázdné) — tedy importované z ADA. Bez kapacity vozidla nelze spočítat vytíženost v procentech.");
            md.AppendLine();
        }

        md.AppendLine("## Soubory s daty pro grafy");
        md.AppendLine();
        foreach (var csv in csvFiles)
        {
            md.AppendLine($"- `{csv}`");
        }

        var reportPath = Path.Combine(outputFolder, "profil-datove-sady.md");
        await File.WriteAllTextAsync(reportPath, md.ToString(), new UTF8Encoding(false), ct);
        return new ProfileResult(reportPath, csvFiles.Count, fingerprint);
    }

    private static string N(long value) => value.ToString("N0", Cs);

    private static string Share(double value) => value.ToString("P1", Cs);

    private static string Pct(long part, long whole) =>
        whole == 0 ? "—" : (part / (double)whole).ToString("P1", Cs);

    private static T Quantile<T>(IReadOnlyList<T> sorted, double q) =>
        sorted[(int)Math.Clamp(Math.Ceiling(q * sorted.Count) - 1, 0, sorted.Count - 1)];
}

public sealed record ProfileResult(string ReportPath, int CsvFiles, string DatasetFingerprint)
{
    public override string ToString() =>
        $"Report written to {ReportPath} (+{CsvFiles} CSV files). Dataset fingerprint {DatasetFingerprint}.";
}
