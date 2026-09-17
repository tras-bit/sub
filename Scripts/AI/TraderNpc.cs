// ============================================================================
//  SUBSISTENCE — AI/TraderNpc.cs
//  Торговля и безопасные комнаты (решение 39_trading: «вендинг-автоматы + обмен
//  + NPC-торговец в безопасной комнате»).
//   • SafeRoom       — зона без PvP и без спавна монстров
//   • TraderNpc      — NPC-торговец: прилавок-обмен (скрап ↔ предметы)
//   • VendingMachine — автомат: фиксированные цены за скрап
//   • TradeStall     — раскладка торговых точек по уровням
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Building;

namespace Subsistence.AI
{
    /// <summary>Безопасная комната: тут не стреляют по своим и не спавнятся монстры.</summary>
    public class SafeRoom : MonoBehaviour
    {
        public float radius = 16f;
        public string title = "Безопасная комната";

        static readonly List<SafeRoom> _all = new List<SafeRoom>();

        void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        void OnDisable() { _all.Remove(this); }

        /// <summary>Точка внутри безопасной зоны?</summary>
        public static bool IsSafe(Vector3 p)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                var r = _all[i];
                if (r == null) continue;
                if ((r.transform.position - p).sqrMagnitude <= r.radius * r.radius) return true;
            }
            return false;
        }

        /// <summary>Блокировать ли урон по игроку (в безопасной зоне PvP выключен).</summary>
        public static bool BlocksPvP(Transform victim) => victim != null && IsSafe(victim.position);

        public static bool AnyInside(Vector3 p) => IsSafe(p);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.25f);
            Gizmos.DrawSphere(transform.position, radius);
        }
    }

    /// <summary>NPC-торговец: держит «прилавок» (6 слотов) — положил скрап, забрал товар.</summary>
    public class TraderNpc : BuildDeployable
    {
        public string traderName = "Торговец Пит";
        public ItemContainer shelf;
        public LevelTheme homeLevel = LevelTheme.Corridors;

        // Курс обмена: сколько скрапа стоит каждая позиция (39_trading)
        public static readonly Dictionary<string, int> Prices = new Dictionary<string, int>
        {
            { "almond.water", 8 },
            { "bandage", 5 },
            { "syringe.medical", 30 },
            { "can.beans", 6 },
            { "water.bottle", 4 },
            { "hazmatsuit", 220 },
            { "metal.facemask", 260 },
            { "tool.keycard.green", 90 },
            { "tool.keycard.blue", 180 },
            { "nightvision", 300 },
        };

        void Awake()
        {
            itemId = string.IsNullOrEmpty(itemId) ? "trader.npc" : itemId;
            requiresPrivilege = false;
            health = float.MaxValue;                 // торговца не убить — он часть экономики
            shelf = new ItemContainer(traderName, 6);
        }

        public override void ApplyDamage(DamageType type, float amount, Vector3 from, HitRegion region) { }

        public override void OnUse(Subsistence.Player.PlayerController player)
        {
            // 38_skins: присел у торговца — вместо прилавка открывается терминал скинов.
            // (Магазин закрытый: только наши скины, никаких модов и мастерской.)
            if (Subsistence.Player.PlayerControllerState.IsCrouching)
            {
                Subsistence.Progression.SkinShopUI.Open();
                return;
            }

            StockShelf();
            Subsistence.UI.HudRuntime.ShowToast($"{traderName}: «Скрап в прилавок — товар забирай» (курс: 1 позиция = 4–300 скрапа). Присядь — покажу витрину скинов");
            Subsistence.UI.InventoryUI.OpenContainer(shelf);
        }

        /// <summary>Автоматическая выкладка товара в прилавок (обновляется при каждом обращении).</summary>
        void StockShelf()
        {
            int i = 0;
            foreach (var kv in Prices)
            {
                if (i >= shelf.Slots.Length) break;
                var def = ItemDatabase.Def(kv.Key);
                if (def == null) continue;
                // если позиция уже лежит — не дублируем
                bool has = false;
                for (int s = 0; s < shelf.Slots.Length; s++)
                {
                    var st = shelf.Get(s);
                    if (st != null && st.id == kv.Key) { has = true; break; }
                }
                if (!has) shelf.Set(i, new ItemStack(kv.Key, 1));
                i++;
            }
        }

        /// <summary>Купить позицию по индексу (вызывается из UI торговли; в игре пока вендинг).</summary>
        public static bool TryBuy(Subsistence.Core.PlayerInventory inv, string itemId, int count = 1)
        {
            if (inv == null || !Prices.TryGetValue(itemId, out int price)) return false;
            int total = price * count;
            if (inv.CountOf("scrap") < total)
            {
                Subsistence.UI.HudRuntime.ShowToast($"Нужно {total} скрапа (нет)");
                return false;
            }
            inv.RemoveAmount("scrap", total);
            inv.TryAdd(new ItemStack(itemId, count));
            Subsistence.UI.HudRuntime.ShowToast($"Куплено: {itemId} ×{count} за {total} скрапа");
            return true;
        }
    }

    /// <summary>Вендинг-автомат: продаёт за скрап, ничего не требует взамен (кроме оплаты).</summary>
    public class VendingMachine : BuildDeployable
    {
        public string[] offers = new string[] { "almond.water", "bandage", "can.beans" };
        public float restartDelay = 2f;
        float _ready;

        void Awake()
        {
            itemId = string.IsNullOrEmpty(itemId) ? "vending.machine" : itemId;
            requiresPrivilege = false;
            health = 600f;
        }

        public override void OnUse(Subsistence.Player.PlayerController player)
        {
            if (Time.time < _ready)
            {
                Subsistence.UI.HudRuntime.ShowToast("Автомат перезагружается...");
                return;
            }
            _ready = Time.time + restartDelay;

            string list = "Автомат: " + string.Join(" · ", System.Array.ConvertAll(offers,
                o => $"{o} — {(TraderNpc.Prices.TryGetValue(o, out int p) ? p : 0)}ск"));
            Subsistence.UI.HudRuntime.ShowToast(list);

            // покупаем первый доступный по цене товар
            for (int i = 0; i < offers.Length; i++)
                if (TraderNpc.TryBuy(player.inventory, offers[i])) return;
        }
    }

    /// <summary>Расставляет торговые точки и безопасные комнаты по уровням.</summary>
    public static class TradeStall
    {
        public static void Populate(List<World.GeneratedLevel> levels)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                var lvl = levels[i];
                if (lvl.playerSpawns.Count == 0) continue;

                var center = lvl.playerSpawns[0];
                CreateSafeRoom(center + new Vector3(6f, 0f, 6f), lvl.theme);
                CreateTrader(center + new Vector3(6f, 0f, 9f), lvl.theme);
                CreateVending(center + new Vector3(3f, 0f, 9f));
            }
        }

        static void CreateSafeRoom(Vector3 pos, LevelTheme theme)
        {
            var go = new GameObject("SafeRoom");
            go.transform.position = new Vector3(pos.x, pos.y + 0.05f, pos.z);
            var sr = go.AddComponent<SafeRoom>();
            sr.radius = 18f;
            sr.title = theme == LevelTheme.Corridors ? "Убежище L0" : theme == LevelTheme.Poolrooms ? "Сушилка L37" : "Диспетчерская L3";

            // визуальная разметка зоны: зелёный свет по периметру
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 2f / 8f;
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lamp.name = "SafeLamp";
                lamp.transform.SetParent(go.transform, false);
                lamp.transform.localPosition = new Vector3(Mathf.Cos(a) * sr.radius, 2.9f, Mathf.Sin(a) * sr.radius);
                lamp.transform.localScale = new Vector3(0.5f, 0.08f, 0.5f);
                Object.Destroy(lamp.GetComponent<Collider>());
            }
        }

        static void CreateTrader(Vector3 pos, LevelTheme theme)
        {
            var go = new GameObject("TraderNpc");
            go.transform.position = new Vector3(pos.x, pos.y + 0.1f, pos.z);
            var trader = go.AddComponent<TraderNpc>();
            trader.homeLevel = theme;
            trader.traderName = theme == LevelTheme.Corridors ? "Торговец Пит"
                              : theme == LevelTheme.Poolrooms ? "Смотритель бассейнов"
                              : "Дежурный инженер";

            // Тело торговца: CH_trader_npc из Blender (кепка, респиратор, разгрузка, планшет),
            // иначе — капсула-заглушка. Коллайдер-прокси нужен, чтобы работало «E» (разговор).
            var proxy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            proxy.name = "Body";
            proxy.transform.SetParent(go.transform, false);
            proxy.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            proxy.layer = Layers.Deployable;

            var visual = World.ModelLibrary.AttachFitted(World.ModelLibrary.Names.Trader, go.transform, 1.82f, 180f);
            if (visual != null) Object.Destroy(proxy.GetComponent<MeshRenderer>());
        }

        static void CreateVending(Vector3 pos)
        {
            var go = new GameObject("VendingMachine");
            go.transform.position = new Vector3(pos.x, pos.y + 0.1f, pos.z);
            go.AddComponent<VendingMachine>();
            go.layer = Layers.Deployable;

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Body";
            box.transform.SetParent(go.transform, false);
            box.transform.localScale = new Vector3(0.9f, 1.9f, 0.7f);
            box.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            box.layer = Layers.Deployable;

            // PR_vending_machine: корпус со стеклом, полки с банками, инфо-таблица
            var visual = World.ModelLibrary.AttachFitted(World.ModelLibrary.Names.Vending, go.transform, 1.92f, 0f);
            if (visual != null)
            {
                var r = box.GetComponent<MeshRenderer>();
                if (r != null) r.enabled = false;
            }
        }
    }
}
