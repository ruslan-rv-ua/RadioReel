# Фаза 4 — Екосистема та Полірування

## Мета

Discovery нових станцій, персоналізація досвіду (профілі), автоматизація постобробки, та фінальний polish (локалізація, теми).

**Залежить від:** Фази 1–3 (стабільна кодова база).

---

## Scope

### Що входить

- Radio Browser API — пошук і додавання станцій з каталогу
- Профілі користувача — іменовані набори даних
- Постобробка — конвертація форматів, зовнішні скрипти
- Локалізація — uk-UA та en-US
- Теми — двошаровий підхід (Fluent + Light/Dark), автовизначення
- Вкладка Лог — повноцінний перегляд журналу
- Налаштування — повний SettingsDialog
- Tray icon — згортання в трей, сповіщення
- Глобальні гарячі клавіші (працюють поза фокусом вікна)
- Командний рядок
- Single-instance (Named Pipe)

### Що НЕ входить

Все вже реалізовано у Фазах 1–3.

---

## Порядок реалізації

### 1. Локалізація + Теми (інфраструктура)

**Чому першими:** впливають на всі наявні та нові UI-елементи. Краще конвертувати хардкоджені рядки до ResourceDictionary один раз, ніж після додавання нових views.

#### Локалізація

**Файли:**
| Файл | Призначення |
|------|-------------|
| `Infrastructure/Localization/LocalizationManager.cs` | Визначення мови, завантаження словника |
| `Infrastructure/Localization/uk-UA.xaml` | Українські рядки |
| `Infrastructure/Localization/en-US.xaml` | Англійські рядки |

**Механізм:**
- ResourceDictionary з рядками: `<system:String x:Key="Btn_StartRecording">Почати запис</system:String>`
- У XAML: `Content="{DynamicResource Btn_StartRecording}"`
- `LocalizationManager.ApplyLanguage("uk-UA")` замінює словник у `MergedDictionaries`
- `AutomationProperties.Name` теж через `DynamicResource`

**Визначення мови (ARCHITECTURE §6.5):**
1. Читати `radioreel_settings.json` → `general.language`
2. Якщо не встановлено → `CultureInfo.CurrentUICulture.Name`
3. Якщо `uk*` → `uk-UA`, якщо `en*` → `en-US`, інше → `en-US`
4. Зберегти результат у settings

**Модифікуються:** всі XAML-файли (заміна хардкоджених рядків на `DynamicResource`).

#### Теми

**Файли:**
| Файл | Призначення |
|------|-------------|
| `Infrastructure/Themes/ThemeManager.cs` | Двошарове управління темою |
| `Themes/Light.xaml` | Шар 2 — світла тема (перевизначення поверх Fluent) |
| `Themes/Dark.xaml` | Шар 2 — темна тема |

**Двошаровий підхід (ARCHITECTURE §6.4):**

**Шар 1** — Fluent тема, підключається статично в `App.xaml`:
```xml
<ResourceDictionary Source="pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml" />
```

**Шар 2** — кастомні перевизначення, завантажуються динамічно:
```csharp
public static class ThemeManager
{
    private static ResourceDictionary? _themeLayer;

    public static void ApplyTheme(AppTheme theme)
    {
        var uri = theme == AppTheme.Light
            ? new Uri("pack://application:,,,/Themes/Light.xaml")
            : new Uri("pack://application:,,,/Themes/Dark.xaml");

        var newTheme = new ResourceDictionary { Source = uri };
        var merged = Application.Current.Resources.MergedDictionaries;

        // Замінити лише Шар 2, не торкаючись Шар 1 (Fluent.xaml)
        if (_themeLayer != null)
            merged.Remove(_themeLayer);

        merged.Add(newTheme);
        _themeLayer = newTheme;
    }

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
            return AppTheme.Dark;
        }
    }
}
```

**Ресурси Шару 2 (приклад `Dark.xaml`):**
```xml
<ResourceDictionary>
    <!-- Специфічні для RadioReel кольори -->
    <SolidColorBrush x:Key="StatusRecordingBrush" Color="#FF4444" />
    <SolidColorBrush x:Key="StatusConnectedBrush" Color="#44FF44" />
    <SolidColorBrush x:Key="StatusErrorBrush"     Color="#FF8800" />

    <!-- Відступи та стилі -->
    <Thickness x:Key="PanelMargin">8</Thickness>
    <Thickness x:Key="ButtonMargin">4,2</Thickness>
</ResourceDictionary>
```

**Контрастність (WCAG AA):**
- Текст на фоні: коефіцієнт ≥ 4.5:1
- Акценти та іконки: ≥ 3:1
- Перевірка через Colour Contrast Analyser

---

### 2. Профілі

**Файли:**
| Файл | Призначення |
|------|-------------|
| `Core/Storage/ProfileData.cs` | Модель per-profile даних (вже створено у Фазі 2, розширити) |
| `Core/Storage/ProfileStore.cs` | JSON серіалізація `.rrprofile` (вже створено у Фазі 2, розширити) |

**Операції:**
- Створити новий профіль (копіювання Default або порожній)
- Перемкнути профіль → перезавантажити streams, wishlist, ignorelist, schedule, savedTracks
- Копіювати профіль
- Видалити профіль (крім Default)
- Імпорт/експорт `.rrprofile` (JSON)
- Швидке перемикання: Ctrl+Alt+↑/↓

**Особливість:** при перемиканні профілю glобальні налаштування (мова, тема, пристрій, хоткеї) **не змінюються** — лише per-profile дані.

**UI:**
- Меню: `Файл → Профіль → {список}`
- SettingsDialog → секція «Профілі»: ListView + кнопки Створити/Копіювати/Видалити
- StatusBar: read-only TextBlock з назвою поточного профілю
- NVDA оголошує: "Профіль змінено: {назва}" (Assertive)

---

### 3. Radio Browser API

**Файли:**
| Файл | Призначення |
|------|-------------|
| `Core/Network/RadioBrowserClient.cs` | HTTP клієнт до Radio Browser API |
| `UI/ViewModels/StreamBrowserViewModel.cs` | Логіка пошуку та додавання |
| `UI/Views/Dialogs/StreamBrowserDialog.xaml / .cs` | Діалог пошуку станцій |

**Реалізація (ARCHITECTURE §10):**
- DNS resolve: `Dns.GetHostAddressesAsync("all.api.radio-browser.info")` → зворотний DNS → базовий URL
- Fallback: `https://de2.api.radio-browser.info/json`
- Endpoint: `GET /stations/search?name=...&tag=...&codec=...&bitrateMin=...&limit=100&hidebroken=true`
- Десеріалізація: `System.Text.Json` → `List<RadioStation>`
- Кешування: одноразова ініціалізація URL на сесію

**StreamBrowserDialog:**

**Tab Order:**
1. TextBox «Назва» — `AutomationProperties.Name="Назва станції"`
2. TextBox «Жанр/тег» — `AutomationProperties.Name="Жанр або тег"`
3. ComboBox «Формат» — `AutomationProperties.Name="Формат"` (MP3/AAC/Всі)
4. TextBox «Мін. бітрейт» — `AutomationProperties.Name="Мінімальний бітрейт"`
5. Button «Шукати» — `AutomationProperties.Name="Шукати станції"`
6. ListView «Результати» — `AutomationProperties.Name="Результати пошуку"`
7. Button «Додати обрані» — `AutomationProperties.Name="Додати обрані станції до списку"`
8. Button «Закрити» — `IsCancel=True`

**Колонки ListView:**

| Колонка | Приклад |
|---------|---------|
| Назва | Radio Paradise |
| Країна | US |
| Жанр | Rock, Eclectic |
| Бітрейт | 320 kbps |
| Формат | MP3 |
| Метадані | Так / Ні |

---

### 4. Постобробка

**Файли:**
| Файл | Призначення |
|------|-------------|
| `Core/Postprocessing/PostprocessingQueue.cs` | Черга файлів для обробки |
| `Core/Postprocessing/FormatConverter.cs` | Конвертація через Windows Media Foundation |
| `Core/Postprocessing/ScriptRunner.cs` | Запуск зовнішніх скриптів |

**Конвертація:**
- `MediaFoundationEncoder.EncodeToMp3(reader, outputPath)` або `EncodeToAac`
- Прогрес через `ProgressBar` з `AutomationProperties.Name="Прогрес конвертації, {відсотки} відсотків"`

**Зовнішні скрипти:**
```csharp
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = scriptPath,
        Arguments = scriptArgs.Replace("%file%", filePath),
        UseShellExecute = false,
        CreateNoWindow = true
    }
};
process.Start();
if (!process.WaitForExit(timeoutMs))
{
    process.Kill();
    Log.Warning("Script timeout: {Script}", scriptPath);
}
```

**Черга:**
- `ConcurrentQueue<PostprocessingTask>` — FIFO
- Обробка в background Task
- SettingsDialog → секція «Постобробка» → ListView «Черга» (Файл, Статус, Дія)

---

### 5. SettingsDialog (повний)

**Файли:**
| Файл | Призначення |
|------|-------------|
| `UI/ViewModels/SettingsViewModel.cs` | Логіка налаштувань |
| `UI/Views/Dialogs/SettingsDialog.xaml / .cs` | Повний діалог |
| `UI/Views/Dialogs/StreamSettingsDialog.xaml / .cs` | Per-stream налаштування |

**Секції (внутрішній TabControl або ListView-навігація):**

1. **Загальне:**
   - ComboBox «Мова» — uk-UA / en-US
   - ComboBox «Тема» — Автоматична / Світла / Темна
   - CheckBox «Згортання в трей»
   - CheckBox «Показувати сповіщення»
   - CheckBox «Назва треку у заголовку вікна»
   - CheckBox «Автозапуск з Windows» (з попередженням для USB)
   - TextBox «Поріг низького місця на диску (ГБ)»

2. **Запис:**
   - ComboBox «Дія при подвійному кліку» — Запис / Відтворення
   - CheckBox «Зберігати потоковий файл»
   - CheckBox «Видаляти потоковий файл після зупинки»
   - CheckBox «Зберігати тільки повні пісні»
   - TextBox «Папка збереження за замовчуванням» + Button «Огляд…»
   - TextBox «Шаблон імені файлу»
   - TextBox «Мін. тривалість треку (сек)»

3. **Гарячі клавіші:**
   - ListView: Дія | Поточна комбінація | [Змінити]
   - Кожен рядок: натиснути «Змінити» → фокус на поле запису → натиснути нову комбінацію → Enter

4. **Профілі:**
   - ListView: список профілів
   - Кнопки: Створити / Копіювати / Видалити / Імпорт / Експорт

5. **Постобробка:**
   - CheckBox «Увімкнути конвертацію»
   - ComboBox «Цільовий формат» — MP3 / AAC / WAV
   - CheckBox «Увімкнути зовнішній скрипт»
   - TextBox «Шлях до скрипту» + Button «Огляд…»
   - TextBox «Аргументи» — ToolTip: "%file% — шлях до записаного файлу"
   - TextBox «Таймаут (сек)» — за замовчуванням 120
   - ListView «Черга постобробки» — Файл, Статус, Дія (Видалити з черги)

6. **Мережа:**
   - TextBox «Ліміт пропускної здатності (кБ/с)» — 0 = без ліміту
   - TextBox «HTTP Proxy» — формат: `http://host:port`

Всі поля мають `AutomationProperties.Name` або `AutomationProperties.LabeledBy`.

---

### 6. Вкладка «Лог» (Ctrl+5)

**Файли:**
| Файл | Призначення |
|------|-------------|
| `UI/ViewModels/LogViewModel.cs` | Фільтрація логу, буфер подій |
| `UI/Views/Tabs/LogTab.xaml / .cs` | Вкладка журналу |

**Tab Order:**
1. ComboBox «Рівень» — `AutomationProperties.Name="Фільтр за рівнем"` (Всі / Інформація / Попередження / Помилки)
2. TextBox «Фільтр» — `AutomationProperties.Name="Фільтр логу"`
3. ListView «Журнал» — `AutomationProperties.Name="Журнал подій"`
4. Button «Очистити» — `AutomationProperties.Name="Очистити журнал"`
5. Button «Відкрити файл логу» — `AutomationProperties.Name="Відкрити файл логу"`

**Реалізація:**
- Serilog custom sink → `ObservableCollection<LogEntry>` для UI
- Фільтрація в реальному часі через `ICollectionView`
- Максимум 10 000 записів у пам'яті (FIFO)

---

### 7. Tray Icon, Глобальні хоткеї, Single Instance, Командний рядок

**Tray Icon:**
- `System.Windows.Forms.NotifyIcon` (через interop) або кастомний WPF підхід
- Контекстне меню: Показати / Згорнути, Почати запис, Зупинити, Вихід
- Balloon notification при зміні треку (якщо `showTrayNotifications=true`)

**Глобальні хоткеї:**
- `RegisterHotKey` WinAPI через P/Invoke
- Працюють навіть коли вікно не у фокусі
- За замовчуванням (ARCHITECTURE §6.2):
  - Ctrl+Shift+R — старт/зупинка запису
  - Ctrl+Shift+P — відтворення/пауза
  - Ctrl+Shift+→/← — наступний/попередній потік
  - Ctrl+Shift+↑/↓ — гучність +/−
  - Ctrl+Shift+H — показати/приховати вікно
  - Ctrl+Alt+↑/↓ — перемикання профілю

**Single Instance:**
- `Mutex` для перевірки чи вже запущено
- `NamedPipeServerStream` для прийому аргументів від другого екземпляра
- Другий екземпляр передає CLI-аргументи і завершується

**Командний рядок (PRD §4.11):**
- `-datadir`, `-tempdir`, `-profile`, `-minimize`
- `-r [URL]`, `-p [URL]`, `-sr`, `-sp`
- `-wishadd`, `-wishremove`

---

## Оголошення скрінрідера (нові для Фази 4)

| Подія | LiveSetting | NVDA оголошує |
|-------|-------------|---------------|
| Профіль змінено | Assertive | "Профіль змінено: {назва}" |
| Мова змінена | Assertive | "Мову змінено: Українська" |
| Тема змінена | Polite | "Тема: Темна" |
| Конвертація завершена | Polite | "Конвертація завершена: {файл}" |
| Tray notification | — | Balloon popup з назвою треку |

---

## Keyboard Shortcuts (нові для Фази 4)

| Дія | Клавіша |
|-----|---------|
| Вкладка Лог | Ctrl+5 |
| Налаштування | Ctrl+, |
| Знайти станції | Ctrl+F |
| Наступний профіль | Ctrl+Alt+↑ |
| Попередній профіль | Ctrl+Alt+↓ |
| Глобальні хоткеї | Ctrl+Shift+R/P/→/←/↑/↓/H |

---

## Критерії завершення Фази 4

- [ ] Шукати станції через Radio Browser API з фільтрами
- [ ] Додавати знайдені станції до списку потоків
- [ ] Створювати, перемикати, видаляти, імпортувати/експортувати профілі
- [ ] Ctrl+Alt+↑/↓ перемикає профіль з оголошенням NVDA
- [ ] Конвертувати записані файли у інший формат
- [ ] Запускати зовнішній скрипт після запису
- [ ] Черга постобробки відображається у SettingsDialog
- [ ] Інтерфейс доступний uk-UA та en-US
- [ ] Зміна мови застосовується без перезапуску
- [ ] Теми Light/Dark/Auto працюють з Fluent як Шар 1
- [ ] WCAG AA контрастність ≥ 4.5:1 у обох темах
- [ ] Вкладка Лог з фільтрацією працює
- [ ] SettingsDialog — всі секції повні
- [ ] Tray icon з контекстним меню та сповіщеннями
- [ ] Глобальні хоткеї працюють поза фокусом вікна
- [ ] Single-instance через Named Pipe
- [ ] Аргументи командного рядка працюють
- [ ] `dotnet publish` → portable EXE ≤ 80 МБ
