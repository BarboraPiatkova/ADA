using AdaPlatform.Domain.Operations;
using AdaPlatform.Infrastructure.Import.Ucp;
using AdaPlatform.Infrastructure.Reconstruction;

namespace AdaPlatform.Api.Tests;

/// <summary>
/// Trip reconstruction on real UCP log lines (DPMB, tram 1083 on 2. 8. 2022, renumbered
/// 9001; door-state lines left out). Two trips: a depot run from Garaz ED Medlanky to
/// Reckovice, whose terminus is logged after the next trip, line 1 to Pisarky, is announced.
/// </summary>
public class UcpTripReconstructorTests
{
    private const int Vehicle = 9001;

    private static readonly string[] Log =
    [
        // Trip A: depot run, pattern 100901.
        "2022-08-02 04:20:29;0.000000 , 0.000000;00101115;1;155301;+00:00;7;trasa;0;id=100901, index=0, prvni_zast=943002 (04:40) Garaz ED Medlanky, posl_zast=155301 (04:47) Reckovice;;",
        "2022-08-02 04:20:29;0.000000 , 0.000000;00101115;1;155301;+00:00;110;trasa - faze;0;pred zahajenim jizdy;;",
        "2022-08-02 04:40:15;49.234379 , 16.582844;00101115;1;155301;+00:00;110;trasa - faze;0;zahajena jizda;;",
        "2022-08-02 04:40:15;49.234379 , 16.582844;00101115;1;155301;+00:00;12;restart;51;in=0, out=0;;",
        "2022-08-02 04:40:15;49.234379 , 16.582844;00101115;1;155301;+00:00;12;restart;52;in=0, out=0;;",
        "2022-08-02 04:40:15;49.234379 , 16.582844;00101115;1;155301;+00:00;12;restart;53;in=0, out=0;;",
        "2022-08-02 04:40:54;49.234562 , 16.583933;00101115;1;155301;+00:54;8;prijezd;0;zastavka=943002;;",
        "2022-08-02 04:40:54;49.234562 , 16.583933;00101115;1;155301;+00:54;10;start;52;in=0, out=0, zastavka=943002;;",
        "2022-08-02 04:40:54;49.234562 , 16.583933;00101115;1;155301;+00:54;10;start;53;in=0, out=0, zastavka=943002;;",
        "2022-08-02 04:40:54;49.234562 , 16.583933;00101115;1;155301;+00:54;10;start;51;in=0, out=0, zastavka=943002;;",
        "2022-08-02 04:41:08;49.234562 , 16.583933;00101115;1;155301;+01:08;9;odjezd;0;zastavka=943002;;",
        "2022-08-02 04:41:12;49.234634 , 16.583996;00101115;1;155301;+01:08;11;stop;51;in=0, out=0, zastavka=943002;;",
        "2022-08-02 04:41:12;49.234638 , 16.584015;00101115;1;155301;+01:08;11;stop;52;in=0, out=0, zastavka=943002;;",
        "2022-08-02 04:41:12;49.234638 , 16.584015;00101115;1;155301;+01:08;11;stop;53;in=0, out=0, zastavka=943002;;",
        "2022-08-02 04:41:12;49.234638 , 16.584015;00101115;1;155301;+01:08;15;cestujici;0;in=0, out=0, zastavka=943002, zast_nazev=Garaz ED Medlanky;0;0",
        "2022-08-02 04:43:13;49.235233 , 16.586668;00101115;1;155301;+01:13;120;zastavka - prujezd;0;manual, index=1, id=175602, n=\"Vozovna Medlanky\", zpozd=+01:13, geo=ano, rychlost=0, gps=[49.235233 , 16.586668];;",
        "2022-08-02 04:44:45;49.240383 , 16.583996;00101115;1;155301;+00:45;120;zastavka - prujezd;0;manual, index=2, id=127201, n=\"Koriskova\", zpozd=+00:45, geo=ano, rychlost=0, gps=[49.240383 , 16.583996];;",
        "2022-08-02 04:45:40;49.244301 , 16.581388;00101115;1;155301;+00:40;120;zastavka - prujezd;0;manual, index=3, id=110801, n=\"Filkukova\", zpozd=+00:40, geo=ano, rychlost=0, gps=[49.244301 , 16.581388];;",
        "2022-08-02 04:47:04;49.247047 , 16.578949;00101115;1;155301;+00:04;8;prijezd;0;zastavka=155301;;",
        "2022-08-02 04:47:04;49.247047 , 16.578949;00101115;1;155301;+00:04;110;trasa - faze;0;prijezd na konecnou;;",
        "2022-08-02 04:47:04;49.247047 , 16.578949;00101115;1;155301;+00:04;10;start;51;in=0, out=0, zastavka=155301;;",
        "2022-08-02 04:47:04;49.247047 , 16.578949;00101115;1;155301;+00:04;10;start;53;in=0, out=0, zastavka=155301;;",
        "2022-08-02 04:47:04;49.247047 , 16.578949;00101115;1;155301;+00:04;10;start;52;in=0, out=0, zastavka=155301;;",
        // Trip B is announced; A's terminus readings and summary are logged after it. The stop
        // readings already name B's first stop (155302). Door 51's two alightings are added to
        // the real (empty) run, to show where they end up.
        "2022-08-02 04:47:18;49.247047 , 16.578949;00101115;1;148701;+00:00;7;trasa;0;id=100101, index=1, prvni_zast=155302 (04:50) Reckovice, posl_zast=148701 (05:23) Pisarky;;",
        "2022-08-02 04:47:18;49.247047 , 16.578949;00101115;1;148701;+00:00;110;trasa - faze;0;pred zahajenim jizdy;;",
        "2022-08-02 04:47:24;49.247047 , 16.578949;00101115;1;148701;+00:00;11;stop;51;in=0, out=2, zastavka=155302;;",
        "2022-08-02 04:47:24;49.247047 , 16.578949;00101115;1;148701;+00:00;11;stop;52;in=0, out=0, zastavka=155302;;",
        "2022-08-02 04:47:24;49.247047 , 16.578949;00101115;1;148701;+00:00;11;stop;53;in=0, out=0, zastavka=155302;;",
        "2022-08-02 04:47:24;49.247047 , 16.578949;00101115;1;155301;+00:00;15;cestujici;0;in=0, out=2, zastavka=155301, zast_nazev=Reckovice;-2;-2",
        // Trip B: line 1, pattern 100101.
        "2022-08-02 04:49:04;49.247047 , 16.578949;00101115;1;148701;+00:00;110;trasa - faze;0;zahajena jizda;;",
        "2022-08-02 04:49:04;49.247047 , 16.578949;00101115;1;148701;+00:00;12;restart;51;in=0, out=0;;",
        "2022-08-02 04:49:04;49.247047 , 16.578949;00101115;1;148701;+00:00;12;restart;53;in=0, out=0;;",
        "2022-08-02 04:49:04;49.247047 , 16.578949;00101115;1;148701;+00:00;12;restart;52;in=0, out=0;;",
        "2022-08-02 04:49:18;49.246948 , 16.579361;00101115;1;148701;-00:42;8;prijezd;0;zastavka=155302;;",
        "2022-08-02 04:49:18;49.246948 , 16.579361;00101115;1;148701;-00:42;10;start;52;in=0, out=0, zastavka=155302;;",
        "2022-08-02 04:49:18;49.246948 , 16.579361;00101115;1;148701;-00:42;10;start;51;in=0, out=0, zastavka=155302;;",
        "2022-08-02 04:49:18;49.246948 , 16.579361;00101115;1;148701;-00:42;10;start;53;in=0, out=0, zastavka=155302;;",
        "2022-08-02 04:50:03;49.246948 , 16.579363;00101115;1;148701;+00:03;9;odjezd;0;zastavka=155302;;",
        "2022-08-02 04:50:07;49.246944 , 16.579409;00101115;1;148701;+00:03;11;stop;53;in=1, out=0, zastavka=155302;;",
        "2022-08-02 04:50:08;49.246937 , 16.579424;00101115;1;148701;+00:03;11;stop;51;in=1, out=0, zastavka=155302;;",
        "2022-08-02 04:50:08;49.246937 , 16.579424;00101115;1;148701;+00:03;11;stop;52;in=2, out=0, zastavka=155302;;",
        "2022-08-02 04:50:08;49.246937 , 16.579424;00101115;1;148701;+00:03;15;cestujici;0;in=4, out=0, zastavka=155302, zast_nazev=Reckovice;4;4",
        "2022-08-02 04:51:02;49.243477 , 16.581928;00101115;1;148701;+00:02;8;prijezd;0;zastavka=110802;;",
        "2022-08-02 04:51:02;49.243477 , 16.581928;00101115;1;148701;+00:02;10;start;51;in=1, out=0, zastavka=110802;;",
        "2022-08-02 04:51:02;49.243477 , 16.581928;00101115;1;148701;+00:02;10;start;52;in=2, out=0, zastavka=110802;;",
        "2022-08-02 04:51:02;49.243477 , 16.581928;00101115;1;148701;+00:02;10;start;53;in=1, out=0, zastavka=110802;;",
        "2022-08-02 04:51:14;49.243477 , 16.581928;00101115;1;148701;+00:14;9;odjezd;0;zastavka=110802;;",
        "2022-08-02 04:51:19;49.243412 , 16.581993;00101115;1;148701;+00:14;11;stop;51;in=3, out=0, zastavka=110802;;",
        "2022-08-02 04:51:19;49.243412 , 16.581993;00101115;1;148701;+00:14;11;stop;52;in=3, out=0, zastavka=110802;;",
        "2022-08-02 04:51:19;49.243412 , 16.581993;00101115;1;148701;+00:14;11;stop;53;in=1, out=0, zastavka=110802;;",
        "2022-08-02 04:51:19;49.243412 , 16.581993;00101115;1;148701;+00:14;15;cestujici;0;in=3, out=0, zastavka=110802, zast_nazev=Filkukova;7;3",
        "2022-08-02 04:51:23;49.243160 , 16.582161;00101115;1;148701;+00:14;11;stop;51;in=3, out=0, zastavka=110802;;",
        "2022-08-02 04:52:22;49.238895 , 16.585016;00101115;1;148701;+00:22;8;prijezd;0;zastavka=127202;;",
        "2022-08-02 04:52:22;49.238895 , 16.585016;00101115;1;148701;+00:22;10;start;52;in=3, out=0, zastavka=127202;;",
        "2022-08-02 04:52:22;49.238895 , 16.585016;00101115;1;148701;+00:22;10;start;53;in=1, out=0, zastavka=127202;;",
        "2022-08-02 04:52:22;49.238895 , 16.585016;00101115;1;148701;+00:22;10;start;51;in=3, out=0, zastavka=127202;;",
        "2022-08-02 04:52:34;49.238892 , 16.585016;00101115;1;148701;+00:34;9;odjezd;0;zastavka=127202;;",
        "2022-08-02 04:52:38;49.238838 , 16.585043;00101115;1;148701;+00:34;11;stop;51;in=3, out=1, zastavka=127202;;",
        "2022-08-02 04:52:38;49.238838 , 16.585043;00101115;1;148701;+00:34;11;stop;52;in=4, out=0, zastavka=127202;;",
        "2022-08-02 04:52:38;49.238838 , 16.585043;00101115;1;148701;+00:34;11;stop;53;in=2, out=0, zastavka=127202;;",
        "2022-08-02 04:52:38;49.238838 , 16.585043;00101115;1;148701;+00:34;15;cestujici;0;in=2, out=1, zastavka=127202, zast_nazev=Koriskova;8;1",
        "2022-08-02 04:52:42;49.238590 , 16.585222;00101115;1;148701;+00:34;11;stop;52;in=4, out=0, zastavka=127202;;",
    ];

    private static ReconstructedDay Reconstruct(IEnumerable<string> lines)
    {
        var events = lines.Select((line, i) => UcpLogParser.ParseLine(line, i + 1, Vehicle)!).ToList();
        return new UcpTripReconstructor(new ReconstructionOptions()).Reconstruct(events, device => 1000 + device);
    }

    /// <summary>The log with one line changed (matched by its start) or removed (replacement null).</summary>
    private static IEnumerable<string> LogWith(string lineStart, string? replacement) =>
        Log.SelectMany(line => line.StartsWith(lineStart, StringComparison.Ordinal)
            ? replacement is null ? [] : [replacement]
            : new[] { line });

    private static (int Boardings, int Alightings) Door(StopVisit visit, int device)
    {
        var door = visit.DoorCounts.Single(d => d.CountingDeviceId == 1000 + device);
        return (door.Boardings, door.Alightings);
    }

    [Fact]
    public void Each_trip_start_opens_a_trip_with_its_pattern_and_block()
    {
        var day = Reconstruct(Log);

        Assert.Equal([100901, 100101], day.Trips.Select(t => t.PatternCode));
        Assert.All(day.Trips, t => Assert.Equal("00101115", t.BlockCode));
        Assert.All(day.Trips, t => Assert.True(t.IsValid));
        Assert.Equal(0, day.Stats.TripsWithoutCounterReset);

        var b = day.Trips[1];
        Assert.Equal(new DateTime(2022, 8, 2, 4, 50, 3), b.StartTime);      // departure from the first stop
        Assert.Equal(new DateTime(2022, 8, 2, 4, 52, 22), b.EndTime);       // arrival at the last stop logged
        Assert.Equal(3, b.InitialDelaySeconds);
    }

    [Fact]
    public void A_terminus_logged_after_the_next_trip_start_stays_with_the_trip_that_arrived()
    {
        var day = Reconstruct(Log);
        var (a, b) = (day.Trips[0], day.Trips[1]);

        var terminus = a.StopVisits[^1];
        Assert.Equal(155301, terminus.StopCode);
        Assert.Equal((0, 2), (terminus.Boardings, terminus.Alightings));
        Assert.Equal((0, 2), Door(terminus, 51));
        Assert.Equal(-2, terminus.Occupancy);   // the vehicle's own figure, kept as it drifted

        // The terminus stop readings name 155302, but B's first visit only has B's own passengers.
        Assert.Equal([155302, 110802, 127202], b.StopVisits.Select(v => v.StopCode));
        Assert.Equal((4, 0), (b.StopVisits[0].Boardings, b.StopVisits[0].Alightings));
        Assert.True(day.Stats.EventsForPreviousTrip > 0);
    }

    [Fact]
    public void Door_counts_are_stop_minus_start_and_the_visit_is_their_sum()
    {
        var visit = Reconstruct(Log).Trips[1].StopVisits.Single(v => v.StopCode == 127202);

        Assert.Equal((0, 1), Door(visit, 51));
        Assert.Equal((1, 0), Door(visit, 52));
        Assert.Equal((1, 0), Door(visit, 53));
        Assert.Equal((2, 1, 8), (visit.Boardings, visit.Alightings, visit.Occupancy));
        Assert.Equal(new DateTime(2022, 8, 2, 4, 52, 22), visit.ArrivalTime);
        Assert.Equal(new DateTime(2022, 8, 2, 4, 52, 34), visit.DepartureTime);
        Assert.Equal(34, visit.DelaySeconds);
        Assert.All(visit.DoorCounts, d => Assert.Equal(DataOrigin.Measured, d.Origin));
    }

    [Fact]
    public void A_repeated_stop_reading_adds_only_the_passengers_counted_late()
    {
        // As logged, both repeated readings are unchanged and add nothing.
        var unchanged = Reconstruct(Log);
        Assert.Equal((2, 0), (unchanged.Stats.RepeatedStopReadings, unchanged.Stats.LateCounts));

        var day = Reconstruct(LogWith("2022-08-02 04:52:42",
            "2022-08-02 04:52:42;49.238590 , 16.585222;00101115;1;148701;+00:34;11;stop;52;in=5, out=0, zastavka=127202;;"));
        var visit = day.Trips[1].StopVisits.Single(v => v.StopCode == 127202);

        Assert.Equal((2, 0), Door(visit, 52));
        Assert.Equal((3, 1), (visit.Boardings, visit.Alightings));
        Assert.Equal(1, day.Stats.LateCounts);
        Assert.Equal(1, day.Stats.SummaryMismatches);   // the vehicle summed before the late passenger
    }

    [Fact]
    public void Stops_passed_without_opening_the_doors_are_pass_through_visits()
    {
        var a = Reconstruct(Log).Trips[0];

        Assert.Equal([943002, 175602, 127201, 110801, 155301], a.StopVisits.Select(v => v.StopCode));
        var passed = a.StopVisits.Where(v => v.IsPassThrough).ToList();
        Assert.Equal([175602, 127201, 110801], passed.Select(v => v.StopCode));
        Assert.All(passed, v => Assert.Empty(v.DoorCounts));
        Assert.Equal(45, passed[1].DelaySeconds);
    }

    [Fact]
    public void A_trip_from_a_depot_is_a_depot_run()
    {
        var day = Reconstruct(Log);
        Assert.Equal([true, false], day.Trips.Select(t => t.IsDepotRun));
    }

    [Fact]
    public void The_log_supplies_the_stops_and_patterns_the_timetable_may_lack()
    {
        var day = Reconstruct(Log);

        Assert.Equal("Garaz ED Medlanky", day.Stops[943002].Name);
        Assert.Equal("Vozovna Medlanky", day.Stops[175602].Name);
        Assert.Equal(49.246948, day.Stops[155302].Latitude);   // where the vehicle arrived
        Assert.Equal("Pisarky", day.Stops[148701].Name);        // planned last stop, not reached yet

        var depotRun = day.Patterns[100901];
        Assert.Equal((1, 155301, "Garaz ED Medlanky", "Reckovice"),
            (depotRun.Pattern.LineId, depotRun.Pattern.TargetCode, depotRun.Pattern.FirstStopName, depotRun.Pattern.LastStopName));
        // A ran its whole pattern, so it defines the stops; B hasn't reached its terminus.
        Assert.Equal([943002, 175602, 127201, 110801, 155301], depotRun.StopCodes);
        Assert.Null(day.Patterns[100101].StopCodes);
        Assert.Equal(["00101115"], day.Blocks);
    }

    [Fact]
    public void A_device_flagged_by_the_vehicle_makes_the_trip_invalid()
    {
        var day = Reconstruct(LogWith("2022-08-02 04:52:38;49.238838 , 16.585043;00101115;1;148701;+00:34;15;",
            "2022-08-02 04:52:38;49.238838 , 16.585043;00101115;1;148701;+00:34;15;cestujici;0;in=2, out=1, zastavka=127202, zast_nazev=Koriskova, chyba=[53];8;1"));
        var b = day.Trips[1];

        Assert.False(b.IsValid);
        Assert.True(b.StopVisits[^1].DoorCounts.Single(d => d.CountingDeviceId == 1053).IsFlaggedInvalid);
        Assert.Equal(1, day.Stats.TripsWithFlaggedDevice);
    }

    [Fact]
    public void A_door_that_started_counting_without_a_usable_stop_reading_makes_the_trip_invalid()
    {
        var day = Reconstruct(LogWith("2022-08-02 04:52:38;49.238838 , 16.585043;00101115;1;148701;+00:34;11;stop;53;", null));
        var visit = day.Trips[1].StopVisits[^1];

        Assert.False(day.Trips[1].IsValid);
        Assert.Equal(2, visit.DoorCounts.Count);
        Assert.Equal((1, 1), (visit.Boardings, visit.Alightings));   // what the two remaining doors counted
        Assert.Equal(1, day.Stats.MissingDoorCounts);
    }

    [Fact]
    public void A_device_reporting_itself_not_alive_during_the_trip_makes_it_invalid()
    {
        var day = Reconstruct(Log.Append(
            "2022-08-02 04:52:50;49.238590 , 16.585222;00101115;1;148701;+00:34;5;status;53;alive=false, alive_sec=0;;"));

        Assert.Equal([true, false], day.Trips.Select(t => t.IsValid));
        Assert.Equal(1, day.Stats.TripsWithDeviceNotAlive);
    }

    [Fact]
    public void A_repeated_trip_start_continues_the_running_trip()
    {
        var restated = "2022-08-02 04:51:30;49.243160 , 16.582161;00101115;1;148701;+00:14;7;trasa;0;id=100101, index=1, prvni_zast=155302 (04:50) Reckovice, posl_zast=148701 (05:23) Pisarky;;";
        var day = Reconstruct(Log.Take(Array.FindLastIndex(Log, l => l.Contains("zast_nazev=Filkukova")) + 1)
            .Append(restated)
            .Concat(Log.Skip(Array.FindLastIndex(Log, l => l.Contains("zast_nazev=Filkukova")) + 1)));

        Assert.Equal(2, day.Trips.Count);
        Assert.Equal(3, day.Trips[1].StopVisits.Count);
        Assert.Equal(1, day.Stats.RepeatedTripStarts);
    }

    [Fact]
    public void At_a_quick_turnaround_the_terminus_logged_after_the_next_trip_began_still_stays_behind()
    {
        // Real lines, 05:23–05:32: arrival at Pisarky, the next trip starts within 20 s, its
        // first arrival comes before the old terminus summary, and the counters restart before
        // the doors stop counting, so the terminus counts are lost (all three doors flagged).
        var day = Reconstruct([
            "2022-08-02 04:47:18;49.247047 , 16.578949;00101115;1;148701;+00:00;7;trasa;0;id=100101, index=1, prvni_zast=155302 (04:50) Reckovice, posl_zast=148701 (05:23) Pisarky;;",
            "2022-08-02 05:23:26;49.192940 , 16.570761;00101115;1;148701;+00:26;8;prijezd;0;zastavka=148701;;",
            "2022-08-02 05:23:26;49.192940 , 16.570761;00101115;1;148701;+00:26;110;trasa - faze;0;prijezd na konecnou;;",
            "2022-08-02 05:23:26;49.192940 , 16.570761;00101115;1;148701;+00:26;10;start;53;in=4, out=8, zastavka=148701;;",
            "2022-08-02 05:23:26;49.192940 , 16.570761;00101115;1;148701;+00:26;10;start;51;in=6, out=6, zastavka=148701;;",
            "2022-08-02 05:23:26;49.192940 , 16.570761;00101115;1;148701;+00:26;10;start;52;in=12, out=12, zastavka=148701;;",
            "2022-08-02 05:23:44;49.192936 , 16.570761;00101115;1;155301;+00:00;7;trasa;0;id=103201, index=2, prvni_zast=148702 (05:32) Pisarky, posl_zast=155301 (06:07) Reckovice;;",
            "2022-08-02 05:23:44;49.192936 , 16.570761;00101115;1;155301;+00:00;110;trasa - faze;0;pred zahajenim jizdy;;",
            "2022-08-02 05:23:45;49.192936 , 16.570761;00101115;1;155301;+00:00;110;trasa - faze;0;zahajena jizda;;",
            "2022-08-02 05:23:46;49.192936 , 16.570761;00101115;1;155301;-08:14;8;prijezd;0;zastavka=148702;;",
            "2022-08-02 05:23:46;49.192936 , 16.570761;00101115;1;148701;-08:14;15;cestujici;0;in=-22, out=-26, zastavka=148701, zast_nazev=Pisarky, chyba=[51 52 53 ];-5;4",
            "2022-08-02 05:23:46;49.192936 , 16.570761;00101115;1;155301;-08:14;12;restart;52;in=0, out=0;;",
            "2022-08-02 05:23:46;49.192936 , 16.570761;00101115;1;155301;-08:14;12;restart;51;in=0, out=0;;",
            "2022-08-02 05:23:46;49.192936 , 16.570761;00101115;1;155301;-08:14;12;restart;53;in=0, out=0;;",
            "2022-08-02 05:23:48;49.192936 , 16.570761;00101115;1;155301;-08:12;11;stop;51;in=0, out=0, zastavka=148702;;",
            "2022-08-02 05:23:48;49.192936 , 16.570761;00101115;1;155301;-08:12;11;stop;52;in=0, out=0, zastavka=148702;;",
            "2022-08-02 05:23:48;49.192936 , 16.570761;00101115;1;155301;-08:12;11;stop;53;in=0, out=0, zastavka=148702;;",
            "2022-08-02 05:23:48;49.192936 , 16.570761;00101115;1;155301;-08:12;10;start;51;in=0, out=0, zastavka=148702;;",
            "2022-08-02 05:23:48;49.192936 , 16.570761;00101115;1;155301;-08:12;10;start;53;in=0, out=0, zastavka=148702;;",
            "2022-08-02 05:23:48;49.192936 , 16.570761;00101115;1;155301;-08:12;10;start;52;in=0, out=0, zastavka=148702;;",
            "2022-08-02 05:32:04;49.193027 , 16.570742;00101115;1;155301;+00:04;9;odjezd;0;zastavka=148702;;",
            "2022-08-02 05:32:09;49.192966 , 16.570801;00101115;1;155301;+00:04;11;stop;51;in=0, out=0, zastavka=148702;;",
            "2022-08-02 05:32:09;49.192966 , 16.570801;00101115;1;155301;+00:04;15;cestujici;0;in=0, out=0, zastavka=148702, zast_nazev=Pisarky;2;0",
            "2022-08-02 05:32:09;49.192966 , 16.570801;00101115;1;155301;+00:04;11;stop;52;in=0, out=0, zastavka=148702;;",
            "2022-08-02 05:32:09;49.192963 , 16.570816;00101115;1;155301;+00:04;11;stop;53;in=4, out=2, zastavka=148702;;",
        ]);
        var (a, b) = (day.Trips[0], day.Trips[1]);

        var terminus = Assert.Single(a.StopVisits);
        Assert.Equal(148701, terminus.StopCode);
        Assert.Equal((0, 0, -5), (terminus.Boardings, terminus.Alightings, terminus.Occupancy));   // negative summary not used
        Assert.Empty(terminus.DoorCounts);
        Assert.False(a.IsValid);
        Assert.Equal(1, day.Stats.RejectedSummaries);
        Assert.Equal(3, day.Stats.MissingDoorCounts);

        var start = Assert.Single(b.StopVisits);
        Assert.Equal(148702, start.StopCode);
        Assert.Equal((4, 2), Door(start, 53));
        Assert.True(b.IsValid);
        Assert.Equal(0, day.Stats.MidTripRestarts);   // the restart began trip B
    }

    [Fact]
    public void Stop_events_before_any_trip_start_belong_to_no_trip()
    {
        var day = Reconstruct(Log.Skip(1));   // the depot run's trip start is missing

        Assert.Equal([100101], day.Trips.Select(t => t.PatternCode));
        Assert.True(day.Stats.EventsOutsideTrips > 0);
    }

    /// <summary>
    /// Real lines (DPMB, tram 1101 on 3. 8. 2022, renumbered; door-state and heartbeat lines left out): line 1 from
    /// Reckovice. At Filkukova and Koriskova the doors open before the arrival is registered, so the counting starts
    /// come first and name the stop just left, and the summary names it too (at Filkukova adding Reckovice's six).
    /// At Hudcova and Tylova the order is normal.
    /// </summary>
    private static readonly string[] DoorsBeforeArrivalLog =
    [
        "2022-08-03 21:50:09;49.247025 , 16.578959;00101515;1;134301;+00:00;7;trasa;0;id=112803, index=23, prvni_zast=155302 (22:08) Reckovice, posl_zast=134301 (22:39) Lipova;;",
        "2022-08-03 22:07:07;49.247025 , 16.578959;00101515;1;134301;+00:00;110;trasa - faze;0;zahajena jizda;;",
        "2022-08-03 22:07:07;49.247025 , 16.578959;00101515;1;134301;+00:00;12;restart;41;in=0, out=0;;",
        "2022-08-03 22:07:07;49.247025 , 16.578959;00101515;1;134301;+00:00;12;restart;42;in=0, out=0;;",
        "2022-08-03 22:07:07;49.247025 , 16.578959;00101515;1;134301;+00:00;12;restart;43;in=0, out=0;;",
        "2022-08-03 22:07:07;49.247025 , 16.578959;00101515;1;134301;+00:00;12;restart;44;in=0, out=0;;",
        "2022-08-03 22:07:20;49.246944 , 16.579254;00101515;1;134301;-00:40;8;prijezd;0;zastavka=155302;;",
        "2022-08-03 22:07:20;49.246944 , 16.579254;00101515;1;134301;-00:40;10;start;41;in=0, out=0, zastavka=155302;;",
        "2022-08-03 22:07:20;49.246944 , 16.579254;00101515;1;134301;-00:40;10;start;42;in=0, out=0, zastavka=155302;;",
        "2022-08-03 22:07:20;49.246944 , 16.579254;00101515;1;134301;-00:40;10;start;43;in=0, out=0, zastavka=155302;;",
        "2022-08-03 22:07:20;49.246944 , 16.579254;00101515;1;134301;-00:40;10;start;44;in=0, out=0, zastavka=155302;;",
        "2022-08-03 22:08:04;49.246941 , 16.579262;00101515;1;134301;+00:04;9;odjezd;0;zastavka=155302;;",
        "2022-08-03 22:08:08;49.246929 , 16.579330;00101515;1;134301;+00:04;11;stop;41;in=0, out=0, zastavka=155302;;",
        "2022-08-03 22:08:09;49.246929 , 16.579330;00101515;1;134301;+00:04;11;stop;42;in=3, out=0, zastavka=155302;;",
        "2022-08-03 22:08:09;49.246929 , 16.579330;00101515;1;134301;+00:04;11;stop;44;in=1, out=0, zastavka=155302;;",
        "2022-08-03 22:08:09;49.246929 , 16.579330;00101515;1;134301;+00:04;11;stop;43;in=2, out=0, zastavka=155302;;",
        "2022-08-03 22:08:09;49.246929 , 16.579330;00101515;1;134301;+00:04;15;cestujici;0;in=6, out=0, zastavka=155302, zast_nazev=Reckovice;6;6",
        // Filkukova: the starts name Reckovice and come before the arrival.
        "2022-08-03 22:09:09;49.243469 , 16.581783;00101515;1;134301;+00:04;10;start;41;in=0, out=0, zastavka=155302;;",
        "2022-08-03 22:09:09;49.243469 , 16.581783;00101515;1;134301;+00:04;10;start;44;in=1, out=0, zastavka=155302;;",
        "2022-08-03 22:09:09;49.243469 , 16.581783;00101515;1;134301;+00:04;10;start;42;in=3, out=0, zastavka=155302;;",
        "2022-08-03 22:09:09;49.243469 , 16.581783;00101515;1;134301;+00:04;10;start;43;in=2, out=0, zastavka=155302;;",
        "2022-08-03 22:09:09;49.243465 , 16.581785;00101515;1;134301;+00:09;8;prijezd;0;zastavka=110802;;",
        "2022-08-03 22:09:22;49.243465 , 16.581785;00101515;1;134301;+00:22;9;odjezd;0;zastavka=110802;;",
        "2022-08-03 22:09:26;49.243378 , 16.581860;00101515;1;134301;+00:22;11;stop;41;in=0, out=0, zastavka=110802;;",
        "2022-08-03 22:09:26;49.243378 , 16.581860;00101515;1;134301;+00:22;11;stop;42;in=4, out=3, zastavka=110802;;",
        "2022-08-03 22:09:26;49.243378 , 16.581860;00101515;1;134301;+00:22;11;stop;44;in=1, out=0, zastavka=110802;;",
        "2022-08-03 22:09:26;49.243378 , 16.581860;00101515;1;134301;+00:22;11;stop;43;in=2, out=0, zastavka=110802;;",
        "2022-08-03 22:09:26;49.243378 , 16.581860;00101515;1;134301;+00:22;15;cestujici;0;in=7, out=3, zastavka=155302, zast_nazev=Reckovice;4;4",
        // Koriskova: the same, naming Filkukova.
        "2022-08-03 22:10:20;49.238827 , 16.584890;00101515;1;134301;+00:22;10;start;43;in=2, out=0, zastavka=110802;;",
        "2022-08-03 22:10:20;49.238827 , 16.584890;00101515;1;134301;+00:22;10;start;44;in=1, out=0, zastavka=110802;;",
        "2022-08-03 22:10:20;49.238827 , 16.584890;00101515;1;134301;+00:22;10;start;42;in=4, out=3, zastavka=110802;;",
        "2022-08-03 22:10:20;49.238827 , 16.584890;00101515;1;134301;+00:22;10;start;41;in=0, out=0, zastavka=110802;;",
        "2022-08-03 22:10:21;49.238823 , 16.584894;00101515;1;134301;+00:21;8;prijezd;0;zastavka=127202;;",
        "2022-08-03 22:10:24;49.238823 , 16.584894;00101515;1;134301;+00:24;9;odjezd;0;zastavka=127202;;",
        "2022-08-03 22:10:28;49.238686 , 16.585009;00101515;1;134301;+00:24;11;stop;41;in=0, out=0, zastavka=127202;;",
        "2022-08-03 22:10:28;49.238686 , 16.585009;00101515;1;134301;+00:24;11;stop;42;in=4, out=3, zastavka=127202;;",
        "2022-08-03 22:10:28;49.238686 , 16.585009;00101515;1;134301;+00:24;11;stop;43;in=2, out=0, zastavka=127202;;",
        "2022-08-03 22:10:28;49.238686 , 16.585009;00101515;1;134301;+00:24;11;stop;44;in=2, out=0, zastavka=127202;;",
        "2022-08-03 22:10:28;49.238686 , 16.585009;00101515;1;134301;+00:24;15;cestujici;0;in=1, out=0, zastavka=110802, zast_nazev=Filkukova;5;1",
        "2022-08-03 22:10:32;49.238361 , 16.585224;00101515;1;134301;+00:24;11;stop;44;in=2, out=0, zastavka=127202;;",
        // Hudcova: normal order.
        "2022-08-03 22:11:04;49.235710 , 16.586985;00101515;1;134301;+00:04;8;prijezd;0;zastavka=116702;;",
        "2022-08-03 22:11:04;49.235710 , 16.586985;00101515;1;134301;+00:04;10;start;41;in=0, out=0, zastavka=116702;;",
        "2022-08-03 22:11:04;49.235710 , 16.586985;00101515;1;134301;+00:04;10;start;43;in=2, out=0, zastavka=116702;;",
        "2022-08-03 22:11:04;49.235710 , 16.586985;00101515;1;134301;+00:04;10;start;42;in=4, out=3, zastavka=116702;;",
        "2022-08-03 22:11:04;49.235710 , 16.586985;00101515;1;134301;+00:04;10;start;44;in=2, out=0, zastavka=116702;;",
        "2022-08-03 22:11:19;49.235710 , 16.586985;00101515;1;134301;+00:19;9;odjezd;0;zastavka=116702;;",
        "2022-08-03 22:11:24;49.235603 , 16.587072;00101515;1;134301;+00:19;11;stop;41;in=2, out=0, zastavka=116702;;",
        "2022-08-03 22:11:24;49.235603 , 16.587072;00101515;1;134301;+00:19;11;stop;42;in=7, out=3, zastavka=116702;;",
        "2022-08-03 22:11:24;49.235603 , 16.587072;00101515;1;134301;+00:19;11;stop;44;in=2, out=1, zastavka=116702;;",
        "2022-08-03 22:11:24;49.235603 , 16.587072;00101515;1;134301;+00:19;11;stop;43;in=3, out=0, zastavka=116702;;",
        "2022-08-03 22:11:24;49.235603 , 16.587072;00101515;1;134301;+00:19;15;cestujici;0;in=6, out=1, zastavka=116702, zast_nazev=Hudcova;10;5",
    ];

    [Fact]
    public void Doors_opening_before_the_arrival_count_at_the_stop_arrived_at_not_a_second_call_at_the_one_left()
    {
        var day = Reconstruct(DoorsBeforeArrivalLog);

        var trip = Assert.Single(day.Trips);
        Assert.Equal([155302, 110802, 127202, 116702], trip.StopVisits.Select(v => v.StopCode));
        var (reckovice, filkukova, koriskova, hudcova) = (trip.StopVisits[0], trip.StopVisits[1], trip.StopVisits[2], trip.StopVisits[3]);
        Assert.Equal((6, 0, 6), (reckovice.Boardings, reckovice.Alightings, reckovice.Occupancy));
        // Filkukova's own passengers (door 42: stop 4/3 minus start 3/0), and the summary's on-board figure.
        Assert.Equal((1, 3), Door(filkukova, 42));
        Assert.Equal((1, 3, 4), (filkukova.Boardings, filkukova.Alightings, filkukova.Occupancy));
        Assert.Equal((1, 0), (koriskova.Boardings, koriskova.Alightings));
        Assert.Equal((6, 1), (hudcova.Boardings, hudcova.Alightings));
        Assert.True(trip.IsValid);
        Assert.Equal((8, 2), (day.Stats.LateStarts, day.Stats.LateSummaries));
    }

    [Fact]
    public void A_counting_start_naming_the_stop_just_left_stays_there_when_no_arrival_follows()
    {
        // Without Filkukova's arrival the doors opened again at Reckovice, as far as the log shows.
        var day = Reconstruct(DoorsBeforeArrivalLog.Where(line => !line.StartsWith("2022-08-03 22:09:09;49.243465", StringComparison.Ordinal)));

        var trip = Assert.Single(day.Trips);
        var reckovice = trip.StopVisits[0];
        Assert.Equal(155302, reckovice.StopCode);
        Assert.Equal((4, 3), Door(reckovice, 42));   // both openings, at the stop the starts name
    }

    [Fact]
    public void Repeated_calls_count_back_and_forth_moves_a_turning_loop_makes_one()
    {
        Assert.Equal(1, UcpTripReconstructor.RepeatedCalls([148702, 148701, 148702, 134302]));
        Assert.Equal(3, UcpTripReconstructor.RepeatedCalls([155302, 110802, 155302, 110802, 127202, 110802]));
        Assert.Equal(0, UcpTripReconstructor.RepeatedCalls([1, 2, 3]));
    }

    /// <summary>
    /// Real lines (DPMB, bus 2002 on 1. 8. 2022, renumbered; door-state lines left out): line 78 after Hanacka goes
    /// through Za krizem without stopping, then at every stop the doors open before the arrival is registered, so the
    /// starts and the summary name the stop before. The summaries' counts match the next stop's door counts.
    /// </summary>
    private static readonly string[] DoorsAfterPassLog =
    [
        "2022-08-01 05:23:20;49.139263 , 16.634270;07802115;78;180901;+00:00;7;trasa;0;id=7804003, index=1, prvni_zast=513002 (05:30) Modrice, Olympia, posl_zast=180901 (06:17) Zidenice, nadrazi;;",
        "2022-08-01 05:40:36;49.146770 , 16.667370;07802115;78;180901;+00:36;8;prijezd;0;zastavka=113102;;",
        "2022-08-01 05:40:36;49.146770 , 16.667370;07802115;78;180901;+00:36;10;start;41;in=0, out=0, zastavka=113102;;",
        "2022-08-01 05:40:36;49.146770 , 16.667370;07802115;78;180901;+00:36;10;start;43;in=10, out=6, zastavka=113102;;",
        "2022-08-01 05:40:36;49.146770 , 16.667370;07802115;78;180901;+00:36;10;start;42;in=2, out=0, zastavka=113102;;",
        "2022-08-01 05:40:36;49.146770 , 16.667370;07802115;78;180901;+00:36;10;start;44;in=0, out=0, zastavka=113102;;",
        "2022-08-01 05:40:51;49.146770 , 16.667370;07802115;78;180901;+00:51;9;odjezd;0;zastavka=113102;;",
        "2022-08-01 05:40:56;49.146858 , 16.667431;07802115;78;180901;+00:51;11;stop;42;in=9, out=0, zastavka=113102;;",
        "2022-08-01 05:40:56;49.146858 , 16.667431;07802115;78;180901;+00:51;11;stop;41;in=0, out=0, zastavka=113102;;",
        "2022-08-01 05:40:56;49.146858 , 16.667431;07802115;78;180901;+00:51;11;stop;43;in=11, out=8, zastavka=113102;;",
        "2022-08-01 05:40:56;49.146858 , 16.667431;07802115;78;180901;+00:51;11;stop;44;in=1, out=0, zastavka=113102;;",
        "2022-08-01 05:40:56;49.146858 , 16.667431;07802115;78;180901;+00:51;15;cestujici;0;in=9, out=2, zastavka=113102, zast_nazev=Hanacka;13;7",
        "2022-08-01 05:42:41;49.159004 , 16.670815;07802115;78;180901;+00:41;120;zastavka - prujezd;0;manual, index=8, id=177202, n=\"Za krizem\", zpozd=+00:41, geo=ano, rychlost=16, gps=[49.159004 , 16.670815];;",
        // Areal Slatina: the starts name Za krizem, which the bus went through.
        "2022-08-01 05:44:06;49.168766 , 16.678358;07802115;78;180901;+00:41;10;start;42;in=9, out=0, zastavka=177202;;",
        "2022-08-01 05:44:06;49.168766 , 16.678358;07802115;78;180901;+00:41;10;start;41;in=0, out=0, zastavka=177202;;",
        "2022-08-01 05:44:06;49.168766 , 16.678358;07802115;78;180901;+00:41;10;start;43;in=11, out=8, zastavka=177202;;",
        "2022-08-01 05:44:06;49.168766 , 16.678358;07802115;78;180901;+00:41;10;start;44;in=1, out=0, zastavka=177202;;",
        "2022-08-01 05:44:06;49.168766 , 16.678358;07802115;78;180901;+00:06;8;prijezd;0;zastavka=158102;;",
        "2022-08-01 05:44:26;49.168766 , 16.678358;07802115;78;180901;+00:26;9;odjezd;0;zastavka=158102;;",
        "2022-08-01 05:44:30;49.168766 , 16.678358;07802115;78;180901;+00:26;11;stop;44;in=1, out=0, zastavka=158102;;",
        "2022-08-01 05:44:30;49.168766 , 16.678358;07802115;78;180901;+00:26;11;stop;42;in=11, out=5, zastavka=158102;;",
        "2022-08-01 05:44:30;49.168766 , 16.678358;07802115;78;180901;+00:26;11;stop;41;in=0, out=1, zastavka=158102;;",
        "2022-08-01 05:44:30;49.168766 , 16.678358;07802115;78;180901;+00:26;11;stop;43;in=13, out=11, zastavka=158102;;",
        "2022-08-01 05:44:30;49.168766 , 16.678358;07802115;78;180901;+00:26;15;cestujici;0;in=4, out=9, zastavka=177202, zast_nazev=Za krizem;8;-5",
        // The next stop: the starts name Areal Slatina.
        "2022-08-01 05:45:26;49.172478 , 16.682011;07802115;78;180901;+00:26;10;start;41;in=0, out=1, zastavka=158102;;",
        "2022-08-01 05:45:26;49.172478 , 16.682011;07802115;78;180901;+00:26;10;start;43;in=13, out=11, zastavka=158102;;",
        "2022-08-01 05:45:26;49.172478 , 16.682011;07802115;78;180901;+00:26;10;start;42;in=11, out=5, zastavka=158102;;",
        "2022-08-01 05:45:26;49.172478 , 16.682011;07802115;78;180901;+00:26;10;start;44;in=1, out=0, zastavka=158102;;",
        "2022-08-01 05:45:27;49.172478 , 16.682011;07802115;78;180901;+00:27;8;prijezd;0;zastavka=156203;;",
        "2022-08-01 05:45:36;49.172478 , 16.682011;07802115;78;180901;+00:36;9;odjezd;0;zastavka=156203;;",
        "2022-08-01 05:45:40;49.172558 , 16.682081;07802115;78;180901;+00:36;11;stop;42;in=11, out=6, zastavka=156203;;",
        "2022-08-01 05:45:40;49.172577 , 16.682102;07802115;78;180901;+00:36;11;stop;41;in=0, out=1, zastavka=156203;;",
        "2022-08-01 05:45:41;49.172577 , 16.682102;07802115;78;180901;+00:36;11;stop;43;in=13, out=11, zastavka=156203;;",
        "2022-08-01 05:45:41;49.172577 , 16.682102;07802115;78;180901;+00:36;11;stop;44;in=1, out=0, zastavka=156203;;",
        "2022-08-01 05:45:41;49.172577 , 16.682102;07802115;78;180901;+00:36;15;cestujici;0;in=0, out=1, zastavka=158102, zast_nazev=Areal Slatina;7;-1",
    ];

    [Fact]
    public void Doors_opening_before_the_arrival_after_a_stop_gone_through_count_at_the_stop_arrived_at()
    {
        var day = Reconstruct(DoorsAfterPassLog);

        var trip = Assert.Single(day.Trips);
        Assert.Equal([113102, 177202, 158102, 156203], trip.StopVisits.Select(v => v.StopCode));
        var (passed, slatina, next) = (trip.StopVisits[1], trip.StopVisits[2], trip.StopVisits[3]);
        Assert.True(passed.IsPassThrough);
        Assert.Empty(passed.DoorCounts);
        // The door counts agree with the summary the vehicle logged under Za krizem: 4 on, 9 off.
        Assert.Equal((4, 9), (slatina.Boardings, slatina.Alightings));
        Assert.Equal((0, 1), (next.Boardings, next.Alightings));
        Assert.Equal(0, day.Stats.SummaryMismatches);
    }

    /// <summary>
    /// Real lines (DPMB, bus 2002 on 1. 8. 2022, renumbered; door-state and heartbeat lines left out): line 78 from
    /// Zidenice, nadrazi. Right after departing, a summary of the previous trip (Stara osada, negative counts across the
    /// counter restart) is logged; then at every stop the doors open before the arrival is registered.
    /// </summary>
    private static readonly string[] StraySummaryLog =
    [
        "2022-08-01 06:17:03;49.202290 , 16.635855;07802115;78;513003;+00:00;7;trasa;0;id=7803902, index=2, prvni_zast=180903 (06:33) Zidenice, nadrazi, posl_zast=513003 (07:25) Modrice, Olympia;;",
        "2022-08-01 06:32:02;49.203224 , 16.636339;07802115;78;513003;+00:00;110;trasa - faze;0;zahajena jizda;;",
        "2022-08-01 06:32:02;49.203224 , 16.636339;07802115;78;513003;+00:00;12;restart;43;in=0, out=0;;",
        "2022-08-01 06:32:02;49.203220 , 16.636358;07802115;78;513003;+00:00;12;restart;42;in=0, out=0;;",
        "2022-08-01 06:32:02;49.203220 , 16.636358;07802115;78;513003;+00:00;12;restart;41;in=0, out=0;;",
        "2022-08-01 06:32:02;49.203220 , 16.636358;07802115;78;513003;+00:00;12;restart;44;in=0, out=0;;",
        "2022-08-01 06:32:16;49.203377 , 16.636747;07802115;78;513003;+00:00;10;start;41;in=0, out=0, zastavka=180903;;",
        "2022-08-01 06:32:16;49.203377 , 16.636747;07802115;78;513003;+00:00;10;start;43;in=0, out=0, zastavka=180903;;",
        "2022-08-01 06:32:16;49.203377 , 16.636747;07802115;78;513003;+00:00;10;start;42;in=0, out=0, zastavka=180903;;",
        "2022-08-01 06:32:16;49.203377 , 16.636747;07802115;78;513003;+00:00;10;start;44;in=0, out=0, zastavka=180903;;",
        "2022-08-01 06:32:17;49.203377 , 16.636747;07802115;78;513003;-00:43;8;prijezd;0;zastavka=180903;;",
        "2022-08-01 06:33:06;49.203377 , 16.636747;07802115;78;513003;+00:06;9;odjezd;0;zastavka=180903;;",
        "2022-08-01 06:33:10;49.203457 , 16.636795;07802115;78;513003;+00:06;11;stop;42;in=0, out=0, zastavka=180903;;",
        "2022-08-01 06:33:10;49.203457 , 16.636795;07802115;78;180901;+00:06;15;cestujici;0;in=-33, out=-20, zastavka=161106, zast_nazev=Stara osada;-3;-13",
        "2022-08-01 06:33:10;49.203457 , 16.636795;07802115;78;513003;+00:06;11;stop;41;in=0, out=0, zastavka=180903;;",
        "2022-08-01 06:33:10;49.203457 , 16.636795;07802115;78;513003;+00:06;11;stop;44;in=0, out=0, zastavka=180903;;",
        "2022-08-01 06:33:10;49.203457 , 16.636795;07802115;78;513003;+00:06;11;stop;43;in=1, out=4, zastavka=180903;;",
        "2022-08-01 06:35:14;49.202354 , 16.641977;07802115;78;513003;+00:06;10;start;42;in=0, out=0, zastavka=180903;;",
        "2022-08-01 06:35:14;49.202354 , 16.641977;07802115;78;513003;+00:06;10;start;43;in=1, out=4, zastavka=180903;;",
        "2022-08-01 06:35:14;49.202354 , 16.641977;07802115;78;513003;+00:06;10;start;44;in=0, out=0, zastavka=180903;;",
        "2022-08-01 06:35:14;49.202354 , 16.641977;07802115;78;513003;+00:06;10;start;41;in=0, out=0, zastavka=180903;;",
        "2022-08-01 06:35:14;49.202354 , 16.641977;07802115;78;513003;-01:46;8;prijezd;0;zastavka=161108;;",
        "2022-08-01 06:37:03;49.202354 , 16.641977;07802115;78;513003;+00:03;9;odjezd;0;zastavka=161108;;",
        "2022-08-01 06:37:07;49.202415 , 16.642029;07802115;78;513003;+00:03;11;stop;42;in=4, out=0, zastavka=161108;;",
        "2022-08-01 06:37:07;49.202415 , 16.642029;07802115;78;513003;+00:03;11;stop;41;in=0, out=0, zastavka=161108;;",
        "2022-08-01 06:37:07;49.202431 , 16.642038;07802115;78;513003;+00:03;11;stop;43;in=12, out=7, zastavka=161108;;",
        "2022-08-01 06:37:07;49.202431 , 16.642038;07802115;78;513003;+00:03;11;stop;44;in=1, out=0, zastavka=161108;;",
        "2022-08-01 06:37:07;49.202431 , 16.642038;07802115;78;513003;+00:03;15;cestujici;0;in=16, out=3, zastavka=180903, zast_nazev=Zidenice, nadrazi;10;13",
        "2022-08-01 06:39:20;49.198708 , 16.645470;07802115;78;513003;+00:03;10;start;42;in=4, out=0, zastavka=161108;;",
        "2022-08-01 06:39:20;49.198708 , 16.645470;07802115;78;513003;+00:03;10;start;41;in=0, out=0, zastavka=161108;;",
        "2022-08-01 06:39:20;49.198708 , 16.645470;07802115;78;513003;+00:03;10;start;44;in=1, out=0, zastavka=161108;;",
        "2022-08-01 06:39:20;49.198708 , 16.645470;07802115;78;513003;+00:03;10;start;43;in=0, out=0, zastavka=161108;;",
        "2022-08-01 06:39:20;49.198708 , 16.645470;07802115;78;513003;+00:20;8;prijezd;0;zastavka=111301;;",
        "2022-08-01 06:39:28;49.198708 , 16.645470;07802115;78;513003;+00:28;9;odjezd;0;zastavka=111301;;",
        "2022-08-01 06:39:33;49.198681 , 16.645498;07802115;78;513003;+00:28;11;stop;43;in=0, out=0, zastavka=111301;;",
        "2022-08-01 06:39:33;49.198681 , 16.645498;07802115;78;513003;+00:28;11;stop;42;in=4, out=1, zastavka=111301;;",
        "2022-08-01 06:39:33;49.198681 , 16.645498;07802115;78;513003;+00:28;11;stop;41;in=0, out=0, zastavka=111301;;",
        "2022-08-01 06:39:33;49.198681 , 16.645498;07802115;78;513003;+00:28;11;stop;44;in=1, out=0, zastavka=111301;;",
        "2022-08-01 06:39:33;49.198681 , 16.645498;07802115;78;513003;+00:28;15;cestujici;0;in=0, out=1, zastavka=161108, zast_nazev=Stara osada;4;-1",
        "2022-08-01 06:39:37;49.198563 , 16.645599;07802115;78;513003;+00:28;11;stop;42;in=4, out=1, zastavka=111301;;",
        "2022-08-01 06:41:49;49.195599 , 16.650114;07802115;78;513003;+00:28;10;start;41;in=0, out=0, zastavka=111301;;",
        "2022-08-01 06:41:49;49.195599 , 16.650114;07802115;78;513003;+00:28;10;start;44;in=1, out=0, zastavka=111301;;",
        "2022-08-01 06:41:49;49.195599 , 16.650114;07802115;78;513003;+00:28;10;start;43;in=0, out=0, zastavka=111301;;",
        "2022-08-01 06:41:49;49.195599 , 16.650114;07802115;78;513003;+00:28;10;start;42;in=4, out=1, zastavka=111301;;",
        "2022-08-01 06:41:49;49.195599 , 16.650114;07802115;78;513003;+00:49;8;prijezd;0;zastavka=108201;;",
        "2022-08-01 06:41:59;49.195599 , 16.650114;07802115;78;513003;+00:59;9;odjezd;0;zastavka=108201;;",
    ];

    [Fact]
    public void A_stray_summary_of_another_trip_neither_adds_a_stop_nor_hides_the_stop_the_vehicle_left()
    {
        var day = Reconstruct(StraySummaryLog);

        var trip = Assert.Single(day.Trips);
        var stops = trip.StopVisits.Select(v => v.StopCode).ToList();
        Assert.DoesNotContain(161106, stops);
        Assert.Equal(0, UcpTripReconstructor.RepeatedCalls(stops));
        Assert.All(trip.StopVisits, v => Assert.NotNull(v.ArrivalTime));
        // 161108: door 43 stop 12/7 minus start 1/4, door 42 4/0 minus 0/0, door 44 1/0 minus 0/0.
        var second = trip.StopVisits[1];
        Assert.Equal(161108, second.StopCode);
        Assert.Equal((16, 3), (second.Boardings, second.Alightings));   // as the summary logged under the stale code says
    }

    /// <summary>
    /// Real lines (DPMB, bus 2001 on 1. 8. 2022, renumbered; door-state and heartbeat lines left out): after going
    /// through Akatky the bus stops at Vlcnovska, where no unit counts; the summary for Vlcnovska, every unit flagged,
    /// is logged under Akatky.
    /// </summary>
    private static readonly string[] SummaryUnderEarlierStopLog =
    [
        "2022-08-01 20:17:03;49.203129 , 16.642025;08401315;27;120806;+00:00;7;trasa;0;id=2700101, index=17, prvni_zast=161104 (20:51) Stara osada, posl_zast=120806 (21:06) Jirova;;",
        "2022-08-01 20:53:16;49.206783 , 16.644558;08401315;27;120806;+00:16;8;prijezd;0;zastavka=169801;;",
        "2022-08-01 20:53:33;49.206783 , 16.644558;08401315;27;120806;+00:33;9;odjezd;0;zastavka=169801;;",
        "2022-08-01 20:53:54;49.207241 , 16.645916;08401315;27;120806;+00:33;15;cestujici;0;in=0, out=0, zastavka=169801, zast_nazev=Udolicek, chyba=[41 42 43 44 ];0;0",
        "2022-08-01 20:54:19;49.208530 , 16.648643;08401315;27;120806;+01:19;120;zastavka - prujezd;0;manual, index=2, id=100201, n=\"Akatky\", zpozd=+01:19, geo=ano, rychlost=0, gps=[49.208530 , 16.648643];;",
        "2022-08-01 20:55:57;49.211010 , 16.660280;08401315;27;120806;+00:57;8;prijezd;0;zastavka=174301;;",
        "2022-08-01 20:56:06;49.211010 , 16.660280;08401315;27;120806;+01:06;9;odjezd;0;zastavka=174301;;",
        "2022-08-01 20:56:27;49.210403 , 16.662157;08401315;27;120806;+01:06;15;cestujici;0;in=0, out=0, zastavka=100201, zast_nazev=Akatky, chyba=[41 42 43 44 ];0;0",
        "2022-08-01 20:56:58;49.208263 , 16.663437;08401315;27;120806;+01:06;10;start;41;in=0, out=0, zastavka=174301;;",
        "2022-08-01 20:56:58;49.208263 , 16.663437;08401315;27;120806;+01:06;10;start;43;in=0, out=0, zastavka=174301;;",
        "2022-08-01 20:56:58;49.208263 , 16.663437;08401315;27;120806;+01:06;10;start;44;in=0, out=0, zastavka=174301;;",
        "2022-08-01 20:56:58;49.208263 , 16.663437;08401315;27;120806;+01:06;10;start;42;in=0, out=0, zastavka=174301;;",
        "2022-08-01 20:56:58;49.208263 , 16.663437;08401315;27;120806;+00:58;8;prijezd;0;zastavka=152301;;",
        "2022-08-01 20:57:09;49.208263 , 16.663437;08401315;27;120806;+01:09;9;odjezd;0;zastavka=152301;;",
        "2022-08-01 20:57:13;49.208202 , 16.663485;08401315;27;120806;+01:09;11;stop;42;in=0, out=0, zastavka=152301;;",
        "2022-08-01 20:57:13;49.208179 , 16.663498;08401315;27;120806;+01:09;11;stop;44;in=0, out=2, zastavka=152301;;",
        "2022-08-01 20:57:13;49.208179 , 16.663498;08401315;27;120806;+01:09;11;stop;41;in=0, out=0, zastavka=152301;;",
        "2022-08-01 20:57:13;49.208179 , 16.663498;08401315;27;120806;+01:09;11;stop;43;in=0, out=1, zastavka=152301;;",
        "2022-08-01 20:57:13;49.208179 , 16.663498;08401315;27;120806;+01:09;15;cestujici;0;in=0, out=3, zastavka=174301, zast_nazev=Vlcnovska;-3;-3",
    ];

    [Fact]
    public void A_summary_naming_the_stop_before_the_last_one_reached_belongs_to_the_last_one()
    {
        var day = Reconstruct(SummaryUnderEarlierStopLog);

        var trip = Assert.Single(day.Trips);
        var stops = trip.StopVisits.Select(v => v.StopCode).ToList();
        Assert.Equal(0, UcpTripReconstructor.RepeatedCalls(stops));
        Assert.Single(stops, code => code == 100201);
        var vlcnovska = trip.StopVisits.Single(v => v.StopCode == 174301);
        Assert.False(vlcnovska.IsPassThrough);
        Assert.False(trip.IsValid);   // its units were flagged
    }
}
