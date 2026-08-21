<#
.SYNOPSIS
	Packs the SaveHub backend "API" libraries into the repo-local NuGet feed.

.DESCRIPTION
	Builds NuGet packages for the seven backend libraries and drops them into
	.\local-feed (configured via Directory.Build.props / nuget.config).

	The seven packages are:
		SaveHub.Core, SaveHub.Hosting, SaveHub.GitHub, SaveHub.GitLab,
		SaveHub.Bitbucket, SaveHub.Supabase, SaveHub.GoogleDrive

	Frontends (WinForms, or any third-party UI) consume these packages via
	PackageReference instead of ProjectReference.

.PARAMETER Version
	Overrides the package version (default comes from Directory.Build.props).
	Bump this when consumers should pick up freshly changed library code.

.EXAMPLE
	./pack-api.ps1

.EXAMPLE
	./pack-api.ps1 -Version 1.0.1
#>
param(
	[string]$Version,
	[string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$projects = @(
	"src/SaveHub.Core/SaveHub.Core.csproj",
	"src/SaveHub.Hosting/SaveHub.Hosting.csproj",
	"src/SaveHub.GitHub/SaveHub.GitHub.csproj",
	"src/SaveHub.GitLab/SaveHub.GitLab.csproj",
	"src/SaveHub.Bitbucket/SaveHub.Bitbucket.csproj",
	"src/SaveHub.Supabase/SaveHub.Supabase.csproj",
	"src/SaveHub.GoogleDrive/SaveHub.GoogleDrive.csproj"
)

$versionArg = @()
if ($Version) {
	$versionArg = @("-p:Version=$Version")
	Write-Host "Packing SaveHub API packages as version $Version ($Configuration)..." -ForegroundColor Cyan
} else {
	Write-Host "Packing SaveHub API packages ($Configuration)..." -ForegroundColor Cyan
}

foreach ($project in $projects) {
	Write-Host "  pack $project" -ForegroundColor DarkGray
	dotnet pack (Join-Path $root $project) -c $Configuration @versionArg
}

# When re-packing with a version that already exists, NuGet may serve the
# cached copy. Clearing the SaveHub.* packages from the global cache forces a
# fresh restore in consuming projects.
$globalPackages = (dotnet nuget locals global-packages --list) -replace '^.*?:\s*', ''
if ($globalPackages -and (Test-Path $globalPackages)) {
	Get-ChildItem -Path $globalPackages -Directory -Filter "savehub.*" -ErrorAction SilentlyContinue |
		ForEach-Object {
			Write-Host "  clearing cache: $($_.Name)" -ForegroundColor DarkGray
			Remove-Item -Recurse -Force $_.FullName
		}
}

Write-Host "Done. Packages are in $(Join-Path $root 'local-feed')." -ForegroundColor Green
