#Requires -Version 7.0

function Invoke-ReleaseCommand {
    <#
    .SYNOPSIS
    Stops release automation when a native command fails instead of trusting partial output.
    #>
    param([string]$Command, [string[]]$Arguments)
    $result = & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command $($Arguments -join ' ') failed (exit code $LASTEXITCODE)."
    }
    return ($result -join "`n")
}

function Get-ReleaseVersionPlan {
    <#
    .SYNOPSIS
    Gives agents and tests the same version decision from recorded release history.
    #>
    param([string]$ProjectVersion, [string]$Changelog, [string[]]$Tags)
    $stable = '(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)'
    if ($ProjectVersion -cnotmatch "\A$stable\z") { throw 'The project needs a stable three-part version.' }
    $current = [version]$ProjectVersion
    $sections = [regex]::Matches($Changelog, "(?m)^## \[($stable)\] - \d{4}-\d{2}-\d{2}\r?$")
    $versions = @($sections | ForEach-Object { [version]$_.Groups[1].Value })
    $stableTags = @($Tags | Where-Object { $_ -cmatch "\Av$stable\z" } |
        Sort-Object { [version]$_.Substring(1) } -Descending)
    $history = @($versions) + @($stableTags | ForEach-Object { [version]$_.Substring(1) })
    $latest = if ($history.Count) { $history | Sort-Object -Descending | Select-Object -First 1 } else { [version]'0.0.0' }
    if ($current -lt $latest) { throw 'The project version is older than release history. Refresh the worktree.' }
    $unreleased = [regex]::Matches($Changelog, '(?ms)^## \[Unreleased\]\r?\n(.*?)(?=^## \[|\z)')
    if ($unreleased.Count -ne 1) { throw 'The changelog needs exactly one Unreleased section.' }
    $changes = $unreleased[0].Groups[1].Value.Trim()
    $hasChanges = $changes -match '(?m)^[-*] \S'
    if (-not $hasChanges -and ($changes -replace '(?m)^### [^\r\n]+\r?$', '').Trim()) {
        throw 'Unreleased changes must use changelog bullets.'
    }
    $prepared = -not $hasChanges -and $versions -contains $current
    if (-not $hasChanges -and -not $prepared) { throw 'There are no unreleased changes or prepared release.' }
    $reason = 'Already prepared in the changelog.'
    $next = $current
    if ($hasChanges) {
        $added = [regex]::Match($changes, '(?ms)^### Added\r?\n(.*?)(?=^### |\z)')
        $breaking = [regex]::Match($changes, '(?ms)^### Breaking changes\r?\n(.*?)(?=^### |\z)')
        if ($breaking.Groups[1].Value -match '(?m)^[-*] \S' -or $changes -cmatch '(?m)^[-*] BREAKING CHANGE:') {
            $next = [version]::new($latest.Major + 1, 0, 0)
            $reason = 'Breaking changes require a major version.'
        }
        elseif ($added.Groups[1].Value -match '(?m)^[-*] \S') {
            $next = [version]::new($latest.Major, $latest.Minor + 1, 0)
            $reason = 'Added changes require a minor version.'
        }
        else {
            $next = [version]::new($latest.Major, $latest.Minor, $latest.Build + 1)
            $reason = 'Other changes require a patch version.'
        }
        if ($current -gt $next) {
            $next = $current
            $reason = 'The project already declares a higher next version.'
        }
    }
    [pscustomobject]@{
        Version = $next.ToString(3)
        Tag = "v$($next.ToString(3))"
        PreviousVersion = $latest.ToString(3)
        PreviousTag = if ($stableTags.Count) { $stableTags[0] } else { $null }
        Prepared = [bool]$prepared
        Reason = $reason
        Changes = $changes
    }
}

function Test-ReleaseChecks {
    <#
    .SYNOPSIS
    Prevents a preparation PR from merging before its real build and other checks finish.
    #>
    param($PullRequest, [string]$ExpectedHead, [string]$ExpectedBase)
    if ($PullRequest.headRefOid -cne $ExpectedHead) { throw 'The preparation PR head changed. Review it again.' }
    if ($PullRequest.baseRefOid -cne $ExpectedBase) { throw 'Main advanced during preparation. Review the new changes before merging.' }
    if ($PullRequest.reviewDecision -eq 'CHANGES_REQUESTED') { throw 'The preparation PR has requested changes.' }
    $buildPassed = $false
    $pending = $false
    foreach ($check in $PullRequest.statusCheckRollup) {
        if ($check.__typename -eq 'StatusContext') {
            if ($check.state -eq 'PENDING') { $pending = $true }
            elseif ($check.state -ne 'SUCCESS') { throw "PR check failed: $($check.context)" }
        }
        elseif ($check.status -ne 'COMPLETED') { $pending = $true }
        elseif ($check.conclusion -notin @('SUCCESS', 'NEUTRAL', 'SKIPPED')) { throw "PR check failed: $($check.name)" }
        if ($check.name -eq 'build' -and $check.conclusion -eq 'SUCCESS') { $buildPassed = $true }
    }
    return $buildPassed -and -not $pending
}

function Test-ReleaseAssets {
    <#
    .SYNOPSIS
    Verifies the actual draft downloads rather than only trusting upload metadata.
    #>
    param([string]$Directory)
    $expected = @('LICENSE', 'QuickText-needs-dotnet.exe', 'QuickText.exe', 'SHA256SUMS.txt', 'THIRD-PARTY-NOTICES.md') | Sort-Object
    $files = @(Get-ChildItem -LiteralPath $Directory -File)
    if ((($files.Name | Sort-Object) -join ',') -cne ($expected -join ',') -or
        @($files | Where-Object Length -eq 0).Count) { throw 'Draft downloads are missing, empty, or unexpected.' }
    $seen = @()
    foreach ($line in [IO.File]::ReadAllLines((Join-Path $Directory 'SHA256SUMS.txt'))) {
        if ($line -cnotmatch '^([a-f0-9]{64})  (QuickText\.exe|QuickText-needs-dotnet\.exe|LICENSE|THIRD-PARTY-NOTICES\.md)$') {
            throw 'Invalid release checksum manifest.'
        }
        $hash, $name = $Matches[1], $Matches[2]
        if ($name -in $seen -or (Get-FileHash -LiteralPath (Join-Path $Directory $name) -Algorithm SHA256).Hash -ine $hash) {
            throw "Duplicate or mismatched release checksum: $name"
        }
        $seen += $name
    }
    if ($seen.Count -ne 4) { throw 'The manifest must cover all four release downloads.' }
}
