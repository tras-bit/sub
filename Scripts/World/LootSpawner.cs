// ============================================================================
//  SUBSISTENCE — World/LootSpawner.cs
//  Лут по трём тирам (уровень = тир), узлы ресурсов, контейнеры (ящики, шкафчики,
//  электрощиты, аптечки, сейфы), респавн по времени, выпадение лута с монстров
//  и мешок с трупа. «Быстрое лутание»: контейнер открывается за 0.6 с, предмет
//  переносится мгновенно Shift+ЛКМ (как в Rust).
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Player;
using Subsistence.Net;

namespace Subsistence.World
{
    public enum LootContainerKind : byte
    {
        WoodenCrate = 0,   // коридоры: ящики
        FilingCabinet = 1, // офисные шкафы L0
        SupplyCrate = 2,   // армейский ящик (Tier2)
        Locker = 3,        // шкафчики бассейнов
        Toolbox = 4,
        ElectricalPanel = 5, // электрощиты станции (ключ-карты, детали)
        MedicalCabinet = 6,
        SafeBox = 7,       // сейф (Tier3, HQM/C4)
        Barrel = 8,        // бочки (радиация рядом)
        Corpse = 9,        // мешок с трупа
        Airdrop = 10       // «птица» — редкий дорогой ящик (в Rust аналог)
    }

    [Serializable]
    public class LootEntry
    {
        public string itemId;
        public float weight;              // вес в таблице (чем больше — тем чаще)
        public int min = 1, max = 1;
        public float condition = 1f;      // 1 = новый предмет, 0.6 = поношенный
        public bool requiresKeycard;
    }

    [Serializable]
    public class LootTable
    {
        public string id;
        public LootTier tier;
        public LootContainerKind container;
        public int rolls = 3;             // сколько «бросков» по таблице
        public List<LootEntry> entries = new List<LootEntry>();

        public LootEntry Roll(System.Random rng)
        {
            float total = 0f;
            for (int i = 0; i < entries.Count; i++) total += entries[i].weight;
            if (total <= 0f) return null;
            float r = (float)rng.NextDouble() * total;
            for (int i = 0; i < entries.Count; i++)
            {
                r -= entries[i].weight;
                if (r <= 0f) return entries[i];
            }
            return entries[entries.Count - 1];
        }
    }

    /// <summary>Все лут-таблицы игры: Tier1 → Tier2 → Tier3 (растёт качество и «военность»).</summary>
    public static class LootTables
    {
        static readonly Dictionary<string, LootTable> _tables = new Dictionary<string, LootTable>(32);

        public static LootTable Get(string id) => _tables.TryGetValue(id, out var t) ? t : null;
        public static IEnumerable<LootTable> All => _tables.Values;

        static void T(string id, LootTier tier, LootContainerKind kind, int rolls, params (string item, float w, int min, int max)[] items)
        {
            var t = new LootTable { id = id, tier = tier, container = kind, rolls = rolls };
            foreach (var it in items) t.entries.Add(new LootEntry { itemId = it.item, weight = it.w, min = it.min, max = it.max });
            _tables[id] = t;
        }

        static LootTables()
        {
            // ---------- TIER 1: жёлтые коридоры ----------
            T("loot.corridor.crate", LootTier.Tier1, LootContainerKind.WoodenCrate, 3,
                ("wood", 10, 20, 60), ("stones", 10, 20, 60), ("metal.fragments", 6, 5, 20), ("cloth", 8, 5, 25),
                ("scrap", 8, 3, 15), ("can.beans", 6, 1, 2), ("water.dirty", 6, 1, 2), ("mushroom", 4, 1, 3),
                ("rock", 3, 1, 1), ("torch", 3, 1, 1), ("flashlight", 2, 1, 1), ("almond.water", 4, 1, 2),
                ("glow.mushroom", 6, 1, 3), ("duct.tape", 5, 1, 2),
                ("brass.shell", 3, 1, 4), ("pistol.eoka", 1.2f, 1, 1), ("shotgun.waterpipe", 1.2f, 1, 1),
                ("bow.hunting", 1.5f, 1, 1), ("ammo.handmade.shell", 4, 2, 6), ("arrow.wooden", 4, 3, 8),
                ("bone.club", 2, 1, 1), ("hatchet", 2, 1, 1), ("hammer", 2, 1, 1), ("building.planner", 3, 1, 1),
                ("wood.armor.jacket", 1.5f, 1, 1), ("hazmatsuit", 0.6f, 1, 1), ("tool.keycard.green", 0.8f, 1, 1));

            T("loot.corridor.cabinet", LootTier.Tier1, LootContainerKind.FilingCabinet, 2,
                ("scrap", 10, 2, 10), ("cloth", 8, 3, 12), ("sewingkit", 5, 1, 2), ("tool.camera", 3, 1, 1),
                ("almond.water", 6, 1, 3), ("bandage", 5, 1, 3), ("metal.fragments", 4, 3, 12),
                ("pistol.semiauto", 1.2f, 1, 1), ("ammo.pistol", 3, 4, 12), ("lock.code", 3, 1, 1),
                ("lowgradefuel", 3, 2, 8), ("rope", 3, 2, 6), ("tool.keycard.green", 0.9f, 1, 1),
                ("duct.tape", 6, 1, 3), ("glow.mushroom", 4, 1, 2),
                ("lamp.portable", 3, 1, 1), ("respirator", 2.5f, 1, 1));        // L0 — только коридоры

            T("loot.corridor.barrel", LootTier.Tier1, LootContainerKind.Barrel, 2,
                ("lowgradefuel", 8, 5, 20), ("metal.fragments", 6, 5, 18), ("gunpowder", 4, 5, 20),
                ("charcoal", 4, 5, 20), ("sulfur", 3, 3, 12), ("water.dirty", 5, 1, 3));

            // ---------- TIER 2: бассейны ----------
            T("loot.pool.supply", LootTier.Tier2, LootContainerKind.SupplyCrate, 4,
                ("metal.fragments", 8, 20, 80), ("metal.refined", 4, 2, 8), ("sulfur", 4, 10, 40), ("gunpowder", 4, 10, 40),
                ("smg.custom", 2, 1, 1), ("smg.thompson", 2, 1, 1), ("smg.mp5", 1.5f, 1, 1), ("shotgun.pump", 2, 1, 1),
                ("rifle.semiauto", 1.5f, 1, 1), ("ammo.rifle", 5, 10, 40), ("ammo.smg", 5, 10, 40), ("ammo.shotgun", 4, 8, 24),
                ("roadsign.jacket", 2, 1, 1), ("roadsign.kilt", 2, 1, 1), ("coffeecan.helmet", 2, 1, 1),
                ("riot.helmet", 1.2f, 1, 1), ("bone.armor.suit", 1.5f, 1, 1), ("tactical.gloves", 1.5f, 1, 1),
                ("boots.tactical", 1.5f, 1, 1), ("weapon.mod.holosight", 1.2f, 1, 1), ("weapon.mod.lasersight", 1.2f, 1, 1),
                ("explosive.satchel", 1.5f, 1, 2), ("grenade.f1", 1.5f, 1, 2), ("largemedkit", 1.5f, 1, 2),
                ("bandage", 2, 1, 3), ("antidote", 2, 1, 2), ("tool.keycard.blue", 1f, 1, 1),
                ("workbench2", 0.8f, 1, 1), ("hive.membrane", 2, 1, 3));

            T("loot.pool.locker", LootTier.Tier2, LootContainerKind.Locker, 3,
                ("cloth", 6, 10, 40), ("leather", 5, 5, 20), ("sewingkit", 4, 1, 2), ("bone.fragments", 4, 5, 20),
                ("water.bottle", 5, 1, 3), ("pool.water", 6, 1, 2), ("bandage", 4, 1, 3), ("syringe.medical", 3, 1, 2),
                ("flashlight", 3, 1, 1), ("torch.lantern", 2, 1, 1), ("bone.knife.skin", 1.2f, 1, 1), ("machete", 1.5f, 1, 1),
                ("diving.mask", 2.5f, 1, 1), ("flippers", 2.5f, 1, 1), ("oxygen.tank", 1.5f, 1, 1),
                ("chlorine", 4, 1, 2),
                ("tool.geiger", 1.5f, 1, 1), ("tool.keycard.blue", 1.2f, 1, 1));

            T("loot.pool.medical", LootTier.Tier2, LootContainerKind.MedicalCabinet, 3,
                ("bandage", 8, 2, 5), ("syringe.medical", 6, 1, 3), ("largemedkit", 3, 1, 2),
                ("antidote", 4, 1, 3), ("bandage", 3, 1, 4), ("chocolate", 3, 1, 2), ("almond.water", 5, 1, 3));

            // ---------- TIER 3: электростанция ----------
            T("loot.station.crate", LootTier.Tier3, LootContainerKind.SupplyCrate, 4,
                ("rifle.ak", 3, 1, 1), ("rifle.lr300", 3, 1, 1), ("rifle.m16", 2.5f, 1, 1), ("rifle.bolt", 2, 1, 1),
                ("lmg.m249", 1, 1, 1), ("hmlmg", 0.8f, 1, 1), ("smg.vector", 2, 1, 1), ("shotgun.spas", 2, 1, 1),
                ("rocket.launcher", 1, 1, 1), ("multiplegrenadelauncher", 0.8f, 1, 1), ("flamethrower", 1, 1, 1),
                ("ammo.rifle", 6, 20, 90), ("ammo.rifle.hv", 3, 10, 40), ("ammo.rifle.incendiary", 2, 10, 30),
                ("ammo.rifle.explosive", 1.5f, 5, 20), ("ammo.rocket.basic", 2, 1, 3), ("ammo.grenadelauncher.he", 2, 2, 6),
                ("metal.plate.torso", 2, 1, 1), ("metal.facemask", 2, 1, 1), ("exoskeleton.suit", 0.6f, 1, 1),
                ("nightvision", 1, 1, 1), ("hazmat.suit.reactor", 1.5f, 1, 1), ("hazmat.suit.reactor", 1f, 1, 1),
                ("explosive.timed", 2, 1, 3), ("mine.landmine", 1.5f, 1, 2), ("weapon.mod.silencer", 1.5f, 1, 1),
                ("weapon.mod.8x.scope", 1.5f, 1, 1), ("weapon.mod.extendedmags", 1.5f, 1, 1),
                ("metal.refined", 5, 10, 40), ("reactor.rod", 1, 1, 2), ("tool.keycard.red", 1.2f, 1, 1),
                ("fuse.hi", 4, 1, 2), ("rubber.gloves", 3, 1, 1),
                ("boots.rubber", 2.5f, 1, 1), ("wrench.insulated", 1.5f, 1, 1));   // L3 — только станция

            T("loot.station.panel", LootTier.Tier3, LootContainerKind.ElectricalPanel, 3,
                ("metal.fragments", 6, 20, 60), ("metal.refined", 4, 3, 12), ("scrap", 5, 10, 30),
                ("tool.keycard.red", 1.5f, 1, 1), ("reactor.rod", 0.8f, 1, 1), ("autoturret", 1, 1, 1),
                ("samsite", 0.6f, 1, 1), ("flameturret", 1, 1, 1), ("generator.wind.scrap", 1, 1, 1),
                ("anomaly.shard", 1.2f, 1, 2), ("fuse.hi", 5, 1, 2), ("rubber.gloves", 4, 1, 1));

            T("loot.station.safe", LootTier.Tier3, LootContainerKind.SafeBox, 5,
                ("explosive.timed", 4, 1, 4), ("rifle.ak", 3, 1, 1), ("lmg.m249", 2, 1, 1), ("minigun", 0.6f, 1, 1),
                ("exoskeleton.suit", 1.5f, 1, 1), ("metal.refined", 6, 20, 60), ("ammo.rifle.explosive", 3, 10, 30),
                ("weapon.mod.silencer", 2, 1, 1), ("reactor.rod", 2, 1, 3), ("tool.keycard.red", 2, 1, 1),
                ("anomaly.shard", 2, 1, 3), ("explosive.timed", 3, 1, 2));

            // ---------- Монстры ----------
            T("monster.smiler", LootTier.Tier1, LootContainerKind.Corpse, 1,
                ("rattler.flesh", 6, 1, 2), ("bone.fragments", 4, 2, 6), ("almond.water", 3, 1, 1), ("brass.shell", 3, 1, 3));
            T("monster.hound", LootTier.Tier2, LootContainerKind.Corpse, 2,
                ("rattler.flesh", 5, 2, 4), ("leather", 4, 3, 10), ("bone.fragments", 4, 3, 8), ("hive.membrane", 2, 1, 2));
            T("monster.partygoer", LootTier.Tier2, LootContainerKind.Corpse, 2,
                ("cloth", 5, 5, 20), ("hive.membrane", 3, 1, 3), ("bandage", 2, 1, 2), ("metal.fragments", 3, 5, 20));
            T("monster.skinstealer", LootTier.Tier3, LootContainerKind.Corpse, 3,
                ("bone.fragments", 5, 10, 30), ("rattler.flesh", 5, 3, 6), ("metal.refined", 3, 2, 10),
                ("anomaly.shard", 2, 1, 2), ("hazmat.suit.reactor", 1, 1, 1));
            // Свои монстры уровней (v4): у каждого свой дроп
            T("monster.whisperer", LootTier.Tier1, LootContainerKind.Corpse, 2,
                ("bone.fragments", 6, 2, 6), ("cloth", 5, 3, 10), ("almond.water", 3, 1, 2),
                ("tool.camera", 2, 1, 1), ("sewingkit", 2, 1, 1), ("tool.keycard.green", 1.2f, 1, 1));
            T("monster.drowned", LootTier.Tier2, LootContainerKind.Corpse, 3,
                ("hive.membrane", 5, 2, 5), ("pool.water", 4, 1, 2), ("syringe.medical", 3, 1, 2),
                ("bone.fragments", 5, 3, 10), ("bandage", 4, 1, 3), ("tool.keycard.blue", 1.2f, 1, 1),
                ("smg.mp5", 0.8f, 1, 1), ("ammo.smg", 3, 8, 24));
            T("monster.spark", LootTier.Tier3, LootContainerKind.Corpse, 3,
                ("metal.refined", 4, 2, 6), ("copper.wire", 4, 3, 10), ("tool.geiger", 2, 1, 1),
                ("largemedkit", 2, 1, 2), ("ammo.rifle", 4, 10, 30), ("tool.keycard.red", 1f, 1, 1),
                ("rifle.ak", 0.7f, 1, 1));
            T("monster.clump", LootTier.Tier2, LootContainerKind.Corpse, 3,
                ("hive.membrane", 5, 3, 8), ("rattler.flesh", 4, 4, 8), ("metal.fragments", 4, 20, 60));
            T("monster.bacteria", LootTier.Tier3, LootContainerKind.Corpse, 8,
                ("anomaly.shard", 6, 3, 8), ("reactor.rod", 4, 2, 5), ("exoskeleton.suit", 3, 1, 1),
                ("minigun", 2, 1, 1), ("metal.refined", 6, 50, 150), ("explosive.timed", 4, 3, 8),
                ("nightvision", 3, 1, 1), ("ammo.rifle.explosive", 4, 30, 90));

            // ---------- Аирдроп ----------
            T("loot.airdrop", LootTier.Tier3, LootContainerKind.Airdrop, 5,
                ("rifle.ak", 3, 1, 1), ("lmg.m249", 1.5f, 1, 1), ("minigun", 1, 1, 1), ("explosive.timed", 3, 2, 6),
                ("metal.plate.torso", 3, 1, 1), ("exoskeleton.suit", 2, 1, 1), ("ammo.rifle.hv", 3, 30, 90),
                ("reactor.rod", 2, 2, 4), ("anomaly.shard", 2, 2, 5), ("tool.keycard.red", 2, 1, 1));
        }
    }

    /// <summary>Лут-контейнер в мире: наполняется таблицей и респавнится по таймеру.</summary>
    public class LootContainer : NetEntity
    {
        public LootContainerKind kind;
        public LootTier tier;
        public string tableId;
        public bool respawns = true;
        public float respawnSeconds = 900f;
        public bool requiresKeycard;

        public ItemContainer Items { get; private set; }
        public bool IsEmpty => Items == null || Items.IsEmpty;
        public float OpenTime => 0.6f;    // «быстрое лутание» — почти моментально

        float _respawnAt;
        int _seed = -1;

        /// <summary>Когда ящик наполнится снова (абсолютное время Unity; 0 — не ждёт). Для сохранения мира.</summary>
        public float RespawnAt { get => _respawnAt; set => _respawnAt = value; }
        /// <summary>Сид содержимого (тот же ящик после загрузки — тот же лут).</summary>
        public int ContentSeed => _seed;

        /// <summary>Все контейнеры мира: сеть ищет ящик по позиции (FindAt, как двери).</summary>
        public static readonly List<LootContainer> All = new List<LootContainer>(512);

        public static LootContainer FindAt(Vector3 pos, float radius = 2.5f)
        {
            LootContainer best = null; float bestD = radius;
            for (int i = 0; i < All.Count; i++)
            {
                var c = All[i];
                if (c == null) continue;
                float d = Vector3.Distance(c.transform.position, pos);
                if (d <= bestD) { bestD = d; best = c; }
            }
            return best;
        }

        protected override void OnEnable() { base.OnEnable(); if (!All.Contains(this)) All.Add(this); }
        protected override void OnDisable() { All.Remove(this); base.OnDisable(); }

        void Awake()
        {
            Items = new ItemContainer(kind.ToString(), kind == LootContainerKind.SafeBox ? 24 : 18);
        }

        /// <summary>Ёмкость другого размера (мешок с лутом — 42 слота, как рюкзак игрока).</summary>
        public void SetCapacity(int slots)
        {
            Items = new ItemContainer(Items != null ? Items.ContainerName : kind.ToString(), Mathf.Max(1, slots));
        }

        public void Fill(int seed)
        {
            _seed = seed;
            var table = LootTables.Get(tableId);
            Items = new ItemContainer(kind.ToString(), kind == LootContainerKind.SafeBox ? 24 : 18);
            if (table == null) return;
            var rng = new System.Random(seed);
            for (int i = 0; i < table.rolls; i++)
            {
                var e = table.Roll(rng);
                if (e == null) continue;
                int amount = rng.Next(e.min, e.max + 1);
                var stack = new ItemStack(e.itemId, amount);
                var def = stack.Def;
                if (def != null && def.HasDurability) stack.durability = def.maxDurability * e.condition;
                Items.TryAdd(stack);
            }
            if (IsEmpty && table.entries.Count > 0)
            {
                var e = table.entries[rng.Next(0, table.entries.Count)];
                Items.TryAdd(new ItemStack(e.itemId, Mathf.Max(1, e.min)));
            }
        }

        public void Looted()
        {
            if (!respawns) return;
            _respawnAt = Time.time + respawnSeconds;
        }

        void Update()
        {
            if (!NetworkBridge.IsServer || _respawnAt <= 0f) return;
            if (Time.time < _respawnAt) return;
            _respawnAt = 0f;
            Fill(_seed >= 0 ? _seed + 7919 : UnityEngine.Random.Range(1, 999999));
            InventoryNet.ServerRefilled(this);      // клиентам: ящик снова полон (кто открыт — обновится)
        }

        public override void WriteSnapshot(BufferWriter w)
        {
            w.WriteByte((byte)kind);
            w.WritePosition(transform.position, -100f, 1000f);
            w.WriteULong((ulong)(Items?.Version ?? 0));
        }

        public override void ReadSnapshot(BufferReader r)
        {
            kind = (LootContainerKind)r.ReadByte();
            transform.position = r.ReadPosition(-100f, 1000f);
        }
    }

    /// <summary>Спавнер лута: раскладывает контейнеры по узлам уровня и держит таблицы тиров.</summary>
    public class LootSpawner : MonoBehaviour
    {
        public LevelGenerator generator;
        public GameObject containerPrefab;    // опционально: модель ящика; иначе бокс-заглушка
        public float respawnSeconds = 0f;     // 0 = брать из тира ящика (см. RespawnForTier)

        public static LootSpawner Instance { get; private set; }

        void Awake() => Instance = this;

        [Serializable] public struct TierConfig { public LevelTheme level; public string[] tables; public int nodeShare; public float respawnSeconds; }

        public TierConfig[] tiers = new TierConfig[]
        {
            // 17_loot_respawn: «5-30 минут в зависимости от тира ящика»
            new TierConfig { level = LevelTheme.Corridors,   tables = new[] { "loot.corridor.crate", "loot.corridor.cabinet", "loot.corridor.barrel" }, nodeShare = 3, respawnSeconds = Balance.LootRespawnTier1 },
            new TierConfig { level = LevelTheme.Poolrooms,   tables = new[] { "loot.pool.supply", "loot.pool.locker", "loot.pool.medical" }, nodeShare = 2, respawnSeconds = Balance.LootRespawnTier2 },
            new TierConfig { level = LevelTheme.PowerStation, tables = new[] { "loot.station.crate", "loot.station.panel", "loot.station.safe" }, nodeShare = 2, respawnSeconds = Balance.LootRespawnTier3 },
        };

        public void PopulateAll(List<GeneratedLevel> levels)
        {
            // Спавнер подписывается на результаты генератора и заполняет мир лутом.
            for (int li = 0; li < levels.Count; li++)
            {
                var lvl = levels[li];
                var cfg = FindTier(lvl.theme);
                int idx = 0;
                foreach (var node in lvl.lootNodes)
                {
                    if (cfg.nodeShare > 0 && idx % cfg.nodeShare != 0 && idx % 2 != 0) { idx++; continue; }
                    string tableId = cfg.tables[idx % cfg.tables.Length];
                    SpawnContainer(node, tableId, lvl.theme);
                    idx++;
                }
            }
        }

        TierConfig FindTier(LevelTheme theme)
        {
            for (int i = 0; i < tiers.Length; i++) if (tiers[i].level == theme) return tiers[i];
            return tiers[0];
        }

        public LootContainer SpawnContainer(Vector3 pos, string tableId, LevelTheme level)
        {
            var table = LootTables.Get(tableId);
            var kind = table != null ? table.container : LootContainerKind.WoodenCrate;
            var tier = table != null ? table.tier : LootTier.Tier1;

            float height = kind == LootContainerKind.Locker ? 1.90f
                         : kind == LootContainerKind.FilingCabinet ? 1.50f
                         : kind == LootContainerKind.ElectricalPanel ? 1.20f
                         : 0.75f;
            float yaw = UnityEngine.Random.Range(0f, 360f);

            GameObject go = null;
            if (containerPrefab != null) go = Instantiate(containerPrefab, pos, Quaternion.Euler(0, yaw, 0));
            if (go == null)
            {
                // Прокси-куб: он же коллайдер для «E» и фолбэк, если модели ещё нет.
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = pos + Vector3.up * (height * 0.5f);
                go.transform.localScale = kind == LootContainerKind.SafeBox ? new Vector3(0.8f, 0.8f, 0.8f)
                                     : kind == LootContainerKind.Locker ? new Vector3(0.7f, 1.9f, 0.6f)
                                     : new Vector3(0.9f, 0.7f, 0.9f);
                go.transform.rotation = Quaternion.Euler(0, yaw, 0);
                var col = go.GetComponent<Collider>();
                if (col != null) col.isTrigger = false;

                // Модель из Blender: ящик / картотека / шкафчик / электрощит / бочка / сейф (ModelLibrary.ForLootKind).
                string modelName = World.ModelLibrary.ForLootKind(kind);
                if (kind == LootContainerKind.Barrel)
                    modelName = World.ModelLibrary.ForBarrel(tableId != null && tableId.Contains("radio"));
                var visual = World.ModelLibrary.AttachFitted(modelName,
                                                            go.transform, height, 0f, groundY: -height * 0.5f);
                if (visual != null)
                {
                    var r = go.GetComponent<MeshRenderer>();
                    if (r != null) r.enabled = false;
                }
            }
            go.name = $"Loot_{kind}";
            go.layer = Layers.Loot;
            var lc = go.GetComponent<LootContainer>();
            if (lc == null) lc = go.AddComponent<LootContainer>();
            lc.kind = kind; lc.tier = tier; lc.tableId = tableId;
            lc.respawnSeconds = respawnSeconds > 0f ? respawnSeconds : RespawnForTier(tier);
            // Содержимое считает только сервер: иначе у каждого игрока был бы свой лут
            // (играем в «мир один на всех» — как в Rust). Клиент получит содержимое при открытии.
            if (NetworkBridge.IsServer) lc.Fill(UnityEngine.Random.Range(1, 999999));
            lc.AssignNetId(new NetId((uint)UnityEngine.Random.Range(1000, 4000000)));
            return lc;
        }

        /// <summary>17_loot_respawn: 5 мин (Tier1) / 15 мин (Tier2) / 30 мин (Tier3).</summary>
        public static float RespawnForTier(LootTier tier)
        {
            switch (tier)
            {
                case LootTier.Tier1: return Balance.LootRespawnTier1;
                case LootTier.Tier2: return Balance.LootRespawnTier2;
                case LootTier.Tier3: return Balance.LootRespawnTier3;
                default: return Balance.LootRespawnTier1;
            }
        }

        /// <summary>Лут с монстра (вызывается MonsterRuntime.Die).</summary>
        public static void SpawnMonsterLoot(AI.MonsterDef def, Vector3 pos)
        {
            if (Instance == null) return;
            var corpse = Instance.SpawnContainer(pos, def.lootTable, def.homeLevel);
            corpse.kind = LootContainerKind.Corpse;
            corpse.respawns = false;
            corpse.name = $"Corpse_{def.nameEn}";
        }
    }

    /// <summary>
    /// Мирные ресурсы: залежи (дерево/камень/руда) на уровнях — их надо добывать киркой/
    /// топором (Rust-механика), а не только находить в ящиках.
    /// </summary>
    public static class WorldDeposits
    {
        public struct Deposit { public Vector3 position; public string item; public float amount; public int hitsLeft; public LevelTheme level; }
        public static readonly List<Deposit> All = new List<Deposit>(512);

        public static void SpawnDeposit(Vector3 pos, string item, float amount, int hits, LevelTheme level)
            => All.Add(new Deposit { position = pos, item = item, amount = amount, hitsLeft = hits, level = level });

        /// <summary>
        /// Собрать предмет «просто на полу». Одну и ту же сборку использует и сервер (создаёт),
        /// и клиенты (воссоздают по object.spawn) — модель PR_* появится здесь.
        /// </summary>
        public static ItemPickup BuildLooseItem(string itemId, int amount, Vector3 pos, float lifetime = 900f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "LooseItem_" + itemId;
            go.transform.position = pos + Vector3.up * 0.3f;
            go.transform.localScale = Vector3.one * 0.25f;
            go.layer = Layers.Loot;
            var pickup = go.AddComponent<ItemPickup>();
            pickup.itemId = itemId; pickup.amount = Mathf.Max(1, amount);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;

            // Своя модель предмета (alpha 1.0): IT_* у «своих» предметов уровней, W_*/EX_* у оружия.
            // Куб остаётся коллайдером и физикой, но его меш выключаем — иначе он торчит из модели.
            string worldModel = World.ModelLibrary.ForWorldItem(itemId);
            var visual = string.IsNullOrEmpty(worldModel)
                ? null
                : World.ModelLibrary.AttachFitted(worldModel, go.transform, 0.26f, UnityEngine.Random.Range(0f, 360f));
            if (visual != null)
            {
                var cube = go.GetComponent<MeshRenderer>();
                if (cube != null) cube.enabled = false;
            }
            // «Предметы исчезают» — как в Rust через 15 мин (0 — не исчезает).
            if (lifetime > 0f) UnityEngine.Object.Destroy(go, lifetime);
            return pickup;
        }

        /// <summary>Выпадение предмета «просто на пол» локально (офлайн/косметика).</summary>
        public static void SpawnLooseItem(string itemId, int amount, Vector3 pos)
            => BuildLooseItem(itemId, amount, pos);

        /// <summary>
        /// Выбросить предмет из рюкзака. Удалённый клиент только просит сервер (предмет он уже
        /// списал у себя оптимистично, поэтому сервер уменьшает зеркало молча); хост и офлайн
        /// создают предмет сами, а в сетевом мире он ещё и репликуется всем.
        /// </summary>
        public static void DropLooseItem(string itemId, int amount, Vector3 pos)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return;

            if (NetworkBridge.IsClient && !NetworkBridge.IsServer)          // удалённый клиент
            {
                var w = BufferWriter.Rent(64);
                w.WriteString(itemId);
                w.WriteUShort((ushort)Mathf.Clamp(amount, 1, 65535));
                w.WritePosition(pos, -100f, 1000f);
                NetworkBridge.Command(ClientCommand.LootDrop, w.ToArray());
                BufferWriter.Return(w);
                return;
            }

            if (NetworkBridge.Host != null && NetworkBridge.Host.Role != NetRole.Offline)
                SpawnNet.ServerSpawnLooseItem(itemId, amount, pos);          // хост/выделенный сервер
            else
                BuildLooseItem(itemId, amount, pos);                         // офлайн
        }

        /// <summary>
        /// Мешок с лутом (смерть игрока). Сервер создаёт его сам, клиенты — по object.spawn,
        /// содержимое приходит при открытии (как у любого контейнера).
        /// </summary>
        public static LootContainer BuildCorpseBag(Vector3 pos, float yaw, ulong ownerId)
        {
            // Прокси-куб: коллайдер для «E» + фолбэк. Визуал — PR_loot_bag из Blender.
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"LootBag_{ownerId}";
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
            go.layer = Layers.Ragdoll;
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = false;

            var visual = ModelLibrary.AttachFitted(ModelLibrary.Names.LootBag, go.transform,
                                                   0.50f, yaw, groundY: -0.25f);
            if (visual != null)
            {
                var r = go.GetComponent<MeshRenderer>();
                if (r != null) r.enabled = false;
            }

            var bag = go.GetComponent<LootContainer>();
            if (bag == null) bag = go.AddComponent<LootContainer>();
            bag.kind = LootContainerKind.Corpse;
            bag.tier = LootTier.Tier2;
            bag.respawns = false;                 // труп не «респавнится»
            bag.tableId = "corpse.player";
            bag.SetCapacity(42);                  // как рюкзак: всё, что было в руках
            return bag;
        }
    }

    /// <summary>Подбираемый предмет из мира (E — подобрать, Shift+E — подобрать всё).</summary>
    public class ItemPickup : MonoBehaviour
    {
        public string itemId;
        public int amount = 1;

        public bool TryPickup(PlayerInventory inv)
        {
            // В сети предметом владеет сервер: удалённый клиент просит, хост/офлайн берёт сам.
            // (Сервер проверит дистанцию и свободное место, после чего пришлёт inv.give.)
            if (NetworkBridge.IsClient && !NetworkBridge.IsServer)
            {
                var marker = GetComponent<NetSpawned>();
                if (marker != null && marker.netId != 0u) { SpawnNet.RequestPickup(marker.netId); return true; }
            }

            var stack = new ItemStack(itemId, amount);
            int left = inv.TryAdd(stack);
            if (left == 0)
            {
                Subsistence.Audio.AudioDirector.Play2D("pickup", 0.6f);
                Destroy(gameObject);
                return true;
            }
            amount = left;
            return false;
        }
    }
}
