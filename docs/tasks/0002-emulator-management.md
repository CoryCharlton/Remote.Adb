# 0002 — Emulator management (list / start / stop)

**Status:** ✅ Done

The first feature: list available AVDs and which are running, start an AVD, stop a running emulator.
Logic lives in `Remote.Adb.Core` and is surfaced in both the desktop GUI and the console.

## Background — relevant Android SDK commands

- List AVDs: `emulator -list-avds` (one name per line).
- List running devices: `adb devices` (lines like `emulator-5554<TAB>device`).
- Correlate a running serial to its AVD name: `adb -s emulator-5554 emu avd name` (first output line).
- Start: `emulator -avd <name>` (long-running — fire-and-forget).
- Stop: `adb -s emulator-5554 emu kill`.

SDK location resolves in order: `ANDROID_HOME` → `ANDROID_SDK_ROOT` → OS default
(`%LOCALAPPDATA%\Android\Sdk`, `~/Library/Android/sdk`, `~/Android/Sdk`), falling back to `PATH`.

## Tasks

### Core foundation
- [x] Add packages: `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`,
      `Microsoft.Extensions.Logging.Console`.
- [x] `ProcessResult` model + `IProcessRunner`/`ProcessRunner` (async run-to-completion with
      captured stdout/stderr/exit code, plus fire-and-forget `Start`). Avoid the pipe-buffer deadlock.
      Throws `ProcessLaunchException` when the tool is missing.
- [x] `IAndroidSdk`/`AndroidSdk` to resolve `emulator`/`adb`/`avdmanager` paths.
- [x] `AddRemoteAdbCore(this IServiceCollection)` DI registration extension.

### Emulator feature
- [x] `AndroidVirtualDevice` model (`Name`, `IsRunning`, `Serial?`).
- [x] Pure parsers: `EmulatorOutputParser.ParseAvdList`/`ParseAvdName`, `AdbOutputParser.ParseDevices`.
- [x] `IEmulatorService`/`EmulatorService` — `ListAsync` / `StartAsync` / `StopAsync`.

### Tests (`Remote.Adb.Core.UnitTests`)
- [x] NUnit 4 project + packages (`Microsoft.NET.Test.Sdk`, `NUnit`, `NUnit3TestAdapter`, `Moq`).
- [x] Parser tests (canned strings).
- [x] `EmulatorService` tests with `IProcessRunner` mocked (Moq) and `LoggerFake`.

### Desktop
- [x] `EmulatorViewModel` (observable list, `IsBusy`, `StatusMessage`, Refresh/Start/Stop commands).
- [x] `EmulatorView.axaml` with `x:DataType`.
- [x] Host the view in `MainWindow`; build the DI service provider in the composition root.

### Console
- [x] `emulator list` / `emulator start <avd>` / `emulator stop <serial>` verb dispatch.

## Verification
- [x] `dotnet test` passes (10 tests).
- [x] `dotnet run --project Remote.Adb.Console -- emulator list` lists AVDs; start/stop work
      (verified against a fake SDK on PATH; missing-SDK path reports a clean error).
- [x] `dotnet run --project Remote.Adb.Desktop` — launched and visually confirmed.

## Notes
- `LoggerFake` is not a public package; added a minimal local `LoggerFake<T>` in the test project.
- Switched mocking from Imposter to **Moq** (Imposter 0.1.x not feature-complete enough).
