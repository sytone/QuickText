<#
.SYNOPSIS
Prepares and merges a release PR, tags its checked commit, and verifies a draft.
.DESCRIPTION
Run only for an explicit release request, after reviewing the prose against the
planned changes. Never publishes a release or bypasses repository protections.
.PARAMETER ExpectedCommit
Binds the agent's notes review to the main commit reported by Get-ReleasePlan.
.PARAMETER NotesReviewed
Records that the caller reviewed meaning and coverage, not just Markdown structure.
.PARAMETER RepositoryRoot
Keeps all file and git actions inside the chosen isolated worktree.
.PARAMETER TimeoutSeconds
Bounds waits for checks and workflows; failures leave existing tags and drafts intact.
#>
#Requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$ExpectedCommit,
    [switch]$NotesReviewed,
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [ValidateRange(1, 7200)][int]$TimeoutSeconds = 1800
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Release.Common.ps1')
$repository = 'sytone/QuickText'
$plan = & (Join-Path $PSScriptRoot 'Get-ReleasePlan.ps1') -RepositoryRoot $RepositoryRoot | ConvertFrom-Json
if ($plan.SourceCommit -cne $ExpectedCommit -or $plan.MainCommit -cne $ExpectedCommit) {
    throw 'HEAD and origin/main must match the reviewed commit. Refresh and review again.'
}
if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot '.git') -PathType Leaf)) {
    throw 'Release preparation requires an isolated git worktree.'
}
$branch = Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'branch', '--show-current')
if ($branch -notlike 'dev/*') { throw 'Use an isolated dev/* branch for the release.' }
$origin = Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'remote', 'get-url', 'origin')
if ($origin -cnotmatch '\A(https://github\.com/|git@github\.com:)sytone/QuickText(\.git)?\z') {
    throw 'Origin does not point at sytone/QuickText.'
}
foreach ($line in ($plan.WorkingTreeChanges -split "`n" | Where-Object { $_ })) {
    if ($line.Substring(3) -cnotin @('docs/RELEASE_NOTES.md', 'CHANGELOG.md')) { throw "Unrelated local change: $line" }
}
if ($plan.Prepared -and $plan.WorkingTreeChanges) { throw 'An already prepared release must have a clean worktree, including its notes.' }
if (-not $PSCmdlet.ShouldProcess("$repository $($plan.Tag)", 'Prepare, merge, tag, and verify a draft (never publish)')) {
    $plan | ConvertTo-Json -Depth 4
    return
}
if (-not $NotesReviewed) { throw 'Review release-note accuracy and coverage, then supply -NotesReviewed.' }
$temporary = Join-Path ([IO.Path]::GetTempPath()) "QuickText-release-agent-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $temporary | Out-Null
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)

function Wait-ReleaseCondition {
    <#
    .SYNOPSIS
    Keeps remote waits bounded while allowing queued checks to appear.
    #>
    param([scriptblock]$Condition)
    while ($true) {
        $value = & $Condition
        if ($value) { return $value }
        if ([DateTime]::UtcNow -ge $deadline) { throw 'Release wait timed out. Inspect the existing PR, tag, and workflow before resuming.' }
        Start-Sleep -Seconds 10
    }
}

function Wait-ReleaseWorkflow {
    <#
    .SYNOPSIS
    Requires a successful push workflow for the exact commit and ref being released.
    #>
    param([string]$Workflow, [string]$Commit, [string]$Ref)
    Wait-ReleaseCondition {
        $runs = @(Invoke-ReleaseCommand gh @('run', 'list', '--repo', $repository, '--workflow', $Workflow,
            '--commit', $Commit, '--branch', $Ref, '--event', 'push', '--limit', '20',
            '--json', 'databaseId,status,conclusion,headSha,url') | ConvertFrom-Json)
        if (-not $runs.Count) { return $null }
        $run = $runs[0]
        if ($run.headSha -cne $Commit) { throw 'Workflow commit does not match the release.' }
        if ($run.status -ne 'completed') { return $null }
        if ($run.conclusion -ne 'success') { throw "Release workflow failed: $($run.url)" }
        return $run
    }
}

try {
    $description = Join-Path $temporary 'RELEASE_NOTES.md'
    & (Join-Path $PSScriptRoot 'New-ReleaseNotes.ps1') -Version $plan.Version -Tag $plan.Tag `
        -NotesPath (Join-Path $RepositoryRoot 'docs\RELEASE_NOTES.md') -OutputPath $description -Repository $repository
    $releaseCommit = $ExpectedCommit
    $preparationPr = $null
    if (-not $plan.Prepared) {
        $existing = @(Invoke-ReleaseCommand gh @('pr', 'list', '--repo', $repository, '--state', 'open', '--base', 'main',
            '--search', "`"chore: prepare $($plan.Tag)`" in:title", '--json', 'url') | ConvertFrom-Json)
        if ($existing.Count) { throw "A preparation PR already exists. Inspect it instead of creating another: $($existing[0].url)" }
        $projectPath = Join-Path $RepositoryRoot 'src\QuickText\QuickText.csproj'
        $projectText = [IO.File]::ReadAllText($projectPath)
        if ([regex]::Matches($projectText, '<Version>[^<]+</Version>').Count -ne 1) {
            throw 'The project needs one literal Version element before release preparation.'
        }
        $projectText = [regex]::Replace($projectText, '<Version>[^<]+</Version>', "<Version>$($plan.Version)</Version>")
        [IO.File]::WriteAllText($projectPath, $projectText)
        $changelogPath = Join-Path $RepositoryRoot 'CHANGELOG.md'
        $changelog = [IO.File]::ReadAllText($changelogPath)
        $newline = if ($changelog.Contains("`r`n")) { "`r`n" } else { "`n" }
        $date = [DateTime]::UtcNow.ToString('yyyy-MM-dd')
        $changelog = $changelog.Replace('## [Unreleased]', "## [Unreleased]$newline$newline## [$($plan.Version)] - $date")
        [IO.File]::WriteAllText($changelogPath, $changelog)
        Push-Location $RepositoryRoot
        try { Invoke-ReleaseCommand dotnet @('build', 'QuickText.sln', '-c', 'Release', '-warnaserror') | Out-Host }
        finally { Pop-Location }
        Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'add', '--', 'src/QuickText/QuickText.csproj', 'CHANGELOG.md', 'docs/RELEASE_NOTES.md') | Out-Host
        Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'commit', '-m', "chore: prepare $($plan.Tag)", '-m',
            'Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>') | Out-Host
        $head = Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'rev-parse', 'HEAD')
        Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'push', '--set-upstream', 'origin', $branch) | Out-Host
        $body = [IO.File]::ReadAllText((Join-Path $RepositoryRoot '.github\PULL_REQUEST_TEMPLATE.md'))
        if (-not $body.Contains('## What does this change?') -or -not $body.Contains('## How did you try it?')) {
            throw 'The PR template changed. Update the release script before continuing.'
        }
        $body = $body.Replace('## What does this change?', "## What does this change?`n`nPrepare $($plan.Tag) with reviewed release notes and a dated changelog.")
        $body = $body.Replace('## How did you try it?', "## How did you try it?`n`n- ``dotnet build QuickText.sln -c Release -warnaserror``: passed.`n- Release-note validation: passed. CI must pass before merge.`n- For this release-preparation PR, Unreleased entries moved into the dated version section.")
        $body = $body.Replace('- [ ] `dotnet build QuickText.sln -c Release` finishes without warnings',
            '- [x] `dotnet build QuickText.sln -c Release` finishes without warnings')
        [IO.File]::WriteAllText((Join-Path $temporary 'pr.md'), $body)
        $preparationPr = Invoke-ReleaseCommand gh @('pr', 'create', '--repo', $repository, '--base', 'main', '--head', $branch,
            '--title', "chore: prepare $($plan.Tag)", '--body-file', (Join-Path $temporary 'pr.md'))
        Wait-ReleaseCondition {
            $pr = Invoke-ReleaseCommand gh @('pr', 'view', $preparationPr, '--repo', $repository,
                '--json', 'headRefOid,baseRefOid,statusCheckRollup,reviewDecision') | ConvertFrom-Json
            Test-ReleaseChecks -PullRequest $pr -ExpectedHead $head -ExpectedBase $ExpectedCommit
        } | Out-Null
        Invoke-ReleaseCommand gh @('pr', 'merge', $preparationPr, '--repo', $repository, '--squash', '--match-head-commit', $head) | Out-Host
        $merged = Invoke-ReleaseCommand gh @('pr', 'view', $preparationPr, '--repo', $repository, '--json', 'state,mergeCommit') | ConvertFrom-Json
        if ($merged.state -ne 'MERGED' -or -not $merged.mergeCommit.oid) { throw 'Preparation PR was not merged. No tag was created.' }
        $releaseCommit = $merged.mergeCommit.oid
    }
    $null = Wait-ReleaseWorkflow 'build.yml' $releaseCommit 'main'
    Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'fetch', 'origin', 'refs/heads/main:refs/remotes/origin/main', '--tags') | Out-Host
    Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'merge-base', '--is-ancestor', $releaseCommit, 'origin/main') | Out-Null
    if ($preparationPr) {
        Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'diff', '--exit-code', $head, $releaseCommit, '--') | Out-Null
    }
    $remoteTag = Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'ls-remote', '--tags', 'origin', "refs/tags/$($plan.Tag)")
    $null = & git -C $RepositoryRoot show-ref --verify --quiet "refs/tags/$($plan.Tag)"
    if ($LASTEXITCODE -eq 0) {
        $tagCommit = Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'rev-parse', "$($plan.Tag)^{commit}")
        if ($tagCommit -cne $releaseCommit) { throw 'The version tag already points at another commit. It will not be moved.' }
    }
    elseif ($LASTEXITCODE -eq 1) {
        Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'tag', '-a', $plan.Tag, $releaseCommit, '-m', "QuickText $($plan.Tag)") | Out-Host
    }
    else { throw 'Could not inspect the existing version tag.' }
    if (-not $remoteTag) { Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'push', 'origin', "refs/tags/$($plan.Tag)") | Out-Host }
    $run = Wait-ReleaseWorkflow 'release.yml' $releaseCommit $plan.Tag
    $draft = Invoke-ReleaseCommand gh @('release', 'view', $plan.Tag, '--repo', $repository,
        '--json', 'tagName,isDraft,body,assets,url') | ConvertFrom-Json
    if ($draft.isDraft -ne $true -or $draft.tagName -cne $plan.Tag) { throw 'The release is not the expected draft. It will not be changed.' }
    if ($draft.body.Replace("`r`n", "`n").Trim() -cne [IO.File]::ReadAllText($description).Trim()) {
        throw 'Draft notes differ from the reviewed description.'
    }
    $downloads = Join-Path $temporary 'downloads'
    Invoke-ReleaseCommand gh @('release', 'download', $plan.Tag, '--repo', $repository, '--dir', $downloads) | Out-Host
    Test-ReleaseAssets -Directory $downloads
    [pscustomobject]@{
        Version = $plan.Version; Tag = $plan.Tag; Commit = $releaseCommit
        PreparationPr = $preparationPr; WorkflowUrl = $run.url
        DraftUrl = $draft.url; IsDraft = $true; DownloadsVerified = $true
    } | ConvertTo-Json
}
finally {
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
