// ============================================================================
//  SUBSISTENCE — Combat/Armor.cs
//  Броня 1:1 как в Rust: резисты по типам урона, деградация, зоны попадания,
//  полные сеты (hazmat → roadsign → metal → exoskeleton). Хазмат-костюм из
//  референса обязателен: единственная защита от радиации на электростанции.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;

namespace Subsistence.Combat
{
    [Serializable]
    public struct ArmorStats
    {
        public string id;
        public EquipSlot slot;
        public float projectile;     // резист от пуль (0..1)
        public float buckshot;       // от дроби
        public float blunt;          // от тупого
        public float slash;          // от режущего
        public float stab;           // от колющего
        public float explosion;      // от взрывов
        public float fire;           // от огня
        public float radiation;      // от радиации (hazmat!)
        public float cold;           // от холода
        public float durabilityLossPerHit;  // 0 = взять умолчание (см. static ArmorTable)
        public float moveSpeedMult;         // металл замедляет (как в Rust); 0 = 1.0
    }

    /// <summary>Характеристики брони — цифры близки к Rust (hazmat: 4 % bullet, 100 % rad).</summary>
    public static class ArmorTable
    {
        // C# 9 (Unity 2022.3) не разрешает инициализаторы полей в структуре — поэтому
        // умолчания раскидываем по таблице здесь. Статический конструктор выполняется
        // ПОСЛЕ инициализатора поля _t, так что словарь уже собран.
        static ArmorTable()
        {
            var keys = new List<string>(_t.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var s = _t[keys[i]];
                if (s.durabilityLossPerHit <= 0f) s.durabilityLossPerHit = 0.5f;
                if (s.moveSpeedMult <= 0f) s.moveSpeedMult = 1f;
                _t[keys[i]] = s;
            }
        }

        static readonly Dictionary<string, ArmorStats> _t = new Dictionary<string, ArmorStats>
        {
            // Полный хазмат-сет = один предмет, закрывающий торс/ноги/голову (как в игре)
            { "hazmatsuit",      new ArmorStats { id="hazmatsuit", slot=EquipSlot.Chest, projectile=0.04f, buckshot=0.06f, blunt=0.02f, slash=0.05f, stab=0.05f, explosion=0.02f, fire=0.10f, radiation=0.98f, cold=0.65f, durabilityLossPerHit=1.2f, moveSpeedMult=1.0f } },
            { "attire.hide.helterneck", new ArmorStats { id="attire.hide.helterneck", slot=EquipSlot.Chest, projectile=0.05f, buckshot=0.07f, blunt=0.03f, slash=0.10f, stab=0.05f, explosion=0.02f, fire=0.03f, moveSpeedMult=1.0f } },
            { "wood.armor.jacket", new ArmorStats { id="wood.armor.jacket", slot=EquipSlot.Chest, projectile=0.20f, buckshot=0.25f, blunt=0.05f, slash=0.15f, stab=0.10f, explosion=0.05f, durabilityLossPerHit=1.0f } },
            { "wood.armor.pants",  new ArmorStats { id="wood.armor.pants", slot=EquipSlot.Legs, projectile=0.18f, buckshot=0.22f, blunt=0.05f, slash=0.14f, stab=0.10f, explosion=0.05f, durabilityLossPerHit=1.0f } },
            { "bone.armor.suit",   new ArmorStats { id="bone.armor.suit", slot=EquipSlot.Chest, projectile=0.28f, buckshot=0.32f, blunt=0.12f, slash=0.25f, stab=0.15f, explosion=0.08f, durabilityLossPerHit=1.1f } },
            { "roadsign.jacket",   new ArmorStats { id="roadsign.jacket", slot=EquipSlot.Chest, projectile=0.45f, buckshot=0.55f, blunt=0.20f, slash=0.30f, stab=0.35f, explosion=0.20f, durabilityLossPerHit=1.4f, moveSpeedMult=0.98f } },
            { "roadsign.kilt",     new ArmorStats { id="roadsign.kilt", slot=EquipSlot.Legs, projectile=0.42f, buckshot=0.52f, blunt=0.20f, slash=0.30f, stab=0.35f, explosion=0.20f, durabilityLossPerHit=1.4f } },
            { "coffeecan.helmet",  new ArmorStats { id="coffeecan.helmet", slot=EquipSlot.Head, projectile=0.40f, buckshot=0.50f, blunt=0.25f, slash=0.35f, stab=0.30f, explosion=0.15f, durabilityLossPerHit=1.3f } },
            { "riot.helmet",       new ArmorStats { id="riot.helmet", slot=EquipSlot.Head, projectile=0.55f, buckshot=0.65f, blunt=0.45f, slash=0.50f, stab=0.40f, explosion=0.25f, durabilityLossPerHit=1.5f } },
            { "metal.facemask",    new ArmorStats { id="metal.facemask", slot=EquipSlot.Face, projectile=0.68f, buckshot=0.80f, blunt=0.40f, slash=0.45f, stab=0.50f, explosion=0.35f, durabilityLossPerHit=2.0f } },
            { "metal.plate.torso", new ArmorStats { id="metal.plate.torso", slot=EquipSlot.Chest, projectile=0.68f, buckshot=0.85f, blunt=0.50f, slash=0.55f, stab=0.60f, explosion=0.45f, durabilityLossPerHit=2.2f, moveSpeedMult=0.95f } },
            { "tactical.gloves",   new ArmorStats { id="tactical.gloves", slot=EquipSlot.Hands, projectile=0.06f, slash=0.15f, stab=0.10f, fire=0.20f } },
            { "boots.tactical",    new ArmorStats { id="boots.tactical", slot=EquipSlot.Feet, projectile=0.05f, blunt=0.10f, cold=0.25f } },
            { "exoskeleton.suit",  new ArmorStats { id="exoskeleton.suit", slot=EquipSlot.Chest, projectile=0.80f, buckshot=0.90f, blunt=0.65f, slash=0.70f, stab=0.75f, explosion=0.60f, fire=0.55f, radiation=0.85f, cold=0.80f, durabilityLossPerHit=2.6f, moveSpeedMult=0.92f } },
            { "nightvision",       new ArmorStats { id="nightvision", slot=EquipSlot.Head, durabilityLossPerHit=0.05f, moveSpeedMult=1f } },
            { "hazmat.suit.reactor", new ArmorStats { id="hazmat.suit.reactor", slot=EquipSlot.Chest, projectile=0.15f, buckshot=0.20f, slash=0.20f, stab=0.20f, explosion=0.10f, fire=0.45f, radiation=1.0f, cold=0.7f, durabilityLossPerHit=1.5f } },
        };

        public static bool TryGet(string id, out ArmorStats s) => _t.TryGetValue(id, out s);
        public static bool IsArmor(string id) => _t.ContainsKey(id);

        public static float ResistFor(DamageType type, ArmorStats a)
        {
            switch (type)
            {
                case DamageType.Bullet: return a.projectile;
                case DamageType.Buckshot: return a.buckshot;
                case DamageType.Blunt: return a.blunt;
                case DamageType.Slash: return a.slash;
                case DamageType.Stab: return a.stab;
                case DamageType.Explosion: return a.explosion;
                case DamageType.Fire: return a.fire;
                case DamageType.Radiation: return a.radiation;
                case DamageType.Cold: return a.cold;
                case DamageType.Bite: return a.slash;      // укусы — как режущий
                default: return 0f;
            }
        }
    }

    /// <summary>Считает суммарные резисты по надетым предметам + износ брони от ударов.</summary>
    public class ArmorSystem : MonoBehaviour
    {
        public PlayerInventory inventory;

        [Tooltip("Покрытие зон: во что одет — то и защищает (шлем не спасает ноги)")]
        public float headCoverage = 1f, chestCoverage = 1f, legsCoverage = 1f;

        float _speedMult = 1f;
        public float MoveSpeedMultiplier => _speedMult;

        void LateUpdate() => Recalculate();

        public void Recalculate()
        {
            _speedMult = 1f;
            if (inventory == null) return;
            foreach (var item in inventory.AllEquipped())
            {
                if (item?.Def == null) continue;
                if (!ArmorTable.TryGet(item.id, out var a)) continue;
                float cond = item.DurabilityPercent;             // сломанная броня не защищает
                _speedMult *= Mathf.Lerp(1f, a.moveSpeedMult, cond);
            }
        }

        /// <summary>Суммарный резист по типу урона (использует SurvivalSystem).</summary>
        public float Resist(DamageType type)
            => Resist(type, HitRegion.Body);

        public float Resist(DamageType type, HitRegion region)
        {
            if (inventory == null) return 0f;
            float coverageMult = region == HitRegion.Head ? headCoverage : region == HitRegion.Legs ? legsCoverage : chestCoverage;
            // Резисты не складываются линейно: 1 - Π(1 - r_i) — как в Rust
            float remaining = 1f;
            foreach (var item in inventory.AllEquipped())
            {
                if (item?.Def == null) continue;
                if (!ArmorTable.TryGet(item.id, out var a)) continue;
                float cond = item.DurabilityPercent;
                float r = ArmorTable.ResistFor(type, a) * cond * coverageMult;
                remaining *= (1f - Mathf.Clamp01(r));
            }
            return 1f - remaining;
        }

        /// <summary>Износ надетой брони при получении урона (шлем бьётся, когда бьют в голову).</summary>
        public void WearArmor(DamageType type, HitRegion region, float rawDamage)
        {
            if (inventory == null) return;
            if (type == DamageType.Radiation && rawDamage < 1f) return;   // пассивный фон не пилит костюм
            foreach (var item in inventory.AllEquipped())
            {
                if (item?.Def == null) continue;
                if (!ArmorTable.TryGet(item.id, out var a)) continue;
                bool relevant = region == HitRegion.Head ? (a.slot == EquipSlot.Head || a.slot == EquipSlot.Face)
                              : region == HitRegion.Legs ? (a.slot == EquipSlot.Legs || a.slot == EquipSlot.Chest)
                              : (a.slot == EquipSlot.Chest);
                if (!relevant && item.id != "hazmatsuit") continue;
                item.Wear(a.durabilityLossPerHit * Mathf.Clamp(rawDamage / 25f, 0.25f, 3f));
            }
        }

        /// <summary>Полный сет хазмата — снимается отдельным предметом «hazmatsuit» (как в Rust).</summary>
        public bool IsFullHazmat
        {
            get
            {
                if (inventory == null) return false;
                var chest = inventory.GetEquipped(EquipSlot.Chest);
                return chest != null && (chest.id == "hazmatsuit" || chest.id == "hazmat.suit.reactor") && chest.DurabilityPercent > 0.2f;
            }
        }
    }
}
