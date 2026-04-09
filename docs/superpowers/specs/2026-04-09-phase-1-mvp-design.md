# Phase 1 MVP Design Spec: ICY Stream Recorder

## Overview

Phase 1 delivers a functional portable Windows app for recording a single internet radio stream (ICY/SHOUTcast) with full screen reader accessibility. It de-risks `IcyStreamClient` — the highest technical risk component — and establishes the project scaffold for subsequent phases.

**Approach:** Single WPF project (`RadioReel.App`) with folder-based separation (`Core/`, `UI/`, `Infrastructure/`). Core has zero WPF dependencies by convention. Separate xUnit test project for Core classes.

---

## 1. Project Structure & Infrastructure

### Solution layout

```
RadioReel/
├── RadioReel.sln
├── global.json                    # SDK 10.0.x, latestPatch
├── .gitignore / .gitattributes / .editorconfig
├── src/
│   └── RadioReel.App/
│       ├── RadioReel.App.csproj   # net10.0-windows, WPF, single-file
│       ├── app.manifest           # asInvoker, DPI-aware
│       ├── TrimmerRoots.xml       # Protect WPF/NAudio from trimming
│       ├── App.xaml / App.xaml.cs  # DI, Serilog, crash handler
│       ├── Core/                  # Business logic (zero WPF deps)
│       ├── UI/                    # MVVM: ViewModels, Views, Dialogs
│       ├── Infrastructure/        # Logging, AccessibilityHelper
│       └── Themes/                # Dark.xaml (minimal)
└── tests/
    └── RadioReel.Tests/
        └── RadioReel.Tests.csproj # xUnit, Core class tests
```

### NuGet packages (Phase 1)

| Package | Version | Purpose |
|---------|---------|---------|
| CommunityToolkit.Mvvm | 8.4.2 | MVVM source generators |
| NAudio | 2.3.0 | Audio types (active use in Phase 3) |
| Serilog | 4.3.1 | Structured logging |
| Serilog.Sinks.File | 6.0.0 | File sink with rotation |
| Microsoft.Extensions.DependencyInjection | latest | DI container |

### DI registration (App.xaml.cs)

```csharp
var services = new ServiceCollection();
services.AddSingleton<SettingsStore>();
services.AddSingleton<MainViewModel>();
services.AddSingleton<StreamsViewModel>();
services.AddTransient<IcyStreamClient>();
services.AddTransient<StreamRecorder>();
services.AddTransient<TrackSplitter>();
```

### NAudio in Phase 1

Included in csproj but only used for content-type awareness. Recording = direct `FileStream.Write()` of raw bytes. WASAPI player deferred to Phase 3.

---

## 2. Core/Audio — IcyStreamClient & Recording Pipeline

### IcyStreamClient

TCP client for ICY/SHOUTcast streams. This is the highest-risk component.

**Lifecycle:** `ConnectAsync()` → reading loop → `StopAsync()` / `Disconnected`

**Connection protocol:**
1. Connect via `TcpClient` (not HttpClient — ICY 200 OK is not valid HTTP)
2. For `https://` URLs: wrap `NetworkStream` in `SslStream` before sending request
3. Send `GET /path HTTP/1.0` with `Icy-MetaData: 1` header
4. Parse response: `ICY 200 OK` or `HTTP/1.x 200 OK`
4. Extract `icy-metaint`, `icy-name`, `content-type` from headers
5. Reading loop: `metaint` audio bytes → 1 byte metadata size → metadata block → audio → ...
6. Publish events: `AudioDataReceived`, `MetadataChanged`, `Disconnected`, `Error`

**Metadata encoding:** Try UTF-8 first, fallback to latin-1 (ISO-8859-1).

**Threading:** Runs on a background `Task` with `CancellationToken` for graceful shutdown.

### IcyMetadataParser

Static parser. Input: `byte[]` metadata block. Output: `IcyMetadata` record. Parses `StreamTitle='Artist - Title';` format.

### StreamRecorder

Receives `AudioDataReceived` from IcyStreamClient. Writes directly to `FileStream` (no decoding/re-encoding). Responsible for flush on stop.

### TrackSplitter

Listens to `MetadataChanged`. On StreamTitle change:
1. Close current FileStream
2. Open new file via `FileNameTemplate`
3. First track marked `_incomplete` (always truncated at start)
4. Short track filter: `skipShortTracksMs` (default 30s) — measured by wall-clock elapsed time between metadata changes; if track shorter, file is deleted

### RecordingSession (coordinator)

Creates and wires IcyStreamClient + StreamRecorder + TrackSplitter.

**State machine:** `Idle` → `Connecting` → `Recording` → `Reconnecting` → `Stopped` | `Error`

**Reconnection:** Exponential backoff (5s → 10s → 20s → ..., capped at 300s), max attempts configurable (0 = infinite).

**Single `CancellationTokenSource`** controls the entire chain.

### Data flow

```
IcyStreamClient
    ├── AudioDataReceived → StreamRecorder → FileStream (raw MP3/AAC)
    └── MetadataChanged   → TrackSplitter → close old / open new file
                          → RecordingSession → UI notification
```

### PlaylistParser

Resolves URL before IcyStreamClient connects:
- `.m3u` / `.m3u8` → first non-comment line
- `.pls` → `File1=` value
- `.asx` → XML `<ref href="..."/>`
- Otherwise → URL is direct stream link

---

## 3. Core/Models, Storage, Metadata

### Models

**StreamEntry** — one radio station:
```csharp
public class StreamEntry
{
    public string Url { get; set; }
    public string Name { get; set; }
    public int MaxReconnectAttempts { get; set; }  // 0 = infinite
    public int ReconnectIntervalSec { get; set; }  // default 5
}
```

**Recording state** exposed by RecordingSession coordinator:
- `Status`: enum `Idle | Connecting | Recording | Reconnecting | Stopped | Error`
- `CurrentTrackTitle`: current StreamTitle
- `RecordedTracksCount`: number of saved tracks
- `BytesRecorded`: total bytes written
- `ConnectedAt`: connection timestamp

### Storage

**AppPaths** — all paths relative to EXE:
```csharp
public static class AppPaths
{
    public static string BaseDir => AppContext.BaseDirectory;
    public static string SettingsFile => Path.Combine(BaseDir, "radioreel_settings.json");
    public static string LogsDir => Path.Combine(BaseDir, "logs");
    public static string DefaultRecordingsDir => Path.Combine(BaseDir, "recordings");
}
```

**AppSettings** — minimal Phase 1 model:
```json
{
  "general": { "lowDiskSpaceWarningGb": 1.0 },
  "recording": {
    "defaultOutputDir": "recordings",
    "fileNameTemplate": "%s\\%a - %t",
    "incompleteFileNameTemplate": "%s\\%a - %t_incomplete",
    "streamFileNameTemplate": "%s_stream_%d",
    "skipShortTracksMs": 30000,
    "maxReconnectAttempts": 0,
    "reconnectIntervalSec": 5
  },
  "streams": []
}
```

**SettingsStore:**
- `Load()`: read file, deserialize, fallback to defaults on error
- `Save()`: serialize with `WriteIndented = true`, atomic write (temp file → rename)
- `System.Text.Json` with `JsonNamingPolicy.CamelCase`

### Metadata

**IcyMetadata:**
```csharp
public record IcyMetadata(string? StreamTitle, string? StreamUrl);
```

**FileNameTemplate:**
- Variables: `%a` (artist), `%t` (title), `%s` (station), `%d` (date YYYY-MM-DD), `%time` (HH-mm-ss), `%n` (track number — per-session counter, resets on each recording start)
- Sanitization: `\ / : * ? " < > |` → `_`, trim whitespace
- Collision: append `_2`, `_3` if file exists
- `\` in template creates subdirectories
- Artist/Title parsed from StreamTitle via `Artist - Title` pattern

---

## 4. UI — Views, ViewModels, Accessibility

### MainWindow

- Menu bar: Файл (Налаштування, Вихід), Потік (Додати, Почати запис, Зупинити)
- TabControl with single tab "Потоки"
- StatusBar at bottom

### StreamsViewModel

- `ObservableCollection<StreamEntry> Streams`
- `StreamEntry? SelectedStream`
- `RelayCommand StartRecordingCommand` (F5)
- `RelayCommand StopRecordingCommand` (F6)
- `RelayCommand AddStreamCommand` (Ctrl+N) → opens AddStreamDialog
- `RelayCommand RemoveStreamCommand`
- Holds one active `RecordingSession` (Phase 1 = single recording)
- Updates bound properties on session state changes (Status, CurrentTrack)

### MainViewModel

- Holds `StreamsViewModel`
- Updates StatusBar: connection state, active recordings count, free disk space
- Free disk space: `DriveInfo` check on start + every 60s

### AddStreamDialog

- TextBox "URL потоку" (required), `AutomationProperties.Name="URL потоку"`
- TextBox "Назва станції" (optional, fallback to icy-name), `AutomationProperties.Name="Назва станції"`
- Button "Додати" (`IsDefault=True`)
- Button "Скасувати" (`IsCancel=True`)

### Accessibility (vertical requirement — every file, every commit)

**Every interactive control gets:**
- `AutomationProperties.Name`
- `AutomationProperties.HelpText` where appropriate
- `KeyboardNavigation.TabIndex` — logical order

**Live regions (screen reader auto-announces):**

| Event | LiveSetting | Text |
|-------|-------------|------|
| Recording started | Assertive | "Запис розпочато: {name}" |
| Recording stopped | Assertive | "Запис зупинено: {name}" |
| Track changed | Polite | "Поточний трек: {artist} — {title}" |
| Connection error | Assertive | "Помилка: {name} — {details}" |
| Reconnecting | Polite | "Перепідключення: {name}, спроба {n}" |

**AccessibilityHelper:**
```csharp
public static class AccessibilityHelper
{
    public static void AnnounceAssertive(string message) { ... }
    public static void AnnouncePolite(string message) { ... }
}
```
Implementation via `AutomationPeer.RaiseAutomationEvent` on a hidden `TextBlock` with `LiveSetting`.

**Keyboard shortcuts:**

| Action | Key | Implementation |
|--------|-----|----------------|
| Start recording | F5 | `InputBinding` + `KeyBinding` |
| Stop recording | F6 | `InputBinding` + `KeyBinding` |
| Add stream | Ctrl+N | `InputBinding` + `KeyBinding` |
| Context menu | Shift+F10 | Native WPF |
| Navigation | Tab, arrows, Home, End | Native WPF |

**Tab order (StreamsTab):**
1. Button "Почати запис"
2. Button "Зупинити запис"
3. ListView "Список потоків" (columns: Назва, Статус, Поточний трек)

---

## 5. Infrastructure — Logging, Theme, Error Handling

### Serilog

- Sink: file `logs/radioreel.log`
- Rotation: by size (10 MB), retain 3 files
- Format: `[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message}{NewLine}{Exception}`
- Station name in messages: `[StationName] Connected`, `[StationName] Track changed: ...`
- Initialized in `App.xaml.cs` before UI creation

### Dark.xaml (minimal Phase 1 theme)

- Dark theme only (no Light, no auto-detect — deferred to Phase 4)
- High-contrast colors for accessibility
- Base styles: Window, Button, TextBox, ListView, Menu, StatusBar, Label
- Visible focus indicator on every interactive element (mandatory for keyboard nav)

### Error handling

**Global:**
- `App.DispatcherUnhandledException` — log, show dialog, don't crash
- `TaskScheduler.UnobservedTaskException` — log
- `AppDomain.CurrentDomain.UnhandledException` — log, graceful shutdown

**Per-recording:**
- Network errors → `RecordingSession` handles via reconnect logic
- File system errors (disk full, permissions) → `Error` state, log, UI notification
- Invalid ICY response → `Error` state with details

### Graceful shutdown (App closing)

1. Stop active `RecordingSession` (flush file)
2. Save `AppSettings`
3. Flush Serilog

---

## 6. Testing

### RadioReel.Tests (xUnit)

**Unit-tested:**
- `IcyMetadataParser` — various StreamTitle formats, UTF-8/latin-1 encoding, edge cases (empty, no separator)
- `FileNameTemplate` — variable substitution, character sanitization, collisions, subdirectories
- `PlaylistParser` — M3U, PLS, ASX parsing, direct URL passthrough
- `AppSettings` / `SettingsStore` — serialization/deserialization, defaults, corrupted file

**Not unit-tested (requires network/UI):**
- `IcyStreamClient` — manual integration testing with real stream
- WPF Views — manual testing with NVDA

---

## 7. Implementation Order

Per phase-1-mvp.md, 11 blocks in sequence. Each block ends with `dotnet build`.

1. Scaffolding: `.gitignore`, `.gitattributes`, `.editorconfig`, `global.json`, `.sln`, `.csproj`, `App.xaml/cs`, `app.manifest`, `TrimmerRoots.xml`
2. Infrastructure: `AppPaths`, `LoggingConfiguration`, `AccessibilityHelper`, `Dark.xaml`
3. UI shell: `MainWindow` (Menu + TabControl + StatusBar)
4. Models & settings: `StreamEntry`, `AppSettings`, `SettingsStore`
5. Parsers: `IcyMetadata`, `IcyMetadataParser`, `PlaylistParser`
6. Network: `IcyStreamClient` (TCP client, connect, stream reading)
7. File output: `FileNameTemplate`, `StreamRecorder`, `TrackSplitter`
8. Coordinator: `RecordingSession` (wires Client + Recorder + Splitter)
9. UI binding: `StreamsViewModel`, `StreamsTab` (ListView, buttons, data binding)
10. Dialog: `AddStreamDialog`
11. Integration: end-to-end test with real stream, bug fixes

---

## 8. Completion Criteria

- [ ] Add stream by URL (dialog or menu)
- [ ] Connect to ICY/SHOUTcast stream
- [ ] Record stream to disk with track splitting by metadata
- [ ] File naming via template (%a, %t, %s)
- [ ] Auto-reconnect on disconnect
- [ ] Graceful recording stop (file flush)
- [ ] NVDA announces: recording start/stop, track change, errors
- [ ] Full keyboard navigation (F5, F6, Ctrl+N, Tab, arrows)
- [ ] Portable single-file EXE (`dotnet publish`)
- [ ] Structured logging to file with rotation

---

## Out of Scope (Phase 1)

- Multi-stream recording
- Wishlist / Ignorelist
- Scheduler
- WASAPI player
- Radio Browser API
- Tag editing (TagLibSharp)
- Profiles
- Postprocessing
- Localization (hardcoded uk-UA strings)
- Light theme / auto-detect
