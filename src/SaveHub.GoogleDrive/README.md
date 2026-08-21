# SaveHub.GoogleDrive

Google Drive storage backend for **SaveHub**. Uploads saves to Google Drive using
`Google.Apis.Drive.v3` with browser-based OAuth. It uses the least-privilege
**`drive.file`** scope and only touches the folder it creates. Users bring their own
Google OAuth client.

Use with [`SaveHub.Core`](https://www.nuget.org/packages/SaveHub.Core).

## Install

```sh
dotnet add package SaveHub.GoogleDrive
```

## Usage

```csharp
using SaveHub.Core;
using SaveHub.Core.Abstractions;
using SaveHub.GoogleDrive;

// Sign in first (browser loopback OAuth, drive.file scope):
//   await GoogleDriveAuthenticator.SignInAsync(...);
ISaveStorageProvider provider = GoogleDriveProviderFactory.Create(config);
SaveHubClient client = new SaveHubClient(provider);
```

Or let **`SaveHub.Hosting`** pick the provider from configuration.

Configure via `GoogleDriveProviderSettings` (root folder name, client id, client secret,
is-owner). Prefer the `SAVEHUB_GDRIVE_CLIENT_SECRET` environment variable for the secret.

## Links & license

- Repository & full docs: https://github.com/uncle-uee/SaveHub
- License: **LGPL-3.0-or-later**
