// ============================================================================
//  SUBSISTENCE — Core/IDs.cs
//  Единые идентификаторы: типы урона, тиры лута/построек, категории предметов.
//  ВАЖНО: эти enum'ы сериализуются в сеть и в сейвы — НЕ меняй порядок значений,
//  только добавляй новые в конец (иначе поедут старые сейвы и снапшоты).
// ============================================================================
using System;

namespace Subsistence.Core
{
    /// <summary>Типы урона. Совпадают с логикой Rust: броня резистит по типу, а не по величине.</summary>
    public enum DamageType : byte
    {
        Generic = 0,   // падение, скриптовые эффекты
        Bullet = 1,    // винтовки, пистолеты, SMG
        Buckshot = 2,  // дробовики (урон по пеллетам, сильно резистится металлом)
        Slash = 3,     // мачете, топор, когти Hound
        Stab = 4,      // нож, копьё, штык
        Blunt = 5,     // кувалда, камни, кулаки, Толстяк
        Bite = 6,      // Skin-Stealer, Партигуер
        Explosion = 7, // C4, сачель, ракеты, гранаты
        Fire = 8,      // огнемёт, коктейль, горящий танк
        Radiation = 9, // реакторный зал, бочки, Badlands-зона
        Cold = 10,     // мокрый в Poolrooms ночью
        Drowning = 11, // забыл вдохнуть под водой
        Electric = 12  // Искровик и оголённые кабели электростанции
        // Sanity = 12 — УДАЛЕНО по решению 12_sanity («убрать рассудок»), см. docs/ANSWERS.md
    }

    /// <summary>Уровень (этаж) Backrooms. Определяет тир лута, опасность и монстров.</summary>
    public enum LevelTheme : byte
    {
        Corridors = 0,     // Level 0 — жёлтые обои, ковролин, гудящие лампы
        Poolrooms = 1,     // Level 37 — плитка, вода, эхо, Hound
        PowerStation = 2   // Level 3 — бетон, реактор, радиация, Skin-Stealer
    }

    /// <summary>Тир лута. Привязан к уровню: 1 — коридоры, 2 — бассейны, 3 — станция.</summary>
    public enum LootTier : byte { Tier1 = 1, Tier2 = 2, Tier3 = 3 }

    /// <summary>Тир постройки (как в Rust: twig → wood → stone → metal → armored).</summary>
    public enum BuildTier : byte { Twig = 0, Wood = 1, Stone = 2, Metal = 3, Armored = 4 }

    public enum ItemCategory : byte
    {
        Resource, Weapon, Tool, Armor, Ammo, Attachment, Medical, Food, Drink,
        Building, Deployable, Explosive, Backrooms, Ammo_Container, Misc
    }

    /// <summary>Слоты одежды/брони — 1:1 как в Rust (6 слотов).</summary>
    public enum EquipSlot : byte { None = 0, Head = 1, Face = 2, Chest = 3, Legs = 4, Hands = 5, Feet = 6, Backpack = 7 }

    public enum AmmoType : byte
    {
        None = 0, Pistol9mm = 1, SMG9mm = 2, Rifle556 = 3, Rifle762 = 4, Shotgun12 = 5,
        Arrow = 6, Bolt = 7, Nail = 8, Rocket = 9, Grenade40mm = 10, HandmadeShell = 11,
        Incendiary556 = 12, HV556 = 13, Explosive556 = 14
    }

    public enum WeaponClass : byte
    {
        Melee = 0, Pistol = 1, SMG = 2, Rifle = 3, LMG = 4, Shotgun = 5, Bow = 6,
        Sniper = 7, Launcher = 8, Throwable = 9, Deployable = 10, Tool = 11
    }

    /// <summary>Слоты обвесов (в Rust их 7, у нас 6 — хватит с головой).</summary>
    public enum AttachmentSlot : byte { None = 0, Sight = 1, Muzzle = 2, Underbarrel = 3, Magazine = 4, Laser = 5, Skin = 6 }

    /// <summary>Зона попадания — множители урона как в Rust (head ×2 для пуль, ×1.5 для дробовика и т.д.).</summary>
    public enum HitRegion : byte { Body = 0, Head = 1, UpperBody = 2, Legs = 3, Arm = 4, WeakPoint = 5 }

    public enum Rarity : byte { Common = 0, Uncommon = 1, Rare = 2, VeryRare = 3, Military = 4, Anomalous = 5 }

    /// <summary>ID слоёв физики — совпадают с настройками из Editor/ProjectBootstrap.cs.</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int Player = 6;         // сюда же монстры (они «живые»)
        public const int Monster = 7;
        public const int Buildable = 8;      // поставленные игроками блоки
        public const int Deployable = 9;     // ящики, верстаки, TC
        public const int LevelGeometry = 10; // статика уровней
        public const int Loot = 11;          // контейнеры и узлы ресурсов
        public const int Water = 12;         // объёмы воды Poolrooms
        public const int Ragdoll = 13;       // трупы/мешки с лутом
        public const int Hitbox = 14;        // хитбоксы частей тела
        public const int Projectile = 15;    // пули/снаряды
        public const int NoBuild = 16;       // зоны, где строить нельзя (входы/выходы)

        public static int Mask(params int[] layers)
        {
            int m = 0;
            for (int i = 0; i < layers.Length; i++) m |= 1 << layers[i];
            return m;
        }
    }

    /// <summary>Константы баланса в одном месте — чтобы правки не разъезжались по коду.</summary>
    public static class Balance
    {
        public const float DayLengthSeconds = 1500f;      // ~25 мин (Rust-like цикл под Backrooms)
        public const float TickRate = 30f;                // серверный тик (30 Гц) — Mirror / NGO
        public const float SnapshotRate = 20f;            // частота снапшотов трансформов
        // ===== сеть 100+ онлайн на одном сервере =====
        public const int TargetOnline = 100;              // цель: «сеть 100+ человек» на одном сервере
        public const int MaxPlayerSlots = 128;            // слотов транспорта (запас на реконнекты)
        public const int MaxPlayersOnline = 112;          // мягкий лимит онлайна (отказ новым выше)
        // пороги приёмки (проверяет стенд NetLoadTest, печатает ОК/ПЕРЕБОР)
        public const float NetKbPerPlayerCap = 13f;       // КБ/с вниз на игрока
        public const float NetMbpsTotalCap = 12f;         // Мбит/с суммарно исходящего
        public const float NetTickBudgetMs = 4f;          // бюджет холостого прогона рассылки на кадр
        public const int LodPlayerThreshold = 64;         // с этого онлайна включается LOD снапшотов
        public const float AoiRadius = 120f;              // базовый радиус интереса (AOI)
        public const float AoiRadiusMin = 55f;            // радиус на полном онлайне
        public const int MaxEntitiesPerSnapshot = 64;     // лимит сущностей в одном пакете (анти-MTU)
        public const float MaxInteractDistance = 3.0f;    // как в Rust: лутание 3 м
        public const float QuickLootDistance = 3.0f;
        public const int HotbarSlots = 6;
        public const int BackpackSlots = 24;              // итого 30, как в Rust (6+24)
        public const float BuildPrivilegeRadius = 16f;    // радиус Tool Cupboard
        public const float DecayTickSeconds = 600f;       // апкип-тик
        public const float StabilityCollapseSeconds = 3f;
        public const int MaxStabilityDepth = 24;

        // ---- зафиксировано ответами на опросник (docs/ANSWERS.md) ----
        public const int TargetFps = 120;                  // 04_perf: 120 FPS / RTX 3060
        public const int WipeDays = 30;                    // 07_wipe: вайп раз в месяц
        public const float LevelSizeMeters = 1000f;        // 08_level_size: 1×1 км
        public const float LootRespawnTier1 = 300f;        // 17: 5 минут  (ящики Level 0)
        public const float LootRespawnTier2 = 900f;        // 17: 15 минут (Level 37)
        public const float LootRespawnTier3 = 1800f;       // 17: 30 минут (электростанция)
        public const int MaxGroupSize = 0;                 // 35_groups: 0 = без ограничений
        public const bool BuildAnywhere = true;            // 25_build_zones: строить можно везде
        public const bool WindowsOnly = true;              // 05_platform: только Windows
        public const bool ClosedSource = true;             // 37_modding: без модов/плагинов
    }
}
