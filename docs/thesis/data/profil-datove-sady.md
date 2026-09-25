# Profil datové sady

Vygenerováno 2026-09-25 09:37 příkazem `AdaPlatform.Cli profile` z databáze platformy, verze kódu `fba53c0-dirty`.
Každý údaj má identifikátor (F1, F2, …) a uvádí, z jakých dat a jak byl spočten. V textu práce stačí odkázat na identifikátor, např. „(vlastní analýza, F4)“.

## Surové záznamy jednotek UCP (DPMB)

**Zdroj:** denní logy palubního počítače pro jednotky UCP-01/UCP-02 (`APC_<vůz>.<datum>.csv`) z archivu `APC_Logs.zip`. Otisk datové sady (SHA-256 přes seřazené otisky souborů, prvních 16 znaků): `b613365322371565`.

### F1 Rozsah datové sady

| Ukazatel | Hodnota |
| --- | --- |
| Období | 1. 8. 2022 – 7. 8. 2022 (7 dní) |
| Vozidla | 104 |
| Soubory (vůz × den) | 698 |
| Zprávy (neprázdné platné řádky) | 2 564 508 |
| Řádky souborů celkem, vč. prázdných | 2 568 059 |
| Objem | 271,7 MiB |
| Poškozené řádky | 17 v 14 souborech |

*Výpočet:* součty přes tabulku `SourceFiles`. Poškozený řádek = nemá 12 sloupců nebo neplatný čas; ve všech zjištěných případech jde o úseky bajtů NUL, typické pro výpadek napájení během zápisu.

### F2 Typy zpráv

| Kód | Typ | Počet | Podíl |
| --- | --- | ---: | ---: |
| 11 | CountingStopped | 813 625 | 31,7 % |
| 10 | CountingStarted | 785 072 | 30,6 % |
| 100 | DoorState | 362 932 | 14,2 % |
| 8 | Arrival | 145 679 | 5,7 % |
| 15 | StopSummary | 145 048 | 5,7 % |
| 9 | Departure | 136 494 | 5,3 % |
| 12 | DeviceRestart | 57 328 | 2,2 % |
| 5 | Heartbeat | 44 507 | 1,7 % |
| 110 | TripPhase | 29 135 | 1,1 % |
| 120 | StopPassed | 20 348 | 0,8 % |
| 7 | TripStart | 9 695 | 0,4 % |
| 200 | VehicleLayout | 3 569 | 0,1 % |
| 1 | Init | 3 542 | 0,1 % |
| 2 | VehicleInfo | 3 542 | 0,1 % |
| 3 | SystemShutdown | 3 466 | 0,1 % |
| 125 | StopChangedManually | 526 | 0,0 % |

*Výpočet:* počet řádků `DeviceEvents` podle kódu zprávy. Význam kódů byl odvozen z parseru aplikace ADA a ověřen na datech; veřejná specifikace formátu neexistuje.

### F3 Sčítací jednotky

V datech se ohlásilo **475 sčítacích jednotek** (zpravidla jedna nad každými dveřmi) na 101 vozidlech; 3 vozidla s logem neohlásila žádnou jednotku.

| Jednotek na vozidle | Vozidel |
| ---: | ---: |
| 2 | 2 |
| 3 | 32 |
| 4 | 30 |
| 5 | 12 |
| 6 | 12 |
| 7 | 2 |
| 9 | 1 |
| 10 | 10 |

| Verze firmware | Jednotek |
| --- | ---: |
| 201913121247 | 448 |
| 201823110921 | 27 |

*Výpočet:* tabulka `CountingDevices` — jednotka = dvojice (vůz, adresa zařízení) s alespoň jednou zprávou; firmware = poslední verze hlášená ve zprávě stavu (kód 5).

### F4 Zprávy o stavu jednotek (kód 5)

Ze 44 507 zpráv o stavu hlásí **11 762 (26,4 %) jednotku jako neživou** (`alive=false`).

| alive | statusReg | Počet |
| --- | --- | ---: |
| True | 0x0 | 31 356 |
| False | — | 11 762 |
| True | 0x2 | 1 389 |

*Výpočet:* `DeviceEvents` typu Heartbeat podle polí `alive` a `statusReg`. Význam bitů `statusReg` zatím není ověřen s výrobcem.

### F5 Restarty jednotek (kód 12)

Celkem **57 328 restartů**, zaznamenaných v 2 688 kombinacích jednotka × den (jednotky sčítaly v 2 243 kombinacích). Počet restartů jedné jednotky za den: medián 22, 95. percentil 39, maximum 152. Pro srovnání: v datech je 9 695 začátků jízd.

**Restart sám o sobě není porucha.** Restarty jsou rovnoměrně rozložené přes téměř všechny jednotky a typicky po sobě následují po 30–60 minutách, tedy zhruba s rytmem jízd; pravděpodobně jde o běžné nulování čítače. Jako signál poruchy mají smysl až odchylky od tohoto rytmu (např. maximum za den výše) — ověřit význam s výrobcem.

*Výpočet:* `DeviceEvents` typu DeviceRestart seskupené podle (vůz, jednotka, kalendářní den). Kombinace „jednotka sčítala“ = alespoň jedna zpráva kódu 11 v daném dni. Odstupy restartů: rozdíl časů po sobě jdoucích restartů téže jednotky (samostatný dotaz, viz poznámky k analýze).

### F6 Zastavení s příznakem chyby a záporná obsazenost

| Ukazatel | Počet | Podíl ze zastavení |
| --- | ---: | ---: |
| Zastavení celkem (zpráva kódu 15) | 145 048 | 100 % |
| s jednotkou označenou jako chybná (`chyba`) | 11 302 | 7,8 % |
| se zápornou obsazeností vozu po zastávce | 20 594 | 14,2 % |

*Výpočet:* `DeviceEvents` typu StopSummary; obsazenost = sloupec 10 logu (počet cestujících ve voze po zastávce, jak jej vede palubní počítač). Záporná hodnota je fyzikálně nemožná a signalizuje kumulovanou chybu sčítání.

### F7 Jednotky, které za celé období nenapočítaly nikoho

**4 z 435 jednotek**, které alespoň jednou dokončily sčítání na zastávce, mělo za celé období nulový součet nástupů i výstupů, přestože odesílaly zprávy o ukončení sčítání; u vozidla 2004 takto mlčely všechny jednotky.

*Výpočet:* součet počtů jednotky na zastávkách (rozdíl stavu čítače mezi zprávou kódu 10 a 11, viz F11) podle (vůz, jednotka).

### F8 Bilance nástupů a výstupů vozidla za den

Za den by počet nástupů a výstupů vozidla měl přibližně souhlasit (kdo nastoupí, také vystoupí). Pro 425 kombinací vůz × den s alespoň 100 napočítanými cestujícími je relativní nesoulad |nástupy − výstupy| / (nástupy + výstupy): medián 3,2 %, 90. percentil 10,2 %, maximum 39,3 %.

Směr nesouladu je celkově smíšený (nástupů více než výstupů má 35,5 % kombinací); podle trakce se ale liší:

| Trakce | Vůz × den | Medián nesouladu | Nástupů více než výstupů |
| --- | ---: | ---: | ---: |
| autobus | 123 | 3,3 % | 56,9 % |
| tramvaj | 211 | 4,2 % | 19,4 % |
| trolejbus | 91 | 1,8 % | 44,0 % |

*Výpočet:* součet počtů na zastávkách (rozdíl stavu čítače mezi zprávou kódu 10 a 11, viz F11) podle (vůz, kalendářní den); trakce z tabulky `Vehicles` (zpráva kódu 2). Nesoulad jednotlivých dveří není sám o sobě chybou (cestující mohou nastupovat jinými dveřmi, než vystupují); kontrola proto probíhá za celé vozidlo. U spřažených tramvají může část nesouladu vznikat tím, že cestující nastoupí do jednoho vozu a vystoupí z druhého — každý vůz má vlastní log.

### F11 Význam hodnot `in`/`out` ve zprávách jednotek

**Hodnoty `in`/`out` ve zprávách kódů 10, 11 a 12 jsou průběžné stavy čítače jednotky, nikoli počty za zastávku.** Počet cestujících na zastávce je rozdíl stavu při ukončení (11) a při zahájení (10) sčítání. Doklad z dat:

| Pozorování | Počet | Podíl |
| --- | ---: | ---: |
| Zahájení (10), které opakuje stav z předchozího ukončení (11) | 732 260 z 785 072 | 93,3 % |
| Ukončení (11) přímo po zahájení, se stavem ≥ stavu při zahájení | 780 287 z 780 287 | 100,0 % |
| Ukončení bez předchozího zahájení (např. po restartu) — počet nelze určit | 33 338 z 813 625 | 4,1 % |
| Souhrn vozu (15) = součet **rozdílů** přes jednotky | 100 957 z 133 794 | 75,5 % |
| Souhrn vozu (15) = součet **stavů** při ukončení | 8 388 z 133 794 | 6,3 % |

*Výpočet:* zprávy 10/11/12 každé jednotky seřazené podle času (`DoorStopPairing`); souhrn vozu porovnán se součtem přes jednotky ukončené ve stejném okamžiku. Důsledek: prostý součet hodnot ze zpráv 11 počet cestujících mnohonásobně nadhodnocuje; tento report i platforma proto pracují s rozdílem.

### F9 Poloha GPS

72 430 z 2 564 508 zpráv (2,8 %) nemá platnou polohu GPS (log zapisuje `0.000000 , 0.000000`).

*Výpočet:* `DeviceEvents` s prázdnou zeměpisnou šířkou.

## Databáze aplikace ADA (legacy)

**Zdroj:** databáze `ADA.dbFile` stávající desktopové aplikace ADA (SQLite), importovaná příkazem `import-ada`. Obsahuje již zpracovaná data — jízdy a zastavení se součty za celé vozidlo, bez údajů jednotlivých dveří a bez surových zpráv.

### F10 Rozsah a kvalita dat ADA

| Ukazatel | Hodnota |
| --- | --- |
| Období | 30. 9. 2024 – 31. 10. 2024 |
| Vozidla | 5 |
| Jízdy | 2 374 |
| Zastavení | 40 931 |
| Jízdy označené jako nevalidní | 111 (4,7 %); záznamů o chybě jednotky: 111 |
| Jízdy, kde obsazenost klesne pod nulu | 1 225 (51,6 %), minimum -88 |
| Vozidla bez vyplněné kapacity | 5 z 5 |

*Výpočet:* tabulky `Trips`, `StopVisits`, `DeviceFaults` a `Vehicles` omezené na záznamy bez vazby na surový soubor (`SourceFileId` je prázdné) — tedy importované z ADA. Bez kapacity vozidla nelze spočítat vytíženost v procentech.

## Soubory s daty pro grafy

- `f2-typy-zprav.csv`
- `f3-jednotky-na-vozidle.csv`
- `f5-restarty-jednotka-den.csv`
- `f7-soucty-jednotek.csv`
- `f8-bilance-vuz-den.csv`
