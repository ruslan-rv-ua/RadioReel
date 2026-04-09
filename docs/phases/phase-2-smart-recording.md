# Фаза 2 — Розумний запис

## Мета

Автоматизація відбору контенту: що записувати, коли записувати, та паралельний запис кількох потоків.

**Залежить від:** Фаза 1 (IcyStreamClient, RecordingSession, StreamRecorder, TrackSplitter).

---

## Scope

### Що входить

- Одночасний запис кількох потоків
- Wishlist — автоматичний запис бажаних треків/артистів
- Ignorelist — глобальний та per-stream пропуск небажаного контенту
- Заплановані записи (Scheduler)
- Розширення UI: мультизапис у StreamsTab, вкладка Wishlist, вкладка Розклад
- Збереження/завантаження потоків як частини per-profile даних (підготовка до профілів)

### Що НЕ входить

- Програвач (WASAPI)
- Бібліотека збережених пісень (повна: з тегами, фільтрацією)
- Radio Browser API
- Профілі (тільки підготовка моделей)
- Постобробка
- Локалізація, теми

---

## Нові файли

### Core

| Файл | Призначення |
|------|-------------|
| `Core/Models/WishlistEntry.cs` | Модель: шаблон + wildcard + мін. бітрейт + формат |
| `Core/Models/IgnorelistEntry.cs` | Модель: шаблон ігнорування |
| `Core/Models/ScheduledRecording.cs` | Модель: потік + день + час + тривалість + тип + enabled |
| `Core/Scheduling/RecordingScheduler.cs` | Timer (1 хв), перевірка розкладу, запуск/зупинка записів |
| `Core/Storage/ProfileData.cs` | Модель per-profile даних (streams, wishlist, ignorelist, schedule) |
| `Core/Storage/ProfileStore.cs` | JSON серіалізація `.rrprofile` |

### UI

| Файл | Призначення |
|------|-------------|
| `UI/ViewModels/WishlistViewModel.cs` | Логіка wishlist + ignorelist |
| `UI/ViewModels/ScheduledRecordingsViewModel.cs` | Логіка розкладу |
| `UI/Views/Tabs/WishlistTab.xaml / .cs` | Вкладка з під-вкладками Бажані/Ігноровані |
| `UI/Views/Tabs/ScheduledRecordingsTab.xaml / .cs` | Вкладка розкладу |
| `UI/Views/Dialogs/ScheduledRecordingDialog.xaml / .cs` | Додавання/редагування запланованого запису |

**Модифікуються:**

| Файл | Зміна |
|------|-------|
| `MainWindow.xaml` | Додати вкладки «Wishlist» та «Розклад», Ctrl+3 та Ctrl+4 |
| `StreamsViewModel.cs` | Мультизапис: список RecordingSession, параллельні Task |
| `RecordingSession.cs` | Інтеграція з Wishlist/Ignorelist фільтрацією |
| `StreamRecorder.cs` | Перевірка Ignorelist перед збереженням треку |
| `App.xaml.cs` | Реєстрація нових сервісів у DI |

---

## Порядок реалізації

### 1. Мультизапис

Розширення `RecordingSession` та `StreamsViewModel`:
- `StreamsViewModel` тримає `ObservableCollection<RecordingSession>`
- Кожен `RecordingSession` — окремий `Task` з власним `CancellationToken`
- UI: кнопки «Почати запис» і «Зупинити запис» працюють на обраному потоці
- ListView показує статус кожного потоку незалежно
- Контекстне меню дозволяє керувати записом окремого потоку

**Обмеження ресурсів:**
- Кожен потік = 1 TCP з'єднання + 1 FileStream
- Стійкість: один потік з помилкою не впливає на інші

### 2. Wishlist / Ignorelist

**WishlistEntry — модель:**
```csharp
public class WishlistEntry
{
    public string Pattern { get; set; }       // "Pink Floyd - *" або "Lady Gaga - Bad Romance"
    public int MinBitrate { get; set; }        // 0 = будь-який
    public string Format { get; set; }         // "mp3", "aac", "" = будь-який
    public bool RemoveAfterRecording { get; set; }
    public bool AddToIgnorelistAfterRecording { get; set; }
    public int RecordedCount { get; set; }
}
```

**Логіка зіставлення:**
- Wildcard: `*` → будь-які символи, `?` → один символ
- Конвертувати в regex: `Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".")`
- Case-insensitive
- Перевірка на кожен `TrackChanged` event

**IgnorelistEntry:**
```csharp
public class IgnorelistEntry
{
    public string Pattern { get; set; }
}
```

**Два рівні:**
- Глобальний ignorelist (перевіряється для всіх потоків)
- Per-stream ignorelist (лише для конкретного потоку, зберігається в `StreamEntry.LocalIgnorelist`)

**Пріоритет:** Ignorelist перевіряється ДО Wishlist. Якщо трек в обох — він ігнорується.

### 3. Scheduler

**RecordingScheduler:**
- `DispatcherTimer` з інтервалом 60 секунд
- На кожен тік: перевірити всі `ScheduledRecording` з `Enabled = true`
- Якщо поточний час відповідає `StartTime` і `DayOfWeek` → запустити запис
- Якщо `StartTime + Duration` мине → зупинити запис (дочекавшись кінця поточного треку)
- Конфлікти (ARCHITECTURE §4.4.3):
  - Потік уже записується вручну → пропустити
  - Два розклади на той самий потік → перший виграє, другий у лог
  - Програма була вимкнена → лог `[ScheduledRecording] Missed: <name> at <time>`

**Часовий пояс:** локальний час системи (без UTC конвертації).

---

## UI деталі

### Вкладка «Wishlist» (Ctrl+3)

**Структура:**
```
┌──────────────────────────────────────────┐
│  Внутрішній TabControl                   │
│  [Бажані] [Ігноровані]                   │
│ ┌──────────────────────────────────────┐ │
│ │  [TextBox: шаблон] [Button: Додати]  │ │
│ │                                      │ │
│ │  ListView: список шаблонів           │ │
│ │  ┌──────────┬─────────┬────────┐    │ │
│ │  │ Шаблон   │ Бітрейт │ Записано│    │ │
│ │  └──────────┴─────────┴────────┘    │ │
│ │                                      │ │
│ │  [Імпорт з файлу] [Експорт у файл]  │ │
│ └──────────────────────────────────────┘ │
└──────────────────────────────────────────┘
```

**Accessibility:**
- `AutomationProperties.Name="Шаблон для wishlist"` на TextBox
- Під-вкладки: `AutomationProperties.Name="Бажані"` / `"Ігноровані"`
- ListView: `AutomationProperties.Name="Список бажаних шаблонів"` / `"Список ігнорованих шаблонів"`
- Контекстне меню: Редагувати, Видалити, Перемістити до Ignorelist

### Вкладка «Розклад» (Ctrl+4)

**Структура:**
```
┌──────────────────────────────────────────┐
│  [Button: Додати запис до розкладу]      │
│                                          │
│  ListView: Заплановані записи            │
│  ┌─────┬──────┬────────┬──────┬────────┐│
│  │ Вкл │ Назва│ Потік  │ Час  │ Тривал.││
│  └─────┴──────┴────────┴──────┴────────┘│
└──────────────────────────────────────────┘
```

**Accessibility:**
- Button: `AutomationProperties.Name="Додати запис до розкладу"`
- ListView: `AutomationProperties.Name="Заплановані записи"`
- Контекстне меню: Увімкнути/Вимкнути, Редагувати, Видалити

### ScheduledRecordingDialog

**Tab Order:**
1. ComboBox «Потік» — `AutomationProperties.Name="Потік"`, список зі streams
2. TextBox «Назва» — `AutomationProperties.Name="Назва запису"` (опціонально)
3. ComboBox «День тижня» — `AutomationProperties.Name="День тижня"`
4. TextBox «Час початку» — `AutomationProperties.Name="Час початку (ГГ:ХХ)"`
5. TextBox «Тривалість» — `AutomationProperties.Name="Тривалість (хвилини)"`
6. ComboBox «Тип» — `AutomationProperties.Name="Тип запису"` (Одноразовий / Повторюваний)
7. CheckBox «Увімкнено» — `AutomationProperties.Name="Запис увімкнено"`
8. Button «Зберегти» — `IsDefault=True`
9. Button «Скасувати» — `IsCancel=True`

### Оголошення скрінрідера (нові для Фази 2)

| Подія | LiveSetting | NVDA оголошує |
|-------|-------------|---------------|
| Wishlist збіг | Assertive | "Wishlist: знайдено {артист} — {назва} на {станція}" |
| Плановий запис старт | Assertive | "Плановий запис розпочато: {назва}" |
| Плановий запис кінець | Assertive | "Плановий запис завершено: {назва}" |
| Пропущений запис | Polite | "Пропущений плановий запис: {назва}" |

---

## Keyboard Shortcuts (нові для Фази 2)

| Дія | Клавіша |
|-----|---------|
| Вкладка Wishlist | Ctrl+3 |
| Вкладка Розклад | Ctrl+4 |

---

## Критерії завершення Фази 2

- [ ] Записувати 5+ потоків одночасно без помилок
- [ ] Додавати шаблони до Wishlist з wildcard (`*`, `?`)
- [ ] Автоматично зберігати трек при збігу з Wishlist
- [ ] Ігнорувати треки зі списку Ignorelist (глобальний + per-stream)
- [ ] Пріоритет: Ignorelist > Wishlist
- [ ] Створювати, редагувати, видаляти заплановані записи
- [ ] Планувальник автоматично запускає/зупиняє записи за розкладом
- [ ] Конфлікти обробляються за ARCHITECTURE §4.4.3
- [ ] NVDA оголошує: збіг Wishlist, старт/стоп планового запису
- [ ] Імпорт/експорт Wishlist з текстового файлу
- [ ] Всі нові UI елементи мають AutomationProperties
