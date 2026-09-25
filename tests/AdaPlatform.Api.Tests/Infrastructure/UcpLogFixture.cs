namespace AdaPlatform.Api.Tests.Infrastructure;

/// <summary>
/// Real UCP log lines (DPMB, August 2022), with the vehicle number changed to 9001.
/// One stop where four doors count, plus the init/heartbeat/trip-start messages around it
/// and a NUL-byte line like the ones a power cut leaves behind.
/// </summary>
public static class UcpLogFixture
{
    public const int VehicleId = 9001;
    public const string FileName = "APC_9001.2022-08-02.csv";

    public static readonly string Content = string.Join('\n',
        "2022-08-02 00:22:17;0.000000 , 0.000000;0;0;0;+00:00;1;init;0;slave_pocet=4;;",
        "2022-08-02 00:22:18;0.000000 , 0.000000;0;0;0;+00:00;2;vozidlo;0;cislo=9001, vozovna=1, trakce=tramvaj, typ=\"Vario LF2R.E\";;",
        "2022-08-02 04:15:18;49.192127 , 16.573528;0;0;0;+00:00;5;status;41;alive=true, version=201913121247, init_img=201913121247, alive_sec=39, vhc_id=36947, statusReg=0x0;;",
        "2022-08-02 04:15:18;49.192127 , 16.573528;0;0;0;+00:00;5;status;42;alive=false, alive_sec=0;;",
        "2022-08-02 07:10:00;49.231468 , 16.576942;01200715;12;125601;+00:00;7;trasa;0;id=1200201, index=0, prvni_zast=941002 (04:35) Garaz ED Pisarky, posl_zast=125601 (04:55) Komarov;;",
        "2022-08-02 07:22:31;49.231468 , 16.576942;01200715;12;125601;-00:29;8;prijezd;0;zastavka=165902;;",
        "2022-08-02 07:22:31;49.231468 , 16.576942;01200715;12;125601;-00:29;10;start;41;in=0, out=0, zastavka=165902;;",
        "2022-08-02 07:22:31;49.231468 , 16.576942;01200715;12;125601;-00:29;10;start;42;in=0, out=0, zastavka=165902;;",
        "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0",
        "2022-08-02 07:23:11;49.231339 , 16.577011;01200715;12;125601;+00:06;11;stop;41;in=2, out=0, zastavka=165902;;",
        "2022-08-02 07:23:11;49.231339 , 16.577011;01200715;12;125601;+00:06;11;stop;42;in=1, out=1, zastavka=165902;;",
        "2022-08-02 07:23:11;49.231339 , 16.577011;01200715;12;125601;+00:06;15;cestujici;0;in=3, out=1, zastavka=165902, zast_nazev=Technologicky park, chyba=[42];2;2",
        "2022-08-02 07:26:45;49.190727 , 16.594667;01200715;12;155301;+00:00;120;zastavka - prujezd;0;manual, index=1, id=134302, n=\"Lipova\", zpozd=-15:41, geo=ano, rychlost=0, gps=[49.191177 , 16.577198];;");

    public static string WriteToNewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"ucp-fixture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, FileName), Content);
        return folder;
    }
}
