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
}
