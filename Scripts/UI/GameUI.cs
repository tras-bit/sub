// ============================================================================
//  SUBSISTENCE — UI/GameUI.cs
//  HUD (здоровье/еда/вода/радиация/выносливость, хотбар, прицел,
//  вспышки урона, тосты) и инвентарь с drag&drop, контейнерами и крафтом.
//  Всё создаётся кодом (uGUI) — работает в любой сцене без префабов.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Subsistence.Core;
using Subsistence.Player;

namespace Subsistence.UI
{
    /// <summary>HUD: статичный оверлей + динамические элементы.</summary>
    public class HudRuntime : MonoBehaviour
    {
        public static HudRuntime Instance { get; private set; }

        Canvas _canvas;
        Image _healthFill, _foodFill, _waterFill, _radFill, _staminaFill;
        Text _healthText, _buildHint, _toast, _vitalsText;
        Image _damageFlash, _crosshairDot;
        RectTransform _crosshair;
        readonly List<Image> _hotbarSlots = new List<Image>(6);
        readonly List<Text> _hotbarTexts = new List<Text>(6);
        float _toastUntil;
        float _flashAmount;

        public PlayerController player;

        void Awake()
        {
            Instance = this;
            BuildUI();
        }

        void BuildUI()
        {
            _canvas = UIStyle.CreateCanvas("HudCanvas", 100, out _);

            // ---------- Прицел ----------
            _crosshair = UIStyle.Panel(_canvas.transform, "Crosshair", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       new Vector2(-18, -18), new Vector2(18, 18), new Color(1, 1, 1, 0.55f));
            _crosshairDot = UIStyle.Panel(_crosshair, "Dot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                          new Vector2(-1.5f, -1.5f), new Vector2(1.5f, 1.5f), new Color(1, 0.95f, 0.6f, 0.9f)).GetComponent<Image>();
            _crosshairDot.enabled = false;

            // ---------- Вспышка урона ----------
            _damageFlash = UIStyle.Panel(_canvas.transform, "DamageFlash", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                                         new Color(0.6f, 0f, 0f, 0f)).GetComponent<Image>();
            _damageFlash.raycastTarget = false;

            // ---------- Витальные шкалы (слева снизу, как в Rust) ----------
            var vitals = UIStyle.Panel(_canvas.transform, "Vitals", new Vector2(0, 0), new Vector2(0, 0),
                                       new Vector2(24, 24), new Vector2(384, 224), new Color(0, 0, 0, 0));
            _healthFill = MakeBar(vitals, "HP", 0, new Color(0.85f, 0.2f, 0.2f), out _healthText);
            _foodFill = MakeBar(vitals, "FOOD", 1, new Color(0.85f, 0.62f, 0.2f), out _);
            _waterFill = MakeBar(vitals, "H2O", 2, new Color(0.25f, 0.6f, 0.95f), out _);
            _radFill = MakeBar(vitals, "RAD", 3, new Color(0.55f, 0.95f, 0.25f), out _);
            _staminaFill = MakeBar(vitals, "STAM", 4, new Color(0.9f, 0.9f, 0.35f), out _);

            _vitalsText = UIStyle.Label(vitals, "", 16, UIStyle.TermGreenDim, TextAnchor.LowerLeft);
            _vitalsText.rectTransform.anchorMin = new Vector2(0, 0); _vitalsText.rectTransform.anchorMax = new Vector2(1, 0);
            _vitalsText.rectTransform.sizeDelta = new Vector2(360, 26);
            _vitalsText.rectTransform.anchoredPosition = new Vector2(0, -26);

            // ---------- Хотбар ----------
            var hotbar = UIStyle.Panel(_canvas.transform, "Hotbar", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                                       new Vector2(-330, 22), new Vector2(330, 106), new Color(0, 0, 0, 0));
            for (int i = 0; i < Balance.HotbarSlots; i++)
            {
                var slot = UIStyle.Panel(hotbar, "Slot" + i, new Vector2(0, 0), new Vector2(0, 0),
                                         new Vector2(i * 110 + 2, 2), new Vector2(i * 110 + 106, 82), new Color(0.05f, 0.06f, 0.05f, 0.7f));
                _hotbarSlots.Add(slot.GetComponent<Image>());
                var t = UIStyle.Label(slot, "", 15, UIStyle.TermGreen, TextAnchor.LowerRight);
                t.rectTransform.offsetMin = new Vector2(4, 4); t.rectTransform.offsetMax = new Vector2(-4, -4);
                _hotbarTexts.Add(t);
            }

            // ---------- Подсказка стройки и тосты ----------
            _buildHint = UIStyle.Label(_canvas.transform, "", 20, UIStyle.DangerRed, TextAnchor.MiddleCenter);
            _buildHint.rectTransform.anchorMin = new Vector2(0.5f, 0.42f); _buildHint.rectTransform.anchorMax = new Vector2(0.5f, 0.42f);
            _buildHint.rectTransform.sizeDelta = new Vector2(900, 40);

            _toast = UIStyle.Label(_canvas.transform, "", 22, UIStyle.BackroomsYellow, TextAnchor.MiddleCenter);
            _toast.rectTransform.anchorMin = new Vector2(0.5f, 0.62f); _toast.rectTransform.anchorMax = new Vector2(0.5f, 0.62f);
            _toast.rectTransform.sizeDelta = new Vector2(1200, 60);
        }

        static Image MakeBar(RectTransform parent, string label, int index, Color fill, out Text valueText)
        {
            const float rowH = 26f;
            float y = -index * (rowH + 6f);
            var row = UIStyle.Panel(parent, "Bar_" + label, new Vector2(0, 1), new Vector2(0, 1),
                                    new Vector2(0, y - rowH), new Vector2(360, y), new Color(0, 0, 0, 0.55f));
            var fillImg = UIStyle.Panel(row, "Fill", Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2), fill).GetComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            var lbl = UIStyle.Label(row, label, 14, new Color(0.9f, 0.9f, 0.9f, 0.85f), TextAnchor.MiddleLeft);
            lbl.rectTransform.offsetMin = new Vector2(8, 0);
            valueText = UIStyle.Label(row, "", 14, Color.white, TextAnchor.MiddleRight);
            valueText.rectTransform.offsetMax = new Vector2(-8, 0);
            return fillImg;
        }

        void Update()
        {
            if (player == null) return;
            var st = player.survival.State;
            _healthFill.fillAmount = Mathf.Clamp01(st.health / 100f);
            _foodFill.fillAmount = st.CaloriePercent;
            _waterFill.fillAmount = st.HydrationPercent;
            _radFill.fillAmount = st.RadPercent;
            _staminaFill.fillAmount = player.Stamina / player.maxStamina;
            _healthText.text = Mathf.RoundToInt(st.health).ToString();

            string status = "";
            if (st.bleeding > 0) status += $"КРОВОТЕЧЕНИЕ x{st.bleeding}  ";
            if (st.infections > 0) status += $"ИНФЕКЦИЯ x{st.infections}  ";
            if (st.wetness > 60f) status += "МОКРЫЙ  ";
            if (st.temperature < 36f) status += "ХОЛОДНО  ";
            if (st.radiation > 250f) status += $"РАДИАЦИЯ {st.radiation:F0}  ";
            // 18_weather + 19_airdrops: аномалия/сброс прямо в строке состояния
            var weather = World.WeatherSystem.Instance;
            if (weather != null)
            {
                if (weather.IsActive)
                    status += $"АНОМАЛИЯ {World.WeatherSystem.NameOf(weather.Current).ToUpper()} {Mathf.CeilToInt(weather.TimeLeft)}с  ";
                else if (weather.NextEventIn > 0f && weather.NextEventIn < weather.warnBefore)
                    status += $"АНОМАЛИЯ ЧЕРЕЗ {Mathf.CeilToInt(weather.NextEventIn)}с  ";
            }
            var air = World.AirdropSystem.Instance;
            if (air != null && air.NextDropIn > 0f && air.NextDropIn < 60f)
                status += $"СБРОС {Mathf.CeilToInt(air.NextDropIn)}с  ";

            if (_vitalsText != null) _vitalsText.text = status;

            // хотбар
            var inv = player.inventory;
            for (int i = 0; i < _hotbarSlots.Count; i++)
            {
                var item = inv.Get(i);
                var def = item?.Def;
                _hotbarTexts[i].text = item == null || item.IsEmpty ? "" :
                    (def != null && def.IsStackable ? $"{def.nameRu}\n{item.amount}" : def?.nameRu ?? item.id);
                _hotbarSlots[i].color = i == inv.ActiveHotbarIndex ? new Color(0.35f, 0.32f, 0.12f, 0.35f) : new Color(0.05f, 0.06f, 0.05f, 0.7f);
            }

            // прицел: расширяется от разброса
            var weapon = player.GetComponentInChildren<Combat.WeaponController>();
            float spread = weapon?.Stats != null ? (weapon.IsAiming ? weapon.Stats.adsSpreadDeg : weapon.Stats.hipSpreadDeg) * PlayerControllerState.SpreadMultiplier : 1f;
            float size = Mathf.Clamp(18f + spread * 6f, 12f, 90f);
            _crosshair.sizeDelta = new Vector2(size, size);
            _crosshairDot.enabled = PlayerControllerState.IsSprinting || PlayerControllerState.IsSwimming;

            // вспышка урона затухает
            if (_flashAmount > 0f)
            {
                _flashAmount = Mathf.Max(0f, _flashAmount - Time.deltaTime * 1.6f);
                var c = _damageFlash.color; c.a = _flashAmount * 0.45f; _damageFlash.color = c;
            }
            if (_toastUntil > 0f && Time.time > _toastUntil) { _toast.text = ""; _toastUntil = 0f; }

            // курсор в инвентаре
            if (Input.GetKeyDown(KeyCode.Tab)) InventoryUI.Toggle();
        }

        public static void ShowToast(string msg, float seconds = 3f)
        {
            if (Instance == null) return;
            Instance._toast.text = msg;
            Instance._toastUntil = Time.time + seconds;
        }

        public static void SetBuildHint(string msg)
        {
            if (Instance == null) return;
            Instance._buildHint.text = msg;
        }

        public static void FlashDamage(DamageType type)
        {
            if (Instance == null) return;
            Instance._flashAmount = Mathf.Min(1f, Instance._flashAmount + (type == DamageType.Explosion ? 0.9f : 0.5f));
        }
    }

    /// <summary>Слот инвентаря/контейнера: контейнер, индекс, рамка, фон, иконка, подписи.</summary>
    /// <remarks>Класс вынесен на уровень namespace — его используют и HudRuntime (иконки в хотбаре),
    /// и InventoryUI. Вложенный класс был виден только внутри InventoryUI (ошибка CS0246 в Unity).</remarks>
    public class Slot
    {
        public ItemContainer container;
        public int index;
        public RectTransform rt;
        public Image bg;
        public Image icon;      // иконка предмета (Resources/icons), выключается, если её нет
        public Text label;
        public Text stack;
    }

    /// <summary>Инвентарь: сетка, drag&drop, сплит правой кнопкой, быстрый перенос Shift+ЛКМ.</summary>
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        Canvas _canvas;
        RectTransform _root;
        RectTransform _gridPlayer, _gridContainer;
        Text _containerTitle, _tooltip;
        ItemContainer _openContainer;                  // основная ёмкость (у печи — вход)
        ItemContainer _secondary;                      // вторая ёмкость (у печи — выход)
        List<Slot> _secondarySlots = new List<Slot>(8);
        RectTransform _gridSecondary;
        Text _secondaryTitle;
        Subsistence.Net.InventoryNet.Handle _openHandle;   // адрес контейнера в сети
        bool _openNetworked;                               // содержимым владеет сервер

        /// <summary>Открытый контейнер обслуживает сервер (в сети) — все действия идём просить ему.</summary>
        bool Networked => _openNetworked && Subsistence.Net.NetworkBridge.IsClient && _openHandle.IsValid;

        /// <summary>Адрес конкретной ёмкости открытого объекта (у печи выход — sub 1).</summary>
        Subsistence.Net.InventoryNet.Handle HandleFor(ItemContainer c)
            => _secondary != null && c == _secondary ? _openHandle.With(1) : _openHandle;
        Button _craftBtn;
        Image _dragIcon;
        Text _dragText;
        Slot _dragging;

        const float Cell = 62f, Gap = 4f;

        readonly List<Slot> _playerSlots = new List<Slot>(42);
        readonly List<Slot> _containerSlots = new List<Slot>(24);

        void Awake()
        {
            Instance = this;
            BuildUI();
            _root.gameObject.SetActive(false);
        }

        void BuildUI()
        {
            _canvas = UIStyle.CreateCanvas("InventoryCanvas", 120, out _);
            _root = UIStyle.Panel(_canvas.transform, "InvRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.55f));

            var panel = UIStyle.Panel(_root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                      new Vector2(-620, -400), new Vector2(620, 400), UIStyle.PanelBg);

            UIStyle.Label(panel, "ИНВЕНТАРЬ", 30, UIStyle.BackroomsYellow, TextAnchor.UpperLeft);
            _gridPlayer = UIStyle.Panel(panel, "PlayerGrid", new Vector2(0, 1), new Vector2(0, 1),
                                       new Vector2(24, -520), new Vector2(24 + 6 * (Cell + Gap) + 16, -24), new Color(0, 0, 0, 0.35f));

            // слоты: 6 хотбар + 24 рюкзак + 6 броня
            for (int i = 0; i < PlayerInventory.TotalSlots; i++)
            {
                int col = i % 6, row = i / 6;
                var rt = UIStyle.Panel(_gridPlayer, "S" + i, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(8 + col * (Cell + Gap), -8 - row * (Cell + Gap) - Cell),
                    new Vector2(8 + col * (Cell + Gap) + Cell, -8 - row * (Cell + Gap)), new Color(0.1f, 0.12f, 0.1f, 0.85f));
                var slot = new Slot { index = i, rt = rt, bg = rt.GetComponent<Image>() };
                ItemIcons.AttachIcon(slot);
                slot.label = UIStyle.Label(rt, "", 11, UIStyle.TermGreen, TextAnchor.UpperLeft);
                slot.stack = UIStyle.Label(rt, "", 15, Color.white, TextAnchor.LowerRight);
                var btn = rt.gameObject.AddComponent<Button>();
                int idx = i;
                btn.onClick.AddListener(() => OnSlotClicked(idx));
                _playerSlots.Add(slot);
            }

            // контейнер (24 слота) — показывается при лутании
            _containerTitle = UIStyle.Label(panel, "ЯЩИК", 24, UIStyle.TermGreen, TextAnchor.UpperLeft);
            _containerTitle.rectTransform.anchoredPosition = new Vector2(700, 330);
            _containerTitle.rectTransform.sizeDelta = new Vector2(500, 40);
            _gridContainer = UIStyle.Panel(panel, "ContainerGrid", new Vector2(1, 1), new Vector2(1, 1),
                                           new Vector2(-24 - 4 * (Cell + Gap) - 16, -400), new Vector2(-24, -60), new Color(0, 0, 0, 0.35f));
            for (int i = 0; i < 24; i++)
            {
                int col = i % 4, row = i / 4;
                var rt = UIStyle.Panel(_gridContainer, "C" + i, new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-8 - (col + 1) * (Cell + Gap), -8 - row * (Cell + Gap) - Cell),
                    new Vector2(-8 - col * (Cell + Gap), -8 - row * (Cell + Gap)), new Color(0.1f, 0.12f, 0.1f, 0.85f));
                var slot = new Slot { index = i, rt = rt, bg = rt.GetComponent<Image>() };
                ItemIcons.AttachIcon(slot);
                slot.label = UIStyle.Label(rt, "", 11, UIStyle.TermGreen, TextAnchor.UpperLeft);
                slot.stack = UIStyle.Label(rt, "", 15, Color.white, TextAnchor.LowerRight);
                var btn = rt.gameObject.AddComponent<Button>();
                int idx = i;
                btn.onClick.AddListener(() => OnContainerSlotClicked(idx));
                _containerSlots.Add(slot);
            }

            // вторая ёмкость (печь: выход металла) — под основной сеткой
            _secondaryTitle = UIStyle.Label(panel, "", 20, UIStyle.TermGreen, TextAnchor.UpperLeft);
            _secondaryTitle.rectTransform.anchorMin = new Vector2(1, 1);
            _secondaryTitle.rectTransform.anchorMax = new Vector2(1, 1);
            _secondaryTitle.rectTransform.pivot = new Vector2(1, 1);
            _secondaryTitle.rectTransform.anchoredPosition = new Vector2(-24, -404);
            _secondaryTitle.rectTransform.sizeDelta = new Vector2(272, 26);

            _gridSecondary = UIStyle.Panel(panel, "SecondaryGrid", new Vector2(1, 1), new Vector2(1, 1),
                                           new Vector2(-24 - 4 * (Cell + Gap) - 16, -566), new Vector2(-24, -436),
                                           new Color(0, 0, 0, 0.35f));
            for (int i = 0; i < 8; i++)
            {
                int col = i % 4, row = i / 4;
                var rt = UIStyle.Panel(_gridSecondary, "S" + i, new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-8 - (col + 1) * (Cell + Gap), -8 - row * (Cell + Gap) - Cell),
                    new Vector2(-8 - col * (Cell + Gap), -8 - row * (Cell + Gap)), new Color(0.1f, 0.12f, 0.1f, 0.85f));
                var slot = new Slot { index = i, rt = rt, bg = rt.GetComponent<Image>() };
                slot.label = UIStyle.Label(rt, "", 11, UIStyle.TermGreen, TextAnchor.UpperLeft);
                slot.stack = UIStyle.Label(rt, "", 15, Color.white, TextAnchor.LowerRight);
                var btn = rt.gameObject.AddComponent<Button>();
                int idx = i;
                btn.onClick.AddListener(() => OnSecondarySlotClicked(idx));
                _secondarySlots.Add(slot);
            }
            _secondaryTitle.gameObject.SetActive(false);
            _gridSecondary.gameObject.SetActive(false);

            UIStyle.Btn(panel, "ВЗЯТЬ ВСЁ", new Vector2(544, -330), new Vector2(300, 54), () =>
            {
                if (_openContainer != null)
                {
                    // «Забрать всё» — как Loot All в Rust (в сети пачку выдаёт сервер).
                    // (было TransferAllFrom самого себя — то есть ничего: теперь в рюкзак игрока)
                    var playerInv = HudRuntime.Instance != null && HudRuntime.Instance.player != null
                                  ? HudRuntime.Instance.player.inventory : null;
                    if (Networked)
                    {
                        Subsistence.Net.InventoryNet.RequestTakeAll(HandleFor(_openContainer));
                        if (_secondary != null) Subsistence.Net.InventoryNet.RequestTakeAll(_openHandle.With(1));
                    }
                    else if (playerInv != null)
                    {
                        playerInv.TransferAllFrom(_openContainer);
                        if (_secondary != null) playerInv.TransferAllFrom(_secondary);
                    }
                    Refresh();
                }
            });
            _craftBtn = UIStyle.Btn(panel, "КРАФТ (Q)", new Vector2(544, -270), new Vector2(300, 54), () => Subsistence.Crafting.CraftingSystem.OpenMenu());

            _tooltip = UIStyle.Label(_root, "", 16, UIStyle.BackroomsYellow, TextAnchor.LowerLeft);
            _tooltip.rectTransform.anchorMin = new Vector2(0, 0); _tooltip.rectTransform.anchorMax = new Vector2(0, 0);
            _tooltip.rectTransform.sizeDelta = new Vector2(600, 120);
            _tooltip.rectTransform.anchoredPosition = new Vector2(24, 24);

            // иконка «в руке» при перетаскивании
            var drag = UIStyle.Panel(_root, "DragIcon", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(Cell, Cell), new Color(1, 1, 1, 0.25f));
            _dragIcon = drag.GetComponent<Image>();
            _dragText = UIStyle.Label(drag, "", 14, Color.white, TextAnchor.MiddleCenter);
            _dragIcon.enabled = false; _dragText.enabled = false;
        }

        void Update()
        {
            if (!_root.gameObject.activeSelf) return;
            UIState.AnyMenuOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_dragging != null)
            {
                Vector2 pos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_root, Input.mousePosition, null, out pos);
                _dragIcon.rectTransform.anchoredPosition = pos + new Vector2(Cell * 0.5f, -Cell * 0.5f);
            }
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        // ---------- Открытие/закрытие ----------
        public static void Toggle()
        {
            if (Instance == null) return;
            if (Instance._root.gameObject.activeSelf) Instance.Close();
            else Instance.Open(null);
        }

        public static void OpenLoot(World.LootContainer container)
        {
            if (container == null || Instance == null) return;
            Instance._openHandle = Subsistence.Net.InventoryNet.HandleOf(container);
            Instance._openNetworked = true;
            // В сети содержимое ящика живёт на сервере: просим его (офлайн — локальный ящик и так верный).
            Subsistence.Net.InventoryNet.RequestOpen(Instance._openHandle);
            Instance.Open(container.Items, container);
        }

        /// <summary>Контейнер деплоя (ящик/печь): содержимым владеет сервер и пришлёт его.</summary>
        public static void OpenDeployable(Building.BuildDeployable d, ItemContainer main, ItemContainer secondary = null)
        {
            if (d == null || Instance == null) return;
            Instance._openHandle = Subsistence.Net.InventoryNet.HandleOf(d);
            OpenContainer(main, secondary);
            Instance._openNetworked = true;             // OpenContainer сбрасывает флаг — ставим после
            Subsistence.Net.InventoryNet.RequestOpen(Instance._openHandle);
        }

        /// <summary>Сервер прислал новые данные контейнера — обновить окно, если открыт именно он.</summary>
        public static void RefreshOpen(Subsistence.Net.InventoryNet.Handle h)
        {
            if (Instance == null || !Instance._openNetworked) return;
            if (!Subsistence.Net.InventoryNet.SameHandle(Instance._openHandle, h)) return;

            // Контейнер мог быть пересоздан (например, ящик мира зарефилился) — берём актуальные ссылки.
            var main = Subsistence.Net.InventoryNet.ClientContainer(h.With(0));
            if (main != null) Instance._openContainer = main;
            Instance._secondary = Subsistence.Net.InventoryNet.HasSecondary(h)
                                ? Subsistence.Net.InventoryNet.ClientContainer(h.With(1))
                                : null;
            Instance.Refresh();
        }

        public static void OpenContainer(ItemContainer main, ItemContainer secondary = null)
        {
            Instance?._root.gameObject.SetActive(true);
            UIState.AnyMenuOpen = true;
            Instance._openContainer = main;
            Instance._secondary = secondary;
            Instance._openNetworked = false;            // локальные меню (крафт, офлайн)
            Instance._containerTitle.text = main?.ContainerName ?? "";
            Instance.Refresh();
        }

        public static void OpenCrafting(int benchLevel) => Subsistence.Crafting.CraftingSystem.OpenMenu(benchLevel);

        void Open(ItemContainer container, World.LootContainer world = null)
        {
            _root.gameObject.SetActive(true);
            _openContainer = container;
            _containerTitle.text = container != null ? container.ContainerName : "Инвентарь";
            Refresh();
        }

        void Close()
        {
            _root.gameObject.SetActive(false);
            UIState.AnyMenuOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _openContainer = null;
            _secondary = null;
            _openNetworked = false;
            if (_dragging != null) { _dragging = null; _dragIcon.enabled = false; _dragText.enabled = false; }
        }

        // ---------- Клики ----------
        void OnSlotClicked(int index)
        {
            var inv = HudRuntime.Instance != null && HudRuntime.Instance.player != null ? HudRuntime.Instance.player.inventory : null;
            if (inv == null) return;
            var slot = _playerSlots[index];
            var item = inv.Get(index);

            if (_dragging != null)
            {
                // завершение перетаскивания
                DropInto(_dragging, inv, index);
                _dragging = null; _dragIcon.enabled = false; _dragText.enabled = false;
                Refresh();
                return;
            }

            if (Input.GetKey(KeyCode.LeftShift) && item != null && _openContainer != null)
            {
                // Быстрый перенос Shift+ЛКМ (в ящик/печь — через сервер, если он открыт по сети)
                if (Networked)
                    Subsistence.Net.InventoryNet.RequestStore(HandleFor(_openContainer), index, item.amount);
                else
                    ItemContainer.QuickMove(inv, index, _openContainer as ItemContainer);
                Refresh();
                return;
            }
            if (Input.GetMouseButton(1))
            {
                // Сплит стека правой кнопкой
                var half = inv.SplitHalf(index);
                if (half != null)
                {
                    var target = _openContainer ?? (ItemContainer)inv;
                    target.TryAdd(half);
                }
                Refresh();
                return;
            }

            _dragging = slot;
            _dragIcon.enabled = true; _dragText.enabled = true;
            _dragText.text = item != null ? ItemDatabase.NameOf(item.id) : "";
            Refresh();
        }

        void OnContainerSlotClicked(int index)
        {
            if (_openContainer == null) return;
            var item = _openContainer.Get(index);

            if (_dragging != null)
            {
                DropInto(_dragging, _openContainer, index);
                _dragging = null; _dragIcon.enabled = false; _dragText.enabled = false;
                Refresh();
                return;
            }
            var inv = HudRuntime.Instance?.player?.inventory;
            if (inv == null) return;

            if (Input.GetKey(KeyCode.LeftShift) && item != null)
            {
                if (Networked)
                    Subsistence.Net.InventoryNet.RequestTake(HandleFor(_openContainer), index, item.amount);
                else
                    ItemContainer.QuickMove(_openContainer, index, inv);   // «быстрое лутание»
                Refresh();
                return;
            }
            _dragging = _containerSlots[index];
            _dragIcon.enabled = true; _dragText.enabled = true;
            _dragText.text = item != null ? ItemDatabase.NameOf(item.id) : "";
        }

        void DropInto(Slot from, ItemContainer target, int index)
        {
            if (from.container == null) return;

            // В сети содержимым контейнера владеет сервер: перетаскивание — тоже запрос ему.
            if (Networked)
            {
                bool fromMain = from.container == _openContainer;
                bool fromSec = _secondary != null && from.container == _secondary;
                bool toMain = target == _openContainer;
                bool toSec = _secondary != null && target == _secondary;
                if (fromMain || fromSec || toMain || toSec)
                {
                    var moved = from.container.Get(from.index);
                    if (moved != null && !moved.IsEmpty)
                    {
                        if (toMain) Subsistence.Net.InventoryNet.RequestStore(HandleFor(_openContainer), from.index, moved.amount);
                        else if (toSec) Subsistence.Net.InventoryNet.RequestStore(_openHandle.With(1), from.index, moved.amount);
                        else if (fromSec) Subsistence.Net.InventoryNet.RequestTake(_openHandle.With(1), from.index, moved.amount);
                        else Subsistence.Net.InventoryNet.RequestTake(HandleFor(_openContainer), from.index, moved.amount);
                    }
                    Refresh();
                    return;
                }
            }

            var src = from.container.Get(from.index);
            var dst = target.Get(index);

            if (src != null && dst != null && src.id == dst.id && src.CanStackWith(dst))
            {
                int left = target.TryAdd(src);
                from.container.Set(from.index, left > 0 ? new ItemStack(src.id, left) : null);
                return;
            }
            target.Set(index, src);
            from.container.Set(from.index, dst);
        }

        void Refresh()
        {
            var inv = HudRuntime.Instance?.player?.inventory;
            if (inv == null) return;

            for (int i = 0; i < _playerSlots.Count; i++)
            {
                var s = _playerSlots[i];
                s.container = inv;
                var item = inv.Get(i);
                s.label.text = item != null ? ItemDatabase.NameOf(item.id) : "";
                s.stack.text = item != null && item.amount > 1 ? item.amount.ToString() : "";
                // иконка предмета (alpha 1.0): показываем, если нарисована под этот id
                if (s.icon != null && !ItemIcons.Apply(s.icon, item?.id))
                {
                    s.bg.color = IsEquipSlot(i) ? new Color(0.14f, 0.12f, 0.06f, 0.9f) : new Color(0.1f, 0.12f, 0.1f, 0.85f);
                }
                else if (s.icon != null)
                {
                    s.bg.color = Color.Lerp(ItemIcons.RarityColor(item.id), Color.white, 0.08f);
                }
            }

            bool hasContainer = _openContainer != null;
            _gridContainer.parent.gameObject.SetActive(hasContainer);
            _gridContainer.gameObject.SetActive(hasContainer);
            if (hasContainer)
            {
                for (int i = 0; i < _containerSlots.Count; i++)
                {
                    var s = _containerSlots[i];
                    s.container = _openContainer;
                    var item = i < _openContainer.SlotCount ? _openContainer.Get(i) : null;
                    s.label.text = item != null ? ItemDatabase.NameOf(item.id) : "";
                    s.stack.text = item != null && item.amount > 1 ? item.amount.ToString() : "";
                    if (s.icon != null)
                    {
                        ItemIcons.Apply(s.icon, item?.id);
                        s.bg.color = item == null ? new Color(0.1f, 0.12f, 0.1f, 0.85f)
                                                  : Color.Lerp(ItemIcons.RarityColor(item.id), Color.white, 0.08f);
                    }
                    s.rt.gameObject.SetActive(i < _openContainer.SlotCount);
                }
            }

            // печь: вторая ёмкость (выход)
            bool hasSecondary = _secondary != null;
            _gridSecondary.gameObject.SetActive(hasSecondary);
            _secondaryTitle.gameObject.SetActive(hasSecondary);
            if (hasSecondary)
            {
                _secondaryTitle.text = _secondary.ContainerName;
                for (int i = 0; i < _secondarySlots.Count; i++)
                {
                    var s = _secondarySlots[i];
                    s.container = _secondary;
                    var item = i < _secondary.SlotCount ? _secondary.Get(i) : null;
                    s.label.text = item != null ? ItemDatabase.NameOf(item.id) : "";
                    s.stack.text = item != null && item.amount > 1 ? item.amount.ToString() : "";
                    s.rt.gameObject.SetActive(i < _secondary.SlotCount);
                }
            }
        }

        /// <summary>Клик по слоту второй ёмкости (у печи — выход).</summary>
        void OnSecondarySlotClicked(int index)
        {
            if (_secondary == null) return;
            var item = _secondary.Get(index);

            if (_dragging != null)
            {
                DropInto(_dragging, _secondary, index);
                _dragging = null; _dragIcon.enabled = false; _dragText.enabled = false;
                Refresh();
                return;
            }
            var inv = HudRuntime.Instance?.player?.inventory;
            if (inv == null) return;

            if (Input.GetKey(KeyCode.LeftShift) && item != null)
            {
                if (Networked) Subsistence.Net.InventoryNet.RequestTake(_openHandle.With(1), index, item.amount);
                else ItemContainer.QuickMove(_secondary, index, inv);
                Refresh();
                return;
            }
            _dragging = _secondarySlots[index];
            _dragIcon.enabled = true; _dragText.enabled = true;
            _dragText.text = item != null ? ItemDatabase.NameOf(item.id) : "";
        }

        static bool IsEquipSlot(int i) => i >= PlayerInventory.ArmorStart;
    }
}
