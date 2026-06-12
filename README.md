# MapLargeBrowser

A file & directory browser single-page app built for the MapLarge 2026 developer test. See the spec PDF in this directory.

## Layout

```
MapLargeBrowser.sln
MapLargeBrowser.Api/   # ASP.NET Core Web API (.NET 10)
MapLargeBrowser.Web/   # Vanilla TypeScript SPA (no framework, no bundler)
```

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

### Web — terminal 2 (TypeScript watcher)

```bash
cd MapLargeBrowser.Web
npm install      # first time only
npm run watch
```

### Web — terminal 3 (static server)

```bash
cd MapLargeBrowser.Web
npm run serve
```

Listens on <http://localhost:5081>. Open that URL in a browser.

`npm start` from a single terminal does a one-shot build + serve (no watch) if you'd rather not run two terminals.

## Browse root

The API exposes the contents of a single root directory on disk. Resolution order:

1. `MAPLARGE_BROWSER_ROOT` environment variable (absolute path)
2. `MapLargeBrowser.Api/SampleRoot/` — committed sample tree, used if the env var is unset

## API surface

All routes under `/api/`. `path` query values are relative to the browse root.

- `GET  /browse?path=&showHidden=` — list children + counts + immediate size
- `GET  /search?path=&q=&showHidden=` — recursive case-insensitive substring search
- `GET  /size?path=` — recursive folder size (expensive; on demand only)
- `GET  /download?path=` — stream a file
- `POST /upload?path=` — multipart upload (250 MB cap)
- `DELETE /entries?path=` — delete file or folder
- `POST /entries/move` — body `{ from, to }`
- `POST /entries/copy` — body `{ from, to }`
