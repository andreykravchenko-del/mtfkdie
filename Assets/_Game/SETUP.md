# Сборка сцены MVP «Мир без тебя»

> **СТАТУС:** сцена `Assets/Scenes/SampleScene.unity` уже собрана и проверена в Play Mode
> (через Unity MCP). Инструкция ниже — справочная (если собирать заново вручную). Учти два
> отличия финальной реализации от изначального плана:
> - **Осмотр предмета** рендерится камерой `InspectCamera` в **RenderTexture**, а показывается
>   через `RawImage` (`ObjectView`) на панели `InspectPanel` поверх чёрного фона `BgBlack`.
>   Так весь UI (описание/реплики/шкала) и предмет лежат на одном Screen Space Overlay —
>   иначе вторая экранная камера перекрывает оверлей в URP.
> - **Интро** гоняет по ракурсам саму **Main Camera** (поле `IntroSequence.flyCamera`) и
>   возвращает её на место игрока; отдельной интро-камеры нет.
>
> Осталось подставить реальный арт/звук вместо плейсхолдеров (цветные кубы, градиентные
> спрайты воспоминаний в `Assets/_Game/Art`, аудио-клипы не назначены — источники готовы).

Скрипты — в `Assets/_Game/Scripts`. Ниже что собрать вручную в редакторе. Порядок важен.

## Управление (итог)

| Действие | Ввод |
|---|---|
| Ходьба / осмотр | WASD + мышь |
| Открыть предмет / лечь в кровать | **ЛКМ** (клик по объекту под прицелом) |
| Запоминать (наполнять шкалу) | зажать **E** |
| Отменить запоминание (опустошать) | зажать **F** (играет серый шум) |
| Выйти из осмотра, предмет остаётся | **Esc** / **ПКМ** / крестик в UI |
| Переиграть звук предмета | **R** |
| Крутить предмет в осмотре | зажать ЛКМ + двигать мышь |
| Листать монолог / пропустить печать | ЛКМ / Пробел / Enter (зажать **E** — быстрее) |
| Пропустить интро | любая клавиша / клик |

---

## 0. Разовая подготовка

1. **TMP:** Window ▸ TextMeshPro ▸ Import TMP Essential Resources.
2. **Слои** (Edit ▸ Project Settings ▸ Tags and Layers): `Interactable`, `InspectStage`.
3. Открыть сцену `Assets/Scenes/SampleScene.unity`.
4. (Опц.) Удалить `Assets/test.cs` и `Assets/TutorialInfo/`.

## 1. Комната

- Пол/стены — примитивы. Материалы предметов — **URP/Lit** (нужно для подсветки Emission).
- Поставь плейсхолдер ГГ (капсула/модель) — его покажет интро.

## 2. Игрок

- Create Empty → **Player** на полу. Add Component **Character Controller** (Center Y≈1, Height≈2)
  и **PlayerController**.
- Внутрь — **Main Camera** (Y≈1.6, тег `MainCamera`, здесь оставить единственный **Audio Listener**).
  Add Component **PlayerInteractor**.
- У Main Camera в **Culling Mask** снять `InspectStage`.
- **Шаги:** внутри Player добавь дочерний объект со своим **AudioSource** (clip = звук шагов,
  **Loop = вкл**, Play On Awake = выкл) и укажи его в `PlayerController ▸ Footsteps Source`.

## 3. Сцена осмотра (InspectStage)

- Create Empty → **InspectStage** далеко от комнаты, напр. `(100,100,100)`.
  - **InspectStagePivot** (пусто) в той же точке.
  - **InspectCamera** (Camera), смотрит на pivot (отступ по -Z ~1.5):
    - Clear Flags = **Solid Color**, Background = **чёрный**;
    - Culling Mask = **только `InspectStage`**; **Depth = 1**;
    - **удалить Audio Listener**; сам GameObject **выключить**.
  - Свет рядом с pivot (иначе предмет тёмный на чёрном).

> Чёрный фон осмотра даёт сама InspectCamera. Панель осмотра в UI НЕ должна иметь
> непрозрачного фона, иначе перекроет 3D-предмет.

## 4. Интро-камера (пункт 0)

- Create Empty → **IntroSetup**, Add Component **IntroSequence**.
- **IntroCamera** (Camera): **Depth = 5** (выше остальных), Clear Flags = Skybox/Solid,
  Culling Mask = всё кроме `InspectStage`; **удалить Audio Listener**; GameObject **выключить**
  (скрипт включит на время интро).
- Создай несколько пустых объектов-«ракурсов» по комнате (позиция+поворот = кадр) и
  перетащи их по порядку в `IntroSequence ▸ Waypoints`. Заполни `Intro Lines` (вступление),
  при желании `Intro Music`.

## 5. Системы

Create Empty → **_Systems**, повесить: **GameManager**, **AudioManager**, **InspectionController**,
**MemoryReveal**, **NarrativeSystem**, **EndingController**.
- **AudioManager ▸ Background Music** = фоновый трек (заиграет со старта).

## 6. UI (Canvas, Screen Space – Overlay) + EventSystem

### Reticle (виден всегда) → **ReticleUI**
- `Dot` = маленький Image по центру; `Prompt Text` = TMP («ЛКМ — …»).

### InspectPanel (БЕЗ непрозрачного фона, выключен)
- `Name Text`, `Description Text`, `Comment Text` (реплики ГГ), `Hold Hint Text`.
- `Capture Bar Root` (выключаемый) → внутри Image **Fill** (*Image Type = Filled, Horizontal*) → `Capture Bar Fill`.
- **Крестик:** UI Button → в OnClick вызвать `InspectionController.Exit()`.

### DialoguePanel (полупрозрачная плашка, выключена)
- TMP `Text` → `NarrativeSystem.Text`; плашка → `NarrativeSystem.Panel`.

### RevealPanel (непрозрачный, выключен)
- Фон + большой `Image` (Preserve Aspect) + `Caption` (TMP). → поля `MemoryReveal`.

### Концовки (выключены)
- **EndingPanel_Life** (светлые картинки/текст), **EndingPanel_Death** (тёмный фон + текст).
  Каждая панель может содержать несколько картинок.

## 7. Аудио-источники под механику звука (пункт 10)

Заведи отдельные **AudioSource** и привяжи в поля скриптов (Play On Awake = выкл):

| Источник | Куда привязать | Loop |
|---|---|---|
| Шаги | `PlayerController ▸ Footsteps Source` | вкл |
| Бубнёж на фразах | `NarrativeSystem ▸ Mumble Source` (clip = бубнёж) | вкл |
| Серый шум при отмене (F) | `InspectionController ▸ Grey Noise Source` (clip = шум) | вкл |
| Музыка/шум финала | внутри `AudioManager` (создаётся сам) | — |

Разовые звуки задаются клипами прямо в полях: `InspectionController` (Open/Success),
`NarrativeSystem` (Advance), `EndingController` (Life Music / Death Grey Noise),
а **звук предмета** и **звук воспоминания** — в ассете `MemoryData` (`Item Sound`, `Reveal Sound`).

## 8. Привязать ссылки

- **PlayerInteractor**: `Cam`, `Reticle`, `Interact Mask = Interactable`, `Range≈2.5`.
- **InspectionController**: `Inspect Camera`, `Stage Pivot`, `Inspect Layer Name = InspectStage`,
  все UI-поля, `Grey Noise Source`, клипы.
- **MemoryReveal / NarrativeSystem / EndingController / IntroSequence** — по полям выше.

## 9. Предметы, кровать, монолог

- **MemoryData:** Create ▸ Game ▸ Memory Data. Заполни название, описание, Good/Bad, points,
  captureDuration, реплики, картинку, `Item Sound`, `Reveal Sound`. Сложи в `_Game/Data`.
- **Предмет:** 3D-объект → слой `Interactable`, Collider, компонент **Interactable**, `Data` = ассет.
  `Inspect Local Scale` — подгон размера в осмотре.
- **Кровать:** 3D-объект → слой `Interactable`, Collider, компонент **BedInteractable**.
- Стартовый монолог можно задать прямо в `IntroSequence ▸ Intro Lines` (покажется после облёта),
  либо отдельным **NarrativeTrigger** (`Play On Start`).

---

## Проверка (Play Mode)

1. Старт → плавный облёт ракурсов, играет фон/интро-музыка. Любая клавиша пропускает →
   (опц.) вступительный монолог с бубнёжом → управление игроку.
2. Ходьба WASD слышны шаги; курсор скрыт по центру.
3. Навёл на предмет → прицел желтеет, «ЛКМ — [имя]», предмет светится.
4. ЛКМ → чёрный экран осмотра, слышен звук предмета (R — переиграть), крутим ЛКМ, видно описание.
5. Зажать **E** → шкала растёт, реплики ГГ. Зажать **F** → шкала падает + серый шум; на нуле
   выходим, **предмет остаётся**. Esc/крестик — тоже выход, предмет остаётся.
6. Довёл E до конца → картинка-воспоминание + её звук, баллы (GameManager: Good→Life, Bad→Death).
7. Клик по кровати → концовка: Life>Death — светлая панель + музыка; иначе — тёмная + серый шум.

## Грабли

- Предмет тёмный в осмотре → свет к pivot или эмиссия материалу.
- Комната перекрыта чёрным вне осмотра → InspectCamera GameObject должен стартовать выключенным.
- «Multiple Audio Listeners» в консоли → оставь Audio Listener только на Main Camera.
- Нет подсветки → материал предмета должен быть URP/Lit.
- Интро сбрасывается в Explore сразу → проверь, что объект с `IntroSequence` включён (GameManager
  отдаёт старт интро, только если находит его в сцене).
