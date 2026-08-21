# SaveHub.GitHub

GitHub storage backend for **SaveHub**. Uploads saves to a GitHub repository through the
Git Data API (blob → tree → commit → branch → pull request, with optional auto-merge).
Non-collaborators are given a fork automatically.

Use with [`SaveHub.Core`](https://www.nuget.org/packages/SaveHub.Core).

## Install

```sh
dotnet add package SaveHub.GitHub
```

## Usage

```csharp
using SaveHub.Core;
using SaveHub.Core.Abstractions;
using SaveHub.GitHub;

ISaveStorageProvider provider = GitHubProviderFactory.Create(config);
SaveHubClient client = new SaveHubClient(provider);
```

Or let **`SaveHub.Hosting`** pick the provider from configuration.

Configure via `GitHubProviderSettings` (owner, repository, branch, token, auto-merge).
Prefer the `SAVEHUB_GITHUB_TOKEN` environment variable over storing the token on disk.

## Links & license

- Repository & full docs: https://github.com/uncle-uee/SaveHub
- License: **LGPL-3.0-or-later**
