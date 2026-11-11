[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '..\NoteStore.cs') -CompilerOptions '/nullable:enable'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('QuickText-NoteStore-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $legacy = Join-Path $testRoot 'notes.txt'
    [IO.File]::WriteAllText($legacy, "# Imported`nOriginal note")
    $store = [QuickText.NoteStore]::new($testRoot)
    $store.Initialize()
    $first = $store.CurrentFileName
    Assert-True ($first -cmatch '^note-\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2}\.md$') 'Unexpected note filename.'
    Assert-True ($store.CurrentText -eq "# Imported`nOriginal note") 'Legacy note was not imported.'
    Assert-True ([IO.File]::ReadAllText($legacy) -eq $store.CurrentText) 'Legacy backup was changed.'

    $store.UpdateText($first, "# First`nSaved text", $true)
    $store.Create()
    $second = $store.CurrentFileName
    Assert-True ($store.CurrentText -eq '') 'New note is not empty.'
    $store.UpdateText($second, "Second note`n`tTabbed", $true)
    Assert-True ([IO.File]::ReadAllText((Join-Path $testRoot $first)) -eq "# First`nSaved text") 'Editing another note changed the first note.'
    Assert-True ([IO.File]::ReadAllText((Join-Path $testRoot $second)) -eq "Second note`n`tTabbed") 'Selected note was not autosaved.'

    $store.Create()
    $third = $store.CurrentFileName
    Assert-True ([string]::CompareOrdinal($third, $second) -gt 0) 'Rapid note creation did not produce increasing unique filenames.'
    Assert-True ([string]::CompareOrdinal($second, $first) -gt 0) 'Rapid note creation overwrote a note.'
    Assert-True (($store.ListNotes() -join ',') -eq (@($third, $second, $first) -join ',')) 'Notes are not newest first.'
    $store.Select($first)
    Assert-True ($store.CurrentText -eq "# First`nSaved text") 'Selecting an older note loaded the wrong content.'
    $reopened = [QuickText.NoteStore]::new($testRoot)
    $reopened.Initialize()
    Assert-True ($reopened.CurrentFileName -eq $first -and $reopened.CurrentText -eq $store.CurrentText) 'Restart did not restore the selected older note.'

    $store.UpdateText($first, 'Unsaved draft', $false)
    Assert-True $store.HasUnsavedChanges 'Autosave-off edits were not tracked as unsaved.'
    Assert-True ([IO.File]::ReadAllText((Join-Path $testRoot $first)) -eq "# First`nSaved text") 'Autosave-off edits changed the saved file.'
    $store.Save()
    Assert-True (-not $store.HasUnsavedChanges) 'Saving did not clear the unsaved state.'
    Assert-True ([IO.File]::ReadAllText((Join-Path $testRoot $first)) -eq 'Unsaved draft') 'Re-enabling save did not persist the draft.'

    $lock = [IO.File]::Open((Join-Path $testRoot $first), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
    $rejected = $false
    try {
        try { $store.DeleteCurrent() } catch { $rejected = $true }
    } finally { $lock.Dispose() }
    Assert-True $rejected 'A failed deletion was not surfaced.'
    Assert-True ($store.CurrentFileName -eq $first) 'Failed deletion changed the selected note.'
    $reopened = [QuickText.NoteStore]::new($testRoot)
    $reopened.Initialize()
    Assert-True ($reopened.CurrentFileName -eq $first) 'Failed deletion changed the persisted selection.'

    $rejected = $false
    try { $store.Select('..\notes.txt') } catch { $rejected = $true }
    Assert-True $rejected 'Path traversal was accepted.'
    Assert-True ($store.CurrentFileName -eq $first) 'Failed selection changed the active note.'
    [IO.File]::WriteAllText((Join-Path $testRoot 'unrelated.md'), 'Do not manage this file')
    Assert-True ($store.ListNotes().Length -eq 3) 'Unrelated Markdown files appeared in the note list.'

    $store.DeleteCurrent()
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $testRoot $first))) 'Selected note was not deleted.'
    Assert-True ($store.CurrentFileName -eq $third) 'Delete did not select the newest remaining note.'
    $store.DeleteCurrent()
    Assert-True ($store.CurrentFileName -eq $second) 'Delete did not load the next newest note.'
    Assert-True ($store.CurrentText -eq "Second note`n`tTabbed") 'Delete loaded the wrong remaining content.'
    $store.DeleteCurrent()
    Assert-True ($store.ListNotes().Length -eq 1 -and $store.CurrentText -eq '') 'Deleting the last note did not create an empty replacement.'
    Assert-True (Test-Path -LiteralPath (Join-Path $testRoot $store.CurrentFileName)) 'Replacement note was not created on disk.'

    $store.UpdateText($store.CurrentFileName, 'Before failure', $true)
    $lockedPath = Join-Path $testRoot $store.CurrentFileName
    $lock = [IO.File]::Open($lockedPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
    $rejected = $false
    try {
        try { $store.UpdateText($store.CurrentFileName, 'Retained draft after failure', $true) } catch { $rejected = $true }
    } finally { $lock.Dispose() }
    Assert-True $rejected 'A failed autosave was not surfaced.'
    Assert-True ([IO.File]::ReadAllText($lockedPath) -eq 'Before failure') 'Failed autosave damaged the saved note.'
    Assert-True ($store.HasUnsavedChanges -and $store.CurrentText -eq 'Retained draft after failure') 'Failed autosave lost the draft.'
    Assert-True (@(Get-ChildItem -LiteralPath $testRoot -Filter '*.tmp').Count -eq 0) 'Failed save left a temporary file.'

    [IO.File]::Delete($lockedPath)
    $reopened = [QuickText.NoteStore]::new($testRoot)
    $reopened.Initialize()
    Assert-True ($reopened.CurrentText -eq '') 'Missing notes caused the legacy backup to be imported again.'
    [IO.File]::WriteAllText((Join-Path $testRoot 'active-note.txt'), '..\notes.txt')
    $rejected = $false
    try { ([QuickText.NoteStore]::new($testRoot)).Initialize() } catch { $rejected = $true }
    Assert-True $rejected 'An invalid persisted filename was silently accepted.'

    Write-Output 'NoteStore tests passed: import, autosave isolation, rapid creation, sorting, selection, restart, unsaved drafts, deletion, validation, and failed saves.'
} finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
