# Data the platform imports

Only the files AdaPlatform actually reads, one folder per operator. They are not in git (`.gitignore`
keeps `data/` out, except this file): some hold personal data (driver columns in the Transportella
files, never read by the platform) and they are large. Keep the folder off cloud sync.

Every import recognises a file by its content, so moving or re-importing never duplicates data.

| Operator | File | Period | What it is | Import |
|---|---|---|---|---|
| **DPMB** Brno | `DPMB/2022-08_epis-apc-logs/APC_Logs.zip` | 1.–7. 8. 2022 | EPIS APC logs (`APC_<vehicle>.<date>.csv`) of 104 vehicles with UCP counting units | `import-ucp` → trips, stop visits, door counts; every screen |
| **DPMB** Brno | `DPMB/2025-05_epcomp/stations.xml` | export 21. 5. 2025 | EPComp's stop list: code, name with diacritics, WGS84 and S-JTSK | `import-stations` → stop names |
| **DPMJ** Jihlava | `DPMJ/2024-10_ada/ADA.dbFile` | 30. 9. – 31. 10. 2024 | legacy ADA database, 5 vehicles, processed trips (totals only) | `import-ada` → stops, lines, patterns (empty database only) |
| **DPMO** Olomouc | `DPMO/2025-10_2026-09_transportella-dump/transportella_stat_statistics_2026-09-29.sql` | 6. 10. 2025 – 29. 9. 2026 | Transportella `Stat.Statistics` table dump: one row per vehicle and stop, planned and actual times; no APC | `import-transportella` → recorded calls (September 2026 imported) |
| **POVED** (Plzeň region) | `POVED/2026-09_transportella-report/StaLineCourse_05092026-000000_06092026-000000.xlsx` | 5.–6. 9. 2026 | Transportella per-trip report (buses and trains) with APC boardings and alightings | `import-transportella` → recorded calls |

```bash
dotnet run -c Release --project tools/AdaPlatform.Cli -- import-ada --source data/DPMJ/2024-10_ada/ADA.dbFile
dotnet run -c Release --project tools/AdaPlatform.Cli -- import-ucp --source data/DPMB/2022-08_epis-apc-logs/APC_Logs.zip
dotnet run -c Release --project tools/AdaPlatform.Cli -- import-stations --source data/DPMB/2025-05_epcomp/stations.xml
dotnet run -c Release --project tools/AdaPlatform.Cli -- import-transportella --source data/DPMO/2025-10_2026-09_transportella-dump/transportella_stat_statistics_2026-09-29.sql --from 2026-09-01 --to 2026-09-30
dotnet run -c Release --project tools/AdaPlatform.Cli -- import-transportella --source data/POVED/2026-09_transportella-report/StaLineCourse_05092026-000000_06092026-000000.xlsx
```

Kept outside, not used yet (on the owner's Desktop, `AdaPlatform-nepouzita-data`): MDML's EPIS vehicle
download of 31. 7. 2026 (no counting unit configured yet), IDS JMK's daily service and punctuality
reports of 20.–27. 9. 2026 (`OneDayTraffic`), a Transportella report of 11. 3. 2026 in a layout not
parsed yet, the legacy ADA program files and an older DPMB ADA package (14. 9. 2022).
