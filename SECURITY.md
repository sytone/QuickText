# Security policy

## Supported versions

Only the latest release receives fixes. Please update before reporting.

## What QuickText does and does not do

QuickText works entirely offline. It has no network code, no telemetry and no
account. It stores two files in `%LOCALAPPDATA%\QuickText` (settings and, if
autosave is on, your note) and, if you enable it, one startup entry in
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Links in a note are opened
in your default browser, and only if they start with `http://` or `https://`.

## Reporting a vulnerability

Please do not open a public issue for security problems. Use the private
reporting form instead:
[Report a vulnerability](https://github.com/BlazeR-28/QuickText/security/advisories/new)

Include the version, what you did, and what you expected to happen. You will get
an answer as soon as possible; this is a hobby project, so please be patient.
