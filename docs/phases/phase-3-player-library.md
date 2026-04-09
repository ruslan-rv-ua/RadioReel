# Фаза 3 — Програвач та Бібліотека

## Мета

Повний post-recording experience: прослуховування записаних треків, управління бібліотекою, редагування тегів — все в межах одного додатку.

**Залежить від:** Фаза 1 (збережені файли на диску).
**Незалежна від:** Фаза 2 (можна реалізовувати паралельно або до Фази 2).

---

## Scope

### Що входить

- Вбудований WASAPI-програвач (NAudio)
  - Відтворення потоків у реальному часі
  - Відтворення записаних файлів (MP3/AAC)
  - Регулювання гучності
  - Пауза / зупинка / перемотка
  - Вибір пристрою виведення звуку
- Бібліотека збережених пісень
  - Список записаних файлів із сортуванням, фільтрацією, пошуком
  - Мітки: wishlist, incomplete, finalized
  - Контекстне меню з повними операціями
  - Імпорт зовнішніх файлів / папок
- Редактор тегів (TagLibSharp)
  - ID3v2 теги для MP3
  - Теги для AAC
  - Поля: артист, назва, альбом, жанр, номер треку
  - Автокорекція регістру

### Що НЕ входить

- Radio Browser API
- Профілі
- Постобробка
- Локалізація, теми

---

## Нові файли

### Core

| Файл | Призначення |
|------|-------------|
| `Core/Audio/AudioPlayer.cs` | WASAPI відтворення через NAudio |
| `Core/Metadata/TagEditor.cs` | Читання/запис ID3/AAC тегів (TagLibSharp) |
| `Core/Models/SavedTrack.cs` | Модель збереженого треку (шлях, теги, мітки, дата) |

### UI

| Файл | Призначення |
|------|-------------|
| `UI/ViewModels/PlayerViewModel.cs` | Стан програвача, команди, binding |
| `UI/ViewModels/SavedSongsViewModel.cs` | Фільтрація, сортування, операції |
| `UI/Views/Panels/PlayerPanel.xaml / .cs` | Панель програвача (завжди видима) |
| `UI/Views/Tabs/SavedSongsTab.xaml / .cs` | Вкладка збережених пісень |
| `UI/Views/Dialogs/TagEditorDialog.xaml / .cs` | Діалог редагування тегів |
| `UI/Controls/VolumeSlider.xaml / .cs` | Слайдер гучності з accessibility |

**Модифікуються:**

| Файл | Зміна |
|------|-------|
| `MainWindow.xaml` | Додати PlayerPanel, вкладку «Пісні» (Ctrl+2), Ctrl+5 для Лог |
| `MainViewModel.cs` | PlayerViewModel injection, media commands |
| `StreamsViewModel.cs` | Можливість програвати потік (передача URL до PlayerViewModel) |
| `App.xaml.cs` | Реєстрація AudioPlayer, PlayerViewModel, SavedSongsViewModel у DI |

---

## Порядок реалізації

### 1. AudioPlayer (WASAPI)

**Архітектура:**
```
URL / файл
    ↓
AudioPlayer
    ├─ StreamPlayback: IcyStreamClient → BufferedWaveProvider → SampleChannel → WasapiOut
    └─ FilePlayback:   AudioFileReader → SampleChannel → WasapiOut
```

**Компоненти NAudio:**

| Режим | Pipeline |
|-------|----------|
| Потік (live) | `IcyStreamClient` → `BufferedWaveProvider` (decode) → `SampleChannel` (volume) → `WasapiOut` |
| Файл | `AudioFileReader` → `SampleChannel` (volume) → `WasapiOut` |

**API:**
```csharp
public class AudioPlayer : IDisposable
{
    // Стан
    public PlaybackState State { get; }          // Playing, Paused, Stopped
    public float Volume { get; set; }             // 0.0 - 1.0
    public TimeSpan Position { get; set; }        // поточна позиція (файли)
    public TimeSpan Duration { get; }             // тривалість (файли)
    public string CurrentDevice { get; set; }     // ID пристрою

    // Команди
    public Task PlayStreamAsync(string url, CancellationToken ct);
    public Task PlayFileAsync(string filePath);
    public void Pause();
    public void Resume();
    public void Stop();

    // Події
    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler<string>? TrackChanged;  // live metadata
}
```

**WASAPI shared mode:** NAudio `WasapiOut(MMDevice, AudioClientShareMode.Shared, ...)` — сумісна з іншими аудіо-додатками та скрінрідером (NVDA використовує WASAPI shared mode для мовлення).

**Вибір пристрою:**
```csharp
var enumerator = new MMDeviceEnumerator();
var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
```

### 2. PlayerPanel

**Структура:**
```
┌──────────────────────────────────────────────────────┐
│  [▶/⏸] [⏹]  Зараз грає: Pink Floyd — Comfortably   │
│              Numb    02:15 / 06:22                    │
│  [───────────●─────────] [🔊──●──────]  [Пристрій ▾] │
└──────────────────────────────────────────────────────┘
```

**Tab Order:**

| # | Елемент | AutomationProperties.Name | Деталі |
|---|---------|--------------------------|--------|
| 1 | Button ▶/⏸ | "Відтворити" або "Пауза" (динамічно) | Space — тогл. ToolTip: "Відтворення / пауза (Space)" |
| 2 | Button ⏹ | "Зупинити відтворення" | |
| 3 | TextBlock «Трек» | LiveSetting="Polite", Name="Зараз грає: {трек}" | Не фокусується |
| 4 | TextBlock «Час» | Name="Позиція: {час}" | Не фокусується |
| 5 | Slider «Позиція» | Name="Позиція відтворення, {поточна} з {загальна}" | Тільки для файлів; приховано для потоків |
| 6 | Slider «Гучність» | Name="Гучність, {відсотки} відсотків" | `VolumeSlider` control |
| 7 | ComboBox «Пристрій» | Name="Пристрій виведення звуку" | Список аудіопристроїв |

**VolumeSlider (custom control):**
- Стандартний WPF `Slider` з `AutomationProperties.Name` що оновлюється через binding
- `Minimum=0`, `Maximum=100`, `SmallChange=1`, `LargeChange=10`
- ToolTip: "{Volume}%"
- NVDA оголошує значення при зміні (нативно — `SliderAutomationPeer`)

### 3. SavedSongsTab

**Структура:**
```
┌──────────────────────────────────────────┐
│  [Імпорт файлів…]                        │
│  [Пошук: ___________] [Сортування: ▾]    │
│                                          │
│  ListView: Збережені пісні               │
│  ┌────────┬──────────┬────────┬─────────┐│
│  │ Артист │ Назва    │ Станція│ Дата    ││
│  ├────────┼──────────┼────────┼─────────┤│
│  │ ...    │ ...      │ ...    │ ...     ││
│  └────────┴──────────┴────────┴─────────┘│
└──────────────────────────────────────────┘
```

**Tab Order:**
1. Button «Імпорт файлів…» — `AutomationProperties.Name="Імпорт аудіофайлів"` → OpenFileDialog
2. TextBox «Пошук» — `AutomationProperties.Name="Пошук збережених пісень"`
3. ComboBox «Сортування» — `AutomationProperties.Name="Сортування"` (За датою / За назвою / За артистом / За станцією)
4. ListView — `AutomationProperties.Name="Список збережених пісень"`

**Колонки ListView:**

| Колонка | AutomationProperties.Name | Приклад |
|---------|--------------------------|---------|
| Артист | "Артист" | Pink Floyd |
| Назва | "Назва" | Comfortably Numb |
| Станція | "Станція" | Radio Paradise |
| Тривалість | "Тривалість" | 6:22 |
| Дата запису | "Дата" | 2026-04-09 |
| Мітки | "Мітки" | ☆ wishlist |

**Контекстне меню (Shift+F10):**
- Відтворити → передає файл до PlayerViewModel
- Редагувати теги… → відкриває TagEditorDialog
- Додати до Wishlist / Видалити з Wishlist
- Додати до Ignorelist
- Копіювати файл
- Перейменувати
- Видалити (в кошик)
- Показати у Провіднику (`Process.Start("explorer.exe", "/select,\"path\"")`)
- Властивості

**SavedTrack — модель:**
```csharp
public class SavedTrack
{
    public string FilePath { get; set; }
    public string Artist { get; set; }
    public string Title { get; set; }
    public string Album { get; set; }
    public string StationName { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime RecordedAt { get; set; }
    public bool IsFromWishlist { get; set; }
    public bool IsIncomplete { get; set; }
}
```

### 4. TagEditorDialog

**Tab Order:**
1. TextBox «Артист» — `AutomationProperties.Name="Артист"`
2. TextBox «Назва» — `AutomationProperties.Name="Назва треку"`
3. TextBox «Альбом» — `AutomationProperties.Name="Альбом"`
4. TextBox «Жанр» — `AutomationProperties.Name="Жанр"`
5. TextBox «Номер треку» — `AutomationProperties.Name="Номер треку"`
6. CheckBox «Автокорекція регістру» — `AutomationProperties.Name="Автоматична корекція регістру (Artist - Title)"`
7. TextBlock «Файл» — шлях до файлу (read-only)
8. Button «Зберегти» — `IsDefault=True`
9. Button «Скасувати» — `IsCancel=True`

**TagEditor (Core):**
```csharp
public class TagEditor
{
    public TagData ReadTags(string filePath);
    public void WriteTags(string filePath, TagData tags);
    public void AutoCorrectCase(TagData tags);  // "pink floyd" → "Pink Floyd"
}
```

---

## Оголошення скрінрідера (нові для Фази 3)

| Подія | LiveSetting | NVDA оголошує |
|-------|-------------|---------------|
| Початок відтворення | Polite | "Відтворення: {артист} — {назва}" |
| Пауза | Polite | "Пауза" |
| Зупинка відтворення | Polite | "Відтворення зупинено" |
| Зміна треку (live) | Polite | "Зараз грає: {артист} — {назва}" |
| Теги збережено | Polite | "Теги збережено" |

---

## Keyboard Shortcuts (нові для Фази 3)

| Дія | Клавіша |
|-----|---------|
| Відтворення/пауза | Space (у PlayerPanel або коли фокус на треку) |
| Вкладка Збережені пісні | Ctrl+2 |

---

## Критерії завершення Фази 3

- [ ] Програвати ICY-потік у реальному часі з регулюванням гучності
- [ ] Програвати записані MP3/AAC файли з перемоткою
- [ ] Вибирати пристрій виведення звуку
- [ ] NVDA оголошує: стан програвача, назву треку, рівень гучності
- [ ] Переглядати список збережених пісень із пошуком і сортуванням
- [ ] Імпортувати зовнішні аудіофайли/папки
- [ ] Редагувати ID3/AAC теги через TagEditorDialog
- [ ] Контекстне меню з усіма операціями (Play, Edit Tags, Delete, Explorer)
- [ ] PlayerPanel: Slider позиції працює для файлів, прихований для потоків
- [ ] Всі нові UI елементи мають AutomationProperties
