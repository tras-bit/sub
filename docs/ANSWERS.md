# SUBSISTENCE — ANSWERS.md
**Зафиксированные решения по 40 вопросам опросника (v1, 2026-09-13).**
Этот файл — источник истины для кода: комментарии в `Scripts/` ссылаются сюда
(`12_sanity`, `04_perf`, `Core/IDs.cs` → `Balance`). Менять значения можно только
вместе с правкой этого файла.

Опросник: `site/questions.html` (40 вопросов / 7 групп), ответы собираются в JSON
(`subsistence_answers.json`), ключи — `NN_id` и `NN_id_custom`.

Смежные уточнения (5 вопросов о стеке) — **HDRP**, **Mirror**, **процедурная генерация
уровней**, **50–100+ игроков (полный Rust-скейл)**, **Blender-скрипты + пайплайн** для
уникальных ассетов (магазинные паки допустимы для оружия/пропсов).

---

## A. Общее и техническое
| # | id | Решение | Как реализовано |
|---|----|---------|-----------------|
| 1 | `game_title` | **Subsistence** (в коде/файлах — `Subsistence`) | `Application.productName`, `RuntimeBootstrap.ProductId` |
| 2 | `pipeline` | **HDRP** (High Definition RP 14.0.12) | `Packages/manifest.json`, `Settings/HDRP_Subsistence.asset` |
| 3 | `unity` | **Unity 2022.3.62f2** (строго) | `ProjectSettings/ProjectVersion.txt` |
| 4 | `perf` | **120 FPS на RTX 3060** | `Balance.TargetFps = 120`, `Application.targetFrameRate` в `RuntimeBootstrap.Awake` |
| 5 | `platform` | **Только Windows** (без Linux-сервера на старте) | `Balance.WindowsOnly = true`, билд `Builds/Windows/Subsistence.exe` (`Editor/ProjectBootstrap`) |

## B. Мир, уровни, прогресс
| # | id | Решение | Как реализовано |
|---|----|---------|-----------------|
| 6 | `scale` | **100+ игроков** (полный Rust-скейл) | `MirrorNetworkHost`: 30 Гц тик, снапшоты 20 Гц, AOI 120 м, ≤128 игроков, `InterestGrid` |
| 7 | `wipe` | **1 месяц** | `Balance.WipeDays = 30` |
| 8 | `level_size` | **1 × 1 км** на каждый уровень | `Balance.LevelSizeMeters = 1000`, `LevelGenSettings`: сетка 168×168 × 6 м ≈ 1008 м |
| 9 | `connect_levels` | **Лифты по ключ-картам**: вниз — свободно, вверх — карта целевого уровня (зелёная → Level 37, синяя → Level 3) | `World/Transitions.cs` → `LevelElevator`, `LevelGenerator.ConnectLevels/BuildElevatorCage`, `LootContainer`-дропы `tool.keycard.*` |
| 10 | `weather` | **События 2 раза в сутки** (затопление Poolrooms / радиационный выброс) | ⚠ не в коде — спринт «События» (см. ROADMAP) |
| 11 | `endgame` | **Бесконечный прогресс без финала** (ТВЭЛ-«выход» отменён) | `Balance.EndlessProgression = true` |

## C. Выживание
| # | id | Решение | Как реализовано |
|---|----|---------|-----------------|
| 12 | `sanity` | **УБРАТЬ полностью** | `DamageType.Sanity` удалён; нет полей/эффектов в `Survival`, `Armor`, `Monsters`, `Deployables`, `GameUI`, `PlayerController`; предмет `sanity.pills` и его рецепт вырезаны |
| 13 | `starting_kit` | **Камень + факел + план + бинты** (Rust-старт) | `RuntimeBootstrap.GrantStartingKit` |
| 14 | `water` | **Полная механика + хлорированная вода** | `Survival` (жажда/Rad/вода), `water.bottle` / `water.chlorinated` |
| 15 | `food` | **Охота + консервы + готовка** | Оружие/ловушки, `can.beans` и др., верстак + костёр |
| 16 | `radiation` | **Нужен хазмат** (Level 3 — радиационная зона) | `hazmatsuit` 0.98 / экзо 0.80 resist, `RadiationZone` |
| 17 | `loot_respawn` | **5–30 минут от тира ящика** (кастомный ответ): T1 = 5 мин, T2 = 15 мин, T3 = 30 мин | `Balance.LootRespawnTier1/2/3 = 300/900/1800`, `LootSpawner.RespawnForTier` |
| 18 | `airdrop` | **Есть, приманка в бассейнах** | ⚠ не в коде — спринт «События» |

## D. Стройка, рейды, транспорт
| # | id | Решение | Как реализовано |
|---|----|---------|-----------------|
| 19 | `build_grid` | **3 × 3 м** | `BuildSystem.CellSize = 3`, грид-снап по центрам |
| 20 | `blueprints` | **Через стол исследований** | `ResearchTable` + `RecipeBook`, крафт только по выученным чертежам |
| 21 | `durability` | **Полная прочность** (оружие/броня/инструменты) | `Items` condition, `Combat` износ, `Repair` |
| 22 | `decay` | **Есть + Tool Cupboard**; кастом: *«гниение, если шкаф не поставить»* | `BuildSystem.DecayDirector`: нет TC → `DecaySystem.ApplyDecay`, есть TC → `PayUpkeep` |
| 23 | `raid` | **Полная копия Rust**: C4 550 / сачель 275 / ракета 275×300 | `Building.ApplyDamage`, `Explosives` |
| 24 | `raid_hours` | **24/7** (рейд-часов нет, хардкор) | `Balance.RaidHoursAlways = true` |
| 25 | `build_zones` | **Стройка разрешена везде** (запретных зон нет) | `Balance.BuildAnywhere = true`; лифты/безопасные комнаты — единственные исключения |
| 26 | `transport` | **Самокаты, тележки для лута, вагонетки, лифты на станции** (кастомный ответ) | `Player/Transport.cs` (LootCart 24 слота, Scooter ×1.7, Minecart на кольцевой колее), `World/Transitions.cs` (LevelElevator), `TransportSpawner` |

## E. Монстры и звук
| # | id | Решение | Как реализовано |
|---|----|---------|-----------------|
| 27 | `monsters` | **Реагируют на свет и шум** (звук приманивает) | `NoiseSystem` (шаги/скрипы/выстрелы), `MonsterDirector`, слух/зрение в FSM |
| 28 | `smiler` | **120 HP, истребим** | `MonsterDef` Smiler 120/18; ярко-белые зубы-эмиссия |
| 29 | `boss` | **Босса Bacteria убрать** | Спавн-волна и босс-механики удалены из `RuntimeBootstrap`/`MonsterDirector`; модель `MN_bacteria` остаётся в архиве ассетов и в игру не спавнится |
| 30 | `audio` | **3D-звук + гул ламп** (атмосфера Backrooms) | `AudioDirector`: 3D-источники, гул ламп/вентиляции, приоритет по дистанции |
| 31 | `voice_ai` | **Процедурная озвучка** (без актёров) | Крики монстров генерируются из шумового слоя + питч-модуляции |

## F. Сеть
| # | id | Решение | Как реализовано |
|---|----|---------|-----------------|
| 32 | `net` | **Mirror** | `Net/MirrorAdapter/MirrorNetworkHost.cs` за `#if MIRROR`; включается `Editor → Subsistence → 3` |
| 33 | `voice` | **Proximity 20 м + рация** | `VoiceDirector`: 3D-чат, рация без границ, приглушение через стены |
| 34 | `ff` | **Полный дружественный огонь** | `DamageSystem`: FF всегда включён, исключение — SafeRoom |
| 35 | `groups` | **Без лимитов** (группы любого размера) | `Balance.MaxGroupSize = 0` (не ограничивать) |
| 36 | `anticheat` | **Свой античит + админы** | `Net/AdminSystem.cs` (ранги Player/Moderator/Owner, аудит-лог 500 событий), `CommandValidator` (3 нарушения → Disconnect) |

## G. Прочее
| # | id | Решение | Как реализовано |
|---|----|---------|-----------------|
| 37 | `modding` | **Игра закрытая** (без поддержки модов) | `Balance.ClosedSource = true`; единый билд, античит сверяет сборку |
| 38 | `skins` | **Скины предметов есть** | ⚠ не в коде — спринт «Магазин скинов» |
| 39 | `trading` | **Вендинг + NPC-торговец в безопасной комнате** | `AI/TraderNpc.cs`: `SafeRoom` (r=18, PvP off), `TraderNpc` (прилавок 6 слотов), `VendingMachine`, `TradeStall.Populate` |
| 40 | `art_style` | **Ультра-реализм** | PBR-материалы, шум-текстуры, масштаб в метрах, ультра-детализация в Blender-пайплайне (`tools/blender/`) |

---

## Что из ответов уже в коде (проверка по файлам)

- `Core/IDs.cs` → `Balance`: `TargetFps=120`, `WipeDays=30`, `LevelSizeMeters=1000`,
  `LootRespawnTier1/2/3=300/900/1800`, `MaxGroupSize=0`, `BuildAnywhere/WindowsOnly/
  ClosedSource/RaidHoursAlways/EndlessProgression = true`; `DamageType.Sanity` удалён.
- Рассудок вычищен в: `Player/Survival.cs`, `AI/Monsters.cs`, `Combat/Armor.cs`,
  `Player/Deployables.cs`, `Player/PlayerController.cs`, `UI/GameUI.cs`,
  `Core/Items.cs` (+рецепт в `Crafting/CraftingSystem.cs`).
- Стройка: `Building/BuildSystem.cs` (`DecayDirector`, `BuildingRegistry.AllBuildingIds`,
  `ToolCupboard.All/FindFor`).
- Мир: `World/LevelGenerator.cs` (сетка 168×168, `ConnectLevels` → лифты, `BuildElevatorCage`),
  `World/Transitions.cs` (`LevelElevator`), `World/LootSpawner.cs` (`RespawnForTier`).
- Транспорт/торговля/админы: `Player/Transport.cs`, `AI/TraderNpc.cs`, `Net/AdminSystem.cs`.
- Точка входа: `Runtime/RuntimeBootstrap.cs` (`Instance`, 120 FPS, набор уровня,
  транспорт, торговцы, decay, админы; телепорт лифтом — `TeleportPlayerTo`).

## Что уже в коде (обновлено)

1. **События/погода** — `World/WeatherSystem.cs`: 2 события в сутки, 7 видов (конденсат, блэкаут,
   хлорный туман, прилив, пар, перегрев…), сервер-only, `SpeedMultiplier` 0.85–1.0, +3 монстра
   за событие, `Force(kind, dur)` для отладки и строка в HUD.
2. **Айрдроп** — `World/AirdropSystem.cs`: интервал 15–28 мин (первый через 3 мин), ящик с парашютом
   и мигающим маяком, лут `loot.airdrop`; в Poolrooms 50 % сбросов — **приманка**: 36 с шума
   (радиус 70 м) и 4 монстра. В безопасных комнатах сбросов нет.
3. **Магазин скинов** — `Progression/Skins.cs`: 14 скинов за скрап/токены, покупка и надевание
   (клавиши 1–9 / 0), привязка `ItemStack.skinId`; открытие — консоль `SKINS` или у торговца (присесть + E).
4. **Постановка деплоев** — `Player/DeployPlacer.cs` (`21_deploy`): взял предмет → ЛКМ поставил,
   призрак зелёный/красный, чужая территория TC запрещает, ящик 6/30 слотов как в Rust.
5. **Модели вместо заглушек** — `World/ModelLibrary.cs` + меню `Subsistence → 7` собирает префабы
   из FBX; модели подставляются монстрам, луту, каретам лифта, транспорту, мешку с лутом и всем деплоям.

## Ассеты под решения (Blender, ultra-detail)

| Модель | Файл | Под что |
|--------|------|---------|
| Самокат | `PR_scooter.fbx/glb` | `26_transport` (×1.7 скорость, шум) |
| Тележка для лута | `PR_loot_cart.fbx/glb` | `26_transport` (24 слота, тянется) |
| Вагонетка | `PR_minecart.fbx/glb` | станция, кольцевая колея |
| Кабина лифта | `BD_elevator_car.fbx/glb` | `09_connect_levels` (лифты по картам) |
| Вендинг-автомат | `PR_vending_machine.fbx/glb` | `39_trading` |
| Торговец (NPC) | `CH_trader_npc.fbx/glb` | `39_trading` (безопасная комната) |
| Мешок с лутом | `PR_loot_bag.fbx/glb` | смерть игрока (всё снаряжение падает в мешок) |
| Деплои (22 шт.) | `DD_*.fbx/glb` | верстаки, печи, TC, кровати, турели, ПВО, генератор, двери, замки, табличка, баррикады, ловушки |

Сборка: `bash tools/build_models.sh transport|deploy` (транспорт/деплои),
`bash tools/build_models.sh props --only PR_loot_bag` (одна модель),
`bash tools/blender.sh chars_trader.py` (торговец). Превью — `docs/previews/` (71 PNG).
