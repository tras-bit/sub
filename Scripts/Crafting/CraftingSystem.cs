// ============================================================================
//  SUBSISTENCE — Crafting/CraftingSystem.cs
//  Крафт как в Rust: рецепты с ресурсами, гейт по верстаку (1/2/3), время крафта,
//  очередь, изучение через стол исследований, «крафт по 1 / по 10 / до конца».
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Subsistence.Core;
using Subsistence.Net;
using Subsistence.UI;

namespace Subsistence.Crafting
{
    [Serializable]
    public class Recipe
    {
        public string resultId;
        public int amount = 1;
        public int benchLevel;                  // 0 = можно в руках
        public float craftSeconds = 3f;
        public List<ItemStack> ingredients = new List<ItemStack>();
        public bool requiresResearch = true;

        public bool CanCraft(PlayerInventory inv) => inv != null && inv.CanAfford(ingredients);
    }

    public static class RecipeBook
    {
        static readonly Dictionary<string, Recipe> _byResult = new Dictionary<string, Recipe>(256);

        public static IEnumerable<Recipe> All => _byResult.Values;
        public static Recipe Get(string resultId) => _byResult.TryGetValue(resultId, out var r) ? r : null;

        static void R(string result, int amount, int bench, float seconds, params (string id, int n)[] ing)
        {
            var r = new Recipe { resultId = result, amount = amount, benchLevel = bench, craftSeconds = seconds };
            foreach (var i in ing) r.ingredients.Add(new ItemStack(i.id, i.n));
            _byResult[result] = r;
        }

        static RecipeBook()
        {
            // ---- 26_transport (самокаты и тележки) и 39_trading (вендинг) ----
            R("cart.loot", 1, 1, 20f, ("wood", 250), ("metal.fragments", 60), ("cloth", 20));
            R("scooter", 1, 1, 15f, ("metal.fragments", 120), ("cloth", 10), ("scrap", 20));
            R("vending.machine", 1, 2, 30f, ("metal.fragments", 250), ("scrap", 60), ("hqm", 2));
            // ---------- базовое (в руках) ----------
            R("rock", 1, 0, 1f, ("stones", 10));
            R("torch", 1, 0, 1f, ("wood", 5), ("cloth", 2));
            R("building.planner", 1, 0, 1f, ("wood", 20));
            R("bandage", 1, 0, 1.5f, ("cloth", 10));
            R("spear.wooden", 1, 0, 2f, ("wood", 30));
            R("bow.hunting", 1, 0, 3f, ("wood", 100), ("cloth", 20));
            R("arrow.wooden", 3, 0, 2f, ("wood", 15), ("cloth", 2));
            R("woodbox", 1, 0, 5f, ("wood", 100));
            R("sleepingbag", 1, 0, 3f, ("cloth", 30));
            R("cupboard.tool", 1, 0, 5f, ("wood", 100), ("cloth", 20));
            // ---- свои рецепты уровней (v0.2) ----
            // Чистая вода теперь через хлорку (предмет уровня L37) — металл бутылку не стерилизует
            R("water.bottle", 2, 1, 6f, ("water.dirty", 2), ("chlorine", 1));
            R("duct.tape", 2, 1, 4f, ("cloth", 20));
            R("diving.mask", 1, 2, 18f, ("cloth", 50), ("duct.tape", 2), ("metal.fragments", 20));
            R("rubber.gloves", 1, 3, 25f, ("cloth", 40), ("duct.tape", 3), ("metal.refined", 5));
            // ---- v1.0 alpha: рецепты ВСЕМ своим предметам уровней (ответ 11а — по 4 на уровень) ----
            // L0 «Жёлтые коридоры»: свет и защита дыхания из того, что валяется в коридорах
            R("lamp.portable", 1, 1, 12f, ("metal.fragments", 40), ("duct.tape", 1), ("lowgradefuel", 20), ("cloth", 10));
            R("respirator", 1, 1, 15f, ("cloth", 30), ("duct.tape", 2), ("metal.fragments", 10));
            // L37 «Бассейны»: нырять и обеззараживать
            R("chlorine", 2, 2, 10f, ("pool.water", 2), ("metal.fragments", 10));
            R("flippers", 1, 2, 20f, ("cloth", 30), ("duct.tape", 3), ("metal.fragments", 25));
            R("oxygen.tank", 1, 2, 30f, ("metal.fragments", 150), ("metal.refined", 8), ("duct.tape", 2));
            // L3 «Электростанция»: изоляция и силовая часть
            R("fuse.hi", 1, 3, 20f, ("metal.fragments", 60), ("metal.refined", 5));
            R("boots.rubber", 1, 3, 25f, ("cloth", 40), ("duct.tape", 4), ("metal.fragments", 30));
            R("wrench.insulated", 1, 3, 30f, ("metal.fragments", 100), ("metal.refined", 10), ("duct.tape", 3));

            // ---------- верстак 1 ----------
            R("hatchet", 1, 1, 5f, ("wood", 100), ("metal.fragments", 50));
            R("pickaxe", 1, 1, 5f, ("wood", 100), ("metal.fragments", 75));
            R("hammer", 1, 1, 4f, ("wood", 50), ("metal.fragments", 25));
            R("furnace", 1, 1, 10f, ("stones", 100), ("wood", 50));
            R("workbench1", 1, 1, 15f, ("wood", 300), ("metal.fragments", 50));
            R("lock.key", 1, 1, 5f, ("metal.fragments", 25));
            R("lock.code", 1, 1, 8f, ("metal.fragments", 50));
            R("door.hinged.wood", 1, 1, 6f, ("wood", 150));
            R("door.hinged.metal", 1, 1, 10f, ("metal.fragments", 100));
            R("pistol.eoka", 1, 1, 8f, ("wood", 50), ("metal.fragments", 50), ("gunpowder", 10));
            R("shotgun.waterpipe", 1, 1, 10f, ("metal.fragments", 100), ("wood", 50));
            R("ammo.handmade.shell", 2, 1, 3f, ("gunpowder", 10), ("metal.fragments", 5));
            R("ammo.pistol", 5, 1, 4f, ("gunpowder", 10), ("metal.fragments", 10));
            R("can.beans", 1, 1, 4f, ("metal.fragments", 10), ("meat.raw", 1));
            R("barricade.concrete", 1, 1, 6f, ("stones", 50));
            R("trap.spikes", 1, 1, 5f, ("wood", 100));
            R("explosive.beancan", 1, 1, 6f, ("gunpowder", 30), ("metal.fragments", 20));

            // ---------- верстак 2 ----------
            R("workbench2", 1, 2, 25f, ("wood", 500), ("metal.fragments", 200), ("metal.refined", 20));
            R("metal.fragments", 1, 2, 1f, ("metal.ore", 1));
            R("gunpowder", 10, 2, 2f, ("sulfur", 20), ("charcoal", 30));
            R("smg.custom", 1, 2, 20f, ("metal.fragments", 200), ("metal.refined", 20), ("scrap", 50));
            R("smg.thompson", 1, 2, 25f, ("metal.fragments", 250), ("metal.refined", 30), ("scrap", 75));
            R("smg.mp5", 1, 2, 30f, ("metal.fragments", 300), ("metal.refined", 40), ("scrap", 100));
            R("shotgun.pump", 1, 2, 25f, ("metal.fragments", 300), ("metal.refined", 30));
            R("rifle.semiauto", 1, 2, 30f, ("metal.fragments", 300), ("metal.refined", 50), ("scrap", 75));
            R("ammo.smg", 5, 2, 5f, ("gunpowder", 15), ("metal.fragments", 15));
            R("ammo.rifle", 5, 2, 5f, ("gunpowder", 20), ("metal.fragments", 20));
            R("ammo.shotgun", 3, 2, 5f, ("gunpowder", 20), ("metal.fragments", 10));
            R("largemedkit", 1, 2, 15f, ("cloth", 30), ("metal.fragments", 20), ("scrap", 20));
            R("antidote", 1, 2, 10f, ("scrap", 30), ("hive.membrane", 2));
            R("almond.water", 1, 2, 8f, ("water.dirty", 1), ("scrap", 5));
            R("roadsign.jacket", 1, 2, 20f, ("metal.fragments", 100), ("scrap", 50), ("sewingkit", 1));
            R("roadsign.kilt", 1, 2, 20f, ("metal.fragments", 80), ("scrap", 40), ("sewingkit", 1));
            R("coffeecan.helmet", 1, 2, 15f, ("metal.fragments", 100), ("scrap", 25));
            R("riot.helmet", 1, 2, 20f, ("metal.fragments", 150), ("scrap", 50));
            R("weapon.mod.holosight", 1, 2, 15f, ("metal.fragments", 50), ("metal.refined", 10), ("scrap", 25));
            R("explosive.satchel", 1, 2, 20f, ("gunpowder", 90), ("metal.fragments", 80), ("cloth", 20));
            R("grenade.f1", 1, 2, 15f, ("gunpowder", 60), ("metal.fragments", 60));
            R("furnace.large", 1, 2, 30f, ("stones", 300), ("metal.fragments", 100));
            R("repair.bench", 1, 2, 25f, ("wood", 300), ("metal.fragments", 100), ("scrap", 50));
            R("research.table", 1, 2, 25f, ("wood", 300), ("metal.fragments", 100), ("scrap", 75));
            R("water.purifier", 1, 2, 20f, ("metal.fragments", 150), ("scrap", 75));
            R("barricade.metal", 1, 2, 10f, ("metal.fragments", 100));
            R("explosive.timed", 1, 3, 30f, ("gunpowder", 200), ("metal.refined", 50), ("scrap", 150));

            // ---------- верстак 3 ----------
            R("workbench3", 1, 3, 40f, ("wood", 1000), ("metal.fragments", 500), ("metal.refined", 100));
            R("rifle.ak", 1, 3, 45f, ("metal.fragments", 500), ("metal.refined", 100), ("scrap", 200));
            R("rifle.lr300", 1, 3, 45f, ("metal.fragments", 500), ("metal.refined", 100), ("scrap", 200));
            R("rifle.m16", 1, 3, 45f, ("metal.fragments", 500), ("metal.refined", 100), ("scrap", 200));
            R("rifle.bolt", 1, 3, 50f, ("metal.fragments", 500), ("metal.refined", 150), ("scrap", 250));
            R("lmg.m249", 1, 3, 60f, ("metal.fragments", 800), ("metal.refined", 200), ("scrap", 400));
            R("rocket.launcher", 1, 3, 50f, ("metal.fragments", 500), ("metal.refined", 150), ("scrap", 300));
            R("ammo.rifle.hv", 5, 3, 8f, ("gunpowder", 25), ("metal.refined", 5));
            R("ammo.rifle.explosive", 5, 3, 12f, ("gunpowder", 40), ("metal.refined", 10), ("sulfur", 10));
            R("ammo.rocket.basic", 1, 3, 20f, ("gunpowder", 100), ("metal.refined", 50), ("scrap", 50));
            R("ammo.grenadelauncher.he", 2, 3, 15f, ("gunpowder", 80), ("metal.refined", 30));
            R("metal.plate.torso", 1, 3, 40f, ("metal.refined", 100), ("metal.fragments", 300), ("sewingkit", 2));
            R("metal.facemask", 1, 3, 35f, ("metal.refined", 50), ("metal.fragments", 200));
            R("weapon.mod.silencer", 1, 3, 30f, ("metal.refined", 40), ("scrap", 100));
            R("weapon.mod.8x.scope", 1, 3, 30f, ("metal.refined", 50), ("scrap", 120));
            R("weapon.mod.extendedmags", 1, 3, 25f, ("metal.refined", 30), ("scrap", 75));
            R("nightvision", 1, 3, 45f, ("metal.refined", 100), ("scrap", 200), ("anomaly.shard", 2));
            R("hazmat.suit.reactor", 1, 3, 40f, ("hazmatsuit", 1), ("metal.refined", 100), ("hazard.foil", 0));
            R("mine.landmine", 1, 3, 20f, ("gunpowder", 50), ("metal.fragments", 100), ("scrap", 25));
            R("autoturret", 1, 3, 60f, ("metal.fragments", 500), ("metal.refined", 100), ("scrap", 150));
            R("flameturret", 1, 3, 60f, ("metal.fragments", 400), ("metal.refined", 80), ("scrap", 120));
            R("samsite", 1, 3, 60f, ("metal.fragments", 500), ("metal.refined", 150), ("scrap", 200));
            R("door.hinged.toptier", 1, 3, 40f, ("metal.refined", 100), ("metal.fragments", 200));
            R("exoskeleton.suit", 1, 3, 90f, ("metal.refined", 300), ("scrap", 500), ("anomaly.shard", 5), ("hive.membrane", 5));
        }
    }

    public class CraftQueueItem
    {
        public Recipe recipe;
        public int remaining;
        public float finishTime;
        public int benchLevel;
    }

    /// <summary>Крафт-очередь игрока + UI выбора рецептов.</summary>
    public class CraftingSystem : MonoBehaviour
    {
        public static CraftingSystem Instance { get; private set; }

        public PlayerInventory inventory;
        public int nearbyBenchLevel = 0;      // выставляется системой верстаков (версия 0.2) или кнопкой E на верстаке
        public float benchSpeed = 1f;         // скорость крафта рядом стоящего верстака (T1 ×1.0, T2 ×1.5, T3 ×2.0)

        readonly List<CraftQueueItem> _queue = new List<CraftQueueItem>(8);
        Canvas _canvas;
        RectTransform _panel;
        Text _listText, _queueText;

        void Awake()
        {
            Instance = this;
            BuildUI();
            _panel.gameObject.SetActive(false);
        }

        void BuildUI()
        {
            _canvas = UIStyle.CreateCanvas("CraftCanvas", 130, out _);
            _panel = UIStyle.Panel(_canvas.transform, "Craft", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   new Vector2(-500, -380), new Vector2(500, 380), UIStyle.PanelBg);
            UIStyle.Label(_panel, "КРАФТ", 30, UIStyle.BackroomsYellow);
            _listText = UIStyle.Label(_panel, "", 18, UIStyle.TermGreen, TextAnchor.UpperLeft);
            _listText.rectTransform.offsetMin = new Vector2(24, 120);
            _queueText = UIStyle.Label(_panel, "", 16, UIStyle.TermGreenDim, TextAnchor.LowerLeft);
            _queueText.rectTransform.offsetMin = new Vector2(24, 24);
            _queueText.rectTransform.sizeDelta = new Vector2(900, 100);
            // Кнопка в правом верхнем углу панели (список крафта идёт слева и снизу её не задевает).
            UIStyle.Btn(_panel, "ЗАКРЫТЬ (Esc)", new Vector2(696, -24), new Vector2(280, 50), () => CloseMenu());
        }

        /// <summary>Верстак в радиусе изменился (вызывает WorkbenchSystem).</summary>
        public void SetBenchProximity(int level, float speed)
        {
            nearbyBenchLevel = level;
            benchSpeed = Mathf.Max(0.1f, speed);
            if (_panel != null && _panel.gameObject.activeSelf) RefreshList();
        }

        /// <summary>Пересчитать уровень по фактическому наличию верстака рядом (сервер/хост).</summary>
        public void RecomputeBench()
        {
            if (Subsistence.Crafting.WorkbenchSystem.Instance != null)
            {
                var ws = Subsistence.Crafting.WorkbenchSystem.Instance;
                if (ws.CurrentLevel > 0) { nearbyBenchLevel = ws.CurrentLevel; benchSpeed = ws.CurrentSpeed; }
            }
        }

        public static void OpenMenu(int benchLevel = -1)
        {
            if (Instance == null) return;
            if (benchLevel >= 0) Instance.nearbyBenchLevel = benchLevel;
            Instance._panel.gameObject.SetActive(true);
            UIState.AnyMenuOpen = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Instance.RefreshList();
        }

        public void CloseMenu()
        {
            _panel.gameObject.SetActive(false);
            UIState.AnyMenuOpen = false;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        void Update()
        {
            if (_panel == null || !_panel.gameObject.activeSelf) return;
            if (Input.GetKeyDown(KeyCode.Escape)) CloseMenu();
            TickQueue();
        }

        void RefreshList()
        {
            var sb = new System.Text.StringBuilder(2048);
            sb.Append(nearbyBenchLevel > 0
                ? $"<color=#ffd23f>Верстак {nearbyBenchLevel} уровня рядом · скорость крафта ×{benchSpeed:0.0#}</color>\n"
                : "<color=#8a8a8a>Верстака рядом нет — доступен только крафт 0 тира</color>\n");
            int shown = 0;
            foreach (var r in RecipeBook.All)
            {
                if (r.benchLevel > nearbyBenchLevel) continue;
                var def = ItemDatabase.Def(r.resultId);
                bool can = r.CanCraft(inventory);
                string ings = "";
                for (int i = 0; i < r.ingredients.Count; i++)
                {
                    var ing = r.ingredients[i];
                    int have = inventory.CountOf(ing.id);
                    ings += $"{(have >= ing.amount ? "<color=#39ff6a>" : "<color=#ff6b5e>")}{ItemDatabase.NameOf(ing.id)} {have}/{ing.amount}</color>  ";
                }
                bool known = !r.requiresResearch || TechTree.IsKnown(NetworkBridge.Host?.LocalPlayerId ?? 0UL, r.resultId);
                string head = known
                    ? $"[{shown + 1}] {def?.Display() ?? r.resultId} x{r.amount}   верстак {r.benchLevel}"
                    : $"[{shown + 1}] {def?.Display() ?? r.resultId} x{r.amount}   верстак {r.benchLevel}   <color=#ffd23f>изучить: {TechTree.CostOf(r.resultId)} хлама</color>";
                sb.Append($"<color={(can && known ? "#ffffff" : "#8a8a8a")}>{head}</color>\n   {ings}\n");
                shown++;
                if (shown >= 14) break;
            }
            _listText.text = sb.ToString();
        }

        void TickQueue()
        {
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                var q = _queue[i];
                if (Time.time < q.finishTime) continue;
                q.remaining--;
                if (q.remaining > 0) q.finishTime = Time.time + q.recipe.craftSeconds / Mathf.Max(1f, benchSpeed);
                else _queue.RemoveAt(i);

                if (NetworkBridge.IsServer)
                {
                    inventory.TryAddAmount(q.recipe.resultId, q.recipe.amount);
                    Subsistence.Audio.AudioDirector.Play2D("craft_done", 0.5f);
                    HudRuntime.ShowToast($"Скрафчено: {ItemDatabase.NameOf(q.recipe.resultId)} x{q.recipe.amount}");
                }
            }
            if (_queue.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var q in _queue)
                    sb.Append($"{ItemDatabase.NameOf(q.recipe.resultId)} x{q.remaining} ({Mathf.Max(0f, q.finishTime - Time.time):F1}с)\n");
                _queueText.text = sb.ToString();
            }
            else if (_queueText != null) _queueText.text = "";
        }

        /// <summary>Заказ крафта (его вызывает UI или клавиши 1..9 в меню).</summary>
        public bool Craft(string resultId, int count = 1)
        {
            var r = RecipeBook.Get(resultId);
            if (r == null) return false;
            if (r.benchLevel > nearbyBenchLevel)
            {
                HudRuntime.ShowToast(nearbyBenchLevel > 0
                    ? $"Нужен верстак {r.benchLevel} уровня (рядом только {nearbyBenchLevel})"
                    : $"Нужен верстак {r.benchLevel} уровня — встаньте к нему");
                return false;
            }
            // Rust-правило: верстак должен стоять рядом именно в момент заказа.
            // WorkbenchSystem обнуляет nearbyBenchLevel, как только игрок вышел из радиуса,
            // поэтому проверка выше уже отсекает «отошёл и крафчу».
            if (!r.CanCraft(inventory))
            {
                HudRuntime.ShowToast("Не хватает ресурсов");
                return false;
            }
            if (r.requiresResearch && !TechTree.IsKnown(NetworkBridge.Host?.LocalPlayerId ?? 0UL, resultId))
            {
                int cost = TechTree.CostOf(resultId);
                int bench = TechTree.BenchLevelFor(resultId);
                HudRuntime.ShowToast($"Рецепт не изучен: нужен стол исследований, {cost} хлама и верстак {bench} уровня");
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                if (!r.CanCraft(inventory)) break;
                inventory.Pay(r.ingredients);
                float speed = Mathf.Max(1f, benchSpeed);
                _queue.Add(new CraftQueueItem
                {
                    recipe = r, remaining = 1,
                    finishTime = Time.time + r.craftSeconds / speed,
                    benchLevel = nearbyBenchLevel
                });
            }
            RefreshList();
            return true;
        }
    }
}
