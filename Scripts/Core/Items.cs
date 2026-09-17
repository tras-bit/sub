// ============================================================================
//  SUBSISTENCE — Core/Items.cs
//  Предметы: определения (ItemDef), рантайм-стек (ItemStack) и база (ItemDatabase).
//  ID предметов = короткие имена Rust ("rifle.ak", "ammo.rifle", "hazmatsuit"),
//  чтобы был валидный перенос таблиц баланса, лут-таблиц и крафта.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Subsistence.Core
{
    /// <summary>Непроверяемое, статичное определение предмета. Живёт в БД, GC не трогает.</summary>
    [Serializable]
    public class ItemDef
    {
        public string id;                       // "rifle.ak"
        public string name;                     // "Assault Rifle (AK)"
        public string nameRu;                   // "Автомат АК"
        public ItemCategory category;
        public int stackSize = 1;               // 1 для оружия/брони, 1000 для ресурсов
        public float maxDurability = 0f;        // 0 = не имеет прочности
        public float weight = 0.1f;             // кг, влияет на выносливость (Rust-like)
        public Rarity rarity = Rarity.Common;
        public LootTier minTier = LootTier.Tier1;
        public LevelTheme[] levels;             // где вообще встречается
        public int scrapCost = 0;               // цена в исследовании/верстаке
        public string iconKey;                  // адрес иконки в Addressables

        public string Display(bool ru = true) => ru && !string.IsNullOrEmpty(nameRu) ? nameRu : name;

        public bool IsStackable => stackSize > 1;
        public bool HasDurability => maxDurability > 0f;
    }

    /// <summary>Рантайм-экземпляр: количество, износ, обвесы, патроны в магазине.</summary>
    [Serializable]
    public class ItemStack
    {
        public string id;
        public int amount = 1;

        /// Текущая прочность (Rust: 0..max). Для оружия/инструментов/брони.
        public float durability;
        /// «Состояние» 0..1 — множитель качества после ремонта (каждый ремонт −25 % от исходного).
        public float condition = 1f;
        /// Патроны в магазине (оружие).
        public int ammoInMag;
        /// Обвесы по слотам.
        public readonly List<string> attachments = new List<string>(4);
        /// Уникальный id инстанса — для синхронизации в сети и трейд-логов.
        public uint instanceId;
        /// Кто крафтил/последний владелец (для античит-логов и «Rust-like» трейда).
        public ulong crafterSteamId;
        public string skinId;
        /// Служебная метка: у ключа (lock.key) — id замка, к которому ключ подходит.
        public int metaTag;
        /// Счётчик убийств (для статистики как в Rust — гравировка).
        public int kills;

        static int _nextInstance = 1;      // Interlocked работает только с int
        public static uint NextInstanceId() => (uint)System.Threading.Interlocked.Increment(ref _nextInstance);

        public ItemStack() { }
        public ItemStack(string id, int amount = 1)
        {
            this.id = id;
            this.amount = amount;
            instanceId = NextInstanceId();
            var def = ItemDatabase.Def(id);
            durability = def?.maxDurability ?? 0f;
            // 38_skins: к любой новой вещи «прилипает» скин, выбранный для её типа
            // (магазин скинов — Progression/Skins.cs). Скины локальные, модов нет.
            skinId = Subsistence.Progression.SkinCatalog.SkinForItem(id);
        }

        public ItemDef Def => ItemDatabase.Def(id);
        public bool IsEmpty => string.IsNullOrEmpty(id) || amount <= 0;

        /// <summary>Процент прочности 0..1 (Rust показывает «%» в описании).</summary>
        public float DurabilityPercent
        {
            get
            {
                var d = Def;
                if (d == null || d.maxDurability <= 0f) return 1f;
                return Mathf.Clamp01(durability / d.maxDurability) * condition;
            }
        }

        /// <summary>Нанести износ предмету. Возвращает true, если предмет сломался.</summary>
        public bool Wear(float amount)
        {
            var d = Def;
            if (d == null || d.maxDurability <= 0f) return false;
            durability -= amount;
            if (durability <= 0f) { durability = 0f; return true; }
            return false;
        }

        public void Repair(float fraction)
        {
            var d = Def;
            if (d == null || d.maxDurability <= 0f) return;
            durability = Mathf.Min(d.maxDurability, durability + d.maxDurability * fraction);
        }

        public ItemStack Clone()
        {
            var c = new ItemStack { id = id, amount = amount, durability = durability, condition = condition, ammoInMag = ammoInMag, instanceId = NextInstanceId(), crafterSteamId = crafterSteamId, skinId = skinId, kills = kills };
            c.attachments.AddRange(attachments);
            return c;
        }

        public bool CanStackWith(ItemStack other)
        {
            if (other == null || IsEmpty || other.IsEmpty) return false;
            if (id != other.id) return false;
            var d = Def;
            if (d == null || !d.IsStackable) return false;
            if (d.HasDurability && !Mathf.Approximately(durability, other.durability)) return false;
            if (attachments.Count != other.attachments.Count) return false;
            return true;
        }

        public override string ToString() => amount > 1 ? $"{id} x{amount}" : id;
    }

    /// <summary>
    /// База предметов. Заполняется кодом (детерминированно, без .asset-файлов — так проект
    /// открывается «как есть»), но умеет догружать/перезаписывать из
    /// StreamingAssets/items.json (туда можно вынести баланс и править без пересборки).
    /// </summary>
    public static class ItemDatabase
    {
        static readonly Dictionary<string, ItemDef> _defs = new Dictionary<string, ItemDef>(512);
        static bool _initialized;

        public static IReadOnlyDictionary<string, ItemDef> All { get { Init(); return _defs; } }

        public static ItemDef Def(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Init();
            return _defs.TryGetValue(id, out var d) ? d : null;
        }

        public static string NameOf(string id, bool ru = true)
        {
            var d = Def(id);
            return d != null ? d.Display(ru) : id;
        }

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            RegisterAll();
            TryLoadOverrides();
        }

        /// <summary>Дозагрузка/переопределение из StreamingAssets/items.json.</summary>
        public static void TryLoadOverrides()
        {
            try
            {
                string path = System.IO.Path.Combine(Application.streamingAssetsPath, "items.json");
                if (!System.IO.File.Exists(path)) return;
                var json = System.IO.File.ReadAllText(path);
                var list = JsonUtility.FromJson<ItemDefList>("{\"items\":" + json + "}");
                if (list?.items == null) return;
                foreach (var d in list.items) _defs[d.id] = d;
                Debug.Log($"[Items] Переопределено из items.json: {list.items.Length}");
            }
            catch (Exception e) { Debug.LogWarning("[Items] items.json не применён: " + e.Message); }
        }

        [Serializable] class ItemDefList { public ItemDef[] items; }

        public static void Register(ItemDef d) => _defs[d.id] = d;

        // ======================= ХЕЛПЕРЫ ОБЪЯВЛЕНИЯ =========================
        static void R(string id, string ru, string en, ItemCategory cat, int stack, float dur = 0, float w = 0.1f,
                      Rarity rarity = Rarity.Common, LootTier tier = LootTier.Tier1, int scrap = 0, LevelTheme[] lv = null)
        {
            Register(new ItemDef
            {
                id = id, nameRu = ru, name = en, category = cat, stackSize = stack, maxDurability = dur,
                weight = w, rarity = rarity, minTier = tier, scrapCost = scrap, levels = lv ?? AllLevels, iconKey = "icons/" + id.Replace('.', '_')
            });
        }

        static readonly LevelTheme[] AllLevels = { LevelTheme.Corridors, LevelTheme.Poolrooms, LevelTheme.PowerStation };
        static readonly LevelTheme[] L0 = { LevelTheme.Corridors };
        static readonly LevelTheme[] L1 = { LevelTheme.Poolrooms };
        static readonly LevelTheme[] L2 = { LevelTheme.PowerStation };
        static readonly LevelTheme[] L01 = { LevelTheme.Corridors, LevelTheme.Poolrooms };
        static readonly LevelTheme[] L12 = { LevelTheme.Poolrooms, LevelTheme.PowerStation };

        static void RegisterAll()
        {
            // ------------------------------------------------------------------
            // РЕСУРСЫ (Tier1 — коридоры, есть везде)
            // ------------------------------------------------------------------
            R("wood", "Древесина", "Wood", ItemCategory.Resource, 1000, 0, 1.0f);
            R("stones", "Камень", "Stones", ItemCategory.Resource, 1000, 0, 1.0f);
            R("metal.fragments", "Металлолом", "Metal Fragments", ItemCategory.Resource, 1000, 0, 0.5f, Rarity.Uncommon, LootTier.Tier1, 5);
            R("metal.refined", "Очищенный металл", "High Quality Metal", ItemCategory.Resource, 1000, 0, 0.3f, Rarity.Rare, LootTier.Tier2, 25, L12);
            R("sulfur", "Сера", "Sulfur", ItemCategory.Resource, 1000, 0, 0.5f, Rarity.Uncommon, LootTier.Tier2, 8, L12);
            R("gunpowder", "Порох", "Gun Powder", ItemCategory.Resource, 1000, 0, 0.2f, Rarity.Uncommon, LootTier.Tier2, 4, L12);
            R("sulfur.ore", "Серная руда", "Sulfur Ore", ItemCategory.Resource, 1000, 0, 1.0f, Rarity.Uncommon, LootTier.Tier2, 0, L12);
            R("metal.ore", "Металлическая руда", "Metal Ore", ItemCategory.Resource, 1000, 0, 1.0f, Rarity.Uncommon, LootTier.Tier1, 0, L01);
            R("scrap", "Хлам", "Scrap", ItemCategory.Resource, 1000, 0, 0.1f, Rarity.Uncommon, LootTier.Tier1, 0);
            R("cloth", "Ткань", "Cloth", ItemCategory.Resource, 1000, 0, 0.05f);
            R("leather", "Кожа", "Leather", ItemCategory.Resource, 1000, 0, 0.1f);
            R("bone.fragments", "Осколки костей", "Bone Fragments", ItemCategory.Resource, 1000, 0, 0.2f, Rarity.Common, LootTier.Tier1, 0, L01);
            R("rope", "Верёвка", "Rope", ItemCategory.Resource, 500, 0, 0.1f);
            R("sewingkit", "Швейный набор", "Sewing Kit", ItemCategory.Resource, 500, 0, 0.1f, Rarity.Uncommon, LootTier.Tier1, 3);
            R("lowgradefuel", "Низкосортное топливо", "Low Grade Fuel", ItemCategory.Resource, 500, 0, 0.2f, Rarity.Uncommon, LootTier.Tier1, 1);
            R("charcoal", "Уголь", "Charcoal", ItemCategory.Resource, 1000, 0, 0.2f);
            R("gunpowder.buckshot", "Дробь (кустарная)", "Buckshot (handmade)", ItemCategory.Resource, 500, 0, 0.1f, Rarity.Common, LootTier.Tier1, 0, L01);

            // Backrooms-специфичные ресурсы (с них начинается вход в станцию)
            R("brass.shell", "Латунная гильза", "Brass Shell", ItemCategory.Backrooms, 500, 0, 0.05f, Rarity.Uncommon, LootTier.Tier1, 0, L0);
            R("almond.water", "Миндальная вода", "Almond Water", ItemCategory.Drink, 10, 0, 0.3f, Rarity.Uncommon, LootTier.Tier1, 0, L0);
            R("rattler.flesh", "Плоть шатуна", "Monster Flesh", ItemCategory.Backrooms, 50, 0, 0.5f, Rarity.Rare, LootTier.Tier1, 0, L01);
            R("hive.membrane", "Мембрана улья", "Hive Membrane", ItemCategory.Backrooms, 50, 0, 0.4f, Rarity.VeryRare, LootTier.Tier2, 0, L1);
            R("reactor.rod", "Топливный стержень", "Fuel Rod", ItemCategory.Backrooms, 20, 0, 2.0f, Rarity.Anomalous, LootTier.Tier3, 0, L2);
            R("anomaly.shard", "Аномальный осколок", "Anomaly Shard", ItemCategory.Backrooms, 20, 0, 1.0f, Rarity.Anomalous, LootTier.Tier3, 0, L12);

            // ------------------------------------------------------------------
            // ЕДА / ВОДА / МЕДИЦИНА  (выживание Backrooms)
            // ------------------------------------------------------------------
            R("can.beans", "Банка фасоли", "Can of Beans", ItemCategory.Food, 10, 0, 0.5f);
            R("can.tuna", "Банка тунца", "Can of Tuna", ItemCategory.Food, 10, 0, 0.5f);
            R("chocolate", "Шоколад", "Chocolate Bar", ItemCategory.Food, 10, 0, 0.2f, Rarity.Uncommon, LootTier.Tier2, 0, L12);
            R("bearmeat.cooked", "Жареная медвежатина", "Cooked Bear Meat", ItemCategory.Food, 10, 0, 0.5f);
            R("meat.raw", "Сырое мясо", "Raw Meat", ItemCategory.Food, 10, 0, 0.5f, Rarity.Common, LootTier.Tier1, 0, L01);
            R("water.bottle", "Бутылка воды", "Water Bottle", ItemCategory.Drink, 4, 0, 0.5f);
            R("water.dirty", "Грязная вода", "Dirty Water", ItemCategory.Drink, 6, 0, 0.5f, Rarity.Common, LootTier.Tier1, 0, L01);
            R("pool.water", "Хлорированная вода", "Pool Water", ItemCategory.Drink, 4, 0, 0.5f, Rarity.Common, LootTier.Tier2, 0, L1);
            R("apple", "Яблоко", "Apple", ItemCategory.Food, 10, 0, 0.2f);
            R("mushroom", "Гриб", "Mushroom", ItemCategory.Food, 10, 0, 0.1f, Rarity.Common, LootTier.Tier1, 0, L01);

            R("bandage", "Бинт", "Bandage", ItemCategory.Medical, 10, 0, 0.1f);
            R("syringe.medical", "Медицинский шприц", "Medical Syringe", ItemCategory.Medical, 10, 0, 0.1f, Rarity.Uncommon);
            R("largemedkit", "Большая аптечка", "Large Medkit", ItemCategory.Medical, 10, 0, 0.3f, Rarity.Rare, LootTier.Tier2, 20, L12);
            R("antidote", "Антирад", "Anti-Radiation Pills", ItemCategory.Medical, 10, 0, 0.1f, Rarity.Rare, LootTier.Tier2, 0, L12);

            // ------------------------------------------------------------------
            // ОРУЖИЕ БЛИЖНЕГО БОЯ  (rock → камень, как в Rust)
            // ------------------------------------------------------------------
            R("rock", "Камень", "Rock", ItemCategory.Weapon, 1, 100, 1.0f);
            R("torch", "Факел", "Torch", ItemCategory.Tool, 1, 150, 0.5f);
            R("bone.club", "Костяная дубина", "Bone Club", ItemCategory.Weapon, 1, 200, 1.0f, Rarity.Common, LootTier.Tier1, 10);
            R("knife.bone", "Костяной нож", "Bone Knife", ItemCategory.Weapon, 1, 200, 0.5f, Rarity.Common, LootTier.Tier1, 10);
            R("hatchet", "Топорик", "Hatchet", ItemCategory.Tool, 1, 300, 1.0f, Rarity.Common, LootTier.Tier1, 15);
            R("pickaxe", "Кирка", "Pickaxe", ItemCategory.Tool, 1, 300, 1.0f, Rarity.Common, LootTier.Tier1, 15);
            R("hammer", "Молоток (стройка/ремонт)", "Hammer", ItemCategory.Tool, 1, 400, 1.0f, Rarity.Common, LootTier.Tier1, 10);
            R("salvaged.cleaver", "Мачете (самопал)", "Salvaged Cleaver", ItemCategory.Weapon, 1, 300, 1.5f, Rarity.Uncommon, LootTier.Tier2, 25, L12);
            R("machete", "Мачете", "Machete", ItemCategory.Weapon, 1, 400, 1.2f, Rarity.Rare, LootTier.Tier2, 40, L12);
            R("pipe.wrench", "Трубный ключ", "Pipe Wrench", ItemCategory.Weapon, 1, 350, 1.2f, Rarity.Uncommon, LootTier.Tier1, 20, L01);
            R("sledgehammer", "Кувалда", "Sledgehammer", ItemCategory.Tool, 1, 500, 3.0f, Rarity.Rare, LootTier.Tier2, 30, L12);
            R("spear.wooden", "Деревянное копьё", "Wooden Spear", ItemCategory.Weapon, 1, 250, 1.5f, Rarity.Common, LootTier.Tier1, 10);
            R("bone.knife.skin", "Тесак из плоти", "Flesh Cleaver", ItemCategory.Weapon, 1, 350, 1.5f, Rarity.VeryRare, LootTier.Tier2, 0, L1);
            R("stun.baton", "Дубинка-электрошокер", "Stun Baton", ItemCategory.Weapon, 1, 250, 1.0f, Rarity.VeryRare, LootTier.Tier3, 0, L2);

            // ------------------------------------------------------------------
            // ОГНЕСТРЕЛЬНОЕ — TIER 1 (коридоры): самопал, дробовики, пистолеты
            // ------------------------------------------------------------------
            R("bow.hunting", "Охотничий лук", "Hunting Bow", ItemCategory.Weapon, 1, 200, 1.0f, Rarity.Common, LootTier.Tier1, 20);
            R("crossbow", "Арбалет", "Crossbow", ItemCategory.Weapon, 1, 250, 1.5f, Rarity.Common, LootTier.Tier1, 25, L01);
            R("pistol.eoka", "Пистолет Эока", "Eoka Pistol", ItemCategory.Weapon, 1, 150, 1.0f, Rarity.Common, LootTier.Tier1, 15, L01);
            R("pistol.nailgun", "Гвоздомёт", "Nailgun", ItemCategory.Weapon, 1, 200, 1.0f, Rarity.Uncommon, LootTier.Tier1, 20, L0);
            R("shotgun.waterpipe", "Дробовик из трубы", "Waterpipe Shotgun", ItemCategory.Weapon, 1, 150, 1.5f, Rarity.Common, LootTier.Tier1, 25, L01);
            R("shotgun.double", "Двустволка", "Double Barrel Shotgun", ItemCategory.Weapon, 1, 200, 1.8f, Rarity.Uncommon, LootTier.Tier1, 30, L01);
            R("revolver.python", "Револьвер «Питон»", "Python Revolver", ItemCategory.Weapon, 1, 300, 0.9f, Rarity.Rare, LootTier.Tier2, 60, L12);
            R("smg.custom", "Самопальный ПП", "Custom SMG", ItemCategory.Weapon, 1, 300, 1.2f, Rarity.Uncommon, LootTier.Tier2, 40, L12);
            R("pistol.semiauto", "Полуавтоматический пистолет", "Semi-Auto Pistol", ItemCategory.Weapon, 1, 300, 0.8f, Rarity.Uncommon, LootTier.Tier1, 35, L01);
            R("smg.thompson", "ПП Томпсона", "Thompson SMG", ItemCategory.Weapon, 1, 350, 1.5f, Rarity.Rare, LootTier.Tier2, 80, L12);

            // ------------------------------------------------------------------
            // ОГНЕСТРЕЛЬНОЕ — TIER 2 (бассейны): ПП и карабины
            // ------------------------------------------------------------------
            R("smg.mp5", "MP5A4", "MP5A4", ItemCategory.Weapon, 1, 400, 1.5f, Rarity.Rare, LootTier.Tier2, 100, L12);
            R("shotgun.pump", "Помповый дробовик", "Pump Shotgun", ItemCategory.Weapon, 1, 350, 1.6f, Rarity.Rare, LootTier.Tier2, 80, L12);
            R("shotgun.spas", "SPAS-12", "SPAS-12", ItemCategory.Weapon, 1, 400, 1.7f, Rarity.VeryRare, LootTier.Tier2, 125, L12);
            R("rifle.semiauto", "Semi-Automatic Rifle", "Semi-Automatic Rifle", ItemCategory.Weapon, 1, 400, 1.8f, Rarity.Rare, LootTier.Tier2, 100, L12);
            R("smg.vector", "Vector .45 ACP", "Vector .45 ACP", ItemCategory.Weapon, 1, 400, 1.4f, Rarity.VeryRare, LootTier.Tier3, 150, L2);
            R("rifle.m39", "M39-винтовка", "M39 Rifle", ItemCategory.Weapon, 1, 450, 1.9f, Rarity.VeryRare, LootTier.Tier2, 150, L12);
            R("rifle.lr300", "LR-300", "LR-300 Assault Rifle", ItemCategory.Weapon, 1, 450, 1.9f, Rarity.Military, LootTier.Tier3, 200, L2);
            R("rifle.ak", "Автомат Калашникова", "Assault Rifle (AK)", ItemCategory.Weapon, 1, 500, 2.1f, Rarity.Military, LootTier.Tier3, 250, L2);

            // ------------------------------------------------------------------
            // ОГНЕСТРЕЛЬНОЕ — TIER 3 (электростанция): военка
            // ------------------------------------------------------------------
            R("rifle.bolt", "Снайперская винтовка (болт)", "Bolt Action Rifle", ItemCategory.Weapon, 1, 500, 2.4f, Rarity.Military, LootTier.Tier3, 300, L2);
            R("lmg.m249", "M249", "M249", ItemCategory.Weapon, 1, 600, 4.0f, Rarity.Military, LootTier.Tier3, 400, L2);
            R("rifle.m16", "M16A2", "M16A2", ItemCategory.Weapon, 1, 500, 2.0f, Rarity.Military, LootTier.Tier3, 250, L2);
            R("hmlmg", "Тяжёлый пулемёт", "HMLMG", ItemCategory.Weapon, 1, 600, 4.5f, Rarity.Anomalous, LootTier.Tier3, 350, L2);
            R("minigun", "Миниган (турель станции)", "Minigun", ItemCategory.Weapon, 1, 700, 8.0f, Rarity.Anomalous, LootTier.Tier3, 500, L2);
            R("rocket.launcher", "РПГ-7 (пусковая)", "Rocket Launcher", ItemCategory.Weapon, 1, 300, 5.0f, Rarity.Military, LootTier.Tier3, 300, L2);
            R("multiplegrenadelauncher", "Револьверный гранатомёт MGL", "Multiple Grenade Launcher", ItemCategory.Weapon, 1, 400, 4.0f, Rarity.Military, LootTier.Tier3, 300, L2);
            R("flamethrower", "Огнемёт", "Flamethrower", ItemCategory.Weapon, 1, 400, 3.5f, Rarity.Military, LootTier.Tier3, 250, L2);

            // ------------------------------------------------------------------
            // ПАТРОНЫ
            // ------------------------------------------------------------------
            R("ammo.pistol", "9 мм патроны", "9mm Ammo", ItemCategory.Ammo, 128, 0, 0.01f);
            R("ammo.smg", "9 мм (ПП)", "9mm SMG Ammo", ItemCategory.Ammo, 128, 0, 0.01f, Rarity.Uncommon, LootTier.Tier2, 0, L12);
            R("ammo.rifle", "5.56 винтовочные", "5.56 Rifle Ammo", ItemCategory.Ammo, 128, 0, 0.02f, Rarity.Rare, LootTier.Tier2, 0, L12);
            R("ammo.rifle.hv", "5.56 HV", "5.56 HV Ammo", ItemCategory.Ammo, 128, 0, 0.02f, Rarity.VeryRare, LootTier.Tier3, 0, L2);
            R("ammo.rifle.incendiary", "5.56 зажигательные", "5.56 Incendiary", ItemCategory.Ammo, 128, 0, 0.02f, Rarity.VeryRare, LootTier.Tier3, 0, L2);
            R("ammo.rifle.explosive", "5.56 разрывные", "5.56 Explosive", ItemCategory.Ammo, 128, 0, 0.03f, Rarity.Military, LootTier.Tier3, 0, L2);
            R("ammo.shotgun", "12-й калибр (дробь)", "12 Gauge Buckshot", ItemCategory.Ammo, 64, 0, 0.05f);
            R("ammo.shotgun.slug", "12-й калибр (пуля)", "12 Gauge Slug", ItemCategory.Ammo, 64, 0, 0.05f, Rarity.Uncommon, LootTier.Tier2, 0, L12);
            R("ammo.handmade.shell", "Кустарный патрон 12к", "Handmade Shell", ItemCategory.Ammo, 64, 0, 0.05f);
            R("ammo.rocket.basic", "Ракета (обычная)", "Rocket (Basic)", ItemCategory.Ammo, 10, 0, 2.8f, Rarity.Military, LootTier.Tier3, 0, L2);
            R("ammo.rocket.hv", "Ракета HV", "Rocket (HV)", ItemCategory.Ammo, 10, 0, 2.8f, Rarity.Military, LootTier.Tier3, 0, L2);
            R("ammo.grenadelauncher.he", "40 мм ОФ", "40mm HE Grenade", ItemCategory.Ammo, 20, 0, 0.4f, Rarity.Military, LootTier.Tier3, 0, L2);
            R("ammo.grenadelauncher.smoke", "40 мм дым", "40mm Smoke", ItemCategory.Ammo, 20, 0, 0.4f, Rarity.Rare, LootTier.Tier2, 0, L12);
            R("arrow.wooden", "Стрела", "Wooden Arrow", ItemCategory.Ammo, 64, 0, 0.05f);
            R("arrow.bone", "Костяная стрела", "Bone Arrow", ItemCategory.Ammo, 64, 0, 0.05f, Rarity.Uncommon, LootTier.Tier1, 0, L01);
            R("arrow.fire", "Огненная стрела", "Fire Arrow", ItemCategory.Ammo, 64, 0, 0.08f, Rarity.Rare, LootTier.Tier2, 0, L12);
            R("ammo.nailgun", "Гвозди", "Nails", ItemCategory.Ammo, 128, 0, 0.01f, Rarity.Common, LootTier.Tier1, 0, L0);

            // ------------------------------------------------------------------
            // ОБВЕСЫ (Rust-like)
            // ------------------------------------------------------------------
            R("weapon.mod.holosight", "Голографический прицел", "Holosight", ItemCategory.Attachment, 1, 0, 0.2f, Rarity.Rare, LootTier.Tier2, 40, L12);
            R("weapon.mod.small.scope", "Малый прицел 4x", "8x Scope", ItemCategory.Attachment, 1, 0, 0.3f, Rarity.VeryRare, LootTier.Tier3, 80, L2);
            R("weapon.mod.lasersight", "ЛЦУ", "Laser Sight", ItemCategory.Attachment, 1, 0, 0.1f, Rarity.Rare, LootTier.Tier2, 25, L12);
            R("weapon.mod.flashlight", "Фонарь", "Weapon Flashlight", ItemCategory.Attachment, 1, 0, 0.1f, Rarity.Uncommon, LootTier.Tier1, 15, L01);
            R("weapon.mod.muzzleboost", "Ускоритель (Muzzle Boost)", "Muzzle Boost", ItemCategory.Attachment, 1, 0, 0.1f, Rarity.VeryRare, LootTier.Tier3, 50, L2);
            R("weapon.mod.muzzlebrake", "Дульный тормоз", "Muzzle Brake", ItemCategory.Attachment, 1, 0, 0.1f, Rarity.Rare, LootTier.Tier2, 40, L12);
            R("weapon.mod.extendedmags", "Расширенный магазин", "Extended Magazine", ItemCategory.Attachment, 1, 0, 0.2f, Rarity.VeryRare, LootTier.Tier2, 60, L12);
            R("weapon.mod.silencer", "Глушитель", "Silencer", ItemCategory.Attachment, 1, 0, 0.3f, Rarity.Military, LootTier.Tier3, 100, L2);
            R("weapon.mod.8x.scope", "Снайперский прицел", "16x Zoom Scope", ItemCategory.Attachment, 1, 0, 0.4f, Rarity.Military, LootTier.Tier3, 120, L2);

            // ------------------------------------------------------------------
            // БРОНЯ (точные цифры Rust)
            // ------------------------------------------------------------------
            // ================= СВОИ ПРЕДМЕТЫ УРОВНЕЙ (v1.0 alpha) =================
            // Ответ 11а: было по 2 предмета на уровень, стало по 4. Уровни «всё своё» —
            // тег уровня строго один (L0 / L1 / L2), вещи между уровнями не пересекаются.
            // У каждого предмета есть своя Blender-модель (IT_* в Models/Items).

            // --- L0 «Жёлтые коридоры» — быт уровня ---
            R("glow.mushroom", "Светогриб", "Glow Mushroom", ItemCategory.Food, 10, 0, 0.2f, Rarity.Uncommon, LootTier.Tier1, 8, L0);
            R("duct.tape", "Скотч", "Duct Tape", ItemCategory.Resource, 20, 0, 0.3f, Rarity.Common, LootTier.Tier1, 6, L0);
            R("lamp.portable", "Лампа-переноска", "Portable Lamp", ItemCategory.Tool, 1, 200, 1.0f, Rarity.Uncommon, LootTier.Tier1, 25, L0);
            R("respirator", "Респиратор", "Respirator", ItemCategory.Armor, 1, 220, 0.4f, Rarity.Uncommon, LootTier.Tier1, 30, L0);

            // --- L37 «Бассейны» — вода: нырять и пить ---
            R("diving.mask", "Маска ныряльщика", "Diving Mask", ItemCategory.Armor, 1, 320, 0.6f, Rarity.Uncommon, LootTier.Tier2, 45, L1);
            R("chlorine", "Хлорка", "Chlorine", ItemCategory.Resource, 20, 0, 0.4f, Rarity.Uncommon, LootTier.Tier2, 12, L1);
            R("flippers", "Ласты", "Flippers", ItemCategory.Armor, 1, 260, 0.8f, Rarity.Uncommon, LootTier.Tier2, 40, L1);
            R("oxygen.tank", "Кислородный баллон", "Oxygen Tank", ItemCategory.Tool, 1, 300, 3.0f, Rarity.Rare, LootTier.Tier2, 60, L1);

            // --- L3 «Электростанция» — ток: держаться подальше от напряжения ---
            R("rubber.gloves", "Диэлектрические перчатки", "Rubber Gloves", ItemCategory.Armor, 1, 280, 0.5f, Rarity.Rare, LootTier.Tier3, 60, L2);
            R("fuse.hi", "Силовой предохранитель", "High-Voltage Fuse", ItemCategory.Resource, 10, 0, 0.5f, Rarity.Rare, LootTier.Tier3, 40, L2);
            R("boots.rubber", "Резиновые сапоги", "Rubber Boots", ItemCategory.Armor, 1, 300, 1.2f, Rarity.Rare, LootTier.Tier3, 55, L2);
            R("wrench.insulated", "Изолированный ключ", "Insulated Wrench", ItemCategory.Tool, 1, 350, 1.4f, Rarity.VeryRare, LootTier.Tier3, 90, L2);
            R("hazmatsuit", "Костюм химзащиты", "Hazmat Suit", ItemCategory.Armor, 1, 500, 3.0f, Rarity.Rare, LootTier.Tier1, 30, L01);
            R("attire.hide.helterneck", "Кожаный жилет", "Hide Halterneck", ItemCategory.Armor, 1, 300, 1.0f, Rarity.Common, LootTier.Tier1, 10, L01);
            R("wood.armor.jacket", "Деревянная броня (торс)", "Wood Armor Jacket", ItemCategory.Armor, 1, 400, 3.0f, Rarity.Uncommon, LootTier.Tier1, 30, L01);
            R("wood.armor.pants", "Деревянная броня (ноги)", "Wood Armor Pants", ItemCategory.Armor, 1, 400, 2.5f, Rarity.Uncommon, LootTier.Tier1, 30, L01);
            R("bone.armor.suit", "Костяная броня", "Bone Armor Suit", ItemCategory.Armor, 1, 450, 4.0f, Rarity.Rare, LootTier.Tier2, 60, L12);
            R("roadsign.jacket", "Броня из дорожных знаков", "Road Sign Jacket", ItemCategory.Armor, 1, 600, 4.0f, Rarity.Rare, LootTier.Tier2, 60, L12);
            R("roadsign.kilt", "Килт из дорожных знаков", "Road Sign Kilt", ItemCategory.Armor, 1, 600, 3.5f, Rarity.Rare, LootTier.Tier2, 60, L12);
            R("coffeecan.helmet", "Шлем из банки", "Coffee Can Helmet", ItemCategory.Armor, 1, 400, 2.0f, Rarity.Rare, LootTier.Tier2, 50, L12);
            R("riot.helmet", "Противобунтарский шлем", "Riot Helmet", ItemCategory.Armor, 1, 700, 2.5f, Rarity.VeryRare, LootTier.Tier2, 90, L12);
            R("metal.facemask", "Металлическая маска", "Metal Facemask", ItemCategory.Armor, 1, 600, 2.5f, Rarity.VeryRare, LootTier.Tier3, 125, L2);
            R("metal.plate.torso", "Металлическая кираса", "Metal Chest Plate", ItemCategory.Armor, 1, 900, 5.0f, Rarity.Military, LootTier.Tier3, 200, L2);
            R("tactical.gloves", "Тактические перчатки", "Tactical Gloves", ItemCategory.Armor, 1, 300, 0.5f, Rarity.Rare, LootTier.Tier2, 25, L12);
            R("boots.tactical", "Тактические ботинки", "Tactical Boots", ItemCategory.Armor, 1, 300, 1.0f, Rarity.Rare, LootTier.Tier2, 25, L12);
            R("exoskeleton.suit", "Экзоскелет (аномальный)", "Exoskeleton Suit", ItemCategory.Armor, 1, 1200, 6.0f, Rarity.Anomalous, LootTier.Tier3, 400, L2);
            R("nightvision", "Прибор ночного видения", "Night Vision Goggles", ItemCategory.Armor, 1, 500, 1.0f, Rarity.Anomalous, LootTier.Tier3, 350, L2);
            R("hazmat.suit.reactor", "Реакторный костюм (рад-защита)", "Reactor Suit", ItemCategory.Armor, 1, 700, 4.0f, Rarity.Anomalous, LootTier.Tier3, 250, L2);

            // ------------------------------------------------------------------
            // ВЗРЫВЧАТКА / РЕЙД
            // ------------------------------------------------------------------
            R("explosive.timed", "C4", "Timed Explosive Charge", ItemCategory.Explosive, 10, 0, 1.0f, Rarity.VeryRare, LootTier.Tier3, 100, L2);
            R("explosive.satchel", "Сачель-заряд", "Satchel Charge", ItemCategory.Explosive, 10, 0, 0.5f, Rarity.Rare, LootTier.Tier2, 40, L12);
            R("explosive.beancan", "Бобовый заряд", "Beancan Grenade", ItemCategory.Explosive, 10, 0, 0.5f, Rarity.Uncommon, LootTier.Tier1, 15, L01);
            R("grenade.f1", "Граната Ф-1", "F1 Grenade", ItemCategory.Explosive, 10, 0, 0.5f, Rarity.Rare, LootTier.Tier2, 60, L12);
            R("grenade.smoke", "Дымовая граната", "Smoke Grenade", ItemCategory.Explosive, 10, 0, 0.5f, Rarity.Rare, LootTier.Tier2, 30, L12);
            R("grenade.flashbang", "Светошумовая граната", "Flashbang", ItemCategory.Explosive, 10, 0, 0.5f, Rarity.Rare, LootTier.Tier2, 30, L12);
            R("grenade.molotov", "Коктейль Молотова", "Molotov Cocktail", ItemCategory.Explosive, 10, 0, 0.5f, Rarity.Uncommon, LootTier.Tier1, 20, L01);
            R("mine.landmine", "Противопехотная мина", "Landmine", ItemCategory.Explosive, 5, 0, 1.0f, Rarity.VeryRare, LootTier.Tier3, 60, L2);
            R("tool.rocket.launcher.sight", "Прицел РПГ", "Rocket Launcher Sight", ItemCategory.Attachment, 1, 0, 0.3f, Rarity.Military, LootTier.Tier3, 50, L2);

            // ------------------------------------------------------------------
            // СТРОИТЕЛЬСТВО И ДЕПЛОИ (Rust-совместимо)
            // ------------------------------------------------------------------
            R("building.planner", "План застройки", "Building Plan", ItemCategory.Building, 1, 400, 1.0f, Rarity.Common, LootTier.Tier1, 5, L01);
            R("door.hinged.wood", "Деревянная дверь", "Wooden Door", ItemCategory.Deployable, 3, 0, 2.0f);
            R("door.hinged.metal", "Металлическая дверь", "Sheet Metal Door", ItemCategory.Deployable, 3, 0, 2.5f, Rarity.Uncommon, LootTier.Tier1, 25, L01);
            R("door.hinged.toptier", "Бронированная дверь", "Armored Door", ItemCategory.Deployable, 3, 0, 3.0f, Rarity.VeryRare, LootTier.Tier3, 75, L2);
            R("door.double.hinged.metal", "Двойная металлическая дверь", "Double Sheet Metal Door", ItemCategory.Deployable, 3, 0, 3.0f, Rarity.Rare, LootTier.Tier2, 40, L12);
            R("lock.key", "Замок с ключом", "Key Lock", ItemCategory.Deployable, 5, 0, 0.2f, Rarity.Common, LootTier.Tier1, 10, L01);
            R("lock.code", "Кодовый замок", "Code Lock", ItemCategory.Deployable, 5, 0, 0.3f, Rarity.Uncommon, LootTier.Tier1, 15, L01);
            R("woodbox", "Деревянный ящик", "Wood Storage Box", ItemCategory.Deployable, 5, 0, 3.0f);
            R("box.wooden.large", "Большой деревянный ящик", "Large Wood Box", ItemCategory.Deployable, 5, 0, 4.0f, Rarity.Uncommon, LootTier.Tier1, 20, L01);
            R("furnace", "Печь", "Furnace", ItemCategory.Deployable, 3, 0, 5.0f, Rarity.Common, LootTier.Tier1, 30, L01);
            R("furnace.large", "Большая печь", "Large Furnace", ItemCategory.Deployable, 3, 0, 8.0f, Rarity.Uncommon, LootTier.Tier2, 60, L12);
            R("workbench1", "Верстак 1 уровня", "Workbench Level 1", ItemCategory.Deployable, 1, 0, 8.0f, Rarity.Common, LootTier.Tier1, 30, L01);
            R("workbench2", "Верстак 2 уровня", "Workbench Level 2", ItemCategory.Deployable, 1, 0, 12.0f, Rarity.Uncommon, LootTier.Tier2, 100, L12);
            R("workbench3", "Верстак 3 уровня", "Workbench Level 3", ItemCategory.Deployable, 1, 0, 16.0f, Rarity.Rare, LootTier.Tier3, 300, L2);
            R("cupboard.tool", "Шкаф с инструментами (Tool Cupboard)", "Tool Cupboard", ItemCategory.Deployable, 3, 0, 5.0f, Rarity.Common, LootTier.Tier1, 25, L01);
            R("generator.wind.scrap", "Генератор (скрап)", "Scrap Wind Turbine", ItemCategory.Deployable, 1, 0, 10.0f, Rarity.Rare, LootTier.Tier2, 100, L12);
            R("sleepingbag", "Спальный мешок", "Sleeping Bag", ItemCategory.Deployable, 5, 0, 1.0f);
            R("bed", "Кровать", "Bed", ItemCategory.Deployable, 3, 0, 3.0f, Rarity.Uncommon, LootTier.Tier1, 25, L01);
            R("flameturret", "Огненная турель", "Flame Turret", ItemCategory.Deployable, 1, 0, 6.0f, Rarity.Military, LootTier.Tier3, 200, L2);
            R("autoturret", "Автотурель", "Auto Turret", ItemCategory.Deployable, 1, 0, 10.0f, Rarity.Military, LootTier.Tier3, 250, L2);
            R("samsite", "ПВО (SAM)", "SAM Site", ItemCategory.Deployable, 1, 0, 12.0f, Rarity.Military, LootTier.Tier3, 300, L2);
            R("sign.pictureframe", "Табличка", "Sign", ItemCategory.Deployable, 5, 0, 1.0f);
            R("wall.frame.cell.gate", "Решётчатые ворота", "Cell Gate", ItemCategory.Deployable, 3, 0, 2.0f, Rarity.Uncommon, LootTier.Tier2, 30, L12);
            R("barricade.concrete", "Бетонная баррикада", "Concrete Barricade", ItemCategory.Deployable, 5, 0, 4.0f, Rarity.Uncommon, LootTier.Tier1, 20, L01);
            R("barricade.metal", "Металлическая баррикада", "Metal Barricade", ItemCategory.Deployable, 5, 0, 4.0f, Rarity.Rare, LootTier.Tier2, 30, L12);
            R("trap.spikes", "Шипы", "Wooden Spikes", ItemCategory.Deployable, 3, 0, 3.0f, Rarity.Uncommon, LootTier.Tier1, 20, L01);
            R("trap.bear", "Медвежий капкан", "Bear Trap", ItemCategory.Deployable, 3, 0, 3.0f, Rarity.Rare, LootTier.Tier2, 25, L12);
            R("water.purifier", "Очиститель воды", "Water Purifier", ItemCategory.Deployable, 3, 0, 2.0f, Rarity.Rare, LootTier.Tier2, 30, L12);

            // ---- 26_transport: самокаты, тележки для лута (вагонетки и лифты — объекты карты)
            R("cart.loot", "Тележка для лута", "Loot Cart", ItemCategory.Deployable, 1, 400f, 12.0f, Rarity.Uncommon, LootTier.Tier1, 40, L01);
            R("scooter", "Самокат", "Scooter", ItemCategory.Deployable, 1, 150f, 8.0f, Rarity.Uncommon, LootTier.Tier1, 30, L01);
            R("elevator.part", "Комплект лифта (ремонт)", "Elevator Kit", ItemCategory.Backrooms, 3, 0, 6.0f, Rarity.Rare, LootTier.Tier3, 60, L2);

            // ---- 39_trading: торговые точки
            R("vending.machine", "Вендинг-автомат", "Vending Machine", ItemCategory.Deployable, 1, 600f, 14.0f, Rarity.Rare, LootTier.Tier2, 120, L12);
            R("trade.token", "Торговая марка", "Trade Token", ItemCategory.Backrooms, 100, 0, 0.05f, Rarity.Uncommon, LootTier.Tier2, 15, L12);

            // ------------------------------------------------------------------
            // ИНСТРУМЕНТЫ
            // ------------------------------------------------------------------
            R("tool.camera", "Фотоаппарат (Backrooms-записи)", "Camera", ItemCategory.Tool, 1, 200, 0.5f, Rarity.Rare, LootTier.Tier1, 20, L0);
            R("tool.geiger", "Дозиметр", "Geiger Counter", ItemCategory.Tool, 1, 300, 0.5f, Rarity.Rare, LootTier.Tier2, 40, L12);
            R("flashlight", "Фонарь", "Flashlight", ItemCategory.Tool, 1, 300, 0.5f, Rarity.Common, LootTier.Tier1, 10, L0);
            R("torch.lantern", "Керосиновая лампа", "Lantern", ItemCategory.Tool, 1, 400, 0.5f, Rarity.Uncommon, LootTier.Tier1, 15, L01);
            R("repair.bench", "Ремонтный верстак", "Repair Bench", ItemCategory.Deployable, 1, 0, 8.0f, Rarity.Rare, LootTier.Tier2, 75, L12);
            R("research.table", "Стол исследований", "Research Table", ItemCategory.Deployable, 1, 0, 8.0f, Rarity.Rare, LootTier.Tier2, 75, L12);
            R("tool.binoculars", "Бинокль", "Binoculars", ItemCategory.Tool, 1, 200, 0.5f, Rarity.Rare, LootTier.Tier2, 30, L12);
            R("tool.keycard.red", "Красная ключ-карта (реактор)", "Red Keycard", ItemCategory.Backrooms, 1, 0, 0.1f, Rarity.Anomalous, LootTier.Tier3, 0, L2);
            R("tool.keycard.blue", "Синяя ключ-карта (насосная)", "Blue Keycard", ItemCategory.Backrooms, 1, 0, 0.1f, Rarity.VeryRare, LootTier.Tier2, 0, L1);
            R("tool.keycard.green", "Зелёная ключ-карта (офисы L0)", "Green Keycard", ItemCategory.Backrooms, 1, 0, 0.1f, Rarity.Rare, LootTier.Tier1, 0, L0);
        }
    }
}
