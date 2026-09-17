# SUBSISTENCE — Unity 2022.3.62f2 / HDRP / Mirror

Хардкорный сетевой выживач на 100+ игроков: **механики Rust** (строительство, рейды, лут, прочность, крафт)
в **мире Backrooms** (3 уровня: Жёлтые коридоры → Бассейны → Электростанция), еда/вода/радиация, монстры.

```
Subsistence/
├── UnityProject/                  ← ЭТО открывать в Unity Hub (в архиве это сам корень)
│   ├── Packages/manifest.json     HDRP 14.0.12 + Burst/Collections/Addressables (Mirror — адаптер)
│   ├── ProjectSettings/           ProjectVersion.txt + настройки генерирует Editor-бутстрап
│   └── Assets/Subsistence/
│       ├── Scripts/               геймплей (C#), 51 файл / ~18 900 строк, 230 типов, zero external deps
│       ├── Editor/                авто-настройка проекта (теги, слои, физика, HDRP-ассет, сцены)
│       └── Net/MirrorAdapter/      адаптер Mirror (включается define-символом MIRROR, меню Subsistence → 3)
├── tools/blender/                 Python-генераторы моделей (Blender 4.3, headless, FBX→Unity)
├── site/index.html                ВЕБ-КОНСОЛЬ (меню, как в игре): BIOS-терминал, меню [1..9], ASCII-лого
├── site/questions.html            ОПРОСНИК v1 (страница для ПК): 57 вопросов → subsistence_answers.json
├── site/questions2.html           ОПРОСНИК v2 (19 ответов уже в коде): 22 вопроса
├── docs/PROJECT_STATUS.md         ← СВОДКА ПО ИТОГУ ПРОЕКТА: что готово, что нет, цифры, порядок работ
└── docs/                          ГДД, строительство, дерево, сеть, ассет-байбл, плейтест, роадмап
└── docs/                          ГДД, строительство, БД предметов, сеть, ассет-байбл, роадмап
```

## Быстрый старт (5 минут)

1. **Unity Hub → Add → Add project from disk → корень архива** (`Subsistence_1.0.0-alpha/`,
   там сразу `Assets\` и `ProjectSettings\`) — Hub сам подтянет 2022.3.62f2.
2. Первый импорт: Unity скачает HDRP/Mirror/Burst из манифеста (нужен интернет), затем
   **меню `Subsistence → 1. Настроить проект`** (теги, слои, матрица физики, HDRP-ассет, сцены, билд-сеттингс).
3. **`Subsistence → 2. Создать мир (seed 1337)`** — генерируется сцена `Assets/Subsistence/Scenes/Levels.unity`
   с тремя уровнями, спавнами, лутом, монстрами и HUD.
4. **Play**. Пустая сцена тоже работает: `RuntimeBootstrap` соберёт мир на лету и покажет терминальную
   консоль (`UI/BootConsole.cs`): шапка `BACKROOMS OPERATING SYSTEM v2.0 CONSOLE`, боковое меню
   `[1] ИГРАТЬ … [0] ВЫХОД`, лог с ASCII-логотипом SUBSISTENCE и строкой ввода `C:\SUBSISTENCE>`.
   Команды: `PLAY HOST JOIN <ip> MAP SYSINFO SETTINGS HELP CLS EXIT` (+ служебная `SEED <n>`);
   `PLAY` печатает `loading level0_corridors ... starts ... ok` и запускает игру.
   Опросник в игре не нужен — он отдельной страницей: **`site/questions.html`**.
5. **`Subsistence → 3. Включить Mirror`** — включается define `MIRROR`, появятся
   `MirrorNetworkHost`, `MirrorPlayer`, `MirrorBuildingBridge`, и станет доступен Host/Join в меню.
6. **Модели**: `bash tools/build_models.sh all` → FBX в `UnityProject/Assets/Subsistence/Models/`
   (скрипты умеют и GLB, но в проект кладём только FBX — Unity собирает префабы из него).
7. **Подключить модели к коду**: в Unity меню `Subsistence → 7. Собрать префабы моделей`
   (импортирует FBX и раскладывает префабы в `Assets/Subsistence/Resources/Models/`).

## Что уже реализовано в коде (без заглушек)

| Система | Файл | Что внутри |
|---|---|---|
| Грид-лут, слоты, quick loot | `Core/Inventory.cs` | 6 хотбар + 24 рюкзак + 5 слотов брони, стаки, split, quick-move (Shift+ЛКМ), «забрать всё» (E-hold), дроп на смерть |
| Прочность предметов | `Core/Items.cs` | износ оружия/инструментов/брони в Rust-цифрах, ремонт в верстаке с потерей 25 % качества |
| Оружие | `Combat/Weapons.cs` | hitscan+снаряды, паттерны отдачи (фактические таблицы), разброс, ADS, перезарядка, 6 слотов обвесов, износ |
| Броня | `Combat/Armor.cs` | полные сеты Rust (hazmat → metal → roadsign) с резистами по типам урона и износом |
| Строительство | `Building/*.cs` | foundation/wall/doorway/window/floor/stairs/roof, сокеты, тиры twig→wood→stone→metal→armored, **стабильность и обвал**, decay+upkeep, tool cupboard с авторизацией, коды/замки, урон от C4/сачел/ракет по тирам |
| Рейды | `Combat/Explosives.cs` | C4, satchel, beancan, ракеты, гранаты, мины — реальные таблицы урона по тирам |
| Монстры | `AI/*.cs` | Smiler / Hound / Partygoer / Skin-Stealer (босс Bacteria отменён по ответам), FSM на сервере, слух/зрение/запах, A* по графу уровня |
| Транспорт | `Player/Transport.cs` | LootCart (24 слота, тянется, шум), Scooter (×1.7, глохнет в воде), Minecart на кольцевой колее станции, лут-лифты кабины `BD_elevator_car` |
| Торговля | `AI/TraderNpc.cs` | безопасная комната (PvP/spawn off), NPC-торговец с прилавком, вендинг-автомат, фиксированные цены |
| Админы | `Net/AdminSystem.cs` | ранги, god/noclip, команды (tp/give/kick/ban/rank/say), аудит-лог, валидатор читерских пакетов |
| Уровни | `World/LevelGenerator.cs` | процедурные «жёлтые коридоры», Poolrooms с водой, электростанция с реакторным залом; граф комнат для ИИ и лута |
| Лут по тирам | `World/LootSpawner.cs` | Tier1 коридоры → Tier2 бассейны → Tier3 электростанция, таблицы вероятностей, время респавна, ящики/шкафчики/электрощиты |
| Выживание | `Player/Survival.cs` | голод, жажда, радиация, кровотечение, инфекция, комфорт у огня, смерть/лут-мешок (рассудка нет — отменён в опроснике) |
| Сеть (100+) | `Net/*.cs` | серверный авторитет, командный буфер с квантизацией, снапшоты+интерполяция, AOI-грид, rate-limit и валидация (спидхак, частота выстрелов, дистанция стройки), Mirror-адаптер. Сервер владеет спавном деплоев/стройки (`SpawnNet`), дверями и замками (`DeployNet`), с `InventoryNet` — инвентарями (зеркало слотов), содержимым лут-ящиков, предметами на полу и мешками с трупом, а с `CombatNet` — уроном по игрокам (монстры/взрывы/огонь и попадания игроков: урон считается по таблице оружия, клиент числа не присылает) |
| UI | `UI/*.cs` | зелёная загрузочная консоль, главное меню, HUD, инвентарь drag&drop, крафт, план-радиал, чат/лог |

## Модели (сгенерированы и лежат в проекте)

```
UnityProject/Assets/Subsistence/Models/
├── Weapons/    W_rifle_ak, W_rifle_m4, W_smg_mp5, W_shotgun_pump, W_rifle_bolt,
│               W_lmg_m249, W_rocket_launcher, EX_grenade_f1, EX_explosive_timed (.fbx)
├── Characters/ CH_hazmat_suit (жёлтый костюм: капюшон, тёмный визор, синие перчатки,
│               шланги, ботинки — как на твоём референсе), MN_smiler, MN_hound,
│               MN_partygoer, MN_skinstealer, MN_bacteria (архив — босс отменён),
│               CH_trader_npc (торговец: разгрузочный жилет с подсумками, респиратор
│               с двумя фильтрами, очки, кепка, рация, фонарь, кобура, планшет) (.fbx)
└── Props/      16 пропсов (панели, лампы, плитка, картотека, реактор…) +
                «решения»: PR_scooter, PR_loot_cart, PR_minecart, PR_vending_machine,
                BD_elevator_car, 22 деплоя DD_* (верстак, печь, шкаф TC, двери, замки, ловушки…),
                7 лут-контейнеров (PR_supply_crate, PR_safe_box, PR_barrel, PR_barrel_radioactive,
                PR_toolbox, PR_medical_cabinet, PR_airdrop_crate) + PR_loot_bag (.fbx)
```

**159 моделей FBX**, сводные листы превью: `docs/previews/_sheet_*.png` — видно, что получилось,
например `CH_hazmat_suit.png` (жёлтый костюм с тёмным визором, синими перчатками и шлангами),
`CH_trader_npc.png` (торговец в респираторе), `PR_minecart.png` (клёпаная ванна + поручни + фонарь),
`PR_vending_machine.png` (витрина с банками и бутылками), `PR_safe_box.png` (сейф с клавиатурой
и стальной створкой), `PR_medical_cabinet.png` (белый шкаф с крестом и стеклом), `MN_smiler.png`,
`W_rifle_ak.png`.

Пересобрать / сделать свои:
`bash tools/build_models.sh [weapons|chars|props|transport|deploy|loot|all] [--no-render] [--only MODEL]`
(хазмат — отдельно: `bash tools/blender.sh chars_hazmat.py`, торговец — `chars_trader.py`).
Blender 4.3.2 LTS уже установлен в этом воркспейсе (`~/.cache/blender`, headless, без root) — файлы
скриптов: `tools/blender/` (`subs_common.py` — примитивы/материалы/экспорт, `models_weapons.py`,
`chars_hazmat.py`, `chars_trader.py`, `models_chars.py`, `models_props.py`, `models_transport.py`,
`models_deployables.py`, `models_loot.py`). Сводные листы батчей — `docs/previews/_batch_*.png`
(например `_batch_loot.png` — все 7 лут-контейнеров одним листом).

Спека качества: оружие — hard-surface с планками Пикатини, прицелами, сошками, насечками гранат;
персонажи — «ультра» плотность сетки (хазмат ≈ 40k трис, Bacteria ≈ 34k трис — модель в архиве, босс отменён), PBR-материалы
процедурные, UV развёрнуты (`smart_uv`), экспорт с осями Unity (-Z forward / Y up).

## Документы (читай по порядку)

- [`docs/GDD.md`](docs/GDD.md) — концепт, петля геймплея, цели дизайна
- [`docs/LEVELS_AND_LOOT.md`](docs/LEVELS_AND_LOOT.md) — 3 уровня, 3 тира лута, монстры, входы/выходы
- [`docs/BUILDING_AND_ITEMS.md`](docs/BUILDING_AND_ITEMS.md) — «копия Rust до мелочей»: стройка, рейды, прочность, цифры
- [`docs/BUILDING_AND_ITEMS.md`](docs/BUILDING_AND_ITEMS.md#предметы) — оружие, броня, крафт-дерево, тиры лута
- [`docs/NETWORK_AND_TECH.md`](docs/NETWORK_AND_TECH.md) — Mirror, 100 игроков, AOI, античит, репликация
- [`docs/ASSET_BIBLE.md`](docs/ASSET_BIBLE.md) — спека моделей (полигоны/текстуры/PBR), Blender→HDRP→Unity, где брать референсы
- [`docs/ANSWERS.md`](docs/ANSWERS.md) — **зафиксированные решения по 40 вопросам** и их привязка к коду
- [`docs/ROADMAP.md`](docs/ROADMAP.md) — 8 спринтов до плейтеста
- [`docs/PROJECT_STATUS.md`](docs/PROJECT_STATUS.md) — **итог проекта: что готово, чего нет, цифры и порядок работ**
- [`docs/QUESTIONS.md`](docs/QUESTIONS.md) — 57 вопросов (интерактивно — `site/questions.html`), v2 — `site/questions2.html`

> Честно про арт: чат-песочница не рендерит AAA-модели уровня Rust. Поэтому пайплайн такой —
> уникальное (хазмат-костюм, монстры Backrooms, жёлтые обои, процедурная геометрия) делается моими
> Blender-скриптами с чистой топологией и PBR-материалами, а оружие/пропсы по `ASSET_BIBLE.md`
> подтягиваются из готовых паков и приводятся к единому стилю (утилиты конформ-пайплайна, спринт 4).


---

## Сборка Windows-билда (в архиве)

- **Одним кликом:** двойной клик по `BUILD_WINDOWS.bat` (Unity Hub → 2022.3.62f2 LTS).
  Скрипт прогоняет пункты меню 1 → 4 → 5 → 7 → 2 → 6 и собирает `Builds/Windows/Subsistence.exe`.
  Лог — `build_log.txt`. Подробно: [`BUILD_WINDOWS.md`](BUILD_WINDOWS.md).
- **В редакторе:** `Subsistence → 9. Собрать всё и билд (одним кликом)` — то же самое из меню;
  после пункта 5 один раз нажать **Fix All** в открывшемся HDRP Wizard.
- **Сеть:** по умолчанию выключена (игра запускается офлайн). Включение — `Subsistence → 3. Включить Mirror`.

Текущее состояние кода: **51 .cs**, 159 моделей FBX — оружие собрано заново в v4 (ресиверы-оболочки,
прорезанные окна и вентиляция, заклёпки и планки; 62 531 трис, листы `_sheet_weapons*.png`),
плюс 37 предметов, 18 элементов стройки,
13 вариантов верстаков, оружие v3, монстры), 180 иконок предметов, 3 уровня, 3 тира лута,
серверная авторитетность лута/инвентаря/урона/деплоев. Компиляция в песочнице не выполнялась —
первый прогон в Unity может выдать несколько `error CS` (см. `BUILD_WINDOWS.md`, раздел «Честно про статус»).

### Что появилось в alpha 1.0.0 (сверх v0.4.0)

* **Сохранение мира** — `Core/SaveSystem.cs`: `F5` сохранить, `F9` загрузить, `F10` удалить сейв;
  автосейв каждые 300 с и при выходе. Сейв = блоки, деплои (ящики, верстак, печь, спальник, шкаф с кодом),
  контейнеры лута, игрок (инвентарь, статы, дерево изучения), погода, сид. Файлы — `persistentDataPath/Subsistence/`.
* **Иконки предметов** — 180 PNG в `Assets/Subsistence/Resources/icons/`, в слотах рисуются иконка
  и фон по редкости (Common … Anomalous).
* **37 моделей предметов** (было 12): еда и вода, медицина, патроны, броня из дерева/кожи, ресурсы,
  инструменты — лист `docs/previews/_sheet_loot_items.png`.
* **Сеть под 100+** — interest management с LOD (AOI 120 → 55 м, 20 → 16 Гц, лимит пакета 64 → 24),
  мягкий лимит онлайна 112, метрики нагрузки каждые 10 с, стенд серверных ботов:
  в консоли меню `LOADBOTS 112` или запуск `Subsistence.exe -batchmode -nographics --load-bots 112`.
  Подробно: [`docs/MULTIPLAYER.md`](docs/MULTIPLAYER.md).
