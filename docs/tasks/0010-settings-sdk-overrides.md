# 0010 — Settings: SDK / JAVA_HOME overrides

**Status:** ⬜ Planned

Let the user override the environment-based tool resolution from the **Settings** page, so the app works when
`ANDROID_HOME`/`JAVA_HOME` aren't set (or are wrong — e.g. a broken Android Studio JBR). The overrides take
precedence over the env vars; left blank, today's env-var behavior is unchanged.

## Settings to expose

- **Android SDK path** — overrides `ANDROID_HOME`/`ANDROID_SDK_ROOT` in `AndroidSdk.ResolveSdkRoot`.
- **AVD home** — overrides `ANDROID_AVD_HOME` in `AvdHome.Resolve`.
- **Java home (JDK/JBR)** — `avdmanager`/`sdkmanager` are Java wrappers; when set, launch them with
  `JAVA_HOME` pointed here (set it on the `ProcessStartInfo.Environment` for those processes). Common default:
  Android Studio's bundled JBR (`<Studio>\jbr`).

## Approach

- `ISettingsService` gains nullable `SdkRoot` / `AvdHome` / `JavaHome` (persisted like the theme setting).
- `AndroidSdk` / `AvdHome` consult the settings first, then the env vars (inject `ISettingsService`, or resolve
  lazily so a settings change re-resolves).
- `IProcessRunner.RunAsync` (or a tool-launch wrapper) sets `JAVA_HOME` for `avdmanager`/`sdkmanager` when a
  Java home is configured.
- Settings UI: three path fields (with folder pickers + "detect" helpers) under a "Android SDK" section.

## Warn when the SDK is unresolved

When neither `ANDROID_HOME`/`ANDROID_SDK_ROOT` is set **and** no override is configured, the app silently
falls back to the platform-default path (`AndroidSdk.DefaultSdkRoot`). If that path happens to exist but is the
wrong SDK — or doesn't exist — the user just sees empty device/image lists with no explanation. Surface this:

- Detect the "resolved by default fallback, not by env var or override" case in `AndroidSdk` and expose it
  (e.g. an `SdkRootSource { Override, EnvironmentVariable, DefaultFallback, NotFound }` alongside `SdkRoot`).
- Show a dismissible banner / inline hint on the relevant screens (create wizard, emulator list, Settings)
  pointing the user at the SDK-path override when the source is `DefaultFallback` or `NotFound`.
- Today the only signal is a `LogWarning` in `AndroidSdk`'s ctor, which a GUI user never sees.

## Why now-ish

Surfaced while debugging create: a user's `avdmanager` failed with `could not open …\jbr\lib\jvm.cfg` (broken
JBR / `JAVA_HOME`), and their `avdmanager` lived in `tools/bin` not `cmdline-tools/latest/bin`. Path resolution
was made robust in 0008; the **Java/SDK override** is the remaining gap this task closes.
