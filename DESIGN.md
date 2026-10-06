---
name: AdaPlatform
description: Quality and passenger data for public transport operators, set like a timetable sheet.
colors:
  route-petrol: "#0b6e6e"
  route-petrol-strong: "#075656"
  route-petrol-soft: "#e0efee"
  on-route: "#ffffff"
  timetable-ink: "#1b2430"
  secondary-ink: "#566170"
  paper: "#ffffff"
  surface: "#f3f5f5"
  surface-raised: "#e7ecec"
  rule: "#d4dbdb"
  fault: "#b42318"
  fault-soft: "#fce9e7"
  warning: "#945400"
  warning-soft: "#fdf0d9"
  ok: "#1d7a46"
  ok-soft: "#e4f3e9"
  unknown: "#566170"
  unknown-soft: "#edf0f2"
  seq-1: "#c1f7f6"
  seq-2: "#77dcdb"
  seq-3: "#25bcbc"
  seq-4: "#009999"
  seq-5: "#007575"
  seq-6: "#1d5151"
  series-1: "#009090"
  gridline: "#e6eaea"
  axis: "#c3cccc"
  map-route: "#0b6e6e"
  map-casing: "#ffffff"
  map-stop: "#ffffff"
  map-no-data: "#7a8591"
typography:
  headline:
    fontFamily: "Barlow Semi Condensed, Barlow, system-ui, sans-serif"
    fontSize: "26px"
    fontWeight: 600
    lineHeight: 1.2
  title:
    fontFamily: "Barlow Semi Condensed, Barlow, system-ui, sans-serif"
    fontSize: "20px"
    fontWeight: 600
    lineHeight: 1.2
  subtitle:
    fontFamily: "Barlow Semi Condensed, Barlow, system-ui, sans-serif"
    fontSize: "17px"
    fontWeight: 600
    lineHeight: 1.2
  body:
    fontFamily: "Barlow, system-ui, sans-serif"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: 1.5
    fontFeature: "tnum"
  label:
    fontFamily: "Barlow Semi Condensed, Barlow, system-ui, sans-serif"
    fontSize: "13px"
    fontWeight: 600
    lineHeight: 1.2
  caption:
    fontFamily: "Barlow, system-ui, sans-serif"
    fontSize: "12px"
    fontWeight: 400
    lineHeight: 1.5
    fontFeature: "tnum"
  vehicle-number:
    fontFamily: "Barlow Semi Condensed, Barlow, system-ui, sans-serif"
    fontSize: "17px"
    fontWeight: 700
    lineHeight: 1.1
rounded:
  sm: "3px"
  md: "6px"
  lg: "8px"
  card: "10px"
  popover: "12px"
  pill: "9999px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "24px"
  gutter: "28px"
components:
  button-primary:
    backgroundColor: "{colors.route-petrol}"
    textColor: "{colors.on-route}"
    typography: "{typography.label}"
    rounded: "{rounded.lg}"
    height: "40px"
  button-primary-hover:
    backgroundColor: "{colors.route-petrol-strong}"
    textColor: "{colors.on-route}"
  input-field:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.timetable-ink}"
    rounded: "{rounded.lg}"
    padding: "0 12px"
    height: "40px"
  select:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.timetable-ink}"
    rounded: "{rounded.lg}"
    padding: "0 10px"
    height: "32px"
  segmented-item:
    textColor: "{colors.secondary-ink}"
    typography: "{typography.label}"
    rounded: "{rounded.md}"
    padding: "0 9px"
    height: "26px"
  segmented-item-selected:
    backgroundColor: "{colors.route-petrol}"
    textColor: "{colors.on-route}"
  pagination-page:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.timetable-ink}"
    rounded: "{rounded.lg}"
    height: "32px"
    width: "32px"
  pagination-page-current:
    backgroundColor: "{colors.route-petrol}"
    textColor: "{colors.on-route}"
  status-pill-fault:
    backgroundColor: "{colors.fault-soft}"
    textColor: "{colors.fault}"
    typography: "{typography.label}"
    rounded: "{rounded.pill}"
    padding: "2px 9px 2px 6px"
  status-pill-warning:
    backgroundColor: "{colors.warning-soft}"
    textColor: "{colors.warning}"
  status-pill-ok:
    backgroundColor: "{colors.ok-soft}"
    textColor: "{colors.ok}"
  status-pill-unknown:
    backgroundColor: "{colors.unknown-soft}"
    textColor: "{colors.unknown}"
  filter-chip:
    backgroundColor: "{colors.route-petrol-soft}"
    textColor: "{colors.timetable-ink}"
    rounded: "{rounded.pill}"
    padding: "0 4px 0 10px"
    height: "30px"
  chart-card:
    backgroundColor: "{colors.paper}"
    rounded: "{rounded.card}"
    padding: "16px 18px 14px"
  nav-tab:
    textColor: "{colors.secondary-ink}"
    padding: "0 12px"
  nav-tab-active:
    textColor: "{colors.timetable-ink}"
  table-header:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.secondary-ink}"
    typography: "{typography.label}"
    padding: "8px 12px"
---

# Design System: AdaPlatform

## Overview

**Creative North Star: "The Timetable Sheet"**

AdaPlatform reads like printed public-transport matter: timetable ink on white paper, a signage face in two widths, and one colour, Route Petrol, drawn like the line on a network diagram. The page is a working document for an analyst, not a display: factual, calm and legible, with a lot of numbers held in a strict rhythm. Nothing competes for attention until something is wrong, and then the status colour says so, always with an icon and a word.

Density is high and deliberate. Tables, a fleet × day heatmap and small charts sit together on one screen at 14 px body text with tabular figures, because analysts compare values side by side. Order comes from hairline rules, one type scale and consistent card frames rather than from boxes, colour or size.

It must never look like a flashy BI tool: no walls of multicoloured charts, no decorative colour, no chart without a reason. Colour appears where it carries meaning (product navigation, status, magnitude on one sequential ramp) and nowhere else. Light and dark themes are designed as a pair; the map, whose tiles stay light, keeps its own fixed ink.

**Key Characteristics:**
- Timetable ink on paper, Route Petrol for wayfinding, status colour only for status.
- Barlow for reading, Barlow Semi Condensed for headings, labels and vehicle numbers.
- Flat at rest; hairline rules divide; a single shadow lifts only what floats.
- Dense, tabular, aligned figures; one type scale (ratio 1.2 from 14 px).
- Every verdict pairs colour with an icon and a word.
- A route line with stops as the product mark and the recurring motif.

## Colors

One restrained product colour on ink and paper neutrals, a reserved status set, and one sequential petrol ramp for magnitude.

### Primary
- **Route Petrol** (#0b6e6e): the product colour. Navigation (active tab underline), selection (the filled segmented option, the current page, a selected chart label), primary buttons, links, focus rings, the brand mark and the route line on the map. Dark theme: #52b5b0.
- **Route Petrol Strong** (#075656): hover and pressed state of petrol surfaces and links. Dark theme: #7fd0cb.
- **Route Petrol Soft** (#e0efee): quiet petrol grounds: active filter chips, an expanded table row, a keyboard-focused chart hit area. Dark theme: petrol at 16 % opacity.
- **On Route** (#ffffff): text and icons on a petrol fill. Dark theme: #0a2323.

### Neutral
- **Timetable Ink** (#1b2430): all primary text and values. Dark theme: #e6ecec.
- **Secondary Ink** (#566170): labels, table headers, captions, axis text, inactive controls. Dark theme: #a2adb6.
- **Paper** (#ffffff): the page, cards, inputs, menus. Dark theme: #111a1f.
- **Surface** (#f3f5f5): recessed grounds: table header row, hovered rows, the login backdrop. Dark theme: #17232a.
- **Surface Raised** (#e7ecec): skeleton blocks, a selected toggle on a surface. Dark theme: #203039.
- **Rule** (#d4dbdb): every hairline border and divider. Dark theme: #2d3d46.

### Status (reserved)
- **Fault** (#b42318) on **Fault Soft** (#fce9e7): a device or vehicle that is failing.
- **Warning** (#945400) on **Warning Soft** (#fdf0d9): something to look at.
- **OK** (#1d7a46) on **OK Soft** (#e4f3e9): working as expected.
- **Too little data** (#566170) on **Unknown Soft** (#edf0f2): not enough to judge; recedes (55 % opacity in solid fills).
- Dark theme uses lighter tones (#ff8b7b, #f0b54a, #5dca8b, #a2adb6) on 14 % tints.

### Charts
- **Sequential petrol ramp** (seq-1 #c1f7f6 → seq-6 #1d5151): magnitude on the heatmap, six bins, monotone lightness, validated for colour-vision deficiency. Dark theme reverses it (#104040 → #acdcdb).
- **Series Petrol** (#009090): single-series marks such as histogram bars (Route Petrol itself fails the chart chroma floor).
- **Gridline** (#e6eaea) and **Axis** (#c3cccc): recessive chart chrome.

### Map
- **Map Route** (#0b6e6e), **Map Casing** (#ffffff), **Map Stop** (#ffffff), **Map No Data** (#7a8591): drawn over map tiles, which stay light in both themes, so these never switch with the theme.

### Named Rules
**The Status Means Status Rule.** Fault, Warning, OK and Too-little-data colours are used only for a health verdict, never for a chart series, a decoration or a brand accent.

**The Petrol Is Wayfinding Rule.** Route Petrol marks where you are and what is selected. It never signals a status, good or bad.

**The Fixed Map Ink Rule.** Anything drawn on the map uses the map tokens, not the theme tokens; the tiles don't change with the theme.

## Typography

**Display Font:** Barlow Semi Condensed (with Barlow, system-ui, sans-serif)
**Body Font:** Barlow (with system-ui, sans-serif)

**Character:** Barlow is a DIN-like signage face, the family of station signs and timetables. The semi-condensed width carries headings, labels and vehicle numbers compactly; the normal width carries reading text and values. Both are self-hosted.

### Hierarchy
- **Headline** (600, 26 px, 1.2): the screen title (h1), e.g. "Stav sčítacích jednotek".
- **Title** (600, 20 px, 1.2): section headings such as "Vozidla", dialog titles, the brand name.
- **Subtitle** (600, 17 px, 1.2): chart titles, rule headings, large counts in the status filter.
- **Body** (400, 14 px, 1.5, tabular figures): all running text and table values; line length held near 72ch in explanatory text.
- **Label** (600, 13 px): table headers, segmented options, pills, form labels.
- **Caption** (400, 12 px): hints, "shown" counts, chart axis and legend text (the minimum size anywhere).
- **Vehicle number** (700, 17 px, 1.1): the vehicle id in tables, set like a fleet number.

### Named Rules
**The Aligned Figures Rule.** Numbers are always tabular (`tnum`) and right-aligned in columns, so digits line up across rows.

**The Two Widths Rule.** Only Barlow and Barlow Semi Condensed; no third family, no monospace for data.

## Layout

A full-height app shell: a 56 px header (brand, screen tabs, language, theme, account) above the active screen. On phones the tabs drop to their own full-width row. The network map screen is a 300 px line list beside the map (stacked above it on phones, capped at 35 % of the height). The device-health screen is one scrolling column: title and facts, the fleet status bar and filters, the chart row (one full-width heatmap, then two cards side by side from 420 px each), then the vehicle table with pagination.

Spacing follows a 4 px grid (4, 8, 12, 16, 24); page gutters are 16 px on phones and 28 px on desktop. Groups are laid out with gaps, not margins. Density is high: controls are 26–32 px tall on desktop and grow to 44 px on touch screens through one shared touch-target rule. Body text is 14 px while the root keeps the browser's 16 px, so rem-based spacing stays true to the user's settings.

## Elevation & Depth

Flat at rest. Surfaces are separated by hairline rules (1 px Rule) and by the paper / surface tone difference, never by shadows. One shadow exists, and only for things that float above the page: menus, the base-map picker card, chart tooltips, the login card and the rules side panel.

### Shadow Vocabulary
- **Float** (`box-shadow: 0 1px 2px rgba(27,36,48,0.12), 0 6px 20px rgba(27,36,48,0.14)`; dark theme `0 1px 2px rgba(0,0,0,0.4), 0 8px 24px rgba(0,0,0,0.45)`): popovers, menus, tooltips, the map layer card, the login card.

### Named Rules
**The Flat-at-Rest Rule.** A card on the page gets a border, never a shadow. A shadow means "this is on top of the page".

## Shapes

Small, even radii that step up with the size of the object: 3 px for swatches, 6 px for segmented options, 8 px for controls (buttons, inputs, selects, page buttons), 10 px for cards and table frames, 12 px for popovers and menus. Pills are fully round: status pills, filter chips, the account initials. Borders are always 1 px Rule; the active tab is marked by a 3 px Route Petrol underline.

The product mark is a route line with three stops: a 4 px petrol line with ring-shaped stops, the same form as the line diagram on the map.

## Components

### Buttons
Quiet and precise; one primary action per view.
- **Shape:** gently rounded (8px).
- **Primary:** Route Petrol fill, On Route text, label type, 40 px tall (44 px on touch). Used for the single main action, e.g. "Přihlásit se".
- **Hover / Focus:** hover darkens to Route Petrol Strong; focus shows the 2 px petrol outline at 2 px offset. Disabled at 50 % opacity.
- **Link button:** petrol text, semibold, underline on hover; for secondary actions ("zkusit znovu", "Jak se stav vyhodnocuje").

### Segmented toggles
The choice between views (Graf / Tabulka, Typ vozidla / Firmware) and the theme and language switches.
- **Frame:** 8 px rounded box, 1 px Rule, Paper ground, 2 px inner padding.
- **Option:** 26 px tall, label type in Secondary Ink.
- **Selected:** filled Route Petrol with On Route text, unmistakable at a glance.

### Status pill
The system's signature: every verdict as colour, icon and word together.
- **Style:** fully round, soft status ground with strong status text, a 14 px status icon (cross, triangle, check, minus) before the word.
- **Use:** table status column, fleet status filter, legends. Never colour alone.

### Filter chips
Active filters shown above the charts, each removable.
- **Style:** fully round, 30 px tall, Route Petrol border on Route Petrol Soft; label in Secondary Ink, value in semibold ink, a 24 px round × button.

### Cards / Containers
- **Corner Style:** 10px.
- **Background:** Paper, 1 px Rule border.
- **Shadow Strategy:** none (Flat-at-Rest).
- **Internal Padding:** 16 px top, 18 px sides, 14 px bottom; title in subtitle type, subtitle in 13 px Secondary Ink, controls on the right.

### Inputs / Fields
- **Style:** 1 px Rule, Paper ground, 8 px radius; 40 px on the login form, 32 px in the filter bar.
- **Focus:** border turns Route Petrol with a 1 px petrol ring.
- **Error:** a Fault Soft message box with icon above the form, announced as an alert.

### Navigation
- **Screen tabs:** label in medium weight Secondary Ink; the active tab turns Timetable Ink, semibold, with a 3 px Route Petrol underline. On phones they fill a full-width row.
- **Skip link:** first Tab stop, a petrol pill fixed top-left when focused.

### Data table
- **Header:** sticky, Surface ground, label type in Secondary Ink, sortable columns with a petrol ▴/▾.
- **Rows:** 1 px Rule dividers, 8 × 12 px cells, numbers right-aligned; hover Surface, expanded row Route Petrol Soft with the device table inset.
- **Share cells:** a value past its threshold gets the status soft ground and strong text.

### Charts
Hand-built SVG in chart cards, each with a table twin behind a Graf / Tabulka toggle. Marks are thin with 4 px rounded data ends; selections elsewhere dim other marks to 25 %; the focused mark gets a 2 px petrol ring; one Tab stop per chart, arrow keys inside.

## Do's and Don'ts

### Do:
- **Do** pair every status colour with its icon and word (Status pill), in tables, legends and filters alike.
- **Do** keep Route Petrol for navigation, selection, primary actions and the route line.
- **Do** use tabular, right-aligned figures in every column of numbers.
- **Do** separate content with 1 px Rule borders and tone, and keep the Float shadow for things that float.
- **Do** design both themes: every colour comes from a token with a light and a dark value; map overlays use the fixed map tokens.
- **Do** keep the minimum text size at 12 px, and targets at 24 px (44 px on touch).

### Don't:
- **Don't** make it a flashy BI tool: no walls of multicoloured charts, no colour that carries no meaning.
- **Don't** use a status colour for a chart series, a decoration or the brand.
- **Don't** show a status by colour alone.
- **Don't** put shadows on cards or other elements at rest on the page.
- **Don't** introduce a third typeface or a monospace for data.
