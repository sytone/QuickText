<#
.SYNOPSIS
Builds the exact downloads and reviewed description used by the release workflow.
.DESCRIPTION
Only creates local files. Publishing a GitHub draft remains a separate workflow
step, so a failed build or a manual preview cannot create a public release.
.PARAMETER Tag
Prevents a pushed tag from disagreeing with the version embedded in the app.
.PARAMETER OutputDirectory
Uses an empty destination so older downloads cannot leak into a new release.
#>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$Tag,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\publish\release')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\QuickText\QuickText.csproj'
$version = ([xml][IO.File]::ReadAllText($project)).Project.PropertyGroup.Version |
    Where-Object { $_ }
if (@($version).Count -ne 1) {
    throw 'The app project must declare exactly one release version.'
}
if (-not $Tag) { $Tag = "v$version" }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and
    @((Get-ChildItem -LiteralPath $output -Force)).Count -gt 0) {
    throw "Release output must be empty: $output"
}
$repository = if ($env:GITHUB_REPOSITORY) { $env:GITHUB_REPOSITORY } else { 'sytone/QuickText' }
& (Join-Path $PSScriptRoot 'New-ReleaseNotes.ps1') -Version $version -Tag $Tag `
    -OutputPath (Join-Path $output 'RELEASE_NOTES.md') -Repository $repository

$staging = Join-Path ([IO.Path]::GetTempPath()) "QuickText-release-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $staging | Out-Null
try {
    foreach ($variant in @(
        @{ Name = 'full'; SelfContained = 'true'; FileName = 'QuickText.exe' },
        @{ Name = 'small'; SelfContained = 'false'; FileName = 'QuickText-needs-dotnet.exe' }
    )) {
        $publish = Join-Path $staging $variant.Name
        & dotnet publish $project -c Release -r win-x64 --self-contained $variant.SelfContained `
            -p:PublishSingleFile=true -p:ContinuousIntegrationBuild=true -warnaserror --nologo -o $publish
        if ($LASTEXITCODE -ne 0) {
            throw "Publishing $($variant.Name) failed (exit code $LASTEXITCODE)."
        }
        $exe = Join-Path $publish 'QuickText.exe'
        if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
            throw "Publishing $($variant.Name) did not produce QuickText.exe."
        }
        $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion
        if ($fileVersion -ne "$version.0") {
            throw "Published executable version '$fileVersion' does not match '$version.0'."
        }
        Copy-Item -LiteralPath $exe -Destination (Join-Path $output $variant.FileName)
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE'), (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.md') `
        -Destination $output
    $checksums = foreach ($name in @('QuickText.exe', 'QuickText-needs-dotnet.exe', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) {
        $hash = (Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $name"
    }
    [IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'), ($checksums -join "`n") + "`n")
    Write-Output "Release preview ready: $output"
}
finally {
    Remove-Item -LiteralPath $staging -Recurse -Force
}
