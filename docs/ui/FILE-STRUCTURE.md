# Структура файлів проєкту RadioReel

## Затверджено: Етап 2

Базується на ARCHITECTURE.md §2 з урахуванням рішень Етапу 1 (UI-концепція).

---

## Дерево файлів

```
RadioReel/
├── .gitignore                             # dotnet new gitignore
├── .gitattributes                         # dotnet new gitattributes
├── .editorconfig                          # C# стиль, naming, severity
├── global.json                            # SDK pin: 10.0.x (latestPatch)
├── RadioReel.sln
└── src/
    └── RadioReel.App/
        ├── RadioReel.App.csproj
        ├── App.xaml
        ├── App.xaml.cs
        ├── app.manifest
        ├── TrimmerRoots.xml
        ├── Resources/
        │   └── radioreel.ico
        │
        ├── Core/                          # Бізнес-логіка (без UI)
        │   ├── Audio/
        │   │   ├── IcyStreamClient.cs       # ICY/SHOUTcast HTTP клієнт
        │   │   ├── IcyMetadataParser.cs     # Парсинг ICY метаданих
        │   │   ├── StreamRecorder.cs        # Запис потоку на диск
        │   │   ├── TrackSplitter.cs         # Розділення треків за метаданими
        │   │   └── AudioPlayer.cs           # Відтворення (NAudio WASAPI)
        │   │
        │   ├── Metadata/
        │   │   ├── IcyMetadata.cs           # ICY metadata model
        │   │   ├── TagEditor.cs             # Запис ID3/AAC тегів (TagLibSharp)
        │   │   └── FileNameTemplate.cs      # Шаблонізатор імен файлів
        │   │
        │   ├── Storage/
        │   │   ├── AppPaths.cs              # Шляхи відносно EXE
        │   │   ├── AppSettings.cs           # Модель глобальних налаштувань
        │   │   ├── ProfileData.cs           # Модель per-profile даних
        │   │   ├── SettingsStore.cs          # JSON зберігання глобальних налаштувань
        │   │   └── ProfileStore.cs          # JSON зберігання профілів
        │   │
        │   ├── Scheduling/
        │   │   └── RecordingScheduler.cs    # Планові записи
        │   │
        │   ├── Network/
        │   │   ├── RadioBrowserClient.cs    # Прямі HTTP запити до Radio Browser API
        │   │   └── PlaylistParser.cs        # Парсинг M3U/PLS/ASX
        │   │
        │   └── Models/
        │       ├── StreamEntry.cs           # Модель інтернет-радіостанції
        │       ├── RecordingSession.cs      # Сесія запису
        │       ├── SavedTrack.cs            # Збережений трек
        │       ├── WishlistEntry.cs         # Запис у wishlist
        │       ├── IgnorelistEntry.cs       # Запис у ignorelist
        │       └── ScheduledRecording.cs    # Запланований запис
        │
        ├── UI/                              # Шар представлення (WPF)
        │   ├── ViewModels/
        │   │   ├── MainViewModel.cs
        │   │   ├── StreamsViewModel.cs      # Список потоків
        │   │   ├── PlayerViewModel.cs       # Програвач
        │   │   ├── SavedSongsViewModel.cs   # Збережені пісні
        │   │   ├── WishlistViewModel.cs     # Wishlist + Ignorelist
        │   │   ├── ScheduledRecordingsViewModel.cs  # Заплановані записи
        │   │   ├── LogViewModel.cs          # Журнал
        │   │   ├── SettingsViewModel.cs     # Налаштування
        │   │   └── StreamBrowserViewModel.cs # Браузер станцій
        │   │
        │   ├── Views/
        │   │   ├── MainWindow.xaml
        │   │   ├── MainWindow.xaml.cs
        │   │   ├── Tabs/
        │   │   │   ├── StreamsTab.xaml / .cs
        │   │   │   ├── SavedSongsTab.xaml / .cs
        │   │   │   ├── WishlistTab.xaml / .cs
        │   │   │   ├── ScheduledRecordingsTab.xaml / .cs
        │   │   │   └── LogTab.xaml / .cs
        │   │   ├── Panels/
        │   │   │   ├── PlayerPanel.xaml / .cs
        │   │   │   └── StatusBar.xaml / .cs
        │   │   └── Dialogs/
        │   │       ├── AddStreamDialog.xaml / .cs
        │   │       ├── StreamSettingsDialog.xaml / .cs
        │   │       ├── SettingsDialog.xaml / .cs
        │   │       ├── StreamBrowserDialog.xaml / .cs  ← замість Panel
        │   │       ├── ScheduledRecordingDialog.xaml / .cs  ← нове
        │   │       └── TagEditorDialog.xaml / .cs  ← нове
        │   │
        │   └── Controls/
        │       ├── AccessibleListView.cs    # ListView з розширеним AutomationPeer
        │       └── VolumeSlider.xaml / .cs  # Слайдер гучності з доступністю
        │
        ├── Infrastructure/
        │   ├── Logging/
        │   │   └── LoggingConfiguration.cs  # Serilog налаштування
        │   ├── Localization/
        │   │   ├── LocalizationManager.cs   # Визначення мови
        │   │   ├── uk-UA.xaml               # Українська
        │   │   └── en-US.xaml               # Англійська
        │   ├── Accessibility/
        │   │   └── AccessibilityHelper.cs   # Live region helper
        │   ├── Themes/
        │   │   └── ThemeManager.cs          # Шар 2 — управління темою
        │   └── Converters/
        │       └── (за потребою)
        │
        └── Themes/                          # ResourceDictionary файли
            ├── Light.xaml                   # Шар 2 — світла тема
            └── Dark.xaml                    # Шар 2 — темна тема
```

---

## Зміни відносно ARCHITECTURE.md §2

| Зміна | Обґрунтування |
|-------|---------------|
| `Panels/StreamBrowserPanel.xaml` → `Dialogs/StreamBrowserDialog.xaml` | Рішення Етапу 1 — модальний діалог |
| Додано `Dialogs/TagEditorDialog.xaml` | PRD §4.7 — редагування тегів |
| Додано `Dialogs/ScheduledRecordingDialog.xaml` | PRD §4.4 — форма додавання/редагування |
| Додано `Core/Network/RadioBrowserClient.cs` | ARCHITECTURE §10 — прямі HTTP запити |
| Додано `Core/Network/PlaylistParser.cs` | ARCHITECTURE §5.1 — парсинг M3U/PLS/ASX |
| Додано `Core/Models/ScheduledRecording.cs` | PRD §4.4 — модель запланованого запису |
| Додано `Infrastructure/Accessibility/AccessibilityHelper.cs` | ARCHITECTURE §7.3 — live region helper |
| Додано `Infrastructure/Themes/ThemeManager.cs` | ARCHITECTURE §6.4 — управління темою |
| Додано `Infrastructure/Localization/LocalizationManager.cs` | ARCHITECTURE §6.5 — визначення мови |

---

## Кількість файлів

| Категорія | Файлів |
|-----------|--------|
| Core (бізнес-логіка) | 18 |
| UI (ViewModels) | 9 |
| UI (Views — XAML + .cs) | 26 (13 пар) |
| UI (Controls) | 3 |
| Infrastructure | 6 |
| Themes | 2 |
| Проєкт (.csproj, App, manifest, TrimmerRoots) | 4 |
| **Разом** | **~68** |
