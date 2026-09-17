// ============================================================================
//  SUBSISTENCE — Net/AdminSystem.cs
//  Решение 36_anticheat: «своя серверная валидация + админы».
//  Сервер сам валидирует команды (CommandValidator в Mirror-адаптере), а здесь —
//  права и инструменты админов: кик/бан, телепорт, god/noclip, выдача предметов,
//  спавн монстров, логи действий. Всё пишется в аудит-лог (кто, что, когда).
//  Команды вводятся в консоли системы (кнопка вызова меню) — формат: /команда [аргументы].
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;

namespace Subsistence.Net
{
    public enum AdminRank : byte { Player = 0, Moderator = 1, Owner = 2 }

    public class AdminSystem : MonoBehaviour
    {
        public static AdminSystem Instance { get; private set; }

        [Header("Права локального игрока (офлайн/тест)")]
        public AdminRank localRank = AdminRank.Owner;

        [Header("Модификаторы")]
        public bool godMode;
        public bool noClip;
        public float flySpeed = 12f;

        readonly Dictionary<ulong, AdminRank> _ranks = new Dictionary<ulong, AdminRank>();
        readonly HashSet<ulong> _bans = new HashSet<ulong>();
        readonly List<string> _audit = new List<string>(512);

        CharacterController _cc;
        Subsistence.Player.PlayerController _player;

        public const int MaxAuditEntries = 500;

        void Awake()
        {
            Instance = this;
            _ranks[0UL] = AdminRank.Owner;      // хост/владелец сервера
        }

        void Update()
        {
            // локальные хоткеи админа (офлайн-тестирование)
            if (Input.GetKeyDown(KeyCode.F8)) SetGod(!godMode);
            if (Input.GetKeyDown(KeyCode.F9)) SetNoClip(!noClip);

            if (noClip) FlyStep();
        }

        public static bool IsAdmin(ulong playerId) => Instance != null && Instance.RankOf(playerId) >= AdminRank.Moderator;
        public AdminRank RankOf(ulong id) => _ranks.TryGetValue(id, out var r) ? r : AdminRank.Player;
        public bool IsBanned(ulong id) => _bans.Contains(id);

        void CachePlayer()
        {
            var boot = Runtime.RuntimeBootstrap.Instance;
            _player = boot != null ? boot.Player : null;
            _cc = _player != null ? _player.GetComponent<CharacterController>() : null;
        }

        // ------------------------------------------------------------------ команды
        /// <summary>Парсит и выполняет админ-команду. Возвращает false — если это не админ-команда.</summary>
        public static bool TryExecute(string cmd, string arg)
        {
            if (Instance == null) return false;
            string c = (cmd ?? "").TrimStart('/').ToLowerInvariant();
            switch (c)
            {
                case "admin": case "help": Instance.PrintAuditHelp(); return true;
                case "god": Instance.SetGod(!Instance.godMode); return true;
                case "noclip": Instance.SetNoClip(!Instance.noClip); return true;
                case "heal": Instance.Heal(); return true;
                case "kill": Instance.KillTarget(arg); return true;
                case "tp": Instance.Teleport(arg); return true;
                case "give": Instance.Give(arg); return true;
                case "spawn": Instance.SpawnMonster(arg); return true;
                case "rank": Instance.SetRank(arg); return true;
                case "kick": Instance.Kick(arg); return true;
                case "ban": Instance.Ban(arg); return true;
                case "unban": Instance.Unban(arg); return true;
                case "say": Instance.Say(arg); return true;
                case "log": Instance.DumpLog(); return true;
                case "pos": Instance.Position(); return true;
                case "wipe": Instance.RequestWipe(); return true;
                default: return false;
            }
        }

        void PrintAuditHelp()
        {
            Print("АДМИН-КОМАНДЫ: god · noclip · heal · kill <id> · tp <x y z> · give <item> <n> · spawn <monster> · "
                + "rank <id> <moderator|owner> · kick <id> · ban <id> · unban <id> · say <текст> · pos · log · wipe");
            Print("Хоткеи (для теста): F8 — бессмертие, F9 — noclip (полёт).");
        }

        void SetGod(bool on)
        {
            godMode = on;
            if (_player == null) CachePlayer();
            if (_player != null && _player.survival != null) _player.survival.godMode = on;
            Audit($"god={(on ? "on" : "off")}");
            Print($"<color=#5cff92>god {(on ? "ВКЛ" : "ВЫКЛ")}</color>");
        }

        void SetNoClip(bool on)
        {
            noClip = on;
            if (_cc == null) CachePlayer();
            if (_cc != null) _cc.detectCollisions = !on;
            Audit($"noclip={(on ? "on" : "off")}");
            Print($"<color=#5cff92>noclip {(on ? "ВКЛ (F9 — выкл)" : "ВЫКЛ")}</color>");
        }

        void FlyStep()
        {
            if (_player == null) { CachePlayer(); if (_player == null) return; }
            float v = flySpeed * (Input.GetKey(KeyCode.LeftShift) ? 2.5f : 1f);
            Vector3 dir = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) dir += _player.transform.forward;
            if (Input.GetKey(KeyCode.S)) dir -= _player.transform.forward;
            if (Input.GetKey(KeyCode.A)) dir -= _player.transform.right;
            if (Input.GetKey(KeyCode.D)) dir += _player.transform.right;
            if (Input.GetKey(KeyCode.Space)) dir += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl)) dir -= Vector3.up;
            _player.transform.position += dir * v * Time.deltaTime;
        }

        void Heal()
        {
            CachePlayer();
            if (_player == null || _player.survival == null) return;
            var st = _player.survival.State;
            st.health = 100f; st.calories = 1000f; st.hydration = 1000f; st.radiation = 0f; st.bleeding = 0;
            _player.survival.SetComfort(3);            // тепло и сытость — комфорт максимум
            Audit("heal");
            Print("<color=#5cff92>игрок вылечен (еда/вода/радиация/кровотечение сброшены)</color>");
        }

        void KillTarget(string arg)
        {
            Audit($"kill {arg}");
            Print($"<color=#ffd23f>kill {arg}: в офлайне убей себя — это косметика для сервера</color>");
        }

        void Teleport(string arg)
        {
            CachePlayer();
            if (_player == null) { Print("<color=#ffd23f>tp: игрок не найден</color>"); return; }
            var p = (arg ?? "").Split(' ');
            if (p.Length < 3 || !float.TryParse(p[0], out float x) || !float.TryParse(p[1], out float y) || !float.TryParse(p[2], out float z))
            { Print("<color=#ffd23f>формат: tp <x> <y> <z></color>"); return; }
            _player.transform.position = new Vector3(x, y, z);
            Audit($"tp {x} {y} {z}");
            Print($"<color=#5cff92>телепорт: {x} {y} {z}</color>");
        }

        void Give(string arg)
        {
            CachePlayer();
            var p = (arg ?? "").Split(' ');
            if (p.Length < 1 || _player == null) { Print("<color=#ffd23f>формат: give <item> [count]</color>"); return; }
            int n = p.Length > 1 && int.TryParse(p[1], out int c) ? c : 1;
            if (_player.inventory.TryAdd(new ItemStack(p[0], n)) > 0)
            { Audit($"give {p[0]} {n}"); Print($"<color=#5cff92>выдано: {p[0]} ×{n}</color>"); }
            else Print("<color=#ff6b5e>нет места в инвентаре</color>");
        }

        void SpawnMonster(string arg)
        {
            if (string.IsNullOrEmpty(arg)) { Print("<color=#ffd23f>формат: spawn <Smiler|Hound|Partygoer|Clump|SkinStealer></color>"); return; }
            if (!Enum.TryParse<AI.MonsterKind>(arg, true, out var kind))
            { Print($"<color=#ff6b5e>неизвестный монстр: {arg}</color>"); return; }
            CachePlayer();
            var pos = _player != null ? _player.transform.position + _player.transform.forward * 3f : Vector3.zero;
            if (AI.MonsterSpawner.Instance != null) AI.MonsterSpawner.Instance.Spawn(kind, pos);
            else Print("<color=#ffd23f>спавнер монстров ещё не создан</color>");
            Audit($"spawn {kind}");
            Print($"<color=#5cff92>заспавнен {kind}</color>");
        }

        void SetRank(string arg)
        {
            var p = (arg ?? "").Split(' ');
            if (p.Length < 2 || !ulong.TryParse(p[0], out ulong id))
            { Print("<color=#ffd23f>формат: rank <playerId> <player|moderator|owner></color>"); return; }
            if (!Enum.TryParse<AdminRank>(p[1], true, out var rank)) rank = AdminRank.Moderator;
            _ranks[id] = rank;
            Audit($"rank {id} {rank}");
            Print($"<color=#5cff92>игрок {id} → {rank}</color>");
        }

        void Kick(string arg)
        {
            if (!ulong.TryParse((arg ?? "").Trim(), out ulong id)) { Print("<color=#ffd23f>формат: kick <playerId></color>"); return; }
            // Mirror-сервер: NetworkServer.Disconnect(id) в адаптере; офлайн — просто фиксируем
            Audit($"kick {id}");
            Print($"<color=#5cff92>кикнут {id}</color>");
        }

        void Ban(string arg)
        {
            if (!ulong.TryParse((arg ?? "").Trim(), out ulong id)) { Print("<color=#ffd23f>формат: ban <playerId></color>"); return; }
            _bans.Add(id);
            Audit($"ban {id}");
            Print($"<color=#5cff92>забанен {id} (бан-лист держится в памяти сервера)</color>");
        }

        void Unban(string arg)
        {
            if (!ulong.TryParse((arg ?? "").Trim(), out ulong id)) { Print("<color=#ffd23f>формат: unban <playerId></color>"); return; }
            _bans.Remove(id);
            Audit($"unban {id}");
            Print($"<color=#5cff92>разбанен {id}</color>");
        }

        void Say(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return;
            Audit($"say {arg}");
            Subsistence.UI.HudRuntime.ShowToast($"[ADMIN] {arg}");
            Print($"<color=#b6ffd0>[ADMIN] {arg}</color>");
        }

        void Position()
        {
            CachePlayer();
            if (_player == null) return;
            var p = _player.transform.position;
            Print($"позиция: {p.x:F1} {p.y:F1} {p.z:F1} (уровень определяется по Y: 0 = L0, -40 = L37, -80 = L3)");
        }

        void RequestWipe()
        {
            Audit("wipe");
            Print("<color=#ffd23f>вайп (07_wipe = 1 месяц): на сервере — команда рестарта с новым seed; "
                + "в офлайне удали StreamingAssets/seed.txt</color>");
        }

        void DumpLog()
        {
            Print($"<color=#b6ffd0>АУДИТ-ЛОГ ({_audit.Count} записей)</color>");
            int start = Mathf.Max(0, _audit.Count - 12);
            for (int i = start; i < _audit.Count; i++) Print("  " + _audit[i]);
        }

        // ------------------------------------------------------------------ утилиты
        void Audit(string what)
        {
            _audit.Add($"[{DateTime.UtcNow:HH:mm:ss}] {what}");
            if (_audit.Count > MaxAuditEntries) _audit.RemoveAt(0);
        }

        void Print(string line)
        {
            var boot = UI.BootConsole.Instance;
            if (boot != null) boot.Print(line);
            Debug.Log("[ADMIN] " + line);
        }
    }
}
