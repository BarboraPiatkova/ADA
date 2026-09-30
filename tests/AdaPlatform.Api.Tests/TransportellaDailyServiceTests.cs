using AdaPlatform.Domain.Operations;
using AdaPlatform.Infrastructure.Import.Transportella;

namespace AdaPlatform.Api.Tests;

/// <summary>Transportella's daily service report: one trip sheet, stop by stop.</summary>
public sealed class TransportellaDailyServiceTests
{
    private static Dictionary<int, string> Row(params (int Column, string Text)[] cells) => cells.ToDictionary(c => c.Column, c => c.Text);

    // A trip sheet as the report lays it out: title rows, the header, then a row per stop (column A stop,
    // B/C planned, D/E actual, F state, G deviation, H vehicle). A missing cell is just absent.
    private static readonly List<Dictionary<int, string>> Sheet =
    [
        Row((0, "Detail spoje 1/4005"), (3, "1093")),
        Row((0, "20.9.2026")),
        Row((0, "čas dle JŘ")),
        Row((0, "zastávka"), (1, "příjezd"), (2, "odjezd"), (3, "příjezd"), (4, "odjezd"), (5, "stav"), (6, "odchylka"), (7, "vůz")),
        Row((0, "ARENA BRNO"), (1, "23:58"), (2, "23:58"), (3, "23:56:24"), (4, "23:58:05"), (5, "OK"), (6, "0"), (7, "1093")),
        Row((0, "Bráfova"), (1, "00:02"), (2, "00:02"), (4, "00:02:15"), (5, "OK"), (6, "0"), (7, "1093")),
    ];

    [Fact]
    public void A_trip_sheet_gives_one_call_per_stop_with_its_times()
    {
        var day = new DateOnly(2026, 9, 20);
        var calls = TransportellaDailyServiceSource.ParseTrip(Sheet, day, "00101", "1", "4005", carrierNumber: null).ToList();

        Assert.Equal(2, calls.Count);
        var first = calls[0];
        Assert.Equal((RecordedCallSource.TransportellaDailyService, "1093", 1093, "1", "4005", "00101"),
            (first.Source, first.VehicleCode, first.VehicleId, first.Line, first.TripNumber, first.LineCourse));
        Assert.Equal(new DateTime(2026, 9, 20, 23, 58, 0), first.TripStart);
        Assert.Equal(new DateTime(2026, 9, 20, 23, 56, 24), first.ActualArrival);
        Assert.Equal(new DateTime(2026, 9, 20, 23, 58, 5), first.ActualDeparture);
        // Named stop, no number: the import matches the name to the stop list.
        Assert.Equal((0, "ARENA BRNO"), (first.StationId, first.StopName));

        // After midnight: the next day. A missing arrival stays missing (the columns don't shift).
        var second = calls[1];
        Assert.Equal(new DateTime(2026, 9, 21, 0, 2, 0), second.PlannedDeparture);
        Assert.Null(second.ActualArrival);
        Assert.Equal(new DateTime(2026, 9, 21, 0, 2, 15), second.ActualDeparture);

        // Same report again: the same ids, so a re-import adds nothing.
        Assert.Equal(calls.Select(c => c.ExternalId), TransportellaDailyServiceSource.ParseTrip(Sheet, day, "00101", "1", "4005", null).Select(c => c.ExternalId));
    }

    [Fact]
    public void Without_a_carrier_filter_the_vehicle_carries_its_carrier_number()
    {
        var call = TransportellaDailyServiceSource.ParseTrip(Sheet, new DateOnly(2026, 9, 20), "00101", "1", "4005", carrierNumber: 7).First();
        // Fleet numbers repeat between carriers, so this is no fleet number.
        Assert.Equal(("7:1093", (int?)null), (call.VehicleCode, call.VehicleId));
    }
}
