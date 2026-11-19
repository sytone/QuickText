<#
.SYNOPSIS
Protects release labels and note quality without building or publishing a release.
#>
#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$generator = Join-Path $PSScriptRoot 'New-ReleaseNotes.ps1'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) "QuickText-release-tests-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $testRoot | Out-Null

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Rejected([scriptblock]$Action, [string]$Expected) {
    try { & $Action }
    catch {
        if ($_.Exception.Message -like "*$Expected*") { return }
        throw
    }
    throw "Expected a release validation error containing '$Expected'."
}

try {
    $source = Join-Path $testRoot 'reviewed.md'
    $output = Join-Path $testRoot 'generated.md'
    $notes = @"
## Overview

Keep useful notes close without covering the rest of your work.

## Highlights

- Fold the editor into a movable icon, then double-click to return to your note.

## Fixes and improvements

- Large pastes no longer wait for a full spelling scan.

## Upgrading

Exit the app before replacing the executable. Your note files stay in place.

## Known limitations

Windows x64 only. Markdown is highlighted source, not a rendered preview.
"@
    [IO.File]::WriteAllText($source, "# Release notes`n`n$notes")
    $arguments = @{ Version = '2.3.4'; Tag = 'v2.3.4'; NotesPath = $source; OutputPath = $output; Repository = 'example/QuickText' }
    & $generator @arguments
    $result = [IO.File]::ReadAllText($output)
    Assert-True ($result.Contains('# QuickText v2.3.4') -and
        $result.Contains($notes.Replace("`r`n", "`n").Trim())) 'Reviewed notes or release version were lost.'
    Assert-True ($result.StartsWith("# QuickText v2.3.4`n`n## Overview")) 'The generated title has extra blank lines or lost the overview.'
    Assert-True (-not $result.Contains('# Release notes')) 'The source title leaked into the versioned description.'
    Assert-True ($result.Contains('https://github.com/example/QuickText/releases/download/v2.3.4/QuickText-needs-dotnet.exe')) 'Download links point at the wrong repository, tag, or variant.'
    Assert-True ($result.Contains('SHA256SUMS.txt') -and $result.Contains('not code-signed') -and
        $result.Contains('THIRD-PARTY-NOTICES.md') -and $result.Contains('Get-FileHash')) 'Download verification or licensing guidance is missing.'

    $arguments.Tag = 'v2.3.5'
    Assert-Rejected { & $generator @arguments } 'must be v followed'
    $arguments.Version = '02.3.4'
    $arguments.Tag = 'v02.3.4'
    Assert-Rejected { & $generator @arguments } 'must be v followed'
    $arguments.Version = '2.3.4'
    $arguments.Tag = 'v2.3.4-rc.1'
    Assert-Rejected { & $generator @arguments } 'must be v followed'
    $arguments.Tag = 'v2.3.4'

    [IO.File]::WriteAllText($source, $notes.Replace('## Upgrading', '## Internal changes'))
    Assert-Rejected { & $generator @arguments } "non-empty '## Upgrading'"
    [IO.File]::WriteAllText($source, [regex]::Replace($notes, '(?s)## Known limitations.*', "## Known limitations`n`n"))
    Assert-Rejected { & $generator @arguments } "non-empty '## Known limitations'"
    [IO.File]::WriteAllText($source, $notes + "`n## Overview`nRepeated summary.")
    Assert-Rejected { & $generator @arguments } "non-empty '## Overview'"
    [IO.File]::WriteAllText($source, $notes.Replace('Keep useful notes close', 'TODO'))
    Assert-Rejected { & $generator @arguments } 'placeholders'

    Write-Output 'Release-note tests passed: reviewed prose, download links, verification guidance, version safety, required sections, and placeholders.'
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}

& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-AgentRelease.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Agent release tests failed.' }
