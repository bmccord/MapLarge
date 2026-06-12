# MapLargeBrowser

A file & directory browsing single-page app built as a take-home developer test for MapLarge. The entire browser lives in a dialog widget triggered from a host page.

The frontend is vanilla TypeScript (no UI framework) bundled with Vite. The backend is an ASP.NET Core Web API (.NET 10) that exposes one configured directory on disk.

## Layout

```
MapLargeBrowser.sln
MapLargeBrowser.Api/   # ASP.NET Core Web API (.NET 10)
MapLargeBrowser.Web/   # Vanilla TypeScript SPA bundled with Vite
```

See [CLAUDE.md](./CLAUDE.md) for the architecture, conventions, and design notes that aren't obvious from reading the code.

## Prerequisites

- .NET 10 SDK
- Node.js 20+ and npm

## Run locally

The API and the web frontend run as separate dev servers.

### API — terminal 1

```bash
cd MapLargeBrowser.Api
dotnet run
```

Listens on <http://localhost:5080>. CORS is wide open while `ASPNETCORE_ENVIRONMENT=Development`.

### Web — terminal 2

```bash
cd MapLargeBrowser.Web
npm install        # first time only
npm run dev        # Vite dev server with HMR on :5081
```

Open <http://localhost:5081>. Vite proxies `/api/*` to the .NET API on :5080.

### Production-like build

```bash
cd MapLargeBrowser.Web
npm run build      # tsc type-check + vite production build to dist/
npm run preview    # serve the production build at :5081
```

`npm run typecheck` runs `tsc --noEmit` for CI-style type checking without building.

## Browse root

The API serves one configured directory on disk. Resolution order:

1. `MAPLARGE_BROWSER_ROOT` environment variable (absolute path)
2. `MapLargeBrowser.Api/SampleRoot/` — seeded on startup by `SampleSeeder` if missing or empty

The sample seed tree is defined in code (`SampleSeeder.cs`) and reset on demand via `POST /api/admin/reset-sample-root` — the endpoint returns 403 if a custom `MAPLARGE_BROWSER_ROOT` is configured, so user data is never touched.

## API surface

All routes under `/api/`. `path` query values are relative to the browse root and validated server-side (path traversal and symlink crossing are rejected with 400).

| Method | Route | Notes |
|---|---|---|
| `GET` | `/browse?path=&showHidden=` | Children + counts + immediate (non-recursive) size |
| `GET` | `/search?path=&q=&showHidden=` | Recursive case-insensitive substring on relative path; capped at 500 results |
| `GET` | `/size?path=` | Recursive folder size (expensive — on demand only) |
| `GET` | `/download?path=` | Stream a file |
| `POST` | `/upload?path=&overwrite=` | Multipart upload to target dir (250 MB cap). 409 on existing target unless `overwrite=true` |
| `DELETE` | `/entries?path=&recursive=` | Delete file or folder. 409 on non-empty dir unless `recursive=true` |
| `POST` | `/entries/move?overwrite=` | Body `{ from, to }`. 409 on existing target unless `overwrite=true` |
| `POST` | `/entries/copy?overwrite=` | Body `{ from, to }`. 409 on existing target unless `overwrite=true` |
| `POST` | `/admin/reset-sample-root` | Wipe and re-seed bundled SampleRoot. 403 if `MAPLARGE_BROWSER_ROOT` is set |
