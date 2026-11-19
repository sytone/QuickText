<#
.SYNOPSIS
Exercises release decisions and orchestration with offline git and GitHub doubles.
#>
#Requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Release.Common.ps1')

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-Rejected([scriptblock]$Action, [string]$Expected) {
    try { & $Action }
    catch {
        if ($_.Exception.Message.Contains($Expected)) { return }
        throw
    }
    throw "Expected rejection: $Expected"
}

$history = "## [1.0.0] - 2030-01-01`n`n### Added`n`n- Original feature."
foreach ($case in @(
    @{ Changes = "### Added`n`n- New feature."; Version = '1.1.0' },
    @{ Changes = "### Fixed`n`n- A correction."; Version = '1.0.1' },
    @{ Changes = "### Breaking changes`n`n- Changed data format."; Version = '2.0.0' },
    @{ Changes = "### Changed`n`n- BREAKING CHANGE: Removed an old format."; Version = '2.0.0' },
    @{ Changes = "### Added`n`n### Fixed`n`n- A correction."; Version = '1.0.1' }
)) {
    $plan = Get-ReleaseVersionPlan '1.0.0' "## [Unreleased]`n`n$($case.Changes)`n`n$history" @('v1.0.0')
    Assert-True ($plan.Version -eq $case.Version -and -not $plan.Prepared) "Wrong version for $($case.Changes)"
}
$prepared = Get-ReleaseVersionPlan '1.0.0' "## [Unreleased]`n`n$history" @('v1.0.0')
Assert-True $prepared.Prepared 'An already prepared version was bumped again.'
$declared = Get-ReleaseVersionPlan '1.5.0' "## [Unreleased]`n`n### Added`n`n- New feature.`n`n$history" @('v1.0.0')
Assert-True ($declared.Version -eq '1.5.0') 'An already declared higher version was discarded.'
Assert-Rejected { Get-ReleaseVersionPlan '1.0.0' "## [Unreleased]`n`n$history" @('v2.0.0') } 'older than release history'
$check = [pscustomobject]@{
    headRefOid = 'head'; baseRefOid = 'base'; reviewDecision = ''
    statusCheckRollup = @([pscustomobject]@{ __typename = 'CheckRun'; name = 'build'; status = 'IN_PROGRESS'; conclusion = '' })
}
Assert-True (-not (Test-ReleaseChecks $check 'head' 'base')) 'A pending build was accepted.'
$check.statusCheckRollup[0].status = 'COMPLETED'
$check.statusCheckRollup[0].conclusion = 'SUCCESS'
Assert-True (Test-ReleaseChecks $check 'head' 'base') 'A successful build was rejected.'
Assert-Rejected { Test-ReleaseChecks $check 'other' 'base' } 'head changed'
Assert-Rejected { Test-ReleaseChecks $check 'head' 'other' } 'Main advanced'
$check.statusCheckRollup[0].conclusion = 'FAILURE'
Assert-Rejected { Test-ReleaseChecks $check 'head' 'base' } 'PR check failed'

$root = Join-Path ([IO.Path]::GetTempPath()) "QuickText-agent-tests-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path (Join-Path $root 'src\QuickText'), (Join-Path $root 'docs'), (Join-Path $root '.github') | Out-Null
$global:ReleaseFixture = @{
    Root = $root; Head = ('a' * 40); Main = ('a' * 40); TagExists = $false
    TagCommit = ''; TagPushed = $false; Corrupt = $false
    Generator = (Join-Path $PSScriptRoot 'New-ReleaseNotes.ps1')
    Calls = [Collections.Generic.List[string]]::new()
}

function global:git {
    $global:LASTEXITCODE = 0
    $command = $args[2..($args.Count - 1)] -join ' '
    $f = $global:ReleaseFixture
    $f.Calls.Add("git $command")
    switch -Regex ($command) {
        '^fetch |^add |^diff |^merge-base ' { return }
        '^tag --list$' { 'v1.0.0'; if ($f.TagExists) { 'v1.1.0' }; return }
        '^rev-parse HEAD$' { return $f.Head }
        '^rev-parse origin/main$' { return $f.Main }
        '^status --porcelain$' { return }
        '^branch --show-current$' { return 'dev/test-release' }
        '^remote get-url origin$' { return 'https://github.com/sytone/QuickText.git' }
        '^commit ' { $f.Head = 'b' * 40; return }
        '^ls-remote ' { if ($f.TagPushed) { return "tag-object`trefs/tags/v1.1.0" }; return }
        '^show-ref ' { if (-not $f.TagExists) { $global:LASTEXITCODE = 1 }; return }
        '^tag -a ' {
            if (-not $command.StartsWith("tag -a v1.1.0 $($f.Main) -m ")) { throw 'The tag does not target the checked merge commit.' }
            $f.TagExists = $true
            $f.TagCommit = $f.Main
            return
        }
        '^rev-parse v1.1.0' { return $f.TagCommit }
        '^push origin refs/tags/' { $f.TagPushed = $true; return }
        '^push --set-upstream ' { return }
        default { throw "Unexpected fake git command: $command" }
    }
}
function global:dotnet {
    $global:LASTEXITCODE = 0
    if (($args -join ' ') -ne 'build QuickText.sln -c Release -warnaserror') { throw 'Unexpected fake dotnet invocation.' }
}
function global:gh {
    $global:LASTEXITCODE = 0
    $command = $args -join ' '
    $f = $global:ReleaseFixture
    $f.Calls.Add("gh $command")
    switch -Regex ($command) {
        '^pr list ' { return '[]' }
        '^pr create ' { return 'https://github.com/sytone/QuickText/pull/999' }
        '^pr view .*mergeCommit' { return (@{ state = 'MERGED'; mergeCommit = @{ oid = $f.Main } } | ConvertTo-Json -Depth 4) }
        '^pr view ' {
            return (@{ headRefOid = $f.Head; baseRefOid = $f.Main; reviewDecision = ''
                statusCheckRollup = @(@{ __typename = 'CheckRun'; name = 'build'; status = 'COMPLETED'; conclusion = 'SUCCESS' })
            } | ConvertTo-Json -Depth 5)
        }
        '^pr merge ' {
            if (-not $command.Contains("--match-head-commit $($f.Head)") -or $command.Contains('--admin')) { throw 'Unsafe merge.' }
            $f.Main = 'c' * 40
            return
        }
        '^run list ' {
            if (-not $command.Contains("--commit $($f.Main)") -or -not $command.Contains('--event push')) {
                throw 'The workflow wait did not select the released commit and push event.'
            }
            return (@(@{ databaseId = 1; status = 'completed'; conclusion = 'success'; headSha = $f.Main; url = 'https://example.test/run' }) | ConvertTo-Json -AsArray)
        }
        '^release view ' {
            & $f.Generator -Version '1.1.0' -OutputPath (Join-Path $f.Root 'expected.md') -NotesPath (Join-Path $f.Root 'docs\RELEASE_NOTES.md')
            return (@{ tagName = 'v1.1.0'; isDraft = $true; body = [IO.File]::ReadAllText((Join-Path $f.Root 'expected.md')); assets = @(); url = 'https://example.test/draft' } | ConvertTo-Json)
        }
        '^release download ' {
            $directory = $args[[array]::IndexOf($args, '--dir') + 1]
            New-Item -ItemType Directory -Path $directory | Out-Null
            $lines = foreach ($name in @('LICENSE', 'QuickText.exe', 'QuickText-needs-dotnet.exe', 'THIRD-PARTY-NOTICES.md')) {
                [IO.File]::WriteAllText((Join-Path $directory $name), "fixture $name")
                "$((Get-FileHash -LiteralPath (Join-Path $directory $name)).Hash.ToLowerInvariant())  $name"
            }
            [IO.File]::WriteAllLines((Join-Path $directory 'SHA256SUMS.txt'), [string[]]$lines)
            if ($f.Corrupt) { [IO.File]::WriteAllText((Join-Path $directory 'QuickText.exe'), 'changed after hashing') }
            return
        }
        default { throw "Unexpected fake GitHub command: $command" }
    }
}

try {
    [IO.File]::WriteAllText((Join-Path $root '.git'), 'isolated fixture')
    [IO.File]::WriteAllText((Join-Path $root 'src\QuickText\QuickText.csproj'), '<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>')
    [IO.File]::WriteAllText((Join-Path $root 'CHANGELOG.md'), "# Changelog`n`n## [Unreleased]`n`n### Added`n`n- New feature.`n`n$history")
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\docs\RELEASE_NOTES.md') -Destination (Join-Path $root 'docs\RELEASE_NOTES.md')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\.github\PULL_REQUEST_TEMPLATE.md') -Destination (Join-Path $root '.github\PULL_REQUEST_TEMPLATE.md')
    $invoke = Join-Path $PSScriptRoot 'Invoke-Release.ps1'
    Assert-Rejected { & $invoke -RepositoryRoot $root -ExpectedCommit ('a' * 40) } '-NotesReviewed'
    & $invoke -RepositoryRoot $root -ExpectedCommit ('a' * 40) -WhatIf | Out-Null
    Assert-True (-not $global:ReleaseFixture.TagPushed) 'WhatIf or missing review changed a tag.'
    $result = & $invoke -RepositoryRoot $root -ExpectedCommit ('a' * 40) -NotesReviewed | ConvertFrom-Json
    Assert-True ($result.IsDraft -and $result.DownloadsVerified -and $result.Commit -eq ('c' * 40)) 'Draft completion was not verified.'
    Assert-True ($global:ReleaseFixture.TagPushed) 'The checked merge commit was not tagged and pushed.'
    $updated = [IO.File]::ReadAllText((Join-Path $root 'CHANGELOG.md'))
    Assert-True ($updated -match '## \[Unreleased\]\s+## \[1.1.0\] - \d{4}-\d{2}-\d{2}') 'Preparation did not clear Unreleased and date the new version.'
    Assert-True (@($global:ReleaseFixture.Calls | Where-Object { $_ -match '^gh release (create|edit|delete)|--admin|--force' }).Count -eq 0) 'The agent bypassed the draft workflow or repository protections.'
    $global:ReleaseFixture.Head = $global:ReleaseFixture.Main
    $global:ReleaseFixture.Corrupt = $true
    Assert-Rejected { & $invoke -RepositoryRoot $root -ExpectedCommit ('c' * 40) -NotesReviewed } 'mismatched release checksum'
    $global:ReleaseFixture.Corrupt = $false
    $global:ReleaseFixture.TagCommit = 'd' * 40
    Assert-Rejected { & $invoke -RepositoryRoot $root -ExpectedCommit ('c' * 40) -NotesReviewed } 'will not be moved'
    Write-Output 'Agent release tests passed: version policy, preparation, green-check merge, exact tagging, dry-run safety, draft verification, corruption, and tag conflicts (offline).'
}
finally {
    Remove-Item -LiteralPath $root -Recurse -Force
    Remove-Item Function:\git, Function:\gh, Function:\dotnet
    Remove-Variable ReleaseFixture -Scope Global
}
