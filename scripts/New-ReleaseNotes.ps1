<#
.SYNOPSIS
Turns reviewed, user-focused notes into the description used by a release draft.
.PARAMETER Version
Keeps the release label consistent with the version built into the app.
.PARAMETER Tag
Rejects version tags that would label the downloads with the wrong version.
.PARAMETER NotesPath
Selects the reviewed Markdown without relying on the caller's working directory.
.PARAMETER OutputPath
Keeps the generated description beside the release assets or isolated test files.
.PARAMETER Repository
Points download links at the repository that owns the release.
#>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Tag = "v$Version",
    [string]$NotesPath = (Join-Path $PSScriptRoot '..\docs\RELEASE_NOTES.md'),
    [Parameter(Mandatory)][string]$OutputPath,
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$Repository = 'sytone/QuickText'
)

$ErrorActionPreference = 'Stop'
$versionPattern = '(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)'
if ($Version -cnotmatch "\A$versionPattern\z" -or $Tag -cne "v$Version") {
    throw "Release tag '$Tag' must be v followed by the app's stable three-part version '$Version'."
}

$notes = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $NotesPath).Path).Trim()
$notes = [regex]::Replace($notes, '\A# [^\r\n]+\r?\n+', '').Trim()
foreach ($section in @('Overview', 'Highlights', 'Fixes and improvements', 'Upgrading', 'Known limitations')) {
    $sectionMatches = [regex]::Matches($notes, "(?ms)^## $([regex]::Escape($section))\r?`n(.*?)(?=^## |\z)")
    if ($sectionMatches.Count -ne 1 -or [string]::IsNullOrWhiteSpace($sectionMatches[0].Groups[1].Value)) {
        throw "Release notes need one non-empty '## $section' section."
    }
}
if ($notes -cmatch '\b(TODO|TBD)\b|\{\{|\}\}') {
    throw 'Replace release-note placeholders before preparing downloads.'
}

$download = "https://github.com/$Repository/releases/download/$Tag"
$description = @"
# QuickText $Tag

$notes

## Downloads

Windows 10 or 11, 64-bit (x64):

| Download | Choose this when |
| :--- | :--- |
| [QuickText.exe]($download/QuickText.exe) | Recommended: includes .NET; no runtime installation needed. |
| [QuickText-needs-dotnet.exe]($download/QuickText-needs-dotnet.exe) | Smaller download; needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0/runtime). |

[License][license] and [third-party notices][notices] accompany both builds.
These executables are not code-signed.

## Verify your download

Download [SHA256SUMS.txt][checksums] from this same release.
Run the following command and compare the hash with the matching filename:

``````powershell
Get-FileHash .\QuickText.exe -Algorithm SHA256
``````

Use the other executable's filename if you chose the smaller build.
The checksums detect damaged or changed downloads; they are not a code signature.

[license]: $download/LICENSE
[notices]: $download/THIRD-PARTY-NOTICES.md
[checksums]: $download/SHA256SUMS.txt
"@
$fullOutputPath = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory((Split-Path -Parent $fullOutputPath)) | Out-Null
[IO.File]::WriteAllText($fullOutputPath, $description.Replace("`r`n", "`n") + "`n")
