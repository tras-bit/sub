// ============================================================================
//  SUBSISTENCE — AI/Monsters.cs
//  Монстры Backrooms: Smiler (гудящие коридоры), Hound (затопленные бассейны),
//  Partygoer (стаи, слышат всё), Skin-Stealer (маскируется под игрока),
//  Bacteria (босс электростанции). FSM на сервере, клиент видит интерполяцию.
//  ИИ: зрение (конус+луч), слух (события шума), «запах» (близость), A* по графу уровня.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Audio;      // MonsterAudio — голос вида (AudioDirector.cs)
using Subsistence.Combat;
using Subsistence.Net;

namespace Subsistence.AI
{
    public enum MonsterKind : byte { Smiler = 0, Hound = 1, Partygoer = 2, SkinStealer = 3, Bacteria = 4, Clump = 5, Whisperer = 6, Drowned = 7, Spark = 8 }
    public enum MonsterState : byte { Idle = 0, Patrol = 1, Investigate = 2, Chase = 3, Attack = 4, Flee = 5, Dead = 6, Stalk = 7 }
    public enum NoiseType : byte { Gunshot = 0, Explosion = 1, Melee = 2, Building = 3, Footstep = 4, Door = 5, Scream = 6, ItemDrop = 7 }

    [Serializable]
    public class MonsterDef
    {
        public MonsterKind kind;
        public string nameRu, nameEn;
        public float maxHealth = 100f;
        public float damage = 20f;
        public DamageType damageType = DamageType.Bite;
        public float attackRange = 2.2f;
        public float attackCooldown = 1.4f;
        public float moveSpeed = 3.2f;
        public float chaseSpeed = 5.4f;
        public float sightRange = 22f;
        public float sightAngle = 100f;
        public float hearingRange = 40f;
        public LootTier tier = LootTier.Tier1;
        public LevelTheme homeLevel = LevelTheme.Corridors;
        public bool isBoss = false;             // боссы в игру не спавнятся (решение опросника)
        public float weakPointMultiplier = 1f;  // урон в слабую точку
        public bool canLoseTarget = true;
        public bool huntsByLight = false;       // реагирует на фонарь/факел
        public string lootTable;
    }

    public static class MonsterTable
    {
        static readonly Dictionary<MonsterKind, MonsterDef> _t = new Dictionary<MonsterKind, MonsterDef>
        {
            { MonsterKind.Smiler, new MonsterDef {
                kind = MonsterKind.Smiler, nameRu = "Улыбающийся", nameEn = "Smiler",
                maxHealth = 120, damage = 18, attackRange = 2.0f, attackCooldown = 1.6f,
                moveSpeed = 1.6f, chaseSpeed = 4.2f, sightRange = 18, sightAngle = 70,
                hearingRange = 45, tier = LootTier.Tier1, homeLevel = LevelTheme.Corridors,
                huntsByLight = true, lootTable = "monster.smiler" } },

            { MonsterKind.Hound, new MonsterDef {
                kind = MonsterKind.Hound, nameRu = "Гончая", nameEn = "Hound",
                maxHealth = 90, damage = 24, attackRange = 2.4f, attackCooldown = 1.1f, damageType = DamageType.Slash,
                moveSpeed = 3.4f, chaseSpeed = 6.8f, sightRange = 26, sightAngle = 120,
                hearingRange = 55, tier = LootTier.Tier2, homeLevel = LevelTheme.Poolrooms,
                lootTable = "monster.hound" } },

            { MonsterKind.Partygoer, new MonsterDef {
                kind = MonsterKind.Partygoer, nameRu = "Патигуер", nameEn = "Partygoer",
                maxHealth = 150, damage = 30, attackRange = 2.2f, attackCooldown = 1.8f,
                moveSpeed = 1.8f, chaseSpeed = 5.0f, sightRange = 16, sightAngle = 360,
                hearingRange = 70, tier = LootTier.Tier2, homeLevel = LevelTheme.Poolrooms,
                lootTable = "monster.partygoer" } },

            { MonsterKind.SkinStealer, new MonsterDef {
                kind = MonsterKind.SkinStealer, nameRu = "Свежеватель", nameEn = "Skin-Stealer",
                maxHealth = 200, damage = 55, attackRange = 2.6f, attackCooldown = 1.5f,
                moveSpeed = 2.4f, chaseSpeed = 6.2f, sightRange = 30, sightAngle = 110,
                hearingRange = 50, tier = LootTier.Tier3, homeLevel = LevelTheme.PowerStation,
                lootTable = "monster.skinstealer" } },

            { MonsterKind.Clump, new MonsterDef {
                kind = MonsterKind.Clump, nameRu = "Сгусток", nameEn = "Clump",
                maxHealth = 400, damage = 40, attackRange = 3.0f, attackCooldown = 2.0f, damageType = DamageType.Blunt,
                moveSpeed = 1.0f, chaseSpeed = 3.6f, sightRange = 12, sightAngle = 200,
                hearingRange = 35, tier = LootTier.Tier2, homeLevel = LevelTheme.Poolrooms,
                lootTable = "monster.clump" } },

            // ---------- СВОИ МОНСТРЫ КАЖДОГО УРОВНЯ (v4) ----------
            { MonsterKind.Whisperer, new MonsterDef {
                kind = MonsterKind.Whisperer, nameRu = "Шептун", nameEn = "Whisperer",
                maxHealth = 140, damage = 14, attackRange = 2.4f, attackCooldown = 1.1f, damageType = DamageType.Bite,
                moveSpeed = 2.6f, chaseSpeed = 6.2f, sightRange = 14, sightAngle = 70,
                hearingRange = 95, tier = LootTier.Tier1, homeLevel = LevelTheme.Corridors,
                canLoseTarget = false, huntsByLight = true, lootTable = "monster.whisperer" } },   // L0: слышит всё, идёт на свет
            { MonsterKind.Drowned, new MonsterDef {
                kind = MonsterKind.Drowned, nameRu = "Утопленник", nameEn = "Drowned",
                maxHealth = 320, damage = 26, attackRange = 2.8f, attackCooldown = 1.8f, damageType = DamageType.Bite,
                moveSpeed = 1.5f, chaseSpeed = 3.4f, sightRange = 20, sightAngle = 110,
                hearingRange = 60, tier = LootTier.Tier2, homeLevel = LevelTheme.Poolrooms,
                canLoseTarget = true, huntsByLight = false, lootTable = "monster.drowned" } },      // L37: медленный, толстый
            { MonsterKind.Spark, new MonsterDef {
                kind = MonsterKind.Spark, nameRu = "Искровик", nameEn = "Spark",
                maxHealth = 190, damage = 34, attackRange = 3.0f, attackCooldown = 1.0f, damageType = DamageType.Electric,
                moveSpeed = 4.4f, chaseSpeed = 8.2f, sightRange = 30, sightAngle = 150,
                hearingRange = 110, tier = LootTier.Tier3, homeLevel = LevelTheme.PowerStation,
                canLoseTarget = true, huntsByLight = true, lootTable = "monster.spark" } },         // L3: быстрый, бьёт током
            { MonsterKind.Bacteria, new MonsterDef {
                kind = MonsterKind.Bacteria, nameRu = "Бактерия (архив — в игру не спавнится)", nameEn = "Bacteria",
                maxHealth = 2500, damage = 85, attackRange = 4.0f, attackCooldown = 2.2f, damageType = DamageType.Explosion,
                moveSpeed = 2.0f, chaseSpeed = 5.5f, sightRange = 45, sightAngle = 180,
                hearingRange = 120, tier = LootTier.Tier3, homeLevel = LevelTheme.PowerStation,
                isBoss = true, weakPointMultiplier = 3.0f, lootTable = "monster.bacteria" } },
        };

        public static MonsterDef Get(MonsterKind k) => _t.TryGetValue(k, out var d) ? d : _t[MonsterKind.Smiler];
        public static IEnumerable<MonsterDef> All => _t.Values;
    }

    /// <summary>Событие шума — монстры «слышат» его и идут проверять (главная механика саспенса).</summary>
    public struct NoiseEvent
    {
        public Vector3 position;
        public float radius;
        public NoiseType type;
        public ulong sourceId;
        public float time;
    }

    public static class NoiseSystem
    {
        public static readonly List<NoiseEvent> Recent = new List<NoiseEvent>(64);
        const float KeepSeconds = 12f;

        /// <summary>Сервер и клиент: клиент — только для визуала/звука, сервер — для ИИ.</summary>
        public static void Emit(Vector3 pos, float radius, NoiseType type, ulong sourceId = 0)
        {
            Recent.Add(new NoiseEvent { position = pos, radius = radius, type = type, sourceId = sourceId, time = Time.time });
            if (Recent.Count > 128) Recent.RemoveAt(0);
        }

        public static bool TryGetHeard(Vector3 listenerPos, float hearingRange, out NoiseEvent ev)
        {
            ev = default;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < Recent.Count; i++)
            {
                var e = Recent[i];
                if (Time.time - e.time > KeepSeconds) continue;
                float d = Vector3.Distance(e.position, listenerPos);
                if (d > Mathf.Min(hearingRange, e.radius)) continue;
                if (d < best) { best = d; ev = e; found = true; }
            }
            return found;
        }

        public static void Clear() => Recent.Clear();
    }

    /// <summary>Позиция «давления» монстров — используется рассудком игрока.</summary>
    public static class MonsterDirector
    {
        static readonly List<MonsterRuntime> _live = new List<MonsterRuntime>(256);
        public static IReadOnlyList<MonsterRuntime> Live => _live;
        public static void Register(MonsterRuntime m) { if (!_live.Contains(m)) _live.Add(m); }
        public static void Unregister(MonsterRuntime m) => _live.Remove(m);

        /// <summary>0..1 — насколько рядом «плохо» (для sanity и музыки).</summary>
        public static float PressureAt(Vector3 pos)
        {
            float pressure = 0f;
            for (int i = 0; i < _live.Count; i++)
            {
                var m = _live[i];
                if (m == null || m.IsDead) continue;
                float d = Vector3.Distance(pos, m.transform.position);
                if (d > 25f) continue;
                float w = 1f - d / 25f;
                pressure += w;
            }
            return Mathf.Clamp01(pressure);
        }

        /// <summary>Волны монстров: 1 в 4 минуты на каждый уровень в одиночке (в сети — сервер).</summary>
        public static void DifficultyTick(float serverTime, int playerCount, LevelTheme level)
        {
            // Вызывается MonsterSpawner'ом: даёт «дышать» между волнами, но не бесконечно.
        }
    }

    /// <summary>
    /// Один монстр. FSM, всё считается на сервере; клиент получает позицию/состояние.
    /// </summary>
    public class MonsterRuntime : NetEntity, IDestructible
    {
        public MonsterKind kind;
        public MonsterDef Def => MonsterTable.Get(kind);
        public MonsterState State { get; private set; } = MonsterState.Idle;
        public float Health { get; private set; }

        float _flavorTimer;      // «характер» вида: свои звуки, чтобы игрок слышал, кто идёт
        public bool IsAlive => Health > 0f && State != MonsterState.Dead;
        public bool IsDead => !IsAlive;

        [Header("Ссылки")]
        public UnityEngine.AI.NavMeshAgent agent;     // если есть NavMesh; иначе A* по графу (Pathfinder)
        public LayerMask sightBlockers;
        public Transform[] patrolPoints;

        // сенсоры
        float _attackReadyAt;
        float _stateEnteredAt;
        float _lastSeenAt;
        Vector3 _lastKnownTarget;
        IDestructible _target;
        int _patrolIndex;
        readonly Collider[] _senseBuf = new Collider[16];

        public event Action<MonsterRuntime, float> DamagedEvent;
        public event Action<MonsterRuntime> DiedEvent;

        public override void WriteSnapshot(BufferWriter w)
        {
            w.WriteByte((byte)kind);
            w.WriteByte((byte)State);
            w.WritePosition(transform.position, -100f, 1000f);
            w.WriteAngles(transform.rotation);
            w.WriteUShort((ushort)Mathf.Clamp(Mathf.RoundToInt(Health), 0, 65535));
        }

        public override void ReadSnapshot(BufferReader r)
        {
            kind = (MonsterKind)r.ReadByte();
            State = (MonsterState)r.ReadByte();
            var pos = r.ReadPosition(-100f, 1000f);
            var rot = r.ReadAngles();
            Health = r.ReadUShort();
            // клиент: плавная интерполяция
            _interp.Push(NetworkBridge.Host?.ServerTime ?? Time.time, pos, rot, Vector3.zero);
        }

        readonly SnapshotInterpolator _interp = new SnapshotInterpolator();

        void Awake()
        {
            Health = Def.maxHealth;
            if (agent == null) agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();                 // NetEntity: InterestGrid.Register(this) — один раз
            MonsterDirector.Register(this);
        }

        protected override void OnDisable()
        {
            MonsterDirector.Unregister(this);
            base.OnDisable();                // NetEntity: InterestGrid.Unregister(this)
        }

        void Update()
        {
            if (!NetworkBridge.IsServer)
            {
                // Клиент: только сглаживание и анимация
                if (_interp.Sample(NetworkBridge.Host?.ServerTime ?? Time.time, out var p, out var q))
                {
                    transform.position = Vector3.Lerp(transform.position, p, Time.deltaTime * 12f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, q, Time.deltaTime * 12f);
                }
                return;
            }

            if (!IsAlive) return;
            Flavor();
            Sense();
            Think();
            Act();
        }

        /// <summary>У каждого вида свой «голос» — игрок по звуку понимает, кто рядом.</summary>
        void Flavor()
        {
            _flavorTimer -= Time.deltaTime;
            if (_flavorTimer > 0f) return;
            switch (kind)
            {
                case MonsterKind.Whisperer:      // шёпот-манить: игрок идёт на звук — как в Backrooms
                    NoiseSystem.Emit(transform.position, 34f, NoiseType.Scream, Net.Value);
                    _flavorTimer = 16f;
                    break;
                case MonsterKind.Drowned:        // мокрые шаги по плитке: слышно издалека
                    NoiseSystem.Emit(transform.position, 22f, NoiseType.Footstep, Net.Value);
                    _flavorTimer = 6.5f;
                    break;
                case MonsterKind.Spark:          // треск электричества: слышно раньше, чем видно
                    NoiseSystem.Emit(transform.position, 18f, NoiseType.Door, Net.Value);
                    _flavorTimer = 9f;
                    break;
                default:
                    _flavorTimer = 30f;          // остальные молчат
                    break;
            }
        }

        // ================== СЕНСОРЫ ==================
        void Sense()
        {
            _target = FindVisibleTarget();
            if (_target != null)
            {
                _lastSeenAt = Time.time;
                _lastKnownTarget = GetPosition(_target);
                return;
            }
            // слух: шум поднимает тревогу (и делает бесполезным «сидеть тихо» с автоматом)
            if (NoiseSystem.TryGetHeard(transform.position, Def.hearingRange, out var ev))
            {
                _lastKnownTarget = ev.position;
                _lastSeenAt = Time.time;
                if (State == MonsterState.Idle || State == MonsterState.Patrol)
                    SetState(MonsterState.Investigate);
            }
        }

        IDestructible FindVisibleTarget()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, Def.sightRange, _senseBuf,
                Layers.Mask(Layers.Player), QueryTriggerInteraction.Ignore);
            IDestructible best = null; float bestScore = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                var d = _senseBuf[i].GetComponentInParent<IDestructible>();
                if (d == null || !d.IsAlive) continue;
                Vector3 p = GetPosition(d);
                Vector3 dir = (p - transform.position);
                float dist = dir.magnitude;
                if (dist > Def.sightRange) continue;
                float angle = Vector3.Angle(transform.forward, dir);
                if (angle > Def.sightAngle * 0.5f) continue;
                if (!HasLineOfSight(p)) continue;
                // Skin-Stealer «прикидывается» и атакует только вблизи; Smiler идёт на свет
                float score = 1f - dist / Def.sightRange;
                if (Def.huntsByLight && IsCarryingLight(d)) score += 0.4f;
                if (score > bestScore) { bestScore = score; best = d; }
            }
            return best;
        }

        bool HasLineOfSight(Vector3 targetPos)
        {
            Vector3 eye = transform.position + Vector3.up * 1.7f;
            Vector3 dir = (targetPos + Vector3.up * 1.2f - eye).normalized;
            if (Physics.Raycast(eye, dir, out var hit, Def.sightRange, sightBlockers, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<IDestructible>() != null;
            return true;
        }

        static bool IsCarryingLight(IDestructible d)
        {
            var mb = d as MonoBehaviour;
            return mb != null && mb.GetComponentInChildren<Light>() != null && mb.GetComponentInChildren<Light>().enabled;
        }

        static Vector3 GetPosition(IDestructible d) => d is MonoBehaviour m ? m.transform.position : Vector3.zero;

        // ================== ЛОГИКА ==================
        void Think()
        {
            switch (State)
            {
                case MonsterState.Idle:
                    if (CanSee()) SetState(MonsterState.Chase);
                    else if (Time.time - _stateEnteredAt > 6f) SetState(MonsterState.Patrol);
                    break;

                case MonsterState.Patrol:
                    if (CanSee()) SetState(MonsterState.Chase);
                    else if (ReachedPatrolPoint()) NextPatrolPoint();
                    break;

                case MonsterState.Investigate:
                    if (CanSee()) SetState(MonsterState.Chase);
                    else if (Vector3.Distance(transform.position, _lastKnownTarget) < 1.5f)
                        SetState(Time.time - _lastSeenAt > 8f ? MonsterState.Patrol : MonsterState.Stalk);
                    break;

                case MonsterState.Stalk:
                    // «принюхивается»: держит дистанцию, копит давление рассудка (Backrooms-саспенс)
                    if (CanSee() && Time.time - _lastSeenAt < 3f) SetState(MonsterState.Chase);
                    else if (Time.time - _stateEnteredAt > 10f) SetState(MonsterState.Investigate);
                    break;

                case MonsterState.Chase:
                    if (!CanSee() && Def.canLoseTarget && Time.time - _lastSeenAt > 6f) SetState(MonsterState.Investigate);
                    else if (DistanceToTarget() <= Def.attackRange) SetState(MonsterState.Attack);
                    break;

                case MonsterState.Attack:
                    if (DistanceToTarget() > Def.attackRange * 1.2f) SetState(MonsterState.Chase);
                    break;

                case MonsterState.Flee:
                    if (Health > Def.maxHealth * 0.4f) SetState(MonsterState.Chase);
                    break;
            }
        }

        void Act()
        {
            var def = Def;
            switch (State)
            {
                case MonsterState.Idle: break;

                case MonsterState.Patrol:
                    MoveTo(CurrentPatrolPoint());
                    break;

                case MonsterState.Investigate:
                case MonsterState.Stalk:
                    MoveTo(_lastKnownTarget, def.moveSpeed);
                    break;

                case MonsterState.Chase:
                    MoveTo(_lastKnownTarget, def.chaseSpeed);
                    // крик при погоне — игрок понимает, что его ведут
                    break;

                case MonsterState.Attack:
                    if (Time.time >= _attackReadyAt) { DoAttack(); _attackReadyAt = Time.time + def.attackCooldown; }
                    break;

                case MonsterState.Flee:
                    MoveTo(transform.position - (GetPosition(_target ?? this) - transform.position), def.chaseSpeed);
                    break;
            }

        }

        bool IsVisibleFrom(Transform other)
        {
            Vector3 dir = (transform.position + Vector3.up * 1.5f) - other.position;
            return Vector3.Angle(other.forward, dir) < 60f;
        }

        void DoAttack()
        {
            if (_target == null || !_target.IsAlive) return;
            var region = UnityEngine.Random.value < 0.12f ? HitRegion.Head : HitRegion.Body;
            float dmg = Def.damage * HitboxResolver.RegionMultiplier(region, false);
            // Игрока бьём через CombatNet: удалённой жертве уедет адресный RPC "dmg"
            // (урон в этом же процессе применился бы к серверному двойнику, а не к игроку).
            if (!CombatNet.ServerDamage(_target, Def.damageType, dmg, transform.position, region, Def.nameRu))
                _target.ApplyDamage(Def.damageType, dmg, transform.position, region);
            NoiseSystem.Emit(transform.position, 25f, NoiseType.Scream);
        }

        void MoveTo(Vector3 pos) => MoveTo(pos, Def.moveSpeed);

        void MoveTo(Vector3 pos, float speed)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.speed = speed;
                agent.isStopped = false;
                agent.SetDestination(pos);
                return;
            }
            // Без NavMesh: прямое движение + «расталкивание» (fallback; графовый A* — Pathfinder.cs)
            Vector3 dir = (pos - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();
            transform.position += dir * speed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);
        }

        bool CanSee() => _target != null && Time.time - _lastSeenAt < 1.5f;
        float DistanceToTarget() => _target == null ? 999f : Vector3.Distance(transform.position, GetPosition(_target));
        Vector3 CurrentPatrolPoint() => patrolPoints != null && patrolPoints.Length > 0 ? patrolPoints[_patrolIndex].position : transform.position;
        bool ReachedPatrolPoint() => Vector3.Distance(transform.position, CurrentPatrolPoint()) < 1.2f;
        void NextPatrolPoint() { if (patrolPoints != null && patrolPoints.Length > 0) _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length; }

        void SetState(MonsterState s)
        {
            if (State == s) return;
            State = s;
            _stateEnteredAt = Time.time;
            if (s == MonsterState.Chase) NoiseSystem.Emit(transform.position, Def.hearingRange, NoiseType.Scream);
        }

        // ================== УРОН И СМЕРТЬ ==================
        public void ApplyDamage(DamageType type, float amount, Vector3 from, HitRegion region)
        {
            if (!NetworkBridge.IsServer || !IsAlive) return;

            // Все монстры Backrooms боятся громкого/взрывного и почти не резистят пулям,
            // но у них есть слабая точка (weak point) — как «backstab» по боссу.
            float mult = region == HitRegion.WeakPoint ? Def.weakPointMultiplier : 1f;
            if (type == DamageType.Fire) mult *= 1.5f;      // огонь — самый эффективный
            if (type == DamageType.Explosion) mult *= 1.25f;

            Health -= amount * mult;
            DamagedEvent?.Invoke(this, amount * mult);
            // аграция: монстр всегда знает, кто его ударил (если рядом)
            _lastKnownTarget = from; _lastSeenAt = Time.time;
            if (State == MonsterState.Idle || State == MonsterState.Patrol) SetState(MonsterState.Investigate);

            if (Health <= 0f) Die(from);
        }

        public void ApplyDamage(DamageType type, float amount) => ApplyDamage(type, amount, transform.position, HitRegion.Body);

        void Die(Vector3 killerPos)
        {
            State = MonsterState.Dead;
            Health = 0f;
            DiedEvent?.Invoke(this);
            Subsistence.World.LootSpawner.SpawnMonsterLoot(Def, transform.position);
            MonsterDirector.Unregister(this);
            Destroy(gameObject, 0.2f);
        }
    }

    /// <summary>Спавнер: держит популяцию на уровне, учитывает число игроков (для сети).</summary>
    public class MonsterSpawner : MonoBehaviour
    {
        [Serializable]
        public class Wave
        {
            public LevelTheme level;
            public MonsterKind kind;
            public int baseCount = 4;
            public int perPlayer = 1;
            public float spawnInterval = 45f;
        }

        public static MonsterSpawner Instance { get; private set; }

        public Wave[] waves = Array.Empty<Wave>();
        public GameObject[] monsterPrefabs;   // индекс = MonsterKind
        public Transform[] spawnPoints;       // заполняет LevelGenerator

        void Awake() { Instance = this; }

        readonly Dictionary<MonsterKind, int> _alive = new Dictionary<MonsterKind, int>();
        float _timer;

        void Update()
        {
            if (!NetworkBridge.IsServer) return;
            _timer += Time.deltaTime;
            if (_timer < 20f) return;
            _timer = 0f;

            int players = Mathf.Max(1, NetworkBridge.Host?.PlayerCount ?? 1);
            for (int i = 0; i < waves.Length; i++)
            {
                var w = waves[i];
                int target = w.baseCount + w.perPlayer * (players - 1);
                _alive.TryGetValue(w.kind, out int cur);
                if (cur >= target) continue;
                if (spawnPoints == null || spawnPoints.Length == 0) continue;
                // 39_trading: в безопасных комнатах монстры не появляются
                Transform pt = null;
                for (int attempt = 0; attempt < 6 && pt == null; attempt++)
                {
                    var candidate = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                    if (candidate != null && !SafeRoom.IsSafe(candidate.position)) pt = candidate;
                }
                if (pt == null) continue;
                Spawn(w.kind, pt.position);
            }
        }

        public MonsterRuntime Spawn(MonsterKind kind, Vector3 pos)
        {
            GameObject prefab = monsterPrefabs != null && (int)kind < monsterPrefabs.Length ? monsterPrefabs[(int)kind] : null;
            GameObject go = prefab != null ? Instantiate(prefab, pos, Quaternion.identity) : CreateFallback(kind, pos);
            var rt = go.GetComponent<MonsterRuntime>();
            if (rt == null) rt = go.AddComponent<MonsterRuntime>();
            rt.kind = kind;
            rt.AssignNetId(new NetId((uint)(UnityEngine.Random.Range(100000, 999999))));
            if (go.GetComponent<MonsterAudio>() == null) go.AddComponent<MonsterAudio>();       // голос вида
            if (go.GetComponent<MonsterMotion>() == null) go.AddComponent<MonsterMotion>();     // процедурная анимация
            rt.DiedEvent += _ => { _alive.TryGetValue(kind, out int c); _alive[kind] = Mathf.Max(0, c - 1); };
            _alive.TryGetValue(kind, out int cur); _alive[kind] = cur + 1;
            return rt;
        }

        /// <summary>
        /// Тело монстра. Если готовая модель из Blender доступна через World/ModelLibrary
        /// (папка Assets/Subsistence/Resources/Models, см. меню «Subsistence → 7») — ставим её,
        /// иначе остаётся капсула-заглушка. Так игра одинаково работает и до, и после
        /// подключения моделей.
        /// </summary>
        static GameObject CreateFallback(MonsterKind kind, Vector3 pos)
        {
            var def = MonsterTable.Get(kind);
            float height = HeightOf(kind);

            string model = World.ModelLibrary.ForMonster(kind);
            if (!string.IsNullOrEmpty(model) && World.ModelLibrary.Has(model))
            {
                var go = new GameObject(def.nameEn);
                go.transform.position = pos;

                var visual = World.ModelLibrary.AttachFitted(model, go.transform, height, UnityEngine.Random.Range(0f, 360f));
                if (visual != null)
                {
                    World.ModelLibrary.MakeVisualOnly(visual, Layers.Monster);
                    go.layer = Layers.Monster;
                    var col = go.AddComponent<CapsuleCollider>();
                    col.center = new Vector3(0f, height * 0.5f, 0f);
                    col.height = Mathf.Max(0.7f, height);
                    col.radius = kind == MonsterKind.Hound ? 0.35f
                               : kind == MonsterKind.Whisperer ? 0.30f
                               : kind == MonsterKind.Drowned ? 0.55f
                               : kind == MonsterKind.Spark ? 0.40f : 0.45f;
                    go.AddComponent<MonsterRuntime>();
                    return go;
                }
            }

            var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.name = def.nameEn;
            fallback.transform.position = pos;
            fallback.transform.localScale = kind == MonsterKind.Bacteria ? new Vector3(2.2f, 3.2f, 2.2f) : new Vector3(0.8f, 1.8f, 0.8f);
            fallback.AddComponent<MonsterRuntime>();
            return fallback;
        }

        /// <summary>Высота модели монстра — подобрана под FBX из tools/blender/models_chars.py.</summary>
        public static float HeightOf(MonsterKind kind)
        {
            switch (kind)
            {
                case MonsterKind.Hound: return 1.05f;        // четвероногая
                case MonsterKind.Partygoer: return 2.05f;
                case MonsterKind.SkinStealer: return 2.20f;
                case MonsterKind.Clump: return 1.60f;
                case MonsterKind.Whisperer: return 1.85f;    // L0: тощий, вытянутый
                case MonsterKind.Drowned: return 1.70f;      // L37: раздутый, полусогнутый
                case MonsterKind.Spark: return 2.10f;        // L3: обгоревший, на протезах
                case MonsterKind.Bacteria: return 3.40f;     // архивная модель, в игре не спавнится
                default: return 1.95f;                       // Smiler
            }
        }
    }

    /// <summary>
    /// A* по графу комнат уровня (без NavMesh): универсально для процедурных коридоров,
    /// бассейнов и станции, и главное — не требует бейка для каждого сида.
    /// </summary>
    public class Pathfinder
    {
        public class Node
        {
            public Vector3 position;
            public readonly List<(Node node, float cost)> links = new List<(Node, float)>(4);
        }

        readonly List<Node> _nodes = new List<Node>(1024);
        public IReadOnlyList<Node> Nodes => _nodes;

        public Node AddNode(Vector3 pos, float linkRadius = 6f)
        {
            var n = new Node { position = pos };
            for (int i = 0; i < _nodes.Count; i++)
            {
                float d = Vector3.Distance(_nodes[i].position, pos);
                if (d <= linkRadius)
                {
                    _nodes[i].links.Add((n, d));
                    n.links.Add((_nodes[i], d));
                }
            }
            _nodes.Add(n);
            return n;
        }

        public Node Nearest(Vector3 p)
        {
            Node best = null; float bd = float.MaxValue;
            for (int i = 0; i < _nodes.Count; i++)
            {
                float d = (_nodes[i].position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = _nodes[i]; }
            }
            return best;
        }

        /// <summary>A*: возвращает список точек пути (пустой, если пути нет).</summary>
        public List<Vector3> FindPath(Vector3 from, Vector3 to, int maxIterations = 4096)
        {
            var start = Nearest(from); var goal = Nearest(to);
            var result = new List<Vector3>(32);
            if (start == null || goal == null) return result;

            var open = new List<Node> { start };
            var cameFrom = new Dictionary<Node, Node>(256);
            var g = new Dictionary<Node, float>(256) { [start] = 0f };
            var f = new Dictionary<Node, float>(256) { [start] = Vector3.Distance(start.position, goal.position) };

            int iter = 0;
            while (open.Count > 0 && iter++ < maxIterations)
            {
                int bestIdx = 0;
                for (int i = 1; i < open.Count; i++) if (f[open[i]] < f[open[bestIdx]]) bestIdx = i;
                var current = open[bestIdx];
                if (current == goal)
                {
                    var cur = goal;
                    while (cur != start) { result.Add(cur.position); cameFrom.TryGetValue(cur, out cur); }
                    result.Reverse();
                    return result;
                }
                open.RemoveAt(bestIdx);
                foreach (var (nb, cost) in current.links)
                {
                    float tentative = g[current] + cost;
                    if (g.TryGetValue(nb, out float old) && tentative >= old) continue;
                    cameFrom[nb] = current;
                    g[nb] = tentative;
                    f[nb] = tentative + Vector3.Distance(nb.position, goal.position);
                    if (!open.Contains(nb)) open.Add(nb);
                }
            }
            return result;
        }
    }
}
