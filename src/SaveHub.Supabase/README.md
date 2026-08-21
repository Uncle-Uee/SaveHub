# SaveHub.Supabase

Supabase Storage backend for **SaveHub**. Uploads saves to a Supabase Storage bucket via
its REST API. The bucket owner publishes to the bucket root; other users upload under a
`pending/` prefix.

Use with [`SaveHub.Core`](https://www.nuget.org/packages/SaveHub.Core).

## Install

```sh
dotnet add package SaveHub.Supabase
```

## Usage

```csharp
using SaveHub.Core;
using SaveHub.Core.Abstractions;
using SaveHub.Supabase;

ISaveStorageProvider provider = SupabaseProviderFactory.Create(config);
SaveHubClient client = new SaveHubClient(provider);
```

Or let **`SaveHub.Hosting`** pick the provider from configuration.

Configure via `SupabaseProviderSettings` (project URL, bucket, API key, is-owner).
Prefer the `SAVEHUB_SUPABASE_KEY` environment variable for the key.

## Links & license

- Repository & full docs: https://github.com/uncle-uee/SaveHub
- License: **LGPL-3.0-or-later**
