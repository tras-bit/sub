// ============================================================================
//  SUBSISTENCE — Building/Doors.cs
//  16_doors: двери и замки как в Rust.
//   • дверь — створка на петле: открывается/закрывается (E), скрип и стук по дереву;
//   • код-лок: 4 цифры, вход через терминал-панель, авторизованные игроки;
//   • ключевой замок: у замка случайный id, парный ключ лежит в инвентаре (item metaTag);
//   • замок ставится на дверь: взять замок в руки и «поставить» на створку;
//   • чужая дверь не открывается, взламывается уроном (C4/сачель — через IDestructible).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Net;

namespace Subsistence.Building
{
    /// <summary>Замок на двери: кодовый (клавиатура) или ключевой (парный ключ в руках).</summary>
    public class DoorLock : MonoBehaviour
    {
        public enum Kind : byte { Code = 0, Key = 1 }

        public Kind kind = Kind.Code;
        public string code = "1337";          // кодовый замок: 4 цифры (Rust: любой код)
        public int keyId = 0;                 // ключевой: id, который должен быть у ключа (ItemStack.metaTag)
        public bool locked = true;
        public readonly HashSet<ulong> granted = new HashSet<ulong>();

        public bool IsUnlockedFor(ulong player) => !locked || granted.Contains(player);

        public string Label => kind == Kind.Code ? "кодовый замок" : $"ключевой замок #{keyId}";

        /// <summary>Даёт доступ и снимает блокировку (код введён или ключ подошёл).</summary>
        public void Grant(ulong player)
        {
            granted.Add(player);
            Subsistence.UI.HudRuntime.ShowToast($"Доступ выдан ({Label})");
        }
    }

    /// <summary>
    /// Дверь: створка висит на петле и поворачивается (модель DD_door_*),
    /// коллайдер-прокси едет вместе со створкой, поэтому открытая дверь не блокирует проход.
    /// </summary>
    public class DoorDeployable : BuildDeployable
    {
        public float leafWidth = 1.0f;
        public float leafHeight = 2.05f;
        public float leafThickness = 0.16f;
        public float openAngle = 94f;
        public float swingSpeed = 260f;

        public Transform leaf;                                  // первая створка (на неё вешается замок)
        readonly List<Transform> _leaves = new List<Transform>(2);
        readonly List<int> _leafDir = new List<int>(2);         // +1 — створка смотрит вправо, −1 — влево

        public DoorLock doorLock;

        // Реестр дверей: дверь не двигается, поэтому клиент опознаёт её по позиции (см. DeployNet).
        static readonly List<DoorDeployable> _all = new List<DoorDeployable>(32);
        public static IReadOnlyList<DoorDeployable> All => _all;

        void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        void OnDisable() { _all.Remove(this); }

        /// <summary>Ближайшая дверь к точке (радиус по умолчанию — 1.5 м).</summary>
        public static DoorDeployable FindAt(Vector3 pos, float radius = 1.5f)
        {
            DoorDeployable best = null;
            float bestSqr = radius * radius;
            for (int i = 0; i < _all.Count; i++)
            {
                var d = _all[i];
                if (d == null) continue;
                float sqr = (d.transform.position - pos).sqrMagnitude;
                if (sqr <= bestSqr) { bestSqr = sqr; best = d; }
            }
            return best;
        }

        public bool IsOpen => _open;
        bool _open;
        float _angle;

        public override void Initialize(ulong owner, uint building)
        {
            base.Initialize(owner, building);
            itemId = string.IsNullOrEmpty(itemId) ? "door.hinged.wood" : itemId;
            requiresPrivilege = false;                       // двери должны открываться и «не своим» — под замком
            health = Mathf.Max(health, 200f);

            switch (itemId)
            {
                case "door.hinged.metal": health = 250f; leafThickness = 0.20f; break;   // Rust: 250 HP
                case "door.hinged.toptier": health = 800f; leafThickness = 0.26f; break; // Rust: 800 HP
                case "door.double.hinged.metal": health = 500f; leafWidth = 2.0f; leafThickness = 0.20f; break;
                default: health = 200f; leafThickness = 0.16f; break;                    // деревянная: 200 HP
            }

            BuildLeaf();
        }

        /// <summary>
        /// Створки: у двойной двери их две (открываются навстречу), у обычной — одна.
        /// Петля у края проёма, прокси-куб переезжает в створку, модель — на створке.
        /// </summary>
        void BuildLeaf()
        {
            int count = itemId == "door.double.hinged.metal" ? 2 : 1;
            float total = leafWidth;
            float w = total / count;

            var body = transform.Find("Body");
            GameObject bodyGo = body != null ? body.gameObject : null;

            for (int i = 0; i < count; i++)
            {
                bool left = i == 0;
                int dir = left ? 1 : -1;                       // куда от петли уходит полотно

                var hinge = new GameObject(count == 2 ? (left ? "LeafL" : "LeafR") : "Leaf");
                hinge.transform.SetParent(transform, false);
                hinge.transform.localPosition = new Vector3(left ? -total * 0.5f : total * 0.5f, 0f, 0f);

                // Коллайдер-прокси: первый кусок — исходный Body, второй создаём сами.
                GameObject proxy;
                if (left && bodyGo != null) proxy = bodyGo;
                else
                {
                    proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    proxy.name = "Body";
                    proxy.layer = Layers.Deployable;
                }
                proxy.transform.SetParent(hinge.transform, false);
                proxy.transform.localPosition = new Vector3(dir * w * 0.5f, leafHeight * 0.5f, 0f);
                proxy.transform.localScale = new Vector3(w, leafHeight, Mathf.Max(0.16f, leafThickness));

                // Модель DD_door_* нарисована по центру полотна → сдвигаем на пол-ширины от петли.
                var model = World.ModelLibrary.AttachFitted(World.ModelLibrary.ForDeployable(itemId),
                                                           hinge.transform, leafHeight, 0f, 0f, centerXZ: false);
                if (model != null)
                {
                    model.transform.localPosition += new Vector3(dir * w * 0.5f, 0f, 0f);
                    var r = proxy.GetComponent<MeshRenderer>();
                    if (r != null) r.enabled = false;          // куб → невидимый прокси, видно только модель
                }

                _leaves.Add(hinge.transform);
                _leafDir.Add(dir);
                if (left) leaf = hinge.transform;
            }
        }

        public override void OnUse(Subsistence.Player.PlayerController player)
        {
            ulong me = NetworkBridge.Host?.LocalPlayerId ?? 0UL;
            doorLock = GetComponentInChildren<DoorLock>();

            if (doorLock != null && doorLock.locked && !doorLock.IsUnlockedFor(me))
            {
                if (doorLock.kind == DoorLock.Kind.Code)
                {
                    Subsistence.UI.CodeLockUI.Open(this, doorLock);   // код проверяет сервер, не клиент
                    return;
                }
                DeployNet.RequestKey(this);                          // сервер сам посмотрит инвентарь
                return;
            }

            DeployNet.RequestToggle(this);                           // сервер применит и разошлёт всем
        }

        // ================== СЕРВЕР / КЛИЕНТ (16_doors + 30–31) ==================

        /// <summary>Серверная обработка «E»/кода/ключа после валидации дистанции в DeployNet.</summary>
        public void ServerUse(DeployNet.DoorAction action, string code, ulong actor,
                              Subsistence.Player.PlayerController player)
        {
            doorLock = GetComponentInChildren<DoorLock>();

            if (doorLock != null && doorLock.locked)
            {
                switch (action)
                {
                    case DeployNet.DoorAction.Code:
                        if (doorLock.kind != DoorLock.Kind.Code || string.IsNullOrEmpty(code) || code != doorLock.code)
                        {
                            DeployNet.BroadcastDeny(this, "НЕВЕРНЫЙ КОД");
                            return;
                        }
                        break;
                    case DeployNet.DoorAction.Key:
                        if (doorLock.kind != DoorLock.Kind.Key || !TryKey(player, doorLock))
                        {
                            DeployNet.BroadcastDeny(this, "НУЖЕН ПАРНЫЙ КЛЮЧ");
                            return;
                        }
                        break;
                    default:
                        DeployNet.BroadcastDeny(this, $"ЗАПЕРТО: {doorLock.Label}");
                        return;
                }

                doorLock.granted.Add(actor);
                DeployNet.BroadcastGrant(this, actor);      // доступ выдан именно этому игроку
            }

            ServerApply(!_open);
        }

        /// <summary>Сервер применил состояние: скрип двери слышат монстры, клиенты получают RPC.</summary>
        public void ServerApply(bool open)
        {
            _open = open;
            AI.NoiseSystem.Emit(transform.position, open ? 9f : 6f, AI.NoiseType.Door);
            DeployNet.BroadcastState(this);
        }

        /// <summary>Клиент получил состояние от сервера — применяем молча, без шума.</summary>
        public void ApplyRemote(bool open) => _open = open;

        /// <summary>Клиент получил информацию о замке: рисуем модель, но без чужого keyId и без кода.</summary>
        public void ApplyRemoteLock(byte kind, bool locked)
        {
            if (doorLock != null) { doorLock.locked = locked; return; }

            var go = new GameObject("Lock");
            go.transform.SetParent(leaf != null ? leaf : transform, false);
            go.transform.localPosition = new Vector3(leafWidth * 0.72f, 1.05f, -leafThickness * 0.6f);
            go.layer = Layers.Deployable;

            var l = go.AddComponent<DoorLock>();
            l.kind = (DoorLock.Kind)kind;
            l.locked = locked;
            l.keyId = 0;                 // чужой id ключа клиенту знать незачем
            l.code = null;               // кода у клиента нет вообще
            World.ModelLibrary.AttachFitted(
                World.ModelLibrary.ForDeployable(kind == (byte)DoorLock.Kind.Key ? "lock.key" : "lock.code"),
                go.transform, 0.34f, 180f);
            doorLock = l;
        }

        /// <summary>Парный ключ у игрока? (ключ — item «lock.key» с metaTag = keyId замка)</summary>
        public static bool TryKey(Subsistence.Player.PlayerController player, DoorLock doorLock)
        {
            if (player == null || player.inventory == null || doorLock == null) return false;
            var items = player.inventory.AllItems();
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it != null && it.id == "lock.key" && it.metaTag == doorLock.keyId) return true;
            }
            return false;
        }

        void Update()
        {
            if (_leaves.Count == 0) return;
            float target = _open ? openAngle : 0f;
            if (Mathf.Abs(_angle - target) < 0.01f) return;
            _angle = Mathf.MoveTowards(_angle, target, swingSpeed * Time.deltaTime);

            for (int i = 0; i < _leaves.Count; i++)
            {
                var l = _leaves[i];
                if (l == null) continue;
                // Петля слева → полотно уходит наружу с минусом; правая створка двойной двери — зеркально.
                l.localRotation = Quaternion.Euler(0f, -_angle * _leafDir[i], 0f);
            }
        }

        /// <summary>Дверь ломается — замок падает вместе с ней (лут урона выдают двери/дому).</summary>
        public override void OnBlockDestroyed()
        {
            if (doorLock != null) Destroy(doorLock);
        }

        /// <summary>Вешает замок на дверь (вызывает DeployPlacer, когда игрок ставит замок).</summary>
        public DoorLock AttachLock(string lockItemId, ulong owner)
        {
            if (doorLock != null) return doorLock;
            var go = new GameObject("Lock");
            go.transform.SetParent(leaf != null ? leaf : transform, false);
            go.transform.localPosition = new Vector3(leafWidth * 0.72f, 1.05f, -leafThickness * 0.6f);
            go.layer = Layers.Deployable;

            var l = go.AddComponent<DoorLock>();
            if (lockItemId == "lock.key")
            {
                l.kind = DoorLock.Kind.Key;
                l.keyId = Random.Range(1000, 9999);
            }
            else
            {
                l.kind = DoorLock.Kind.Code;
                l.code = Random.Range(1000, 9999).ToString();      // код можно сменить в терминале
            }
            l.granted.Add(owner);

            // модель замка (DD_lock_key / DD_lock_code) на створке
            World.ModelLibrary.AttachFitted(World.ModelLibrary.ForDeployable(lockItemId),
                                            go.transform, 0.34f, 180f);
            doorLock = l;

            // 30–31: остальные клиенты узнают о замке (без keyId и без кода)
            DeployNet.BroadcastLock(this);
            return l;
        }
    }
}
