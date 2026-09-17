// ============================================================================
//  SUBSISTENCE — Crafting/TechTree.cs
//  Технологическое дерево (Rust: «blueprints»):
//    • unknown-предмет нельзя скрафтить, пока не изучен на столе исследований;
//    • стоимость исследования — в хламе, зависит от редкости и тира лута;
//    • тир исследований привязан к верстаку: T2-предмет требует верстак 2, T3 — верстак 3;
//    • изучение — только сервер; прогресс хранится по игроку (для сети — по Steam-id);
//    • стол исследований показывает СПИСОК: что можно изучить, сколько стоит, что уже знаешь.
//
//  До этого файла: requiresResearch у рецептов был true, а изучать было негде (кроме
//  «предмета в руках» без UI и без привязки к верстакам) → большая часть крафта была
//  недоступна в принципе. Здесь это закрыто.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Subsistence.Core;
using Subsistence.Net;
using Subsistence.UI;

namespace Subsistence.Crafting
{
    /// <summary>Стоимость и условия исследования предмета.</summary>
    public struct ResearchOffer
    {
        public string itemId;
        public string name;
        public int scrap;
        public int benchLevel;
        public LootTier tier;
        public Rarity rarity;
        public bool known;
        public bool affordable;
    }

    public static class TechTree
    {
        static readonly Dictionary<ulong, HashSet<string>> _known = new Dictionary<ulong, HashSet<string>>(64);
        static readonly Dictionary<LootTier, int> _tierMult = new Dictionary<LootTier, int>
        {
            { LootTier.Tier1, 100 }, { LootTier.Tier2, 150 }, { LootTier.Tier3, 250 }
        };

        /// <summary>Цена в хламе: редкость × тир (как в Rust: от 20 за мелочь до 750 за военное).</summary>
        public static int CostOf(string itemId)
        {
            var def = ItemDatabase.Def(itemId);
            if (def == null) return 20;
            int baseCost;
            switch (def.rarity)
            {
                case Rarity.Common: baseCost = 20; break;
                case Rarity.Uncommon: baseCost = 75; break;
                case Rarity.Rare: baseCost = 125; break;
                case Rarity.VeryRare: baseCost = 250; break;
                case Rarity.Military: baseCost = 500; break;
                default: baseCost = 750; break;            // Anomalous
            }
            _tierMult.TryGetValue(def.minTier, out int mult);
            if (mult == 0) mult = 100;
            return Mathf.RoundToInt(baseCost * mult / 100f / 5f) * 5;
        }

        /// <summary>Нужен ли для исследования верстак (T1 → 1, T2 → 2, T3 → 3).</summary>
        public static int BenchLevelFor(string itemId)
        {
            var def = ItemDatabase.Def(itemId);
            if (def == null) return 1;
            switch (def.minTier)
            {
                case LootTier.Tier1: return 1;
                case LootTier.Tier2: return 2;
                default: return 3;
            }
        }

        /// <summary>
        /// Дефолтные чертежи (как в Rust): базовое выживание и инструменты доступны сразу,
        /// всё «военное» и технику надо изучать. Без этого списка 20 стартовых рецептов
        /// (план застройки, топор, верстак 1, печь…) оказались бы за исследованиями, что
        /// ломало бы прогрессию первого часа игры.
        /// </summary>
        public static bool IsDefaultKnown(string itemId)
        {
            var def = ItemDatabase.Def(itemId);
            if (def == null) return true;
            if (def.scrapCost <= 0) return true;        // ресурсы, еда-основа, лут-компоненты
            return DefaultBlueprints.Contains(itemId);
        }

        static readonly HashSet<string> DefaultBlueprints = new HashSet<string>
        {
            // стройка и быт
            "building.planner", "building.twig", "hammer", "cupboard.tool", "sleepingbag", "bed", "woodbox",
            // инструменты первого часа
            "rock", "torch", "hatchet", "pickaxe", "bow.hunting", "arrow.wooden", "spear.wooden",
            "bandage", "water.bottle", "duct.tape",
            // переработка и первый верстак
            "furnace", "workbench1", "lock.key"
        };

        /// <summary>Список изученных чертежей игрока (для сохранения мира).</summary>
        public static List<string> ExportKnown(ulong player)
        {
            var res = new List<string>(64);
            if (_known.TryGetValue(player, out var set))
                foreach (var id in set) res.Add(id);
            return res;
        }

        /// <summary>Вернуть изученное из сохранения (заменяет текущий список игрока).</summary>
        public static void ImportKnown(ulong player, List<string> ids)
        {
            if (ids == null) return;
            if (!_known.TryGetValue(player, out var set)) { set = new HashSet<string>(); _known[player] = set; }
            set.Clear();
            for (int i = 0; i < ids.Count; i++)
                if (!string.IsNullOrEmpty(ids[i])) set.Add(ids[i]);
            Debug.Log($"[TechTree] восстановлено изученного: {set.Count}");
        }

        public static bool IsKnown(ulong player, string itemId)
        {
            if (IsDefaultKnown(itemId)) return true;
            return _known.TryGetValue(player, out var set) && set.Contains(itemId);
        }

        public static void Grant(ulong player, string itemId)
        {
            if (!_known.TryGetValue(player, out var set)) { set = new HashSet<string>(); _known[player] = set; }
            set.Add(itemId);
        }

        public static int KnownCount(ulong player)
        {
            int n = 0;
            foreach (var d in ItemDatabase.All)
                if (IsKnown(player, d.Value.id)) n++;          // All — словарь id→ItemDef
            return n;
        }

        public static int TotalCount()
        {
            int n = 0;
            foreach (var _ in ItemDatabase.All) n++;
            return n;
        }

        /// <summary>Что вообще можно исследовать: у чего есть цена в хламе и что ещё не известно.</summary>
        public static List<ResearchOffer> OffersFor(ulong player, PlayerInventory inv, bool onlyUnknown, bool onlyInInventory)
        {
            var list = new List<ResearchOffer>(64);
            var seen = new HashSet<string>();

            if (onlyInInventory && inv != null)
            {
                for (int i = 0; i < inv.SlotCount; i++)
                {
                    var st = inv.Get(i);
                    if (st == null || st.IsEmpty || !seen.Add(st.id)) continue;
                    var offer = MakeOffer(player, st.id, inv);
                    if (!onlyUnknown || !offer.known) list.Add(offer);
                }
            }
            else
            {
                foreach (var def in ItemDatabase.All)
                {
                    if (!seen.Add(def.Value.id)) continue;
                    var offer = MakeOffer(player, def.Value.id, inv);
                    if (!onlyUnknown || !offer.known) list.Add(offer);
                }
            }

            list.Sort((a, b) =>
            {
                int c = ((int)a.tier).CompareTo((int)b.tier);
                if (c != 0) return c;
                c = a.benchLevel.CompareTo(b.benchLevel);
                if (c != 0) return c;
                return a.scrap.CompareTo(b.scrap);
            });
            return list;
        }

        public static ResearchOffer MakeOffer(ulong player, string itemId, PlayerInventory inv)
        {
            var def = ItemDatabase.Def(itemId);
            int cost = CostOf(itemId);
            bool known = IsKnown(player, itemId);
            bool canPay = inv != null && inv.CountOf("scrap") >= cost;
            return new ResearchOffer
            {
                itemId = itemId,
                name = def != null ? def.Display() : itemId,
                scrap = cost,
                benchLevel = BenchLevelFor(itemId),
                tier = def != null ? def.minTier : LootTier.Tier1,
                rarity = def != null ? def.rarity : Rarity.Common,
                known = known,
                affordable = canPay
            };
        }

        /// <summary>
        /// Изучить предмет: сервер проверяет хлам, близость верстака нужного тира и «уже изучено».
        /// Возвращает текст причины отказа или null при успехе.
        /// </summary>
        public static string TryResearch(ulong player, PlayerInventory inv, string itemId, int nearbyBenchLevel)
        {
            if (inv == null) return "Нет инвентаря";
            if (string.IsNullOrEmpty(itemId)) return "Предмет не выбран";
            if (IsKnown(player, itemId)) return "Уже изучено";

            int bench = BenchLevelFor(itemId);
            if (nearbyBenchLevel < bench)
                return $"Нужен верстак {bench} уровня рядом (изучать можно только у верстака)";

            int cost = CostOf(itemId);
            if (inv.CountOf("scrap") < cost)
                return $"Нужно {cost} хлама (есть {inv.CountOf("scrap")})";

            if (!NetworkBridge.IsServer) return "Изучение делает сервер";

            inv.RemoveAmount("scrap", cost);
            Grant(player, itemId);
            Subsistence.Audio.AudioDirector.Play2D("research_done", 0.6f);
            HudRuntime.ShowToast($"Изучено: {ItemDatabase.NameOf(itemId)} (хлам −{cost}) · открыто {KnownCount(player)}/{TotalCount()}");
            Subsistence.AI.NoiseSystem.Emit(Vector3.zero, 0f, Subsistence.AI.NoiseType.ItemDrop);  // тихий маркер
            if (ResearchUI.Instance != null) ResearchUI.Instance.Refresh();
            return null;
        }
    }

    /// <summary>Стол исследований: список того, что можно изучить (хлам, тир верстака, прогресс).</summary>
    public class ResearchUI : MonoBehaviour
    {
        public static ResearchUI Instance { get; private set; }

        Canvas _canvas;
        RectTransform _panel;
        Text _list, _status;
        PlayerInventory _inv;
        ulong _player;
        int _benchLevel = 1;
        readonly List<ResearchOffer> _offers = new List<ResearchOffer>(64);

        void Awake()
        {
            Instance = this;
            BuildUI();
            _panel.gameObject.SetActive(false);
        }

        void BuildUI()
        {
            _canvas = UIStyle.CreateCanvas("ResearchCanvas", 132, out _);
            _panel = UIStyle.Panel(_canvas.transform, "Research", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   new Vector2(-520, -390), new Vector2(520, 390), UIStyle.PanelBg);
            UIStyle.Label(_panel, "СТОЛ ИССЛЕДОВАНИЙ", 30, UIStyle.BackroomsYellow);
            _status = UIStyle.Label(_panel, "", 17, UIStyle.TermGreenDim, TextAnchor.UpperLeft);
            _status.rectTransform.offsetMin = new Vector2(24, 660);
            _status.rectTransform.sizeDelta = new Vector2(960, 60);
            _list = UIStyle.Label(_panel, "", 18, UIStyle.TermGreen, TextAnchor.UpperLeft);
            _list.rectTransform.offsetMin = new Vector2(24, 110);
            UIStyle.Btn(_panel, "ИЗУЧИТЬ (1-9)", new Vector2(24, 24), new Vector2(300, 54), () => { });
            UIStyle.Btn(_panel, "ЗАКРЫТЬ (Esc)", new Vector2(696, -24), new Vector2(280, 50), () => Close());
        }

        public static void Open(PlayerInventory inv, ulong player, int nearbyBenchLevel)
        {
            if (Instance == null) return;
            Instance._inv = inv; Instance._player = player;
            Instance._benchLevel = Mathf.Max(1, nearbyBenchLevel);
            Instance._panel.gameObject.SetActive(true);
            UIState.AnyMenuOpen = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Instance.Refresh();
        }

        public void Close()
        {
            _panel.gameObject.SetActive(false);
            UIState.AnyMenuOpen = false;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        public void Refresh()
        {
            _offers.Clear();
            _offers.AddRange(TechTree.OffersFor(_player, _inv, true, true));

            var sb = new System.Text.StringBuilder(2048);
            int shown = 0;
            for (int i = 0; i < _offers.Count && shown < 14; i++)
            {
                var o = _offers[i];
                bool benchOk = _benchLevel >= o.benchLevel;
                string color = o.affordable && benchOk ? "#ffffff" : "#8a8a8a";
                string benchMark = benchOk ? "" : $" <color=#ff6b5e>нужен верстак {o.benchLevel}</color>";
                sb.Append($"<color={color}>[{shown + 1}] {o.name}  —  {o.scrap} хлама{benchMark}</color>\n");
                shown++;
            }
            if (shown == 0)
                sb.Append("<color=#8a8a8a>Нечего изучать: поднеси предметы, которых ещё нет в твоих рецептах,\n" +
                          "или проверь прогресс справа.</color>\n");
            _list.text = sb.ToString();

            int known = TechTree.KnownCount(_player);
            _status.text = $"Изучено: {known}/{TechTree.TotalCount()}    ·    хлам: {(_inv != null ? _inv.CountOf("scrap") : 0)}    ·    " +
                           $"верстак рядом: {_benchLevel} уровня\n" +
                           "<size=14>Цена в хламе: редкость × тир. T2-предметы требуют верстак 2, T3 — верстак 3.\n" +
                           "Предметы без цены в хламе (ресурсы, еда-основа) доступны сразу — их изучать не нужно.</size>";
        }

        void Update()
        {
            if (_panel == null || !_panel.gameObject.activeSelf) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }

            for (int i = 0; i < 9 && i < _offers.Count; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
                var o = _offers[i];
                string err = TechTree.TryResearch(_player, _inv, o.itemId, _benchLevel);
                if (err != null) HudRuntime.ShowToast(err);
                Refresh();
                break;
            }
        }
    }
}
