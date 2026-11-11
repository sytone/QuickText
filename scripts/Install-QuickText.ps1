<#
.SYNOPSIS
Publishes and installs or updates QuickText for the current Windows user.
.DESCRIPTION
Stops the installed instance before replacing its files, enables launch at
sign-in, and starts QuickText unless -NoStart is specified.
#>
[CmdletBinding()]
param(
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $repoRoot 'bin\publish'
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\QuickText'
$installedExe = Join-Path $installDirectory 'QuickText.exe'
$startupDirectory = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup'

& dotnet publish (Join-Path $repoRoot 'QuickText.csproj') -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "QuickText publish failed (exit code $LASTEXITCODE). The installed app has not been changed."
}
if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory 'QuickText.exe') -PathType Leaf)) {
    throw 'Publish did not produce QuickText.exe. The installed app has not been changed.'
}

$runningInstances = Get-Process | Where-Object {
    $_.ProcessName -eq 'QuickText' -and $_.Path -eq $installedExe
}
foreach ($instance in $runningInstances) {
    Stop-Process -Id $instance.Id -Force
    if (-not $instance.WaitForExit(10000)) {
        throw "QuickText process $($instance.Id) did not exit. Installation stopped."
    }
}

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $publishDirectory | Copy-Item -Destination $installDirectory -Recurse -Force

New-Item -ItemType Directory -Path $startupDirectory -Force | Out-Null
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $startupDirectory 'QuickText.lnk'))
$shortcut.TargetPath = $installedExe
$shortcut.WorkingDirectory = $installDirectory
$shortcut.Save()

if (-not $NoStart) {
    Start-Process -FilePath $installedExe -WorkingDirectory $installDirectory
}

Write-Host "QuickText installed at $installedExe. Launch at sign-in is enabled."
