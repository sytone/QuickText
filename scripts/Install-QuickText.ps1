<#
.SYNOPSIS
Publishes and installs or updates QuickText for the current Windows user.
.DESCRIPTION
Builds the native WPF app, closes the installed instance gracefully, enables
Start with Windows, and starts QuickText unless -NoStart is specified.
Exit a hidden instance from its tray menu before updating.
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\QuickText\QuickText.csproj'
$publishDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "QuickText-install-$([guid]::NewGuid().ToString('N'))"
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\QuickText'
$installedExe = Join-Path $installDirectory 'QuickText.exe'
$legacyShortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\QuickText.lnk'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

try {
    & dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publishDirectory
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
        if (-not $instance.CloseMainWindow() -or -not $instance.WaitForExit(10000)) {
            throw "QuickText process $($instance.Id) could not close safely. Exit QuickText from its tray menu and run this script again. The installed app has not been changed."
        }
    }

    New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
    Get-ChildItem -LiteralPath $publishDirectory |
        Copy-Item -Destination $installDirectory -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE'), (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.md') -Destination $installDirectory -Force

    if (-not (Test-Path -LiteralPath $runKey)) {
        New-Item -Path $runKey -Force | Out-Null
    }
    New-ItemProperty -Path $runKey -Name 'QuickText' -PropertyType String -Value "`"$installedExe`" --hidden" -Force | Out-Null

    if (Test-Path -LiteralPath $legacyShortcut -PathType Leaf) {
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($legacyShortcut)
        if ($shortcut.TargetPath -eq $installedExe) {
            Remove-Item -LiteralPath $legacyShortcut
        }
        else {
            Write-Warning "The existing Startup shortcut points to another installation and was left unchanged: $legacyShortcut"
        }
    }

    if (-not $NoStart) {
        Start-Process -FilePath $installedExe -WorkingDirectory $installDirectory
    }

    Write-Host "QuickText installed at $installedExe."
    Write-Host 'Start with Windows is enabled. Change it in QuickText settings.'
}
finally {
    if (Test-Path -LiteralPath $publishDirectory -PathType Container) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }
}
