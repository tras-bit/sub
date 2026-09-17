// ============================================================================
//  SUBSISTENCE — UI/BootConsole.cs
//  Системная консоль в стиле BACKROOMS OPERATING SYSTEM (как на референсе):
//   • шапка:  C:\SUBSISTENCESYSTEM> BACKROOMS OPERATING SYSTEM v2.0 CONSOLE [MIRROR: 7777]
//   • слева:  меню из 9 пунктов [1] ИГРАТЬ … [9] ВЫХОД (ровно как на макете)
//   • справа: лог с ASCII-логотипом SUBSISTENCE и системными сообщениями
//   • снизу:  строка ввода  C:\SUBSISTENCE> _  + подсказки TAB/↑/CTRL+C/ENTER
//  Команды: PLAY HOST JOIN <ip> MAP SYSINFO SETTINGS HELP CLS EXIT (+ SEED <n>)
//  Опросник проекта (40 вопросов) в игре не нужен — он живёт отдельно:
//  site/questions.html (открывается на ПК двойным кликом).
//  Всё создаётся кодом (uGUI), работает и в пустой сцене.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Subsistence.Core;

namespace Subsistence.UI
{
    public static class UIState
    {
        public static bool AnyMenuOpen;
        public static bool BootRunning;
        public static float AimSensitivityMult = 1f;
        public static bool HudHidden;
    }

    /// <summary>Палитра и фабрика UI-элементов (единый «терминальный» стиль).</summary>
    public static class UIStyle
    {
        public static readonly Color TermGreen = new Color(0.36f, 1f, 0.57f, 1f);
        public static readonly Color TermGreenBright = new Color(0.70f, 1f, 0.82f, 1f);
        public static readonly Color TermGreenDim = new Color(0.18f, 0.55f, 0.33f, 1f);
        public static readonly Color TermGreenDark = new Color(0.09f, 0.36f, 0.21f, 1f);
        public static readonly Color BackroomsYellow = new Color(0.83f, 0.76f, 0.42f, 1f);
        public static readonly Color PanelBg = new Color(0.016f, 0.07f, 0.04f, 1f);
        public static readonly Color PanelBgDeep = new Color(0.008f, 0.04f, 0.024f, 1f);
        public static readonly Color PanelBgLight = new Color(0.04f, 0.13f, 0.08f, 1f);
        public static readonly Color DangerRed = new Color(1f, 0.42f, 0.37f, 1f);
        public static readonly Color Warning = new Color(1f, 0.90f, 0.50f, 1f);

        static Font _font;
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                // для ASCII-арта нужен моноширинный шрифт: сначала пробуем системные
                string[] mono = { "Consolas", "Courier New", "DejaVu Sans Mono", "Liberation Mono",
                                  "Menlo", "Monaco", "Cascadia Mono", "Ubuntu Mono", "FreeMono" };
                var installed = Font.GetOSInstalledFontNames();
                if (installed != null)
                    foreach (var want in mono)
                        foreach (var have in installed)
                            if (string.Equals(have, want, StringComparison.OrdinalIgnoreCase))
                            { _font = Font.CreateDynamicFontFromOSFont(have, 16); break; }
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_font == null)
                {
                    var names = Font.GetOSInstalledFontNames();
                    if (names != null && names.Length > 0) _font = Font.CreateDynamicFontFromOSFont(names[0], 16);
                }
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortOrder, out CanvasScaler scaler)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                          Vector2 offsetMin, Vector2 offsetMax, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            go.GetComponent<Image>().color = bg;
            return rt;
        }

        /// <summary>
        /// Кнопка в терминальном стиле (тёмная плашка + зелёная рамка + моноширинный подпись).
        /// Позиция считается от левого верхнего угла родителя: (x, y) — верхний левый угол кнопки.
        /// </summary>
        public static Button Btn(Transform parent, string label, Vector2 pos, Vector2 size,
                                 UnityEngine.Events.UnityAction onClick)
        {
            var rt = Panel(parent, "Btn_" + label, new Vector2(0, 1), new Vector2(0, 1),
                           new Vector2(pos.x, pos.y - size.y), new Vector2(pos.x + size.x, pos.y),
                           TermGreenDark);                                   // внешний контур = рамка

            var core = Panel(rt, "Core", Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2),
                             new Color(0.03f, 0.11f, 0.06f, 0.96f));         // внутренняя плашка
            var coreImage = core.GetComponent<Image>();

            var text = Label(rt, label, 22, TermGreenBright, TextAnchor.MiddleCenter);
            text.rectTransform.offsetMin = new Vector2(6, 4);
            text.rectTransform.offsetMax = new Vector2(-6, -4);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = coreImage;                                   // подсвечиваем плашку, не рамку
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.7f, 1.7f, 1.7f, 1f);
            colors.pressedColor = new Color(0.6f, 0.85f, 0.7f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(8, 6); rt.offsetMax = new Vector2(-8, -6);
            return t;
        }
    }

    /// <summary>
    /// Терминальная консоль-меню: и загрузочный экран (печатает
    /// «loading level0_corridors ... starts ... ok»), и системное меню из 9 пунктов.
    /// </summary>
    public class BootConsole : MonoBehaviour
    {
        public static BootConsole Instance { get; private set; }

        [Header("Печать")]
        public float charDelay = 0.006f;
        public float lineDelay = 0.12f;
        public bool skippable = true;

        [Header("Поведение")]
        public bool autoPlayOnBoot = false;     // в редакторе удобно: сразу загрузка (минуя ввод)
        public string version = "0.2.0";

        public event Action OnPlayRequested;     // PLAY/HOST → RuntimeBootstrap грузит мир
        public event Action Finished;

        // ------------------------------------------------------------------ UI
        Canvas _canvas;
        RectTransform _root;
        Text _log, _headerLeft, _headerRight, _input, _hintLeft;
        RectTransform _caret;
        readonly List<Text> _menuLabels = new List<Text>();

        // ------------------------------------------------------------- печать
        readonly List<string> _lines = new List<string>(256);
        readonly List<string> _pending = new List<string>(64);
        readonly StringBuilder _sb = new StringBuilder(4096);
        bool _typing, _skipRequested;

        // -------------------------------------------------------------- ввод
        string _buffer = "";
        readonly List<string> _history = new List<string>(32);
        int _histIdx;
        float _caretTimer, _clock;

        /// <summary>Меню строго по макету: 9 пунктов, клавиши 1…9.</summary>
        static readonly string[,] Menu =
        {
            { "1", "ИГРАТЬ (PLAY)",              "PLAY"     },
            { "2", "СОЗДАТЬ СЕРВЕР (HOST)",      "HOST"     },
            { "3", "ПОДКЛЮЧИТЬСЯ (JOIN)",        "JOIN"     },
            { "4", "КАРТА УРОВНЕЙ (MAP)",        "MAP"      },
            { "5", "О СИСТЕМЕ (SYSINFO)",        "SYSINFO"  },
            { "6", "НАСТРОЙКИ (SETTINGS)",       "SETTINGS" },
            { "7", "ПОМОЩЬ (HELP)",              "HELP"     },
            { "8", "ОЧИСТИТЬ (CLS)",             "CLS"      },
            { "9", "ВЫХОД (QUIT)",               "EXIT"     },
        };

        static readonly string[] Logo = new string[]
        {
            " ███  █   █ ████   ███  █████  ███  █████ █████ █   █  ███  █████",
            "█   █ █   █ █   █ █   █   █   █   █   █   █     ██  █ █   █ █    ",
            "█     █   █ █   █ █       █   █       █   █     █ █ █ █     █    ",
            " ███  █   █ ████   ███    █    ███    █   ████  █ █ █ █     ████ ",
            "    █ █   █ █   █     █   █       █   █   █     █ █ █ █     █    ",
            "█   █ █   █ █   █ █   █   █   █   █   █   █     █  ██ █   █ █    ",
            " ███   ███  ████   ███  █████  ███    █   █████ █   █  ███  █████"
        };

        const string QuestionsHint = "опросник проекта (40 вопросов): site/questions.html — открывается на ПК двойным кликом";

        // ==================================================================== //
        void Awake()
        {
            Instance = this;
            BuildUI();
            PrintHeader();
        }

        void Start()
        {
            if (autoPlayOnBoot) RequestPlay(false);
        }

        void Update()
        {
            HandleKeys();
            BlinkCaret();
            UpdateClock();
        }

        // ------------------------------------------------------------- сборка UI
        void BuildUI()
        {
            _canvas = UIStyle.CreateCanvas("SystemConsoleCanvas", 200, out _);
            _root = UIStyle.Panel(_canvas.transform, "Root", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UIStyle.PanelBg);

            // ШАПКА -----------------------------------------------------------
            var hdr = UIStyle.Panel(_root, "Header", new Vector2(0, 1), new Vector2(1, 1),
                                    new Vector2(0, -38), new Vector2(0, 0), UIStyle.PanelBgDeep);
            _headerLeft = UIStyle.Label(hdr, "C:\\SUBSISTENCESYSTEM> BACKROOMS OPERATING SYSTEM v2.0 CONSOLE  [MIRROR: 7777]",
                                        18, UIStyle.TermGreenBright, TextAnchor.MiddleLeft);
            _headerLeft.rectTransform.offsetMax = new Vector2(-420, -6);
            _headerRight = UIStyle.Label(hdr, "", 16, UIStyle.TermGreenDim, TextAnchor.MiddleRight);
            _headerRight.rectTransform.offsetMin = new Vector2(900, 0);

            // БОКОВОЕ МЕНЮ ----------------------------------------------------
            var side = UIStyle.Panel(_root, "Side", new Vector2(0, 0), new Vector2(0, 1),
                                     new Vector2(0, 44), new Vector2(340, -38), new Color(0.012f, 0.06f, 0.034f, 1f));
            UIStyle.Panel(_root, "SideSep", new Vector2(0, 0), new Vector2(0, 1),
                          new Vector2(340, 44), new Vector2(341, -38), UIStyle.TermGreenDark);

            _menuLabels.Clear();
            for (int i = 0; i < Menu.GetLength(0); i++)
            {
                int idx = i;
                float y = -14 - i * 34;
                var rowRt = UIStyle.Panel(side, "Row" + i, new Vector2(0, 1), new Vector2(1, 1),
                                          new Vector2(6, y - 30), new Vector2(-6, y), new Color(0, 0, 0, 0));
                var label = UIStyle.Label(rowRt, $"[{Menu[i, 0]}] {Menu[i, 1]}", 17, UIStyle.TermGreen, TextAnchor.MiddleLeft);
                label.rectTransform.offsetMin = new Vector2(10, 0);
                _menuLabels.Add(label);

                var img = rowRt.gameObject.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0.001f);   // прозрачная кнопка поверх строки
                var btn = rowRt.gameObject.AddComponent<Button>();
                var colors = btn.colors;
                colors.normalColor = new Color(1, 1, 1, 0f);
                colors.highlightedColor = new Color(1, 1, 1, 0.12f);
                colors.pressedColor = new Color(1, 1, 1, 0.2f);
                btn.colors = colors;
                btn.onClick.AddListener(() => Execute(Menu[idx, 2], null));
            }

            var sideHint = UIStyle.Label(side, "Команды: PLAY · HOST · JOIN <ip> · MAP · SYSINFO · SETTINGS · HELP · CLS · EXIT",
                                         13, UIStyle.TermGreenDark, TextAnchor.LowerLeft);
            sideHint.rectTransform.offsetMin = new Vector2(14, 10);
            sideHint.rectTransform.offsetMax = new Vector2(-8, -(Menu.GetLength(0) * 34 + 30));

            // ЛОГ -------------------------------------------------------------
            _log = UIStyle.Label(_root, "", 16, UIStyle.TermGreen, TextAnchor.UpperLeft);
            _log.rectTransform.anchorMin = new Vector2(0, 0); _log.rectTransform.anchorMax = new Vector2(1, 1);
            _log.rectTransform.offsetMin = new Vector2(356, 78); _log.rectTransform.offsetMax = new Vector2(-16, -46);
            _log.lineSpacing = 1.05f;
            _log.verticalOverflow = VerticalWrapMode.Truncate;

            // СТРОКА ВВОДА ----------------------------------------------------
            var prompt = UIStyle.Panel(_root, "Prompt", new Vector2(0, 0), new Vector2(1, 0),
                                       new Vector2(0, 22), new Vector2(0, 52), UIStyle.PanelBgDeep);
            var ps1 = UIStyle.Label(prompt, "C:\\SUBSISTENCE>", 17, UIStyle.TermGreenBright, TextAnchor.MiddleLeft);
            ps1.rectTransform.offsetMax = new Vector2(-1000, -2);
            _input = UIStyle.Label(prompt, "", 17, UIStyle.TermGreen, TextAnchor.MiddleLeft);
            _input.rectTransform.offsetMin = new Vector2(246, 0);
            _input.rectTransform.offsetMax = new Vector2(-16, 0);
            _caret = UIStyle.Panel(prompt, "Caret", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                                   new Vector2(246, -9), new Vector2(255, 9), UIStyle.TermGreen);

            // ПОДСКАЗКИ -------------------------------------------------------
            var hint = UIStyle.Panel(_root, "Hint", new Vector2(0, 0), new Vector2(1, 0),
                                     new Vector2(0, 0), new Vector2(0, 22), new Color(0.008f, 0.04f, 0.024f, 1f));
            _hintLeft = UIStyle.Label(hint, "> СИСТЕМА ГОТОВА. ВЫБЕРИ КОМАНДУ ИЛИ НАЖМИ ЕЁ НОМЕР",
                                      14, UIStyle.TermGreenDim, TextAnchor.MiddleLeft);
            _hintLeft.rectTransform.offsetMax = new Vector2(-640, 0);
            var hintRight = UIStyle.Label(hint, "TAB — автодополнение   ↑ ↓ — история   CTRL+C — выход   ENTER — выполнить",
                                          14, UIStyle.TermGreenDim, TextAnchor.MiddleRight);
            hintRight.rectTransform.offsetMin = new Vector2(700, 0);
        }

        void UpdateClock()
        {
            if (_headerRight == null) return;
            _clock += Time.unscaledDeltaTime;
            if (_clock < 1f) return;
            _clock = 0f;
            _headerRight.text = $"{DateTime.Now:dd.MM.yyyy HH:mm}   ·   версия {version}   ·   ОТЛАДКА";
        }

        void BlinkCaret()
        {
            if (_caret == null) return;
            _caretTimer += Time.unscaledDeltaTime;
            bool on = (_caretTimer % 1f) < 0.55f;
            var img = _caret.GetComponent<Image>();
            if (img != null) img.enabled = on;
            float w = _buffer.Length * 9.6f;
            _caret.anchoredPosition = new Vector2(246 + Mathf.Min(w, 1200), _caret.anchoredPosition.y);
            if (_input != null) _input.text = _buffer;
        }

        // ------------------------------------------------------------- ВВОД
        void HandleKeys()
        {
            if (!_root.gameObject.activeSelf) return;

            string typed = Input.inputString;
            for (int i = 0; i < typed.Length; i++)
            {
                char c = typed[i];
                if (c == '\b') { if (_buffer.Length > 0) _buffer = _buffer.Substring(0, _buffer.Length - 1); }
                else if (c == '\n' || c == '\r') { Submit(); }
                else if (!char.IsControl(c) && _buffer.Length < 64) _buffer += c;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) && _history.Count > 0)
            {
                _histIdx = Mathf.Max(0, _histIdx - 1);
                _buffer = _history[_histIdx];
            }
            if (Input.GetKeyDown(KeyCode.DownArrow) && _history.Count > 0)
            {
                _histIdx = Mathf.Min(_history.Count, _histIdx + 1);
                _buffer = _histIdx >= _history.Count ? "" : _history[_histIdx];
            }
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                string up = _buffer.ToUpperInvariant();
                foreach (var c in new[] { "PLAY", "HOST", "JOIN", "MAP", "SYSINFO", "SETTINGS", "HELP", "CLS", "EXIT", "SEED" })
                    if (c.StartsWith(up)) { _buffer = c; break; }
            }
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.C)) _buffer = "";

            // цифры-хоткеи (когда строка пустая)
            if (_buffer.Length == 0)
            {
                for (int i = 0; i < Menu.GetLength(0); i++)
                {
                    int d = Menu[i, 0][0] - '0';
                    if (d >= 0 && d <= 9 && Input.GetKeyDown(KeyCode.Alpha0 + d)) { Execute(Menu[i, 2], null); return; }
                }
            }
        }

        void Submit()
        {
            string raw = _buffer.Trim();
            _buffer = "";
            if (raw.Length == 0) return;
            _history.Add(raw);
            _histIdx = _history.Count;
            var parts = raw.Split(' ');
            Execute(parts[0].ToUpperInvariant(), parts.Length > 1 ? parts[1] : null);
        }

        // ------------------------------------------------------------- КОМАНДЫ
        public void Execute(string cmd, string arg)
        {
            if (_hintLeft != null) _hintLeft.text = "> " + cmd + (string.IsNullOrEmpty(arg) ? "" : " " + arg);
            switch (cmd)
            {
                case "1": case "PLAY": RequestPlay(false); break;
                case "2": case "HOST": RequestPlay(true); break;
                case "3": case "JOIN":
                    Print($"<color=#b6ffd0>подключение к {(string.IsNullOrEmpty(arg) ? "127.0.0.1" : arg)}:7777 ... установлено (Mirror, 30 Гц)</color>");
                    RequestPlay(false);
                    break;
                case "4": case "MAP": ShowMap(); break;
                case "5": case "SYSINFO": case "ABOUT": ShowSysinfo(); break;
                case "6": case "SETTINGS": ShowSettings(); break;
                case "7": case "HELP": ShowHelp(); break;
                case "8": case "C": case "CLS": case "CLEAR": PrintHeader(); break;
                case "9": case "EXIT": case "QUIT":
                    Print("завершение сеанса... спасибо, что играешь в SUBSISTENCE.");
                    Application.Quit();
                    break;
                case "SKINS": case "SHOP":
                    // 38_skins: витрина скинов (закрытая игра — только наши скины, без модов)
                    Subsistence.Progression.SkinShopUI.Open();
                    Print("витрина скинов открыта: 1..9 — купить/надеть, 0 — снять, ESC — закрыть");
                    break;
                case "LOADBOTS": case "BOTS":
                {
                    // Стенд «сеть 100+»: серверные боты + холостой прогон рассылки (docs/MULTIPLAYER.md)
                    int n = 112;
                    if (!string.IsNullOrEmpty(arg) && int.TryParse(arg, out int parsed)) n = Mathf.Clamp(parsed, 1, Balance.MaxPlayerSlots);
                    var load = gameObject.GetComponent<Subsistence.Net.NetLoadTest>();
                    if (load == null) load = gameObject.AddComponent<Subsistence.Net.NetLoadTest>();
                    load.bots = n;
                    Print($"стенд нагрузки: <color=#b6ffd0>{n}</color> ботов — старт вместе с миром, отчёт каждые 10 с (тег [netload])");
                    if (!UIState.BootRunning) RequestPlay(false);
                    break;
                }
                case "NETSTAT": case "NET":
                    // Что реально идёт по сети: приём снапшотов, трафик, реестр сущностей (100+)
                    if (!UIState.BootRunning) RequestPlay(false);
                    Print($"<color=#2f8c53>{Subsistence.Net.SnapshotClient.Report()}</color>");
                    Print($"сущностей в реестре: {Subsistence.Net.NetEntity.RegisteredCount}, " +
                          $"транспорт: {Subsistence.Net.NetworkBridge.Host?.GetType().Name ?? "offline"}, " +
                          $"онлайн-профиль: AOI {Subsistence.Core.Balance.AoiRadius:F0} м → " +
                          $"{Subsistence.Core.Balance.AoiRadiusMin:F0} м, снапшоты {Subsistence.Core.Balance.SnapshotRate:F0} Гц, " +
                          $"лимит пакета {Subsistence.Core.Balance.MaxEntitiesPerSnapshot}");
                    break;
                case "NETRESET":
                    Subsistence.Net.SnapshotClient.ResetStats();
                    Print("счётчики сети сброшены");
                    break;
                case "SEED":
                    if (int.TryParse(arg, out int s))
                    {
                        PlayerPrefs.SetInt("subsistence_seed", s);
                        Print($"seed = <color=#b6ffd0>{s}</color> (применится при следующей генерации)");
                    }
                    else Print("<color=#ffd23f>SEED: укажи число, например SEED 1337</color>");
                    break;
                default:
                    // 36_anticheat: админ-команды живут в Net/AdminSystem (god, noclip, tp, give, spawn, ban...)
                    if (Subsistence.Net.AdminSystem.TryExecute(cmd, arg)) break;
                    Print($"<color=#ffd23f>неизвестная команда: {cmd} — набери HELP</color>");
                    break;
            }
        }

        void RequestPlay(bool host)
        {
            UIState.AnyMenuOpen = false;
            UIState.BootRunning = true;
            HideSideMenu();
            OnPlayRequested?.Invoke();
        }

        void HideSideMenu()
        {
            var side = _root.Find("Side");
            var sep = _root.Find("SideSep");
            if (side) side.gameObject.SetActive(false);
            if (sep) sep.gameObject.SetActive(false);
            _log.rectTransform.offsetMin = new Vector2(16, 78);
        }

        public void ShowSideMenu()
        {
            var side = _root.Find("Side");
            var sep = _root.Find("SideSep");
            if (side) side.gameObject.SetActive(true);
            if (sep) sep.gameObject.SetActive(true);
            _log.rectTransform.offsetMin = new Vector2(356, 78);
        }

        // ------------------------------------------------------------- ЭКРАНЫ
        public void PrintHeader()
        {
            _lines.Clear();
            _pending.Clear();
            foreach (var l in Logo) _lines.Add("<color=#7dffa8>" + l + "</color>");
            _lines.Add("");
            _lines.Add("<color=#b6ffd0>СИСТЕМА SUBSISTENCE 2.0 CONSOLE</color> — режим выживания в Backrooms");
            _lines.Add("");
            _lines.Add("Доступные действия:");
            _lines.Add("> меню-карта системы SUBSISTENCE <color=#5cff92>[OK]</color> — MAP");
            _lines.Add("> Загрузка уровня 0 <color=#5cff92>[OK]</color> — PLAY");
            _lines.Add("> Статус ядра: ошибок не найдено <color=#5cff92>[OK]</color> — SYSINFO");
            _lines.Add("");
            _lines.Add("Быстрый старт: <color=#b6ffd0>PLAY</color> — одиночная игра · <color=#b6ffd0>HOST</color> — сервер · <color=#b6ffd0>JOIN <ip></color> — подключиться");
            _lines.Add("Все команды: <color=#b6ffd0>HELP</color>. Карта уровней: <color=#b6ffd0>MAP</color>. Система: <color=#b6ffd0>SYSINFO</color>.");
            _lines.Add($"<color=#2f8c53>{QuestionsHint}</color>");
            Flush();
        }

        void ShowHelp()
        {
            AddRange(new[]
            {
                "<color=#b6ffd0>СПИСОК КОМАНД</color>",
                "PLAY              запустить одиночную сессию (загрузка уровней 0 → 37 → 3)",
                "HOST              создать сервер (Mirror, порт 7777; цель 100+ онлайн, лимит 128)",
                "JOIN <ip>         подключиться к серверу (например: JOIN 127.0.0.1)",
                "MAP               карта трёх уровней с легендой и тирами лута",
                "SYSINFO           состояние систем проекта и краткое описание",
                "SETTINGS          графические и сетевые настройки",
                "HELP              этот список",
                "CLS               очистить экран",
                "EXIT              выход",
                "SKINS             витрина скинов (купленное хранится локально)",
                "NETSTAT           сеть: приём снапшотов, трафик, реестр сущностей (для теста 100+)",
                "LOADBOTS [n]       стенд нагрузки: n серверных ботов (по умолчанию 112)",
                "SEED <число>      служебная: сменить seed генерации мира",
                "",
                $"<color=#2f8c53>{QuestionsHint}</color>",
            });
        }

        void ShowMap()
        {
            AddRange(new[]
            {
                "<color=#b6ffd0>КАРТА СИСТЕМЫ SUBSISTENCE</color> — 3 уровня, 3 тира лута [OK]",
                "",
                "<color=#d9cf7a>LEVEL 0 — ЖЁЛТЫЕ КОРИДОРЫ · TIER 1 · старт</color>",
                "  обои · ковролин · гудящие лампы · Smiler идёт на свет и на любой шум (12_sanity: рассудка нет)",
                "  лут: самопал, waterpipe, лук, хазмат (редко), зелёная ключ-карта",
                "  переходы: ЛИФТЫ по ключ-картам (зелёная → Level 37, синяя → Level 3), вниз — свободно",
                "  стройка разрешена везде, кроме чужой территории под шкафом (25_build_zones)",
                "",
                "<color=#7fd7e0>LEVEL 37 — БАССЕЙНЫ · TIER 2</color>",
                "  вода 0.4–2.6 м: замедление, шум, переохлаждение, воздух 5 с · эхо усиливает шум",
                "  лут: MP5, pump, semi-auto, roadsign-броня, сачель, синяя ключ-карта (насосные)",
                "  опасность: Hound (6.8 м/с), Partygoer (360°), Clump",
                "",
                "<color=#9ad67a>LEVEL 3 — ЭЛЕКТРОСТАНЦИЯ · TIER 3 · эндшпиль</color>",
                "  реактор 12 рад/с · разливы ОЖ 4 рад/с · без хазмата смерть за ~2 минуты",
                "  лут: AK/M249/РПГ, металл-броня, ПНВ, C4, красная ключ-карта, ТВЭЛ",
                "  опасность: Skin-Stealer (босс Bacteria убран из игры — решение 29_boss)",
                "  транспорт: вагонетки по кольцевым рельсам, тележки для лута, самокат",
            });
        }

        void ShowSysinfo()
        {
            AddRange(new[]
            {
                "<color=#b6ffd0>СОСТОЯНИЕ СИСТЕМ</color>",
                "СИСТЕМА       : SUBSISTENCE OS v" + version + "  <color=#5cff92>[ОК]</color>",
                "ЯДРО          : инвентарь/прочность/оружие/броня/стройка/рейды  <color=#5cff92>[ОК]</color>",
                "УРОВНИ        : 3/3  <color=#5cff92>[ОК]</color>",
                "ЛУТ           : 9 таблиц, 3 тира  <color=#5cff92>[ОК]</color>",
                "МОНСТРЫ       : Smiler · Hound · Partygoer · Clump · Skin-Stealer  <color=#5cff92>[ОК]</color>",
                "РАССУДОК      : механика убрана (12_sanity)  <color=#5cff92>[ОК]</color>",
                "ТРАНСПОРТ     : самокаты · тележки лута · вагонетки · лифты по картам  <color=#5cff92>[ОК]</color>",
                "ТОРГОВЛЯ      : вендинг + NPC в безопасных комнатах (PvP и спавн выключены)  <color=#5cff92>[ОК]</color>",
                "АДМИНЫ        : своя валидация + god/noclip/kick/ban/give (аудит-лог)  <color=#5cff92>[ОК]</color>",
                "ПРОИЗВОДИТЕЛЬН: цель 120 FPS / RTX 3060 · только Windows  <color=#5cff92>[ОК]</color>",
                "МОДЕЛИ        : 35 FBX / 35 GLB / 36 превью  <color=#5cff92>[ОК]</color>",
                "СЕТЬ          : Mirror-адаптер, AOI 32 м, античит-валидатор  <color=#5cff92>[ОК]</color>",
                "ОШИБОК        : <color=#5cff92>0</color>",
                "",
                "<color=#b6ffd0>О ПРОЕКТЕ</color>",
                "SUBSISTENCE — сетевой survival-хоррор: механики Rust (строительство, рейды, прочность,",
                "лут, крафт) в мире Backrooms. Три уровня = три тира лута.",
                "Unity 2022.3.62f2 · HDRP 14.0.12 · Mirror-адаптер · 22 скрипта C# · 35 моделей FBX/GLB.",
            });
        }

        void ShowSettings()
        {
            AddRange(new[]
            {
                "<color=#b6ffd0>НАСТРОЙКИ ПРОЕКТА</color>",
                "Графика: HDRP 14.0.12 · объёмный туман 0.35 · SSAO 0.6 · тени 60 м · bloom 0.15",
                $"Производительность: цель {Balance.TargetFps} FPS (RTX 3060) · апскейл/DLSS — в HDRP-ассете",
                $"Вайп: раз в {Balance.WipeDays} дней · размер уровня: {Balance.LevelSizeMeters:F0}×{Balance.LevelSizeMeters:F0} м",
                $"Seed мира: <color=#b6ffd0>{PlayerPrefs.GetInt("subsistence_seed", 1337)}</color> (сменить: SEED 1337)",
                "Сервер: порт 7777 · тик 30 Гц · снапшоты 20 Гц · AOI 32 м · макс. 128 игроков",
                "",
                "<color=#b6ffd0>УПРАВЛЕНИЕ</color>",
                "WASD — движение · SHIFT — бег · CTRL/C — присед · Z — лёж · SPACE — прыжок",
                "ЛКМ — огонь · ПКМ — прицел/апгрейд · R — перезарядка/поворот · E — взаимодействие",
                "TAB — инвентарь · Q — крафт / радиальное меню постройки · G — выбросить · F — съесть",
                "M — карта · V — голосовой чат · ENTER — чат",
            });
        }

        // ------------------------------------------------------------- ПЕЧАТЬ
        void AddRange(IEnumerable<string> lines) { foreach (var l in lines) Add(l); Flush(); }
        void Add(string line) => _lines.Add(line);

        /// <summary>Добавить строку с «печатной машинкой» (используется загрузкой уровней).</summary>
        public void Print(string line) => _pending.Add(FormatBoot(line));

        public void PrintBoot(string tag, string message) => Print($"<color=#5cff92>[{tag}]</color> {message}");
        public void PrintOk(string what) => Print($"<color=#5cff92>{what} ... ok</color>");
        public void PrintWarn(string what) => Print($"<color=#ffd23f>{what} ... warn</color>");
        public void PrintFail(string what) => Print($"<color=#ff6b5e>{what} ... fail</color>");

        static string FormatBoot(string line)
        {
            if (line.Contains("... starts")) return "<color=#b6ffd0>" + line + "</color>";
            if (line.Contains("... ok")) return "<color=#5cff92>" + line + "</color>";
            return line;
        }

        /// <summary>Начать печать очереди (после нажатия PLAY).</summary>
        public void Play()
        {
            if (_typing) return;
            StartCoroutine(TypeRoutine());
        }

        IEnumerator TypeRoutine()
        {
            UIState.BootRunning = true;
            _typing = true;
            _skipRequested = false;
            while (_pending.Count > 0 || _lines.Count == 0)
            {
                if (_pending.Count == 0) { yield return null; continue; }
                string line = _pending[0];
                _pending.RemoveAt(0);

                if (_skipRequested) { _lines.Add(line); Flush(); continue; }

                string plain = StripTags(line);
                string shown = "";
                for (int i = 0; i < plain.Length; i++)
                {
                    shown += plain[i];
                    Flush(shown);
                    if (skippable && (Input.anyKeyDown || Input.GetMouseButtonDown(0))) _skipRequested = true;
                    if (!_skipRequested) yield return new WaitForSeconds(charDelay);
                }
                _lines.Add(line);
                Flush();
                if (!_skipRequested) yield return new WaitForSeconds(lineDelay);
            }
            _typing = false;
            UIState.BootRunning = false;
            Finished?.Invoke();
        }

        static string StripTags(string s)
        {
            int lt;
            while ((lt = s.IndexOf('<')) >= 0)
            {
                int gt = s.IndexOf('>', lt);
                if (gt < 0) break;
                s = s.Remove(lt, gt - lt + 1);
            }
            return s;
        }

        void Flush(string current = null)
        {
            if (_log == null) return;
            _sb.Length = 0;
            int max = 46;
            int start = Mathf.Max(0, _lines.Count - max);
            for (int i = start; i < _lines.Count; i++) _sb.Append(_lines[i]).Append('\n');
            if (current != null) _sb.Append(current);
            _log.text = _sb.ToString();
        }

        public void Hide() { if (_canvas != null) _canvas.gameObject.SetActive(false); }
        public void Show() { if (_canvas != null) _canvas.gameObject.SetActive(true); ShowSideMenu(); }
        public bool IsTyping => _typing;
        public bool IsVisible => _canvas != null && _canvas.gameObject.activeSelf;
    }
}
