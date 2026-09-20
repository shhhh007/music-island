<div align="center">
<img src="../assets/logo.svg" width="80" alt="Music Island">

# Music Island

**Your music, one glance away.**

A compact, always-on-top music overlay for Windows.

[Русский](../README.md) · [Install](INSTALL.md) · [Privacy](PRIVACY.md)
</div>

Show artwork, playback controls, progress and synchronized lyrics without switching away from your work. The island expands with a spring animation and collapses into a small pill.

## Get started

Download the Setup EXE from the repository's **Releases → Latest**, run it and select **Install**. Administrator privileges are not required. Alternatively, extract the entire portable ZIP and run `Music Island.exe`.

Windows 10 1809+ / Windows 11 x64, .NET Framework 4.8 and Windows PowerShell 5.1 are required. There is no account or subscription. Right-click for settings; **Ctrl+Alt+I** toggles visibility.

## What works independently

Windows media-session metadata, artwork, supported playback controls, seeking, automatic artwork color, the compact clock and line-synchronized lyrics from LRCLIB. SoundCloud and other websites must expose a Windows media session through the browser; arbitrary audio without metadata cannot be identified.

## Optional integrations

A compatible server on `127.0.0.1:8770` can supply frequency bands, application volume and word-timed lyrics. **That server is not bundled.** Without it the equalizer rests as dots, and LRCLIB supplies line-level lyrics when available. Games must run in borderless windowed mode; exclusive fullscreen is not supported.

The installer is currently unsigned. SHA-256 checksums and source code are provided with each release. No analytics or audio recording are included; LRCLIB receives the track title, artist and duration for lyric lookup.

Licensed under [MIT](../LICENSE).
