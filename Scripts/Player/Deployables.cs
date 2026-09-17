// ============================================================================
//  SUBSISTENCE — Player/Deployables.cs
//  Деплои: Tool Cupboard, верстаки (1-3), печи, ящики, кровати/спальники,
//  водяной очиститель, ремонтный верстак, стол исследований, автотурели, SAM, ловушки.
//  Всё это — часть «Rust-копии»: у каждого своя логика и свои меню.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Net;

namespace Subsistence.Building
{
    /// <summary>Базовый деплой: владелец, здоровье, апкип-привязка к постройке.</summary>
    public class BuildDeployable : MonoBehaviour, Subsistence.Combat.IDestructible
    {
        public string itemId;
        public ulong ownerId;
        public float health = 100f;
        public uint buildingId;
        public bool requiresPrivilege = true;
        public bool IsAlive => health > 0f;

        public virtual void Initialize(ulong owner, uint building)
        {
            ownerId = owner; buildingId = building;
            var def = ItemDatabase.Def(itemId);
            if (def != null) health = Mathf.Max(50f, def.maxDurability > 0 ? def.maxDurability : 100f);
        }

        public virtual void OnUse(Subsistence.Player.PlayerController player) { }

        public virtual void ApplyDamage(DamageType type, float amount, Vector3 from, HitRegion region)
        {
            if (!NetworkBridge.IsServer) return;
            health -= amount * RaidTable.Multiplier(type, BuildMaterialClass.Metal);
            if (health <= 0f) Destroy(gameObject);
        }

        public void ApplyDamage(DamageType type, float amount) => ApplyDamage(type, amount, transform.position, HitRegion.Body);
        public virtual void OnBlockDestroyed() { }
    }

    /// <summary>Ящик/шкаф: контейнер с сеткой предметов (как в Rust: 24 слота).</summary>
    public class StorageBox : BuildDeployable
    {
        public int slots = 24;
        public ItemContainer storage;

        void Awake()
        {
            itemId = string.IsNullOrEmpty(itemId) ? "box.wooden.large" : itemId;
            storage = new ItemContainer("Box", slots);
        }

        public override void OnUse(Subsistence.Player.PlayerController player)
        {
            // 30–31: содержимым ящика владеет сервер — в сети он пришлёт его при открытии.
            Subsistence.UI.InventoryUI.OpenDeployable(this, storage);
        }
    }

    /// <summary>Верстак: гейтит крафт (1-3 уровень), ускоряет крафт, открывает исследования.</summary>
    public class Workbench : BuildDeployable
    {
        public int level = 1;
        public float craftSpeedMult = 1f;

        Subsistence.Crafting.WorkbenchStation _station;

        void Awake()
        {
            if (string.IsNullOrEmpty(itemId)) itemId = "workbench" + level;
            EnsureStation();
        }

        /// <summary>Физический верстак регистрируется в системе: радиус и скорость крафта.</summary>
        public Subsistence.Crafting.WorkbenchStation EnsureStation()
        {
            if (_station == null) _station = GetComponent<Subsistence.Crafting.WorkbenchStation>();
            if (_station == null) _station = gameObject.AddComponent<Subsistence.Crafting.WorkbenchStation>();
            _station.level = level;
            craftSpeedMult = _station.SpeedMult;
            return _station;
        }

        public override void OnUse(Subsistence.Player.PlayerController player)
        {
            EnsureStation();
            // Ответ 1б: апгрейда на месте нет — только снос киянкой и постройка нового.
            // Ответ 4а: чужим верстаком пользуются все (как в Rust) — проверок владельца нет,
            // а если предмет следующего тира в руках, просто подсказываем, что апгрейда больше нет.
            var inv = player != null ? player.inventory : null;
            if (level < 3 && inv != null && inv.CountOf("workbench" + (level + 1)) > 0)
                _station.ExplainNoUpgrade();
            Subsistence.UI.InventoryUI.OpenCrafting(level);
        }
    }

    /// <summary>Печь: плавит руду в металл/серу, жрёт дерево/уголь (Rust-циклы).</summary>
    public class Furnace : BuildDeployable
    {
        public int inputSlots = 4, outputSlots = 4;
        public ItemContainer input, output;
        public float smeltSecondsPerUnit = 2.5f;
        public bool isLarge;

        float _timer;

        void Awake()
        {
            if (string.IsNullOrEmpty(itemId)) itemId = isLarge ? "furnace.large" : "furnace";
            input = new ItemContainer("Furnace Input", inputSlots, ItemCategory.Resource);
            output = new ItemContainer("Furnace Output", outputSlots);
        }

        void Update()
        {
            if (!NetworkBridge.IsServer) return;
            _timer += Time.deltaTime;
            float step = smeltSecondsPerUnit * (isLarge ? 0.6f : 1f);
            if (_timer < step) return;
            _timer = 0f;
            if (SmeltOnce()) InventoryNet.ServerContainersChanged(this);   // открытые окна у игроков обновятся
        }

        bool SmeltOnce()
        {
            // топливо
            if (!BurnFuel()) return false;
            // руда → слиток (металл/серу), дерево → уголь
            if (TryConvert("metal.ore", "metal.fragments", 1, 1)) return true;
            if (TryConvert("sulfur.ore", "sulfur", 1, 1)) return true;
            if (TryConvert("wood", "charcoal", 2, 1)) return true;
            return true;      // топливо сгорело — вход всё равно изменился
        }

        bool BurnFuel()
        {
            if (input.CountOf("wood") > 0) return input.RemoveAmount("wood", 1);
            if (input.CountOf("charcoal") > 0) return input.RemoveAmount("charcoal", 1);
            if (input.CountOf("lowgradefuel") > 0) return input.RemoveAmount("lowgradefuel", 1);
            return false;
        }

        bool TryConvert(string from, string to, int ratio, int outAmount)
        {
            if (input.CountOf(from) < ratio) return false;
            input.RemoveAmount(from, ratio);
            output.TryAddAmount(to, outAmount);
            return true;
        }

        public override void OnUse(Subsistence.Player.PlayerController player)
            => Subsistence.UI.InventoryUI.OpenDeployable(this, input, output);   // вход + выход (сеть)
    }

    /// <summary>Ремонтный верстак: ремонт за ресурсы с потерей качества 25 % (Rust-механика).</summary>
    public class RepairBench : BuildDeployable
    {
        public override void OnUse(Subsistence.Player.PlayerController player)
        {
            var item = player.inventory?.ActiveItem;
            if (item == null) { Subsistence.UI.HudRuntime.ShowToast("Возьми предмет в руки"); return; }
            var def = item.Def;
            if (def == null || !def.HasDurability) { Subsistence.UI.HudRuntime.ShowToast("Это не ремонтируется"); return; }
            if (item.durability >= def.maxDurability * item.condition)
            { Subsistence.UI.HudRuntime.ShowToast("Предмет не требует ремонта"); return; }

            int cost = Mathf.CeilToInt((def.maxDurability - item.durability) * 0.1f);
            if (player.inventory.CountOf("metal.fragments") < cost)
            { Subsistence.UI.HudRuntime.ShowToast($"Нужно {cost} металлолома"); return; }

            player.inventory.RemoveAmount("metal.fragments", cost);
            item.Repair(1f);
            item.condition = Mathf.Max(0.1f, item.condition * 0.75f);   // каждый ремонт хуже
            Subsistence.UI.HudRuntime.ShowToast($"Отремонтировано (качество {item.condition * 100f:F0} %)");
        }
    }

    /// <summary>Стол исследований: разобрать предмет → изучить → крафтить (Rust-прогрессия).</summary>
    public class ResearchTable : BuildDeployable
    {
        /// <summary>
        /// Стол исследований: открывает список (хлам, тир верстака, прогресс). Раньше он умел
        /// только «изучить предмет в руках» без UI и без привязки к верстакам — теперь это
        /// полноценное дерево (Crafting/TechTree.cs): тир исследований задаёт верстак рядом.
        /// </summary>
        public override void OnUse(Subsistence.Player.PlayerController player)
        {
            if (player == null || player.inventory == null) return;
            int bench = Subsistence.Crafting.WorkbenchSystem.Instance != null
                        ? Subsistence.Crafting.WorkbenchSystem.Instance.CurrentLevel : 1;

            // Быстрый путь, как в Rust: предмет в руках → изучение одним нажатием
            var item = player.inventory.ActiveItem;
            if (item != null && item.Def != null && !Subsistence.Crafting.TechTree.IsKnown(player.OwnerId, item.id))
            {
                string err = Subsistence.Crafting.TechTree.TryResearch(player.OwnerId, player.inventory, item.id, bench);
                if (err == null) return;                                  // изучили — стол можно не открывать
                Subsistence.UI.HudRuntime.ShowToast(err);
            }
            Subsistence.Crafting.ResearchUI.Open(player.inventory, player.OwnerId, bench);
        }
    }

    /// <summary>Кровать/спальный мешок: точка возрождения (частично «съедает» рассудок).</summary>
    public class SleepBag : BuildDeployable
    {
        public bool isBed;
        public int resetsLeft = 3;

        public override void OnUse(Subsistence.Player.PlayerController player)
        {
            if (resetsLeft <= 0) { Subsistence.UI.HudRuntime.ShowToast("Мешок больше не работает"); return; }
            player.Respawn(transform.position + Vector3.up * 0.5f);
            resetsLeft--;
        }
    }

    /// <summary>Автотурель: стреляет по всем, кто не авторизован в TC (Rust-логика).</summary>
    public class AutoTurret : BuildDeployable
    {
        public float range = 30f;
        public float rpm = 200f;
        public float damage = 12f;
        public float ammo = 256f;
        public float rotateSpeed = 120f;
        Transform _head;
        float _nextShot;

        void Awake() { _head = transform.Find("Head") ?? transform; }

        void Update()
        {
            if (!NetworkBridge.IsServer) return;
            var target = FindTarget();
            if (target == null) return;
            Vector3 dir = (target.position - _head.position).normalized;
            _head.rotation = Quaternion.RotateTowards(_head.rotation, Quaternion.LookRotation(dir), rotateSpeed * Time.deltaTime);
            if (Vector3.Angle(_head.forward, dir) > 4f) return;
            if (Time.time < _nextShot || ammo <= 0f) return;
            _nextShot = Time.time + 60f / rpm;
            ammo -= 1f;
            var d = target.GetComponentInParent<Subsistence.Combat.IDestructible>();
            d?.ApplyDamage(DamageType.Bullet, damage, transform.position, HitRegion.Body);
        }

        Transform FindTarget()
        {
            var cols = Physics.OverlapSphere(transform.position, range, Layers.Mask(Layers.Player, Layers.Monster), QueryTriggerInteraction.Ignore);
            Transform best = null; float bd = float.MaxValue;
            for (int i = 0; i < cols.Length; i++)
            {
                var pc = cols[i].GetComponentInParent<Subsistence.Player.PlayerController>();
                if (pc != null)
                {
                    if (SubsistencePlayerIsAuthorized(pc)) continue;
                }
                float d = Vector3.Distance(cols[i].transform.position, transform.position);
                if (d < bd) { bd = d; best = cols[i].transform; }
            }
            return best;
        }

        bool SubsistencePlayerIsAuthorized(Subsistence.Player.PlayerController pc)
        {
            var tcs = FindObjectsOfType<ToolCupboard>();
            for (int i = 0; i < tcs.Length; i++)
                if (tcs[i].buildingId == buildingId && tcs[i].IsAuthorized(pc.OwnerId)) return true;
            return false;
        }
    }

    /// <summary>Очиститель воды: Poolrooms-вода → бутылки питьевой (важно для выживания).</summary>
    public class WaterPurifier : BuildDeployable
    {
        public float secondsPerBottle = 8f;
        float _timer;

        void Update()
        {
            if (!NetworkBridge.IsServer) return;
            _timer += Time.deltaTime;
            if (_timer < secondsPerBottle) return;
            _timer = 0f;
            // требует 1 бутылку грязной воды + немного топлива
        }

        public override void OnUse(Subsistence.Player.PlayerController player)
        {
            if (player.inventory.CountOf("water.dirty") <= 0)
            { Subsistence.UI.HudRuntime.ShowToast("Нужна грязная вода"); return; }
            player.inventory.RemoveAmount("water.dirty", 1);
            player.inventory.TryAddAmount("water.bottle", 1);
        }
    }

    /// <summary>SAM-сайт: сбивает ракеты (защита от ракетных рейдов как в Rust).</summary>
    public class SamSite : BuildDeployable
    {
        public float range = 60f;
        public float cooldown = 4f;
        float _ready;

        void Update()
        {
            if (!NetworkBridge.IsServer || Time.time < _ready) return;
            // ищем снаряды в радиусе (слой Projectile)
            var cols = Physics.OverlapSphere(transform.position, range, Layers.Mask(Layers.Projectile), QueryTriggerInteraction.Collide);
            if (cols.Length == 0) return;
            _ready = Time.time + cooldown;
            Destroy(cols[0].gameObject);
        }
    }

    /// <summary>Система исследований (крафт-прогрессия): что игрок уже знает.</summary>
    /// <summary>
    /// Совместимость: старый API исследований. Вся логика теперь в Crafting/TechTree.cs
    /// (цены по редкости и тиру, привязка к верстакам, UI стола исследований).
    /// </summary>
    public static class ResearchSystem
    {
        public static bool IsUnlocked(ulong player, string itemId) => Subsistence.Crafting.TechTree.IsKnown(player, itemId);
        public static void Unlock(ulong player, string itemId) => Subsistence.Crafting.TechTree.Grant(player, itemId);
        public static int CostOf(string itemId) => Subsistence.Crafting.TechTree.CostOf(itemId);
        public static int KnownCount(ulong player) => Subsistence.Crafting.TechTree.KnownCount(player);
    }
}
