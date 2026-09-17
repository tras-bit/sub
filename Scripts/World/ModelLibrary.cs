// ============================================================================
//  SUBSISTENCE — World/ModelLibrary.cs
//  Мост между кодом и готовыми моделями Blender (Assets/Subsistence/Models/*).
//
//  Зачем отдельная библиотека, а не прямые ссылки на префабы:
//   • игра поднимается из кода в пустой сцене (Runtime/RuntimeBootstrap.cs),
//     поэтому ссылок в сцене нет и быть не может;
//   • в билд попадают только ассеты из папки Resources/, поэтому Editor-меню
//     «Subsistence → 7. Модели: префабы и Resources» (Editor/ModelPrefabBuilder.cs)
//     раскладывает префабы в Assets/Subsistence/Resources/Models/<имя>.prefab;
//   • если префабов ещё нет (или человек только что склонировал репозиторий и
//     не нажал пункт меню) — Resources.Load вернёт null, и мы молча оставляем
//     старую примитив-заглушку. Никаких исключений и «розовых» объектов.
//
//  Габариты: модель может прийти из Blender в любых единицах (типичные 0.01),
//  поэтому FitTo() сам мерит bounds, ставит модель нужной высоты и «сажает» низ
//  на уровень пола родителя. Ручная настройка импорта не нужна.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Subsistence.Core;
using Subsistence.AI;
using Subsistence.Building;

namespace Subsistence.World
{
    /// <summary>Свои вещи для каждого уровня: генератор берёт декор только из своего набора.</summary>
    public static class LevelProps
    {
        public static readonly string[] Corridors =
        {
            ModelLibrary.Names.CorridorPanel, ModelLibrary.Names.OfficeChair, ModelLibrary.Names.LvFilingCabinet,
            ModelLibrary.Names.WaterCooler, ModelLibrary.Names.CardboardStack, ModelLibrary.Names.MopBucket,
            ModelLibrary.Names.DeskFan, ModelLibrary.Names.ExitSign, ModelLibrary.Names.CeilingLight,
        };

        public static readonly string[] Poolrooms =
        {
            ModelLibrary.Names.PoolLadder, ModelLibrary.Names.Lifebuoy, ModelLibrary.Names.DeckChair,
            ModelLibrary.Names.PoolPump, ModelLibrary.Names.PipeValve, ModelLibrary.Names.ShowerHead,
            ModelLibrary.Names.WetFloorSign, ModelLibrary.Names.PoolTilePanel, ModelLibrary.Names.InflatableRing,
        };

        public static readonly string[] PowerStation =
        {
            ModelLibrary.Names.ControlPanel, ModelLibrary.Names.BreakerCabinet, ModelLibrary.Names.TransformerUnit,
            ModelLibrary.Names.PipeFlange, ModelLibrary.Names.ValveWheel, ModelLibrary.Names.CoolantTank,
            ModelLibrary.Names.CableSpool, ModelLibrary.Names.WarningSign, ModelLibrary.Names.TurbineHousing,
        };

        public static string[] For(LevelTheme theme)
            => theme == LevelTheme.Poolrooms ? Poolrooms
             : theme == LevelTheme.PowerStation ? PowerStation
             : Corridors;
    }

    public static class ModelLibrary
    {
        public const string Root = "Models/";

        /// <summary>Имена моделей = имена файлов в Assets/Subsistence/Models (FBX без расширения).</summary>
        public static class Names
        {
            // --- персонажи / монстры ---
            public const string Smiler = "MN_smiler";
            public const string Hound = "MN_hound";
            public const string Partygoer = "MN_partygoer";
            public const string SkinStealer = "MN_skinstealer";
            public const string Whisperer = "MN_whisperer";
            public const string Drowned = "MN_drowned";
            public const string Spark = "MN_spark";
            public const string Hazmat = "CH_hazmat_suit";
            public const string Trader = "CH_trader_npc";

            // --- транспорт и станция ---
            public const string Scooter = "PR_scooter";
            public const string LootCart = "PR_loot_cart";
            public const string Minecart = "PR_minecart";
            public const string Vending = "PR_vending_machine";
            public const string ElevatorCar = "BD_elevator_car";

            // --- лут / пропсы ---
            public const string CrateWood = "PR_crate_wood";
            public const string Locker = "PR_locker";
            public const string FilingCabinet = "PR_filing_cabinet";
            public const string ElectricalPanel = "PR_electrical_panel";
            public const string PipeKit = "PR_pipe_kit";
            public const string Transformer = "PR_transformer";
            public const string Turbine = "PR_turbine";
            public const string Reactor = "PR_reactor";
            public const string LampPanel = "PR_lamp_panel_L0";
            public const string WallPanel = "PR_wall_panel_L0";
            public const string CarpetTile = "PR_carpet_tile_L0";
            public const string PoolTile = "PR_pool_tile_block";
            // --- лут-контейнеры PR_* (7): ящик, армейский ящик, сейф, бочки, тулбокс, медшкаф, айрдроп ---
            public const string SupplyCrate = "PR_supply_crate";
            public const string SafeBox = "PR_safe_box";
            public const string Barrel = "PR_barrel";
            public const string BarrelRadioactive = "PR_barrel_radioactive";
            public const string Toolbox = "PR_toolbox";
            public const string MedicalCabinet = "PR_medical_cabinet";
            public const string AirdropCrate = "PR_airdrop_crate";

        public const string LootBag = "PR_loot_bag";

        // ---------- УНИКАЛЬНЫЙ ДЕКОР УРОВНЕЙ (LV_*): у каждого уровня свои вещи ----------
        // L0 «Жёлтые коридоры»
        public const string CorridorPanel = "LV_corridor_panel";
        public const string CeilingLight = "LV_ceiling_light";
        public const string OfficeChair = "LV_office_chair";
        public const string LvFilingCabinet = "LV_filing_cabinet";   // декор L0 (не путать с PR_filing_cabinet — лут-контейнером)
        public const string WaterCooler = "LV_water_cooler";
        public const string CardboardStack = "LV_cardboard_stack";
        public const string MopBucket = "LV_mop_bucket";
        public const string ExitSign = "LV_exit_sign";
        public const string DeskFan = "LV_desk_fan";
        // L37 «Бассейны»
        public const string PoolLadder = "LV_pool_ladder";
        public const string Lifebuoy = "LV_lifebuoy";
        public const string DeckChair = "LV_deck_chair";
        public const string PoolPump = "LV_pool_pump";
        public const string PipeValve = "LV_pipe_valve";
        public const string ShowerHead = "LV_shower_head";
        public const string WetFloorSign = "LV_wet_floor_sign";
        public const string PoolTilePanel = "LV_pool_tile_panel";
        public const string InflatableRing = "LV_inflatable_ring";
        // L3 «Электростанция»
        public const string TurbineHousing = "LV_turbine_housing";
        public const string ControlPanel = "LV_control_panel";
        public const string TransformerUnit = "LV_transformer";
        public const string BreakerCabinet = "LV_breaker_cabinet";
        public const string PipeFlange = "LV_pipe_flange";
        public const string ValveWheel = "LV_valve_wheel";
        public const string CoolantTank = "LV_coolant_tank";
        public const string CableSpool = "LV_cable_spool";
        public const string WarningSign = "LV_warning_sign";

            // --- деплои (модели DD_*) ---
            public const string Workbench = "DD_workbench";       // базовая (архивная) модель
            public const string Workbench1 = "DD_workbench1";     // T1: деревянный стол с тисками
            public const string Workbench2 = "DD_workbench2";     // T2: стальная рама, сверлильный станок
            public const string Workbench3 = "DD_workbench3";     // T3: токарный узел, шкаф, барабан
            public const string RepairBench = "DD_repair_bench";
            public const string ResearchTable = "DD_research_table";
            public const string Furnace = "DD_furnace";
            public const string FurnaceLarge = "DD_furnace_large";
            public const string Cupboard = "DD_cupboard";
            public const string SleepingBag = "DD_sleepingbag";
            public const string Bed = "DD_bed";
            public const string Purifier = "DD_purifier";
            public const string AutoTurret = "DD_autoturret";
            public const string SamSite = "DD_samsite";
            public const string WindGenerator = "DD_wind_generator";
            public const string DoorWood = "DD_door_wood";
            public const string DoorMetal = "DD_door_metal";
            public const string DoorArmored = "DD_door_armored";
            public const string LockCode = "DD_lock_code";
            public const string LockKey = "DD_lock_key";
            public const string Sign = "DD_sign";
            public const string BarricadeConcrete = "DD_barricade_concrete";
            public const string BarricadeMetal = "DD_barricade_metal";
            public const string TrapSpikes = "DD_trap_spikes";
            public const string TrapBear = "DD_trap_bear";

            // --- предметы уровней (Models/Items, по 4 на уровень, ответ 11а) ---
            public const string ItGlowMushroom = "IT_glow_mushroom";
            public const string ItDuctTape = "IT_duct_tape";
            public const string ItLampPortable = "IT_lamp_portable";
            public const string ItRespirator = "IT_respirator";
            public const string ItDivingMask = "IT_diving_mask";
            public const string ItChlorine = "IT_chlorine";
            public const string ItFlippers = "IT_flippers";
            public const string ItOxygenTank = "IT_oxygen_tank";
            public const string ItRubberGloves = "IT_rubber_gloves";
            public const string ItFuseHi = "IT_fuse_hi";
            public const string ItBootsRubber = "IT_boots_rubber";
            public const string ItWrenchInsulated = "IT_wrench_insulated";

            // --- оружие и взрывчатка (Models/Weapons) ---
            public const string WRifleAk = "W_rifle_ak";
            public const string WRifleM4 = "W_rifle_m4";
            public const string WRifleBolt = "W_rifle_bolt";
            public const string WSmgMp5 = "W_smg_mp5";
            public const string WShotgunPump = "W_shotgun_pump";
            public const string WLmgM249 = "W_lmg_m249";
            public const string WRocketLauncher = "W_rocket_launcher";
            public const string ExGrenadeF1 = "EX_grenade_f1";
            public const string ExExplosiveTimed = "EX_explosive_timed";

            // --- стройка ---
            public const string BDFoundation = "BD_foundation";
            public const string BDWall = "BD_wall";
            public const string BDFloor = "BD_floor";
            public const string BDDoorway = "BD_doorway";
            public const string BDWindow = "BD_window";
            public const string BDStairs = "BD_stairs";
            public const string BDDoorMetal = "BD_door_metal";
            public const string BDDoorArmored = "BD_door_armored";
            // новые элементы стройки (ответ 9в) + «кубовые» модели всем 6 (ответ 10а)
            public const string BDFoundationTri = "BD_foundation_tri";
            public const string BDFloorTri = "BD_floor_tri";
            public const string BDRoof = "BD_roof";
            public const string BDRamp = "BD_ramp";
            public const string BDRampCorner = "BD_ramp_corner";
            public const string BDHighWall = "BD_high_wall";
            public const string BDPillar = "BD_pillar";
            public const string BDRailing = "BD_railing";
            public const string BDShutters = "BD_shutters";
        }

        static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>(64);
        static readonly HashSet<string> _missing = new HashSet<string>();

        /// <summary>Сколько моделей реально нашлось (печатается в консоль загрузки).</summary>
        public static int Found { get; private set; }
        /// <summary>Сколько разных имён спросили.</summary>
        public static int Requested { get; private set; }

        // ================== ЗАГРУЗКА ==================

        /// <summary>Префаб модели или null, если его нет (тогда вызывающий рисует заглушку).</summary>
        public static GameObject Get(string modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return null;
            if (_cache.TryGetValue(modelName, out var cached)) return cached;
            if (_missing.Contains(modelName)) return null;

            Requested++;
            var prefab = Resources.Load<GameObject>(Root + modelName);
            if (prefab == null)
            {
                _missing.Add(modelName);       // второй раз не ищем — не спамим диск
                return null;
            }
            _cache[modelName] = prefab;
            Found++;
            return prefab;
        }

        public static bool Has(string modelName) => Get(modelName) != null;

        /// <summary>Строка для терминала загрузки: «models 14 / 41».</summary>
        public static string Stats() => $"models {Found} / {Requested}";

        /// <summary>
        /// Кладёт визуал модели под <paramref name="parent"/> и возвращает его (или null).
        /// Ничего не ломает, если модели нет.
        /// </summary>
        public static GameObject Attach(string modelName, Transform parent, float yaw = 0f)
        {
            var prefab = Get(modelName);
            if (prefab == null || parent == null) return null;

            var go = Object.Instantiate(prefab, parent);
            go.name = modelName + "_visual";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one;
            MakeVisualOnly(go, parent.gameObject.layer);
            return go;
        }

        /// <summary>Модель, сразу вписанная в габариты (высота + низ на полу).</summary>
        public static GameObject AttachFitted(string modelName, Transform parent, float targetHeight,
                                              float yaw = 0f, float groundY = 0f, bool centerXZ = true)
        {
            var go = Attach(modelName, parent, 0f);
            if (go == null) return null;
            FitTo(go, parent, targetHeight, yaw, groundY, centerXZ);
            return go;
        }

        // ================== ГЕОМЕТРИЯ ==================

        /// <summary>World-bounds всех рендереров объекта (включая детей).</summary>
        public static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one * 0.01f);

            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>
        /// Подгонка визуала: масштаб по высоте, разворот, центрирование по XZ и посадка
        /// низа на <paramref name="groundY"/> относительно родителя.
        /// </summary>
        public static void FitTo(GameObject visual, Transform parent, float targetHeight,
                                 float yaw = 0f, float groundY = 0f, bool centerXZ = true)
        {
            if (visual == null || parent == null) return;

            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            visual.transform.localScale = Vector3.one;

            var b = BoundsOf(visual);
            if (targetHeight > 0.001f && b.size.y > 0.0001f)
            {
                float k = targetHeight / b.size.y;
                visual.transform.localScale *= k;
                b = BoundsOf(visual);
            }

            var origin = parent.position;              // localPosition = 0 → визуал ровно в родителе
            var delta = new Vector3(
                centerXZ ? origin.x - b.center.x : 0f,
                (origin.y + groundY) - b.min.y,
                centerXZ ? origin.z - b.center.z : 0f);
            visual.transform.position += delta;
        }

        /// <summary>Визуал не должен ловить рейкасты: убираем коллайдеры и ставим слой родителя.</summary>
        public static void MakeVisualOnly(GameObject visual, int layer)
        {
            if (visual == null) return;
            var colliders = visual.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) Object.Destroy(colliders[i]);

            var transforms = visual.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layer;

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = ShadowCastingMode.On;
                renderers[i].receiveShadows = true;
            }
        }

        // ================== ТИНТ (тиры постройки, скины, погода) ==================

        static readonly int[] TintIds =
        {
            Shader.PropertyToID("_BaseColor"),      // HDRP / URP
            Shader.PropertyToID("_Color"),           // Standard
            Shader.PropertyToID("_UnlitColor")       // как в BuildVisual
        };

        static MaterialPropertyBlock _mpb;

        /// <summary>Перекрашивает модель, не создавая новых материалов (тир/скин/подсветка).</summary>
        public static void Tint(GameObject root, Color color, float emission = 0f)
        {
            if (root == null) return;
            _mpb = _mpb ?? new MaterialPropertyBlock();
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                renderers[r].GetPropertyBlock(_mpb);
                for (int i = 0; i < TintIds.Length; i++) _mpb.SetColor(TintIds[i], color);
                if (emission > 0f) _mpb.SetColor(Shader.PropertyToID("_EmissiveColor"), color * emission);
                renderers[r].SetPropertyBlock(_mpb);
            }
        }

        /// <summary>Стандартный цвет тира постройки (Twig → Armored).</summary>
        public static Color TierColor(BuildTier tier)
        {
            switch (tier)
            {
                case BuildTier.Twig: return new Color(0.55f, 0.44f, 0.26f);
                case BuildTier.Wood: return new Color(0.66f, 0.47f, 0.25f);
                case BuildTier.Stone: return new Color(0.52f, 0.52f, 0.50f);
                case BuildTier.Metal: return new Color(0.38f, 0.41f, 0.44f);
                default: return new Color(0.24f, 0.26f, 0.29f);      // Armored (HQM)
            }
        }

        // ================== СООТВЕТСТВИЕ «ИГРА → МОДЕЛЬ» ==================

        public static string ForMonster(MonsterKind kind)
        {
            switch (kind)
            {
                case MonsterKind.Smiler: return Names.Smiler;
                case MonsterKind.Hound: return Names.Hound;
                case MonsterKind.Partygoer: return Names.Partygoer;
                case MonsterKind.SkinStealer: return Names.SkinStealer;
                case MonsterKind.Whisperer: return Names.Whisperer;
                case MonsterKind.Drowned: return Names.Drowned;
                case MonsterKind.Spark: return Names.Spark;
                // 29_boss: Bacteria убрана из игры — модель осталась архивной (MN_bacteria)
                default: return null;
            }
        }

        /// <summary>Модель под вид лут-контейнера (если для него есть FBX).</summary>
        public static string ForLootKind(LootContainerKind kind)
        {
            switch (kind)
            {
                case LootContainerKind.WoodenCrate: return Names.CrateWood;
                case LootContainerKind.SupplyCrate: return Names.SupplyCrate;   // PR_supply_crate
                case LootContainerKind.FilingCabinet: return Names.FilingCabinet;
                case LootContainerKind.Locker: return Names.Locker;
                case LootContainerKind.Toolbox: return Names.Toolbox;           // PR_toolbox
                case LootContainerKind.ElectricalPanel: return Names.ElectricalPanel;
                case LootContainerKind.MedicalCabinet: return Names.MedicalCabinet; // PR_medical_cabinet
                case LootContainerKind.SafeBox: return Names.SafeBox;           // PR_safe_box (Tier3)
                case LootContainerKind.Barrel: return Names.Barrel;             // PR_barrel (радиоактивная — см. ForBarrel)
                case LootContainerKind.Airdrop: return Names.AirdropCrate;      // PR_airdrop_crate
                case LootContainerKind.Corpse: return Names.LootBag;            // мешок с лутом после смерти
                default: return null;
            }
        }

        /// <summary>Бочка: рядом с радиацией — жёлтая (PR_barrel_radioactive), иначе ржавая (PR_barrel).</summary>
        public static string ForBarrel(bool radioactive)
            => radioactive ? Names.BarrelRadioactive : Names.Barrel;

        /// <summary>Модель для элемента постройки. Дверей в наборе пока нет — вернётся null.</summary>
        public static string ForBuildPiece(BuildPieceType piece)
        {
            switch (piece)
            {
                case BuildPieceType.Foundation: return Names.BDFoundation;
                case BuildPieceType.FoundationTriangle: return Pick(Names.BDFoundationTri, Names.BDFoundation);
                case BuildPieceType.Wall: return Names.BDWall;
                case BuildPieceType.HighWall: return Pick(Names.BDHighWall, Names.BDWall);
                case BuildPieceType.Floor: return Names.BDFloor;
                case BuildPieceType.FloorTriangle: return Pick(Names.BDFloorTri, Names.BDFloor);
                case BuildPieceType.Roof: return Pick(Names.BDRoof, Names.BDFloor);
                case BuildPieceType.Doorway: return Names.BDDoorway;
                case BuildPieceType.Window: return Names.BDWindow;
                case BuildPieceType.Stairs: return Names.BDStairs;
                case BuildPieceType.Ramp: return Pick(Names.BDRamp, Names.BDStairs);
                case BuildPieceType.RampCorner: return Pick(Names.BDRampCorner, Names.BDStairs);
                case BuildPieceType.Pillar: return Pick(Names.BDPillar, Names.BDWall);
                case BuildPieceType.Railing: return Pick(Names.BDRailing, Names.BDWindow);
                case BuildPieceType.Shutters: return Pick(Names.BDShutters, Names.BDWindow);
                default: return null;
            }
        }

        /// <summary>
        /// Модель под предмет-деплой (ящик, печь, верстак, шкаф, кровать…).
        /// Пока в наборе есть только ящик/вендинг/транспорт — остальное рисуется
        /// серым макетом (куб-прокси), пока не собран пак DD_* (models_deployables.py).
        /// </summary>
        public static string ForItem(string itemId) => ForDeployable(itemId);

        /// <summary>
        /// Тег уровня для палитр (ответ 5в): L0 — коридоры, L37 — бассейны, L3 — станция.
        /// Пустая строка, если точка вне уровней (меню, лифт, тестовый полигон).
        /// </summary>
        public static string LevelTag(LevelTheme theme)
        {
            switch (theme)
            {
                case LevelTheme.Corridors: return "L0";
                case LevelTheme.Poolrooms: return "L37";
                case LevelTheme.PowerStation: return "L3";
                default: return "";
            }
        }

        /// <summary>Тег уровня по позиции в мире.</summary>
        public static string LevelTagAt(Vector3 pos)
        {
            var zone = SingularitySafeZoneAt(pos);
            return zone == null ? "" : LevelTag(zone.theme);
        }

        static LevelZone SingularitySafeZoneAt(Vector3 pos)
        {
            var z = LevelZone.At(pos);
            return z;                       // отдельный метод, чтобы не тянуть остальные системы
        }

        /// <summary>
        /// Модель деплоя с учётом уровня (палитры 5в): если у модели есть вариант _L0/_L37/_L3 —
        /// берём его, иначе обычную. Так верстак 1 уровня в коридорах и на станции выглядит по-своему.
        /// </summary>
        public static string ForDeployableAt(string itemId, Vector3 pos)
        {
            string baseName = ForDeployable(itemId);
            if (string.IsNullOrEmpty(baseName)) return baseName;
            string tag = LevelTagAt(pos);
            if (string.IsNullOrEmpty(tag)) return baseName;
            string themed = baseName + "_" + tag;
            return Has(themed) ? themed : baseName;
        }

        /// <summary>Первое имя, для которого реально есть префаб (страховка при частичной сборке).</summary>
        static string Pick(params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
                if (!string.IsNullOrEmpty(candidates[i]) && Has(candidates[i])) return candidates[i];
            return candidates.Length > 0 ? candidates[candidates.Length - 1] : null;
        }

        /// <summary>Модель для деплоя по itemId (см. DeployableFactory.SpecOf).</summary>
        public static string ForDeployable(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            switch (itemId)
            {
                case "woodbox":
                case "box.wooden.large": return Names.CrateWood;      // деревянный ящик (Rust: малый 6 / большой 30)
                case "furnace": return Names.Furnace;
                case "furnace.large": return Names.FurnaceLarge;
                // у каждого тира свой верстак (v0.4); если модели нет — падаем на общую DD_workbench
                case "workbench1": return Pick(Names.Workbench1, Names.Workbench);
                case "workbench2": return Pick(Names.Workbench2, Names.Workbench);
                case "workbench3": return Pick(Names.Workbench3, Names.Workbench);
                case "cupboard.tool": return Names.Cupboard;
                case "sleepingbag": return Names.SleepingBag;
                case "bed": return Names.Bed;
                case "water.purifier": return Names.Purifier;
                case "autoturret": case "flameturret": return Names.AutoTurret;
                case "samsite": return Names.SamSite;
                case "repair.bench": return Names.RepairBench;
                case "research.table": return Names.ResearchTable;
                case "generator.wind.scrap": return Names.WindGenerator;
                case "vending.machine": return Names.Vending;
                case "cart.loot": return Names.LootCart;
                case "scooter": return Names.Scooter;
                case "door.hinged.wood": return Names.DoorWood;
                case "door.hinged.metal": case "door.double.hinged.metal": return Names.DoorMetal;
                case "door.hinged.toptier": return Names.DoorArmored;
                case "lock.key": return Names.LockKey;
                case "lock.code": return Names.LockCode;
                case "sign.pictureframe": return Names.Sign;
                case "barricade.concrete": return Names.BarricadeConcrete;
                case "barricade.metal": case "wall.frame.cell.gate": return Names.BarricadeMetal;
                case "trap.spikes": return Names.TrapSpikes;
                case "trap.bear": return Names.TrapBear;
                default: return null;
            }
        }

        /// <summary>
        /// Модель «своего» предмета уровня для дропа/витрины (ответ 11а).
        /// null — у предмета нет отдельной модели (рисуется иконка/куб).
        /// </summary>
        public static string ForLevelItem(string itemId)
        {
            switch (itemId)
            {
                case "glow.mushroom": return Names.ItGlowMushroom;
                case "duct.tape": return Names.ItDuctTape;
                case "lamp.portable": return Names.ItLampPortable;
                case "respirator": return Names.ItRespirator;
                case "diving.mask": return Names.ItDivingMask;
                case "chlorine": return Names.ItChlorine;
                case "flippers": return Names.ItFlippers;
                case "oxygen.tank": return Names.ItOxygenTank;
                case "rubber.gloves": return Names.ItRubberGloves;
                case "fuse.hi": return Names.ItFuseHi;
                case "boots.rubber": return Names.ItBootsRubber;
                case "wrench.insulated": return Names.ItWrenchInsulated;
                default: return null;
            }
        }

        /// <summary>
        /// Модель предмета, который лежит в мире: дроп из рюкзака, лут из контейнера на полу.
        /// Сначала «свои» предметы уровней (IT_*), потом оружие и взрывчатка (W_*/EX_*).
        /// null — модель не заведена, предмет останется кубом (как было раньше всем).
        /// </summary>
        public static string ForWorldItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            string own = ForLevelItem(itemId);
            if (!string.IsNullOrEmpty(own) && Has(own)) return own;
            // модели лут-предметов (alpha 1.0): еда, медицина, патроны, броня, ресурсы, инструменты
            string loot = ForLootItem(itemId);
            if (!string.IsNullOrEmpty(loot) && Has(loot)) return loot;
            switch (itemId)
            {
                // стволы: силуэт «по семейству» — лучше, чем серый куб на полу
                case "rifle.ak": return Names.WRifleAk;
                case "rifle.m16":
                case "rifle.lr300":
                case "rifle.semiauto": return Names.WRifleM4;
                case "rifle.bolt": return Names.WRifleBolt;
                case "smg.mp5":
                case "smg.thompson":
                case "smg.custom":
                case "smg.vector": return Names.WSmgMp5;
                case "shotgun.pump":
                case "shotgun.double":
                case "shotgun.spas":
                case "shotgun.waterpipe": return NamesWShotgun();
                case "lmg.m249": return Names.WLmgM249;
                case "rocket.launcher": return Names.WRocketLauncher;
                // взрывчатка
                case "grenade.f1": return Names.ExGrenadeF1;
                case "explosive.timed":
                case "explosive.satchel": return Names.ExExplosiveTimed;
                default: return null;
            }
        }

        static string NamesWShotgun() => Names.WShotgunPump;

        /// <summary>
        /// Модель «бытового» предмета (Models/Items/IT_*): еда, вода, медицина, патроны,
        /// броня из дерева/кожи, ресурсы, инструменты. null — модели нет, будет куб.
        /// </summary>
        public static string ForLootItem(string itemId)
        {
            switch (itemId)
            {
                // еда и вода
                case "can.beans": return "IT_can_beans";
                case "can.tuna": return "IT_can_tuna";
                case "water.bottle": return "IT_water_bottle";
                case "water.dirty": return "IT_water_bottle";
                case "apple": return "IT_apple";
                case "mushroom": return "IT_glow_mushroom";
                case "chocolate": return "IT_chocolate";
                case "meat.raw": return "IT_meat_raw";
                case "bearmeat.cooked": return "IT_meat_raw";
                case "fish.minnows": return "IT_meat_raw";
                // медицина
                case "largemedkit": return "IT_medkit_large";
                case "bandage": return "IT_bandage";
                case "syringe.medical": return "IT_antidote";
                case "antidote": return "IT_antidote";
                // патроны и стрелы
                case "ammo.rifle": case "ammo.rifle.hv": case "ammo.rifle.incendiary":
                case "ammo.rifle.explosive": case "ammo.pistol": case "ammo.smg":
                case "ammo.rifle.hv.ap": case "ammo.pistol.hv":
                    return "IT_ammo_556";
                case "ammo.shotgun": case "ammo.shotgun.slug": case "ammo.handmade.shell":
                    return "IT_ammo_shell";
                case "arrow.wooden": case "arrow.bone": case "arrow.fire":
                    return "IT_arrow_bundle";
                // броня из дерева/кожи
                case "wood.armor.helmet": return "IT_wood_helmet";
                case "wood.armor.jacket": return "IT_wood_chestplate";
                case "attire.hide.helterneck": case "attire.hide.vest": case "attire.hide.pants":
                    return "IT_hide_vest";
                case "attire.hide.boots": case "shoes.boots": return "IT_boots_hide";
                // ресурсы
                case "scrap": return "IT_scrap_pile";
                case "metal.fragments": return "IT_scrap_pile";
                case "wood": return "IT_wood_pile";
                case "stones": case "metal.ore": case "sulfur.ore":
                    return itemId == "stones" ? "IT_stone_pile" : "IT_sulfur_lump";
                case "sulfur": return "IT_sulfur_lump";
                case "gunpowder": case "charcoal": return "IT_sulfur_lump";
                case "cloth": case "leather": case "rope": case "sewingkit": return "IT_cloth_roll";
                // инструменты
                case "bucket.water": case "bucket": return "IT_bucket";
                case "flashlight": return "IT_flashlight";
                case "tool.geiger": return "IT_geiger";
                case "torch.lantern": case "lantern": return "IT_torch_lantern";
                default: return null;
            }
        }

        /// <summary>Модель под транспорт.</summary>
        public static string ForTransport(string kind)
        {
            switch (kind)
            {
                case "scooter": return Names.Scooter;
                case "cart": return Names.LootCart;
                case "minecart": return Names.Minecart;
                default: return null;
            }
        }

        /// <summary>Сброс кэша (например, после смены папки Resources в редакторе).</summary>
        public static void Clear()
        {
            _cache.Clear();
            _missing.Clear();
            Found = 0;
            Requested = 0;
        }
    }
}
