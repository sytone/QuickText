<#
.SYNOPSIS
Reports the next version and exact review baseline without tagging or releasing.
.PARAMETER RepositoryRoot
Lets isolated worktrees and offline tests use the same release rules.
.PARAMETER SkipFetch
Allows offline fixture tests; real release planning must refresh origin and tags.
#>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$SkipFetch
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Release.Common.ps1')
if (-not $SkipFetch) {
    Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'fetch', 'origin', 'refs/heads/main:refs/remotes/origin/main', '--tags') | Out-Host
}
$project = [xml][IO.File]::ReadAllText((Join-Path $RepositoryRoot 'src\QuickText\QuickText.csproj'))
$version = @($project.Project.PropertyGroup.Version | Where-Object { $_ })
if ($version.Count -ne 1) { throw 'The project must declare exactly one version.' }
$changelog = [IO.File]::ReadAllText((Join-Path $RepositoryRoot 'CHANGELOG.md'))
$tags = (Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'tag', '--list')) -split "`n"
$plan = Get-ReleaseVersionPlan -ProjectVersion $version[0] -Changelog $changelog -Tags $tags
$plan | Add-Member -NotePropertyName SourceCommit -NotePropertyValue (Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'rev-parse', 'HEAD'))
$plan | Add-Member -NotePropertyName MainCommit -NotePropertyValue (Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'rev-parse', 'origin/main'))
$plan | Add-Member -NotePropertyName WorkingTreeChanges -NotePropertyValue (Invoke-ReleaseCommand git @('-C', $RepositoryRoot, 'status', '--porcelain'))
$plan | ConvertTo-Json -Depth 4
