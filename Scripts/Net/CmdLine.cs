// ============================================================================
//  SUBSISTENCE — Net/CmdLine.cs
//  Разбор аргументов командной строки: headless-сервер, клиент и стенд нагрузки.
//  Нужен для прогона «100+» на реальном железе (см. docs/MULTIPLAYER.md).
//
//      Subsistence.exe --server --port 7777            — сервер (в т.ч. -batchmode -nographics)
//      Subsistence.exe --client --host 10.0.0.5        — клиент
//      Subsistence.exe --load-bots 112                 — стенд: 112 серверных ботов (замер AOI/трафика)
//      Subsistence.exe -batchmode -nographics --server --load-bots 112
//
//  Формы записи: --key value, --key=value, -key value.
// ============================================================================
using UnityEngine;
using Subsistence.Core;

namespace Subsistence.Net
{
    public static class CmdLine
    {
        public static bool Server { get; private set; }
        public static bool Client { get; private set; }
        public static bool Headless { get; private set; }
        public static string Host { get; private set; } = "127.0.0.1";
        public static ushort Port { get; private set; } = 7777;
        public static int LoadBots { get; private set; }
        public static string Name { get; private set; } = "";
        public static bool HasAny { get; private set; }

        static bool _parsed;
        public static string[] Raw { get; private set; } = System.Array.Empty<string>();

        public static void Parse(string[] args)
        {
            if (_parsed) return;
            _parsed = true;
            Raw = args ?? System.Array.Empty<string>();
            for (int i = 0; i < Raw.Length; i++)
            {
                string a = Raw[i] ?? "";
                if (!a.StartsWith("-")) continue;
                string key = a.TrimStart('-');
                string val = null;
                int eq = key.IndexOf('=');
                if (eq >= 0) { val = key.Substring(eq + 1); key = key.Substring(0, eq); }
                key = key.ToLowerInvariant();

                if (val == null && i + 1 < Raw.Length && !Raw[i + 1].StartsWith("-")) { val = Raw[i + 1]; i++; }

                switch (key)
                {
                    case "server": case "host-server": Server = true; HasAny = true; break;
                    case "client": Client = true; HasAny = true; break;
                    case "batchmode": case "nographics": case "headless": Headless = true; HasAny = true; break;
                    case "host": case "address": case "ip": if (!string.IsNullOrEmpty(val)) Host = val; HasAny = true; break;
                    case "port": if (ushort.TryParse(val, out ushort p)) Port = p; HasAny = true; break;
                    case "load-bots": case "loadbots": case "bots":
                        if (int.TryParse(val, out int n)) LoadBots = Mathf.Clamp(n, 1, Balance.MaxPlayerSlots);
                        HasAny = true; break;
                    case "name": case "nick": if (!string.IsNullOrEmpty(val)) Name = val; break;
                }
            }
            if (Application.isBatchMode) Headless = true;
        }

        /// <summary>Строка для зелёной консоли: что за режим подняли.</summary>
        public static string Describe()
        {
            if (!HasAny) return "обычный запуск (меню)";
            var sb = new System.Text.StringBuilder();
            if (Headless) sb.Append("headless, ");
            if (Server) sb.Append($"сервер {Port}, ");
            if (Client) sb.Append($"клиент → {Host}:{Port}, ");
            if (LoadBots > 0) sb.Append($"стенд {LoadBots} ботов, ");
            if (sb.Length == 0) return "обычный запуск (меню)";
            sb.Length -= 2;
            return sb.ToString();
        }
    }
}
