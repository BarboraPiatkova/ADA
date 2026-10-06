# ADR 0006: Different counting units per customer, one data model

## Status
Proposed. It is needed before the second operator, MDML (Městská doprava Mariánské Lázně),
whose vehicles will count with iris IRMA MATRIX sensors.

## Context

### The counting units
DPMB's vehicles count with Herman UCP-01/UCP-02 units. MDML's vehicles will use
**iris IRMA MATRIX**: a 3D time-of-flight sensor (500-pixel matrix) over each door. It connects
over **Ethernet** (iris API, VDV 301 / IBIS-IP, or direct UDP) or **CAN** (iris API), and has a
gateway to IBIS and J1708. Its newer sibling IRMA 6 adds ITxPT certification. Other operators may
bring other sensors. One fleet can also mix them, e.g. UCP on trams and IRMA on new buses.

### MDML
From the project kick-off ("Modernizace prvků telematiky MDML"):
- **Passenger counting is part of the delivery:** "LCD in vehicles + APC (IRMA)" and "software for
  evaluation". The central systems include a block "Zpracování APC" (APC processing), next to the
  Herman back office, the Herman dispatch, timetable preparation (EPCOMP) and camera processing.
  That block is this platform.
- **Fleet:** about 12 vehicles: 8 Škoda 30Tr trolleybuses, 2 SOR NB12 buses and 2 SOR electric
  buses (delivered 05/2026); one Rošero bus is retired during 2027.
- **In the vehicle:** one sensor over each door, on the vehicle's Ethernet network, connected to the
  Herman on-board computer (EPC with EPIS). A 3-door trolleybus has 3 sensors.
- **MDML runs its central systems itself.** That matches one deployment per operator inside its
  own network (ADR 0002).
- **Reference data:** the timetable (stops, patterns, lines) comes from EPCOMP. Its export and the
  vehicle's `stations.xml` / `vehicles.xml` exist for MDML.
- **No counts yet:** the logs downloaded from MDML vehicles (July 2026) show no counting unit
  configured (`APC - konfigurace; typ=n_a, pocet=0`). They do show complete trip data: trips,
  door opening and closing at stops with delay and GPS, and passes.

### The on-board computer: EPIS
EPIS is the Herman on-board computer application (Java, version 5.33 at MDML). Its source, shipped
inside the application package, settles how counts reach the platform:
- **EPIS writes the APC log.** `ApcLogFileFormatter` writes `APC_<vehicle>.<date>.csv` in exactly the
  12-column format of the DPMB logs (time; GPS; block; line; destination; delay; code; name; device
  address; message; passengers on board; change). `ApcLogAction` defines the same codes
  `UcpLogParser` reads, plus a few it doesn't map yet: 13 session, 50 communication error, 52/53
  camera errors. So the DPMB logs were written by EPIS, not by the UCP units.
- **The log is the same for every supported sensor.** `EApcProvider` lists Herman (UCP), EyeOne,
  Abirail Dilax, Abirail Vivotek and **IRMA MATRIX**. The logging and counting logic sits above the
  provider.
- **EPIS itself computes the counts.** A stop's count is the sum of the stop readings minus the sum
  of the start readings over all doors (`RoutePointPassengerCount`). That is the vehicle summary
  (code 15), and why the per-door difference is the correct reading (report F11). A restart between
  start and stop makes that difference negative: the broken terminus summaries seen at quick
  turnarounds.
- **Counter restarts are rules, not faults.** EPIS restarts the counters when a trip's route phase
  begins, except after a driver change during a trip (`ApcCounterRestartMonitor`). That matches the
  DPMB data (report F5) and the reconstruction rules.
- EPIS also writes its own audit log (`Audit_<vehicle>.<date>.csv`, trip and door events, device
  status). It is a different format and carries no counts.

### What still differs between sensors
1. **The meaning of a reading** for a provider other than UCP: EPIS logs every provider as running
   readings at counting start and stop (codes 10/11). Whether an IRMA MATRIX reading behaves exactly
   like a UCP one (monotonic, reset only by a restart) has to be checked on the first real log.
2. **Health signals.** Heartbeats, restarts and error flags mean different things on each device. A
   UCP `alive=false` has no IRMA equivalent, and IRMA has camera errors (52/53) that UCP doesn't.
3. **Foreign on-board computers.** An operator without EPIS would deliver a different log, or none.

## Decision
One pipeline, one derived model. The on-board computer's log is the only input. Adapters exist only
where something actually differs.

```
EPIS APC log (any sensor) ─┐
other on-board log ────────┴─► [1] format parser ──► raw layer (SourceFiles, DeviceEvents; never modified)
                                                         │
                         ┌───────────────────────────────┴──────────────────────┐
                         ▼                                                      ▼
          [2] trip context (from the on-board computer)     [3] door cycles (per counting semantics):
              trip starts, stop visits, passes                   device, started, stopped, in, out,
                         │                                       flags
                         └──────────────► [4] trip assembly ◄───────────────────┘
                                              (vendor-neutral)
                                                   ▼
                          derived layer: Trips, StopVisits, DoorCounts (Measured / Imputed)
                                                   ▼
                   [5] quality rules, per device type ──► reports, API, UI (the same for every customer)
```

1. **Format parser, one per log format.** EPIS vehicles, whatever the sensor, use today's parser
   (`UcpLogParser` is really the EPIS APC log parser). A foreign on-board computer gets its own
   parser mapping onto the same `DeviceEventType` vocabulary.
2. **Trip context.** Trips, the late-terminus rule, stop visits and passes, as in today's
   `UcpTripReconstructor`. It depends on the on-board computer only.
3. **Door cycles, one extractor per counting semantics.** For EPIS logs this is today's
   `DoorStopPairing` (stop − start, repeated readings, restarts), for every EPIS provider unless the
   first IRMA log shows otherwise.
4. **Trip assembly, vendor-neutral.** It attaches each door cycle to the visit where it started, sums
   the doors and decides validity.
5. **Quality rules per device type.** `CountingDevice` gets a device type (the EPIS provider: herman,
   eyeone, abi_dilax, abi_vivotek, irma). Health thresholds and rules are chosen per type.

**Configuration, not code, per deployment (ADR 0002):**
- which log formats the deployment accepts, and where it picks them up;
- the device type per vehicle, from the EPIS APC configuration or a vehicle list;
- rule thresholds per device type.

## Output
The same for every customer, whatever the sensors:
- **Derived data:** trips, stop visits and door counts, each with its origin (measured / manual /
  imputed) and validity. Each keeps its provenance: source file, format, device type.
- **Quality:** device and vehicle health with reasons, judged by the rules for that device type.
- **Reports and API:** the same endpoints, screens and thesis figures. A figure can be split by
  device type, so UCP and IRMA accuracy can be compared (useful for VO2).
- **Anomaly report per reconstruction run:** what couldn't be reconstructed and why, for each format.

## Consequences
- **For MDML, no new parser is expected:** the EPIS APC log from IRMA vehicles goes through the
  existing parser and reconstruction. The new parts are a device type on `CountingDevice`, an IRMA
  rule profile, and mappings for codes 13, 50, 52 and 53.
- Steps 2 and 3 stay in one class until a second counting semantics or a foreign log format actually
  appears. The split then happens with that data in hand, not guessed.
- Tests run per format and device type, on real log excerpts, as `UcpTripReconstructorTests` does.
- Open until the first MDML log with IRMA connected:
  - whether IRMA readings in codes 10/11 behave like UCP's (running, monotonic, reset by a restart);
  - which status and error messages IRMA produces, for its rule profile;
  - how the EPIS log files reach the platform at MDML (the collection path from the vehicles).

## Alternatives considered
- **Import from iris's cloud** instead of the on-board log. It gives only the counts, without the
  on-board computer's trip context, and the data would leave the operator's network (against
  ADR 0002).
- **Talk to the sensors directly** (VDV 301 or UDP from the platform). This bypasses the on-board
  computer, which already owns the vehicle's network, the trip context and the counting logic. The
  platform would also need a live link into every vehicle.
- **A separate pipeline per vendor, up to the derived tables.** It duplicates the trip logic, and
  the vendors' results would drift apart. With EPIS normalising the log, there is also nothing to
  gain.
- **Parse the EPIS audit log instead of the APC log.** It has the trip context but no counts. It is
  useful only for vehicles without counting units, e.g. to check timetable adherence.
