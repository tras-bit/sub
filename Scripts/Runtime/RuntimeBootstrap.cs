// ============================================================================
//  SUBSISTENCE — Runtime/RuntimeBootstrap.cs
//  Один компонент, который поднимает всю игру: материалы (HDRP → fallback Standard),
//  мир (3 уровня по сиду), игрока, UI, монстров, лут, сеть.
//  Повесь на пустой объект в пустой сцене — и можно играть без настройки сцены.
//  Порядок вывода в консоль — тот самый, «loading … starts … ok».
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Net;
using Subsistence.World;
using Subsistence.AI;
using Subsistence.UI;
using Subsistence.Player;

namespace Subsistence.Runtime
{
    public class RuntimeBootstrap : MonoBehaviour
    {
        [Header("Мир")]
        public int seed = 1337;

        /// <summary>Сид текущего мира — нужен сохранению (мир уровня восстанавливается тем же).</summary>
        public static int SeedValue { get; private set; } = 1337;
        public bool readSeedFromFile = true;
        public Vector3 playerSpawn = new Vector3(6f, 0.6f, 6f);
        public bool generateLoot = true;
        public bool generateMonsters = true;

        [Header("Игрок")]
        public bool spawnPlayer = true;
        public float mouseSensitivity = 2.2f;
        public float fieldOfView = 90f;

        [Header("Отладка")]
        public bool autoPlay = false;              // в редакторе удобно: сразу загрузка (минуя ввод)

        public static RuntimeBootstrap Instance { get; private set; }

        public PlayerController Player { get; private set; }
        public LevelGenerator Generator { get; private set; }

        Material _wallpaper, _carpet, _ceiling, _tile, _water, _concrete, _metal, _lamp, _reactor;

        void Awake()
        {
            Instance = this;
            // 04_perf: цель 120 FPS / RTX 3060 (решение из docs/ANSWERS.md)
            Application.targetFrameRate = Balance.TargetFps;
            QualitySettings.vSyncCount = 0;
            CreateMaterials();
            SetupUrpLikeAmbience();
        }

        void Start()
        {
            if (readSeedFromFile) seed = ReadSeed();
            SeedValue = seed;
            NetworkBridge.UseLocalHost();     // офлайн-хост; Mirror подменит транспорт при подключении

            // Аргументы командной строки: headless-сервер, клиент, стенд нагрузки 100+ (docs/MULTIPLAYER.md)
            CmdLine.Parse(System.Environment.GetCommandLineArgs());
            if (CmdLine.Server || CmdLine.LoadBots > 0) autoPlay = true;

            // Терминальная консоль = главное меню (стиль BACKROOMS OPERATING SYSTEM):
            // шапка, боковое меню [1] ИГРАТЬ … [0] ВЫХОД, лог с логотипом, строка ввода.
            var boot = gameObject.AddComponent<BootConsole>();
            boot.autoPlayOnBoot = autoPlay;
            boot.OnPlayRequested += () => StartCoroutine(BootSequence(boot));

            if (CmdLine.HasAny) boot.Print($"режим запуска: <color=#b6ffd0>{CmdLine.Describe()}</color>");
            if (CmdLine.LoadBots > 0)
            {
                var load = gameObject.AddComponent<Subsistence.Net.NetLoadTest>();
                load.bots = CmdLine.LoadBots;
                boot.Print($"стенд нагрузки: <color=#b6ffd0>{CmdLine.LoadBots}</color> ботов — отчёт в консоль каждые 10 с (тег [netload])");
            }
            boot.Print("<color=#2f8c53>(c) subsistence project — unity 2022.3.62f2 / HDRP 14.0.12 / mirror-ready</color>");
            boot.Print("набери <color=#b6ffd0>HELP</color> для списка команд или жми номера пунктов слева.");
        }

        int ReadSeed()
        {
            try
            {
                string p = System.IO.Path.Combine(Application.streamingAssetsPath, "seed.txt");
                if (System.IO.File.Exists(p))
                {
                    var text = System.IO.File.ReadAllText(p).Trim();
                    if (int.TryParse(text, out int s)) return s;
                }
            }
            catch { /* офлайн-сборка без StreamingAssets — не критично */ }
            return seed;
        }

        /// <summary>Полная последовательность загрузки — то, что видно в зелёной консоли.</summary>
        IEnumerator BootSequence(BootConsole boot)
        {
            boot.Show();
            boot.PrintBoot("BOOT", "subsistence core v1.0.0-alpha");
            yield return null;

            boot.Print($"seed = <color=#b6ffd0>{seed}</color>");
            if (Subsistence.Core.SaveSystem.HasAny)
            {
                boot.Print("<color=#ffd76a>найдено сохранение мира:</color> " +
                           Subsistence.Core.SaveSystem.Info(Subsistence.Core.SaveSystem.AutoSlot) + " · " +
                           Subsistence.Core.SaveSystem.Info(Subsistence.Core.SaveSystem.ManualSlot));
                boot.Print("продолжить — <color=#b6ffd0>F9</color> в игре · новая жизнь — просто играй дальше (автосейв перезапишется)");
            }
            boot.Print("loading level0_corridors ... starts");
            yield return null;
            var levels = BuildWorld();
            boot.PrintOk("level0_corridors generated");

            boot.Print("loading level1_poolrooms ... starts");
            yield return null;
            boot.PrintOk("level1_poolrooms generated");

            boot.Print("loading level2_powerstation ... starts");
            yield return null;
            boot.PrintOk("level2_powerstation generated");

            boot.Print($"loot tables: tier1={CountTable(LootTier.Tier1)} tier2={CountTable(LootTier.Tier2)} tier3={CountTable(LootTier.Tier3)}");
            if (generateLoot)
            {
                boot.Print("populating loot nodes ... starts");
                yield return null;
                PopulateLoot(levels);
                boot.PrintOk("loot populated");
            }

            if (generateMonsters)
            {
                boot.Print("spawning monsters ... starts");
                yield return null;
                SpawnMonsters(levels);
                boot.PrintOk("monsters spawned");
            }

            boot.Print("building nav graph ... starts");
            yield return null;
            boot.PrintOk($"nav graph nodes={CountNodes(levels)}");

            if (generateLoot || generateMonsters)
            {
                boot.Print("transport + traders ... starts");
                yield return null;
                Subsistence.Player.TransportSpawner.Populate(levels);   // `Player` — свойство-инстанс, не namespace
                AI.TradeStall.Populate(levels);
                boot.PrintOk("transport + traders ready");
            }

            boot.Print("decay + admin systems ... starts");
            yield return null;
            gameObject.AddComponent<Subsistence.Building.DecayDirector>();   // 22_decay: гниение без шкафа
            // Сохранение мира: F5 — сохранить, F9 — загрузить, F10 — удалить, автосейв раз в 5 минут.
            // Сохранение есть — консоль скажет об этом; мир восстанавливается тем же сидом.
            gameObject.AddComponent<Subsistence.Core.SaveDirector>();
            gameObject.AddComponent<Subsistence.Crafting.WorkbenchSystem>(); // 0.2: радиусы верстаков 1-3, скорость крафта
            gameObject.AddComponent<Subsistence.Audio.AudioDirector>();       // 0.3: процедурный звук (файлов нет)
            gameObject.AddComponent<Subsistence.Net.AdminSystem>();          // 36_anticheat: админы
            boot.PrintOk("decay + admin ready");

            // 18_weather + 19_airdrops: аномалии дважды в сутки и «птица» с грузом
            boot.Print("weather + airdrops ... starts");
            yield return null;
            var weather = gameObject.AddComponent<World.WeatherSystem>();
            weather.seed = seed;
            weather.logLine = msg => boot.Print($"<color=#2f8c53>[SYS]</color> {msg}");
            var airdrops = gameObject.AddComponent<World.AirdropSystem>();
            airdrops.seed = seed;
            airdrops.logLine = weather.logLine;
            boot.PrintOk($"weather ready (2 events/day) · airdrop in {(int)airdrops.firstDropIn}s");

            // 16_doors / 30–31: сетевой путь дверей и замков (офлайн — LocalHost, в сети — Mirror)
            Subsistence.Net.DeployNet.Init();
            Subsistence.Net.SpawnNet.Init();
            Subsistence.Net.InventoryNet.Init();                 // 30–31: зеркало инвентарей + лут
            Subsistence.Net.CombatNet.Init();                    // 30–31: урон по игрокам — на сервере
            Subsistence.Net.SnapshotClient.Init();               // 100+: приём снапшотов и статистика трафика
            boot.PrintOk("doors + locks ready (код и ключ проверяет сервер)");
            boot.PrintOk("spawn replication ready (деплои + стройка, всё делает сервер)");
            boot.PrintOk("inventory + loot replication ready (предметы и ящики — на сервере)");
            boot.PrintOk("combat authority ready (урон по игрокам считает сервер)");

            if (spawnPlayer)
            {
                boot.Print("creating player ... starts");
                yield return null;
                CreatePlayer(levels);
                boot.PrintOk("player ready");
            }

            boot.Print("hud + inventory ... starts");
            yield return null;
            CreateUI();
            boot.PrintOk("ui ready");

            // Модели Blender: если префабы собраны (меню Subsistence → 7), они уже в мире
            boot.Print($"<color=#2f8c53>[MODELS] {World.ModelLibrary.Stats()} — префабы лежат в Assets/Subsistence/Resources/Models</color>");
            boot.Print($"<color=#2f8c53>[NET] transport={NetworkBridge.Host?.GetType().Name ?? "offline"} tick={(int)Balance.TickRate}Hz snapshot={Balance.SnapshotRate}Hz aoi=32m</color>");
            boot.Print("server-authoritative: damage/build/loot validated on server");
            boot.Print(" ");
            boot.Print("<color=#39ff6a>subsistence ready. good luck. level 0 has no exit signs.</color>");
            yield return new WaitForSeconds(1.2f);

            boot.Hide();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            UIState.AnyMenuOpen = false;
        }

        /// <summary>26_transport: самокаты, тележки для лута, вагонетки на станции.</summary>
        static void TransportSpawnerPopulate(List<GeneratedLevel> levels)
            => Subsistence.Player.TransportSpawner.Populate(levels);

        /// <summary>Имя уровня для сообщений интерфейса и лифтов.</summary>
        public static string NameOf(LevelTheme theme)
        {
            switch (theme)
            {
                case LevelTheme.Corridors: return "Level 0 — Жёлтые коридоры";
                case LevelTheme.Poolrooms: return "Level 37 — Бассейны";
                default: return "Level 3 — Электростанция";
            }
        }

        /// <summary>Куда приезжает лифт на уровне (первая точка спавна игроков).</summary>
        public Vector3 ArrivalPointFor(LevelTheme theme)
        {
            if (Generator != null && Generator.Generated != null)
            {
                var levels = Generator.Generated;
                for (int i = 0; i < levels.Count; i++)
                    if (levels[i].theme == theme && levels[i].playerSpawns.Count > 0)
                        return levels[i].playerSpawns[0];
            }
            return playerSpawn + (theme == LevelTheme.Corridors ? Vector3.zero
                : theme == LevelTheme.Poolrooms ? new Vector3(0, -40f, 0) : new Vector3(0, -80f, 0));
        }

        /// <summary>Перенос игрока на другой уровень (лифты, админ-телепорт, респавн по кровати).</summary>
        public void TeleportPlayerTo(LevelTheme theme, Vector3 where)
        {
            if (Player == null) return;
            var cc = Player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            Player.transform.position = where + Vector3.up * 0.2f;
            if (cc != null) cc.enabled = true;
            Subsistence.UI.HudRuntime.ShowToast($"Прибытие: {NameOf(theme)}");
        }

        int CountNodes(List<GeneratedLevel> levels)
        {
            int n = 0;
            for (int i = 0; i < levels.Count; i++) n += levels[i].pathfinder.Nodes.Count;
            return n;
        }

        static int CountTable(LootTier tier)
        {
            int n = 0;
            foreach (var t in LootTables.All) if (t.tier == tier) n++;
            return n;
        }

        // ================== МИР ==================
        List<GeneratedLevel> BuildWorld()
        {
            var genGo = new GameObject("LevelGenerator");
            Generator = genGo.AddComponent<LevelGenerator>();
            AssignMaterials(Generator);
            Generator.levels = new[]
            {
                // 08_level_size: 1×1 км → сетка 168 клеток × 6 м ≈ 1008 м на уровень
                new LevelGenSettings { theme = LevelTheme.Corridors,    seed = seed,       gridWidth = 168, gridDepth = 168, originY = 0f,    roomCount = 90, monsterCount = 55, lootNodeCount = 260, exitCount = 4 },
                new LevelGenSettings { theme = LevelTheme.Poolrooms,    seed = seed + 101, gridWidth = 168, gridDepth = 168, originY = -40f,  roomCount = 80, monsterCount = 50, lootNodeCount = 230, exitCount = 4 },
                new LevelGenSettings { theme = LevelTheme.PowerStation, seed = seed + 202, gridWidth = 168, gridDepth = 168, originY = -80f,  roomCount = 70, monsterCount = 45, lootNodeCount = 200, exitCount = 4 },
            };
            Generator.buildOnStart = false;
            Generator.GenerateAll();
            return Generator.Generated;
        }

        void PopulateLoot(List<GeneratedLevel> levels)
        {
            var spawnerGo = new GameObject("LootSpawner");
            var spawner = spawnerGo.AddComponent<World.LootSpawner>();
            spawner.generator = Generator;
            spawner.PopulateAll(levels);

            // Мирные ресурсы: дерево/камень/металл (добывается киркой/топором)
            for (int i = 0; i < levels.Count; i++)
            {
                var lvl = levels[i];
                for (int k = 0; k < lvl.lootNodes.Count; k += 4)
                {
                    var p = lvl.lootNodes[k];
                    if (i == 0) WorldDeposits.SpawnDeposit(p, "wood", 200f, 8, lvl.theme);
                    else if (i == 1) WorldDeposits.SpawnDeposit(p, "stones", 200f, 10, lvl.theme);
                    else WorldDeposits.SpawnDeposit(p, "metal.ore", 150f, 10, lvl.theme);
                }
            }
        }

        void SpawnMonsters(List<GeneratedLevel> levels)
        {
            var spawnerGo = new GameObject("MonsterSpawner");
            var spawner = spawnerGo.AddComponent<MonsterSpawner>();

            var waves = new List<MonsterSpawner.Wave>();
            // ===== У КАЖДОГО УРОВНЯ СВОЙ НАБОР МОНСТРОВ (v4) =====
            // L0 «Жёлтые коридоры»: Smiler — лицо уровня, Whisperer — охотник на свет
            for (int i = 0; i < 8; i++) waves.Add(new MonsterSpawner.Wave { level = LevelTheme.Corridors, kind = MonsterKind.Smiler, baseCount = 5, perPlayer = 1, spawnInterval = 40f });
            for (int i = 0; i < 4; i++) waves.Add(new MonsterSpawner.Wave { level = LevelTheme.Corridors, kind = MonsterKind.Whisperer, baseCount = 2, perPlayer = 1, spawnInterval = 90f });
            // L37 «Бассейны»: Hound по суше, Drowned из воды, Partygoer — стаями
            for (int i = 0; i < 8; i++) waves.Add(new MonsterSpawner.Wave { level = LevelTheme.Poolrooms, kind = MonsterKind.Hound, baseCount = 6, perPlayer = 1, spawnInterval = 35f });
            for (int i = 0; i < 5; i++) waves.Add(new MonsterSpawner.Wave { level = LevelTheme.Poolrooms, kind = MonsterKind.Drowned, baseCount = 3, perPlayer = 1, spawnInterval = 55f });
            for (int i = 0; i < 4; i++) waves.Add(new MonsterSpawner.Wave { level = LevelTheme.Poolrooms, kind = MonsterKind.Partygoer, baseCount = 3, perPlayer = 1, spawnInterval = 70f });
            // L3 «Электростанция»: SkinStealer в машинных залах, Spark — быстрый и электрический
            for (int i = 0; i < 8; i++) waves.Add(new MonsterSpawner.Wave { level = LevelTheme.PowerStation, kind = MonsterKind.SkinStealer, baseCount = 5, perPlayer = 1, spawnInterval = 45f });
            for (int i = 0; i < 5; i++) waves.Add(new MonsterSpawner.Wave { level = LevelTheme.PowerStation, kind = MonsterKind.Spark, baseCount = 3, perPlayer = 1, spawnInterval = 60f });
            spawner.waves = waves.ToArray();

            var pts = new List<Transform>();
            for (int i = 0; i < levels.Count; i++)
                for (int k = 0; k < levels[i].monsterSpawns.Count; k++)
                {
                    var go = new GameObject($"Spawn_{i}_{k}");
                    go.transform.position = levels[i].monsterSpawns[k];
                    pts.Add(go.transform);
                }
            spawner.spawnPoints = pts.ToArray();

            // Первичный спавн, чтобы мир не был пустым
            for (int i = 0; i < levels.Count; i++)
            {
                var lvl = levels[i];
                int count = lvl.theme == LevelTheme.Corridors ? 6 : lvl.theme == LevelTheme.Poolrooms ? 8 : 7;
                // на каждом уровне минимум два своих вида: основной + «специалист»
                MonsterKind main = lvl.theme == LevelTheme.Corridors ? MonsterKind.Smiler
                                 : lvl.theme == LevelTheme.Poolrooms ? MonsterKind.Hound : MonsterKind.SkinStealer;
                MonsterKind extra = lvl.theme == LevelTheme.Corridors ? MonsterKind.Whisperer
                                 : lvl.theme == LevelTheme.Poolrooms ? MonsterKind.Drowned : MonsterKind.Spark;
                for (int k = 0; k < count && k < lvl.monsterSpawns.Count; k++)
                    spawner.Spawn(k % 3 == 2 ? extra : main, lvl.monsterSpawns[k]);   // каждый третий — специалист
            }
        }

        // ================== ИГРОК ==================
        void CreatePlayer(List<GeneratedLevel> levels)
        {
            var spawn = levels.Count > 0 && levels[0].playerSpawns.Count > 0 ? levels[0].playerSpawns[0] : playerSpawn;
            var go = new GameObject("Player") { layer = Layers.Player };
            go.transform.position = spawn;

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0); cc.slopeLimit = 50f; cc.stepOffset = 0.4f;

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(go.transform, false);
            pivot.transform.localPosition = new Vector3(0, 1.62f, 0);

            var camGo = new GameObject("ViewCamera", typeof(Camera), typeof(AudioListener));
            camGo.transform.SetParent(pivot.transform, false);
            var cam = camGo.GetComponent<Camera>();
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 300f;
            cam.tag = "MainCamera";

            var inv = new PlayerInventory();
            var survival = go.AddComponent<SurvivalSystem>();
            survival.Inventory = inv;
            survival.waterMask = Layers.Mask(Layers.Water);
            survival.radiationMask = 1 << 0;   // источники радиации — на дефолтном слое

            var armor = go.AddComponent<Combat.ArmorSystem>();
            armor.inventory = inv;
            survival.ArmorResistProvider = armor.Resist;

            var weapons = go.AddComponent<Combat.WeaponController>();
            weapons.inventory = inv;
            weapons.viewCamera = cam;
            weapons.hitMask = Layers.Mask(Layers.LevelGeometry, Layers.Player, Layers.Monster, Layers.Buildable, Layers.Deployable, Layers.Default);

            var controller = go.AddComponent<PlayerController>();
            controller.inventory = inv;
            controller.survival = survival;
            controller.cameraPivot = pivot.transform;
            controller.viewCamera = cam;
            controller.mouseSensitivity = mouseSensitivity;
            controller.groundMask = Layers.Mask(Layers.LevelGeometry, Layers.Buildable);

            var crafting = go.AddComponent<Subsistence.Crafting.CraftingSystem>();
            crafting.inventory = inv;

            var build = go.AddComponent<Subsistence.Building.BuildController>();
            build.inventory = inv;
            build.viewCamera = cam;
            build.library = BuildPrefabLibraryRuntime.Shared;

            // 21_deploy: постановка деплоев из инвентаря (ящик/печь/верстак/шкаф/кровать/турель…)
            var deployer = go.AddComponent<Subsistence.Building.DeployPlacer>();
            deployer.inventory = inv;
            deployer.viewCamera = cam;

            // 0.2: киянка — апгрейд/ремонт/снос с возвратом + мягкая сторона
            var hammer = go.AddComponent<Subsistence.Building.BuildInteraction>();
            hammer.inventory = inv;
            hammer.viewCamera = cam;

            go.AddComponent<Subsistence.Audio.PlayerFootsteps>();              // шаги по материалу уровня
            go.AddComponent<Subsistence.Audio.LevelAmbience>();                // гул ламп / трансформатор / вода

            go.AddComponent<PlayerEntity>();
            go.AddComponent<NetEntityMarker>().Assign(controller);

            Player = controller;
            GiveStartingKit(inv);
            Subsistence.Net.InventoryNet.HookPlayer(inv);        // сервер узнаёт, что у игрока в карманах
        }

        /// <summary>Стартовый набор: камень, факел, план — как первый респ в Rust.</summary>
        static void GiveStartingKit(PlayerInventory inv)
        {
            inv.TryAdd(new ItemStack("rock", 1));
            inv.TryAdd(new ItemStack("torch", 1));
            inv.TryAdd(new ItemStack("building.planner", 1));
            inv.TryAdd(new ItemStack("bandage", 3));
            inv.TryAdd(new ItemStack("can.beans", 2));
            inv.TryAdd(new ItemStack("water.bottle", 2));
            inv.TryAdd(new ItemStack("wood", 500));
            inv.TryAdd(new ItemStack("cloth", 60));
            inv.TryAdd(new ItemStack("metal.fragments", 200));
            inv.TryAdd(new ItemStack("stones", 300));
        }

        void CreateUI()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<HudRuntime>();
            hud.player = Player;
            hudGo.AddComponent<InventoryUI>();
            hudGo.AddComponent<Subsistence.Crafting.ResearchUI>();            // 0.4: дерево исследований
            hudGo.AddComponent<BuildPaletteUI>();
        }

        // ================== МАТЕРИАЛЫ (HDRP или Standard) ==================
        void CreateMaterials()
        {
            _wallpaper = MakeMat("M_Wallpaper_L0", new Color(0.72f, 0.66f, 0.36f), 0.72f, 0f);
            _carpet = MakeMat("M_Carpet_L0", new Color(0.62f, 0.57f, 0.34f), 0.9f, 0f);
            _ceiling = MakeMat("M_Ceiling_L0", new Color(0.78f, 0.76f, 0.62f), 0.65f, 0f);
            _tile = MakeMat("M_Tile_L1", new Color(0.82f, 0.86f, 0.85f), 0.25f, 0.05f);
            _water = MakeMat("M_Water_L1", new Color(0.15f, 0.55f, 0.62f, 0.75f), 0.05f, 0.35f, transparent: true);
            _concrete = MakeMat("M_Concrete_L2", new Color(0.36f, 0.36f, 0.38f), 0.8f, 0f);
            _metal = MakeMat("M_Metal_L2", new Color(0.45f, 0.47f, 0.5f), 0.35f, 0.7f);
            _reactor = MakeMat("M_Reactor", new Color(0.25f, 0.3f, 0.3f), 0.5f, 0.4f, emission: new Color(0.1f, 0.8f, 0.4f));
            _lamp = MakeMat("M_LampEmissive", new Color(1f, 0.98f, 0.85f), 0.4f, 0f, emission: new Color(1f, 1f, 0.9f) * 2.2f);
        }

        Material MakeMat(string name, Color color, float smoothness, float metallic,
                         Color? emission = null, bool transparent = false)
        {
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null) shader = Shader.Find("HDRP/LitTessellation");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            var m = new Material(shader) { name = name };
            SetIfExists(m, "_BaseColor", color);
            SetIfExists(m, "_Color", color);
            SetIfExists(m, "_Smoothness", smoothness);
            SetIfExists(m, "_Metallic", metallic);
            SetIfExists(m, "_MetallicRemapMin", metallic);
            SetIfExists(m, "_SmoothnessRemapMax", smoothness);
            if (emission.HasValue)
            {
                SetIfExists(m, "_EmissiveColor", emission.Value);
                SetIfExists(m, "_EmissionColor", emission.Value);
                m.EnableKeyword("_EMISSION");
                m.EnableKeyword("_EMISSIVE_COLOR");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (transparent)
            {
                SetIfExists(m, "_SurfaceType", 1f);
                SetIfExists(m, "_AlphaCutoffEnable", 0f);
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                m.SetOverrideTag("RenderType", "Transparent");
            }
            return m;
        }

        static void SetIfExists(Material m, string prop, float v) { if (m.HasProperty(prop)) m.SetFloat(prop, v); }
        static void SetIfExists(Material m, string prop, Color v) { if (m.HasProperty(prop)) m.SetColor(prop, v); }

        void AssignMaterials(LevelGenerator gen)
        {
            gen.wallpaperMat = _wallpaper; gen.carpetMat = _carpet; gen.ceilingMat = _ceiling;
            gen.tileMat = _tile; gen.poolWaterMat = _water; gen.concreteMat = _concrete;
            gen.metalMat = _metal; gen.reactorMat = _reactor; gen.emissiveLampMat = _lamp;
        }

        void SetupUrpLikeAmbience()
        {
            // Атмосфера теперь у каждого уровня своя — здесь только стартовая палитра L0
            // (World/LevelAtmosphere меняет свет и туман при переходах между уровнями).
            gameObject.AddComponent<World.LevelAtmosphere>();
            Subsistence.World.LevelAtmosphere.Palette.TryGetValue(LevelTheme.Corridors, out var startAtmo);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = startAtmo.ambientSky;
            RenderSettings.ambientEquatorColor = startAtmo.ambientEquator;
            RenderSettings.ambientGroundColor = startAtmo.ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = startAtmo.fogDensity;
            RenderSettings.fogColor = startAtmo.fogColor;
            QualitySettings.shadowDistance = 60f;
            QualitySettings.shadowCascades = 2;
        }
    }

    /// <summary>Метка сетевого объекта для контроллера игрока (чтобы не плодить компоненты).</summary>
    public class NetEntityMarker : MonoBehaviour
    {
        public NetEntity target;
        public void Assign(NetEntity e)
        {
            target = e;
            if (target != null && !target.Net.IsValid)
                target.AssignNetId(new NetId((uint)Random.Range(1, 1000000)));
        }
    }

    /// <summary>Простые «боксовые» префабы для постройки, когда FBX ещё не импортированы.</summary>
    public static class BuildPrefabLibraryRuntime
    {
        static Building.BuildPrefabLibrary _shared;

        /// <summary>
        /// Одна библиотека блоков на процесс: и сервер, и клиент берут префабы отсюда
        /// (нужно сети — объект стройки создаётся на сервере, а рисуется у всех).
        /// </summary>
        public static Building.BuildPrefabLibrary Shared
        {
            get
            {
                if (_shared == null) _shared = CreateRuntimeLibrary();
                return _shared;
            }
        }

        public static Building.BuildPrefabLibrary CreateRuntimeLibrary()
        {
            var lib = ScriptableObject.CreateInstance<Building.BuildPrefabLibrary>();
            var entries = new List<Building.BuildPrefabLibrary.Entry>();
            foreach (Building.BuildPieceType piece in System.Enum.GetValues(typeof(Building.BuildPieceType)))
                foreach (Core.BuildTier tier in System.Enum.GetValues(typeof(Core.BuildTier)))
                    entries.Add(new Building.BuildPrefabLibrary.Entry { piece = piece, tier = tier, prefab = MakeBlock(piece, tier) });
            lib.entries = entries.ToArray();
            return lib;
        }

        static GameObject MakeBlock(Building.BuildPieceType piece, Core.BuildTier tier)
        {
            GameObject go;
            switch (piece)
            {
                case Building.BuildPieceType.Foundation:
                case Building.BuildPieceType.FoundationTriangle:
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = new Vector3(3f, 0.3f, 3f);
                    go.transform.localPosition = new Vector3(0, -0.15f, 0f);
                    break;
                case Building.BuildPieceType.Floor:
                case Building.BuildPieceType.FloorTriangle:
                case Building.BuildPieceType.Roof:
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = new Vector3(3f, 0.2f, 3f);
                    go.transform.localPosition = new Vector3(0, 2.9f, 0f);
                    break;
                case Building.BuildPieceType.Wall:
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = new Vector3(3f, 3f, 0.2f);
                    go.transform.localPosition = new Vector3(0, 1.5f, 0f);
                    break;
                case Building.BuildPieceType.Doorway:
                    // проём собирается из частей (левая/правая стойка + перемычка), поэтому
                    // через дверь реально можно пройти (раньше это был глухой куб)
                    go = new GameObject("Doorway");
                    MakePart(go, new Vector3(0.35f, 3f, 0.16f), new Vector3(-1.32f, 0f, 0f));
                    MakePart(go, new Vector3(0.35f, 3f, 0.16f), new Vector3(1.32f, 0f, 0f));
                    MakePart(go, new Vector3(3f, 0.55f, 0.16f), new Vector3(0f, 2.445f, 0f));
                    break;
                case Building.BuildPieceType.Window:
                    go = new GameObject("Window");
                    MakePart(go, new Vector3(3f, 1.0f, 0.16f), new Vector3(0f, 0f, 0f));       // подоконник
                    MakePart(go, new Vector3(3f, 0.8f, 0.16f), new Vector3(0f, 2.2f, 0f));     // верхний пояс
                    MakePart(go, new Vector3(0.4f, 3f, 0.16f), new Vector3(-1.3f, 0f, 0f));   // стойки
                    MakePart(go, new Vector3(0.4f, 3f, 0.16f), new Vector3(1.3f, 0f, 0f));
                    break;
                case Building.BuildPieceType.Stairs:
                    go = new GameObject("Stairs");
                    for (int i = 0; i < 8; i++)
                        MakePart(go, new Vector3(1.5f, 0.15f, 0.4f), new Vector3(0f, 0.2f + i * 0.35f, i * 0.42f));
                    break;
                default:
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = new Vector3(3f, 3f, 0.2f);
                    go.transform.localPosition = new Vector3(0, 1.5f, 0f);
                    break;
            }
            // --- Модель из Blender (BD_*) вместо примитивов ---
            // Примитивы остаются коллайдерами (и фолбэком, если префабов ещё нет),
            // а визуал берётся из модели: BD_wall / BD_floor / BD_foundation / BD_doorway /
            // BD_window / BD_stairs. Тир постройки показываем тинтом (Twig → Armored).
            string modelName = World.ModelLibrary.ForBuildPiece(piece);
            if (!string.IsNullOrEmpty(modelName) && World.ModelLibrary.Has(modelName))
            {
                float modelHeight, modelGround;
                switch (piece)
                {
                    case Building.BuildPieceType.Foundation:
                    case Building.BuildPieceType.FoundationTriangle: modelHeight = 0.63f; modelGround = -0.40f; break;
                    case Building.BuildPieceType.Floor:
                    case Building.BuildPieceType.FloorTriangle:
                    case Building.BuildPieceType.Roof: modelHeight = 0.30f; modelGround = 2.71f; break;
                    case Building.BuildPieceType.Stairs: modelHeight = 3.0f; modelGround = 0f; break;
                    case Building.BuildPieceType.Ramp:
                    case Building.BuildPieceType.RampCorner: modelHeight = 1.70f; modelGround = 0f; break;
                    case Building.BuildPieceType.HighWall: modelHeight = 6.0f; modelGround = 0f; break;
                    case Building.BuildPieceType.Pillar: modelHeight = 3.0f; modelGround = 0f; break;
                    case Building.BuildPieceType.Railing: modelHeight = 0.90f; modelGround = 0f; break;
                    case Building.BuildPieceType.Shutters: modelHeight = 3.0f; modelGround = 0f; break;
                    default: modelHeight = 3.0f; modelGround = 0f; break;   // стена / проём / окно
                }

                var model = World.ModelLibrary.AttachFitted(modelName, go.transform, modelHeight, 0f, modelGround);
                if (model != null)
                {
                    World.ModelLibrary.Tint(model, World.ModelLibrary.TierColor(tier));
                    var prims = go.GetComponentsInChildren<MeshRenderer>(true);
                    for (int i = 0; i < prims.Length; i++)
                        if (!prims[i].transform.IsChildOf(model.transform)) prims[i].enabled = false;
                }
            }

            go.name = $"Block_{piece}_{tier}";
            go.layer = Layers.Buildable;
            var block = go.AddComponent<Building.BuildBlock>();
            block.Initialize(piece, tier, 0UL, 0u);
            var visual = go.AddComponent<Building.BuildVisual>();
            visual.renderers = go.GetComponentsInChildren<Renderer>();
            return go;
        }

        static void MakePart(GameObject parent, Vector3 scale, Vector3 pos)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.transform.SetParent(parent.transform, false);
            p.transform.localScale = scale;
            p.transform.localPosition = pos + new Vector3(0, scale.y * 0.5f + pos.y * 0f, 0);
        }

        static void MakeHole(GameObject go, float width = 1.1f) { /* «дырка» делается вычетом меша в FBX-версии */ }
    }
}
