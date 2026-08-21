# Credits & third-party licenses

SaveHub is built with the help of these open-source projects. Each is used under its
own license (linked below); their copyright notices are retained here as required.

## API libraries & CLI (this repository)

| Library | Used by | License | Copyright |
| --- | --- | --- | --- |
| [Octokit.net](https://github.com/octokit/octokit.net) | `SaveHub.GitHub` | MIT | © GitHub, Inc. and contributors |
| [Google APIs Client Library for .NET](https://github.com/googleapis/google-api-dotnet-client) (`Google.Apis.Drive.v3`, `Google.Apis.Auth`, …) | `SaveHub.GoogleDrive` | Apache-2.0 | © Google LLC |
| [Spectre.Console](https://github.com/spectreconsole/spectre.console) (`Spectre.Console`, `Spectre.Console.Cli`) | `SaveHub.Cli` | MIT | © Patrik Svensson, Phil Scott, Nils Andresen and contributors |
| [.NET runtime & BCL](https://github.com/dotnet/runtime) | all | MIT | © .NET Foundation and Contributors |

The GitLab, Bitbucket, and Supabase providers use only the .NET base class library
(`HttpClient`) — no additional third-party packages.

Full license texts are reproduced in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt),
and are also available at each project's repository linked above. SaveHub's own license
is in [LICENSE](LICENSE) (LGPL-3.0-or-later for the libraries and CLI).
