# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A take-home test project for MapLarge: a file & directory browser SPA. The original spec lives at the repo root as `MapLarge Test Project -Developer - 2026.pdf`. The project is evaluated as a code review and an architecture discussion — reviewers explicitly value original code and barebones structure over framework/template usage. No UI frameworks (React/Angular/etc.) are allowed.

## Layout

- `MapLargeBrowser.sln` — solution file at the root
- `MapLargeBrowser.Api/` — ASP.NET Core Web API (.NET 10). Controllers, DTOs (`Models/`), services (`Services/`), configuration (`Configuration/`), sample data (`SampleRoot/`).
- `MapLargeBrowser.Web/` — vanilla TypeScript SPA built with Vite. `index.html` at the project root is the entry; sources in `src/` (including `app.css`); static assets in `public/` (Vite serves these as-is — `icons/` lives here). Production bundles emit to `dist/`.

The two projects run as separate dev servers on different ports.

## Commands

### API (from `MapLargeBrowser.Api/`)

```bash
dotnet run        # http://localhost:5080
dotnet build
```

### Web (from `MapLargeBrowser.Web/`)

```bash
npm install
npm run dev        # vite dev server + HMR on :5081
npm run build      # tsc type-check + vite production build to dist/
npm run preview    # serve the production build at :5081
npm run typecheck  # tsc --noEmit (CI-style type check)
```

Open `http://localhost:5081/`.

## Browse root configuration

The API serves files from a single configured directory on disk. Resolution order:

1. `MAPLARGE_BROWSER_ROOT` environment variable (absolute path)
2. `MapLargeBrowser.Api/SampleRoot/` — seeded on startup by `SampleSeeder` if missing or empty. Gitignored.

There is no `appsettings.json` knob for this — env var or fallback only.

When the fallback root is in use, `POST /api/admin/reset-sample-root` wipes it and re-seeds. The endpoint returns 403 if a custom root is configured (never touches user data). The UI shows a Reset button only when the `BrowseResponse.rootIsResettable` flag is true.

## API surface

All routes under `/api/`. All `path` query values are *relative to the browse root* and validated server-side.

| Method | Path | Notes |
|---|---|---|
| GET | `/browse?path=&showHidden=` | Children + counts + immediate (non-recursive) size |
| GET | `/search?path=&q=&showHidden=` | Recursive case-insensitive substring on relative path |
| GET | `/size?path=` | Recursive folder size (expensive — on demand only) |
| GET | `/download?path=` | Stream a file |
| POST | `/upload?path=&overwrite=` | Multipart upload to target dir (250 MB cap). 409 on existing target unless `overwrite=true` |
| DELETE | `/entries?path=&recursive=` | Delete file or folder. 409 on non-empty dir unless `recursive=true` |
| POST | `/entries/move?overwrite=` | Body `{ from, to }`. 409 on existing target unless `overwrite=true` |
| POST | `/entries/copy?overwrite=` | Body `{ from, to }`. 409 on existing target unless `overwrite=true` |
| POST | `/admin/reset-sample-root` | Wipe and re-seed bundled SampleRoot. 403 if `MAPLARGE_BROWSER_ROOT` is set |

## Architecture notes that aren't obvious from the code

- **Symlinks are not traversed.** When the API encounters a symlink during a listing, it surfaces it as `type: "Symlink"` with a `symlinkTarget`, but does not follow it. The path resolver rejects any resolved path that crosses a symlink boundary. As a side effect, file actions (download/move/copy/delete) cannot operate on symlinks via this API.
- **Hidden files are off by default.** A `showHidden` query parameter (mirrored into the URL on the client) toggles them. Dotfile detection on Unix plus `FileAttributes.Hidden` on Windows.
- **Counts are cheap and always returned; recursive size is expensive and on-demand.** The browse and search responses carry counts + an immediate (non-recursive) size sum. The `/size` endpoint exists for recursive sizing per-folder.
- **Client routing uses the History API.** URLs look like `/browse/some/path?q=foo&hidden=1`. Vite's dev server falls back to `index.html` for unmatched routes so refreshes on deep links return the SPA shell.
- **Vite is the dev server + bundler.** `tsconfig.json` uses `module: ESNext` / `moduleResolution: bundler` and `noEmit: true` — `tsc` is type-check only; Vite transpiles + bundles.
- **API base URL is `/api`** (in `src/config.ts`). Vite's dev server proxies `/api/*` to `http://localhost:5080` (see `vite.config.ts`). In production, the assumption is that the API is served from the same origin behind a reverse proxy.
- **CORS is enabled only in `Development`.** Wide-open dev policy registered in `Program.cs`. Not strictly needed when going through the Vite proxy, but doesn't hurt if the API is hit directly.
- **The entire file browser lives in a dialog.** `index.html` mounts a host page with a trigger button. Deep links (`/browse/...`) auto-open the dialog at that path; closing the dialog clears the URL to `/`.

## Code style

- `.editorconfig` at the repo root drives Rider and the C# compiler.
- C#: file-scoped namespaces, Allman braces, top-level statements in `Program.cs`, nullable + implicit usings on, `var` where the type is apparent, expression-bodied members where they fit.
- TypeScript: `strict`, `verbatimModuleSyntax`, ES2022 with `moduleResolution: bundler`. Imports use no file extensions (Vite resolves `.ts`).

## Workflow

Brian edits in Rider. `.idea/` is gitignored.
