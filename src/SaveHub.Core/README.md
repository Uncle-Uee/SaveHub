# SaveHub.Core

Core library for **SaveHub** — a .NET toolkit for backing up emulator and game saves
(**memory cards**, **save states**, and **save folders**) to cloud storage you control.

`SaveHub.Core` is backend-agnostic: it builds the archive, detects metadata (title ids,
game names, cover art), and orchestrates uploads/downloads through the
`ISaveStorageProvider` abstraction. Pair it with a provider package
(`SaveHub.GitHub`, `SaveHub.GitLab`, `SaveHub.Bitbucket`, `SaveHub.Supabase`,
`SaveHub.GoogleDrive`) — or use `SaveHub.Hosting` to select one from configuration.

## Install

```sh
dotnet add package SaveHub.Core
```

## What's inside

- **`SaveHubClient`** — prepares and uploads a save; also download, edit/replace, delete,
  list, and read game names + cover art.
- **Models** — `SaveType`, `SaveUploadRequest`, `SaveEntry`, `KnownPlatforms`.
- **Archiving** — `SaveNaming`, `SaveArchiveBuilder`, `SaveManifest`, PS1/PS2 memory-card
  and `PARAM.SFO` detection, and cover-art resolution + on-disk caching.
- **Configuration** — `SaveHubConfig`, `SaveHubConfigStore`.
- **Abstraction** — implement `ISaveStorageProvider` to add your own backend.

## Usage

```csharp
using SaveHub.Core;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Models;

// provider is any ISaveStorageProvider (see the SaveHub.* backend packages)
SaveHubClient client = new SaveHubClient(provider);

SaveUploadRequest request = new SaveUploadRequest
{
    Platform = "PS2",
    GameId = "SLUS-20488",
    SaveType = SaveType.MemoryCard,
    Files = [@"C:\saves\card.ps2"],
    Description = "Chapter 5, all upgrades",
};

SaveUploadResult result = await client.UploadAsync(request, new UploadOptions());
```

## Links & license

- Repository & full docs (`docs/API.md`): https://github.com/uncle-uee/SaveHub
- License: **LGPL-3.0-or-later**
- Support: https://pay.yoco.com/savehub
