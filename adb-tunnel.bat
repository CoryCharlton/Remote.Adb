@echo off
REM adb-tunnel.bat — bridge Windows adb server to a remote dev host.
REM
REM Workflow:
REM   1. Locate adb on Windows; ensure the Windows adb server is running.
REM   2. SSH to the remote and kill any local adb server there (IntelliJ's Android
REM      plugin respawns one on Gradle sync; it would block the reverse forward).
REM   3. Open the SSH reverse tunnel with ExitOnForwardFailure so a silent bind
REM      failure becomes a visible exit instead of a tunnel that "looks up" but isn't.
REM   4. If the bind raced and lost, retry up to N times.
REM
REM Usage: adb-tunnel.bat [remote-host]   (defaults to blaine.midworld.internal)

setlocal enabledelayedexpansion

where adb >nul 2>&1
if errorlevel 1 (
    if exist "%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe" (
        set "PATH=%LOCALAPPDATA%\Android\Sdk\platform-tools;%PATH%"
    ) else (
        echo ERROR: adb not found on PATH or at %%LOCALAPPDATA%%\Android\Sdk\platform-tools.
        exit /b 1
    )
)

adb start-server
if errorlevel 1 (
    echo ERROR: failed to start Windows adb server.
    exit /b 1
)

set "REMOTE=%~1"
if "%REMOTE%"=="" set "REMOTE=blaine.midworld.internal"

set MAX_RETRIES=3
set ATTEMPT=1

:retry
echo [attempt !ATTEMPT!/%MAX_RETRIES%] Killing any local adb server on %REMOTE%...
REM Use pkill -x adb (exact name match) only — NOT `adb kill-server`. The latter talks
REM to the server over localhost:5037, which may already be the SSH-forwarded port from
REM a stale tunnel session, causing kill-server to hang on a half-open connection.
REM pkill is signal-only, no network round-trip, can't hang.
ssh %REMOTE% "pkill -x adb >/dev/null 2>&1; true"
if errorlevel 1 (
    echo ERROR: ssh to %REMOTE% failed. Check connectivity / SSH config.
    exit /b 1
)

echo [attempt !ATTEMPT!/%MAX_RETRIES%] Opening reverse tunnel: %REMOTE%:5037 -^> 127.0.0.1:5037
echo Keep this window open. Ctrl+C closes the tunnel.
REM Use 127.0.0.1 explicitly (NOT "localhost") for the Windows-side forward target. Windows OpenSSH
REM has historically resolved "localhost" to ::1 (IPv6), and the Windows adb server only binds to
REM 127.0.0.1 (IPv4) — connections forwarded to ::1:5037 get refused, surfacing on Linux as
REM "adb: protocol fault (couldn't read status): Success" (i.e., remote closed connection on EOF).
ssh -o ExitOnForwardFailure=yes -N -R 5037:127.0.0.1:5037 %REMOTE%

REM ssh exits 0 only on Ctrl+C / clean disconnect. Nonzero = bind failed (IntelliJ raced and respawned adb).
if errorlevel 1 (
    if !ATTEMPT! lss %MAX_RETRIES% (
        set /a ATTEMPT+=1
        echo Bind failed - IntelliJ likely respawned adb. Retrying...
        goto retry
    )
    echo ERROR: tunnel bind failed after %MAX_RETRIES% attempts.
    echo The remote IntelliJ Android plugin keeps respawning adb on port 5037.
    echo Workaround: in the remote IntelliJ, close any open Android run configs / Device Manager,
    echo then re-run this script. Long-term fix: switch the bridge to a non-default port.
    exit /b 1
)

endlocal
