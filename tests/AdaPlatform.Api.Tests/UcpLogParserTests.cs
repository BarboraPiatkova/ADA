using AdaPlatform.Api.Tests.Infrastructure;
using AdaPlatform.Domain.Raw;
using AdaPlatform.Infrastructure.Import.Ucp;

namespace AdaPlatform.Api.Tests;

/// <summary>Pure parser tests — no database, lines taken from real logs.</summary>
public class UcpLogParserTests
{
    [Fact]
    public void Counting_stop_line_yields_per_device_counts()
    {
        var e = UcpLogParser.ParseLine(
            "2022-08-02 07:23:11;49.231339 , 16.577011;01200715;12;125601;+00:06;11;stop;43;in=2, out=0, zastavka=165902;;",
            lineNumber: 7, vehicleId: 9001);

        Assert.NotNull(e);
        Assert.Equal(DeviceEventType.CountingStopped, e.Type);
        Assert.Equal(new DateTime(2022, 8, 2, 7, 23, 11), e.Time);
        Assert.Equal(43, e.DeviceNumber);
        Assert.Equal("01200715", e.BlockCode);   // leading zero kept
        Assert.Equal(12, e.LineId);
        Assert.Equal(6, e.DelaySeconds);
        Assert.Equal(165902, e.StopCode);
        Assert.Equal((2, 0), (e.Boardings, e.Alightings));
        Assert.Equal(49.231339, e.Latitude);
        Assert.Null(e.OnBoard);
    }

    [Fact]
    public void Stop_summary_carries_on_board_count_and_invalid_devices()
    {
        var e = UcpLogParser.ParseLine(
            "2022-08-02 07:23:11;49.231339 , 16.577011;01200715;12;125601;+00:06;15;cestujici;0;in=5, out=0, zastavka=165902, zast_nazev=Technologicky park, chyba=[41 42];5;5",
            1, 9001);

        Assert.NotNull(e);
        Assert.Equal(DeviceEventType.StopSummary, e.Type);
        Assert.Equal((5, 5), (e.OnBoard, e.OnBoardChange));
        Assert.Equal("41 42", e.InvalidDevices);
    }

    [Fact]
    public void Payload_values_may_contain_commas()
    {
        var payload = UcpLogParser.ParsePayload(
            "manual, index=1, id=134302, n=\"Lipova\", zpozd=+00:18, geo=ano, rychlost=0, gps=[49.191177 , 16.577198]");

        Assert.Equal("134302", payload["id"]);
        Assert.Equal("Lipova", payload["n"]);                        // quotes stripped
        Assert.Equal("[49.191177 , 16.577198]", payload["gps"]);    // comma inside the value
    }

    [Fact]
    public void Heartbeat_reports_alive_state_firmware_and_status_register()
    {
        var e = UcpLogParser.ParseLine(
            "2022-08-01 04:15:18;49.192127 , 16.573528;0;0;0;+00:00;5;status;51;alive=true, version=201913121247, init_img=201913121247, alive_sec=39, vhc_id=36947, statusReg=0x2;;",
            1, 9001);

        Assert.NotNull(e);
        Assert.Equal(DeviceEventType.Heartbeat, e.Type);
        Assert.True(e.Alive);
        Assert.Equal("201913121247", e.FirmwareVersion);
        Assert.Equal("0x2", e.StatusRegister);
        Assert.Null(e.BlockCode);   // "0" means none
        Assert.Null(e.LineId);
    }

    [Fact]
    public void Trip_start_carries_the_pattern_code()
    {
        var e = UcpLogParser.ParseLine(
            "2022-08-01 04:26:37;49.192127 , 16.573528;01200315;12;125601;+00:00;7;trasa;0;id=1200201, index=0, prvni_zast=941002 (04:35) Garaz ED Pisarky, posl_zast=125601 (04:55) Komarov;;",
            1, 9001);

        Assert.NotNull(e);
        Assert.Equal(1200201, e.PatternCode);
        Assert.Null(e.StopCode);
    }

    [Theory]
    [InlineData("+00:02", 2)]
    [InlineData("-00:56", -56)]
    [InlineData("-15:41", -941)]
    [InlineData("+95:00", 5700)]   // minutes can exceed 59
    public void Delay_is_minutes_and_seconds(string value, int expectedSeconds) =>
        Assert.Equal(expectedSeconds, UcpLogParser.ParseDelay(value));

    [Fact]
    public void Nul_filled_line_is_malformed_and_the_rest_still_parses()
    {
        using var reader = new StringReader(UcpLogFixture.Content);
        var result = UcpLogParser.Parse(reader, UcpLogFixture.VehicleId);

        Assert.Equal(1, result.MalformedLineCount);
        Assert.Equal(12, result.Events.Count);
        Assert.Equal(13, result.LineCount);
        Assert.Equal("tramvaj", result.VehicleInfo!["trakce"]);
        Assert.Equal("Vario LF2R.E", result.VehicleInfo["typ"]);
    }

    [Fact]
    public void File_name_gives_vehicle_and_day()
    {
        Assert.True(UcpLogParser.TryParseFileName("APC_1083.2022-08-01.csv", out var vehicle, out var day));
        Assert.Equal(1083, vehicle);
        Assert.Equal(new DateOnly(2022, 8, 1), day);
        Assert.False(UcpLogParser.TryParseFileName("notes.csv", out _, out _));
    }
}
