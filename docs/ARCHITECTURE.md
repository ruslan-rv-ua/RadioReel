# Архітектура: RadioReel

## Технологічний стек: .NET 10 + WPF (C#)

---

## 1. Огляд технологічного стеку

| Компонент | Технологія | Версія | Ліцензія |
|-----------|-----------|--------|----------|
| **Runtime** | .NET 10 | 10.0 | MIT |
| **UI Framework** | WPF (Windows Presentation Foundation) | .NET 10 вбудовано | MIT |
| **UI Архітектура** | MVVM | — | — |
| **MVVM Toolkit** | CommunityToolkit.Mvvm | 8.4.2 | MIT |
| **Аудіо** | NAudio | 2.3.0 | MIT |
| **Теги** | TagLibSharp | 2.3.0 | LGPL-2.1 |
| **Логування** | Serilog | 4.3.1 | Apache-2.0 |
| **Серіалізація** | System.Text.Json | вбудовано в .NET 10 | MIT |

### 1.1. Обґрунтування вибору

**.NET 10 + WPF** обрано через:
- **Найкраща доступність**: нативна підтримка UI Automation через `AutomationPeer` для кожного контролу — найкраща сумісність з NVDA, JAWS та Windows Narrator
- **NAudio** — зрілий аудіо-фреймворк для .NET з підтримкою потокового відтворення та запису
- **Портативна публікація**: єдиний self-contained EXE без зовнішніх залежностей
- **Єдина мова** (C#) для всього проекту

### 1.2. Версії та сумісність

```xml
<!-- Мінімальна версія ОС -->
<SupportedOSPlatformVersion>10.0.22000.0</SupportedOSPlatformVersion>
```

- ✅ Windows 11 (build 22000+)
- ✅ .NET 10 вбудований в EXE (self-contained)

---

## 2. Структура проекту

```
RadioReel/
├── RadioReel.sln
└── src/
    └── RadioReel.App/                    # Єдиний проект WPF
        ├── RadioReel.App.csproj
        ├── App.xaml / App.xaml.cs
        │
        ├── Core/                   # Бізнес-логіка (без UI)
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
        │   │   ├── AppSettings.cs           # Модель налаштувань
        │   │   ├── ProfileData.cs           # Модель per-profile даних
        │   │   ├── SettingsStore.cs         # JSON зберігання глобальних налаштувань
        │   │   └── ProfileStore.cs          # JSON зберігання профілів
        │   │
        │   ├── Scheduling/
        │   │   └── RecordingScheduler.cs    # Планові записи
        │   │
        │   └── Models/
        │       ├── StreamEntry.cs           # Модель інтернет-радіостанції
        │       ├── RecordingSession.cs      # Сесія запису
        │       ├── SavedTrack.cs            # Збережений трек
        │       ├── WishlistEntry.cs         # Запис у wishlist
        │       └── IgnorelistEntry.cs       # Запис у ignorelist
        │
        ├── UI/                              # Шар представлення (WPF)
        │   ├── ViewModels/
        │   │   ├── MainViewModel.cs
        │   │   ├── StreamsViewModel.cs      # Список потоків
        │   │   ├── PlayerViewModel.cs       # Програвач
        │   │   ├── SavedSongsViewModel.cs   # Збережені пісні
        │   │   ├── StreamBrowserViewModel.cs # Браузер станцій
        │   │   ├── WishlistViewModel.cs
        │   │   ├── ScheduledRecordingsViewModel.cs  # Заплановані записи
        │   │   ├── LogViewModel.cs
        │   │   └── SettingsViewModel.cs
        │   │
        │   ├── Views/
        │   │   ├── MainWindow.xaml / .cs
        │   │   ├── Tabs/
        │   │   │   ├── StreamsTab.xaml
        │   │   │   ├── SavedSongsTab.xaml
        │   │   │   ├── WishlistTab.xaml
        │   │   │   ├── ScheduledRecordingsTab.xaml
        │   │   │   └── LogTab.xaml
        │   │   ├── Panels/
        │   │   │   ├── PlayerPanel.xaml
        │   │   │   ├── StreamBrowserPanel.xaml
        │   │   │   └── StatusBar.xaml
        │   │   └── Dialogs/
        │   │       ├── AddStreamDialog.xaml
        │   │       ├── StreamSettingsDialog.xaml
        │   │       └── SettingsDialog.xaml
        │   │
        │   └── Controls/
        │       ├── AccessibleListView.cs    # ListView з розширеним AutomationPeer
        │       └── VolumeSlider.xaml        # Слайдер гучності з доступністю
        │
        └── Infrastructure/
            ├── Logging/
            │   └── LoggingConfiguration.cs  # Serilog налаштування
            ├── Localization/
            │   ├── uk-UA.xaml              # Українська мова
            │   └── en-US.xaml              # Англійська мова
            └── Converters/                 # WPF ValueConverters
```

---

## 3. Конфігурація проекту (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Таргет і платформа -->
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <SupportedOSPlatformVersion>10.0.22000.0</SupportedOSPlatformVersion>

    <!-- Портативний EXE -->
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>true</PublishTrimmed>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
    <TrimmerRootDescriptor>TrimmerRoots.xml</TrimmerRootDescriptor>

    <!-- Метадані EXE -->
    <AssemblyName>radioreel</AssemblyName>
    <ApplicationIcon>Resources\radioreel.ico</ApplicationIcon>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>

  <ItemGroup>
    <!-- Аудіо -->
    <PackageReference Include="NAudio" Version="2.3.0" />

    <!-- Теги -->
    <PackageReference Include="TagLibSharp" Version="2.3.0" />

    <!-- MVVM -->
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />

    <!-- Логування -->
    <PackageReference Include="Serilog" Version="4.3.1" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
  </ItemGroup>
</Project>
```

> **Примітка щодо PublishTrimmed**: WPF та NAudio використовують рефлексію. Необхідно прописати `TrimmerRoots.xml` з переліком типів, що не підлягають триму, щоб уникнути runtime помилок. Типовий розмір EXE після публікації: **50–80 МБ**.

---

## 4. MVVM Архітектура

### 4.1. Шаблон MVVM з CommunityToolkit.Mvvm

```
View (XAML) ←→ ViewModel (C#) ←→ Model/Service (C#)
```

**CommunityToolkit.Mvvm 8.4.2** надає source generators, що суттєво зменшують boilerplate:

```csharp
// Приклад ViewModel з source generators
public partial class StreamsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _streamUrl = string.Empty;

    [ObservableProperty]
    private bool _isRecording;

    [RelayCommand]
    private async Task StartRecordingAsync()
    {
        // ...
    }
}
```

### 4.2. Повідомлення між ViewModel (Messenger)

```
WeakReferenceMessenger (CommunityToolkit.Mvvm)
  StreamStartedMessage  → PlayerViewModel, LogViewModel
  TrackChangedMessage   → UI live region оновлення
  RecordingErrorMessage → StatusBar, Notifications
```

### 4.3. Dependency Injection

Використовується **Microsoft.Extensions.DependencyInjection** (вбудовано в .NET 10):

```csharp
// App.xaml.cs
var services = new ServiceCollection();
services.AddSingleton<SettingsStore>();
services.AddSingleton<ProfileStore>();
services.AddTransient<IcyStreamClient>();
services.AddTransient<StreamRecorder>();
// ... ViewModels
services.AddSingleton<MainViewModel>();
```

---

## 5. Аудіо-підсистема

### 5.1. Обробка ICY/SHOUTcast/Icecast потоків

> **⚠️ Важливе уточнення:** NAudio **не має** вбудованої підтримки ICY протоколу. `MediaFoundationReader` не підтримує сервери, що повертають `ICY 200 OK` замість стандартного `HTTP/1.1 200 OK`. ICY метадані та підключення потрібно реалізувати власноруч.

#### Протокол ICY (SHOUTcast/Icecast)

```
Клієнт → сервер:
  GET /stream HTTP/1.0
  Host: radio.example.com
  Icy-MetaData: 1           ← запит на метадані

Сервер → клієнт:
  ICY 200 OK                ← нестандартний статус (не "HTTP/1.1 200 OK")
  icy-name: Radio Name
  icy-metaint: 16000        ← аудіо-байти між блоками метаданих
  Content-Type: audio/mpeg
  [аудіо байти][1 байт - розмір блоку метаданих][метадані][аудіо байти]...
```

#### Реалізація `IcyStreamClient`

```csharp
// Core/Audio/IcyStreamClient.cs
public class IcyStreamClient
{
    // Використовує Socket або HttpClient з власною обробкою рядка статусу
    // 1. Встановити TCP з'єднання
    // 2. Надіслати GET запит з "Icy-MetaData: 1"
    // 3. Прочитати заголовки відповіді (включаючи "ICY 200 OK")
    // 4. Розпарсити icy-metaint
    // 5. Читати потік, виділяючи аудіо-байти та блоки метаданих
    // 6. Передавати аудіо-байти в BufferedWaveProvider NAudio
    // 7. Публікувати подію при зміні метаданих

    public event EventHandler<IcyMetadata>? MetadataChanged;
    // AudioStream → передається в NAudio для відтворення/запису
}
```

#### Підтримка плейлістів

Перед підключенням до потоку необхідно розпарсити плейлісти:

| Формат | Парсинг |
|--------|---------|
| M3U / M3U8 | Ручна реалізація (простий текстовий формат) |
| PLS | Ручна реалізація (INI-подібний формат) |
| ASX | `System.Xml` — стандартний XML |

### 5.2. Схема потоку даних для запису

> **⚠️ Важливе уточнення щодо збереження:** ICY-потік уже містить закодований MP3/AAC. Аудіо-байти **не декодуються** і не перекодуються — вони зберігаються напряму через `FileStream`. `WaveFileWriter` (WAV-контейнер NAudio) тут **не використовується**. `MediaFoundationEncoder` застосовується лише у функції постобробки (конвертація на вимогу, §4.8 PRD).

```
[IcyStreamClient]
      ↓ raw аудіо-байти (encoded MP3 / AAC)
      ├─────────────────────────────────┐
      ↓                                 ↓
[BufferedWaveProvider]          [StreamRecorder]
      ↓ (decoded PCM)            (FileStream → .mp3 / .aac)
[AudioPlayer]                        ↓ при зміні трек-метаданих
(WASAPI Out)                    [TrackSplitter]
                                     ↓ фінальний файл
                                [TagEditor] → TagLibSharp
```

**Два незалежних шляхи для одних і тих самих байт:**
- **Відтворення** (`AudioPlayer`): `IcyStreamClient` → `BufferedWaveProvider` → NAudio декодує → `WasapiOut`
- **Запис** (`StreamRecorder`): `IcyStreamClient` → `FileStream.Write(rawBytes)` → файл .mp3/.aac на диску

### 5.3. NAudio — основні компоненти

| Компонент NAudio | Використання |
|-----------------|-------------|
| `BufferedWaveProvider` | Буфер між мережевим потоком та програвачем |
| `WasapiOut` | Відтворення (WASAPI shared mode) |
| `WaveFileWriter` | **Не використовується** для ICY-запису; залишається для можливого майбутнього WAV-виводу |
| `MediaFoundationEncoder` | Конвертація у інший формат (постобробка за §4.8 PRD) |
| `Mp3FileReader` | Читання записаних MP3 для редагування/відтворення |
| `AudioFileReader` | Відтворення записаних файлів |
| `SampleChannel` | Керування гучністю |

### 5.4. Запис декількох потоків одночасно

Кожен потік записується в окремому `Task` з власним `CancellationToken`:

```csharp
// Управляється через RecordingSession - незалежні Task<> для кожного потоку
var session = new RecordingSession(stream, cancellationToken);
// Task.Run не блокує UI
```

---

## 6. Зберігання даних

### 6.1. Портативна стратегія

Всі дані зберігаються **поряд з EXE файлом**:

```csharp
// AppPaths.cs
public static class AppPaths
{
    // Визначаємо базову директорію відносно EXE, а не %AppData%
    public static readonly string BaseDir = AppContext.BaseDirectory;
    public static readonly string SettingsFile = Path.Combine(BaseDir, "radioreel_settings.json");
    public static readonly string ProfilesDir = Path.Combine(BaseDir, "profiles");
    public static readonly string LogsDir = Path.Combine(BaseDir, "logs");
    public static readonly string LogsFile = Path.Combine(LogsDir, "radioreel.log");
    public static readonly string DefaultRecordingsDir = Path.Combine(BaseDir, "recordings");

    public static string ProfileFile(string profileName) =>
        Path.Combine(ProfilesDir, $"{profileName}.rrprofile");
}
```

> **Примітка**: При публікації як SingleFile, `AppContext.BaseDirectory` повертає директорію EXE, а не тимчасову папку розпакування. Для `Process.GetCurrentProcess().MainModule.FileName` та `Environment.ProcessPath` поведінка аналогічна.

> **Автозапуск**: Функція "Автозапуск з Windows" записує/видаляє значення в `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. При запуску з USB-носія літера диску може змінитись — при включенні автозапуску програма відображає попередження. Управляється полем `general.autoStartEnabled`.

### 6.2. Структура файлів налаштувань

**`radioreel_settings.json`** — **глобальні** налаштування програми (спільні для всіх профілів):

```json
{
  "activeProfile": "Default",
  "general": {
    "language": "uk-UA",
    "theme": "auto",
    "minimizeToTray": true,
    "showTrayNotifications": true,
    "autoStartEnabled": false,
    "lowDiskSpaceWarningGb": 1.0
  },
  "player": {
    "outputDeviceId": ""
  },
  "hotkeys": {
    "startStopRecording": "Ctrl+Shift+R",
    "playPause":          "Ctrl+Shift+P",
    "nextStream":         "Ctrl+Shift+Right",
    "prevStream":         "Ctrl+Shift+Left",
    "volumeUp":           "Ctrl+Shift+Up",
    "volumeDown":         "Ctrl+Shift+Down",
    "toggleWindow":       "Ctrl+Shift+H"
  }
}
```

**`profiles/Default.rrprofile`** — **per-profile** дані (окремий файл для кожного профілю):

```json
{
  "recording": {
    "defaultOutputDir": "recordings",
    "fileNameTemplate": "%s\\%a - %t",
    "incompleteFileNameTemplate": "%s\\%a - %t_incomplete",
    "streamFileNameTemplate": "%s_stream_%date",
    "skipShortTracksMs": 30000,
    "bandwidthLimitKbps": 0
  },
  "playerSession": {
    "volume": 0.8
  },
  "postprocessing": {
    "convertEnabled": false,
    "convertTargetFormat": "mp3",
    "scriptEnabled": false,
    "scriptPath": "",
    "scriptArgs": "%file%",
    "scriptTimeoutSec": 120,
    "queue": []
  },
  "streams": [
    {
      "url": "...",
      "name": "...",
      "maxReconnectAttempts": 0,
      "reconnectIntervalSec": 5,
      "localIgnorelist": []
    }
  ],
  "savedTracks": [ ],
  "wishlist": [ ],
  "ignorelist": [ ],
  "scheduledRecordings": [
    {
      "streamUrl": "...",
      "dayOfWeek": "Monday",
      "startTime": "08:00",
      "durationMin": 60,
      "repeat": true,
      "enabled": true
    }
  ],
  "activeRecordingUrls": [ ]
}
```

> **Per-stream ignorelist**: кожен `StreamEntry` містить власний `localIgnorelist: List<IgnorelistEntry>` (відповідно до PRD §4.2.2). Глобальний `ignorelist` в корені профілю покриває всі потоки.

> **Розподіл відповідальності**: `radioreel_settings.json` — незмінний при перемиканні профілів (мова, тема, пристрій, хоткеї). `.rrprofile` — повністю замінюється при завантаженні іншого профілю.

### 6.3. Серіалізація

```csharp
// System.Text.Json (вбудовано в .NET 10, без залежностей)
var options = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
```

---

### 6.4. Теми оформлення

Тема визначається в `App.xaml.cs` при старті і при зміні налаштування. Використовується WPF `ResourceDictionary` (Мержа `Light.xaml` та `Dark.xaml`).

```csharp
public static class ThemeManager
{
    // Алгоритм визначення теми:
    // 1. Читаємо налаштування з radioreel_settings.json ("auto" | "dark" | "light")
    // 2. Якщо "auto" — читаємо реєстр Windows:
    //    HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize
    //    Параметр: AppsUseLightTheme (DWORD): 1 = світла, 0 = темна
    // 3. При помилці читання — fallback на темну тему

    public static AppTheme DetectWindowsTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return (value is int v && v == 1) ? AppTheme.Light : AppTheme.Dark;
        }
        catch
        {
            return AppTheme.Dark; // fallback
        }
    }

    public static void ApplyTheme(AppTheme theme)
    {
        var uri = theme == AppTheme.Light
            ? new Uri("pack://application:,,,/Themes/Light.xaml")
            : new Uri("pack://application:,,,/Themes/Dark.xaml");

        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(
            new ResourceDictionary { Source = uri });
    }
}

public enum AppTheme { Light, Dark }
```

Структура тем:

```
RadioReel.App/
    Themes/
        Light.xaml    # Світла висококонтрастна тема
        Dark.xaml     # Темна висококонтрастна тема
```

### 6.5. Локалізація

Мова визначається при запуску з `radioreel_settings.json`. При першому запуску (settings відсутні) мова визначається автоматично і зберігається.

```csharp
public static class LocalizationManager
{
    // Алгоритм:
    // 1. Читаємо settings.language (якщо файл існує)
    // 2. Якщо не встановлено — визначаємо з CultureInfo.CurrentUICulture
    // 3. Якщо локалізація не підтримується або помилка — fallback на "en-US"
    // 4. Зберігаємо результат в settings.language

    public static string DetectLanguage()
    {
        try
        {
            var culture = CultureInfo.CurrentUICulture.Name; // e.g. "uk-UA"
            return IsSupportedLanguage(culture) ? culture : "en-US";
        }
        catch
        {
            return "en-US"; // fallback
        }
    }

    private static bool IsSupportedLanguage(string culture)
        => culture.StartsWith("uk", StringComparison.OrdinalIgnoreCase)
        || culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
}
```

---

## 7. Доступність (Accessibility)

### 7.1. Архітектурний підхід

WPF надає нативну підтримку **UI Automation (UIA)** через `AutomationPeer`. Кожен стандартний WPF-контрол має відповідний `AutomationPeer`:

| Контрол | AutomationPeer | Функція |
|---------|---------------|---------|
| `Button` | `ButtonAutomationPeer` | Назва, стан, виклик дії |
| `ListView` | `ListViewAutomationPeer` | Навігація по рядках/стовпцях |
| `TabControl` | `TabControlAutomationPeer` | Навігація між вкладками |
| `Slider` | `SliderAutomationPeer` | Читання/зміна значення (гучність) |
| `ProgressBar` | `ProgressBarAutomationPeer` | Озвучення прогресу |
| `CheckBox` | `CheckBoxAutomationPeer` | Стан: увімкнено/вимкнено |

### 7.2. Обов'язкові XAML-атрибути

```xaml
<!-- Доступна кнопка -->
<Button AutomationProperties.Name="Почати запис"
        AutomationProperties.HelpText="Починає запис обраного потоку"
        ToolTip="Почати запис (F5)">
    <Image Source="..." />
</Button>

<!-- Стовпець ListView з доступністю -->
<GridViewColumn Header="Назва станції"
                AutomationProperties.Name="Назва станції"
                DisplayMemberBinding="{Binding Name}" />

<!-- Поле вводу -->
<TextBox AutomationProperties.LabeledBy="{Binding ElementName=urlLabel}"
         AutomationProperties.Name="URL потоку" />
```

### 7.3. Live Regions (динамічні оновлення)

Live Regions забезпечують автоматичне оголошення скрінрідером при зміні вмісту (назва треку, статус запису):

```csharp
// Базовий клас для live region
public static class AccessibilityHelper
{
    public static void AnnounceToScreenReader(string message, 
        AutomationLiveSetting liveSetting = AutomationLiveSetting.Polite)
    {
        // WPF підхід: AutomationProperties.LiveSetting
        // при зміні тексту TextBlock, скрінрідер оголосить нове значення
    }

    // Для критичних подій (помилки, кінець запису):
    public static void AnnounceAssertive(string message)
    {
        // AutomationLiveSetting.Assertive - перериває поточне оголошення
    }
}
```

```xaml
<!-- Live region для назви треку -->
<TextBlock x:Name="CurrentTrackText"
           AutomationProperties.LiveSetting="Polite"
           Text="{Binding CurrentTrack}" />

<!-- Live region для статусу (помилки) -->
<TextBlock AutomationProperties.LiveSetting="Assertive"
           Text="{Binding StatusMessage}"
           Visibility="Collapsed" />
```

### 7.4. Навігація клавіатурою

| Дія | Клавіша |
|-----|---------|
| Перемикання вкладок | `Ctrl+1` … `Ctrl+5` |
| Навігація в списку | `↑` `↓` `Home` `End` |
| Контекстне меню | `Shift+F10` або клавіша `Menu` |
| Почати/зупинити запис | `F5` / `F6` |
| Відтворення/пауза | `Space` (у програвачі) |
| Закрити діалог | `Escape` |

### 7.5. Контрастність кольорів (WCAG AA)

Відповідно до WCAG 2.1 рівень AA та PRD §3.3, обидві теми (Light та Dark) повинні забезпечувати:

| Елемент | Мінімальний коефіцієнт контрасту |
|---------|--------------------------------|
| Звичайний текст (< 18pt) | **4.5:1** |
| Великий текст (≥ 18pt або ≥ 14pt жирний) | **3:1** |
| Іконки та функціональні графічні елементи | **3:1** |
| Фокусний індикатор | **3:1** відносно фону |

**Інструменти перевірки:**

- [Colour Contrast Analyser](https://www.tpgi.com/color-contrast-checker/) — безкоштовний desktop-інструмент для вимірювання контрасту
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/) — онлайн
- Inspect.exe — не вимірює контраст; лише UIA-дерево

**Практичні правила для `Light.xaml` / `Dark.xaml`:**

```xaml
<!-- Dark тема — приклад пари кольорів з контрастом > 7:1 -->
<Color x:Key="TextPrimary">#FFFFFF</Color>        <!-- Білий на темному -->
<Color x:Key="BackgroundPrimary">#1E1E1E</Color>  <!-- ~16:1 -->

<!-- Акцентний колір — перевірити що контраст ≥ 4.5:1 -->
<Color x:Key="AccentColor">#4FC3F7</Color>        <!-- Перевірити на BackgroundPrimary -->
```

> **Примітка**: Windows High Contrast Mode (Ease of Access) автоматично перевизначає кольори — WPF підтримує це нативно. Тема `"auto"` не замінює системний High Contrast Mode.

### 7.6. Фокус і Tab Order

```xaml
<!-- Логічний порядок фокусу в MainWindow -->
<DockPanel KeyboardNavigation.TabNavigation="Cycle">
    <!-- 1. Меню -->
    <Menu KeyboardNavigation.TabIndex="0" />
    <!-- 2. Панель інструментів -->
    <ToolBar KeyboardNavigation.TabIndex="1" />
    <!-- 3. Адресний рядок -->
    <TextBox KeyboardNavigation.TabIndex="2" />
    <!-- 4. Вкладки -->
    <TabControl KeyboardNavigation.TabIndex="3" />
    <!-- 5. Програвач -->
    <ContentControl KeyboardNavigation.TabIndex="4" />
</DockPanel>
```

### 7.7. Тестування доступності

Рекомендований порядок тестування:

1. **Inspect.exe** (з Windows SDK) — перевірка дерева UI Automation та наявності властивостей
2. **AccChecker** (Microsoft) — автоматична перевірка
3. **NVDA** (безкоштовний) — функціональне тестування з реальним скрінрідером
4. **JAWS** (комерційний) — перевірка із найпопулярнішим скрінрідером
5. **Windows Narrator** — вбудований скрінрідер Windows

---

## 8. Теги (TagLibSharp)

**TagLibSharp 2.3.0** (LGPL-2.1, .NET Standard 2.0, сумісна з .NET 9):

```csharp
// Запис ID3v2 тегів для MP3
using TagLib;

var file = TagLib.File.Create(filePath);
file.Tag.Title = trackTitle;
file.Tag.Performers = new[] { artist };
file.Tag.Album = album;
file.Tag.Track = (uint)trackNumber;
file.Tag.Genres = new[] { genre };
file.Save();
```

> **⚠️ Увага щодо ліцензії**: TagLibSharp розповсюджується під LGPL-2.1. При dynamic linking (стандартний NuGet) це прийнятно для комерційних та закритих проектів. При static linking (trimming) необхідно уважно перевірити умови LGPL.

> **Альтернатива**: `ATL.net` (MIT) — якщо ліцензія LGPL є проблемою, але менш популярна.

---

## 9. Логування (Serilog)

```csharp
// Infrastructure/Logging/LoggingConfiguration.cs
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        path: AppPaths.LogsFile,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
```

Serilog sinks, що використовуються:

| Sink | NuGet | Версія | Призначення |
|------|-------|--------|------------|
| `Serilog.Sinks.File` | `Serilog.Sinks.File` | 6.0.0 | Ротаційний файловий лог |

---

## 10. Каталог радіостанцій (Radio Browser API)

**[radio-browser.info](https://www.radio-browser.info/)** — відкрите API без API ключа.

### Доступні NuGet пакети

На момент досліджень виявлено декілька пакетів-обгорток, однак усі вони мають обмежений розвиток та малу кількість завантажень:
- `RadioBrowser` 0.7.0 (2023, .NET 6.0+) — GPL v3 ⚠️
- `RadioBrowserWrapper` 1.0.1 (2024, .NET Standard 2.0) — мало завантажень

**Рекомендація**: Використовувати **прямі HTTP виклики** через `HttpClient` — API просте, документоване та стабільне. Це також усуває залежність від бібліотек з невідомим статусом підтримки.

```csharp
// Приклад прямого запиту до Radio Browser API
public class RadioBrowserClient
{
    private readonly HttpClient _http;
    private string _baseUrl = "https://de2.api.radio-browser.info/json"; // fallback

    /// <summary>
    /// Резолвить актуальний сервер Radio Browser через DNS SRV.
    /// Radio Browser має декілька серверів; DNS all.api.radio-browser.info
    /// повертає список IP — вибираємо перший досяжний.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync("all.api.radio-browser.info");
            if (addresses.Length > 0)
            {
                // Отримати hostname для першої IP через зворотний DNS
                var hostEntry = await Dns.GetHostEntryAsync(addresses[0]);
                _baseUrl = $"https://{hostEntry.HostName}/json";
            }
        }
        catch
        {
            // Fallback на відомий сервер при помилці DNS
            _baseUrl = "https://de2.api.radio-browser.info/json";
        }
    }

    public async Task<IList<RadioStation>> SearchStationsAsync(
        string name = "", string tag = "", string country = "",
        string codec = "", int limit = 100)
    {
        var url = $"{_baseUrl}/stations/search?name={Uri.EscapeDataString(name)}" +
                  $"&limit={limit}&hidebroken=true&order=clickcount&reverse=true";
        // HttpClient → System.Text.Json десеріалізація
    }
}
```

> **Примітка**: `InitializeAsync()` викликається один раз при старті програми або при першому пошуку стацній. Результат кешується на сесію (не зберігається між запусками).

---

## 11. Портативний EXE — публікація

### 11.1. Команда публікації

```powershell
dotnet publish src/RadioReel.App/RadioReel.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:PublishTrimmed=true `
  /p:EnableCompressionInSingleFile=true
```

### 11.2. Очікуваний розмір

| Конфігурація | Розмір |
|-------------|-------|
| Self-contained, без trimming | ~155–200 МБ |
| З `PublishTrimmed` | ~90–130 МБ |
| З `EnableCompressionInSingleFile` | **~50–80 МБ** |

### 11.3. TrimmerRoots.xml

WPF використовує XAML reflection та `Type.GetType()`. Необхідно захистити від тримінгу:

```xml
<!-- TrimmerRoots.xml -->
<linker>
  <!-- WPF вимагає збереження усіх View, ViewModel та Converter типів -->
  <assembly fullname="RadioReel.App" preserve="all" />
  <!-- NAudio використовує рефлексію для Media Foundation кодеків -->
  <assembly fullname="NAudio.WinMM" preserve="all" />
  <assembly fullname="NAudio.Core" preserve="all" />
</linker>
```

### 11.4. App Manifest (UAC)

```xml
<!-- app.manifest — запуск без прав адміністратора -->
<requestedPrivileges>
  <requestedExecutionLevel level="asInvoker" uiAccess="false" />
</requestedPrivileges>
```

---

## 12. Паттерни та угоди

### 12.1. Асинхронність

- Вся мережева робота та IO — `async/await`
- Одночасний запис — `Task.Run()` з `CancellationToken`
- Оновлення UI з background thread — `Dispatcher.InvokeAsync()`
- **Заборонено**: `Thread.Sleep`, блокуючі виклики в UI thread

### 12.2. Обробка помилок

```
Exception типи:
  NetworkException    → автоматичне перепідключення (якщо налаштовано)
  DiskSpaceException  → попередження користувача + зупинка запису
  TagException        → логування, продовження без тегів
  ICYProtocolError    → оголошення скрінрідером + лог
```

### 12.3. Угоди іменування

| Елемент | Угода | Приклад |
|---------|-------|---------|
| Інтерфейси | `I` + PascalCase | `IStreamRecorder` |
| ViewModels | PascalCase + `ViewModel` | `StreamsViewModel` |
| Views | PascalCase + `View`/`Tab`/`Panel` | `StreamsTab` |
| Сервіси | PascalCase без суфіксу | `AudioPlayer` |
| Моделі | PascalCase | `StreamEntry` |
| Приватні поля | `_` + camelCase | `_isRecording` |

### 12.4. Збереження стану при аварійному завершенні

Відповідає PRD §5.2 "Збереження стану при аварійному завершенні".

**Стратегія:**

```csharp
// App.xaml.cs — реєстрація глобальних обробників
AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
{
    // 1. Логувати виняток через Serilog (Log.Fatal)
    // 2. Flush усіх відкритих FileStream у StreamRecorder (записати неповний трек)
    // 3. Маркувати неповні файли суфіксом _incomplete
    // 4. Зберегти активні URL записів у activeRecordingUrls профілю
    // 5. Явно викликати Log.CloseAndFlush()
}
```

**Що зберігається при краші:**

| Дані | Механізм |
|------|---------|
| Активні URL записів | `activeRecordingUrls` у `.rrprofile` через `ProfileStore.SaveEmergency()` |
| Частково записаний трек | `FileStream.Flush()` → файл залишається з `_incomplete` суфіксом |
| Лог помилки | Serilog `Log.Fatal(...)` + `Log.CloseAndFlush()` |

> **Примітка**: `ProfileStore.SaveEmergency()` повинен бути синхронним (не `async`) — виклик з `UnhandledException` відбувається поза нормальним async-контекстом.

---

## 13. Тестування

### 13.1. Стратегія

| Рівень | Інструмент | Охоплення |
|--------|-----------|-----------|
| Юніт-тести | `xUnit` + `FluentAssertions` | Core-логіка без UI: `IcyMetadataParser`, `TrackSplitter`, `FileNameTemplate`, `RecordingScheduler` |
| Інтеграційні тести | `xUnit` + mock-сервер (напр. `WireMock.Net`) | `IcyStreamClient` з емульованим ICY-сервером |
| UI / Accessibility | Ручне тестування + `Inspect.exe` | Перевірка дерева UI Automation, accessible names |
| End-to-end | Ручне тестування | Запис реального потоку, split треків, теги |

### 13.2. Юніт-тести (xUnit)

- Окремий проект `RadioReel.Tests` поряд з `RadioReel.App`
- Тестуються **тільки** класи з `Core/` (без залежності від WPF / UI)
- Моки через `Moq` або `NSubstitute` для `IcyStreamClient`, `SettingsStore`

```
RadioReel/
├── src/
│   └── RadioReel.App/
└── tests/
    └── RadioReel.Tests/
        ├── Audio/
        │   ├── IcyMetadataParserTests.cs   # Парсинг ICY блоків
        │   └── TrackSplitterTests.cs       # Логіка розділення треків
        ├── Metadata/
        │   └── FileNameTemplateTests.cs     # Шаблони %a, %t, санітизація
        └── Scheduling/
            └── RecordingSchedulerTests.cs   # Логіка розкладу, конфлікти
```

### 13.3. Тестування доступності

Порядок перевірки (описано в §7.7):

1. **Inspect.exe** — перевірити `AutomationProperties.Name` для **кожного** інтерактивного елемента
2. **Narrator** (вбудований) — швидка перевірка базового сценарію без встановлення додаткового ПЗ
3. **NVDA** — основний цільовий скрінрідер; перевірити live regions, оголошення запису
4. **JAWS** — перевірка сумісності

### 13.4. Тестування PublishTrimmed

Після кожного релізного білду виконати:

```powershell
# Запустити опублікований EXE і перевірити:
# 1. Старт без винятків
# 2. Відкриття вкладок
# 3. Підключення до потоку
# 4. Запис та split треку
# 5. Читання/запис тегів через TagLibSharp
./radioreel.exe
```

---

## 14. Зауваження та ризики

### 14.1. ICY metadata — ключова складність

Реалізація ICY протоколу — **найбільш нетривіальна技術завдання** проекту:

1. **ICY 200 OK замість HTTP**: `HttpClient` і `Socket` потребують спеціальної обробки нестандартного статус-рядка
2. **Interleaved metadata**: Метадані вбудовані в потік кожні `icy-metaint` байт. Потрібна точна побайтова обробка при одночасному буферизуванні аудіо для NAudio
3. **Кодування метаданих**: ICY метадані у latin-1, але можуть містити UTF-8. Потрібна перевірка BOM або евристика
4. **Reconnection**: При перепідключенні потрібно зберегти стан запису та коректно `flush` поточний файл

**Рекомендація**: Використовувати `TcpClient` або `Socket` напряму, а не `HttpClient` — для повного контролю над raw bytes.

### 14.2. TagLibSharp — стан проекту

Остання версія TagLibSharp (2.3.0) вийшла у **липні 2022** — бібліотека активно не розвивається. Однак вона стабільна та широко використовується. Для даного проекту достатньо поточної версії.

### 14.3. PublishTrimmed з WPF

Тримінг WPF застосунків може призводити до runtime помилок через XAML-рефлексію. **Необхідно проводити повне тестування після публікації** перед кожним релізом.

### 14.4. WASAPI на Windows

NAudio's `WasapiOut` у shared mode є стандартним підходом для відтворення. За замовчуванням Windows може мікшувати потоки від різних додатків.

### 14.5. Media Foundation кодеки

`MediaFoundationEncoder` для конвертації MP3/AAC залежить від кодеків Windows Media Foundation, які **вбудовані у Windows 10/11** та не потребують окремого встановлення.

---

## 15. Посилання на документацію

- [.NET 10 WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/) — офіційна документація WPF
- [WPF UI Automation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/ui-automation-of-a-wpf-custom-control) — кастомні AutomationPeer
- [UI Automation Overview](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-uiautomationoverview) — загальний огляд UI Automation
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) — офіційна документація MVVM Toolkit
- [NAudio GitHub](https://github.com/naudio/NAudio) — код та документація NAudio
- [NAudio Docs](https://github.com/naudio/NAudio/tree/master/Docs) — приклади використання
- [TagLibSharp](https://github.com/mono/taglib-sharp) — бібліотека тегів
- [Serilog](https://serilog.net/) — документація логування
- [Radio Browser API](https://api.radio-browser.info/) — документація Radio Browser
- [.NET Single File Publishing](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview) — публікація single file
- [SHOUTcast DSP Protocol](https://wiki.shoutcast.com/wiki/SHOUTcast_DNAS_Server_2_Technical_Specification) — технічна специфікація SHOUTcast DNAS
- [Icecast HTTP Protocol](https://icecast.org/docs/icecast-trunk/icecast_protocol/) — ICY-заголовки та формат метаданих Icecast
- [NVDA Screen Reader](https://www.nvaccess.org/) — безкоштовний скрінрідер для тестування
