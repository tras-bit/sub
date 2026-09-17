// ============================================================================
//  SUBSISTENCE — Building/BuildSystem.cs
//  Строительство «копия Rust до мелочей»:
//   • грид-сокеты (foundation → wall/doorway/window/floor/stairs/roof);
//   • тиры twig → wood → stone → metal → armored (апгрейд молотком с расходом);
//   • СТАБИЛЬНОСТЬ: блок без опоры разваливается (BFS от фундамента, глубина 24);
//   • DECAY + UPKEEP: постройка без ресурсов в Tool Cupboard гниёт;
//   • Tool Cupboard = авторизация (нельзя строить/ломать чужое в радиусе);
//   • рейд-урон C4/сачель/ракетами/киркой — таблица как в Rust;
//   • двери, кодовые замки, ворота, турели, ловушки.
//  Все изменения проходят через сервер (NetworkBridge.IsServer).
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Combat;
using Subsistence.Net;

namespace Subsistence.Building
{
    public enum BuildMaterialClass { Soft, Wood, Stone, Metal, Hard }

    /// <summary>Тир постройки: HP, расход ресурсов, класс материала, множители рейда.</summary>
    [Serializable]
    public struct BuildGradeDef
    {
        public BuildTier tier;
        public string id;
        public float hpWall, hpFoundation, hpFloor, hpRoof, hpDoor;
        public int wood, stone, metalFragments, hqm;      // стоимость апгрейда (как в Rust)
        public BuildMaterialClass matClass;
        public float decayPerHour;
        public string upgradeSound;
    }

    public static class BuildGrades
    {
        public static readonly Dictionary<BuildTier, BuildGradeDef> All = new Dictionary<BuildTier, BuildGradeDef>
        {
            { BuildTier.Twig, new BuildGradeDef { tier=BuildTier.Twig, id="twig", hpWall=10, hpFoundation=10, hpFloor=10, hpRoof=10, hpDoor=10, wood=0, matClass=BuildMaterialClass.Soft, decayPerHour=0f } },
            { BuildTier.Wood, new BuildGradeDef { tier=BuildTier.Wood, id="wood", hpWall=250, hpFoundation=250, hpFloor=250, hpRoof=250, hpDoor=200, wood=200, matClass=BuildMaterialClass.Wood, decayPerHour=30f } },
            { BuildTier.Stone, new BuildGradeDef { tier=BuildTier.Stone, id="stone", hpWall=500, hpFoundation=500, hpFloor=500, hpRoof=500, hpDoor=250, stone=300, matClass=BuildMaterialClass.Stone, decayPerHour=25f } },
            { BuildTier.Metal, new BuildGradeDef { tier=BuildTier.Metal, id="metal", hpWall=1000, hpFoundation=1000, hpFloor=1000, hpRoof=1000, hpDoor=250, metalFragments=200, matClass=BuildMaterialClass.Metal, decayPerHour=20f } },
            { BuildTier.Armored, new BuildGradeDef { tier=BuildTier.Armored, id="armored", hpWall=2000, hpFoundation=2000, hpFloor=2000, hpRoof=2000, hpDoor=800, hqm=25, matClass=BuildMaterialClass.Hard, decayPerHour=15f } },
        };

        public static BuildGradeDef Get(BuildTier t) => All.TryGetValue(t, out var d) ? d : All[BuildTier.Twig];
        public static BuildTier Next(BuildTier t) => (BuildTier)Mathf.Min((int)t + 1, (int)BuildTier.Armored);
    }

    /// <summary>Что это за элемент (определяет HP и сокеты).</summary>
    public enum BuildPieceType : byte
    {
        Foundation = 0, FoundationTriangle = 1, Wall = 2, Doorway = 3, Window = 4, Floor = 5,
        FloorTriangle = 6, Stairs = 7, Roof = 8, Ramp = 9, HighWall = 10, Pillar = 11,
        // ответ 9в: было 12 элементов, стало 15
        RampCorner = 12,       // рампа-угол: подъём по диагонали в угол клетки
        Railing = 13,          // перила: перекладины и балясины
        Shutters = 14          // ставни: закрывают окно/проём
    }

    /// <summary>
    /// Один блок постройки. Хранит владельца, тир, прочность, стабильность, апкип.
    /// </summary>
    public class BuildBlock : MonoBehaviour, IDestructible
    {
        public BuildPieceType piece;
        public BuildTier tier = BuildTier.Twig;
        public ulong ownerId;
        public uint buildingId;                     // все блоки одной базы делят id (нужно для upkeep/decay)
        public float health;
        public int stability = int.MaxValue;         // «глубина» до фундамента
        public bool isFoundation;

        // ---- Rust-правила постройки (v0.2) ----
        public float placedAt;                     // время постановки: снос с возвратом — 10 минут
        public Vector3 softSideLocal;              // «мягкая сторона»: куда смотрел строитель
        public bool hasSoftSide;

        public const float SoftSideMultiplier = 2.5f;      // ближний бой по мягкой стороне
        public const float DemolishRefundSeconds = 600f;   // 10 минут, как в Rust

        public float MaxHp => MaxHpFor(piece, Grade);
        public float HealthFraction => Mathf.Clamp01(health / Mathf.Max(1f, MaxHp));
        public bool IsDamaged => health < MaxHp - 0.5f;
        public bool CanDemolish => Time.time - placedAt <= DemolishRefundSeconds;
        public float DemolishSecondsLeft => Mathf.Max(0f, DemolishRefundSeconds - (Time.time - placedAt));

        /// <summary>Запомнить мягкую сторону (направление от блока к строителю).</summary>
        public void SetSoftSide(Vector3 worldDir)
        {
            if (worldDir.sqrMagnitude < 0.0001f) return;
            softSideLocal = transform.InverseTransformDirection(worldDir.normalized);
            hasSoftSide = true;
        }

        /// <summary>Удар пришёл в мягкую сторону? (тогда ближний бой бьёт сильнее).</summary>
        public bool IsSoftSideHit(Vector3 worldHitDir)
        {
            if (!hasSoftSide || worldHitDir.sqrMagnitude < 0.0001f) return false;
            var local = transform.InverseTransformDirection(worldHitDir.normalized);
            return Vector3.Dot(local, softSideLocal) > 0.35f;
        }
        public NetId NetId { get; private set; }

        BuildGradeDef Grade => BuildGrades.Get(tier);
        public BuildMaterialClass MaterialClass => Grade.matClass;
        public bool IsAlive => health > 0f;

        public event Action<BuildBlock> Destroyed;

        public void Initialize(BuildPieceType type, BuildTier t, ulong owner, uint building, float hpOverride = -1f)
        {
            piece = type; tier = t; ownerId = owner; buildingId = building;
            var g = BuildGrades.Get(t);
            health = hpOverride > 0 ? hpOverride : MaxHpFor(type, g);
        }

        public static float MaxHpFor(BuildPieceType piece, BuildGradeDef g)
        {
            switch (piece)
            {
                case BuildPieceType.Foundation:
                case BuildPieceType.FoundationTriangle: return g.hpFoundation;
                case BuildPieceType.Floor:
                case BuildPieceType.FloorTriangle:
                case BuildPieceType.Roof:
                case BuildPieceType.Ramp:
                case BuildPieceType.RampCorner: return g.hpFloor;
                case BuildPieceType.Railing: return g.hpWall * 0.6f;      // перила — тонкая деталь
                case BuildPieceType.Shutters: return g.hpWall * 0.8f;     // ставни: чуть слабее стены
                default: return g.hpWall;
            }
        }

        /// <summary>Урон от рейда с учётом класса материала (таблица RaidTable) и зоны попадания.</summary>
        public void ApplyRaidDamage(float amount, DamageType type) => ApplyRaidDamage(amount, type, Vector3.zero);

        /// <summary>
        /// Урон по блоку. hitDir — направление удара (из оружия): если он пришёлся в мягкую
        /// сторону, ближний бой бьёт в SoftSideMultiplier раз сильнее (взрывчатке всё равно).
        /// </summary>
        public void ApplyRaidDamage(float amount, DamageType type, Vector3 hitDir)
        {
            if (!NetworkBridge.IsServer) return;
            float mult = RaidTable.Multiplier(type, Grade.matClass);
            bool melee = type == DamageType.Slash || type == DamageType.Stab
                      || type == DamageType.Blunt || type == DamageType.Bite;
            if (melee && IsSoftSideHit(hitDir)) mult *= SoftSideMultiplier;
            float dmg = amount * mult;
            health -= dmg;
            if (health <= 0f) ServerDestroy();
            else OnDamaged?.Invoke(dmg);
        }

        /// <summary>Урон от оружия игрока: пуля по дереву/камню почти бесполезна (как в Rust).</summary>
        public virtual void ApplyDamage(DamageType type, float amount, Vector3 from, HitRegion region)
            => ApplyRaidDamage(amount, type);

        public event Action<float> OnDamaged;

        /// <summary>Апгрейд тира (молотком). Списывать ресурсы должен вызывающий (сервер).</summary>
        public bool Upgrade(BuildTier target)
        {
            if (!NetworkBridge.IsServer) return false;
            if (target <= tier) return false;
            float frac = health / Mathf.Max(1f, MaxHpFor(piece, Grade));
            tier = target;
            var g = Grade;
            health = MaxHpFor(piece, g) * Mathf.Clamp(frac, 0.1f, 1f);

            // Визуал: подмена материала/меша по тиру (модели: tools/blender/…, см. ASSET_BIBLE)
            var visual = GetComponent<BuildVisual>();
            visual?.SetTier(target);
            StabilitySolver.MarkDirty(buildingId);
            return true;
        }

        public void Repair(float amount)
        {
            var g = Grade;
            health = Mathf.Min(MaxHpFor(piece, g), health + amount);
        }

        /// <summary>Снос киянкой: обнуляем прочность и убираем блок (реестр/обвал пересчитаются).</summary>
        public void Demolish()
        {
            if (!NetworkBridge.IsServer) return;
            health = 0f;
            ServerDestroy();
        }

        void ServerDestroy()
        {
            Destroyed?.Invoke(this);
            GetComponent<BuildDeployable>()?.OnBlockDestroyed();
            if (NetworkBridge.IsServer)
            {
                StabilitySolver.OnBlockRemoved(this);   // пересчёт опор → возможен обвал верхних блоков
                BuildingRegistry.Unregister(this);
            }
            Destroy(gameObject);
        }
    }

    /// <summary>Множители рейда: чем «жёстче» материал — тем сильнее нужны взрывчатки (Rust-логика).</summary>
    public static class RaidTable
    {
        public static float Multiplier(DamageType type, BuildMaterialClass mat)
        {
            switch (type)
            {
                case DamageType.Explosion:
                    return mat == BuildMaterialClass.Soft ? 3f : mat == BuildMaterialClass.Wood ? 1.5f : 1f;
                case DamageType.Fire:
                    return mat == BuildMaterialClass.Soft ? 4f : mat == BuildMaterialClass.Wood ? 2.5f : 0.1f;
                case DamageType.Bullet:
                    return mat == BuildMaterialClass.Soft ? 1.5f : mat == BuildMaterialClass.Wood ? 0.35f : mat == BuildMaterialClass.Stone ? 0.15f : 0.05f;
                case DamageType.Buckshot:
                    return mat == BuildMaterialClass.Soft ? 1.2f : 0.15f;
                case DamageType.Blunt:      // кувалда/кирка
                    return mat == BuildMaterialClass.Soft ? 3f : mat == BuildMaterialClass.Wood ? 1.2f : mat == BuildMaterialClass.Stone ? 0.5f : 0.15f;
                case DamageType.Slash:
                    return mat == BuildMaterialClass.Soft ? 2.5f : mat == BuildMaterialClass.Wood ? 0.9f : 0.2f;
                case DamageType.Stab:
                    return mat == BuildMaterialClass.Soft ? 2.2f : mat == BuildMaterialClass.Wood ? 0.8f : 0.2f;
                default: return 1f;
            }
        }

        /// <summary>Сколько зарядов нужно на блок (для баланса/C4-калькулятора как в Rust).</summary>
        public static int ChargesToDestroy(ExplosiveKind kind, BuildPieceType piece, BuildTier tier)
        {
            var g = BuildGrades.Get(tier);
            float hp = BuildBlock.MaxHpFor(piece, g);
            float dmg = ExplosiveTable.Damage(kind);
            float mult = Multiplier(DamageType.Explosion, g.matClass);
            return Mathf.CeilToInt(hp / Mathf.Max(1f, dmg * mult));
        }
    }

    /// <summary>Реестр блоков по buildingId для upkeep/decay/обвалов.</summary>
    public static class BuildingRegistry
    {
        static readonly Dictionary<uint, HashSet<BuildBlock>> _byBuilding = new Dictionary<uint, HashSet<BuildBlock>>(256);
        static uint _nextBuildingId = 1;

        public static uint NewBuildingId() => _nextBuildingId++;

        public static void Register(BuildBlock b)
        {
            if (!_byBuilding.TryGetValue(b.buildingId, out var set)) { set = new HashSet<BuildBlock>(); _byBuilding[b.buildingId] = set; }
            set.Add(b);
        }

        public static void Unregister(BuildBlock b)
        {
            if (_byBuilding.TryGetValue(b.buildingId, out var set))
            {
                set.Remove(b);
                if (set.Count == 0) _byBuilding.Remove(b.buildingId);
            }
        }

        public static IEnumerable<BuildBlock> Of(uint buildingId)
            => _byBuilding.TryGetValue(buildingId, out var set) ? set : (IEnumerable<BuildBlock>)Array.Empty<BuildBlock>();

        public static int CountOf(uint buildingId) => _byBuilding.TryGetValue(buildingId, out var s) ? s.Count : 0;

        /// <summary>Список всех «баз» (для гниения построек без шкафа).</summary>
        public static List<uint> AllBuildingIds()
        {
            var list = new List<uint>(_byBuilding.Count);
            foreach (var kv in _byBuilding) if (kv.Value.Count > 0) list.Add(kv.Key);
            return list;
        }
        public static void Clear() { _byBuilding.Clear(); _nextBuildingId = 1; }
    }

    /// <summary>
    /// Стабильность: блок «держится», если связан с фундаментом/землёй цепочкой
    /// не длиннее 24 (как в Rust). При удалении блока — BFS и обвал «висящих» верхних частей.
    /// </summary>
    public static class StabilitySolver
    {
        static readonly HashSet<uint> _dirty = new HashSet<uint>();

        public static void MarkDirty(uint buildingId) => _dirty.Add(buildingId);

        public static void OnBlockRemoved(BuildBlock removed)
        {
            if (removed == null) return;
            _dirty.Add(removed.buildingId);
            // обвал делаем с задержкой (в Rust блоки падают через ~3 сек после потери опоры)
            CollapseScheduler.Schedule(removed.buildingId);
        }

        public static void Solve(uint buildingId)
        {
            var blocks = new HashSet<BuildBlock>(BuildingRegistry.Of(buildingId));
            if (blocks.Count == 0) return;

            // 1) ищем «корни»: фундаменты на земле + блоки с флагом isFoundation
            var depths = new Dictionary<BuildBlock, int>(blocks.Count);
            var queue = new Queue<BuildBlock>();
            foreach (var b in blocks)
                if (b.isFoundation) { depths[b] = 0; queue.Enqueue(b); }

            // 2) BFS по связям (соседи, которые соприкасаются гранями)
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                int d = depths[cur];
                foreach (var nb in Neighbours(cur))
                {
                    if (!blocks.Contains(nb)) continue;
                    int nd = d + 1;
                    if (depths.TryGetValue(nb, out int old) && old <= nd) continue;
                    depths[nb] = nd; queue.Enqueue(nb);
                }
            }

            // 3) всё, что не достали (глубже MAX или ваще не связано) — обвал
            foreach (var b in blocks)
            {
                if (!depths.TryGetValue(b, out int depth) || depth > Balance.MaxStabilityDepth)
                    CollapseScheduler.Schedule(b, NetworkBridge.IsServer ? 3f : 99f);
                else
                    b.stability = depth;
            }
        }

        static IEnumerable<BuildBlock> Neighbours(BuildBlock b)
        {
            // Соседи ищутся по сокетам (BuildSocket), к которым блок привязан при постановке
            var sockets = b.GetComponentsInChildren<BuildSocket>();
            for (int i = 0; i < sockets.Length; i++)
                if (sockets[i].connected != null) yield return sockets[i].connected;
        }
    }

    /// <summary>Отложенный обвал (в Rust «падение» видно и слышно).</summary>
    public static class CollapseScheduler
    {
        class Item { public BuildBlock block; public uint buildingId; public float at; public bool solveBuilding; }
        static readonly List<Item> _items = new List<Item>(64);

        public static void Schedule(BuildBlock b, float delay = Balance.StabilityCollapseSeconds)
        {
            _items.Add(new Item { block = b, at = Time.time + delay });
        }

        public static void Schedule(uint buildingId) => _items.Add(new Item { buildingId = buildingId, solveBuilding = true, at = Time.time + Balance.StabilityCollapseSeconds });

        /// <summary>Вызывается BuildingRuntime каждый кадр (сервер и клиент-хост).</summary>
        public static void Tick()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (Time.time < _items[i].at) continue;
                var it = _items[i];
                _items.RemoveAt(i);
                if (it.solveBuilding) StabilitySolver.Solve(it.buildingId);
                else if (it.block != null)
                {
                    // «падает»: открепляем и роняем физикой (клиент увидит)
                    var rb = it.block.GetComponent<Rigidbody>();
                    if (rb == null) rb = it.block.gameObject.AddComponent<Rigidbody>();
                    rb.mass = 80f;
                    it.block.transform.SetParent(null, true);
                    BuildingRegistry.Unregister(it.block);
                    UnityEngine.Object.Destroy(it.block.gameObject, 8f);
                }
            }
        }
        public static void Clear() => _items.Clear();
    }

    /// <summary>Сокет: точка привязки блока. Хранит связь для расчёта стабильности.</summary>
    public class BuildSocket : MonoBehaviour
    {
        public BuildSocketKind kind = BuildSocketKind.Edge;
        public BuildBlock connected;             // кто занимает сокет
        public BuildBlock parent;                // для стабильности: откуда пришёл
        public Vector3 snapOffset;
    }

    public enum BuildSocketKind : byte { Edge = 0, Corner = 1, Top = 2, Bottom = 3, Side = 4 }

    /// <summary>
    /// Контроллер постановки для игрока: грид 3 м (как в Rust), снап по сокетам,
    /// вращение R, превью с цветом валидности, проверка привилегий и стоимости.
    /// </summary>
    public class BuildController : MonoBehaviour
    {
        public const float GridSize = 3f;                 // Rust: фундамент 3×3 м
        public const float MaxPlaceDistance = 6f;

        public Camera viewCamera;
        public PlayerInventory inventory;
        public LayerMask buildMask;

        public BuildPieceType currentPiece = BuildPieceType.Foundation;
        public BuildTier currentTier = BuildTier.Twig;
        public int rotationQuarters;

        [Header("Префабы (заполняет BuildPrefabLibrary)")]
        public BuildPrefabLibrary library;

        GameObject _preview;
        BuildBlock _previewBlock;
        bool _valid;
        float _lastPlaceTime;
        const float PlaceInterval = 0.08f;

        void Update()
        {
            if (inventory == null || viewCamera == null || library == null) return;

            // Достали план застройки?
            var active = inventory.ActiveItem;
            if (active == null || active.id != "building.planner") { ClearPreview(); return; }

            // Выбор элемента: 1..5 цифрами (как Rust: 1 фундамент, 2 стена, 3 дверной проём, 4 окно, 5 перекрытие)
            HandlePieceHotkeys();

            Vector3 point;
            bool hasTarget = FindSnapPoint(out point);
            if (!hasTarget) { ClearPreview(); return; }
            UpdatePreview(point);

            if (Subsistence.Player.PlayerInput.FirePressed && _valid && Time.time - _lastPlaceTime > PlaceInterval)
            {
                _lastPlaceTime = Time.time;
                PlaceBlock(point);
            }
            if (Subsistence.Player.PlayerInput.ReloadPressed)
                rotationQuarters = (rotationQuarters + 1) & 3;
        }

        void HandlePieceHotkeys()
        {
            // 15 элементов (ответ 9в): 1-9, 0, «-», «=», «[», «]», «\»
            if (Input.GetKeyDown(KeyCode.Alpha1)) currentPiece = BuildPieceType.Foundation;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) currentPiece = BuildPieceType.FoundationTriangle;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) currentPiece = BuildPieceType.Wall;
            else if (Input.GetKeyDown(KeyCode.Alpha4)) currentPiece = BuildPieceType.Doorway;
            else if (Input.GetKeyDown(KeyCode.Alpha5)) currentPiece = BuildPieceType.Window;
            else if (Input.GetKeyDown(KeyCode.Alpha6)) currentPiece = BuildPieceType.Floor;
            else if (Input.GetKeyDown(KeyCode.Alpha7)) currentPiece = BuildPieceType.FloorTriangle;
            else if (Input.GetKeyDown(KeyCode.Alpha8)) currentPiece = BuildPieceType.Stairs;
            else if (Input.GetKeyDown(KeyCode.Alpha9)) currentPiece = BuildPieceType.Roof;
            else if (Input.GetKeyDown(KeyCode.Alpha0)) currentPiece = BuildPieceType.Ramp;
            else if (Input.GetKeyDown(KeyCode.Minus)) currentPiece = BuildPieceType.HighWall;
            else if (Input.GetKeyDown(KeyCode.Equals)) currentPiece = BuildPieceType.Pillar;
            else if (Input.GetKeyDown(KeyCode.LeftBracket)) currentPiece = BuildPieceType.RampCorner;
            else if (Input.GetKeyDown(KeyCode.RightBracket)) currentPiece = BuildPieceType.Railing;
            else if (Input.GetKeyDown(KeyCode.Backslash)) currentPiece = BuildPieceType.Shutters;
        }

        bool FindSnapPoint(out Vector3 point)
        {
            point = Vector3.zero;
            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, MaxPlaceDistance, buildMask, QueryTriggerInteraction.Ignore)) return false;

            if (currentPiece == BuildPieceType.Foundation)
            {
                if (hit.collider.gameObject.layer != Layers.LevelGeometry) return false;  // фундамент — только на пол уровня
                point = SnapToGrid(hit.point);
                return true;
            }

            // остальные элементы — к сокетам соседних блоков
            var socket = hit.collider.GetComponentInParent<BuildSocket>();
            if (socket == null) return false;
            point = socket.transform.position + Quaternion.Euler(0, rotationQuarters * 90f, 0) * socket.snapOffset;
            return true;
        }

        public static Vector3 SnapToGrid(Vector3 p)
        {
            float half = GridSize * 0.5f;
            return new Vector3(Mathf.Floor(p.x / GridSize) * GridSize + half, 0f, Mathf.Floor(p.z / GridSize) * GridSize + half);
        }

        void UpdatePreview(Vector3 point)
        {
            var prefab = library.Get(currentPiece, currentTier);
            if (prefab == null) { ClearPreview(); return; }
            if (_preview == null || _preview.name != prefab.name)
            {
                ClearPreview();
                _preview = Instantiate(prefab, point, Quaternion.identity);
                StripPhysics(_preview);            // превью без коллайдеров, иначе она ловит собственные рейкасты
                _previewBlock = _preview.GetComponent<BuildBlock>();
                _preview.name = prefab.name;
            }
            _preview.transform.position = point;
            _preview.transform.rotation = Quaternion.Euler(0, rotationQuarters * 90f, 0);

            _valid = Validate(point, out string reason);
            var visual = _preview.GetComponent<BuildVisual>();
            visual?.SetTint(_valid ? new Color(0.35f, 1f, 0.4f, 0.45f) : new Color(1f, 0.25f, 0.2f, 0.45f));
            Subsistence.UI.HudRuntime.SetBuildHint(_valid ? string.Empty : reason);
        }

        bool Validate(Vector3 point, out string reason)
        {
            reason = null;
            // 1) привилегии (свой TC или вне чужого радиуса)
            if (!BuildingPrivilege.CanBuild(transform.position, NetworkBridge.Host?.LocalPlayerId ?? 0UL, out reason)) return false;
            // 2) зона, где строить нельзя (входы/выходы уровней, аномалии)
            if (Physics.CheckSphere(point + Vector3.up * 1.5f, 1.4f, Layers.Mask(Layers.NoBuild), QueryTriggerInteraction.Collide))
            { reason = "Здесь строить нельзя"; return false; }
            // 3) запрет «блок-в-блок» (перекрытие с существующим)
            if (Physics.CheckBox(point + Vector3.up * 1.5f, Vector3.one * 1.4f, Quaternion.identity, Layers.Mask(Layers.Buildable), QueryTriggerInteraction.Collide))
            { reason = "Занято"; return false; }
            // 4) хватает ли ресурсов для постановки twig
            var cost = BuildCosts.PlacementCost(currentPiece, currentTier);
            if (!inventory.CanAfford(cost)) { reason = "Не хватает ресурсов"; return false; }
            return true;
        }

        void PlaceBlock(Vector3 point)
        {
            // Клиент ничего не создаёт сам (учёт прочности и стабильности — только на сервере),
            // но чтобы не ждать RTT, платит и показывает блок сразу — предсказание (SpawnNet.PredictBlock).
            if (!NetworkBridge.IsServer)
            {
                NetworkBridge.Command(ClientCommand.PlaceBuilding, SerializePlacement(point));
                if (inventory.Pay(BuildCosts.PlacementCost(currentPiece, currentTier)))
                    SpawnNet.PredictBlock(currentPiece, currentTier, rotationQuarters, point);
                return;
            }

            var cost = BuildCosts.PlacementCost(currentPiece, currentTier);
            if (!inventory.Pay(cost)) return;

            var block = SpawnNet.ServerSpawnBlock(currentPiece, currentTier, rotationQuarters, point,
                                                  NetworkBridge.Host?.LocalPlayerId ?? 0UL,
                                                  BuildingRegistry.NewBuildingId(), transform.position);
            if (block == null)                            // префаб не найден — ресурсы игрока не теряем
            {
                for (int i = 0; i < cost.Count; i++) inventory.TryAdd(cost[i]);
                return;
            }
            Subsistence.AI.NoiseSystem.Emit(point, 20f, Subsistence.AI.NoiseType.Building);
            Subsistence.Audio.AudioDirector.PlayAt("build_place", point, 0.7f);
        }

        /// <summary>
        /// Единый путь создания блока: локальная постановка, серверный спавн по команде
        /// и создание на клиенте по object.spawn — всё здесь.
        /// </summary>
        public static BuildBlock SpawnBlock(BuildPrefabLibrary library, BuildPieceType piece, BuildTier tier,
                                            int rotationQuarters, Vector3 point, ulong owner, uint buildingId,
                                            Vector3 placerPos = default(Vector3))
        {
            if (library == null) return null;
            var prefab = library.Get(piece, tier);
            if (prefab == null) return null;

            var go = UnityEngine.Object.Instantiate(prefab, point, Quaternion.Euler(0, rotationQuarters * 90f, 0));
            var block = go.GetComponent<BuildBlock>();
            if (block == null) block = go.AddComponent<BuildBlock>();
            block.Initialize(piece, tier, owner, buildingId);
            block.isFoundation = piece == BuildPieceType.Foundation || piece == BuildPieceType.FoundationTriangle;
            if (placerPos != default(Vector3)) block.SetSoftSide(placerPos - point);   // мягкая сторона — откуда строили
            BuildingRegistry.Register(block);

            // связываем сокеты для стабильности
            foreach (var s in go.GetComponentsInChildren<BuildSocket>())
            {
                var hits = Physics.OverlapSphere(s.transform.position, 0.35f, Layers.Mask(Layers.Buildable), QueryTriggerInteraction.Collide);
                for (int i = 0; i < hits.Length; i++)
                {
                    var nb = hits[i].GetComponentInParent<BuildBlock>();
                    if (nb != null && nb != block) { s.connected = nb; s.parent = nb; break; }
                }
            }
            StabilitySolver.MarkDirty(block.buildingId);
            StabilitySolver.Solve(block.buildingId);
            return block;
        }

        byte[] SerializePlacement(Vector3 point)
        {
            var w = BufferWriter.Rent(48);
            w.WriteByte((byte)currentPiece);
            w.WriteByte((byte)currentTier);
            w.WriteByte((byte)rotationQuarters);
            w.WritePosition(point, -100f, 1000f);
            var data = w.ToArray();
            BufferWriter.Return(w);
            return data;
        }

        void ClearPreview()
        {
            if (_preview != null) Destroy(_preview);
            _preview = null; _previewBlock = null;
        }

        static void StripPhysics(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
            foreach (var nb in go.GetComponentsInChildren<BuildBlock>()) Destroy(nb);
        }
    }

    /// <summary>Стоимость постановки и апгрейда (Rust-совместимо: twig бесплатно/дёшево).</summary>
    public static class BuildCosts
    {
        public static List<ItemStack> PlacementCost(BuildPieceType piece, BuildTier tier)
        {
            var list = new List<ItemStack>(1);
            switch (tier)
            {
                case BuildTier.Twig:
                    // фундамент дороже, лёгкие детали (перила/ставни) дешевле
                    int wood = piece == BuildPieceType.Foundation ? 50
                             : (piece == BuildPieceType.Railing || piece == BuildPieceType.Shutters) ? 15 : 25;
                    list.Add(new ItemStack("wood", wood));
                    break;
                default:
                    list.Add(new ItemStack("wood", 25));   // базовое «полено» на любую постановку в высоком тире
                    break;
            }
            return list;
        }

        /// <summary>Ремонт (Rust): четверть стоимости апгрейда до текущего тира.</summary>
        public static List<ItemStack> RepairCost(BuildPieceType piece, BuildTier tier)
        {
            var full = UpgradeCost(piece, tier);
            var list = new List<ItemStack>(full.Count);
            for (int i = 0; i < full.Count; i++)
                list.Add(new ItemStack(full[i].id, Mathf.Max(1, full[i].amount / 4)));
            return list;
        }

        /// <summary>Возврат при сносе: постановка + все апгрейды до текущего тира.</summary>
        public static List<ItemStack> Refund(BuildPieceType piece, BuildTier tier)
        {
            var total = new Dictionary<string, int>(4);
            void Add(List<ItemStack> list)
            {
                for (int i = 0; i < list.Count; i++)
                    total[list[i].id] = (total.TryGetValue(list[i].id, out int have) ? have : 0) + list[i].amount;
            }
            Add(PlacementCost(piece, BuildTier.Twig));
            for (int t = (int)BuildTier.Wood; t <= (int)tier; t++) Add(UpgradeCost(piece, (BuildTier)t));
            var res = new List<ItemStack>(total.Count);
            foreach (var kv in total) res.Add(new ItemStack(kv.Key, kv.Value));
            return res;
        }

        /// <summary>Ресурсы на апгрейд (как в Rust: wood 200 → stone 300 → metal 200 → armored 25 HQM).</summary>
        public static List<ItemStack> UpgradeCost(BuildPieceType piece, BuildTier target)
        {
            var list = new List<ItemStack>(2);
            switch (target)
            {
                case BuildTier.Wood: list.Add(new ItemStack("wood", 200)); break;
                case BuildTier.Stone: list.Add(new ItemStack("wood", 100)); list.Add(new ItemStack("stones", 300)); break;
                case BuildTier.Metal: list.Add(new ItemStack("metal.fragments", 200)); break;
                case BuildTier.Armored: list.Add(new ItemStack("metal.refined", 25)); break;
            }
            return list;
        }
    }

    /// <summary>Tool Cupboard: авторизация в радиусе, апкип (ресурсы) и радиус запрета стройки.</summary>
    public class ToolCupboard : MonoBehaviour
    {
        public ulong ownerId;
        public uint buildingId;
        public float radius = Balance.BuildPrivilegeRadius;
        public ItemContainer storage = new ItemContainer("Tool Cupboard", 6);
        public readonly HashSet<ulong> authorized = new HashSet<ulong>();

        /// <summary>Все шкафы в мире: нужны, чтобы понять, есть ли у базы хозяин (гниение/апкип).</summary>
        static readonly List<ToolCupboard> _all = new List<ToolCupboard>();
        public static IReadOnlyList<ToolCupboard> All => _all;

        /// <summary>Шкаф, обслуживающий эту базу (или null — тогда база гниёт).</summary>
        public static ToolCupboard FindFor(uint buildingId)
        {
            for (int i = 0; i < _all.Count; i++)
                if (_all[i] != null && _all[i].buildingId == buildingId) return _all[i];
            return null;
        }

        void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        void OnDisable() { _all.Remove(this); }

        float _upkeepTimer;

        void Update()
        {
            if (!NetworkBridge.IsServer) return;
            _upkeepTimer += Time.deltaTime;
            if (_upkeepTimer < Balance.DecayTickSeconds) return;
            _upkeepTimer = 0f;
            PayUpkeep();
        }

        /// <summary>Апкип: сколько блоков — столько и расход. Нет ресурсов → постройка гниёт.</summary>
        public void PayUpkeep()
        {
            int blocks = BuildingRegistry.CountOf(buildingId);
            float need = blocks * 0.05f;                        // ~настраиваемый коэффициент
            int woodNeeded = Mathf.CeilToInt(need);
            bool paid = storage.RemoveAmount("wood", woodNeeded)
                     || storage.RemoveAmount("metal.fragments", Mathf.CeilToInt(woodNeeded / 4f))
                     || storage.RemoveAmount("stones", Mathf.CeilToInt(woodNeeded / 2f));
            if (!paid) DecaySystem.ApplyDecay(buildingId, Balance.DecayTickSeconds);
        }

        public bool IsAuthorized(ulong player) => player == ownerId || authorized.Contains(player);
    }

    /// <summary>
    /// Гниение построек (решение 22_decay): блоки гниют, ТОЛЬКО если у базы нет шкафа (TC).
    /// Шкаф сам решает вопрос — оплатил апкип (лес/камень/металл в нём) → база цела,
    /// не оплатил → гниёт. Стройка при этом разрешена где угодно (25_build_zones),
    /// единственное исключение — чужая территория под шкафом.
    /// </summary>
    public class DecayDirector : MonoBehaviour
    {
        public static DecayDirector Instance { get; private set; }
        float _timer;

        void Awake() { Instance = this; _timer = 0f; }

        void Update()
        {
            if (!NetworkBridge.IsServer) return;
            _timer += Time.deltaTime;
            if (_timer < Balance.DecayTickSeconds) return;
            _timer = 0f;
            Tick();
        }

        /// <summary>Один тик гниения (раз в 10 минут): по каждой базе — свой шкаф или гниение.</summary>
        public static void Tick()
        {
            var ids = BuildingRegistry.AllBuildingIds();
            for (int i = 0; i < ids.Count; i++)
            {
                var tc = ToolCupboard.FindFor(ids[i]);
                if (tc != null) { tc.PayUpkeep(); continue; }   // шкаф есть → он и решает (оплатил/не оплатил)
                DecaySystem.ApplyDecay(ids[i], Balance.DecayTickSeconds);
            }
        }
    }

    /// <summary>Проверка привилегий: чужой TC блокирует стройку/ревосходство в радиусе.</summary>
    public static class BuildingPrivilege
    {
        static readonly Collider[] _buf = new Collider[8];

        public static bool CanBuild(Vector3 pos, ulong playerId, out string reason)
        {
            reason = null;
            int n = Physics.OverlapSphereNonAlloc(pos, Balance.BuildPrivilegeRadius, _buf, Layers.Mask(Layers.Deployable), QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var tc = _buf[i].GetComponentInParent<ToolCupboard>();
                if (tc == null) continue;
                if (!tc.IsAuthorized(playerId)) { reason = "Чужая территория (Tool Cupboard)"; return false; }
            }
            return true;
        }
    }

    /// <summary>Гниение: постройка без апкипа теряет HP каждый тик (и рушится, когда 0).</summary>
    public static class DecaySystem
    {
        public static void ApplyDecay(uint buildingId, float seconds)
        {
            float hours = seconds / 3600f;
            foreach (var b in BuildingRegistry.Of(buildingId))
            {
                if (b == null) continue;
                float dmg = BuildGrades.Get(b.tier).decayPerHour * hours;
                if (dmg <= 0f) continue;
                b.ApplyRaidDamage(dmg, DamageType.Generic);
            }
        }
    }

    /// <summary>Визуал блока: меш/материал по тиру + подсветка превью.</summary>
    public class BuildVisual : MonoBehaviour
    {
        public Renderer[] renderers;
        public Material twigMat, woodMat, stoneMat, metalMat, armoredMat;
        MaterialPropertyBlock _mpb;
        static readonly int TintId = Shader.PropertyToID("_UnlitColor");

        public void SetTier(BuildTier t)
        {
            var mat = t == BuildTier.Twig ? twigMat : t == BuildTier.Wood ? woodMat : t == BuildTier.Stone ? stoneMat :
                      t == BuildTier.Metal ? metalMat : armoredMat;
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++) if (mat != null) renderers[i].sharedMaterial = mat;
        }

        public void SetTint(Color c)
        {
            _mpb ??= new MaterialPropertyBlock();
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(_mpb);
                _mpb.SetColor(TintId, c);
                renderers[i].SetPropertyBlock(_mpb);
            }
        }
    }

    /// <summary>Библиотека префабов блоков (заполняется генератором или вручную).</summary>
    [CreateAssetMenu(menuName = "Subsistence/Build Prefab Library")]
    public class BuildPrefabLibrary : ScriptableObject
    {
        [Serializable] public struct Entry { public BuildPieceType piece; public BuildTier tier; public GameObject prefab; }
        public Entry[] entries = Array.Empty<Entry>();

        public GameObject Get(BuildPieceType piece, BuildTier tier)
        {
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].piece == piece && entries[i].tier == tier && entries[i].prefab != null) return entries[i].prefab;
            // фоллбэк: любой тир этого элемента
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].piece == piece && entries[i].prefab != null) return entries[i].prefab;
            return null;
        }
    }

    /// <summary>Базовый рантайм мира: тикает обвалы и гниение (ставится на объект "World").</summary>
    public class BuildingRuntime : MonoBehaviour
    {
        float _decayTimer;
        void Update()
        {
            CollapseScheduler.Tick();
            if (!NetworkBridge.IsServer) return;
            _decayTimer += Time.deltaTime;
            if (_decayTimer >= Balance.DecayTickSeconds)
            {
                _decayTimer = 0f;
            }
        }
    }
}
