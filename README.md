# SaveHub

[![Support SaveHub](https://img.shields.io/badge/%E2%9D%A4-Support%20SaveHub-ff5f5f)](https://pay.yoco.com/savehub)

**SaveHub** is a generalized .NET API (with a CLI) for uploading emulator
**memory cards** and **save states** to an online storage backend so they can be
shared and re-downloaded. It targets **GitHub** today and is designed so other
backends (Google Drive, Supabase, Firebase, ...) can be added later.

It works with any emulator/frontend: you give SaveHub the platform, game id, save
type, and files — it builds the zip (with an embedded description), maintains the
per-game and per-platform `README.md` indexes, and submits a pull request
to your repository.

> Open source under the **MIT License** — see [LICENSE](LICENSE). Please keep the
> copyright notice so the author gets credit. If SaveHub is useful to you, please
> consider supporting it: **https://pay.yoco.com/savehub**.

---

## Contents

- [SaveHub](#savehub)
  - [Contents](#contents)
  - [Features](#features)
  - [Repository layout produced](#repository-layout-produced)
  - [Projects](#projects)
  - [Requirements](#requirements)
  - [Quick start (CLI)](#quick-start-cli)
    - [CLI command reference](#cli-command-reference)
  - [Adding a save for a new / unsupported platform](#adding-a-save-for-a-new--unsupported-platform)
    - [Example: a PS1 memory card for *Oddworld: Abe's Oddysee*](#example-a-ps1-memory-card-for-oddworld-abes-oddysee)
  - [Title IDs (game id / folder name)](#title-ids-game-id--folder-name)
  - [Cover art](#cover-art)
  - [Using the API from your own code](#using-the-api-from-your-own-code)
    - [Key types](#key-types)
  - [Configuration](#configuration)
  - [Storage providers](#storage-providers)
    - [GitHub](#github)
    - [Supabase](#supabase)
    - [Google Drive (bring your own OAuth client, browser sign-in)](#google-drive-bring-your-own-oauth-client-browser-sign-in)
  - [GitHub token \& permissions](#github-token--permissions)
    - [1. Create the token](#1-create-the-token)
    - [2. Do you need `tokenEnvironmentVariable`?](#2-do-you-need-tokenenvironmentvariable)
    - [3. Set the environment variable (Windows PowerShell)](#3-set-the-environment-variable-windows-powershell)
  - [Auto-merge rules](#auto-merge-rules)
  - [Implementing your own provider](#implementing-your-own-provider)
    - [Example: using SaveHub with Supabase](#example-using-savehub-with-supabase)
    - [Example: using SaveHub with Google Drive](#example-using-savehub-with-google-drive)
  - [License \& support](#license--support)

---

## Features

- Upload a **memory card**, a **save state**, or a **save folder** (e.g. PS3/PS4/PS5
  save data — the folder structure is preserved in the zip).
- Multi-file saves supported (save states, and multi-file handheld saves such as
  GBA save + RTC files).
- **Download** any stored archive, and **edit/replace** an existing save in place
  (overwrite `01.zip` instead of adding `02.zip`).
- SaveHub **builds the zip for you** and embeds a `README.txt` describing the save.
- **Automatic title id**: the game serial is read from PS1/PS2 memory cards; Nintendo
  saves use the file name as the folder. You can always override with `--game`.
- **Automatic console detection**: PS1 vs PS2 memory cards are recognised from the
  card image itself, so SaveHub selects the right console for you.
- **Automatic game name**: the title is read from PS2 (`icon.sys`) / PS1 memory
  cards and from `PARAM.SFO` (`TITLE`) on PS3+, so the Game Name field is
  pre-filled where possible.
- Each game folder gets a **`README.md`** listing every save (`01.zip`, ...) with
  its description, so it is easy to tell what each save is for.
- **Automatic cover art**: PS1/PS2/PSP covers are downloaded by serial; you can
  also supply your own image (`--icon`).
- Uploads are delivered as **pull requests**; optional **auto-merge** for
  owners/contributors (at their own risk).
- Non-collaborators contribute automatically through a **fork + pull request**.
- JSON configuration selecting the target repo and provider.
- Pluggable **storage provider** abstraction for future backends.

## Repository layout produced

```
<PLATFORM>/
  README.md           # games index: title id + game name (one row per game)
  <GAME-ID>/
    README.md         # saves index: | 01.zip | Memory Card | description |
    01.zip            # memory card (contains the card + embedded README.txt)
    01-sstate.zip     # save state (may contain multiple files + embedded README.txt)
    icon.jpg          # cover art (auto-downloaded or user-supplied)
```

The save description lives in two places: inside the zip as `README.txt`, and in
the game folder's `README.md` table (so you can read it without downloading).

Examples of `<PLATFORM>`: `PS1`, `PS2`, `PS3`, `PS4`, `PSP`, `PSV`, `GB`, `GBC`,
`GBA`, `DS`, `3DS`, `NES`, `SNES`, `N64`, `GC`, `WII`, `SWITCH`, `GENESIS`,
`DREAMCAST`, `VB`. Any string is accepted; these are just the well-known ones.

A drop-in README you can place in your save repository is provided at
[docs/save-repo-README-template.md](docs/save-repo-README-template.md).

## Projects

| Project | Description |
| --- | --- |
| `SaveHub.Core` | Models, zip/manifest builder, config, provider abstraction, `SaveHubClient`. |
| `SaveHub.GitHub` | GitHub storage provider (based on Octokit). |
| `SaveHub.Supabase` | Supabase Storage provider (REST). |
| `SaveHub.GoogleDrive` | Google Drive provider (OAuth browser sign-in). |
| `SaveHub.Hosting` | Aggregates all providers; builds a client from config (`SaveHubHost`). |
| `SaveHub.Cli` | Cross-platform command-line frontend (`savehub`). |

## Requirements

- .NET SDK **8.0 or later** (built and tested against **.NET 10**). No
  `netstandard` targets.
- A **public GitHub repository** to hold the saves.
- A GitHub **personal access token** (see below).

## Quick start (CLI)

> New here? Follow the step-by-step [upload tutorial](docs/TUTORIAL.md) to publish a
> PS1/PS2 memory card, or the [download tutorial](docs/TUTORIAL-DOWNLOAD.md) to fetch
> one back.

Build:

```powershell
dotnet build
```

Configure your GitHub connection (interactive prompts, or pass options):

```powershell
dotnet run --project src/SaveHub.Cli -- config github --owner Uncle-Uee --repo Emu-Saves-Backup
```

Provide your token via environment variable (recommended):

```powershell
$env:SAVEHUB_GITHUB_TOKEN = "ghp_xxx"
```

Upload a memory card:

```powershell
dotnet run --project src/SaveHub.Cli -- upload `
  --platform PS2 --game SLUS-21274 --type mc `
  --file "C:\saves\Mcd001.ps2" `
  --description "100% completion, all worlds cleared"
```

Upload a save state made of multiple files:

```powershell
dotnet run --project src/SaveHub.Cli -- upload `
  --platform GBA --game AGB-BPRE --type state --emulator mGBA `
  --file "C:\saves\pokemon.ss0" --file "C:\saves\pokemon.rtc" `
  --description "Elite Four ready"
```

Run `upload` with no options to be prompted for everything interactively
(including selecting multiple files for save states / GBA saves).

Browse the database:

```powershell
dotnet run --project src/SaveHub.Cli -- list platforms
dotnet run --project src/SaveHub.Cli -- list games --platform PS2
dotnet run --project src/SaveHub.Cli -- list saves --platform PS2 --game SLUS-21274
```

Show config / support info:

```powershell
dotnet run --project src/SaveHub.Cli -- config show
dotnet run --project src/SaveHub.Cli -- info
```

### CLI command reference

| Command | Purpose |
| --- | --- |
| `config github` | Create/update the GitHub connection. Options: `--owner`, `--repo`, `--branch`, `--token`, `--auto-merge`. |
| `config supabase` | Create/update the Supabase connection: `--url`, `--bucket`, `--key`, `--owner`. |
| `config google` | Create/update the Google Drive connection: `--root`, `--client-id`, `--secret`, `--owner`. |
| `config google-login` | Sign in to Google Drive via the browser (caches the token). |
| `config use <provider>` | Set the active provider: `github` \| `supabase` \| `googledrive`. |
| `config show` | Print the current config (secrets redacted). |
| `config test` | Verify the active provider authenticates and can be reached. |
| `upload` | Upload a memory card / save state / folder. Key options: `--platform`, `--titleid` (or `--game`), `--name` (or `--title`), `--type mc|state|folder`, `--file` (repeatable), `--description`, `--emulator`, `--icon`, `--no-cover-art`, `--index N` (replace), `--notes`, `--auto-merge`, `--no-auto-merge`. |
| `download` | Download a save archive: `--platform`, `--game`, `--archive`, `--output`. |
| `list platforms\|games\|saves` | Browse the backend. `--platform`, `--game` as needed. |
| `info` | Product, version, attribution and support links. |

All commands accept `--config <path>` to use a specific config file.

## Adding a save for a new / unsupported platform

You do **not** need to pre-create anything. The platform is just a folder name, so
any string works (`PS1`, `GBA`, `DS`, `SEGACD`, ...). On upload SaveHub
automatically:

1. creates the platform folder if it does not exist (e.g. `PS1/`),
2. creates the platform games index `PS1/README.md` (title id + game name),
3. creates the game folder named by the title id (e.g. `PS1/SLUS-00190/`),
4. writes the archive `01.zip` and the game folder `README.md` (saves index),
5. attaches cover art (`icon.jpg`) downloaded for the serial when available.

### Example: a PS1 memory card for *Oddworld: Abe's Oddysee*

The USA title id is `SLUS-00190`. It is read automatically from the memory card,
so you can omit `--game`. Even though no `PS1` folder exists yet, this single
command creates the whole structure and (with auto-merge on) publishes it:

```powershell
dotnet run --project src/SaveHub.Cli -- upload `
  --platform PS1 --type mc `
  --title "Oddworld: Abe's Oddysee" `
  --file ".reference\PS1\Oddworld - Abe's Oddysee (USA) (Rev 2)_1.mcd" `
  --description "Completed - all Mudokons rescued"
```

Resulting layout:

```
PS1/
  README.md                 # games index: | SLUS-00190 | Oddworld: Abe's Oddysee |
  SLUS-00190/
    README.md               # saves index: | 01.zip | Memory Card | Completed - ... |
    01.zip                  # memory card + embedded README.txt
    icon.jpg                # cover art auto-downloaded for SLUS-00190
```

Pass `--title` so the platform games index shows a friendly name next to the title
id. Prefer the file's serial as `--game` (e.g. `SLUS-00190`) so folders stay
consistent and sortable, mirroring the Apollo save database.

Tip: run `upload` with no options to be prompted for each value interactively
(handy when you don't remember the flags).

## Title IDs (game id / folder name)

You can always set the folder name yourself with **`--titleid`** (the id) or
**`--name`** (the game name). When you omit both, SaveHub resolves the folder in
this priority order:

| Priority | Source | Example |
| --- | --- | --- |
| 1 | `--titleid` (explicit) | `SCUS-97199` |
| 2 | Serial read from a PS1/PS2 memory card | `SLUS-00190` |
| 3 | `TITLE_ID`/`DISC_ID` from a PS3/PS4/PS5/PSP/Vita `PARAM.SFO` | `CUSA12345` |
| 4 | `--name` (game name) — used for Nintendo & anything without an id | `Pokemon Emerald` |
| 5 | Game name read from the save (`PARAM.SFO` `TITLE`, or a PS1/PS2 memory-card title) | `The Last of Us` |
| 6 | The `Unknown` folder (last resort) | `PS2/Unknown/` |

- **Nintendo (GBA, DS, ...):** a raw `.sav` has no title id or game code (that lives
  in the ROM), so pass **`--name`** and the game is filed by name. If you don't, it
  lands in `Unknown`.
- **PS1/PS2:** the serial is read from the card, so it matches the real title id.
- **PS3+:** include the save's `PARAM.SFO`; the title id (and, as a fallback, the
  game name) is read from it.

## Cover art

When you don't pass `--icon`, SaveHub tries to download cover art automatically by
serial and stores it as `icon.jpg`/`icon.png` in the game folder:

| Platform | Source | Serial format |
| --- | --- | --- |
| PS1 | `raw.githubusercontent.com/xlenore/psx-covers` | `SLUS-00190` (with dash) |
| PS2 | `raw.githubusercontent.com/xlenore/ps2-covers` | `SLUS-20073` (with dash) |
| PSP | `raw.githubusercontent.com/Andiweli/HexFlow-Covers` | `NPEG00001` (no dash) |

- Provide your own art with `--icon "C:\art\cover.png"` (this overrides the
  auto-download).
- Disable the auto-download with `--no-cover-art`.
- If no cover is found for a serial, the upload still succeeds without an icon.

Where to find cover art to upload yourself:

- [ramiabraham/cover-art-collection](https://github.com/ramiabraham/cover-art-collection) — many retro consoles.
- [Andiweli/HexFlow-Covers (PSP)](https://github.com/Andiweli/HexFlow-Covers/tree/main/Covers/PSP) — PSP covers named by serial (e.g. `NPEG00001.png`).
- [The Cover Project](https://www.thecoverproject.net/) — high-quality disc/box scans.

## Using the API from your own code

> For the complete API surface and a language-agnostic format specification (so you
> can reimplement SaveHub or add a backend), see [docs/API.md](docs/API.md).

Reference `SaveHub.Core` and `SaveHub.GitHub`, then:

```csharp
using SaveHub.Core;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Models;
using SaveHub.GitHub;

var settings = new GitHubProviderSettings
{
    Owner = "Uncle-Uee",
    Repository = "Emu-Saves-Backup",
    // Branch = "" -> use repo default
    // Token via env var SAVEHUB_GITHUB_TOKEN, or set settings.Token
    AutoMerge = false,
};

var provider = new GitHubSaveStorageProvider(settings);
var client = new SaveHubClient(provider);

var request = new SaveUploadRequest
{
    Platform = KnownPlatforms.Ps2,
    GameId = "SLUS-21274",
    SaveType = SaveType.MemoryCard,
    Files = ["C:/saves/Mcd001.ps2"],
    Description = "100% completion, all worlds cleared",
    GameTitle = "Kingdom Hearts II",
};

SaveUploadResult result = await client.UploadAsync(
    request,
    new UploadOptions { AutoMerge = false });

Console.WriteLine(result.Message);
if (result.PullRequestUrl is not null)
    Console.WriteLine(result.PullRequestUrl);
```

To only build the zip/side-car locally (e.g. for a custom frontend) use
`client.PrepareAsync(request)` and inspect the returned `PreparedSave`.

### Key types

- `SaveHubClient` — orchestrates prepare + upload.
- `SaveUploadRequest` — platform, game id, save type, files, description, icon,
  emulator, notes.
- `ISaveStorageProvider` — the backend abstraction.
- `SaveArchiveBuilder` / `SaveManifest` / `SaveNaming` — archive layout and naming.
- `SaveHubConfig` / `SaveHubConfigStore` — JSON configuration.

## Configuration

SaveHub uses **one config file**, used by the CLI (and any UI built on the API):

- Location: `%APPDATA%\SaveHub\savehub.config.json` (per-user).
- Override in the CLI with `--config <path>`.

The file records the **active provider** and a settings section per provider. The
library itself does not require the file — you can construct provider settings in
code. A sample is in [savehub.config.sample.json](savehub.config.sample.json):

```json
{
  "activeProvider": "github",
  "providers": {
    "github": {
      "owner": "Uncle-Uee",
      "repository": "Emu-Saves-Backup",
      "branch": "",
      "tokenEnvironmentVariable": "SAVEHUB_GITHUB_TOKEN",
      "autoMerge": false
    }
  }
}
```

Prefer supplying secrets (tokens/keys) through the environment variables named in
each provider's settings so they never land in the config file.

## Storage providers

SaveHub works with **GitHub**, **Supabase**, or **Google Drive** — the CLI (and any
UI built on the API) is provider-agnostic (it calls `SaveHubHost` which builds a
client from the active provider). Every provider produces the **same folder layout**
because all artifacts are built in `SaveHub.Core`.

> **Full account/setup walkthroughs** for all three providers are in the
> [provider setup guide](docs/PROVIDER-SETUP.md).

Switch the active provider any time:

```powershell
dotnet run --project src/SaveHub.Cli -- config use github|supabase|googledrive
```

### GitHub

```powershell
dotnet run --project src/SaveHub.Cli -- config github --owner Uncle-Uee --repo Emu-Saves-Backup
$env:SAVEHUB_GITHUB_TOKEN = "ghp_xxx"
```
Uploads are pull requests; auto-merge for owners/contributors. See the token
section below.

### Supabase

Create a Storage **bucket** (e.g. `saves`), then:

```powershell
dotnet run --project src/SaveHub.Cli -- config supabase --url https://YOUR-PROJECT.supabase.co --bucket saves --owner
$env:SAVEHUB_SUPABASE_KEY = "your-service-or-user-key"
dotnet run --project src/SaveHub.Cli -- config test
```
Owners publish directly; non-owners upload under a `pending/` prefix for review.
Use row-level-security policies to enforce who can write where.

### Google Drive (bring your own OAuth client, browser sign-in)

Google Drive uses the least-privilege **`drive.file`** scope: SaveHub can only see
and manage a folder **it creates** in your Drive (it can't read the rest of your
Drive). **Each user brings their own OAuth client** — create a free Google Cloud
project + Desktop OAuth client (kept in "Testing" mode), then:

```powershell
dotnet run --project src/SaveHub.Cli -- config google --client-id <CLIENT_ID> --owner
$env:SAVEHUB_GDRIVE_CLIENT_SECRET = "your-client-secret"
dotnet run --project src/SaveHub.Cli -- config google-login   # opens the browser
```

- SaveHub creates (and reuses) a folder named **`SaveHub`** at your Drive root — no
  folder ID needed. Change the name with `--folder-name`.
- Sign-in opens your browser; SaveHub receives a **session token** used while the
  app runs, valid for **~2.5 hours**, after which you sign in again.
- In the CLI the token is cached so subsequent commands don't reprompt.
- Owners publish directly; others upload to a `pending/` sub-folder.

> Full step-by-step (Google Cloud project, consent screen, Desktop client) is in the
> [provider setup guide](docs/PROVIDER-SETUP.md#google-drive).

## GitHub token & permissions

SaveHub needs a **GitHub personal access token (PAT)** to push a branch and open a
pull request on your `Uncle-Uee/Emu-Saves-Backup` repository.

### 1. Create the token

GitHub → **Settings** → **Developer settings** → **Personal access tokens**:

- **Fine-grained token** (recommended): set *Resource owner* to `Uncle-Uee`, limit
  it to the `Emu-Saves-Backup` repository, and grant these repository permissions:
  - *Contents*: **Read and write**
  - *Pull requests*: **Read and write**
- **Classic token** (simpler): select the `repo` scope (or `public_repo` since the
  repo is public).

Copy the token (it looks like `ghp_...` or `github_pat_...`).

### 2. Do you need `tokenEnvironmentVariable`?

No — it is **optional**. It is just the *name* of an environment variable that
SaveHub reads the token from, so your secret stays **out of the config file**.
The default name is `SAVEHUB_GITHUB_TOKEN`. You have two choices:

- **Recommended — environment variable:** leave `token` empty in the config and
  put the token in the env var named by `tokenEnvironmentVariable`.
- **Simple — inline:** set the `token` field directly in `savehub.config.json`
  (or `config github --token ...`). Less secure; the token sits in the file.

### 3. Set the environment variable (Windows PowerShell)

Current session only:

```powershell
$env:SAVEHUB_GITHUB_TOKEN = "ghp_xxx"
```

Persist it for your user (survives new terminals; reopen the terminal after):

```powershell
[Environment]::SetEnvironmentVariable("SAVEHUB_GITHUB_TOKEN", "ghp_xxx", "User")
```

Verify it is set:

```powershell
echo $env:SAVEHUB_GITHUB_TOKEN
```

You can name the variable anything — just make `tokenEnvironmentVariable` match it
(handy if you keep tokens for several projects).

## Auto-merge rules

- Every upload is submitted as a **pull request**.
- The `autoMerge` **config setting is the master switch** (enable it with
  `config github --auto-merge`). When it is on and the authenticated user has
  **write access** (owner or contributor), uploads merge automatically.
- Per upload you can override: `--no-auto-merge` opens a PR for review even when
  the config switch is on; `--auto-merge` forces it on for that run (still gated by
  the config switch and write access).
- Anyone **without** write access can only open a pull request; it must be
  reviewed and merged by the owner or a contributor. Their contribution is pushed
  to a **fork** automatically and a PR is opened against the target repo.
- Auto-merge is provided as a convenience and is **at the user's own risk**.

## Implementing your own provider

SaveHub is backend-agnostic through `ISaveStorageProvider`. **GitHub, Supabase, and
Google Drive are implemented** (see [Storage providers](#storage-providers) above to
configure them). Other candidates and how they map:

| Backend | Notes |
| --- | --- |
| **Firebase** | Cloud Storage + Firestore metadata; security rules for owner-only publish. |
| **S3 / R2 / Backblaze** | Object storage with a metadata index file per folder. |
| **Git (self-hosted / GitLab)** | Same PR/merge-request flow as GitHub. |

To add one, implement `ISaveStorageProvider`, add a settings class + factory (mirror
`GitHubProviderFactory`), register it in `SaveHubHost`, and it becomes available to
both frontends. The archive/zip/manifest building in `SaveHub.Core` is reused
unchanged. The two sections below are the **reference implementations** of the
shipped Supabase and Google Drive providers.

### Example: using SaveHub with Supabase

Supabase gives you an S3-like **Storage** bucket plus **Postgres + RLS**, which map
cleanly onto SaveHub concepts:

| SaveHub concept | Supabase equivalent |
| --- | --- |
| Repository | A Storage **bucket** (e.g. `saves`) |
| Folder tree `PS2/SLUS-.../01.zip` | Object **paths** inside the bucket |
| `PS2/SLUS-.../README.md` + `PS2/README.md` indexes | Plain objects, re-uploaded on change |
| GitHub write access (owner/contributor) | A row-level-security **policy** (owner vs. anon) |
| Pull request for review | Writing to a `pending/` path a maintainer later approves |
| Auto-merge | Owner writes straight to the published path |

**Suggested setup**

1. Create a bucket `saves` (public read if you want anonymous downloads).
2. RLS/policies: allow the owner (service role or a specific user) to write
   anywhere; allow others to write only under `pending/`. Nothing else can
   overwrite published saves.
3. Non-owners upload to `pending/PS2/SLUS-.../01.zip`; the owner reviews and copies
   it to the published path (the "merge").

**Settings + provider (illustrative)**

```csharp
using System.Net.Http.Json;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Archiving;
using SaveHub.Core.Models;

public sealed class SupabaseProviderSettings
{
    public string Url { get; set; } = "";          // https://<project>.supabase.co
    public string Bucket { get; set; } = "saves";
    public string? ApiKey { get; set; }             // prefer env var, like the GitHub token
    public string ApiKeyEnvironmentVariable { get; set; } = "SAVEHUB_SUPABASE_KEY";
    public bool IsOwner { get; set; }               // true => publish directly; false => write to pending/

    public string? ResolveKey() =>
        string.IsNullOrWhiteSpace(ApiKey)
            ? Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable)
            : ApiKey;
}

public sealed class SupabaseSaveStorageProvider : ISaveStorageProvider
{
    private readonly SupabaseProviderSettings _s;
    private readonly HttpClient _http;

    public SupabaseSaveStorageProvider(SupabaseProviderSettings settings, HttpClient? http = null)
    {
        _s = settings;
        var key = settings.ResolveKey()
            ?? throw new InvalidOperationException("No Supabase API key configured.");
        _http = http ?? new HttpClient { BaseAddress = new Uri(settings.Url.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Add("apikey", key);
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
    }

    public string Name => "supabase";

    public StorageProviderCapabilities Capabilities { get; } = new()
    {
        SupportsPullRequests = true,   // modeled as the pending/ prefix
        SupportsAutoMerge = true,      // owner writes straight to the published path
        SupportsBrowsing = true,
    };

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        var res = await ListRawAsync(prefix: "", ct);
        return new ConnectionTestResult
        {
            Success = res is not null,
            Target = $"{_s.Url}/{_s.Bucket}",
            HasWriteAccess = _s.IsOwner,
            AutoMergeEffective = _s.IsOwner,
            Message = res is not null ? "Connected to Supabase Storage." : "Could not reach the bucket.",
        };
    }

    public async Task<SaveUploadResult> UploadAsync(PreparedSave save, UploadOptions options, CancellationToken ct = default)
    {
        // Non-owners land under pending/ for review; owners publish directly (auto-merge).
        var publish = _s.IsOwner && (options.AutoMerge ?? true);
        var prefix = publish ? "" : "pending/";

        // Refresh indexes exactly like the GitHub provider does.
        var gameReadmePath = $"{save.GameFolder}/{GameReadmeFormatter.FileName}";
        var gameReadme = GameReadmeFormatter.Upsert(
            await ReadTextAsync(prefix + gameReadmePath, ct),
            save.Platform, save.GameId, save.GameTitle, save.Index, save.SaveType, save.Description);

        var readmePath = $"{SaveNaming.Sanitize(save.Platform)}/{PlatformReadmeFormatter.FileName}";
        var readme = PlatformReadmeFormatter.Upsert(
            await ReadTextAsync(prefix + readmePath, ct), save.Platform, save.GameId, save.GameTitle);

        var files = save.AllFiles().ToList();
        files.Add(new StorageFile(gameReadmePath, System.Text.Encoding.UTF8.GetBytes(gameReadme)));
        files.Add(new StorageFile(readmePath, System.Text.Encoding.UTF8.GetBytes(readme)));

        foreach (var f in files)
            await PutObjectAsync(prefix + f.Path, f.Content, ct);

        return new SaveUploadResult
        {
            Success = true,
            Merged = publish,
            ArchivePath = prefix + save.Archive.Path,
            Message = publish
                ? $"Published {save.Archive.Path}."
                : $"Submitted for review at pending/{save.Archive.Path}.",
        };
    }

    public async Task<int> GetNextIndexAsync(string platform, string gameId, SaveType type, CancellationToken ct = default)
    {
        var saves = await ListSavesAsync(platform, gameId, ct);
        return saves.Where(s => s.SaveType == type).Select(s => s.Index).DefaultIfEmpty(0).Max() + 1;
    }

    // ListPlatformsAsync / ListGamesAsync / ListSavesAsync call ListRawAsync(prefix)
    // and parse names with SaveNaming.TryParseArchiveName, mirroring the GitHub provider.
    // PutObjectAsync => POST storage/v1/object/{bucket}/{path} with header "x-upsert: true".
    // ReadTextAsync  => GET  storage/v1/object/{bucket}/{path} (returns null on 404).
    // ListRawAsync   => POST storage/v1/object/list/{bucket} { "prefix": prefix }.

    private async Task PutObjectAsync(string path, byte[] bytes, CancellationToken ct)
    {
        using var content = new ByteArrayContent(bytes);
        content.Headers.Add("x-upsert", "true");
        var resp = await _http.PostAsync($"storage/v1/object/{_s.Bucket}/{path}", content, ct);
        resp.EnsureSuccessStatusCode();
    }

    private async Task<string?> ReadTextAsync(string path, CancellationToken ct)
    {
        var resp = await _http.GetAsync($"storage/v1/object/{_s.Bucket}/{path}", ct);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(ct) : null;
    }

    private async Task<object?> ListRawAsync(string prefix, CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync(
            $"storage/v1/object/list/{_s.Bucket}", new { prefix, limit = 1000 }, ct);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<object>(ct) : null;
    }

    public Task<IReadOnlyList<string>> ListPlatformsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<string>> ListGamesAsync(string platform, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<SaveEntry>> ListSavesAsync(string platform, string gameId, CancellationToken ct = default) => throw new NotImplementedException();
}
```

Wire it into the config the same way as GitHub:

```json
{
  "activeProvider": "supabase",
  "providers": {
    "supabase": {
      "url": "https://YOUR-PROJECT.supabase.co",
      "bucket": "saves",
      "apiKeyEnvironmentVariable": "SAVEHUB_SUPABASE_KEY",
      "isOwner": true
    }
  }
}
```

Then use the exact same client code as GitHub — only the provider changes:

```csharp
var provider = new SupabaseSaveStorageProvider(settings);
var client = new SaveHubClient(provider);
await client.UploadAsync(request, new UploadOptions { AutoMerge = true });
```

Because the archive, manifest, per-game `README.md`, and platform `README.md` are all built
in `SaveHub.Core`, a Supabase bucket ends up with the **same folder structure** as
the GitHub repository.

### Example: using SaveHub with Google Drive

Google Drive stores a **folder tree** (like GitHub) but addresses items by opaque
**file IDs** rather than paths, so a provider resolves/creates each folder level and
keeps a small path→ID cache. Sharing a single root folder ("Anyone with the link")
gives you public downloads.

| SaveHub concept | Google Drive equivalent |
| --- | --- |
| Repository | A shared **root folder** (its folder ID) |
| Folder `PS2/SLUS-.../` | Nested Drive **folders**, resolved/created on demand |
| `01.zip`, game `README.md`, platform `README.md` | Drive **files** (upsert by name within a folder) |
| Owner / contributor write access | Drive **sharing permissions** on the root folder |
| Pull request for review | Upload to a `pending/` subfolder a maintainer approves |
| Auto-merge | Owner uploads straight into the published folder |

**Suggested setup**

1. Create a root folder in Drive and copy its **folder ID** (from the URL).
2. Auth with a **service account** (share the root folder with the service
   account's email) or an **OAuth** user token. Give write access only to the
   owner/maintainers; everyone else uploads into `pending/`.
3. Install the official client: `dotnet add package Google.Apis.Drive.v3`.

**Settings + provider (illustrative)**

```csharp
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Archiving;
using SaveHub.Core.Models;
using DriveData = Google.Apis.Drive.v3.Data;

public sealed class GoogleDriveProviderSettings
{
    public string RootFolderId { get; set; } = "";       // the shared root folder's ID
    public string? CredentialsPath { get; set; }          // service-account JSON, or use env var
    public string CredentialsEnvironmentVariable { get; set; } = "SAVEHUB_GDRIVE_CREDENTIALS";
    public bool IsOwner { get; set; }                     // true => publish directly; else pending/

    public string ResolveCredentialsPath() =>
        CredentialsPath
        ?? Environment.GetEnvironmentVariable(CredentialsEnvironmentVariable)
        ?? throw new InvalidOperationException("No Google Drive credentials configured.");
}

public sealed class GoogleDriveSaveStorageProvider : ISaveStorageProvider
{
    private readonly GoogleDriveProviderSettings _s;
    private readonly DriveService _drive;
    private readonly Dictionary<string, string> _folderCache = new(); // relative path -> folder ID

    public GoogleDriveSaveStorageProvider(GoogleDriveProviderSettings settings)
    {
        _s = settings;
        var credential = GoogleCredential
            .FromFile(settings.ResolveCredentialsPath())
            .CreateScoped(DriveService.Scope.Drive);
        _drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "SaveHub",
        });
    }

    public string Name => "googledrive";

    public StorageProviderCapabilities Capabilities { get; } = new()
    {
        SupportsPullRequests = true,   // modeled as the pending/ subfolder
        SupportsAutoMerge = true,      // owner uploads into the published folder
        SupportsBrowsing = true,
    };

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        var root = await _drive.Files.Get(_s.RootFolderId).ExecuteAsync(ct);
        return new ConnectionTestResult
        {
            Success = root is not null,
            Target = root?.Name,
            HasWriteAccess = _s.IsOwner,
            AutoMergeEffective = _s.IsOwner,
            Message = root is not null ? $"Connected to Drive folder '{root.Name}'." : "Root folder not found.",
        };
    }

    public async Task<SaveUploadResult> UploadAsync(PreparedSave save, UploadOptions options, CancellationToken ct = default)
    {
        var publish = _s.IsOwner && (options.AutoMerge ?? true);
        var prefix = publish ? "" : "pending/";

        // Rebuild the indexes just like the other providers.
        var gameReadmePath = $"{save.GameFolder}/{GameReadmeFormatter.FileName}";
        var gameReadme = GameReadmeFormatter.Upsert(
            await ReadTextAsync(prefix + gameReadmePath, ct),
            save.Platform, save.GameId, save.GameTitle, save.Index, save.SaveType, save.Description);

        var readmePath = $"{SaveNaming.Sanitize(save.Platform)}/{PlatformReadmeFormatter.FileName}";
        var readme = PlatformReadmeFormatter.Upsert(
            await ReadTextAsync(prefix + readmePath, ct), save.Platform, save.GameId, save.GameTitle);

        var files = save.AllFiles().ToList();
        files.Add(new StorageFile(gameReadmePath, System.Text.Encoding.UTF8.GetBytes(gameReadme)));
        files.Add(new StorageFile(readmePath, System.Text.Encoding.UTF8.GetBytes(readme)));

        foreach (var f in files)
            await UpsertFileAsync(prefix + f.Path, f.Content, ct);

        return new SaveUploadResult
        {
            Success = true,
            Merged = publish,
            ArchivePath = prefix + save.Archive.Path,
            Message = publish ? $"Published {save.Archive.Path}." : $"Submitted for review under pending/.",
        };
    }

    public async Task<int> GetNextIndexAsync(string platform, string gameId, SaveType type, CancellationToken ct = default)
    {
        var saves = await ListSavesAsync(platform, gameId, ct);
        return saves.Where(s => s.SaveType == type).Select(s => s.Index).DefaultIfEmpty(0).Max() + 1;
    }

    // Uploads a file by name into the folder for its path, creating folders and
    // replacing an existing file of the same name (upsert).
    private async Task UpsertFileAsync(string path, byte[] content, CancellationToken ct)
    {
        var slash = path.LastIndexOf('/');
        var folderId = await EnsureFolderAsync(slash < 0 ? "" : path[..slash], ct);
        var name = slash < 0 ? path : path[(slash + 1)..];

        var existingId = await FindChildIdAsync(folderId, name, ct);
        using var stream = new MemoryStream(content);
        if (existingId is null)
        {
            var meta = new DriveData.File { Name = name, Parents = new[] { folderId } };
            await _drive.Files.Create(meta, stream, "application/octet-stream").UploadAsync(ct);
        }
        else
        {
            await _drive.Files.Update(new DriveData.File(), existingId, stream, "application/octet-stream").UploadAsync(ct);
        }
    }

    // Resolves (creating as needed) the folder ID for a relative path, starting at RootFolderId.
    private async Task<string> EnsureFolderAsync(string relativePath, CancellationToken ct)
    {
        if (_folderCache.TryGetValue(relativePath, out var cached)) return cached;
        var parent = _s.RootFolderId;
        var accumulated = "";
        foreach (var segment in relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            accumulated = string.IsNullOrEmpty(accumulated) ? segment : $"{accumulated}/{segment}";
            if (!_folderCache.TryGetValue(accumulated, out var id))
            {
                id = await FindChildIdAsync(parent, segment, ct, foldersOnly: true)
                     ?? await CreateFolderAsync(parent, segment, ct);
                _folderCache[accumulated] = id;
            }
            parent = id;
        }
        return parent;
    }

    private async Task<string> CreateFolderAsync(string parentId, string name, CancellationToken ct)
    {
        var meta = new DriveData.File
        {
            Name = name,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new[] { parentId },
        };
        var created = await _drive.Files.Create(meta).ExecuteAsync(ct);
        return created.Id;
    }

    private async Task<string?> FindChildIdAsync(string parentId, string name, CancellationToken ct, bool foldersOnly = false)
    {
        var q = $"'{parentId}' in parents and name = '{name.Replace("'", "\\'")}' and trashed = false";
        if (foldersOnly) q += " and mimeType = 'application/vnd.google-apps.folder'";
        var list = _drive.Files.List();
        list.Q = q;
        list.Fields = "files(id,name)";
        var result = await list.ExecuteAsync(ct);
        return result.Files.FirstOrDefault()?.Id;
    }

    private async Task<string?> ReadTextAsync(string path, CancellationToken ct)
    {
        var slash = path.LastIndexOf('/');
        var folderId = await EnsureFolderAsync(slash < 0 ? "" : path[..slash], ct);
        var id = await FindChildIdAsync(folderId, slash < 0 ? path : path[(slash + 1)..], ct);
        if (id is null) return null;
        using var stream = new MemoryStream();
        await _drive.Files.Get(id).DownloadAsync(stream, ct);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    // ListPlatformsAsync / ListGamesAsync / ListSavesAsync enumerate folder children
    // via Files.List and parse names with SaveNaming.TryParseArchiveName.
    public Task<IReadOnlyList<string>> ListPlatformsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<string>> ListGamesAsync(string platform, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<SaveEntry>> ListSavesAsync(string platform, string gameId, CancellationToken ct = default) => throw new NotImplementedException();
}
```

Config and usage mirror the other providers:

```json
{
  "activeProvider": "googledrive",
  "providers": {
    "googledrive": {
      "rootFolderId": "1AbCdEfGhIjKlMnOpQrStUvWxYz",
      "credentialsEnvironmentVariable": "SAVEHUB_GDRIVE_CREDENTIALS",
      "isOwner": true
    }
  }
}
```

```csharp
var provider = new GoogleDriveSaveStorageProvider(settings);
var client = new SaveHubClient(provider);
await client.UploadAsync(request, new UploadOptions { AutoMerge = true });
```

The shared Drive root then mirrors the exact same `PLATFORM/GAMEID/NN.zip` layout,
with per-game and platform `README.md` indexes, as the GitHub repository.

## License & support

SaveHub is open source under the **MIT License** — see [LICENSE](LICENSE). You are
free to use it, including in commercial and closed-source projects; just keep the
copyright notice so the author gets credit.

If it saves you time, please support development:
**https://pay.yoco.com/savehub**.
