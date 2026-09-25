# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

- **Data and quality analysts at a public transport operator** (first: DPMB, Brno). They review the counting data from the vehicles' passenger counting units (APC) to find out which vehicles and devices can be trusted, where data is missing, and whether occupancy figures are fit to use.
- **Planners and management at the same operator.** They use the network map and passenger figures for network and timetable decisions, and care less about device detail.

## Product Purpose

AdaPlatform automates what the desktop tool ADA did by hand: importing, checking and evaluating APC data, then showing the results on the web to many users of one operator. Success means an operator sees, without manual work, which counting devices are faulty, how much of the data can be trusted, and what the trustworthy data says about passengers on the network.

It is also the implementation part of a diploma thesis (FI MU): comparing methods for filling in missing counts, and detecting faulty counting devices from their data.

## Positioning

AdaPlatform works from the counting units' own raw, unmodified event logs (Herman UCP-01/UCP-02), not from exported summaries. That lets it judge the data itself (silent devices, flagged stops, impossible occupancy, gaps) before it shows any figure. Power BI dashboards, the old ADA desktop tool and Transportella's statistics exports start from already-processed numbers and cannot make that claim.

## Operating Context

- Used mainly on an office desktop with a large screen, in analysis sessions; dense tables and charts suit it. Occasionally used on a phone or tablet for a quick check away from the desk.
- Czech first, English second (Herman colleagues, foreign operators, thesis examiners).
- One deployment per operator, inside the operator's own network (on-prem); PostgreSQL or SQL Server, whichever the operator runs.
- Users sign in with their Tokari account, the same one they use for Herman's other applications; what they see depends on their permissions.
- Data arrives as log files from the vehicles (one file per vehicle and day), imported in bulk.

## Capabilities and Constraints

- Today: raw log import, per-stop passenger counts per door (counter readings are running totals; per stop = stop reading − start reading), device and vehicle health with reasons, a fleet × day view, the network map with stops and line routes, cross-filtering between charts and the vehicle table.
- Terms users know: vůz (vehicle), sčítací jednotka (counting device), zastávka, linka, trasa, nástupy/výstupy, obsazenost, spoj.
- Health thresholds are provisional; calibrating them is part of the thesis and they are configurable per deployment.
- Undecided: the source of occupancy %, kilometres and energy (the verified Transportella export has none of them); vehicle capacities are not available yet, so occupancy can't be shown as a share of capacity.
- Planned, not built: trip reconstruction, gap-filling comparison, Transportella delay data, the stop × trip matrix, exports.

## Brand Commitments

- Name: **AdaPlatform**. It continues ADA and belongs to Herman's family of transport products (Transportella, Tokari).

## Evidence on Hand

- One real week of DPMB data (1.–7. 8. 2022): 104 vehicles, 475 counting devices, 2.56 million raw events. Permission to use it in the thesis is still pending.
- A data report with every figure and its derivation: `docs/thesis/data/profil-datove-sady.md` (figures F1–F11) plus CSVs.
- No customer testimonials, adoption figures, benchmarks or case studies exist. Do not invent any.

## Product Principles

1. **Trust before numbers.** Show how reliable the data is before, or next to, what it says; never present a figure as clean when the device behind it is suspect.
2. **Explain every verdict.** A status always comes with its reason and the measured value, in the user's language, so an analyst can check it.
3. **Work from the raw record.** Keep the device's own data unmodified; everything shown is derived from it and can be recomputed.
4. **Fit the operator's world.** Their network, their database, their accounts, their language; nothing leaves their network.
5. **Dense but calm.** Analysts work with a lot of data at once; the interface lets them scan, filter and drill down without noise.

## Accessibility & Inclusion

WCAG 2.2 AA: keyboard access to every control including chart marks, 24 px pointer targets (44 px on touch), status never shown by colour alone (icon + word + colour), readable contrast in light and dark themes, reduced motion respected.
