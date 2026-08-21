# SaveHub.Hosting

Provider aggregation for **SaveHub**. A single call builds a `SaveHubClient` for whichever
backend your configuration selects, so your app depends only on `SaveHub.Core` + this
package — never on a specific provider.

Installing this package brings in every SaveHub backend: **GitHub, GitLab, Bitbucket,
Supabase, and Google Drive**.

## Install

```sh
dotnet add package SaveHub.Hosting
```

## Usage

```csharp
using SaveHub.Core;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Configuration;
using SaveHub.Hosting;

SaveHubConfig config = new SaveHubConfigStore(SaveHubConfigStore.DefaultPath).Load();

SaveHubClient? client = SaveHubHost.TryCreateClient(config, out string error);
if (client is not null)
{
    await client.UploadAsync(request, new UploadOptions());
}
```

- `SaveHubHost.Providers` — the available backends.
- `CreateClient` / `TryCreateClient` — switch on `config.ActiveProvider`; an overload
  accepts an `ICoverArtResolver` (e.g. a caching resolver).

## Links & license

- Repository & full docs: https://github.com/uncle-uee/SaveHub
- License: **LGPL-3.0-or-later**
- Support: https://pay.yoco.com/savehub
