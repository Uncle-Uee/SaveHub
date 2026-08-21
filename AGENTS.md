# AGENTS.md

Guidance for AI agents and contributors working in the **SaveHub** repository.

## What this project is

SaveHub is a generalized .NET library + CLI that uploads emulator **memory cards**
and **save states** to an online storage backend (GitHub today). It builds the
zip, embeds and mirrors a description, maintains a per-folder index, and submits a
pull request. It is meant to be reusable by any emulator frontend.

## Roadmap & key decisions

- **License:** LGPL-3.0-or-later. Open source; keep the copyright notice in
  LICENSE. Donations via the FUNDING link.
- **Two-repo split:** this repo (`SaveHub`) holds the **API libraries + CLI** and
  publishes NuGet packages; a separate `SaveHub.UI` repo holds **WinForms + a
  planned cross-platform Avalonia app** and consumes those packages. WinForms will
  move out of this repo into `SaveHub.UI`.
- **Distribution:** publish the libraries to **NuGet.org** (frictionless public
  consumption); `pack-api.ps1` + `local-feed` is the local dev feed. Apps set
  `IsPackable=false`. Publishing is automated via **GitHub Actions Trusted
  Publishing (OIDC)** — `.github/workflows/publish.yml`; no long-lived NuGet API
  key (set the `NUGET_USER` repo variable + a nuget.org trusted-publisher policy).
- **CLI parity:** the CLI covers the full UI feature set (config / upload / edit /
  download / delete / list / info) so it ships usefully alongside the API.
- **Code style:** see `.github/copilot-instructions.md` (explicit types, Allman
  braces, block-bodied methods, member order, one type per file).
- **Signing:** no self-signed cert (NuGet rejects it). Use Azure Trusted Signing,
  an OV/EV cert, or SignPath Foundation for real trust.

## Solution layout

```
SaveHub.sln
src/
  SaveHub.Core/        # models, archive/manifest builder, config, provider abstraction, SaveHubClient
  SaveHub.GitHub/      # GitHub provider (Octokit)
  SaveHub.GitLab/      # GitLab provider (HttpClient/REST v4)
  SaveHub.Bitbucket/   # Bitbucket provider (HttpClient/REST 2.0)
  SaveHub.Supabase/    # Supabase Storage provider (HttpClient/REST)
  SaveHub.GoogleDrive/ # Google Drive provider (Google.Apis.Drive.v3 + OAuth)
  SaveHub.Hosting/     # aggregates providers -> SaveHubHost.CreateClient(config)
  SaveHub.Cli/         # Spectre.Console CLI frontend (savehub)
  SaveHub.WinForms/    # Windows desktop frontend (net10.0-windows)
docs/
  API.md                          # full API + format specification
  TUTORIAL.md                     # step-by-step memory-card upload
  save-repo-README-template.md    # README to drop into a save database repo
```

### Core building blocks (`SaveHub.Core`)

- `Models/` — `SaveType`, `KnownPlatforms`, `SaveUploadRequest`, `SaveEntry`.
- `Archiving/SaveNaming` — the single source of truth for names/paths:
  `NN.zip` (memory card), `NN-sstate.zip` (save state), `NN-folder.zip` (save
  folder), `SaveNaming.Label`, and `README.txt` inside the zip.
- `Archiving/SaveArchiveBuilder` — builds the zip (+ optional icon) into a
  `PreparedSave`. `SaveFolder` uploads preserve structure via `RootDirectory`.
- `Archiving/SaveManifest` — the human-readable description text (embedded in zip).
- `Archiving/GameReadmeFormatter` — upserts the per-game `README.md` saves index
  (archive, type, description). Replaces the old side-car `.txt`/`saves.txt`.
- `Archiving/PlatformReadmeFormatter` — upserts the per-platform `README.md` games
  index (title id + game name), created/updated automatically on upload.
- `Archiving/MemoryCardIndexFormatter` — upserts a platform's bulk memory-card
  index `README.md` under the top-sorting `!index` folder (`SaveNaming.MemoryCardIndexFolderName`):
  one row per card with a cover-art thumbnail, game name, and id. Used by bulk uploads.
- `Archiving/CoverArt` — `CoverArtSource` maps platform+serial to a cover URL;
  `HttpCoverArtResolver` downloads it. PS1/PS2 use `SLUS-#####`; PSP uses `NPEG#####`.
  `CachingCoverArtResolver` wraps a resolver and caches downloads on disk (keyed by
  platform+serial) so covers are not re-fetched; the apps enable it via a cache folder.
  `CoverArtCache` is the on-disk store (find/read/store) shared by the resolver and the apps
  (to preview cached covers and to persist user-supplied cover art).
- `Archiving/PsSerialScanner` — reads the game serial (title id) from a PS1/PS2
  memory-card image by scanning for the stored serial strings.
- `Archiving/ParamSfoReader` — parses `PARAM.SFO` (PS3/PS4/PS5/PSP/Vita) and returns
  its `TITLE_ID`/`DISC_ID` (`TitleIdFromFiles`) or game name `TITLE` (`GameNameFromFiles`).
- `Archiving/MemoryCardReader` — detects the console from a memory-card image
  (`DetectPlatform` → `PS1`/`PS2`, covering raw plus wrapped `.gme`/`.vgs`/`.vmp` images) and
  reads the stored game/save title
  (Shift-JIS code page 932, full-width normalised).
- `Archiving/SaveNameExtractor` — best-effort game name: `PARAM.SFO` `TITLE` first,
  then a PS1/PS2 memory-card title.
- `Archiving/GameIdResolver` — resolves the folder game id: explicit title id →
  PS1/PS2 card serial → PS3+ `PARAM.SFO` id → user game name → save `TITLE` →
  `Unknown`. `DetectTitleId(...)` returns just the machine id for "detect" buttons.
- `Configuration/` — `SaveHubConfig` (raw per-provider JSON sections) +
  `SaveHubConfigStore`.
- `Abstractions/ISaveStorageProvider` — the backend contract (browse, upload,
  `DownloadFileAsync`).
- `SaveHubClient` — orchestrates prepare + upload; also `DownloadArchiveAsync` /
  `DownloadArchiveToFileAsync`, `GetGameNamesAsync` (platform index → id/name map),
  `GetGameIconAsync`, and `DeleteSaveAsync` (archive + README row).
  `UploadOptions.TargetIndex` replaces an existing save (edit) instead of appending.
  Providers expose `DownloadFileAsync` / `UploadFileAsync` / `DeleteFileAsync`.
- `Models/LibraryIndex` + root `library.json` — consolidated `platform → {id → name}`
  index for reading the whole library in one request. `SaveHubClient.GetLibraryIndexAsync`
  reads it, `RebuildLibraryIndexAsync` regenerates it from per-platform READMEs, and
  `SetGameNameAsync` renames a game (updates the platform README + the index).
- `SaveHubClient.UpdateMemoryCardIndexAsync(platform, entries)` — adds/updates rows in
  a platform's `!index/README.md` (bulk memory-card catalog); rows merge by id.

### GitHub provider (`SaveHub.GitHub`)

- `GitHubProviderSettings` — owner/repo/branch/token/autoMerge.
- `GitHubSaveStorageProvider` — browsing + upload via Octokit Git Data API
  (blob → tree → commit → branch → PR → optional merge). Non-collaborators get a
  fork automatically.
- `GitHubProviderFactory` — read/write settings and build the provider.

### GitLab provider (`SaveHub.GitLab`)

- `GitLabProviderSettings` — baseUrl/owner/repo/branch/token(+env)/autoMerge.
- `GitLabSaveStorageProvider` — browsing + upload via REST v4 (commits API with
  create/update actions → merge request → optional merge). Non-members get a fork.
- `GitLabProviderFactory` — read/write settings and build the provider.

### Bitbucket provider (`SaveHub.Bitbucket`)

- `BitbucketProviderSettings` — workspace/repo/branch/username/appPassword(+env)/autoMerge.
- `BitbucketSaveStorageProvider` — browsing + upload via REST 2.0 (create branch →
  `POST /src` multi-file commit → pull request → optional merge). Non-members get a fork.
- `BitbucketProviderFactory` — read/write settings and build the provider.

### Supabase provider (`SaveHub.Supabase`)

- `SupabaseProviderSettings` — url/bucket/apiKey(+env)/isOwner.
- `SupabaseSaveStorageProvider` — Storage REST (`object`, `object/list`) via
  `HttpClient`. Owner publishes to the root; others upload under `pending/`.
- `SupabaseProviderFactory` — read/write settings and build the provider.

### Google Drive provider (`SaveHub.GoogleDrive`)

- `GoogleDriveProviderSettings` — rootFolderName/clientId/clientSecret(+env)/isOwner
  (plus optional advanced rootFolderId). Users **bring their own OAuth client**.
- `GoogleDriveAuthenticator.SignInAsync` — browser loopback OAuth using the
  least-privilege **`drive.file`** scope; produces a `GoogleDriveSession` (holds the
  `DriveService`, ~2.5h soft expiry). Desktop uses `MemoryTokenStore` (session only);
  CLI uses a `FileDataStore` (persists).
- `GoogleDriveSaveStorageProvider` — finds/creates its own `RootFolderName` folder
  (drive.file can only see files the app created), then folder-id resolution + upsert
  + list + download.
- `GoogleDriveProviderFactory` — builds from `GoogleDriveSession.Current`.

### Hosting (`SaveHub.Hosting`)

- `SaveHubHost.CreateClient(config)` / `TryCreateClient` — switches on
  `config.ActiveProvider` and builds the right provider. Overloads take an optional
  `ICoverArtResolver` (the apps pass a `CachingCoverArtResolver`). Both frontends use this so
  they depend only on Core + Hosting, never on a specific provider.

### Desktop app (`SaveHub.WinForms`)

- `net10.0-windows`, fixed 800x800 window, tabs: Upload / Download / Edit / Manage /
  Settings. A bottom status bar shows a text status plus a marquee **busy
  indicator** (`pbar`) that animates while any operation runs (all tab actions go
  through `IShellContext.RunBusy` → `MainForm.SetBusy`).
- `AppServices` builds a client via `SaveHubHost` from the shared per-user config
  (`SaveHubConfigStore.DefaultPath`). Settings tab has a provider dropdown + a panel
  per provider (Google has "Sign in with Google"). Download/Edit show the game name
  (from the platform index) and a cover-icon preview; Manage does multi-select
  delete. `Devices` groups consoles by manufacturer (`Devices.Groups`: Nintendo /
  Sony / Microsoft / Sega) rendered as one dropdown per manufacturer; `Devices.All`
  is the flat lookup. PS1/PS2 memory cards auto-select the console and pre-fill the
  game name on Browse. `MainForm` is split into `MainForm.cs` (logic) and
  `MainForm.Designer.cs` (layout).

## Conventions

- Target frameworks: **net8.0+** (repo currently uses **net10.0**). No
  `netstandard`.
- Nullable and implicit usings are enabled.
- Keep all naming/layout decisions in `SaveNaming`; do not hardcode archive names
  elsewhere.
- `SaveType.MemoryCard` = exactly one file per zip. `SaveType.SaveState` and
  `SaveType.SaveFolder` = multiple files allowed (folder preserves structure via
  `SaveUploadRequest.RootDirectory`).
- Secrets: never write tokens to disk by default; prefer the env var named by
  `GitHubProviderSettings.TokenEnvironmentVariable` (`SAVEHUB_GITHUB_TOKEN`).
- One shared config file at `SaveHubConfigStore.DefaultPath`
  (`%APPDATA%/SaveHub/savehub.config.json`) is used by both the CLI and desktop app.
  Each provider stores its settings under its own key in `config.Providers`.
- Auto-merge only when requested **and** enabled **and** the user has write
  access; otherwise open a PR.

## Build, run, test

```powershell
dotnet build
dotnet run --project src/SaveHub.Cli -- info
dotnet run --project src/SaveHub.Cli -- --help
```

There is no automated test project yet. When adding logic, prefer pure,
side-effect-free helpers in `SaveHub.Core` (e.g. naming, manifest, index) so they
are easy to unit test without network access. Network calls live in providers.

## Release & signing

- App icon: `assets/icon.svg` (source) and `assets/icon.png`; `assets/icon.ico`
  (multi-size, generated from the png) is wired into both exes via
  `<ApplicationIcon>` and used as the WinForms window icon.
- Share build (WinForms, self-contained single-file, Windows x64). The exe is
  named `SaveHub.exe` (`<AssemblyName>SaveHub</AssemblyName>`); WinForms can't be
  IL-trimmed, so keep it self-contained and use single-file compression to shrink
  it (~50 MB) rather than deleting runtime DLLs:

  ```powershell
  dotnet publish src/SaveHub.WinForms -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -o publish/SaveHub-win-x64
  ```

  Output/zip live under `publish/` (git-ignored artifacts, not committed).
- Code signing: none by default. A self-signed cert is intentionally **not** used —
  NuGet rejects packages signed with one, and it clears neither SmartScreen nor
  cross-machine trust. For real trust use a CA cert (Azure Trusted Signing, or an
  OV/EV certificate), or apply for free signing via the SignPath Foundation once
  the project is public. Never commit a `.pfx` or its password.

## Adding a new storage backend

1. Create a `SaveHub.<Backend>` project referencing `SaveHub.Core`.
2. Implement `ISaveStorageProvider` (use `GitHubSaveStorageProvider` as a model).
3. Add a settings class and a factory mirroring `GitHubProviderFactory`.
4. Reuse `SaveArchiveBuilder`, `SaveManifest`, `SavesIndexFormatter`, `SaveNaming`
   unchanged — only the transport differs.
5. Register it in `SaveHubHost` (the `Providers` list + `CreateClient` switch) and add
   the project to `SaveHub.slnx` + `SaveHub.Hosting.csproj`.
6. For CLI parity add a `config <backend>` command (mirror `ConfigureGitHubCommand`),
   register it in `Program.cs`, and add the key to `UseProviderCommand`.
7. For the apps add a Settings panel + `Save<Backend>Settings`/`LoadSettings` wiring in
   both `MainFormController` (WinForms) and `AppController` (Avalonia).

## Safety / scope

- The API libraries and CLI are licensed under the GNU LGPL-3.0-or-later; see
  `LICENSE` and the `Licenses/` folder. Keep the copyright notice and donation
  references intact.
- Do not remove the copyright or license notice.
- Avoid destructive Git operations against user repositories; uploads must go
  through pull requests.
