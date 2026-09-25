# AdaPlatform frontend

React + TypeScript SPA (Vite) for the AdaPlatform API.

- **TanStack Query** — all server reads (`src/queries.ts`): caching, de-duplication, cancellation
- **TanStack Table v9** — the device-health tables (sorting, column filters, expandable rows)
- **Radix UI** — accessible primitives (tabs, accordion, select, toggle group, collapsible, tooltip)
- **Leaflet** — the map; base layers come from `/api/map/config` (Mapy.com via the API's tile proxy)

```bash
npm install
npm run dev     # http://localhost:5173, proxies /api to http://localhost:8080 (override with API_URL)
npm run build
npm run lint
```

Screens: `#/mapa` (network map, lines and patterns) and `#/jednotky` (counting-device health).
