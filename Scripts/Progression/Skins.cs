// ============================================================================
//  SUBSISTENCE — Progression/Skins.cs
//  Решение 38_skins: «магазин скинов есть» (игра закрытая, модов нет — только
//  то, что нарисовали мы; никаких Steam Workshop и плагинов).
//
//  Что тут:
//   • SkinCatalog — каталог скинов: предмет, цена в скрапе (и/или торговых
//     жетонах trade.token), цвет и «легенда». Владение и выбор хранятся в
//     PlayerPrefs (клиентская косметика, серверу всё равно);
//   • SkinShop    — покупка: списывает скрап/жетоны из инвентаря игрока;
//   • SkinShopUI  — терминальное меню в стиле зелёной консоли игры
//     (открывается командой SKINS в консоли или сидя у торговца).
//
//  Как скин попадает на предмет: у ItemStack есть поле skinId, и любой
//  созданный предмет автоматически получает скин, выбранный для его типа
//  (см. хук в Core/Items.cs). Дальше World/ModelLibrary.Tint красит меш —
//  то есть скины реально видно на модели, а не только в инвентаре.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Subsistence.Core;
using Subsistence.UI;

namespace Subsistence.Progression
{
    [Serializable]
    public class SkinDef
    {
        public string id;
        public string name;        // как это читает игрок
        public string itemId;      // к какому предмету применяется
        public int scrapPrice;     // цена в скрапе
        public int tokenPrice;     // цена в жетонах (trade.token), 0 = только скрап
        public Color color;        // цвет меша (ModelLibrary.Tint)
        public string note;        // строчка в магазине
    }

    public static class SkinCatalog
    {
        const string OwnedKey = "subsistence.skins.owned";
        const string EquipPrefix = "subsistence.skins.equipped.";

        static List<SkinDef> _all;

        public static IReadOnlyList<SkinDef> All
        {
            get { Init(); return _all; }
        }

        static void Init()
        {
            if (_all != null) return;
            _all = new List<SkinDef>(16)
            {
                // ---------- оружие ----------
                S("ak.asbestos",   "АК «Асбест»",        "rifle.ak",   90, 0, 0.78f, 0.74f, 0.66f, "выцветшая краска коридоров"),
                S("ak.pooltile",   "АК «Плитка»",        "rifle.ak",  130, 1, 0.45f, 0.62f, 0.60f, "хлор и кафель, следы воды"),
                S("ak.reactor",    "АК «Изотоп»",        "rifle.ak",  180, 3, 0.34f, 0.72f, 0.44f, "светится в темноте, чуть-чуть"),
                S("smg.mp5.damp",  "MP5 «Сырость»",      "smg.mp5",   110, 1, 0.40f, 0.46f, 0.44f, "промокший металл бассейнов"),
                S("m249.turbine",  "M249 «Турбина»",     "lmg.m249",  240, 4, 0.52f, 0.48f, 0.42f, "обшивка третьего уровня"),
                S("pump.quarantine", "Помпа «Карантин»", "shotgun.pump", 120, 2, 0.86f, 0.80f, 0.34f, "жёлтая лента и предупреждения"),

                // ---------- броня ----------
                S("hazmat.clean",  "Хазмат «Стерильный»", "hazmatsuit", 150, 2, 0.92f, 0.90f, 0.80f, "новый костюм, ещё пахнет резиной"),
                S("hazmat.blood",  "Хазмат «Не мой»",     "hazmatsuit", 200, 3, 0.58f, 0.30f, 0.28f, "кто-то его уже носил. и остался тут"),
                S("plate.oxidized", "Броня «Окисел»",     "metal.plate.torso", 210, 3, 0.36f, 0.44f, 0.46f, "зелёная коррозия реактора"),
                S("exo.servo",     "Экзо «Серво»",        "exoskeleton.suit", 300, 6, 0.30f, 0.34f, 0.44f, "гудит, но держит радиацию"),

                // ---------- инструмент ----------
                S("hatchet.rust",  "Топор «Ржавый»",     "hatchet",    70, 0, 0.52f, 0.36f, 0.26f, "нашёл в подсобке, ей лет двадцать"),
                S("hammer.wallpaper", "Молоток «Обои»",  "hammer",     80, 0, 0.83f, 0.76f, 0.42f, "жёлтый, как всё вокруг"),

                // ---------- транспорт (для будущих моделей) ----------
                S("cart.tagged",   "Тележка «Меченая»",  "cart.loot", 140, 2, 0.66f, 0.52f, 0.28f, "краска, бирки, чужая фамилия"),
                S("scooter.neon",  "Самокат «Неон»",     "scooter",   170, 3, 0.36f, 0.86f, 0.62f, "видно в тумане за квартал")
            };
        }

        static SkinDef S(string id, string name, string itemId, int scrap, int tokens, float r, float g, float b, string note)
        {
            return new SkinDef { id = id, name = name, itemId = itemId, scrapPrice = scrap, tokenPrice = tokens, color = new Color(r, g, b), note = note };
        }

        public static SkinDef Get(string id)
        {
            Init();
            for (int i = 0; i < _all.Count; i++) if (_all[i].id == id) return _all[i];
            return null;
        }

        public static List<SkinDef> ForItem(string itemId)
        {
            Init();
            var list = new List<SkinDef>(4);
            for (int i = 0; i < _all.Count; i++) if (_all[i].itemId == itemId) list.Add(_all[i]);
            return list;
        }

        public static Color TintFor(string skinId)
        {
            var s = Get(skinId);
            return s != null ? s.color : Color.white;
        }

        // ---------- владение ----------
        public static bool IsOwned(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var csv = PlayerPrefs.GetString(OwnedKey, string.Empty);
            return ("," + csv + ",").Contains("," + id + ",");
        }

        public static void Unlock(string id)
        {
            if (string.IsNullOrEmpty(id) || IsOwned(id)) return;
            var csv = PlayerPrefs.GetString(OwnedKey, string.Empty);
            PlayerPrefs.SetString(OwnedKey, string.IsNullOrEmpty(csv) ? id : csv + "," + id);
            PlayerPrefs.Save();
        }

        public static int OwnedCount()
        {
            Init();
            int n = 0;
            for (int i = 0; i < _all.Count; i++) if (IsOwned(_all[i].id)) n++;
            return n;
        }

        // ---------- экипировка ----------
        public static string Equipped(string itemId) => string.IsNullOrEmpty(itemId) ? string.Empty : PlayerPrefs.GetString(EquipPrefix + itemId, string.Empty);

        public static void Equip(SkinDef skin)
        {
            if (skin == null) return;
            PlayerPrefs.SetString(EquipPrefix + skin.itemId, skin.id);
            PlayerPrefs.Save();
        }

        public static void Unequip(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            PlayerPrefs.DeleteKey(EquipPrefix + itemId);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Проставляет скин предмету. Вызывается при создании любого ItemStack
        /// (см. Core/Items.cs): скин «прилипает» к вещам игрока, как в Rust.
        /// </summary>
        public static string SkinForItem(string itemId)
        {
            string id = Equipped(itemId);
            if (string.IsNullOrEmpty(id)) return string.Empty;
            return IsOwned(id) ? id : string.Empty;   // скины, которых нет, не надеваем
        }
    }

    /// <summary>Покупка скинов: списание скрапа/жетонов из инвентаря игрока.</summary>
    public static class SkinShop
    {
        public static bool TryBuy(PlayerInventory inv, SkinDef skin, out string message)
        {
            message = string.Empty;
            if (inv == null || skin == null) { message = "нет инвентаря"; return false; }
            if (SkinCatalog.IsOwned(skin.id)) { message = "уже куплено"; return false; }

            var cost = new List<ItemStack>(2);
            if (skin.scrapPrice > 0) cost.Add(new ItemStack("scrap", skin.scrapPrice));
            if (skin.tokenPrice > 0) cost.Add(new ItemStack("trade.token", skin.tokenPrice));

            if (!inv.CanAfford(cost))
            {
                message = $"нужно: {Describe(cost)}";
                return false;
            }
            if (!inv.Pay(cost))
            {
                message = "не удалось списать";
                return false;
            }

            SkinCatalog.Unlock(skin.id);
            SkinCatalog.Equip(skin);          // купил — сразу надел
            message = $"{skin.name} — куплено и надето";
            return true;
        }

        public static string Describe(IList<ItemStack> cost)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cost.Count; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(ItemDatabase.NameOf(cost[i].id, true)).Append(' ').Append(cost[i].amount);
            }
            return sb.ToString();
        }
    }

    /// <summary>Терминальное меню магазина скинов (тот же зелёный стиль, что консоль загрузки).</summary>
    public class SkinShopUI : MonoBehaviour
    {
        public static SkinShopUI Instance { get; private set; }

        Text _header, _list, _footer;
        bool _open;

        public static void Open()
        {
            if (Instance == null)
            {
                var go = new GameObject("SkinShopUI");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<SkinShopUI>();
            }
            Instance.Show(true);
        }

        public static void Close() { if (Instance != null) Instance.Show(false); }
        public static bool IsOpen => Instance != null && Instance._open;

        void Show(bool open)
        {
            _open = open;
            if (open && _header == null) Build();
            if (_header != null) _header.transform.root.gameObject.SetActive(open);

            UIState.AnyMenuOpen = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            if (open) Refresh();
            else if (Subsistence.Runtime.RuntimeBootstrap.Instance != null)
            { /* игра сама вернёт лок камеры при движении */ }
        }

        void Build()
        {
            CanvasScaler scaler;
            var canvas = UIStyle.CreateCanvas("SkinShop", 60, out scaler);

            UIStyle.Panel(canvas.transform, "Bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.01f, 0.05f, 0.03f, 0.96f));

            var frame = UIStyle.Panel(canvas.transform, "Frame", new Vector2(0.12f, 0.10f), new Vector2(0.88f, 0.90f), Vector2.zero, Vector2.zero, UIStyle.PanelBg);
            frame.gameObject.AddComponent<Outline>().effectColor = UIStyle.TermGreenDim;

            _header = UIStyle.Label(frame, Header(), 24, UIStyle.TermGreenBright, TextAnchor.UpperLeft);
            _header.rectTransform.offsetMin = new Vector2(24f, 0f);
            _header.rectTransform.offsetMax = new Vector2(-24f, -18f);
            _header.rectTransform.anchorMin = new Vector2(0f, 0.86f);

            _list = UIStyle.Label(frame, string.Empty, 19, UIStyle.TermGreen, TextAnchor.UpperLeft);
            _list.rectTransform.offsetMin = new Vector2(24f, 54f);
            _list.rectTransform.offsetMax = new Vector2(-24f, -70f);
            _list.rectTransform.anchorMin = new Vector2(0f, 0.06f);
            _list.rectTransform.anchorMax = new Vector2(1f, 0.85f);

            _footer = UIStyle.Label(frame, "1..9 — купить/надеть · 0 — снять скин · ESC — закрыть", 17, UIStyle.TermGreenDim, TextAnchor.LowerLeft);
            _footer.rectTransform.anchorMin = new Vector2(0f, 0f);
            _footer.rectTransform.anchorMax = new Vector2(1f, 0.08f);
            _footer.rectTransform.offsetMin = new Vector2(24f, 10f);
        }

        static string Header()
        {
            return "SKIN TERMINAL — RETAIL 04\n" +
                   "<color=#5cff92>магазин скинов subsistence // модов нет, только наши</color>\n" +
                   "оплата: скрап (scrap) и торговые жетоны (trade.token)";
        }

        void Refresh()
        {
            if (_list == null) return;
            var inv = PlayerRef()?.inventory;
            var sb = new System.Text.StringBuilder();

            var skins = SkinCatalog.All;
            for (int i = 0; i < skins.Count; i++)
            {
                var s = skins[i];
                bool owned = SkinCatalog.IsOwned(s.id);
                bool equipped = SkinCatalog.Equipped(s.itemId) == s.id;

                sb.Append(i < 9 ? $"[{i + 1}] " : "[•] ");
                sb.Append(owned ? "<color=#8ef0b4>" : "<color=#b6ffd0>").Append(s.name.PadRight(26)).Append("</color>");
                sb.Append(ItemDatabase.NameOf(s.itemId, true).PadRight(18));
                if (owned) sb.Append(equipped ? "<color=#5cff92>[НАДЕТО]</color>" : "<color=#3f7d58>[куплено]</color>");
                else sb.Append($"<color=#ffd76a>{Price(s)}</color>");
                sb.Append("   <color=#3f7d58>").Append(s.note).Append("</color>\n");
            }

            int owned2 = SkinCatalog.OwnedCount();
            sb.Append('\n').Append($"<color=#5cff92>скинов открыто: {owned2} / {skins.Count}</color>");
            if (inv != null)
                sb.Append($"   <color=#8ef0b4>скрап: {inv.CountOf("scrap")} · жетоны: {inv.CountOf("trade.token")} · годных: {skin_hint(inv)}</color>");
            _list.text = sb.ToString();
        }

        static string Price(SkinDef s)
        {
            if (s.tokenPrice <= 0) return $"{s.scrapPrice} скрап";
            if (s.scrapPrice <= 0) return $"{s.tokenPrice} жетон(ов)";
            return $"{s.scrapPrice} скрап + {s.tokenPrice} жетон(ов)";
        }

        static string skin_hint(PlayerInventory inv)
        {
            return inv.ActiveItem != null && !inv.ActiveItem.IsEmpty ? ItemDatabase.NameOf(inv.ActiveItem.id, true) : "нет в руках";
        }

        static Subsistence.Player.PlayerController PlayerRef()
        {
            var boot = Subsistence.Runtime.RuntimeBootstrap.Instance;
            return boot != null ? boot.Player : null;
        }

        void Update()
        {
            if (!_open) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Show(false); return; }

            int index = -1;
            for (int i = 0; i < 9 && index < 0; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) index = i;

            if (index >= 0 && index < SkinCatalog.All.Count)
            {
                var skin = SkinCatalog.All[index];
                string msg;
                var inv = PlayerRef()?.inventory;

                if (SkinCatalog.IsOwned(skin.id)) { SkinCatalog.Equip(skin); msg = $"{skin.name} — надето"; }
                else if (!SkinShop.TryBuy(inv, skin, out msg)) { /* msg уже собран */ }

                Refresh();
                if (_footer != null) _footer.text = msg + "   ·   ESC — закрыть";
            }

            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                var inv = PlayerRef()?.inventory;
                var item = inv != null ? inv.ActiveItem : null;
                if (item != null && !item.IsEmpty) { SkinCatalog.Unequip(item.id); if (_footer != null) _footer.text = $"скин снят с {ItemDatabase.NameOf(item.id, true)}"; }
                Refresh();
            }
        }
    }
}
