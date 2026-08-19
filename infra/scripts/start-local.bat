@echo off
REM Double-click-safe launcher for start-local.ps1.
REM
REM This machine's PowerShell execution policy (LocalMachine/CurrentUser
REM both Undefined -- the Windows default, which means Restricted) blocks
REM *any* unsigned local .ps1 script from running at all, including this
REM one -- "running scripts is disabled on this system." -ExecutionPolicy
REM Bypass here scopes that override to just this one process, the same
REM well-established pattern used to hand a PowerShell script to someone
REM without asking them to change a machine-wide security setting first.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-local.ps1" %*
