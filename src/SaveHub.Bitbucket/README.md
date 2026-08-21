# SaveHub.Bitbucket

Bitbucket storage backend for **SaveHub**. Uploads saves via the Bitbucket REST 2.0 API
(create branch → multi-file `POST /src` commit → pull request → optional merge).
Non-members are given a fork automatically.

Use with [`SaveHub.Core`](https://www.nuget.org/packages/SaveHub.Core).

## Install

```sh
dotnet add package SaveHub.Bitbucket
```

## Usage

```csharp
using SaveHub.Core;
using SaveHub.Core.Abstractions;
using SaveHub.Bitbucket;

ISaveStorageProvider provider = BitbucketProviderFactory.Create(config);
SaveHubClient client = new SaveHubClient(provider);
```

Or let **`SaveHub.Hosting`** pick the provider from configuration.

Configure via `BitbucketProviderSettings` (workspace, repository, branch, username,
app password, auto-merge). Prefer the `SAVEHUB_BITBUCKET_APP_PASSWORD` environment
variable for the app password.

## Links & license

- Repository & full docs: https://github.com/uncle-uee/SaveHub
- License: **LGPL-3.0-or-later**
