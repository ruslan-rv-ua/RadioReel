# Фаза 1 — MVP: Записувач потоків

## Мета

Функціональний продукт, яким незрячий користувач може реально записувати інтернет-радіо через скрінрідер (NVDA/JAWS).

**De-ризикує** IcyStreamClient — найскладніший технічний компонент проекту.

---

## Scope

### Що входить

- ICY/SHOUTcast клієнт (TcpClient) з парсингом метаданих
- Запис одного потоку в файл
- Розбивка потоку на окремі треки за ICY-метаданими
- Головне вікно з мінімальним UI
- Повна accessibility (AutomationProperties, keyboard nav, live regions)
- Портативний single-file EXE
- Базова конфігурація (JSON)
- Структуроване логування (Serilog)

### Що НЕ входить

- Мультизапис (кілька потоків одночасно)
- Wishlist / Ignorelist
- Scheduler
- Програвач (WASAPI)
- Браузер станцій (Radio Browser API)
- Теги (TagLibSharp)
- Профілі
- Постобробка
- Локалізація (тільки одна мова — `uk-UA`, рядки хардкоджені або в ResourceDictionary)
- Теми (тільки Dark + Fluent як базова)

---

## Файли для створення

### Інфраструктура (корінь репозиторію)

| Файл | Призначення | Спосіб створення |
|------|-------------|------------------|
| `.gitignore` | Ігнорування `bin/`, `obj/`, `.vs/`, `*.user` | `dotnet new gitignore` |
| `.gitattributes` | Нормалізація line endings, бінарні файли | `dotnet new gitattributes` |
| `.editorconfig` | C# стиль: відступи, naming conventions, severity | `dotnet new editorconfig` → налаштувати |
| `global.json` | Пінить SDK до 10.0.x (latestPatch) | `dotnet new globaljson --sdk-version 10.0.104 --roll-forward latestPatch` |

### Проект

| Файл | Призначення |
|------|-------------|
| `RadioReel.sln` | Solution |
| `src/RadioReel.App/RadioReel.App.csproj` | .NET 10, WPF, self-contained, NuGet |
| `src/RadioReel.App/app.manifest` | UAC asInvoker, DPI |
| `src/RadioReel.App/TrimmerRoots.xml` | Захист від тримінгу |
| `src/RadioReel.App/App.xaml` | Ресурси, Fluent тема (Шар 1) |
| `src/RadioReel.App/App.xaml.cs` | DI, Serilog, crash handler |

### Core

| Файл | Призначення |
|------|-------------|
| `Core/Audio/IcyStreamClient.cs` | ICY/SHOUTcast TCP клієнт |
| `Core/Audio/IcyMetadataParser.cs` | Парсинг ICY metadata блоків |
| `Core/Audio/StreamRecorder.cs` | Запис raw-байтів у файл |
| `Core/Audio/TrackSplitter.cs` | Розділення потоку на треки за метаданими |
| `Core/Metadata/IcyMetadata.cs` | Модель ICY metadata |
| `Core/Metadata/FileNameTemplate.cs` | Шаблонізатор імен файлів (%a, %t, %s тощо) |
| `Core/Storage/AppPaths.cs` | Шляхи відносно EXE |
| `Core/Storage/AppSettings.cs` | Модель глобальних налаштувань (мінімальна) |
| `Core/Storage/SettingsStore.cs` | JSON серіалізація налаштувань |
| `Core/Models/StreamEntry.cs` | Модель радіостанції |
| `Core/Models/RecordingSession.cs` | Сесія запису одного потоку |
| `Core/Network/PlaylistParser.cs` | Парсинг M3U/PLS/ASX → URL потоку |

### UI

| Файл | Призначення |
|------|-------------|
| `UI/ViewModels/MainViewModel.cs` | Головна ViewModel |
| `UI/ViewModels/StreamsViewModel.cs` | Логіка списку потоків та запису |
| `UI/Views/MainWindow.xaml` | Головне вікно (Menu + TabControl + StatusBar) |
| `UI/Views/MainWindow.xaml.cs` | Code-behind |
| `UI/Views/Tabs/StreamsTab.xaml` | Вкладка «Потоки» (мінімальна) |
| `UI/Views/Tabs/StreamsTab.xaml.cs` | Code-behind |
| `UI/Views/Panels/StatusBar.xaml` | Статусний рядок |
| `UI/Views/Panels/StatusBar.xaml.cs` | Code-behind |
| `UI/Views/Dialogs/AddStreamDialog.xaml` | Діалог додавання потоку |
| `UI/Views/Dialogs/AddStreamDialog.xaml.cs` | Code-behind |

### Infrastructure

| Файл | Призначення |
|------|-------------|
| `Infrastructure/Logging/LoggingConfiguration.cs` | Serilog → файл з ротацією |
| `Infrastructure/Accessibility/AccessibilityHelper.cs` | Live region helper |

### Themes

| Файл | Призначення |
|------|-------------|
| `Themes/Dark.xaml` | Шар 2 — темна тема (мінімальна) |

**Разом:** ~26 файлів.

---

## UI Фази 1

### MainWindow

```
┌──────────────────────────────────────────────┐
│  Menu bar                                    │
│  Файл | Потік                                │
├──────────────────────────────────────────────┤
│  TabControl (одна вкладка)                   │
│  [Потоки]                                    │
│ ┌──────────────────────────────────────────┐ │
│ │  [Почати запис F5]  [Зупинити F6]        │ │
│ │                                          │ │
│ │  ListView «Список потоків»               │ │
│ │  ┌────────┬────────┬──────────────────┐  │ │
│ │  │ Назва  │ Статус │ Поточний трек    │  │ │
│ │  ├────────┼────────┼──────────────────┤  │ │
│ │  │ ...    │ ...    │ ...              │  │ │
│ │  └────────┴────────┴──────────────────┘  │ │
│ └──────────────────────────────────────────┘ │
├──────────────────────────────────────────────┤
│  StatusBar                                   │
│  Стан: Відключено | 0 записів | 45 ГБ вільно │
└──────────────────────────────────────────────┘
```

### Menu

```
Файл (Alt+F)
  ├─ Налаштування…          (Ctrl+,) — у Фазі 1: мінімальний діалог або відсутній
  └─ Вихід                   (Alt+F4)

Потік (Alt+P)
  ├─ Додати потік…           (Ctrl+N)   → AddStreamDialog
  ├─ Почати запис             (F5)
  └─ Зупинити запис           (F6)
```

### StreamsTab

**Tab Order:**
1. Button «Почати запис» — `AutomationProperties.Name="Почати запис"`, `ToolTip="Почати запис (F5)"`
2. Button «Зупинити запис» — `AutomationProperties.Name="Зупинити запис"`, `ToolTip="Зупинити запис (F6)"`
3. ListView «Список потоків» — `AutomationProperties.Name="Список потоків"`

**Колонки ListView:**

| Колонка | AutomationProperties.Name |
|---------|--------------------------|
| Назва | "Назва станції" |
| Статус | "Статус" (LiveSetting="Polite") |
| Поточний трек | "Поточний трек" (LiveSetting="Polite") |

**Контекстне меню (Shift+F10):**
- Почати запис (F5)
- Зупинити запис (F6)
- Видалити потік

### AddStreamDialog

**Tab Order:**
1. TextBox «URL потоку» — обов'язкове, `AutomationProperties.Name="URL потоку"`
2. TextBox «Назва станції» — `AutomationProperties.Name="Назва станції"` (опціонально; якщо порожнє — береться з ICY headers)
3. Button «Додати» — `IsDefault=True`
4. Button «Скасувати» — `IsCancel=True`

### StatusBar

| Секція | AutomationProperties.Name | LiveSetting |
|--------|--------------------------|-------------|
| Стан | "Стан з'єднання" | Polite |
| Записи | "Активні записи" | Polite |
| Місце на диску | "Вільне місце на диску" | — |

### Оголошення скрінрідера (Фаза 1)

| Подія | LiveSetting | NVDA оголошує |
|-------|-------------|---------------|
| Початок запису | Assertive | "Запис розпочато: {назва}" |
| Зупинка запису | Assertive | "Запис зупинено: {назва}" |
| Зміна треку | Polite | "Поточний трек: {артист} — {назва}" |
| Помилка з'єднання | Assertive | "Помилка: {назва} — {деталі}" |
| Перепідключення | Polite | "Перепідключення: {назва}, спроба {n}" |

---

## Keyboard Shortcuts (Фаза 1)

| Дія | Клавіша |
|-----|---------|
| Почати запис | F5 |
| Зупинити запис | F6 |
| Додати потік | Ctrl+N |
| Контекстне меню | Shift+F10 |
| Закрити діалог | Escape |
| Навігація в списку | ↑ ↓ Home End |
| Меню | Alt |

---

## Технічні деталі

### IcyStreamClient — реалізація

Найскладніший компонент. Послідовність:

1. Підключення через `TcpClient` до хосту
2. Надсилання HTTP-подібного запиту:
   ```
   GET /stream HTTP/1.0\r\n
   Host: radio.example.com\r\n
   Icy-MetaData: 1\r\n
   User-Agent: RadioReel/1.0\r\n
   \r\n
   ```
3. Читання відповіді `ICY 200 OK` (або `HTTP/1.1 200 OK`)
4. Парсинг заголовків: `icy-metaint`, `icy-name`, `content-type`
5. Потоковий режим: читання `icy-metaint` байтів аудіо → 1 байт розміру метаданих → метадані → аудіо → ...
6. Публікація події `MetadataChanged` при зміні StreamTitle
7. Передача raw аудіо-байтів у `StreamRecorder`

**Обробка помилок:**
- Обрив з'єднання → подія `Disconnected` → автоперепідключення (якщо налаштовано)
- Некоректна відповідь → подія `Error`
- Timeout → retry з backoff

**Кодування метаданих:**
- Спроба UTF-8, при помилці — latin-1 (ISO-8859-1)

### StreamRecorder

- Отримує raw bytes від IcyStreamClient (без метаданих)
- Записує у `FileStream` (прямий запис MP3/AAC — без перекодування)
- При зміні метаданих → `TrackSplitter.OnTrackChanged()`

### TrackSplitter

- При зміні StreamTitle: закрити поточний FileStream, відкрити новий
- Іменування файлу через `FileNameTemplate`
- Перший неповний трек: суфікс `_incomplete` (за налаштуванням)
- Мінімальна тривалість треку (фільтр реклами): параметр `skipShortTracksMs`

### FileNameTemplate

- Заміна змінних: `%a` (артист), `%t` (назва), `%s` (станція), `%d` (дата), `%time` (час)
- Санітизація: `\ / : * ? " < > |` → `_`
- Колізії: `_2`, `_3` тощо
- Ієрархія папок через `\` у шаблоні

### AppSettings (мінімальна для Фази 1)

```json
{
  "general": {
    "lowDiskSpaceWarningGb": 1.0
  },
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

### PlaylistParser

Перед підключенням IcyStreamClient перевіряє URL:
- Якщо `.m3u` / `.m3u8` → парсить текстовий файл, повертає перший URL
- Якщо `.pls` → парсить INI-формат, повертає `File1=...`
- Якщо `.asx` → парсить XML, повертає `<ref href="..."/>`
- Інакше → URL вже є прямим посиланням на потік

---

## Порядок реалізації (всередині Фази 1)

1. **Блок 1:** `.gitignore`, `.gitattributes`, `.editorconfig`, `global.json`, `RadioReel.sln`, `.csproj`, `App.xaml/cs`, `app.manifest`, `TrimmerRoots.xml` → `dotnet build`
2. **Блок 2:** `AppPaths`, `LoggingConfiguration`, `AccessibilityHelper`, `Dark.xaml` → `dotnet build`
3. **Блок 3:** `MainWindow` (каркас: Menu, TabControl з 1 вкладкою, StatusBar) → `dotnet build`
4. **Блок 4:** `StreamEntry`, `AppSettings`, `SettingsStore` (моделі + серіалізація) → `dotnet build`
5. **Блок 5:** `IcyMetadata`, `IcyMetadataParser`, `PlaylistParser` (парсинг, без мережі) → `dotnet build`
6. **Блок 6:** `IcyStreamClient` (TCP клієнт, підключення, потокове читання) → `dotnet build`
7. **Блок 7:** `FileNameTemplate`, `StreamRecorder`, `TrackSplitter` (запис файлів) → `dotnet build`
8. **Блок 8:** `RecordingSession` (координація IcyStreamClient + StreamRecorder + TrackSplitter) → `dotnet build`
9. **Блок 9:** `StreamsViewModel`, `StreamsTab` (UI: ListView, кнопки, binding до RecordingSession) → `dotnet build`
10. **Блок 10:** `AddStreamDialog` (додавання потоків) → `dotnet build`
11. **Блок 11:** Інтеграція, тестування з реальним потоком, виправлення

---

## Критерії завершення Фази 1

- [ ] Додати потік за URL (через діалог або меню)
- [ ] Підключитися до ICY/SHOUTcast потоку
- [ ] Записати потік на диск з розбивкою на треки за метаданими
- [ ] Иіменування файлів за шаблоном (%a, %t, %s)
- [ ] Автоматичне перепідключення при обриві
- [ ] Коректне завершення запису (Flush файлу)
- [ ] NVDA оголошує: початок/зупинку запису, зміну треку, помилки
- [ ] Повна клавіатурна навігація (F5, F6, Ctrl+N, Tab, ↑↓)
- [ ] Portable single-file EXE (`dotnet publish`)
- [ ] Логування у файл з ротацією
