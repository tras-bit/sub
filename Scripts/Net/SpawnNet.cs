// ============================================================================
//  SUBSISTENCE — Net/SpawnNet.cs
//  30–31: репликация спавна объектов мира — деплоев и элементов стройки.
//   • сервер единственный, кто создаёт объект: клиент шлёт команду
//     (PlaceDeployable / PlaceBuilding), сервер проверяет и рассылает object.spawn;
//   • у каждого объекта сетевой id (uint), клиенты отсекают дубликаты по нему;
//   • деспавн уезжает вместе с объектом (NetSpawned.OnDestroy → object.despawn);
//   • новому игроку высылается текущий мир (сохранённые payload'ы).
//  Формат object.spawn: kind(byte) + netId(uint) + данные по виду объекта.
//  Внимание: инвентарь игрока пока не репликуется (спайк 30) — поэтому
//  серверная проверка «есть ли предмет» выключена флагом RequireServerInventory.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Building;
using Subsistence.Core;

namespace Subsistence.Net
{
    /// <summary>Маркер сетевого объекта: сам сообщает о своём удалении.</summary>
    public class NetSpawned : MonoBehaviour
    {
        public uint netId;
        public string itemId;
        public ulong ownerId;
        public float yaw;

        void OnDestroy()
        {
            if (!SpawnNet.Ready) return;
            if (NetworkBridge.IsServer) SpawnNet.ServerDestroyed(netId);
            else SpawnNet.Forget(netId);
        }
    }

    public static class SpawnNet
    {
        /// <summary>
        /// Проверять предмет на сервере. Включено: у сервера есть зеркало инвентаря игрока
        /// (Net/InventoryNet.cs) — и для своих, и для чужих предметов.
        /// </summary>
        public static bool RequireServerInventory = true;

        // вид команды (PlaceDeployable)
        const byte CmdDeployable = 0;
        const byte CmdLock = 1;
        // вид объекта в object.spawn
        const byte SpawnDeployable = 0;
        const byte SpawnBlock = 1;
        const byte SpawnLoose = 2;          // предмет на полу
        const byte SpawnBag = 3;            // мешок с лутом (смерть игрока)

        const float DropReach = 3.5f;       // насколько далеко можно выбросить предмет
        const float PickupReach = 4.5f;     // и с какого расстояния подобрать
        const float BagLifetime = 900f;     // мешок с лутом лежит 15 мин (как в Rust)

        static readonly Dictionary<uint, GameObject> _byId = new Dictionary<uint, GameObject>(256);
        static readonly Dictionary<uint, byte[]> _serverPayloads = new Dictionary<uint, byte[]>(256);
        static uint _next = 1;

        public static bool Ready { get; private set; }
        public static int Count => _byId.Count;

        // ================== ПРЕДСКАЗАНИЕ ПОСТАНОВКИ (клиент) ==================
        //  Клиент ставит объект СРАЗУ и не ждёт ответа сервера — «поставил, значит стоит».
        //  Сервер подтверждает спавном, и клиент «усыновляет» своё предсказание (по предмету и
        //  позиции), а не создаёт второй объект. Если сервер отказал — предсказание убирается,
        //  а предмет (или стоимость постройки) возвращается в инвентарь.

        /// <summary>Сколько ждать ответ сервера, прежде чем откатить предсказание.</summary>
        public static float PredictionTimeout = 2.5f;

        class Pending
        {
            public GameObject go;
            public string itemId;       // деплой: что вернуть при откате
            public bool isBlock;        // стройка: возвращаем стоимость
            public BuildPieceType piece;
            public BuildTier tier;
            public Vector3 pos;
            public float time;
        }

        static readonly List<Pending> _pending = new List<Pending>(32);

        public static int PendingCount => _pending.Count;

        public static void Init()
        {
            if (Ready) return;
            Ready = true;

            var ticker = new GameObject("~SpawnNet");
            UnityEngine.Object.DontDestroyOnLoad(ticker);
            ticker.AddComponent<SpawnNetTick>();       // откат предсказаний по таймауту

            Hook();
            NetworkBridge.HostChanged += Hook;
            Debug.Log("[Net] SpawnNet: репликация спавна (деплои + стройка) активна");
        }

        static void Hook()
        {
            var host = NetworkBridge.Host;
            if (host == null) return;

            // В Mirror команды роутит MirrorPlayer (см. DeployNet.Hook) — здесь только офлайн.
            host.OnCommand -= OnCommand;
            if (host is LocalHost) host.OnCommand += OnCommand;

            host.OnRpc -= OnRpc;
            host.OnRpc += OnRpc;
            host.OnPlayerConnected -= OnPlayerConnected;
            host.OnPlayerConnected += OnPlayerConnected;
        }

        // ================== КЛИЕНТ → СЕРВЕР ==================

        /// <summary>Клиент: поставить деплой сразу (сервер подтвердит netId — см. ApplySpawn).</summary>
        public static GameObject PredictDeployable(string itemId, Vector3 pos, float yaw)
        {
            if (!NetworkBridge.IsClient) return null;
            var go = DeployableFactory.Create(itemId, pos, yaw, NetworkBridge.Host?.LocalPlayerId ?? 0UL, out _);
            if (go == null) return null;
            _pending.Add(new Pending { go = go, itemId = itemId, pos = pos, time = Time.time });
            return go;
        }

        /// <summary>
        /// Клиент: показать блок сразу. Настоящий блок создаст сервер (учёт прочности/стабильности —
        /// только на нём), поэтому предсказание здесь визуальное: без физики и без BuildBlock.
        /// </summary>
        public static GameObject PredictBlock(BuildPieceType piece, BuildTier tier, int rot, Vector3 point)
        {
            if (!NetworkBridge.IsClient) return null;
            var lib = Subsistence.Runtime.BuildPrefabLibraryRuntime.Shared;
            var prefab = lib != null ? lib.Get(piece, tier) : null;
            if (prefab == null) return null;

            var go = UnityEngine.Object.Instantiate(prefab, point, Quaternion.Euler(0, rot * 90f, 0));
            foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.Destroy(c);
            foreach (var b in go.GetComponentsInChildren<BuildBlock>()) UnityEngine.Object.Destroy(b);
            _pending.Add(new Pending { go = go, isBlock = true, piece = piece, tier = tier, pos = point, time = Time.time });
            return go;
        }

        /// <summary>Найти предсказание под подтверждение сервера (тот же предмет и та же точка).</summary>
        static Pending TakePending(string itemId, Vector3 pos, bool block)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                var p = _pending[i];
                if (p.isBlock != block) continue;
                if (Vector3.Distance(p.pos, pos) > 0.3f) continue;   // сервер шлёт ту же точку (квант 0.01 м)
                if (!block && p.itemId != itemId) continue;
                _pending.RemoveAt(i);
                return p;
            }
            return null;
        }

        static void DropPending(Pending p)
        {
            if (p == null) return;
            if (p.go != null) UnityEngine.Object.Destroy(p.go);
            Refund(p);
        }

        /// <summary>Вернуть в инвентарь то, что клиент потратил под откатившееся предсказание.</summary>
        static void Refund(Pending p)
        {
            var inv = Subsistence.Runtime.RuntimeBootstrap.Instance != null && Subsistence.Runtime.RuntimeBootstrap.Instance.Player != null
                    ? Subsistence.Runtime.RuntimeBootstrap.Instance.Player.inventory
                    : null;
            if (inv == null) return;

            if (p.isBlock)
            {
                var cost = BuildCosts.PlacementCost(p.piece, p.tier);
                for (int i = 0; i < cost.Count; i++) inv.TryAdd(cost[i]);
            }
            else if (!string.IsNullOrEmpty(p.itemId))
            {
                inv.TryAdd(new Subsistence.Core.ItemStack(p.itemId, 1));
            }
        }

        /// <summary>Откат по таймауту: сервер молчит — считаем, что не принял (см. SpawnNetTick).</summary>
        internal static void Tick()
        {
            if (_pending.Count == 0) return;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                if (Time.time - p.time < PredictionTimeout) continue;
                _pending.RemoveAt(i);
                DropPending(p);
                Subsistence.UI.HudRuntime.ShowToast("Сервер не подтвердил постановку — предмет возвращён", 2.5f);
            }
        }

        /// <summary>Клиент просит поставить деплой (сервер создаст объект у всех).</summary>
        public static void RequestPlace(string itemId, Vector3 pos, float yaw)
        {
            var w = BufferWriter.Rent();
            w.WriteByte(CmdDeployable);
            w.WriteString(itemId);
            w.WritePosition(pos, -100f, 1000f);
            w.WriteUShort(AngleToUShort(yaw));
            NetworkBridge.Command(ClientCommand.PlaceDeployable, w.ToArray());
            BufferWriter.Return(w);
        }

        /// <summary>Клиент вешает замок на дверь (сервер выдаст ключ и разошлёт замок).</summary>
        public static void RequestAttachLock(string lockItemId, Vector3 doorPos)
        {
            var w = BufferWriter.Rent();
            w.WriteByte(CmdLock);
            w.WriteString(lockItemId);
            w.WritePosition(doorPos, -100f, 1000f);
            NetworkBridge.Command(ClientCommand.PlaceDeployable, w.ToArray());
            BufferWriter.Return(w);
        }

        // ================== СЕРВЕР: команды ==================

        public static void HandleServerCommand(ulong from, ClientCommand cmd, byte[] payload)
        {
            if (!NetworkBridge.IsServer) return;

            if (cmd == ClientCommand.PlaceDeployable)
            {
                var r = new BufferReader(payload);
                byte kind = r.ReadByte();
                string itemId = r.ReadString();

                if (kind == CmdLock) ServerAttachLock(from, itemId, r.ReadPosition(-100f, 1000f));
                else
                {
                    var pos = r.ReadPosition(-100f, 1000f);
                    ServerPlace(from, itemId, pos, UShortToAngle(r.ReadUShort()));
                }
            }
            else if (cmd == ClientCommand.PlaceBuilding)
            {
                var r = new BufferReader(payload);
                var piece = (BuildPieceType)r.ReadByte();
                var tier = (BuildTier)r.ReadByte();
                int rot = r.ReadByte();
                var point = r.ReadPosition(-100f, 1000f);
                ServerPlaceBlock(from, piece, tier, rot, point);
            }
            else if (cmd == ClientCommand.LootDrop) ServerDrop(from, payload);
            else if (cmd == ClientCommand.LootPickup) ServerPickup(from, payload);
        }

        /// <summary>Игрок выбросил предмет: он уже списал его у себя — уменьшаем зеркало молча.</summary>
        static void ServerDrop(ulong from, byte[] payload)
        {
            var r = new BufferReader(payload);
            string itemId = r.ReadString();
            int amount = r.ReadUShort();
            var pos = r.ReadPosition(-100f, 1000f);   // ±500 м — с запасом на размер уровня
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return;

            var player = DeployNet.PlayerOf(from);
            if (player != null && Vector3.Distance(player.transform.position, pos) > DropReach) return;

            // notifyClient по умолчанию false: клиент списал предмет оптимистично, второй раз нельзя.
            if (RequireServerInventory && !InventoryNet.ServerConsume(from, itemId, amount))
            {
                DeployNet.Notify(from, "Предмета нет в инвентаре");
                return;
            }
            ServerSpawnLooseItem(itemId, amount, pos);
        }

        /// <summary>Игрок подобрал предмет: проверяем дистанцию, место — и отдаём (или отказываем).</summary>
        static void ServerPickup(ulong from, byte[] payload)
        {
            var r = new BufferReader(payload);
            uint netId = r.ReadUInt();
            if (netId == 0u || !_byId.TryGetValue(netId, out var go) || go == null) return;

            var pickup = go.GetComponent<Subsistence.World.ItemPickup>();
            if (pickup == null) return;

            var player = DeployNet.PlayerOf(from);
            if (player != null && Vector3.Distance(player.transform.position, go.transform.position) > PickupReach) return;

            // Берём целиком или никак: иначе пришлось бы возвращать «половину» обратно.
            var stack = new Subsistence.Core.ItemStack(pickup.itemId, pickup.amount);
            if (!InventoryNet.ServerHasRoomFor(from, stack))
            {
                DeployNet.Notify(from, "Инвентарь полон");
                return;
            }
            InventoryNet.ServerAdd(from, stack);          // чужому уедет inv.give
            Object.Destroy(go);                           // NetSpawned сам разошлёт object.despawn
        }

        static void ServerPlace(ulong from, string itemId, Vector3 pos, float yaw)
        {
            var player = DeployNet.PlayerOf(from);
            if (player == null || string.IsNullOrEmpty(itemId)) return;
            if (!DeployableFactory.IsDeployable(itemId)) { RejectPlace(from, itemId, pos); return; }

            // Античит: ставим только рядом с собой.
            if (Vector3.Distance(player.transform.position, pos) > 7f) { RejectPlace(from, itemId, pos); return; }

            // Чужая территория (Tool Cupboard) — нельзя.
            if (!BuildingPrivilege.CanBuild(pos, from, out string reason))
            {
                DeployNet.Notify(from, reason);
                RejectPlace(from, itemId, pos);
                return;
            }

            // Место свободно? Та же проверка, что у призрака на клиенте.
            var spec = DeployableFactory.SpecOf(itemId);
            var half = spec.size * 0.5f;
            var hits = Physics.OverlapBox(pos + Vector3.up * half.y,
                                          new Vector3(half.x * 0.92f, half.y * 0.92f, half.z * 0.92f),
                                          Quaternion.Euler(0f, yaw, 0f),
                                          Layers.Mask(Layers.LevelGeometry, Layers.Buildable, Layers.Deployable),
                                          QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0) { RejectPlace(from, itemId, pos); return; }

            if (RequireServerInventory && InventoryNet.ServerCount(from, itemId) <= 0)
            {
                DeployNet.Notify(from, "Предмета нет в инвентаре");
                RejectPlace(from, itemId, pos);
                return;
            }

            var go = ServerSpawnDeployable(itemId, pos, yaw, from);
            if (go == null) return;
            if (RequireServerInventory) InventoryNet.ServerConsume(from, itemId, 1);
            Debug.Log($"[Net] {itemId} поставлен игроком {from} → netId={go.GetComponent<NetSpawned>()?.netId}");
        }

        static void ServerPlaceBlock(ulong from, BuildPieceType piece, BuildTier tier, int rot, Vector3 point)
        {
            var player = DeployNet.PlayerOf(from);
            if (player == null) return;

            // 1) дистанция (как MaxPlaceDistance у BuildController + запас на пинг)
            if (Vector3.Distance(player.transform.position, point) > BuildController.MaxPlaceDistance + 1.5f)
            {
                RejectBlock(from, piece, tier, point);
                return;
            }

            // 2) ресурсы
            var cost = BuildCosts.PlacementCost(piece, tier);
            if (!InventoryNet.ServerCanAfford(from, cost))          // зеркало инвентаря игрока
            {
                DeployNet.Notify(from, "Не хватает ресурсов");
                RejectBlock(from, piece, tier, point);
                return;
            }

            // 3) привилегия (чужой TC) + 4) занятое место — те же проверки, что в BuildController
            if (!BuildingPrivilege.CanBuild(point, from, out string reason))
            {
                DeployNet.Notify(from, reason);
                RejectBlock(from, piece, tier, point);
                return;
            }
            if (Physics.CheckBox(point + Vector3.up * 1.5f, Vector3.one * 1.4f, Quaternion.identity,
                                 Layers.Mask(Layers.Buildable), QueryTriggerInteraction.Collide))
            {
                RejectBlock(from, piece, tier, point);   // занято (предсказание клиента тоже уберём)
                return;
            }

            if (!InventoryNet.ServerPay(from, cost)) return;         // клиент уже заплатил сам, тут только зеркало

            uint buildingId = BuildingRegistry.NewBuildingId();
            var fromPlayer = DeployNet.PlayerOf(from);
            Vector3 placerPos = fromPlayer != null ? fromPlayer.transform.position : default(Vector3);
            if (ServerSpawnBlock(piece, tier, rot, point, from, buildingId, placerPos) == null)
            {
                // Префабов нет (меню «Subsistence → 7» не собрано) — сервер поставить не смог:
                // возвращаем стоимость и просим клиента убрать предсказание.
                for (int i = 0; i < cost.Count; i++) InventoryNet.ServerAdd(from, cost[i]);
                RejectBlock(from, piece, tier, point);
            }
        }

        static void ServerAttachLock(ulong from, string lockItemId, Vector3 doorPos)
        {
            var player = DeployNet.PlayerOf(from);
            if (player == null || (lockItemId != "lock.key" && lockItemId != "lock.code")) return;

            var door = DoorDeployable.FindAt(doorPos, 1.5f);
            if (door == null) { DeployNet.Notify(from, "Замок ставится на дверь"); RejectPlace(from, lockItemId, doorPos); return; }
            if (door.doorLock != null)
            {
                DeployNet.Notify(from, $"На двери уже есть {door.doorLock.Label}");
                RejectPlace(from, lockItemId, doorPos);
                return;
            }
            if (Vector3.Distance(player.transform.position, door.transform.position) > 9f) { RejectPlace(from, lockItemId, doorPos); return; }

            if (RequireServerInventory && InventoryNet.ServerCount(from, lockItemId) <= 0) { RejectPlace(from, lockItemId, doorPos); return; }

            var l = door.AttachLock(lockItemId, from);            // внутри уже BroadcastLock
            if (l == null) return;
            if (RequireServerInventory) InventoryNet.ServerConsume(from, lockItemId, 1);

            if (l.kind == DoorLock.Kind.Key)
            {
                var key = new Subsistence.Core.ItemStack("lock.key", 1);
                key.metaTag = l.keyId;
                // ключ уезжает владельцу адресно (inv.give) — раньше в Mirror он терялся
                InventoryNet.ServerAdd(from, key);
            }
            Debug.Log($"[Net] {l.Label} на двери {door.itemId} (игрок {from})");
        }

        // ================== СЕРВЕР: спавн ==================

        /// <summary>Создать деплой на сервере и разослать всем (в т.ч. тому, кто просил).</summary>
        public static GameObject ServerSpawnDeployable(string itemId, Vector3 pos, float yaw, ulong owner)
        {
            var go = DeployableFactory.Create(itemId, pos, yaw, owner, out _);
            if (go == null) return null;

            uint id = Register(go, itemId, owner, yaw);
            var w = BufferWriter.Rent();
            w.WriteByte(SpawnDeployable);
            w.WriteUInt(id);
            w.WriteString(itemId);
            w.WritePosition(pos, -100f, 1000f);
            w.WriteUShort(AngleToUShort(yaw));
            w.WriteULong(owner);
            StoreAndBroadcast(id, w);
            return go;
        }

        /// <summary>Создать элемент стройки на сервере и разослать всем.</summary>
        public static BuildBlock ServerSpawnBlock(BuildPieceType piece, BuildTier tier, int rot, Vector3 point,
                                                 ulong owner, uint buildingId, Vector3 placerPos = default(Vector3))
        {
            var library = Subsistence.Runtime.BuildPrefabLibraryRuntime.Shared;   // одна на процесс
            if (library == null) return null;

            var block = BuildController.SpawnBlock(library, piece, tier, rot, point, owner, buildingId);
            if (block == null) return null;
            block.isFoundation = piece == BuildPieceType.Foundation || piece == BuildPieceType.FoundationTriangle;

            uint id = _next++;
            var marker = block.gameObject.AddComponent<NetSpawned>();
            marker.netId = id; marker.itemId = null; marker.ownerId = owner; marker.yaw = rot * 90f;
            _byId[id] = block.gameObject;

            var w = BufferWriter.Rent();
            w.WriteByte(SpawnBlock);
            w.WriteUInt(id);
            w.WriteByte((byte)piece);
            w.WriteByte((byte)tier);
            w.WriteByte((byte)rot);
            w.WritePosition(point, -100f, 1000f);
            w.WriteULong(owner);
            w.WriteUInt(buildingId);
            StoreAndBroadcast(id, w);
            return block;
        }

        /// <summary>Предмет на полу: создаём на сервере и рассылаем всем (в т.ч. тому, кто выбросил).</summary>
        public static GameObject ServerSpawnLooseItem(string itemId, int amount, Vector3 pos)
        {
            var pickup = Subsistence.World.WorldDeposits.BuildLooseItem(itemId, amount, pos);
            if (pickup == null) return null;

            uint id = Register(pickup.gameObject, itemId, 0UL, 0f);
            var w = BufferWriter.Rent(80);
            w.WriteByte(SpawnLoose);
            w.WriteUInt(id);
            w.WriteString(itemId);
            w.WriteUShort((ushort)Mathf.Clamp(amount, 1, 65535));
            w.WritePosition(pos, -100f, 1000f);
            StoreAndBroadcast(id, w);
            return pickup.gameObject;
        }

        /// <summary>
        /// Мешок с лутом: сервер создаёт его и рассылает. Содержимое кладёт вызывающий —
        /// клиенты получат его при открытии (адрес у мешка сетевой: kind 1 / netId).
        /// </summary>
        public static Subsistence.World.LootContainer ServerSpawnBag(Vector3 pos, float yaw, ulong owner)
        {
            var bag = Subsistence.World.WorldDeposits.BuildCorpseBag(pos, yaw, owner);
            if (bag == null) return null;

            uint id = Register(bag.gameObject, null, owner, yaw);
            Object.Destroy(bag.gameObject, BagLifetime);     // труп лежит 15 мин (despawn уедет сам)

            var w = BufferWriter.Rent(64);
            w.WriteByte(SpawnBag);
            w.WriteUInt(id);
            w.WritePosition(pos, -100f, 1000f);
            w.WriteUShort(AngleToUShort(yaw));
            w.WriteULong(owner);
            StoreAndBroadcast(id, w);
            return bag;
        }

        /// <summary>Клиент: подобрать предмет с пола (сервер проверит дистанцию и место).</summary>
        public static void RequestPickup(uint netId)
        {
            if (netId == 0u) return;
            var w = BufferWriter.Rent(16);
            w.WriteUInt(netId);
            NetworkBridge.Command(ClientCommand.LootPickup, w.ToArray());
            BufferWriter.Return(w);
        }

        static uint Register(GameObject go, string itemId, ulong owner, float yaw)
        {
            uint id = _next++;
            var marker = go.AddComponent<NetSpawned>();
            marker.netId = id; marker.itemId = itemId; marker.ownerId = owner; marker.yaw = yaw;
            _byId[id] = go;
            return id;
        }

        static void StoreAndBroadcast(uint id, BufferWriter w)
        {
            var data = w.ToArray();
            BufferWriter.Return(w);
            _serverPayloads[id] = data;
            NetworkBridge.Host?.SendRpc(NetId.None, "object.spawn", data);
        }

        public static void ServerDestroyed(uint netId)
        {
            if (!_byId.ContainsKey(netId)) return;
            _byId.Remove(netId);
            _serverPayloads.Remove(netId);

            var w = BufferWriter.Rent();
            w.WriteUInt(netId);
            NetworkBridge.Host?.SendRpc(NetId.None, "object.despawn", w.ToArray());
            BufferWriter.Return(w);
        }

        // ================== ОТКАЗ (сервер → клиент) ==================

        /// <summary>Сервер не поставил деплой: клиент уберёт предсказание и вернёт предмет.</summary>
        public static void RejectPlace(ulong to, string itemId, Vector3 pos)
        {
            var w = BufferWriter.Rent(64);
            w.WritePosition(pos, -100f, 1000f);
            w.WriteByte(0);                              // 0 = деплой/замок
            w.WriteString(itemId);
            PlayerRpc.Send(to, "place.reject", w.ToArray());
            BufferWriter.Return(w);
        }

        /// <summary>Сервер не поставил блок: клиент вернёт стоимость постройки.</summary>
        public static void RejectBlock(ulong to, BuildPieceType piece, BuildTier tier, Vector3 point)
        {
            var w = BufferWriter.Rent(32);
            w.WritePosition(point, -100f, 1000f);
            w.WriteByte(1);                              // 1 = блок
            w.WriteByte((byte)piece);
            w.WriteByte((byte)tier);
            PlayerRpc.Send(to, "place.reject", w.ToArray());
            BufferWriter.Return(w);
        }

        static void ApplyReject(byte[] payload)
        {
            var r = new BufferReader(payload);
            var pos = r.ReadPosition(-100f, 1000f);
            bool block = r.ReadByte() == 1;
            string itemId = block ? null : r.ReadString();
            BuildPieceType piece = block ? (BuildPieceType)r.ReadByte() : BuildPieceType.Foundation;
            BuildTier tier = block ? (BuildTier)r.ReadByte() : BuildTier.Twig;

            var p = TakePending(itemId, pos, block);
            if (p != null)
            {
                DropPending(p);
                Subsistence.UI.HudRuntime.ShowToast("Постановка отклонена сервером — предмет возвращён", 2.5f);
                return;
            }

            // Предсказания не было (замок/ключ, например) — но клиент уже списал предмет: возвращаем.
            if (!block && !string.IsNullOrEmpty(itemId))
            {
                var inv = Subsistence.Runtime.RuntimeBootstrap.Instance != null && Subsistence.Runtime.RuntimeBootstrap.Instance.Player != null
                        ? Subsistence.Runtime.RuntimeBootstrap.Instance.Player.inventory : null;
                inv?.TryAdd(new Subsistence.Core.ItemStack(itemId, 1));
            }
            else if (block)
            {
                Refund(new Pending { isBlock = true, piece = piece, tier = tier });
            }
        }

        /// <summary>Объект по сетевому id — есть и на сервере, и на клиентах (после object.spawn).</summary>
        public static GameObject ById(uint netId)
            => _byId.TryGetValue(netId, out var go) ? go : null;

        // ================== ПРИЁМ ==================

        static void OnCommand(ulong from, ClientCommand cmd, byte[] payload)
            => HandleServerCommand(from, cmd, payload);

        static void OnPlayerConnected(ulong player)
        {
            if (!NetworkBridge.IsServer) return;
            // Новому игроку — текущий мир. Адресно (раньше уходило всем сразу — лишний трафик).
            foreach (var kv in _serverPayloads)
                PlayerRpc.Send(player, "object.spawn", kv.Value);
            Debug.Log($"[Net] игроку {player} отправлено {_serverPayloads.Count} объектов мира");
        }

        static void OnRpc(NetId target, string method, byte[] payload)
        {
            if (method == "object.spawn") ApplySpawn(payload);
            else if (method == "object.despawn") ApplyDespawn(payload);
            else if (method == "place.reject") ApplyReject(payload);
        }

        static void ApplySpawn(byte[] payload)
        {
            var r = new BufferReader(payload);
            byte kind = r.ReadByte();
            uint id = r.ReadUInt();
            if (id == 0 || _byId.ContainsKey(id)) return;              // дубликат — уже создан

            if (kind == SpawnBlock)
            {
                var piece = (BuildPieceType)r.ReadByte();
                var tier = (BuildTier)r.ReadByte();
                int rot = r.ReadByte();
                var point = r.ReadPosition(-100f, 1000f);
                ulong bOwner = r.ReadULong();
                uint buildingId = r.ReadUInt();

                var predicted = TakePending(null, point, true);   // свою визуальную заглушку убираем
                if (predicted != null && predicted.go != null) UnityEngine.Object.Destroy(predicted.go);

                var library = LocalLibrary();
                if (library == null) return;
                var block = BuildController.SpawnBlock(library, piece, tier, rot, point, bOwner, buildingId);
                if (block == null) return;
                block.isFoundation = piece == BuildPieceType.Foundation || piece == BuildPieceType.FoundationTriangle;
                var m = block.gameObject.AddComponent<NetSpawned>();
                m.netId = id; m.ownerId = bOwner; m.yaw = rot * 90f;
                _byId[id] = block.gameObject;
                return;
            }

            if (kind == SpawnLoose)
            {
                string looseId = r.ReadString();
                int amount = r.ReadUShort();
                var loosePos = r.ReadPosition(-100f, 1000f);
                var pickup = Subsistence.World.WorldDeposits.BuildLooseItem(looseId, amount, loosePos);
                if (pickup == null) return;
                var lm = pickup.gameObject.AddComponent<NetSpawned>();
                lm.netId = id; lm.itemId = looseId;
                _byId[id] = pickup.gameObject;
                return;
            }

            if (kind == SpawnBag)
            {
                var bagPos = r.ReadPosition(-100f, 1000f);
                float bagYaw = UShortToAngle(r.ReadUShort());
                ulong bagOwner = r.ReadULong();
                var bag = Subsistence.World.WorldDeposits.BuildCorpseBag(bagPos, bagYaw, bagOwner);
                if (bag == null) return;
                var bm = bag.gameObject.AddComponent<NetSpawned>();
                bm.netId = id; bm.ownerId = bagOwner; bm.yaw = bagYaw;
                _byId[id] = bag.gameObject;
                return;
            }

            // обычный деплой
            string itemId = r.ReadString();
            var pos = r.ReadPosition(-100f, 1000f);
            float yaw = UShortToAngle(r.ReadUShort());
            ulong owner = r.ReadULong();

            // Мы это уже поставили сами? Тогда просто усыновляем свой объект (и не мигаем моделью).
            var mine = TakePending(itemId, pos, false);
            if (mine != null && mine.go != null)
            {
                var mm = mine.go.AddComponent<NetSpawned>();
                mm.netId = id; mm.itemId = itemId; mm.ownerId = owner; mm.yaw = yaw;
                _byId[id] = mine.go;
                return;
            }

            var go = DeployableFactory.Create(itemId, pos, yaw, owner, out _);
            if (go == null) return;
            var marker = go.AddComponent<NetSpawned>();
            marker.netId = id; marker.itemId = itemId; marker.ownerId = owner; marker.yaw = yaw;
            _byId[id] = go;
        }

        static void ApplyDespawn(byte[] payload)
        {
            var r = new BufferReader(payload);
            uint id = r.ReadUInt();
            if (!_byId.TryGetValue(id, out var go)) return;
            _byId.Remove(id);
            if (go != null) Object.Destroy(go);
        }

        /// <summary>Библиотека блоков (сервер и клиент используют одну и ту же — см. Shared).</summary>
        static BuildPrefabLibrary LocalLibrary() => Subsistence.Runtime.BuildPrefabLibraryRuntime.Shared;

        public static void Forget(uint netId)
        {
            _byId.Remove(netId);
            _serverPayloads.Remove(netId);
        }

        static ushort AngleToUShort(float yaw) => (ushort)Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 360f * 65535f);
        static float UShortToAngle(ushort v) => v / 65535f * 360f;
    }

    /// <summary>Кадровый тик предсказаний (скрытый объект, создаёт SpawnNet.Init).</summary>
    public class SpawnNetTick : MonoBehaviour
    {
        void Update() => SpawnNet.Tick();
    }
}
