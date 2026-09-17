// ============================================================================
//  SUBSISTENCE — Combat/Explosives.cs
//  Рейд-арсенал как в Rust: C4, сачель, бобовый заряд, Ф-1, дым, светошумовая,
//  коктейль, мины, ракеты РПГ, 40 мм гранаты. Урон — по таблице RaidTable.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Building;
using Subsistence.Net;

namespace Subsistence.Combat
{
    public enum ExplosiveKind : byte
    {
        TimedCharge = 0,    // C4
        Satchel = 1,        // сачель
        Beancan = 2,        // бобовый
        Grenade = 3,        // Ф-1
        Smoke = 4,
        Flashbang = 5,
        Molotov = 6,
        Landmine = 7,
        RocketBasic = 8,
        RocketHV = 9,
        Grenade40mmHE = 10,
        Grenade40mmSmoke = 11,
        FlamethrowerFuel = 12
    }

    public static class ExplosiveTable
    {
        public struct Def
        {
            public ExplosiveKind kind; public string itemId; public float damage;       // по постройкам (база)
            public float radius; public float playerDamage; public float fuse; public bool sticky;
            public int maxCount;  // сколько ставить/бросать (для мин — лимит)
        }

        static readonly Dictionary<ExplosiveKind, Def> _t = new Dictionary<ExplosiveKind, Def>
        {
            { ExplosiveKind.TimedCharge,     new Def { kind=ExplosiveKind.TimedCharge, itemId="explosive.timed", damage=550f, radius=3.5f, playerDamage=200f, fuse=10f, sticky=true } },
            { ExplosiveKind.Satchel,         new Def { kind=ExplosiveKind.Satchel, itemId="explosive.satchel", damage=275f, radius=4.5f, playerDamage=110f, fuse=6f } },
            { ExplosiveKind.Beancan,         new Def { kind=ExplosiveKind.Beancan, itemId="explosive.beancan", damage=110f, radius=3.5f, playerDamage=70f, fuse=4f } },
            { ExplosiveKind.Grenade,         new Def { kind=ExplosiveKind.Grenade, itemId="grenade.f1", damage=90f, radius=6f, playerDamage=120f, fuse=3.5f } },
            { ExplosiveKind.Smoke,           new Def { kind=ExplosiveKind.Smoke, itemId="grenade.smoke", damage=0f, radius=8f, playerDamage=0f, fuse=1.5f } },
            { ExplosiveKind.Flashbang,       new Def { kind=ExplosiveKind.Flashbang, itemId="grenade.flashbang", damage=0f, radius=12f, playerDamage=10f, fuse=1.8f } },
            { ExplosiveKind.Molotov,         new Def { kind=ExplosiveKind.Molotov, itemId="grenade.molotov", damage=45f, radius=5f, playerDamage=60f, fuse=0f } },
            { ExplosiveKind.Landmine,        new Def { kind=ExplosiveKind.Landmine, itemId="mine.landmine", damage=120f, radius=4f, playerDamage=180f, fuse=0f, maxCount=40 } },
            { ExplosiveKind.RocketBasic,     new Def { kind=ExplosiveKind.RocketBasic, itemId="ammo.rocket.basic", damage=275f, radius=5f, playerDamage=180f, fuse=0f } },
            { ExplosiveKind.RocketHV,        new Def { kind=ExplosiveKind.RocketHV, itemId="ammo.rocket.hv", damage=300f, radius=5.5f, playerDamage=200f, fuse=0f } },
            { ExplosiveKind.Grenade40mmHE,   new Def { kind=ExplosiveKind.Grenade40mmHE, itemId="ammo.grenadelauncher.he", damage=40f, radius=3f, playerDamage=60f, fuse=0f } },
            { ExplosiveKind.Grenade40mmSmoke,new Def { kind=ExplosiveKind.Grenade40mmSmoke, itemId="ammo.grenadelauncher.smoke", damage=0f, radius=5f, playerDamage=0f, fuse=0f } },
            { ExplosiveKind.FlamethrowerFuel,new Def { kind=ExplosiveKind.FlamethrowerFuel, itemId="lowgradefuel", damage=3f, radius=1.5f, playerDamage=12f, fuse=0f } },
        };

        public static Def Get(ExplosiveKind k) => _t.TryGetValue(k, out var d) ? d : _t[ExplosiveKind.Grenade];
        public static float Damage(ExplosiveKind k) => Get(k).damage;
        public static string ItemId(ExplosiveKind k) => Get(k).itemId;

        public static bool TryParse(string itemId, out ExplosiveKind kind)
        {
            foreach (var kv in _t) if (kv.Value.itemId == itemId) { kind = kv.Key; return true; }
            kind = ExplosiveKind.Grenade; return false;
        }
    }

    /// <summary>Механика бросания/закладки: таймер, липкость, урон по радиусу с пробитием стен.</summary>
    public class ExplosiveRuntime : MonoBehaviour
    {
        public ExplosiveKind kind;
        public float fuseRemaining;
        public ulong throwerId;
        public bool planted;

        public event Action<ExplosiveRuntime> Detonated;

        static readonly Collider[] _buf = new Collider[64];

        void Update()
        {
            if (!NetworkBridge.IsServer) return;
            fuseRemaining -= Time.deltaTime;
            if (fuseRemaining <= 0f) Detonate();
        }

        void Detonate()
        {
            var def = ExplosiveTable.Get(kind);
            Detonated?.Invoke(this);

            // 1) постройки в радиусе (через RaidTable — то, что делает рейд «растовым»)
            int n = Physics.OverlapSphereNonAlloc(transform.position, def.radius, _buf,
                       Layers.Mask(Layers.Buildable, Layers.Deployable, Layers.Player, Layers.Monster), QueryTriggerInteraction.Ignore);
            var processedParts = new HashSet<BuildBlock>();
            for (int i = 0; i < n; i++)
            {
                var col = _buf[i];
                float dist = Vector3.Distance(col.transform.position, transform.position);
                float falloff = Mathf.Clamp01(1f - dist / def.radius);

                var block = col.GetComponentInParent<BuildBlock>();
                if (block != null && processedParts.Add(block))
                {
                    // C4 обычно «пробивает» только одну грань: урон применяем к ближайшим блокам
                    block.ApplyRaidDamage(def.damage * Mathf.Max(0.35f, falloff), DamageType.Explosion);
                    continue;
                }

                var destructible = col.GetComponentInParent<IDestructible>();
                if (destructible != null && destructible is not BuildBlock)
                {
                    float dmg = def.playerDamage * falloff;
                    // Проверка стены между взрывом и целью (упрощённо — 1 raycast)
                    Vector3 dir = (col.transform.position - transform.position).normalized;
                    if (!Physics.Raycast(transform.position, dir, out var wallHit, dist - 0.3f, Layers.Mask(Layers.Buildable), QueryTriggerInteraction.Ignore)
                        || wallHit.collider.GetComponentInParent<BuildBlock>() == null)
                    {
                        // Игрока бьём через CombatNet (удалённому уедет RPC), остальных — как раньше
                        if (!CombatNet.ServerDamage(destructible, DamageType.Explosion, dmg, transform.position, HitRegion.Body, "взрыв"))
                            destructible.ApplyDamage(DamageType.Explosion, dmg, transform.position, HitRegion.Body);
                    }
                }
            }

            // 2) поджог (молотов) и дым/флеш обрабатываются на клиенте эффектами
            if (kind == ExplosiveKind.Molotov) SpawnFirePool();
            Subsistence.AI.NoiseSystem.Emit(transform.position, 120f, Subsistence.AI.NoiseType.Explosion);

            Destroy(gameObject);
        }

        void SpawnFirePool()
        {
            var go = new GameObject("FirePool");
            go.transform.position = transform.position;
            var pool = go.AddComponent<FirePool>();
            pool.radius = 4f; pool.burnDuration = 25f; pool.dpsToPlayers = 8f; pool.dpsToBlocks = 6f;
        }
    }

    /// <summary>Лужа огня: жжёт игроков и постройки (молотов, огнемёт).</summary>
    public class FirePool : MonoBehaviour
    {
        public float radius = 4f, burnDuration = 25f, dpsToPlayers = 8f, dpsToBlocks = 6f;
        float _end;
        float _tick;
        static readonly Collider[] _buf = new Collider[32];

        void Start() => _end = Time.time + burnDuration;

        void Update()
        {
            if (!NetworkBridge.IsServer) return;
            if (Time.time > _end) { Destroy(gameObject); return; }
            _tick += Time.deltaTime;
            if (_tick < 0.5f) return;
            _tick = 0f;

            int n = Physics.OverlapSphereNonAlloc(transform.position, radius, _buf,
                Layers.Mask(Layers.Player, Layers.Monster, Layers.Buildable), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var d = _buf[i].GetComponentInParent<IDestructible>();
                if (d != null && !CombatNet.ServerDamage(d, DamageType.Fire, dpsToPlayers * 0.5f, transform.position, HitRegion.Body, "огонь"))
                    d.ApplyDamage(DamageType.Fire, dpsToPlayers * 0.5f, transform.position, HitRegion.Body);
                var b = _buf[i].GetComponentInParent<BuildBlock>();
                b?.ApplyRaidDamage(dpsToBlocks * 0.5f, DamageType.Fire);
            }
        }
    }

    /// <summary>Мина: ставится на пол, срабатывает на «живых» (кроме владельца 5 сек).</summary>
    public class LandmineRuntime : MonoBehaviour
    {
        public ulong ownerId;
        float _armTime;
        void Start() => _armTime = Time.time + 5f;

        void OnTriggerEnter(Collider other)
        {
            if (!NetworkBridge.IsServer || Time.time < _armTime) return;
            var d = other.GetComponentInParent<IDestructible>();
            if (d == null) return;
            var go = new GameObject("MineBlast");
            go.transform.position = transform.position;
            var ex = go.AddComponent<ExplosiveRuntime>();
            ex.kind = ExplosiveKind.Landmine; ex.fuseRemaining = 0.05f; ex.throwerId = ownerId;
            Destroy(gameObject);
        }
    }
}
