// English. Same keys as cs.ts, except plurals, which follow English rules (_one, _other).
// A key missing here falls back to Czech.

const percent = 'number(style: percent; maximumFractionDigits: 0)'

export const en = {
  app: {
    screens: 'Screens',
    views: {
      mapa: 'Network map',
      jednotky: 'Device health',
    },
    loading: 'Loading…',
    comingSoon: 'Coming soon.',
  },
  common: {
    apiUnavailable: 'The API is unavailable: {{message}}',
    retry: 'try again',
  },
  theme: {
    label: 'Colour mode',
    system: 'Follow system',
    light: 'Light mode',
    dark: 'Dark mode',
  },
  language: {
    label: 'Language',
  },
  map: {
    loadingStops: 'Loading stops…',
    loadingLines: 'Loading lines…',
    noStops: 'No stops have been imported yet.',
    lines: 'Lines',
    linesHint: 'Patterns sorted by the number of trips in the data. Patterns without trips are hidden.',
    patternsWithTrips_one: '{{count}} pattern with trips',
    patternsWithTrips_other: '{{count}} patterns with trips',
    trips_one: '{{count}} trip',
    trips_other: '{{count}} trips',
    stops_one: '{{count}} stop',
    stops_other: '{{count}} stops',
    hiddenPatterns_one: '+ {{count}} pattern without trips',
    hiddenPatterns_other: '+ {{count}} patterns without trips',
    patternMeta: '{{trips}}, {{stops}}',
    noVisits: 'no recorded stop visits',
    stopActivity_one:
      '{{count, number}} stop visit, on average {{boardings, number(maximumFractionDigits: 1)}} boardings, {{alightings, number(maximumFractionDigits: 1)}} alightings',
    stopActivity_other:
      '{{count, number}} stop visits, on average {{boardings, number(maximumFractionDigits: 1)}} boardings, {{alightings, number(maximumFractionDigits: 1)}} alightings',
    legendFewer: 'fewer boardings',
    legendMore: 'more boardings',
    legendNoData: 'no data',
    chooseBaseMap: 'Base map',
    layers: {
      'mapy-basic': 'Basic',
      'mapy-outdoor': 'Outdoor',
      'mapy-aerial': 'Aerial',
      'mapy-winter': 'Winter',
      osm: 'OpenStreetMap',
    },
  },
  health: {
    loading: 'Computing device health from raw data…',
    empty: 'No raw logs have been imported yet.',
    title: 'Counting device health',
    vehicles_one: '{{count, number}} vehicle',
    vehicles_other: '{{count, number}} vehicles',
    devices_one: '{{count, number}} device',
    devices_other: '{{count, number}} devices',
    summary: '{{from}} – {{to}} · {{vehicles}} · {{devices}}. Evaluated from raw messages using provisional rules (below).',
    facts: {
      period: 'Period',
      vehicles: 'Vehicles',
      devices: 'Devices',
    },
    note: 'Evaluated from the devices’ raw messages using provisional rules, described below the table.',
    fleetStatus: 'Fleet status',
    status: {
      Fault: 'Fault',
      Warning: 'Warning',
      Ok: 'OK',
      Unknown: 'Too little data',
    },
    statusFilter: 'Status filter',
    traction: 'Traction',
    allTractions: 'all',
    tractions: {
      tramvaj: 'tram',
      autobus: 'bus',
      trolejbus: 'trolleybus',
    },
    clearFilters: 'clear filters',
    shown: '{{count, number}} shown. Click a row to see the vehicle’s devices.',
    columns: {
      status: 'Status',
      vehicle: 'Vehicle',
      traction: 'Traction',
      boardings: 'Boardings',
      alightings: 'Alightings',
      imbalance: 'Imbalance',
      negative: 'Negative occup.',
      flagged: 'Error flag',
      reasons: 'Reason',
      device: 'Device',
      firmware: 'Firmware',
      stops: 'Stops',
      notAlive: 'alive=false',
      restarts: 'Restarts',
      flaggedStops: 'Error flag',
    },
    hints: {
      imbalance: '|boardings − alightings| / (boardings + alightings) over the whole period',
      negative: 'Share of stops after which the vehicle reports fewer than zero passengers on board',
      flagged: 'Share of stops at which the vehicle flagged a device as invalid (chyba)',
    },
    reasons: {
      DeviceSilent_one: 'counted nobody at {{count, number}} stop',
      DeviceSilent_other: 'counted nobody at {{count, number}} stops',
      DeviceFlagged: `flagged invalid at {{value, ${percent}}} of stops`,
      DeviceNotAlive: `{{value, ${percent}}} of heartbeats report alive=false`,
      AllDevicesSilent: 'no device counted anybody',
      SomeDevicesSilent_one: '{{count}} device counted nobody',
      SomeDevicesSilent_other: '{{count}} devices counted nobody',
      Imbalance: `boarding/alighting imbalance {{value, ${percent}}}`,
      NegativeOccupancy: `negative occupancy after {{value, ${percent}}} of stops`,
      FlaggedStops: `error flag at {{value, ${percent}}} of stops`,
      DeviceWarning: 'a device has a warning',
    },
    rules: {
      title: 'Evaluation rules (provisional)',
      imbalance:
        '<strong>Imbalance</strong> = |boardings − alightings| / (boardings + alightings) over the whole period, only with at least {{min}} passengers counted. Warning from {{warning}}, fault from {{fault}}.',
      negative:
        '<strong>Negative occupancy</strong> = share of stops after which the on-board computer reports fewer than zero passengers. Warning from {{warning}}, fault from {{fault}}.',
      flagged:
        '<strong>Error flag</strong> = share of stops at which the vehicle flagged a device as invalid (<code>chyba</code>). Warning from {{warning}}.',
      silent:
        '<strong>Silent device</strong> = finished counting at stops but counted nobody over the whole period — a fault if every device of the vehicle is silent.',
      counts: 'Counts per stop = difference of the counter reading between counting start and stop (log values are running readings, see report F11).',
      restarts: 'Restarts are not evaluated: in the data they are a routine operational event (see report F5).',
    },
  },
}
