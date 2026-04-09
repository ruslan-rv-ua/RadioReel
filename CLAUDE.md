# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

RadioReel — portable Windows radio recording app (.NET 10 + WPF). Target: single self-contained EXE, full screen reader accessibility (NVDA, JAWS, Narrator), no installer.

Full specs: [docs/PRD.md](docs/PRD.md) · [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · [docs/phases/index.md](docs/phases/index.md)

## Build & Run

```bash
dotnet build
dotnet run --project src/RadioReel.App
dotnet publish -c Release -r win-x64   # single-file portable EXE
dotnet test                             # unit tests (xUnit)
```

## Architecture

```
Core/           — zero WPF deps, fully unit-testable
  Audio/        — IcyStreamClient (custom TcpClient), StreamRecorder, AudioPlayer
  Metadata/     — ICY parsing, ID3/AAC tagging, filename templates
  Network/      — Radio Browser API, playlist parsing (.m3u/.pls)
  Scheduling/   — scheduled recording rules
  Storage/      — settings & profile persistence
  Models/       — StreamEntry, RecordingSession, SavedTrack, etc.

UI/             — MVVM via CommunityToolkit.Mvvm source generators
  ViewModels/   — one VM per tab/panel
  Views/        — 5 tabs + fixed Player/Status panels
  Controls/     — accessible custom controls

Infrastructure/ — Serilog, localization (uk-UA/en-US), dark/light theme, WPF converters
```

Data files live **next to the EXE** (portable): `radioreel_settings.json`, `profiles/*.rrprofile`, `recordings/`, `logs/`.

## Key Constraints

**Accessibility is non-negotiable:** every new control gets `AutomationProperties.Name/Role/HelpText` in the same commit it is created. Live regions for dynamic state changes (recording start/stop, track splits). See [docs/PRD.md](docs/PRD.md#accessibility) for full requirements.

**ICY streaming:** NAudio does not handle `ICY 200 OK` — use a custom `TcpClient`-based `IcyStreamClient`. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the dual-pipeline design (record + playback from one stream).

**No re-encoding on record:** save streams as-is (MP3/AAC); conversion only on explicit user request.

**Portability:** no writes to `%AppData%` or registry (except optional autostart). `PublishSingleFile` + `PublishTrimmed` + `EnableCompressionInSingleFile`. Protect reflection-heavy libs via `TrimmerRoots.xml`.

## Development Phases

Phases 2 and 3 can proceed in parallel; Phase 4 requires stable 2+3. See [docs/phases/index.md](docs/phases/index.md).

| Phase | Focus |
|-------|-------|
| 1 | `IcyStreamClient` + single-stream recording + accessibility foundation |
| 2 | Multi-stream, Wishlist/Ignorelist, Scheduler |
| 3 | WASAPI player, saved-songs library, tag editor |
| 4 | Radio Browser, profiles, post-processing, localization, themes |
