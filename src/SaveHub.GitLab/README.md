# SaveHub.GitLab

GitLab storage backend for **SaveHub**. Uploads saves via the GitLab REST v4 API
(commits API → merge request → optional merge). Non-members are given a fork
automatically; works with gitlab.com or a self-hosted instance.

Use with [`SaveHub.Core`](https://www.nuget.org/packages/SaveHub.Core).

## Install

```sh
dotnet add package SaveHub.GitLab
```

## Usage

```csharp
using SaveHub.Core;
using SaveHub.Core.Abstractions;
using SaveHub.GitLab;

ISaveStorageProvider provider = GitLabProviderFactory.Create(config);
SaveHubClient client = new SaveHubClient(provider);
```

Or let **`SaveHub.Hosting`** pick the provider from configuration.

Configure via `GitLabProviderSettings` (base URL, owner/group, repository, branch, token,
auto-merge). Prefer the `SAVEHUB_GITLAB_TOKEN` environment variable for the token.

## Links & license

- Repository & full docs: https://github.com/uncle-uee/SaveHub
- License: **LGPL-3.0-or-later**
