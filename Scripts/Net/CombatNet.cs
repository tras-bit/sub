// ============================================================================
//  SUBSISTENCE — Net/CombatNet.cs
//  30–31: урон по игрокам считает сервер.
//   • Монстры, взрывы, огонь и мины живут только на сервере (`if (!IsServer) return;`)
//     — их урон идёт сюда (`ServerDamage`), а до удалённой жертвы доезжает
//     адресным RPC "dmg": она сама применит урон, кровотечение и смерть.
//   • Клиент, попавший в чужого игрока, только СООБЩАЕТ о выстреле
//     (`ClientCommand.HitPlayer`: оружие + точка попадания + зона) — урон считает
//     сервер по таблице оружия, а не по числу из пакета: подделать урон нельзя.
//   • Своё здоровье клиент сообщает сам (`PlayerHealth`): голод, радиация,
//     кровотечение, утопление — это длинная симуляция, её крутит клиент. Сервер
//     принимает только ухудшение и с потолком на восстановление, поэтому
//     «вылечиться» правкой пакета нельзя.
//   • Смерть и мешок с лутом остаются за сервером (см. InventoryNet.ServerDeath).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Combat;

namespace Subsistence.Net
{
    public static class CombatNet
    {
        // ---------- настройки ----------
        const float HealthReportInterval = 0.5f;    // как часто клиент сообщает здоровье
        const float MaxHealPerReport = 5f;          // потолок восстановления за пакет (реген 0.6/с)
        const float HitRangeTolerance = 2.5f;       // запас дистанции на пинг
        const float HitPointTolerance = 3.5f;       // точка попадания должна быть рядом с целью
        const float MaxHitAngle = 65f;              // угол между взглядом и точкой попадания
        const float EyeHeight = 1.5f;               // «глаза» игрока (как у монстров)

        static readonly Dictionary<ulong, float> _health = new Dictionary<ulong, float>(64);
        static bool _inited;
        static float _nextReport;

        public static void Init()
        {
            if (_inited) return;
            _inited = true;

            var go = new GameObject("~CombatNet");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<CombatNetTick>();

            Hook();
            NetworkBridge.HostChanged += Hook;
            Debug.Log("[Net] CombatNet: урон по игрокам считается на сервере");
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

            host.OnPlayerDisconnected -= OnPlayerDisconnected;
            host.OnPlayerDisconnected += OnPlayerDisconnected;
        }

        static void OnPlayerDisconnected(ulong player, string reason)
        {
            _health.Remove(player);
            DeployNet.UnregisterKnownPlayer(player);
        }

        // ================== ЗЕРКАЛО ЗДОРОВЬЯ ==================

        /// <summary>Здоровье игрока на сервере: у своего игрока — живое, у чужого — зеркало.</summary>
        public static float HealthOf(ulong id)
        {
            if (InventoryNet.IsLocalPlayer(id))
            {
                var pc = DeployNet.PlayerOf(id);
                if (pc != null && pc.survival != null) return pc.survival.State.health;
            }
            return _health.TryGetValue(id, out float h) ? h : 100f;
        }

        static void SetHealth(ulong id, float value)
        {
            if (InventoryNet.IsLocalPlayer(id)) return;      // у своего игрока здоровье живое
            _health[id] = Mathf.Clamp(value, 0f, 300f);
        }

        /// <summary>Игрок погиб (вызывает InventoryNet, когда собирает мешок): зеркало обнуляется.</summary>
        public static void OnPlayerDied(ulong id) => SetHealth(id, 0f);

        // ================== СЕРВЕР: УРОН ==================

        /// <summary>Объект разрушаемой цели (чтобы найти на нём игрока).</summary>
        public static GameObject Of(IDestructible d) => d is MonoBehaviour mb ? mb.gameObject : null;

        /// <summary>
        /// Единая точка урона на сервере. Игрока бьём через сетевой путь (адресно), всё
        /// остальное (деплои, постройки, чужие IDestructible) — как раньше, сразу.
        /// </summary>
        public static bool ServerDamage(IDestructible target, DamageType type, float amount,
                                        Vector3 from, HitRegion region, string cause)
        {
            if (target == null || amount <= 0f) return false;

            var pc = target as Subsistence.Player.PlayerController;
            if (pc == null)
            {
                var mb = target as MonoBehaviour;
                if (mb != null) pc = mb.GetComponentInParent<Subsistence.Player.PlayerController>();
            }
            if (pc == null) return false;                 // не игрок — пусть вызывающий решает сам

            ulong id = DeployNet.IdOf(pc);
            if (id == 0UL) return false;
            ServerDamagePlayer(id, type, amount, from, region, cause);
            return true;
        }

        /// <summary>Урон конкретному игроку: свой игрок — сразу, удалённый — зеркало + RPC ему.</summary>
        public static void ServerDamagePlayer(ulong victimId, DamageType type, float amount,
                                              Vector3 from, HitRegion region, string cause)
        {
            if (!NetworkBridge.IsServer || amount <= 0f) return;

            var pc = DeployNet.PlayerOf(victimId);
            if (pc == null || !pc.IsAlive) return;

            // Хост и офлайн: сервер и жертва — один процесс, RPC самому себе не нужен.
            if (InventoryNet.IsLocalPlayer(victimId))
            {
                pc.ApplyDamage(type, amount, from, region);
                return;
            }

            SetHealth(victimId, HealthOf(victimId) - amount);
            SendDamage(victimId, type, amount, from, region, cause);
            if (HealthOf(victimId) <= 0f) Debug.Log($"[Net] игрок {victimId} убит ({cause})");
        }

        // ================== КЛИЕНТ: ПОПАДАНИЕ ПО ИГРОКУ ==================

        /// <summary>
        /// Попадание по игроку из оружия. true — попадание обработано здесь
        /// (локально его применять больше нельзя), false — цель не игрок.
        /// </summary>
        public static bool TryPlayerHit(GameObject targetGo, string weaponId, DamageType type,
                                        Vector3 hitPoint, HitRegion region)
        {
            if (targetGo == null || string.IsNullOrEmpty(weaponId)) return false;
            var pc = targetGo.GetComponentInParent<Subsistence.Player.PlayerController>();
            if (pc == null) return false;

            var host = NetworkBridge.Host;
            ulong id = DeployNet.IdOf(pc);

            // Сетевой двойник нас самих: по себе урон не считаем вовсе.
            if (host != null && host.IsClient && id != 0UL && id == host.LocalPlayerId) return true;

            if (NetworkBridge.IsServer)
            {
                // Хост и офлайн: цель рядом, в этом же процессе — считаем сразу.
                var shooter = DeployNet.LocalPlayer();
                float dist = shooter != null ? Vector3.Distance(shooter.transform.position, hitPoint) : 0f;
                float dmg = DamageFor(weaponId, type, dist, region);
                if (dmg > 0f) ServerDamagePlayer(id, type, dmg, hitPoint, region, "выстрел");
                return true;
            }

            RequestHitPlayer(id, weaponId, hitPoint, region);
            return true;
        }

        /// <summary>Клиент: сообщить серверу о попадании (числа урона не присылаем — считает сервер).</summary>
        public static void RequestHitPlayer(ulong targetId, string weaponId, Vector3 hitPoint, HitRegion region)
        {
            if (!NetworkBridge.IsClient || NetworkBridge.IsServer) return;   // сервер/офлайн считает сам
            if (targetId == 0UL || string.IsNullOrEmpty(weaponId)) return;

            var w = BufferWriter.Rent(96);
            w.WriteULong(targetId);
            w.WriteString(weaponId);
            w.WritePosition(hitPoint, -100f, 1000f);
            w.WriteByte((byte)region);
            NetworkBridge.Command(ClientCommand.HitPlayer, w.ToArray());
            BufferWriter.Return(w);
        }

        /// <summary>Урон по таблице оружия: ни клиент, ни RPC число урона не задают.</summary>
        static float DamageFor(string weaponId, DamageType type, float distance, HitRegion region)
        {
            var s = WeaponTable.Get(weaponId);
            if (s == null) return 0f;

            bool melee = type != DamageType.Bullet;
            float dmg = melee
                ? (s.meleeDamage > 0f ? s.meleeDamage : 15f)
                : WeaponTable.PelletDamageAt(s, distance);
            return dmg * HitboxResolver.RegionMultiplier(region, s.pellets > 1);
        }

        // ================== СЕРВЕР: КОМАНДЫ ==================

        static void OnCommand(ulong from, ClientCommand cmd, byte[] payload)
            => HandleServerCommand(from, cmd, payload);

        /// <summary>Вызывается из MirrorPlayer (в Mirror) и из LocalHost (офлайн).</summary>
        public static void HandleServerCommand(ulong from, ClientCommand cmd, byte[] payload)
        {
            if (!NetworkBridge.IsServer) return;
            switch (cmd)
            {
                case ClientCommand.HitPlayer:    ServerHitPlayer(from, payload); break;
                case ClientCommand.PlayerHealth: ServerHealth(from, payload); break;
            }
        }

        /// <summary>Проверка попадания: оружие у стрелка, дистанция, угол, линия видимости.</summary>
        static void ServerHitPlayer(ulong from, byte[] payload)
        {
            var r = new BufferReader(payload);
            ulong targetId = r.ReadULong();
            string weaponId = r.ReadString();
            var hitPoint = r.ReadPosition(-100f, 1000f);
            var region = (HitRegion)r.ReadByte();

            if (targetId == 0UL || targetId == from || string.IsNullOrEmpty(weaponId)) return;

            var shooter = DeployNet.PlayerOf(from);
            var victim = DeployNet.PlayerOf(targetId);
            if (shooter == null || victim == null) return;
            if (!shooter.IsAlive || !victim.IsAlive) return;

            // 1) оружие должно быть у стрелка — по зеркалу инвентаря (можно и не верить клиенту)
            if (SpawnNet.RequireServerInventory && InventoryNet.ServerCount(from, weaponId) <= 0) return;

            var stats = WeaponTable.Get(weaponId);
            if (stats == null) return;

            // 2) дистанции: до точки попадания — по дальности оружия, до жертвы — рядом с попаданием
            var shooterPos = shooter.transform.position;
            float shotDist = Vector3.Distance(shooterPos, hitPoint);
            if (shotDist > stats.effectiveRange + HitRangeTolerance) return;
            if (Vector3.Distance(victim.transform.position + Vector3.up * 1.1f, hitPoint) > HitPointTolerance) return;

            // 3) направление взгляда (сервер знает позицию и поворот игрока из Move-команд)
            Vector3 eye = shooterPos + Vector3.up * EyeHeight;
            Vector3 dir = hitPoint - eye;
            if (dir.sqrMagnitude < 0.01f) return;
            if (Vector3.Angle(shooter.transform.forward, dir) > MaxHitAngle) return;

            // 4) линия видимости: сквозь стену не стреляем
            if (Physics.Linecast(eye, hitPoint, Layers.Mask(Layers.LevelGeometry, Layers.Buildable, Layers.Deployable),
                                 QueryTriggerInteraction.Ignore)) return;

            float dmg = DamageFor(weaponId, DamageType.Bullet, shotDist, region);
            if (dmg <= 0f) return;

            ServerDamagePlayer(targetId, DamageType.Bullet, dmg, hitPoint, region, "выстрел");
            Debug.Log($"[Net] {from} → {targetId}: {dmg:F0} ({region}, {shotDist:F1} м)");
        }

        /// <summary>
        /// Клиент сообщил своё здоровье (голод, радиация, кровотечение). Принимаем только
        /// ухудшение; рост — лишь медленное восстановление или возрождение (зеркало было 0).
        /// </summary>
        static void ServerHealth(ulong from, byte[] payload)
        {
            if (!NetworkBridge.IsServer) return;
            if (InventoryNet.IsLocalPlayer(from)) return;        // свой игрок: живая симуляция точнее

            var r = new BufferReader(payload);
            float reported = r.ReadUShort() / 10f;
            float mirror = HealthOf(from);

            if (reported > mirror && mirror > 0f && reported - mirror > MaxHealPerReport) return;
            SetHealth(from, reported);
        }

        // ================== СЕРВЕР → КЛИЕНТ ==================

        static void SendDamage(ulong to, DamageType type, float amount, Vector3 from, HitRegion region, string cause)
        {
            var w = BufferWriter.Rent(96);
            w.WriteULong(to);
            w.WriteByte((byte)type);
            w.WriteUShort((ushort)Mathf.Clamp(Mathf.RoundToInt(amount * 10f), 0, 65535));
            w.WritePosition(from, -100f, 1000f);
            w.WriteByte((byte)region);
            w.WriteString(cause ?? "урон");
            PlayerRpc.Send(to, "dmg", w.ToArray());
            BufferWriter.Return(w);
        }

        static void OnRpc(NetId target, string method, byte[] payload)
        {
            if (method == "dmg") ApplyDamage(payload);
        }

        static bool IsMine(ulong id)
        {
            var host = NetworkBridge.Host;
            if (host == null) return true;
            return !host.IsClient || id == host.LocalPlayerId;
        }

        /// <summary>Жертва применила серверный урон: смерть и мешок с лутом поедут сами.</summary>
        static void ApplyDamage(byte[] payload)
        {
            var r = new BufferReader(payload);
            ulong to = r.ReadULong();
            if (!IsMine(to)) return;

            var type = (DamageType)r.ReadByte();
            float amount = r.ReadUShort() / 10f;
            var from = r.ReadPosition(-100f, 1000f);
            var region = (HitRegion)r.ReadByte();
            string cause = r.ReadString();

            var pc = DeployNet.LocalPlayer();
            if (pc == null || !pc.IsAlive) return;

            pc.ApplyDamage(type, amount, from, region);
            if (!string.IsNullOrEmpty(cause))
                Subsistence.UI.HudRuntime.ShowToast($"{cause}: -{Mathf.RoundToInt(amount)}", 1.5f);
        }

        // ================== ТИК КЛИЕНТА ==================

        /// <summary>Раз в 0.5 с шлём своё здоровье серверу (только удалённый клиент).</summary>
        public static void Tick()
        {
            if (!NetworkBridge.IsClient || NetworkBridge.IsServer) return;
            if (Time.time < _nextReport) return;
            _nextReport = Time.time + HealthReportInterval;

            var pc = DeployNet.LocalPlayer();
            if (pc == null || pc.survival == null) return;

            var w = BufferWriter.Rent(16);
            w.WriteUShort((ushort)Mathf.Clamp(Mathf.RoundToInt(pc.survival.State.health * 10f), 0, 65535));
            NetworkBridge.Command(ClientCommand.PlayerHealth, w.ToArray());
            BufferWriter.Return(w);
        }
    }

    /// <summary>Кадровый тик отчётов о здоровье (скрытый объект, создаёт CombatNet.Init).</summary>
    public class CombatNetTick : MonoBehaviour
    {
        void Update() => CombatNet.Tick();
    }
}
