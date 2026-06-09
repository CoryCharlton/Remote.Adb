# 0010 — Settings: SDK / JAVA_HOME overrides

**Status:** ⬜ Planned

Let the user override the environment-based tool resolution from the **Settings** page, so the app works when
`ANDROID_HOME`/`JAVA_HOME` aren't set (or are wrong — e.g. a broken Android Studio JBR). The overrides take
precedence over the env vars; left blank, today's env-var behavior is unchanged.

## Settings to expose

- **Android SDK path** — overrides `ANDROID_HOME`/`ANDROID_SDK_ROOT` in `AndroidSdk.ResolveSdkRoot`.
- **AVD home** — overrides `ANDROID_AVD_HOME` in `AvdHome.Resolve`.
- **Java home (JDK)** — `avdmanager`/`sdkmanager` are Java wrappers. They already find a JDK on their own via
  `JAVA_HOME` or any `java` on `PATH`, so **no install-path probing** — Android Studio need not be installed at
  all; a standalone JDK works. The override is for the case where neither is set: when configured, launch those
  tools with `JAVA_HOME` pointed here (set it on the `ProcessStartInfo.Environment` for those processes). The
  user can point it at Android Studio's bundled JBR (`<Studio>/jbr`) or any JDK.

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

Surfaced repeatedly while debugging create:

- On a Linux box with no `JAVA_HOME` and no `java` on `PATH`, `avdmanager` exited 1 with
  `ERROR: JAVA_HOME is not set and no 'java' command could be found` — printed to **stdout**, which the app was
  discarding, so the dialog showed only a generic "Check the SDK installation." `CreateAsync` now returns the
  tool's merged stdout/stderr (`AvdOperationResult`) so the real reason shows; the **override** is what lets the
  user fix it from inside the app.
- Earlier: a user's `avdmanager` failed with `could not open …\jbr\lib\jvm.cfg` (broken JBR / `JAVA_HOME`), and
  their `avdmanager` lived in `tools/bin` not `cmdline-tools/latest/bin`.

Path resolution was made robust in 0008, and create failures are now legible; the **Java/SDK override** (+ the
unresolved-SDK warning above) is the remaining gap this task closes.
