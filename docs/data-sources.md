# Data sources: what we know

How counting data reaches the platform, what each source contains, and what we found by reading
the data and the code that produces it. The design decisions built on this are in
[ADR 0006](adr/0006-counting-units-per-customer.md); the figures for the thesis are in the
[data report](thesis/data/profil-datove-sady.md).

## The chain

```
sensor over each door ──► on-board computer (EPIS) ──► APC log per vehicle and day ──► platform
(UCP, IRMA MATRIX, …)      counts per stop, trip context   APC_<vehicle>.<yyyy-MM-dd>.csv      import → reconstruction
```

The platform never talks to a sensor. Everything it knows comes from the on-board computer's log.

## EPIS, the on-board computer

EPIS is Herman's on-board computer application (Java; version 5.33 at MDML). It controls the
vehicle's devices (ticket validator, displays, radio link to dispatch, counting units) and writes two
logs per vehicle and day:

| Log | File | Content |
|---|---|---|
| **APC log** | `APC_<vehicle>.<date>.csv` | counting and trip context; **the platform's input** |
| Audit log | `Audit_<vehicle>.<date>.csv` | trips, door opening/closing, passes, device status; **no counts** |

Findings from the EPIS source (it ships inside the application package, `Epis-*.jar`):

- **The APC log format is EPIS's.** `ApcLogFileFormatter` writes the 12 columns `UcpLogParser` reads:
  time; GPS; block; line; destination; delay; code; name; device address; message; passengers on
  board; change. The DPMB logs were written by EPIS, not by the UCP units.
- **The same log for every sensor.** `EApcProvider`: `herman` (UCP), `eyeone`, `abi_dilax`,
  `abi_vivotek`, `irma` (IRMA MATRIX). Logging and counting sit above the provider. The audit log
  shows the provider at start-up: `APC - konfigurace; typ=<provider>, pocet=<number of units>`.
- **Codes** (`ApcLogAction`):

  | Code | Name | Used by the platform |
  |---|---|---|
  | 1 / 2 / 3 | init / vozidlo / vypnuti systemu | vehicle info; trip boundaries |
  | 5 | status | heartbeat (`alive`, firmware, status register) |
  | 7 | trasa | trip start: pattern, index, planned first and last stop |
  | 8 / 9 | prijezd / odjezd | stop visit |
  | 10 / 11 | start / stop | counter reading at counting start / stop, per device |
  | 12 | restart | counter reset |
  | 13 | relace | *not mapped yet* |
  | 15 | cestujici | vehicle summary per stop, flagged devices (`chyba`) |
  | 50 | chyba komunikace | *not mapped yet* (communication error) |
  | 52 / 53 | kamera-chyba 2 / 3 | *not mapped yet* (camera exposure errors) |
  | 100 | dvere | door state |
  | 110 | trasa - faze | trip phase |
  | 120 | zastavka - prujezd | stop passed |
  | 125 | zastavka - man.zmena | stop changed by the driver |
  | 200 | vozidlo - layout | coupled vehicles |

- **Counts are differences of running readings.** EPIS takes each device's reading when counting
  starts and when it stops. The stop's count is the sum of stop readings minus the sum of start
  readings (`RoutePointPassengerCount`), and that is the summary (code 15). The platform does the same
  per door (report F11).
- **Restarts follow a rule.** Counters restart when a trip's route phase begins, except after a driver
  change during a trip (`ApcCounterRestartMonitor`). A restart between a stop's start and stop
  readings loses that stop's counts, and the summary goes negative. This is the main cause of invalid
  trips at quick terminus turnarounds.

## What reconstruction does with it

`UcpTripReconstructor` (rules documented on the class):

- A trip opens at code 7 and is under way once it departs its first stop.
- Late terminus events still carry the previous trip's destination (column 4) and go to that trip.
- A door count is the stop reading minus the start reading. It belongs to the visit where the door
  **started** counting, because at a terminus the stop readings already carry the next trip's stop.
- A repeated stop reading adds late passengers.
- A trip is invalid when a device is flagged, not alive, or lost a count.

Result on the DPMB week (698 files): 9,216 trips, 178,247 stop visits, 766,653 door counts; 24 % of
trips invalid.

## IRMA MATRIX, MDML's sensor

From iris's public documents: the [product page](https://www.iris-sensing.com/products/irma-matrix/),
the [data sheet](https://www.iris-sensing.com/fileadmin/user_upload/support/IRMA_MATRIX/Produktdatenblatt/IRMA-MATRIX_R2_ProductDataSheet_4-1_en.pdf)
(R2, rev. 4.1.1, 2024) and the [product sheet](https://www.iris-sensing.com/fileadmin/user_upload/support/IRMA_MATRIX/Produktblatt/24_iris_IRMA_Matrix_en.pdf).
The data sheet may not be passed on to third parties, so it is summarised and linked here, not copied.

- 3D time-of-flight sensor with 500 pixels, mounted above the door. An integrated signal processor
  counts on the sensor itself, detecting the direction of motion and passengers' stature (height
  classes).
- Works in complete darkness (own infrared light). Normally one sensor per standard door: at 2 m
  mounting height it covers a door up to 1.2 m wide (1.8 m with two sensors).
- Counting is switched on and off either by a door contact magnet or **by the on-board computer**
  over Ethernet or CAN.
- It communicates with the on-board computer via **UIP 2.0** (Universal IRMA Protocol), with an API
  library from iris. Other options: VDV 301 over Ethernet, direct UDP, or a gateway to IBIS and J1708.
- The public documents give no accuracy figure and no protocol description. The UIP specification
  has to come from iris (through Herman).

## Datasets on hand

| Dataset | Operator | Contains | Counts |
|---|---|---|---|
| `APC_Logs.zip` (1.–7. 8. 2022) | DPMB, Brno | EPIS APC logs, 104 vehicles | yes (UCP) |
| `ADA.dbFile` (30. 9. – 31. 10. 2024) | DPMJ, Jihlava | legacy ADA database, 5 vehicles, processed trips | totals only |
| MDML vehicle download (7/2026) | MDML, Mariánské Lázně | EPIS audit logs, 24 vehicle-days; vehicle data package | **no counting unit configured yet** |
| Split, IDS JMK, DSZO (various) | other operators | EPIS audit logs; Split has 3 counting units per vehicle | APC logs not collected |
| Transportella statistics table dump (6. 10. 2025 – 29. 9. 2026) | DPMO, Olomouc | 9,183,667 stop records (`DBStatistics`: one row per vehicle and stop), 151 vehicles, 429 stops; planned and actual arrival/departure, delay, line, duty, trip | **none**: APC, card, distance, temperature and GPS columns are all zero |
| Transportella `StaLineCourse_05092026…xlsx` (5.–6. 9. 2026) | POVED (Plzeň region): registration plates `8P…`, buses and trains, 2,374 stops | Transportella statistics per trip: one sheet per trip, per stop planned/actual times, dwell, deviation, **APC boarded/alighted/total, vehicle utilisation, tickets per APC**, vehicle and type; 1,649 trips, 24,937 stop rows | APC in 9,506 stop rows |
| Transportella `StaLineCourse_11032026…xlsx` (11. 3. 2026) | to confirm | same report, different sheet layout (not parsed yet) | ? |

**How Transportella's data reaches the platform is still open.** The platform may run on the same
server as Transportella with read access to its statistics database, receive a dump like DPMO's, or
only get the XLSX report. All three are supported (README, "Transportella operations").

**Joining stops needs no mapping table.** Every system numbers stops from the operator's EPComp
timetable: the post code is station × 100 + post. EPComp's `stations.xml` uses it (MDML `101` =
station 1, post 01), EPIS writes it in the APC log (DPMB `110802`), and Transportella stores station
and post separately (POVED `14038` + `01` → `1403801`). Where a source leaves the post out (DPMO:
always 0), calls match at station level (post code / 100). EPComp's stop list is the mapping table if
one is ever needed.

Imported for development: DPMO September 2026 (796,900 calls, 1 min 41 s for the whole 2.1 GB
dump), POVED's report of 5.–6. 9. 2026 (26,037 calls: buses, trains). Trains use 7-digit railway stop
codes and their sheets start with "Detail vlaku:" instead of "Detail spoje:".

The DPMO dump is tab-separated rows without a header. The columns follow the order of Transportella's
`Statistics` constructor (`DBStatistics.GetStatistics`, `C:\Projects\transportella`): ID, trip start,
vehicle, driver, speed, line|duty, trip, delay, stop, post, longitude, latitude, planned arrival and
departure, actual arrival and departure, APC in/out/on board, distances, stopped flag, predicted
time, temperature, electricity, ride type, tariff, stop name, stop type, four ticketing counts,
traction. The driver column is personal data: the platform doesn't need it and shouldn't import it.
Dwell times: 23 % under 10 s, 65 % 10–30 s, 9 % 30–60 s, 2 % 1–2 min, 7,625 stops over 5 min.
Departures: 89.8 % within −1…+3 min of the timetable, 2.1 % more than 1 min early, 3.0 % more
than 5 min late.

The newer Transportella statistics export has APC and utilisation columns, which the verified
DPMB-era export did not. Its cells carry an empty `r=""` attribute, so the parser has to read cells
by position.

MDML's kick-off presentation: about 12 vehicles (8 Škoda 30Tr trolleybuses, 2 SOR NB12, 2 SOR
electric buses), one IRMA MATRIX per door, Herman EPC/EPIS on board. MDML runs its central systems
itself, and "Zpracování APC" (APC processing) in the central systems is this platform.

**Which data the thesis uses (decided 2026-09-29):** most likely **MDML's**, once its vehicles count
with IRMA MATRIX. DPMB's week may not appear in the thesis; it stays the development and test data
until MDML's logs exist.

### MDML fleet and capacities

From the vehicle list in MDML's EPIS data package (`vehicles.xml`, July 2026). Capacities are the
manufacturers' figures per type. They vary with seats and door layout, so the per-vehicle values
from MDML (registration papers) replace them when available.

| Type | Vehicles | In the IRMA project | Seated + standing (by type) |
|---|---|---|---|
| Škoda 30Tr (SOR 30 TR) | 58–65 (8) | yes | 32 + 62 = 94 in the 3-door version; up to 104 total by configuration ([Wikipedia](https://en.wikipedia.org/wiki/%C5%A0koda_30Tr_SOR), [Škoda](https://www.skodagroup.com/cs/reference/trolejbus-30tr)) |
| SOR NB 12 | 38, 39 | yes | 26 + 76 in the 4-door version; 88 in others ([trucker.cz](https://www.trucker.cz/rubriky/bus/sor-nb-12-city-moderni-vzhled-i-vybaveni_38436.html), [DPP](https://www.dpp.cz/vozovy-park/autobusy-a-elektrobusy/sor-nb12)) |
| SOR electric bus | not in the list yet (delivered 05/2026) | yes | to find out |
| Škoda 24Tr (incl. -DA, -ZO, prototype) | 51–57 (7) | not in the presentation's scope | 30 + 69 ([Wikipedia](https://en.wikipedia.org/wiki/%C5%A0koda_24Tr_Irisbus)) |
| Irisbus Citelis, Irisbus 3P5, Citybus, Rošero | 34, 36, 37, 40, 41 | no (Rošero retired 2027) | — |

The platform already stores `SeatingCapacity` and `StandingCapacity` per vehicle (null = unknown),
so occupancy as a share of capacity only needs these values filled in.

### Where vehicle data comes from: the fleet register

Vehicles are expected to come from **Atlas**, Herman's register of vehicles and on-board devices. The
platform reads them through a switchable adapter (`IFleetSource`, `Fleet:Source`; see the README).
The same sync also accepts EPIS's `vehicles.xml` or a CSV, and a new source is one more class.

What Atlas offers today: `GET /api/Integrations/Transportella/VehiclesTraction`, a full snapshot of
vehicle numbers and traction (the ordinal of the shared `TractionType`), behind a shared-secret header
and the `TransportellaSync` feature flag. Atlas's `Vehicle` has **no type and no capacity**. To take
them from Atlas, Atlas needs an endpoint returning `code`, `traction`, `type` (or `model`), `depot`,
`seatingCapacity` and `standingCapacity`. The adapter already reads those fields, so switching is only
a change of `Fleet:Atlas:VehiclesPath`.

## EPComp, the timetable

EPComp is Herman's timetable tool (desktop, `C:\Projects\EPComp2`). It imports operators' timetables
and exports:

- **the vehicle data package for EPIS:** stops, lines, patterns (trace scenarios), trips and duties;
  MDML's is in the vehicle download;
- **the dispatch database:** stops with WGS84 and S-JTSK coordinates and tariff zones; duties with
  their trips and departure times; day validity.

ADA's `Stations` table has the same columns as EPComp's stop export, so ADA's reference data most
likely came from EPComp.

**Does the platform need a connection to EPComp? Not for reconstruction.** The APC log carries the
trip context, and the stops and patterns it reveals are added automatically. **It does need EPComp
data, as a read-only file import, for three things:**

1. **Planned trips**, to know which trips should have run and have no measurement: the target set for
   gap-filling (VO1) and a data-coverage figure.
2. **Planned times per stop**, for punctuality against the timetable.
3. **The complete network**: stop positions, names and tariff zones, and patterns that no measured
   trip has covered.

The export file is preferred to a live database link: it keeps the platform independent of EPComp's
schema, and it is the same package the vehicles receive.

## Getting the MDML logs: what to ask for

- The **`APC_<vehicle>.<date>.csv`** files once IRMA is connected and configured, together with the
  `Audit_*` files for the same days. In the IDS JMK download they sit in the same folder as the audit
  logs; in the MDML July download there were none.
- At least **one to two weeks, every vehicle**, including a day with a driver change and days with the
  trolleybuses and the buses.
- The EPIS APC configuration per vehicle: provider and number of units (the audit log shows it).
- The current EPComp export for MDML (the vehicle data package).
- A **Transportella export** from MDML's new Transportella installation (timetable statistics:
  planned and actual times, delays). Check whether it has occupancy, kilometres or energy, which the
  verified DPMB-era export didn't.
- **Seated and standing capacity per vehicle** (from the registration papers), since it varies within
  a type.
- Leave out anything with network or connection details. The platform doesn't need them.

## Next steps

1. **When the MDML logs arrive:** import them with `import-ucp`, check that IRMA readings behave like
   UCP's (running, monotonic, reset only by a restart), and add a reconstruction test from real MDML
   lines.
2. **Map codes 13, 50, 52 and 53** in the parser, and add a device type (EPIS provider) to
   `CountingDevice` with a health-rule profile per type.
3. **EPComp import:** planned trips, stops and patterns from the export file.
4. **Fault detection (VO2)** and the **gap-filling experiment (VO1)** on the reconstructed DPMB trips.
5. **Ask iris (through Herman) for the UIP 2.0 description**, for the thesis's description of the
   sensor, and for any accuracy statement.
