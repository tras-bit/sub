// ============================================================================
//  SUBSISTENCE — Net/InventoryNet.cs
//  30–31 (продолжение): сетевой путь инвентарей и лута.
//   • сервер держит ЗЕРКАЛО инвентаря каждого игрока: клиент шлёт дельту слотов
//     ~5 раз в секунду; по зеркалу сервер проверяет «есть ли предмет» и списывает
//     его (поэтому SpawnNet.RequireServerInventory теперь включён);
//   • выдача обратно — адресная: inv.give (ключ от замка, возврат, награда);
//   • лут-контейнеры авторитетны на сервере: клиент просит открыть/взять/положить,
//     сервер проверяет дистанцию и рассылает loot.contents / loot.slot;
//   • офлайн (LocalHost) работает без сети — тем же кодом, что и Mirror.
//  Важно: содержимое контейнеров генерируется заново на каждом пире (UnityEngine.Random),
//  поэтому клиент всегда перезаписывает своё локальное содержимое серверным при открытии.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;

namespace Subsistence.Net
{
    public static class InventoryNet
    {
        // ---------- настройки ----------
        public static bool Enabled = true;
        const float SyncInterval = 0.18f;      // как часто клиент шлёт дельту слотов
        const float FullSyncInterval = 10f;    // редкая полная сверка (лечим расхождения)
        const float LootReach = 4.5f;          // докуда дотягивается игрок в контейнер
        const float CorpseReach = 8f;          // насколько мешок с лутом может «отстать» от тела
        const float CorpseCooldown = 10f;      // второй мешок за это время — это уже не смерть, а чит
        const float LootFindRadius = 2.5f;     // поиск контейнера по позиции (как у дверей)

        static bool _inited;

        /// <summary>Зеркало слотов игрока на сервере: id игрока → виртуальный рюкзак.</summary>
        static readonly Dictionary<ulong, PlayerInventory> _mirror = new Dictionary<ulong, PlayerInventory>(128);

        // ---------- клиентская часть ----------
        static PlayerInventory _local;
        static bool _dirty;
        static bool _forceFull;
        static float _nextSync;
        static float _nextFullSync;

        // ================== ИНИЦИАЛИЗАЦИЯ ==================

        public static void Init()
        {
            if (_inited) return;
            _inited = true;

            var go = new GameObject("~InventoryNet");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<InventoryNetSync>();

            Hook();
            NetworkBridge.HostChanged += Hook;
            Debug.Log("[Net] InventoryNet: зеркало инвентарей + серверный лут активны");
        }

        static void Hook()
        {
            var host = NetworkBridge.Host;
            if (host == null) return;

            // Офлайн цикл замыкается сам (LocalHost); в Mirror команды роутит MirrorPlayer.
            host.OnCommand -= OnCommand;
            if (host is LocalHost) host.OnCommand += OnCommand;

            host.OnRpc -= OnRpc;
            host.OnRpc += OnRpc;

            host.OnPlayerDisconnected -= OnPlayerDisconnected;
            host.OnPlayerDisconnected += OnPlayerDisconnected;
        }

        static void OnPlayerDisconnected(ulong player, string reason)
        {
            _mirror.Remove(player);
            _open.Remove(player);
            _lastDeath.Remove(player);
            if (NetworkBridge.IsServer) Debug.Log($"[Net] InventoryNet: зеркало игрока {player} выброшено");
        }

        /// <summary>Клиент: взять инвентарь под наблюдение (вызывается после создания игрока).</summary>
        public static void HookPlayer(PlayerInventory inv)
        {
            if (_local == inv) return;
            if (_local != null) _local.Changed -= OnLocalChanged;
            _local = inv;
            if (_local != null) _local.Changed += OnLocalChanged;
            _dirty = true; _forceFull = true;
        }

        static void OnLocalChanged(IItemContainer c)
        {
            _dirty = true;
        }

        static PlayerInventory CurrentInventory()
            => _local ?? (Subsistence.Runtime.RuntimeBootstrap.Instance != null && Subsistence.Runtime.RuntimeBootstrap.Instance.Player != null
                ? Subsistence.Runtime.RuntimeBootstrap.Instance.Player.inventory
                : null);

        // ================== КЛИЕНТ → СЕРВЕР: дельта инвентаря ==================

        internal static void Tick()
        {
            if (!_inited || !Enabled) return;
            if (!NetworkBridge.IsClient || _local == null) return;   // офлайн: сервер и так всё знает

            if (_forceFull)
            {
                _forceFull = false; _dirty = false;
                _nextSync = Time.time + SyncInterval;
                _nextFullSync = Time.time + FullSyncInterval;
                SendSync(true);
                return;
            }
            if (!_dirty) return;
            if (Time.time < _nextSync) return;                        // подождём — изменения накопятся

            bool full = Time.time >= _nextFullSync;
            _dirty = false;
            _nextSync = Time.time + SyncInterval;
            if (full) _nextFullSync = Time.time + FullSyncInterval;
            SendSync(full);
        }

        static void SendSync(bool full)
        {
            ulong mask = full ? ulong.MaxValue : _local.DirtyMask;
            if (mask == 0UL) return;

            int count = 0;
            for (int i = 0; i < _local.SlotCount; i++)
                if ((mask & (1UL << i)) != 0UL) count++;

            var w = BufferWriter.Rent(768);
            w.WriteByte(full ? (byte)1 : (byte)0);
            w.WriteULong(mask);
            w.WriteByte((byte)Mathf.Min(count, 255));
            for (int i = 0; i < _local.SlotCount && count > 0; i++)
            {
                if ((mask & (1UL << i)) == 0UL) continue;
                var s = _local.Get(i);
                w.WriteByte((byte)i);
                w.WriteBool(s == null || s.IsEmpty);
                if (s != null && !s.IsEmpty) WriteStack(w, s);
                count--;
            }
            NetworkBridge.Command(ClientCommand.InvSync, w.ToArray());
            BufferWriter.Return(w);
            _local.ClearDirty();
        }

        // ================== СЕРВЕР: зеркало ==================

        static PlayerInventory Mirror(ulong id)
        {
            if (!_mirror.TryGetValue(id, out var inv))
            {
                inv = new PlayerInventory();
                _mirror[id] = inv;
            }
            return inv;
        }

        /// <summary>Инвентарь, которым распоряжается сервер: офлайн — настоящий, в сети — зеркало.</summary>
        static ItemContainer ServerInventory(ulong id)
        {
            if (IsLocalPlayer(id))
            {
                var pc = DeployNet.PlayerOf(id);      // офлайн/хост: это объект из RuntimeBootstrap
                if (pc != null && pc.inventory != null) return pc.inventory;
            }
            return Mirror(id);
        }

        /// <summary>
        /// Наш ли это игрок (офлайн или хост-клиент)? У него настоящий инвентарь в этом же процессе —
        /// значит платим и выдаём напрямую. Чужие игроки живут в зеркале и получают адресные RPC.
        /// </summary>
        public static bool IsLocalPlayer(ulong id)
        {
            var host = NetworkBridge.Host;
            if (host == null) return true;            // офлайн
            if (!host.IsClient) return false;         // выделенный сервер: все игроки чужие
            return id == host.LocalPlayerId;          // хост-клиент
        }

        /// <summary>Забрать предмет из конкретного слота игрока (например, перекладывание в ящик).</summary>
        static void ServerConsumeSlot(ulong id, int slot, int amount)
        {
            var inv = ServerInventory(id);
            var s = inv != null ? inv.Get(slot) : null;
            if (s == null || s.IsEmpty) return;

            int moved = Mathf.Min(amount, s.amount);
            string itemId = s.id;
            s.amount -= moved;
            inv.Set(slot, s.amount > 0 ? s : null);
            // Чужому игроку предмет снимаем по сети: сам он об этом действии не знал.
            if (!IsLocalPlayer(id)) SendTake(id, itemId, moved);
        }

        public static int ServerCount(ulong id, string itemId)
        {
            var inv = ServerInventory(id);
            return inv != null ? inv.CountOf(itemId) : 0;
        }

        public static bool ServerHas(ulong id, string itemId, int amount = 1)
            => ServerCount(id, itemId) >= amount;

        /// <summary>
        /// Списать предмет у игрока (сервер). true — списано полностью.
        /// <paramref name="notifyClient"/> нужен только там, где клиент НЕ списывал предмет сам
        /// (например, перекладывание в ящик): иначе получится двойное списание — клиент уже
        /// заплатил оптимистично, а сервер спишет второй раз (пользователь потерял бы предмет).
        /// </summary>
        public static bool ServerConsume(ulong id, string itemId, int amount = 1, bool notifyClient = false)
        {
            if (amount <= 0) return true;
            var inv = ServerInventory(id);
            if (inv == null || inv.CountOf(itemId) < amount) return false;
            if (IsLocalPlayer(id)) return inv.RemoveAmount(itemId, amount);   // свой игрок: списываем напрямую

            inv.RemoveAmount(itemId, amount);    // зеркало: слоты клиент подтвердит сам
            if (notifyClient) SendTake(id, itemId, amount);
            return true;
        }

        /// <summary>Выдать предмет игроку (сервер). Возвращает остаток, который не влез.</summary>
        public static int ServerAdd(ulong id, ItemStack stack)
        {
            if (stack == null || stack.IsEmpty) return 0;
            var inv = ServerInventory(id);
            if (inv == null) return stack.amount;

            int total = stack.amount;
            int left = inv.TryAdd(stack);        // TryAdd «съедает» стек — количество считаем заранее
            int given = total - left;
            if (!IsLocalPlayer(id) && given > 0) SendGive(id, stack, given);   // чужому — по сети
            return left;
        }

        /// <summary>
        /// Поместится ли стек целиком. Считаем на копии инвентаря: подбор идёт «всё или ничего» —
        /// иначе пришлось бы возвращать игроку половину стека.
        /// </summary>
        public static bool ServerHasRoomFor(ulong id, ItemStack stack)
        {
            if (stack == null || stack.IsEmpty) return true;
            var source = ServerInventory(id);
            if (source == null) return false;

            var probe = new PlayerInventory();
            for (int i = 0; i < source.SlotCount; i++)
            {
                var s = source.Get(i);
                if (s != null && !s.IsEmpty) probe.TryAdd(Clone(s, s.amount));
            }
            return probe.TryAdd(Clone(stack, stack.amount)) == 0;
        }

        public static bool ServerCanAfford(ulong id, IList<ItemStack> cost)
        {
            if (cost == null) return true;
            var inv = ServerInventory(id);
            if (inv == null) return false;
            for (int i = 0; i < cost.Count; i++)
            {
                var c = cost[i];
                if (c == null || c.IsEmpty) continue;
                if (inv.CountOf(c.id) < c.amount) return false;
            }
            return true;
        }

        /// <summary>Списать стоимость (крафт/постройка). Каждая позиция уходит адресно клиенту.</summary>
        public static bool ServerPay(ulong id, IList<ItemStack> cost)
        {
            if (!ServerCanAfford(id, cost)) return false;
            for (int i = 0; i < cost.Count; i++)
            {
                var c = cost[i];
                if (c == null || c.IsEmpty) continue;
                ServerConsume(id, c.id, c.amount);
            }
            return true;
        }

        /// <summary>Отдать игроку список координат его зеркала (диагностика/логи).</summary>
        // ================== АДРЕС КОНТЕЙНЕРА (Handle) ==================

        /// <summary>
        /// Адрес контейнера в сети. Лут-ящики мира статичны — их ищем по позиции (как двери),
        /// а деплои (ящик, печь, мешок с трупом) имеют сетевой netId — адресуемся по нему.
        /// sub — вторая ёмкость объекта: у печи 0 = вход, 1 = выход.
        /// </summary>
        public struct Handle
        {
            public byte kind;        // 0 = контейнер мира, 1 = деплой
            public Vector3 pos;
            public uint netId;
            public byte sub;

            public bool IsValid => kind == 1 ? netId != 0 : true;
            public Handle With(byte newSub) => new Handle { kind = kind, pos = pos, netId = netId, sub = newSub };
        }

        /// <summary>Кто что держит открытым (сервер): чтобы рассылать изменения только этим игрокам.</summary>
        static readonly Dictionary<ulong, Handle> _open = new Dictionary<ulong, Handle>(128);

        /// <summary>Когда игрок последний раз оставлял мешок с лутом (защита от «смерть-спама»).</summary>
        static readonly Dictionary<ulong, float> _lastDeath = new Dictionary<ulong, float>(64);

        public static Handle HandleOf(Subsistence.World.LootContainer c)
        {
            var h = new Handle { kind = 0, pos = c != null ? c.transform.position : Vector3.zero };
            if (c == null) return h;

            // Объект, созданный сервером (мешок с трупом, аирдроп), знает свой netId — адресуем им:
            // этот id одинаков у всех. У ящиков, сгенерированных уровнем, netId свой у каждого
            // клиента (сид один, а id — нет), поэтому для них остаётся адрес по позиции.
            var marker = c.GetComponent<NetSpawned>();
            if (marker == null) marker = c.GetComponentInParent<NetSpawned>();
            if (marker != null && marker.netId != 0u) { h.kind = 1; h.netId = marker.netId; }
            return h;
        }

        public static Handle HandleOf(Subsistence.Building.BuildDeployable d, byte sub = 0)
        {
            var h = new Handle { kind = 1, sub = sub };
            if (d == null) return h;
            h.pos = d.transform.position;
            var marker = d.GetComponent<NetSpawned>();
            if (marker == null) marker = d.GetComponentInParent<NetSpawned>();
            h.netId = marker != null ? marker.netId : 0u;
            return h;
        }

        static void WriteHandle(BufferWriter w, Handle h)
        {
            w.WriteByte(h.kind);
            w.WritePosition(h.pos, -100f, 1000f);
            w.WriteUInt(h.netId);
            w.WriteByte(h.sub);
        }

        static Handle ReadHandle(BufferReader r)
            => new Handle { kind = r.ReadByte(), pos = r.ReadPosition(-100f, 1000f), netId = r.ReadUInt(), sub = r.ReadByte() };

        public static bool SameHandle(Handle a, Handle b)
        {
            if (a.kind != b.kind) return false;
            if (a.kind == 1) return a.netId == b.netId;      // деплой — по netId
            return Vector3.Distance(a.pos, b.pos) < 0.5f;    // мир — по позиции
        }

        /// <summary>Контейнер внутри объекта: ящик, печь (вход/выход) или мешок с трупом.</summary>
        static ItemContainer ContainerOf(GameObject go, byte sub)
        {
            if (go == null) return null;
            var box = go.GetComponent<Subsistence.Building.StorageBox>();
            if (box != null) return box.storage;
            var furnace = go.GetComponent<Subsistence.Building.Furnace>();
            if (furnace != null) return sub == 1 ? furnace.output : furnace.input;
            var loot = go.GetComponent<Subsistence.World.LootContainer>();
            if (loot != null) return loot.Items;
            return null;
        }

        static int SubCount(GameObject go)
            => go != null && go.GetComponent<Subsistence.Building.Furnace>() != null ? 2 : 1;

        /// <summary>Сервер: контейнер по адресу + объект (нужен и для дистанции, и для рассылки).</summary>
        static ItemContainer Resolve(Handle h, out Transform tr, out GameObject go)
        {
            tr = null; go = null;
            if (h.kind == 0)
            {
                var lc = Subsistence.World.LootContainer.FindAt(h.pos, LootFindRadius);
                if (lc == null) return null;
                tr = lc.transform; go = lc.gameObject;
                return lc.Items;
            }
            go = SpawnNet.ById(h.netId);
            if (go == null) return null;
            tr = go.transform;
            return ContainerOf(go, h.sub);
        }

        /// <summary>Есть ли у объекта вторая ёмкость (печь: вход/выход).</summary>
        public static bool HasSecondary(Handle h)
        {
            if (h.kind == 0) return false;
            var go = SpawnNet.ById(h.netId);
            return go != null && SubCount(go) > 1;
        }

        /// <summary>Клиент: где у нас лежит эта ёмкость (сервер присылает сюда содержимое).</summary>
        public static ItemContainer ClientContainer(Handle h)
        {
            if (h.kind == 0)
            {
                var lc = Subsistence.World.LootContainer.FindAt(h.pos, LootFindRadius);
                return lc != null ? lc.Items : null;
            }
            return ContainerOf(SpawnNet.ById(h.netId), h.sub);
        }

        public static int MirrorSlotCount(ulong id) => _mirror.TryGetValue(id, out var inv) ? inv.SlotCount : 0;

        // ================== СЕРВЕР: команды ==================

        static void OnCommand(ulong from, ClientCommand cmd, byte[] payload)
            => HandleServerCommand(from, cmd, payload);

        /// <summary>Вызывается из MirrorPlayer (в Mirror) и из LocalHost (офлайн).</summary>
        public static void HandleServerCommand(ulong from, ClientCommand cmd, byte[] payload)
        {
            if (!NetworkBridge.IsServer) return;
            switch (cmd)
            {
                case ClientCommand.InvSync:  ServerSync(from, payload); break;
                case ClientCommand.LootOpen: ServerOpen(from, payload); break;
                case ClientCommand.LootTake: ServerTake(from, payload); break;
                case ClientCommand.LootStore: ServerStore(from, payload); break;
                case ClientCommand.PlayerDied: ServerDeath(from, payload); break;
            }
        }

        static void ServerSync(ulong from, byte[] payload)
        {
            var r = new BufferReader(payload);
            r.ReadByte();                                   // 1 = полная сверка, 0 = дельта (для отладки)
            r.ReadULong();                                  // маска изменённых слотов
            int count = r.ReadByte();
            var inv = Mirror(from);
            for (int k = 0; k < count; k++)
            {
                int index = r.ReadByte();
                bool empty = r.ReadBool();
                if (empty) { inv.Set(index, null); continue; }
                inv.Set(index, ReadStack(r));
            }
        }

        static void ServerOpen(ulong from, byte[] payload)
        {
            var r = new BufferReader(payload);
            var h = ReadHandle(r);
            var c = Resolve(h, out var tr, out var go);
            if (c == null || !InReach(from, tr)) return;
            _open[from] = h;                                  // помним адрес: сюда же шлём изменения
            SendAll(from, h, go);
        }

        static void ServerTake(ulong from, byte[] payload)
        {
            var r = new BufferReader(payload);
            var h = ReadHandle(r);
            int slot = r.ReadByte();
            int amount = r.ReadUShort();

            var c = Resolve(h, out var tr, out _);
            if (c == null || !InReach(from, tr)) return;

            if (slot == 255)                                  // «взять всё» из этой ёмкости
            {
                for (int i = 0; i < c.SlotCount; i++) ServerTakeSlot(from, h, c, i, int.MaxValue);
            }
            else ServerTakeSlot(from, h, c, slot, amount <= 0 ? int.MaxValue : amount);

            if (h.kind == 0)                                  // у лут-ящика мира — таймер респавна
            {
                var lc = Subsistence.World.LootContainer.FindAt(h.pos, LootFindRadius);
                if (lc != null && lc.IsEmpty) lc.Looted();
            }
        }

        static void ServerTakeSlot(ulong from, Handle h, ItemContainer c, int slot, int amount)
        {
            var stack = c.Get(slot);
            if (stack == null || stack.IsEmpty) return;

            amount = Mathf.Clamp(amount, 1, stack.amount);
            var taken = Clone(stack, amount);

            int left = ServerAdd(from, taken);                // сначала пробуем отдать: не влезло — не берём
            int moved = amount - left;
            if (moved <= 0)
            {
                DeployNet.Notify(from, "Инвентарь полон");
                return;
            }

            stack.amount -= moved;
            c.Set(slot, stack.amount > 0 ? stack : null);
            BroadcastSlot(h, slot, c);
        }

        static void ServerStore(ulong from, byte[] payload)
        {
            var r = new BufferReader(payload);
            var h = ReadHandle(r);
            int invSlot = r.ReadByte();
            int amount = r.ReadUShort();

            var c = Resolve(h, out var tr, out _);
            if (c == null || !InReach(from, tr)) return;

            var inv = ServerInventory(from);
            var s = inv != null ? inv.Get(invSlot) : null;
            if (s == null || s.IsEmpty) return;

            amount = Mathf.Clamp(amount, 1, s.amount);
            var before = Snapshot(c);
            int left = c.TryAdd(Clone(s, amount));
            int moved = amount - left;
            if (moved <= 0) { DeployNet.Notify(from, "Здесь нет места"); return; }

            ServerConsumeSlot(from, invSlot, moved);           // чужому уйдёт inv.take: сам он этого не делал
            BroadcastDiff(h, c, before);
        }

        /// <summary>
        /// Смерть игрока: сервер собирает мешок из своего зеркала, кладёт его в мир (адрес — netId)
        /// и обнуляет инвентарь мёртвого, чтобы после респавна не осталось копий.
        /// </summary>
        static void ServerDeath(ulong from, byte[] payload)
        {
            if (!NetworkBridge.IsServer) return;
            var r = new BufferReader(payload);
            var pos = r.ReadPosition(-100f, 1000f);
            float yaw = r.ReadUShort() / 65535f * 360f;

            // Античит: мешок появляется только там, где игрок и вправду был, и не чаще раза в 10 с.
            var pc = DeployNet.PlayerOf(from);
            if (pc == null && !(NetworkBridge.Host is LocalHost)) return;      // неизвестный игрок — не верим
            if (pc != null && Vector3.Distance(pc.transform.position, pos) > CorpseReach) return;
            if (_lastDeath.TryGetValue(from, out float last) && Time.time - last < CorpseCooldown) return;
            _lastDeath[from] = Time.time;

            var inv = ServerInventory(from);
            var bag = SpawnNet.ServerSpawnBag(pos, yaw, from);
            int count = 0;
            if (bag != null && inv != null)
            {
                for (int i = 0; i < inv.SlotCount; i++)
                {
                    var s = inv.Get(i);
                    if (s == null || s.IsEmpty) continue;
                    if (bag.Items.TryAdd(Clone(s, s.amount)) == 0) count++;
                }
            }

            // У мёртвого пусто — и в зеркале, и в его рюкзаке (иначе после респавна дубли).
            if (inv != null) for (int i = 0; i < inv.SlotCount; i++) inv.Set(i, null);
            _open.Remove(from);
            if (!IsLocalPlayer(from)) SendClear(from);
            CombatNet.OnPlayerDied(from);                     // зеркало здоровья обнуляем (и урон по трупу не считаем)

            Debug.Log($"[Net] игрок {from} погиб: мешок с лутом на {pos} ({count} стеков)");
        }

        static void SendClear(ulong to)
        {
            var w = BufferWriter.Rent(16);
            w.WriteULong(to);
            PlayerRpc.Send(to, "inv.clear", w.ToArray());
            BufferWriter.Return(w);
        }

        /// <summary>Дотягивается ли игрок до контейнера (серверная проверка дистанции).</summary>
        static bool InReach(ulong from, Transform tr)
        {
            if (tr == null) return true;
            var pc = DeployNet.PlayerOf(from);
            if (pc == null) return true;                       // игрок неизвестен (офлайн) — не мешаем
            return Vector3.Distance(pc.transform.position, tr.position) <= LootReach;
        }

        // ================== СЕРВЕР → КЛИЕНТ ==================

        static void SendGive(ulong to, ItemStack s, int amount)
        {
            var w = BufferWriter.Rent(128);
            w.WriteULong(to);
            WriteStack(w, Clone(s, amount));
            PlayerRpc.Send(to, "inv.give", w.ToArray());
            BufferWriter.Return(w);
        }

        static void SendTake(ulong to, string itemId, int amount)
        {
            var w = BufferWriter.Rent(64);
            w.WriteULong(to);
            w.WriteString(itemId);
            w.WriteUShort((ushort)Mathf.Clamp(amount, 0, 65535));
            PlayerRpc.Send(to, "inv.take", w.ToArray());
            BufferWriter.Return(w);
        }

        /// <summary>Содержимое одной ёмкости — тому, кто её открыл.</summary>
        static void SendContainer(ulong to, Handle h, ItemContainer c)
        {
            var w = BufferWriter.Rent(640);
            w.WriteULong(to);
            WriteHandle(w, h);
            int count = 0;
            for (int i = 0; i < c.SlotCount; i++) { var s = c.Get(i); if (s != null && !s.IsEmpty) count++; }
            w.WriteByte((byte)Mathf.Min(count, 255));
            for (int i = 0; i < c.SlotCount && count > 0; i++)
            {
                var s = c.Get(i);
                if (s == null || s.IsEmpty) continue;
                w.WriteByte((byte)i);
                WriteStack(w, s);
                count--;
            }
            PlayerRpc.Send(to, "loot.contents", w.ToArray());
            BufferWriter.Return(w);
        }

        /// <summary>Все ёмкости объекта: у ящика одна, у печи две (вход/выход).</summary>
        static void SendAll(ulong to, Handle h, GameObject go)
        {
            int subs = SubCount(go);
            for (byte s = 0; s < subs; s++)
            {
                var c = ContainerOf(go, s);
                if (c != null) SendContainer(to, h.With(s), c);
            }
        }

        /// <summary>Разослать обновление только тем, у кого этот контейнер открыт (а не всем подряд).</summary>
        static void RefreshHolders(Handle h, GameObject go)
        {
            if (go == null) return;
            foreach (var kv in _open)
                if (SameHandle(kv.Value, h)) SendAll(kv.Key, kv.Value, go);
        }

        static void BroadcastSlot(Handle h, int slot, ItemContainer c)
        {
            var s = c.Get(slot);
            var w = BufferWriter.Rent(160);
            WriteHandle(w, h);
            w.WriteByte((byte)Mathf.Clamp(slot, 0, 255));
            w.WriteBool(s != null && !s.IsEmpty);
            if (s != null && !s.IsEmpty) WriteStack(w, s);
            var data = w.ToArray();
            BufferWriter.Return(w);

            foreach (var kv in _open)
                if (SameHandle(kv.Value, h)) PlayerRpc.Send(kv.Key, "loot.slot", data);
        }

        /// <summary>Разослать только изменившиеся слоты (после перекладывания предмета).</summary>
        static void BroadcastDiff(Handle h, ItemContainer c, ItemStack[] before)
        {
            for (int i = 0; i < c.SlotCount; i++)
            {
                var now = c.Get(i);
                var old = i < before.Length ? before[i] : null;
                if (SameStack(now, old)) continue;
                BroadcastSlot(h, i, c);
            }
        }

        /// <summary>Ящик мира снова полон — обновляем у тех, кто его держит открытым.</summary>
        public static void ServerRefilled(Subsistence.World.LootContainer c)
        {
            if (!NetworkBridge.IsServer || c == null) return;
            RefreshHolders(HandleOf(c), c.gameObject);
        }

        /// <summary>Печь переплавила руду — содержимое изменилось: обновить открытые окна.</summary>
        public static void ServerContainersChanged(Subsistence.Building.BuildDeployable d)
        {
            if (!NetworkBridge.IsServer || d == null) return;
            RefreshHolders(HandleOf(d), d.gameObject);
        }

        // ================== ПРИЁМ НА КЛИЕНТЕ ==================

        static void OnRpc(NetId target, string method, byte[] payload)
        {
            switch (method)
            {
                case "inv.give":      ApplyGive(payload); break;
                case "inv.take":      ApplyTake(payload); break;
                case "inv.clear":     ApplyClear(payload); break;
                case "loot.contents": ApplyContents(payload); break;
                case "loot.slot":     ApplySlot(payload); break;
            }
        }

        static bool IsMine(ulong id)
        {
            var host = NetworkBridge.Host;
            if (host == null) return true;
            return !host.IsClient || id == host.LocalPlayerId;
        }

        static void ApplyGive(byte[] payload)
        {
            var r = new BufferReader(payload);
            ulong to = r.ReadULong();
            var s = ReadStack(r);
            if (!IsMine(to)) return;

            var inv = CurrentInventory();
            if (inv == null) return;
            int left = inv.TryAdd(s);
            if (left > 0) Subsistence.UI.HudRuntime.ShowToast("Инвентарь полон — предмет остался на сервере", 3f);
            _dirty = true;                                   // подтвердим серверу реальное состояние
        }

        static void ApplyTake(byte[] payload)
        {
            var r = new BufferReader(payload);
            ulong to = r.ReadULong();
            string id = r.ReadString();
            int amount = r.ReadUShort();
            if (!IsMine(to)) return;

            var inv = CurrentInventory();
            inv?.RemoveAmount(id, amount);
            _dirty = true;
        }

        /// <summary>Смерть: сервер обнулил инвентарь — клиент чистит рюкзак (и вещи уехали в мешок).</summary>
        static void ApplyClear(byte[] payload)
        {
            var r = new BufferReader(payload);
            ulong to = r.ReadULong();
            if (!IsMine(to)) return;
            CurrentInventory()?.ClearAll();
            _dirty = true;
        }

        static void ApplyContents(byte[] payload)
        {
            var r = new BufferReader(payload);
            ulong to = r.ReadULong();
            var h = ReadHandle(r);
            if (to != 0UL && !IsMine(to)) return;

            int count = r.ReadByte();
            var c = ClientContainer(h);
            if (c == null) return;

            var slots = new ItemStack[c.SlotCount];
            for (int k = 0; k < count; k++)
            {
                int index = r.ReadByte();
                var stack = ReadStack(r);
                if (index >= 0 && index < slots.Length) slots[index] = stack;
            }
            // Содержимое авторитетно: чего сервер не прислал — то пусто (у клиента свои копии не живут).
            for (int i = 0; i < c.SlotCount; i++) c.Set(i, slots[i]);
            c.ClearDirty();
            Subsistence.UI.InventoryUI.RefreshOpen(h);
        }

        static void ApplySlot(byte[] payload)
        {
            var r = new BufferReader(payload);
            var h = ReadHandle(r);
            int slot = r.ReadByte();
            bool present = r.ReadBool();
            var stack = present ? ReadStack(r) : null;

            var c = ClientContainer(h);
            if (c == null || slot < 0 || slot >= c.SlotCount) return;
            c.Set(slot, stack);
            c.ClearDirty();
            Subsistence.UI.InventoryUI.RefreshOpen(h);
        }

        // ================== КЛИЕНТ: запросы ==================

        /// <summary>Открыть контейнер: сервер пришлёт содержимое (клиент своего не выдумывает).</summary>
        public static void RequestOpen(Handle h)
        {
            if (!NetworkBridge.IsClient || !h.IsValid) return;
            var w = BufferWriter.Rent(32);
            WriteHandle(w, h);
            NetworkBridge.Command(ClientCommand.LootOpen, w.ToArray());
            BufferWriter.Return(w);
        }

        /// <summary>Взять предмет (slot = 255 — «взять всё» из этой ёмкости).</summary>
        public static void RequestTake(Handle h, int slot, int amount)
        {
            if (!NetworkBridge.IsClient)
            {
                LocalTake(ClientContainer(h), h, slot, amount);
                return;
            }
            if (!h.IsValid) return;
            var w = BufferWriter.Rent(32);
            WriteHandle(w, h);
            w.WriteByte((byte)Mathf.Clamp(slot, 0, 255));
            w.WriteUShort((ushort)Mathf.Clamp(amount, 0, 65535));
            NetworkBridge.Command(ClientCommand.LootTake, w.ToArray());
            BufferWriter.Return(w);
        }

        public static void RequestTakeAll(Handle h) => RequestTake(h, 255, 0);

        /// <summary>
        /// Клиент: я умер. Мешок с лутом собирает сервер — из своего зеркала инвентаря, а не из
        /// того, что пришлёт клиент (иначе можно было бы «нафармить» мешок чужими предметами).
        /// </summary>
        public static void RequestDeath(Vector3 pos, float yaw)
        {
            if (!NetworkBridge.IsClient) return;                 // сервер/офлайн делает это напрямую
            var w = BufferWriter.Rent(32);
            w.WritePosition(pos, -100f, 1000f);
            w.WriteUShort((ushort)Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 360f * 65535f));
            NetworkBridge.Command(ClientCommand.PlayerDied, w.ToArray());
            BufferWriter.Return(w);
        }

        /// <summary>Положить предмет из инвентаря (invSlot — слот игрока).</summary>
        public static void RequestStore(Handle h, int invSlot, int amount)
        {
            if (!NetworkBridge.IsClient)
            {
                LocalStore(ClientContainer(h), h, invSlot, amount);
                return;
            }
            if (!h.IsValid) return;
            var w = BufferWriter.Rent(32);
            WriteHandle(w, h);
            w.WriteByte((byte)Mathf.Clamp(invSlot, 0, 255));
            w.WriteUShort((ushort)Mathf.Clamp(amount, 0, 65535));
            NetworkBridge.Command(ClientCommand.LootStore, w.ToArray());
            BufferWriter.Return(w);
        }

        // ---------- офлайн-пути (без сети, но с той же логикой) ----------

        static void LocalTake(ItemContainer c, Handle h, int slot, int amount)
        {
            var inv = CurrentInventory();
            if (inv == null || c == null) return;

            if (slot == 255)
            {
                for (int i = 0; i < c.SlotCount; i++) LocalTake(c, h, i, int.MaxValue);
                return;
            }
            var stack = c.Get(slot);
            if (stack == null || stack.IsEmpty) return;

            amount = Mathf.Clamp(amount, 1, stack.amount);
            int left = inv.TryAdd(Clone(stack, amount));
            int moved = amount - left;
            if (moved <= 0) { Subsistence.UI.HudRuntime.ShowToast("Инвентарь полон", 2f); return; }

            stack.amount -= moved;
            c.Set(slot, stack.amount > 0 ? stack : null);
            if (h.kind == 0)
            {
                var lc = Subsistence.World.LootContainer.FindAt(h.pos, LootFindRadius);
                if (lc != null && lc.IsEmpty) lc.Looted();
            }
            Subsistence.UI.InventoryUI.RefreshOpen(h);
        }

        static void LocalStore(ItemContainer c, Handle h, int invSlot, int amount)
        {
            var inv = CurrentInventory();
            var s = inv != null ? inv.Get(invSlot) : null;
            if (s == null || s.IsEmpty || c == null) return;

            amount = Mathf.Clamp(amount, 1, s.amount);
            int left = c.TryAdd(Clone(s, amount));
            int moved = amount - left;
            if (moved <= 0) { Subsistence.UI.HudRuntime.ShowToast("Здесь нет места", 2f); return; }

            s.amount -= moved;
            inv.Set(invSlot, s.amount > 0 ? s : null);
            Subsistence.UI.InventoryUI.RefreshOpen(h);
        }

        // ================== СЕРИАЛИЗАЦИЯ ==================

        static void WriteStack(BufferWriter w, ItemStack s)
        {
            w.WriteString(s.id);
            w.WriteUShort((ushort)Mathf.Clamp(s.amount, 0, 65535));
            var def = s.Def;
            float dur = def != null && def.HasDurability && def.maxDurability > 0f
                      ? Mathf.Clamp01(s.durability / def.maxDurability) : 1f;
            w.WriteByte((byte)Mathf.RoundToInt(dur * 255f));
            w.WriteByte((byte)Mathf.RoundToInt(Mathf.Clamp01(s.condition) * 255f));
            w.WriteByte((byte)Mathf.Clamp(s.ammoInMag, 0, 255));
            w.WriteUShort((ushort)Mathf.Clamp(s.metaTag, 0, 65535));
        }

        static ItemStack ReadStack(BufferReader r)
        {
            string id = r.ReadString();
            int amount = r.ReadUShort();
            byte dur = r.ReadByte(), cond = r.ReadByte(), ammo = r.ReadByte();
            ushort tag = r.ReadUShort();

            var s = new ItemStack(id, Mathf.Max(1, amount));
            var def = s.Def;
            s.durability = def != null && def.HasDurability ? def.maxDurability * (dur / 255f) : 0f;
            s.condition = cond > 0 ? cond / 255f : 1f;
            s.ammoInMag = ammo;
            s.metaTag = tag;                                 // ключ помнит свой замок (16_doors)
            return s;
        }

        static ItemStack Clone(ItemStack s, int amount)
        {
            var c = new ItemStack(s.id, amount);
            c.durability = s.durability;
            c.condition = s.condition;
            c.ammoInMag = s.ammoInMag;
            c.metaTag = s.metaTag;
            c.skinId = s.skinId;
            return c;
        }

        /// <summary>Копии слотов (именно копии: TryAdd меняет стеки на месте — иначе не увидим разницу).</summary>
        static ItemStack[] Snapshot(ItemContainer c)
        {
            var arr = new ItemStack[c.SlotCount];
            for (int i = 0; i < c.SlotCount; i++)
            {
                var s = c.Get(i);
                arr[i] = s != null && !s.IsEmpty ? Clone(s, s.amount) : null;
            }
            return arr;
        }

        static bool SameStack(ItemStack a, ItemStack b)
        {
            bool ae = a == null || a.IsEmpty, be = b == null || b.IsEmpty;
            if (ae || be) return ae == be;
            return a.id == b.id && a.amount == b.amount && Mathf.Abs(a.durability - b.durability) < 0.01f;
        }
    }

    /// <summary>Кадровый тик зеркала инвентаря (скрытый объект, создаёт InventoryNet.Init).</summary>
    public class InventoryNetSync : MonoBehaviour
    {
        void Update() => InventoryNet.Tick();
    }
}
