// ============================================================================
//  SUBSISTENCE — Combat/Weapons.cs
//  Оружие «как в Rust»: hitscan для пуль, снаряды для ракет/стрел, реальные паттерны
//  отдачи, разброс по стойкам (стоя/сидя/лёжа/в движении), ADS, обвесы, износ,
//  патроны в магазине, перезарядка с выбрасыванием патрона (Rust-механика).
//  Считает ВСЁ сервер, клиент предсказывает визуал и отдачу.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Net;

namespace Subsistence.Combat
{
    /// <summary>Полная боевая характеристика. Таблица ниже — по мотивам Rust (bullets/melee).</summary>
    [Serializable]
    public class WeaponStats
    {
        public string id;
        public WeaponClass cls;
        public AmmoType ammo;
        public float damage = 30f;               // урон за попадание (пуля)
        public float rpm = 450f;                 // выстрелов в минуту
        public float muzzleVelocity = 375f;      // м/с (для баллистики снарядов)
        public float effectiveRange = 200f;      // дальше — падение урона
        public float damageFalloffStart = 40f;
        public float damageFalloffEnd = 250f;
        public float damageFalloffMin = 0.5f;    // до 50 % на пределе
        public int magazine = 30;
        public float reloadTime = 2.5f;
        public bool boltAction;                  // перезарядка после каждого выстрела
        public int pellets = 1;                  // дробовики
        public float hipSpreadDeg = 2.0f;
        public float adsSpreadDeg = 0.15f;
        public float moveSpreadMult = 2.2f;
        public float crouchSpreadMult = 0.7f;
        public float proneSpreadMult = 0.5f;
        public float adsTime = 0.25f;
        public float durabilityLossPerShot = 0.12f;
        public float meleeDamage;                // 0 = не оружие ближнего боя
        public float meleeRange = 1.6f;
        public float meleeRate = 0.6f;
        public DamageType meleeDamageType = DamageType.Slash;
        public RecoilPattern recoil = RecoilPattern.Rifle();
        public float soundRadius = 200f;         // слышимость выстрела (монстры идут на звук!)
        public float deployTime = 0.7f;
        public bool fullAuto = true;
        public float projectileGravity = 0f;     // >0 для стрел/гранат (баллистика)
        public string projectilePrefab;

        public float ShotInterval => 60f / Mathf.Max(1f, rpm);
    }

    /// <summary>Паттерн отдачи — массив «толчков» камеры по выстрелам (как в Rust/CS).</summary>
    [Serializable]
    public class RecoilPattern
    {
        public Vector2[] kicks = Array.Empty<Vector2>();  // x = влево/вправо, y = вверх
        public float recoverySpeed = 6f;
        public float randomness = 0.25f;                  // доля случайности (Rust: детерминированный паттерн + разброс)

        public Vector2 KickAt(int shotIndex)
        {
            if (kicks.Length == 0) return new Vector2(UnityEngine.Random.Range(-0.4f, 0.4f), 0.7f);
            var k = kicks[Mathf.Min(shotIndex, kicks.Length - 1)];
            if (randomness > 0f)
                k += new Vector2(UnityEngine.Random.Range(-k.x, k.x) * randomness, UnityEngine.Random.Range(-k.y, k.y) * randomness);
            return k;
        }

        public static RecoilPattern Rifle()
        {
            // «Галочка» как у АК: вверх, потом плавно вправо-влево
            var pts = new List<Vector2>();
            float[] v = { 1.35f, 1.25f, 1.05f, 0.85f, 0.7f, 0.6f, 0.5f, 0.45f, 0.42f, 0.4f, 0.4f, 0.4f, 0.42f, 0.45f, 0.5f, 0.55f, 0.6f, 0.62f, 0.6f, 0.55f };
            for (int i = 0; i < v.Length; i++)
            {
                float x = i < 6 ? -0.05f * i : Mathf.Sin(i * 0.5f) * 0.55f * Mathf.Min(1f, (i - 5) / 6f);
                pts.Add(new Vector2(x, v[i]));
            }
            return new RecoilPattern { kicks = pts.ToArray(), recoverySpeed = 7f, randomness = 0.22f };
        }

        public static RecoilPattern SMG() => new RecoilPattern
        {
            kicks = new[] { new Vector2(0.1f, 0.55f), new Vector2(-0.15f, 0.5f), new Vector2(0.2f, 0.45f), new Vector2(-0.2f, 0.42f), new Vector2(0.25f, 0.4f), new Vector2(-0.25f, 0.4f) },
            recoverySpeed = 9f, randomness = 0.35f
        };

        public static RecoilPattern Sniper() => new RecoilPattern
        {
            kicks = new[] { new Vector2(0f, 3.4f), new Vector2(-0.6f, 3.2f), new Vector2(0.6f, 3.0f) },
            recoverySpeed = 2.5f, randomness = 0.05f
        };

        public static RecoilPattern Shotgun() => new RecoilPattern
        {
            kicks = new[] { new Vector2(0.3f, 2.2f), new Vector2(-0.4f, 2.0f) },
            recoverySpeed = 3.5f, randomness = 0.15f
        };

        public static RecoilPattern Pistol() => new RecoilPattern
        {
            kicks = new[] { new Vector2(0.05f, 0.9f), new Vector2(-0.1f, 0.85f), new Vector2(0.15f, 0.8f), new Vector2(-0.15f, 0.8f) },
            recoverySpeed = 8f, randomness = 0.3f
        };

        public static RecoilPattern LMG() => new RecoilPattern
        {
            kicks = new[] { new Vector2(0f, 0.85f), new Vector2(0.35f, 0.8f), new Vector2(-0.45f, 0.75f), new Vector2(0.5f, 0.7f), new Vector2(-0.5f, 0.7f), new Vector2(0.55f, 0.65f), new Vector2(-0.55f, 0.65f), new Vector2(0.5f, 0.6f) },
            recoverySpeed = 5f, randomness = 0.2f
        };
    }

    /// <summary>Влияние обвеса на оружие (складывается).</summary>
    [Serializable]
    public struct AttachmentMod
    {
        public AttachmentSlot slot;
        public float spreadMult;
        public float recoilMult;
        public float adsTimeMult;
        public float soundRadiusMult;
        public int magazineBonus;
        public bool givesLaser;
        public bool givesFlashlight;
        public float zoom;
    }

    public static class AttachmentTable
    {
        static readonly Dictionary<string, AttachmentMod> _mods = new Dictionary<string, AttachmentMod>
        {
            { "weapon.mod.holosight",     new AttachmentMod { slot = AttachmentSlot.Sight,      spreadMult = 0.85f, adsTimeMult = 1.1f, zoom = 1.15f } },
            { "weapon.mod.small.scope",   new AttachmentMod { slot = AttachmentSlot.Sight,      spreadMult = 0.85f, adsTimeMult = 1.35f, zoom = 4f } },
            { "weapon.mod.8x.scope",      new AttachmentMod { slot = AttachmentSlot.Sight,      spreadMult = 0.85f, adsTimeMult = 1.5f,  zoom = 16f } },
            { "weapon.mod.lasersight",    new AttachmentMod { slot = AttachmentSlot.Laser,      spreadMult = 0.9f,  givesLaser = true } },
            { "weapon.mod.flashlight",    new AttachmentMod { slot = AttachmentSlot.Laser,      givesFlashlight = true } },
            { "weapon.mod.muzzleboost",   new AttachmentMod { slot = AttachmentSlot.Muzzle,     soundRadiusMult = 1.3f, spreadMult = 0.92f, recoilMult = 0.95f } },
            { "weapon.mod.muzzlebrake",   new AttachmentMod { slot = AttachmentSlot.Muzzle,     recoilMult = 0.75f, adsTimeMult = 1.05f, soundRadiusMult = 1.1f } },
            { "weapon.mod.silencer",      new AttachmentMod { slot = AttachmentSlot.Muzzle,     soundRadiusMult = 0.45f, recoilMult = 0.85f, adsTimeMult = 1.1f } },
            { "weapon.mod.extendedmags",  new AttachmentMod { slot = AttachmentSlot.Magazine,   magazineBonus = 10 } },
            { "weapon.mod.lasersight.ir", new AttachmentMod { slot = AttachmentSlot.Laser,      givesLaser = true, spreadMult = 0.85f } },
        };

        public static bool TryGet(string id, out AttachmentMod mod) => _mods.TryGetValue(id, out mod);

        public static void Apply(ref WeaponStats stats, IList<string> attachments)
        {
            if (attachments == null) return;
            for (int i = 0; i < attachments.Count; i++)
            {
                if (!TryGet(attachments[i], out var m)) continue;
                stats.hipSpreadDeg *= m.spreadMult;
                stats.adsSpreadDeg *= m.spreadMult;
                stats.recoil.randomness *= m.recoilMult;
                stats.adsTime *= m.adsTimeMult;
                stats.soundRadius *= m.soundRadiusMult;
                stats.magazine += m.magazineBonus;
            }
        }
    }

    /// <summary>Таблица оружия: цифры по мотивам Rust (урон/темп/скорость пули).</summary>
    public static class WeaponTable
    {
        static readonly Dictionary<string, WeaponStats> _stats = new Dictionary<string, WeaponStats>(64);

        static WeaponTable()
        {
            // ---- Пистолеты (Tier 1) ----
            F("pistol.eoka", WeaponClass.Pistol, AmmoType.HandmadeShell, 40, 120, 150, mag: 1, reload: 2.5f, bolt: true,
              hip: 4.5f, dur: 2.5f, range: 60, falloffEnd: 60, rp: RecoilPattern.Pistol(), sound: 180);
            F("pistol.nailgun", WeaponClass.Pistol, AmmoType.Nail, 15, 220, 60, mag: 16, reload: 2.2f,
              hip: 3.0f, dur: 0.2f, range: 50, falloffEnd: 60, rp: RecoilPattern.Pistol(), sound: 120, auto: false);
            F("pistol.semiauto", WeaponClass.Pistol, AmmoType.Pistol9mm, 35, 300, 300, mag: 10, reload: 2.3f,
              hip: 2.2f, ads: 0.2f, dur: 0.4f, range: 100, falloffEnd: 120, rp: RecoilPattern.Pistol(), sound: 200, auto: false);
            F("revolver.python", WeaponClass.Pistol, AmmoType.Pistol9mm, 60, 180, 320, mag: 6, reload: 3.2f,
              hip: 3.2f, ads: 0.25f, dur: 1.2f, range: 120, falloffEnd: 150, rp: RecoilPattern.Pistol(), sound: 260, auto: false);

            // ---- ПП (Tier 2) ----
            F("smg.custom", WeaponClass.SMG, AmmoType.SMG9mm, 20, 600, 240, mag: 24, reload: 2.5f,
              hip: 3.4f, ads: 0.55f, dur: 0.25f, range: 80, falloffEnd: 100, rp: RecoilPattern.SMG(), sound: 200);
            F("smg.thompson", WeaponClass.SMG, AmmoType.SMG9mm, 30, 750, 300, mag: 20, reload: 2.8f,
              hip: 3.0f, ads: 0.45f, dur: 0.3f, range: 90, falloffEnd: 120, rp: RecoilPattern.SMG(), sound: 240);
            F("smg.mp5", WeaponClass.SMG, AmmoType.SMG9mm, 35, 800, 340, mag: 30, reload: 2.9f,
              hip: 2.6f, ads: 0.35f, dur: 0.25f, range: 100, falloffEnd: 130, rp: RecoilPattern.SMG(), sound: 240);
            F("smg.vector", WeaponClass.SMG, AmmoType.SMG9mm, 27, 900, 380, mag: 33, reload: 3.0f,
              hip: 2.4f, ads: 0.3f, dur: 0.22f, range: 110, falloffEnd: 140, rp: RecoilPattern.SMG(), sound: 250);

            // ---- Дробовики ----
            F("shotgun.waterpipe", WeaponClass.Shotgun, AmmoType.HandmadeShell, 13, 90, 120, mag: 1, reload: 2.8f, bolt: true,
              hip: 5.5f, dur: 1.0f, range: 30, falloffEnd: 45, pellets: 10, rp: RecoilPattern.Shotgun(), sound: 260);
            F("shotgun.double", WeaponClass.Shotgun, AmmoType.Shotgun12, 11, 200, 140, mag: 2, reload: 2.4f,
              hip: 5.0f, dur: 0.9f, range: 35, falloffEnd: 50, pellets: 12, rp: RecoilPattern.Shotgun(), sound: 280, auto: false);
            F("shotgun.pump", WeaponClass.Shotgun, AmmoType.Shotgun12, 11.5f, 80, 150, mag: 6, reload: 3.2f,
              hip: 4.4f, dur: 0.8f, range: 40, falloffEnd: 60, pellets: 12, rp: RecoilPattern.Shotgun(), sound: 300, auto: false);
            F("shotgun.spas", WeaponClass.Shotgun, AmmoType.Shotgun12, 12.5f, 140, 160, mag: 6, reload: 3.4f,
              hip: 4.0f, dur: 0.75f, range: 45, falloffEnd: 65, pellets: 12, rp: RecoilPattern.Shotgun(), sound: 300);

            // ---- Винтовки (Tier 2-3) ----
            F("rifle.semiauto", WeaponClass.Rifle, AmmoType.Rifle556, 45, 250, 375, mag: 16, reload: 2.7f,
              hip: 2.0f, ads: 0.35f, dur: 0.5f, range: 200, falloffEnd: 260, rp: RecoilPattern.Rifle(), sound: 320, auto: false);
            F("rifle.m39", WeaponClass.Rifle, AmmoType.Rifle762, 55, 200, 400, mag: 10, reload: 2.6f,
              hip: 2.4f, ads: 0.7f, dur: 0.55f, range: 220, falloffEnd: 300, rp: RecoilPattern.Rifle(), sound: 340, auto: false);
            F("rifle.ak", WeaponClass.Rifle, AmmoType.Rifle556, 50, 450, 375, mag: 30, reload: 2.9f,
              hip: 2.6f, ads: 0.35f, dur: 0.55f, range: 220, falloffEnd: 280, rp: RecoilPattern.Rifle(), sound: 380);
            F("rifle.lr300", WeaponClass.Rifle, AmmoType.Rifle556, 40, 500, 375, mag: 30, reload: 2.7f,
              hip: 2.2f, ads: 0.3f, dur: 0.5f, range: 220, falloffEnd: 280, rp: RecoilPattern.Rifle(), sound: 360);
            F("rifle.m16", WeaponClass.Rifle, AmmoType.Rifle556, 45, 600, 400, mag: 30, reload: 2.8f,
              hip: 2.0f, ads: 0.28f, dur: 0.5f, range: 240, falloffEnd: 300, rp: RecoilPattern.Rifle(), sound: 360);
            F("rifle.bolt", WeaponClass.Sniper, AmmoType.Rifle556, 80, 45, 656, mag: 4, reload: 3.6f, bolt: true,
              hip: 4.5f, ads: 0.15f, dur: 2.0f, range: 400, falloffStart: 120, falloffEnd: 500, rp: RecoilPattern.Sniper(), sound: 500, zoom: 8f);
            F("lmg.m249", WeaponClass.LMG, AmmoType.Rifle556, 50, 800, 400, mag: 100, reload: 5.5f,
              hip: 5.0f, ads: 1.4f, dur: 0.35f, range: 260, falloffEnd: 320, rp: RecoilPattern.LMG(), sound: 420);
            F("hmlmg", WeaponClass.LMG, AmmoType.Rifle762, 75, 500, 420, mag: 60, reload: 6.0f,
              hip: 6.5f, ads: 1.8f, dur: 0.5f, range: 300, falloffEnd: 380, rp: RecoilPattern.LMG(), sound: 460);
            F("minigun", WeaponClass.LMG, AmmoType.Rifle556, 35, 1200, 450, mag: 200, reload: 7.0f,
              hip: 8.0f, ads: 2.5f, dur: 0.2f, range: 200, falloffEnd: 260, rp: RecoilPattern.LMG(), sound: 520);

            // ---- Тяжёлое ----
            F("rocket.launcher", WeaponClass.Launcher, AmmoType.Rocket, 275, 30, 40, mag: 1, reload: 4.0f, bolt: true,
              hip: 3.0f, ads: 0.6f, dur: 0, range: 300, falloffEnd: 300, rp: RecoilPattern.Shotgun(), sound: 420,
              projectile: "rocket_basic", gravity: 0.35f);
            F("multiplegrenadelauncher", WeaponClass.Launcher, AmmoType.Grenade40mm, 40, 60, 80, mag: 6, reload: 5.0f,
              hip: 3.4f, ads: 0.5f, dur: 0.4f, range: 200, falloffEnd: 200, rp: RecoilPattern.Shotgun(), sound: 340,
              projectile: "grenade40mm", gravity: 1f);
            F("flamethrower", WeaponClass.Launcher, AmmoType.None, 12, 300, 45, mag: 100, reload: 4.5f,
              hip: 4.0f, ads: 1.0f, dur: 0.05f, range: 25, falloffEnd: 25, rp: RecoilPattern.SMG(), sound: 160,
              projectile: "flame", gravity: 0f);

            // ---- Луки ----
            F("bow.hunting", WeaponClass.Bow, AmmoType.Arrow, 45, 60, 90, mag: 1, reload: 1.4f, bolt: true,
              hip: 1.5f, ads: 0.2f, dur: 0.8f, range: 120, falloffEnd: 160, rp: RecoilPattern.Pistol(), sound: 40,
              projectile: "arrow", gravity: 0.9f);
            F("crossbow", WeaponClass.Bow, AmmoType.Bolt, 60, 40, 130, mag: 1, reload: 2.6f, bolt: true,
              hip: 0.9f, ads: 0.1f, dur: 1.0f, range: 160, falloffEnd: 220, rp: RecoilPattern.Pistol(), sound: 60,
              projectile: "bolt", gravity: 0.5f);

            // ---- Ближний бой (те же таблицы, но melee-ветка) ----
            M("rock", 25, 1.5f, 0.7f, DamageType.Blunt, 50);
            M("torch", 12, 1.6f, 0.7f, DamageType.Fire, 30);
            M("bone.club", 35, 1.8f, 0.75f, DamageType.Blunt, 60);
            M("bone.knife", 22, 1.5f, 0.5f, DamageType.Stab, 25);
            M("hatchet", 40, 1.7f, 0.7f, DamageType.Slash, 45);
            M("pickaxe", 32, 1.9f, 0.8f, DamageType.Stab, 45);
            M("hammer", 20, 1.7f, 0.8f, DamageType.Blunt, 40);
            M("salvaged.cleaver", 55, 1.9f, 0.85f, DamageType.Slash, 60);
            M("machete", 65, 2.0f, 0.9f, DamageType.Slash, 70);
            M("pipe.wrench", 45, 1.7f, 0.8f, DamageType.Blunt, 55);
            M("sledgehammer", 75, 2.0f, 1.3f, DamageType.Blunt, 80);
            M("spear.wooden", 40, 2.6f, 0.9f, DamageType.Stab, 55);
            M("bone.knife.skin", 50, 1.8f, 0.7f, DamageType.Slash, 70);
            M("stun.baton", 25, 1.8f, 0.9f, DamageType.Blunt, 90);

            // Инструменты с боевыми данными (кирка/топор режут постройки быстрее — см. Building)
        }

        static void F(string id, WeaponClass cls, AmmoType ammo, float dmg, float rpm, float vel, int mag, float reload,
                      float hip, float dur, float range, float falloffEnd, RecoilPattern rp, float sound,
                      float ads = 0.3f, bool bolt = false, int pellets = 1, bool auto = true,
                      float falloffStart = 30f, float gravity = 0f, string projectile = null, float zoom = 0f)
        {
            _stats[id] = new WeaponStats
            {
                id = id, cls = cls, ammo = ammo, damage = dmg, rpm = rpm, muzzleVelocity = vel, magazine = mag,
                reloadTime = reload, boltAction = bolt, pellets = pellets, hipSpreadDeg = hip,
                adsSpreadDeg = Mathf.Max(0.05f, hip * 0.08f), adsTime = ads, durabilityLossPerShot = dur,
                effectiveRange = range, damageFalloffStart = falloffStart, damageFalloffEnd = falloffEnd,
                recoil = rp, soundRadius = sound, fullAuto = auto, projectileGravity = gravity, projectilePrefab = projectile
            };
        }

        static void M(string id, float dmg, float range, float rate, DamageType dt, float sound)
        {
            _stats[id] = new WeaponStats
            {
                id = id, cls = WeaponClass.Melee, ammo = AmmoType.None, damage = 0, meleeDamage = dmg,
                meleeRange = range, meleeRate = rate, meleeDamageType = dt, rpm = 60f / rate, magazine = 0,
                soundRadius = sound, recoil = new RecoilPattern { kicks = new[] { new Vector2(0.05f, 0.35f) }, recoverySpeed = 10f, randomness = 0.1f } // не используется для melee
            };
        }

        public static WeaponStats Get(string weaponId)
        {
            if (_stats.TryGetValue(weaponId, out var s)) return s;
            // копия, чтобы обвесы меняли только локальный экземпляр
            var clone = new WeaponStats { id = weaponId, cls = WeaponClass.Melee, meleeDamage = 15, meleeRange = 1.5f, meleeRate = 0.7f };
            return clone;
        }

        /// <summary>Статы с учётом обвесов (используется и сервером, и клиентом — детерминизм).</summary>
        public static WeaponStats GetWithAttachments(ItemStack item)
        {
            var baseStats = Get(item?.id);
            var copy = (WeaponStats)Clone(baseStats);
            if (item != null) AttachmentTable.Apply(ref copy, item.attachments);
            return copy;
        }

        static object Clone(WeaponStats s)
        {
            var json = JsonUtility.ToJson(s);
            return JsonUtility.FromJson<WeaponStats>(json);
        }

        public static float PelletDamageAt(WeaponStats s, float distance)
        {
            if (distance <= s.damageFalloffStart) return s.damage;
            float t = Mathf.InverseLerp(s.damageFalloffStart, s.damageFalloffEnd, distance);
            return Mathf.Lerp(s.damage, s.damage * s.damageFalloffMin, t);
        }

        public static readonly Dictionary<AmmoType, string> AmmoItem = new Dictionary<AmmoType, string>
        {
            { AmmoType.Pistol9mm, "ammo.pistol" }, { AmmoType.SMG9mm, "ammo.smg" }, { AmmoType.Rifle556, "ammo.rifle" },
            { AmmoType.Rifle762, "ammo.rifle" }, { AmmoType.Shotgun12, "ammo.shotgun" }, { AmmoType.HandmadeShell, "ammo.handmade.shell" },
            { AmmoType.Arrow, "arrow.wooden" }, { AmmoType.Bolt, "arrow.wooden" }, { AmmoType.Nail, "ammo.nailgun" },
            { AmmoType.Rocket, "ammo.rocket.basic" }, { AmmoType.Grenade40mm, "ammo.grenadelauncher.he" }
        };
    }

    /// <summary>
    /// Рантайм-контроллер оружия. Один на игрока, обслуживает активный слот хотбара.
    /// Сервер авторитетен по выстрелам/урону; клиент рисует отдачу и трассеры.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [Header("Ссылки")]
        public Transform muzzle;                  // точка вылета
        public Camera viewCamera;                 // камера от первого лица
        public PlayerInventory inventory;
        public LayerMask hitMask;                 // что пробивается: мир, игроки, монстры, постройки

        [Header("Отдача — состояние (синхронизируется в сети как часть вью)")]
        public Vector2 recoilAccum;
        public bool IsAiming { get; private set; }
        public bool IsReloading { get; private set; }
        public bool IsDeployed { get; private set; }

        public event Action<Vector3, Vector3> Fired;          // origin, direction (для трассера/звука)
        public event Action<WeaponStats> ReloadStarted;
        public event Action<float> DamageDealt;               // нанесённый урон — для хитмаркера

        float _nextShotTime;
        int _shotIndex;
        float _reloadEndTime;
        float _deployEndTime;
        int _shotsFired;
        readonly RaycastHit[] _hitBuffer = new RaycastHit[16];
        float _laserTimer, _flashlightTimer;

        WeaponStats CurrentStats
        {
            get
            {
                var item = inventory?.ActiveItem;
                if (item == null) return null;
                var d = item.Def;
                if (d == null) return null;
                if (d.category != ItemCategory.Weapon && d.category != ItemCategory.Tool) return null;
                return WeaponTable.GetWithAttachments(item);
            }
        }

        public ItemStack CurrentItem => inventory?.ActiveItem;
        public WeaponStats Stats => CurrentStats;

        void Update()
        {
            if (inventory == null) return;
            var item = CurrentItem;
            if (item == null) { IsDeployed = false; return; }

            var def = item.Def;

            // Смена предмета → анимация «достать» (deploy), сброс отдачи
            if (!IsDeployed)
            {
                _deployEndTime = Time.time + 0.6f;
                IsDeployed = true;
                recoilAccum = Vector2.zero;
                _shotIndex = 0;
            }

            RecoverRecoil(Time.deltaTime);

            if (def != null && (def.category == ItemCategory.Weapon || def.category == ItemCategory.Tool))
                UpdateWeapon(item);
        }

        void UpdateWeapon(ItemStack item)
        {
            var s = CurrentStats;
            if (s == null) return;

            if (IsReloading)
            {
                if (Time.time >= _reloadEndTime) FinishReload(item, s);
                return;
            }

            if (Subsistence.Player.PlayerInput.FireHeld && CanShoot(s) && s.cls != WeaponClass.Melee)
                TryFire(item, s);
            else if (Subsistence.Player.PlayerInput.FirePressed && s.cls == WeaponClass.Melee)
                TryMelee(item, s);

            if (Subsistence.Player.PlayerInput.ReloadPressed && !IsReloading && s.cls != WeaponClass.Melee)
                StartReload(item, s);

            IsAiming = Subsistence.Player.PlayerInput.AimHeld && s.cls != WeaponClass.Melee;
        }

        bool CanShoot(WeaponStats s)
        {
            if (Time.time < _nextShotTime || Time.time < _deployEndTime) return false;
            var item = CurrentItem;
            if (item == null) return false;
            if (s.boltAction && _shotsFired > 0 && item.ammoInMag <= 0) return false;
            if (item.ammoInMag <= 0)
            {
                // авто-перезарядка как в Rust (при пустом магазине)
                if (Subsistence.Player.PlayerInput.FireHeld) StartReload(item, s);
                return false;
            }
            return true;
        }

        void TryFire(ItemStack item, WeaponStats s)
        {
            _nextShotTime = Time.time + s.ShotInterval;
            if (!s.fullAuto && !Subsistence.Player.PlayerInput.FirePressed) return;

            item.ammoInMag--;
            _shotsFired++;

            // Отдача (клиентская визуальная + серверная проверка угла)
            var kick = s.recoil.KickAt(_shotIndex++);
            recoilAccum += kick;

            // Разброс: стойка + движение + ADS
            float spread = IsAiming ? s.adsSpreadDeg : s.hipSpreadDeg;
            spread *= Subsistence.Player.PlayerControllerState.SpreadMultiplier;
            spread *= s.recoil.randomness;   // «растущий» разброс при длинной очереди
            float spreadRad = spread * Mathf.Deg2Rad;

            Vector3 origin = viewCamera != null ? viewCamera.transform.position : transform.position;
            Vector3 dir = viewCamera != null ? viewCamera.transform.forward : transform.forward;

            // Сервер считает попадания; клиент — только эффекты
            if (NetworkBridge.IsServer)
            {
                int pellets = Mathf.Max(1, s.pellets);
                for (int p = 0; p < pellets; p++)
                {
                    Vector3 d = ApplySpread(dir, spreadRad);
                    FireHitscan(item, s, origin, d);
                }
                // Износ оружия
                if (item.Wear(s.durabilityLossPerShot))
                    OnWeaponBroken(item);
            }

            Fired?.Invoke(origin, dir);
            Subsistence.Audio.AudioDirector.PlayAt("shot", origin, s.cls == WeaponClass.Sniper ? 1f : 0.8f, 1f, 0.06f);
            Subsistence.AI.NoiseSystem.Emit(transform.position, s.soundRadius, Subsistence.AI.NoiseType.Gunshot);

            if (s.boltAction) StartReload(item, s);
        }

        Vector3 ApplySpread(Vector3 dir, float spreadRad)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * spreadRad;
            var right = Vector3.Cross(dir, Vector3.up).normalized;
            var up = Vector3.Cross(right, dir).normalized;
            return (dir + right * r.x + up * r.y).normalized;
        }

        void FireHitscan(ItemStack item, WeaponStats s, Vector3 origin, Vector3 dir)
        {
            int count = Physics.RaycastNonAlloc(origin, dir, _hitBuffer, s.effectiveRange, hitMask, QueryTriggerInteraction.Ignore);
            if (count <= 0) return;

            // ближайший валидный хит
            int best = -1; float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hitBuffer[i].distance < bestDist) { bestDist = _hitBuffer[i].distance; best = i; }
            }
            if (best < 0) return;

            var hit = _hitBuffer[best];
            var region = HitboxResolver.Resolve(hit.collider, out var damageable);
            float dmg = WeaponTable.PelletDamageAt(s, hit.distance);

            // Множители по зоне (Rust: голова ×2 для пуль, ×1.5 для дроби, ×0.75 по ногам)
            dmg *= HitboxResolver.RegionMultiplier(region, s.pellets > 1);

            // Постройка под попаданием (нужна и для рейд-урона, и для пробития ниже).
            Subsistence.Building.BuildBlock build = null;

            // Попадание по игроку: урон считает сервер (клиент только сообщает о выстреле).
            // true — попадание обработано, локально применять нельзя.
            if (damageable != null && !CombatNet.TryPlayerHit(CombatNet.Of(damageable), item.id, DamageType.Bullet, hit.point, region))
                damageable.ApplyDamage(DamageType.Bullet, dmg, transform.position, region);
            else
            {
                // Постройки и деплои (рейд-урон)
                build = hit.collider.GetComponentInParent<Subsistence.Building.BuildBlock>();
                if (build != null) build.ApplyRaidDamage(dmg, DamageType.Bullet);
                else
                {
                    var destructible = hit.collider.GetComponentInParent<IDestructible>();
                    destructible?.ApplyDamage(DamageType.Bullet, dmg, transform.position, region);
                }
            }
            DamageDealt?.Invoke(dmg);

            // Пробитие: пуля пробивает дерево/стекло с потерей урона
            if (build != null && build.MaterialClass == Subsistence.Building.BuildMaterialClass.Soft)
            {
                float remaining = dmg * 0.5f;
                Vector3 nextOrigin = hit.point + dir * 0.1f;
                // один рикошет/пробитие — как в Rust для twig/wood
                int c2 = Physics.RaycastNonAlloc(nextOrigin, dir, _hitBuffer, s.effectiveRange - hit.distance, hitMask, QueryTriggerInteraction.Ignore);
                if (c2 > 0) { int b2 = 0; for (int i = 1; i < c2; i++) if (_hitBuffer[i].distance < _hitBuffer[b2].distance) b2 = i;
                    var h2 = _hitBuffer[b2];
                    var reg2 = HitboxResolver.Resolve(h2.collider, out var d2);
                    float dmg2 = remaining * HitboxResolver.RegionMultiplier(reg2, s.pellets > 1);
                    if (d2 != null && !CombatNet.TryPlayerHit(CombatNet.Of(d2), item.id, DamageType.Bullet, h2.point, reg2))
                        d2.ApplyDamage(DamageType.Bullet, dmg2, transform.position, reg2);
                }
            }
        }

        void TryMelee(ItemStack item, WeaponStats s)
        {
            if (Time.time < _nextShotTime) return;
            _nextShotTime = Time.time + s.meleeRate;

            Vector3 origin = viewCamera.transform.position;
            Vector3 dir = viewCamera.transform.forward;
            if (Physics.Raycast(origin, dir, out var hit, s.meleeRange, hitMask, QueryTriggerInteraction.Ignore))
            {
                var region = HitboxResolver.Resolve(hit.collider, out var damageable);
                float dmg = s.meleeDamage * HitboxResolver.RegionMultiplier(region, false);

                // По игроку бьём всегда: сервер сам посчитает урон (в офлайне применит сразу).
                bool playerHit = CombatNet.TryPlayerHit(CombatNet.Of(damageable), item.id, s.meleeDamageType, hit.point, region);
                Subsistence.Audio.AudioDirector.PlayAt(playerHit || damageable != null ? "hit_flesh" : "hit_metal", hit.point, 0.5f, 1f, 0.15f);
                if (!playerHit && NetworkBridge.IsServer)
                {
                    if (damageable != null) damageable.ApplyDamage(s.meleeDamageType, dmg, transform.position, region);
                    else
                    {
                        var build = hit.collider.GetComponentInParent<Subsistence.Building.BuildBlock>();
                        if (build != null) build.ApplyRaidDamage(MeleeToBuildDamage(s), s.meleeDamageType,
                                                                 (hit.point - transform.position).normalized);
                        else hit.collider.GetComponentInParent<IDestructible>()?.ApplyDamage(s.meleeDamageType, dmg, transform.position, region);
                    }
                }
                if (NetworkBridge.IsServer && item.Wear(0.35f)) OnWeaponBroken(item);
            }
            Fired?.Invoke(origin, dir);
            Subsistence.Audio.AudioDirector.PlayAt(s.cls == WeaponClass.Tool ? "build_place" : "hit_flesh", origin, 0.6f, 1f, 0.12f);
            Subsistence.AI.NoiseSystem.Emit(transform.position, 25f, Subsistence.AI.NoiseType.Melee);
        }

        /// <summary>Инструменты бьют по постройкам с множителем (топор — по дереву, кирка — по камню).</summary>
        static float MeleeToBuildDamage(WeaponStats s)
        {
            switch (s.id)
            {
                case "hatchet": return 12f;
                case "pickaxe": return 8f;          // по дереву хуже, по камню — лучше (см. BuildBlock)
                case "sledgehammer": return 18f;
                case "hammer": return 2f;
                case "torch": return 3f;
                default: return 4f;
            }
        }

        public void StartReload(ItemStack item, WeaponStats s)
        {
            if (IsReloading || s.cls == WeaponClass.Melee) return;
            var ammoItem = WeaponTable.AmmoItem.TryGetValue(s.ammo, out var ai) ? ai : null;
            if (ammoItem == null) return;
            if (inventory.CountOf(ammoItem) <= 0 && item.ammoInMag >= s.magazine) return;

            IsReloading = true;
            _reloadEndTime = Time.time + s.reloadTime;
            _shotsFired = 0;
            Subsistence.Audio.AudioDirector.PlayAt("reload", transform.position, 0.5f);
            ReloadStarted?.Invoke(s);
        }

        void FinishReload(ItemStack item, WeaponStats s)
        {
            IsReloading = false;
            var ammoItem = WeaponTable.AmmoItem.TryGetValue(s.ammo, out var ai) ? ai : null;
            if (ammoItem == null) return;

            int need = s.magazine - item.ammoInMag;
            int have = inventory.CountOf(ammoItem);
            int take = Mathf.Min(need, have);
            if (take <= 0) return;

            inventory.RemoveAmount(ammoItem, take);
            item.ammoInMag += take;

            // Rust: при перезарядке неполного магазина один патрон теряется (tactical reload)
            if (item.ammoInMag > 0 && have > take && item.ammoInMag > s.magazine - 1 && UnityEngine.Random.value < 0.15f)
                Subsistence.World.WorldDeposits.SpawnLooseItem(ammoItem, 1, transform.position);
        }

        void RecoverRecoil(float dt)
        {
            var s = CurrentStats;
            float speed = s?.recoil.recoverySpeed ?? 6f;
            recoilAccum = Vector2.Lerp(recoilAccum, Vector2.zero, dt * speed);
        }

        void OnWeaponBroken(ItemStack item)
        {
            Debug.Log($"[Weapons] {item.id} сломан (прочность 0)");
        }

        public void ResetAfterDeath()
        {
            IsReloading = false; IsDeployed = false; recoilAccum = Vector2.zero; _shotIndex = 0; _shotsFired = 0;
        }
    }

    /// <summary>Всё, что может получить урон (игрок, монстр, турель, животное).</summary>
    public interface IDestructible
    {
        void ApplyDamage(DamageType type, float amount, Vector3 from, HitRegion region);
        bool IsAlive { get; }
    }

    /// <summary>Хитбоксы: голова/торс/ноги. Множители — как в Rust (head ×2, legs ×0.75).</summary>
    public static class HitboxResolver
    {
        public static HitRegion Resolve(Collider c, out IDestructible owner)
        {
            owner = null;
            if (c == null) return HitRegion.Body;
            var hb = c.GetComponent<Hitbox>();
            owner = c.GetComponentInParent<IDestructible>();
            return hb != null ? hb.region : HitRegion.Body;
        }

        public static float RegionMultiplier(HitRegion r, bool buckshot)
        {
            switch (r)
            {
                case HitRegion.Head: return buckshot ? 1.5f : 2.0f;
                case HitRegion.UpperBody: return 1.0f;
                case HitRegion.Body: return 1.0f;
                case HitRegion.Arm: return 0.85f;
                case HitRegion.Legs: return 0.75f;
                case HitRegion.WeakPoint: return 2.5f;   // слабая точка монстров (Backrooms)
                default: return 1f;
            }
        }
    }

    /// <summary>Маркер хитбокса на коллайдере тела.</summary>
    public class Hitbox : MonoBehaviour
    {
        public HitRegion region = HitRegion.Body;
        [Tooltip("Множитель брони для этой зоны (голова обычно защищена шлемом сильнее)")]
        public float armorCoverage = 1f;
    }

    /// <summary>Осколки/рикошеты и эффекты попаданий (клиентская часть, сервер не считает).</summary>
    public class ImpactEffects : MonoBehaviour
    {
        public static ImpactEffects Instance;
        public GameObject woodImpact, metalImpact, concreteImpact, fleshImpact, waterImpact;
        public AudioClip[] ricochetClips;

        void Awake() => Instance = this;

        public void Spawn(Vector3 point, Vector3 normal, Subsistence.Building.BuildMaterialClass mat, bool flesh = false)
        {
            GameObject prefab = flesh ? fleshImpact :
                mat == Subsistence.Building.BuildMaterialClass.Metal ? metalImpact :
                mat == Subsistence.Building.BuildMaterialClass.Stone ? concreteImpact : woodImpact;
            if (prefab != null) Instantiate(prefab, point, Quaternion.LookRotation(normal));
            if (ricochetClips != null && ricochetClips.Length > 0)
                AudioSource.PlayClipAtPoint(ricochetClips[UnityEngine.Random.Range(0, ricochetClips.Length)], point, 0.6f);
        }
    }
}
