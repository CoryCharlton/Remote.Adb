# 0004 — Apply the CCSWE design system

**Status:** ✅ Done

Skin `Remote.Adb.Desktop` with the **CCSWE.Avalonia.Material** design-system package — a standalone
Material 3 theme and control library — rather than hand-rolling a token theme. Dark by default.

> **Supersedes an earlier plan.** This task originally described an Avalonia FluentTheme base plus
> locally-vendored M3 token dictionaries and brand fonts (`Themes/Tokens.axaml`, `Typography.axaml`,
> `FluentOverrides.axaml`, vendored `DMSans`/`PlusJakartaSans`, and a reciprocal `docs/handoff/` loop).
> That approach was dropped once the design system shipped as a consumable Avalonia package; those files
> no longer exist.

## What we did

- [x] Reference **CCSWE.Avalonia.Material** (centrally managed in `Directory.Packages.props`; currently 12.0.9).
- [x] `App.axaml` — apply the standalone `<theme:MaterialTheme/>` (it supplies the whole M3 control
      surface; no Fluent/Simple base required), `RequestedThemeVariant="Dark"` (Light/Default available
      for a future toggle).
- [x] Use the package's M3 controls directly: `Card` (emulator rows), the `Button` variants,
      `FloatingActionButton` (refresh), `CircularProgressIndicator` (busy overlay), `PathIcon`.
- [x] `Themes/Icons.axaml` — vendored Phosphor (regular) glyphs as `StreamGeometry`, app-specific by
      design (MIT, attributed in `Assets/Icons/PHOSPHOR-LICENSE.txt`); rendered via the built-in `PathIcon`.
- [x] Style against `DynamicResource` token roles so a future Light/Dark toggle re-resolves live.
- [x] Visual confirmation — launched on Windows; confirmed.

## Notes

- The package is the source of truth for tokens, type scale, fonts, and control themes. Read its source
  (local checkout — see `CLAUDE.md` / memory) for control APIs rather than re-deriving them.
- On a package bump, prefer the package's controls/styles over local overrides.
