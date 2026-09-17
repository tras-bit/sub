// ============================================================================
//  SUBSISTENCE — UI/CodeLockUI.cs
//  16_doors: терминал кодового замка (в стиле игровой консоли).
//   • цифры 0-9 — ввод, Backspace — стереть, Enter — подтвердить, ESC — закрыть;
//   • КОД ПРОВЕРЯЕТ СЕРВЕР (Net/DeployNet.cs): у клиента кода нет вообще,
//     поэтому «НЕВЕРНЫЙ КОД» приходит ответом, а не считается на месте;
//   • C — смена кода (доступно тому, кому уже выдан доступ, как в Rust).
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using Subsistence.Building;

namespace Subsistence.UI
{
    public class CodeLockUI : MonoBehaviour
    {
        static CodeLockUI _instance;

        DoorDeployable _door;
        DoorLock _lock;
        string _buffer = "";
        bool _changeMode;
        Text _entry, _state;
        bool _open;

        public static bool IsOpen => _instance != null && _instance._open;

        public static void Open(DoorDeployable door, DoorLock l)
        {
            if (_instance == null)
            {
                var go = new GameObject("CodeLockUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CodeLockUI>();
            }
            _instance.Show(door, l);
        }

        public static void Close() { if (_instance != null) _instance.Hide(); }

        void Show(DoorDeployable door, DoorLock l)
        {
            _door = door; _lock = l;
            _buffer = ""; _changeMode = false;
            if (_entry == null) Build();
            _open = true;
            _entry.transform.root.gameObject.SetActive(true);

            ulong me = Net.NetworkBridge.Host?.LocalPlayerId ?? 0UL;
            bool granted = l != null && l.granted.Contains(me);
            _state.text = granted ? "ДОСТУП ЕСТЬ · C — СМЕНИТЬ КОД" : $"ЗАПЕРТО — {l.Label}";
            _state.color = granted ? UIStyle.TermGreenDim : UIStyle.Warning;
            Refresh();

            UIState.AnyMenuOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Hide()
        {
            _open = false;
            if (_entry != null) _entry.transform.root.gameObject.SetActive(false);
            UIState.AnyMenuOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Build()
        {
            CanvasScaler scaler;
            var canvas = UIStyle.CreateCanvas("CodeLockCanvas", 65, out scaler);

            UIStyle.Panel(canvas.transform, "Bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                          new Color(0.01f, 0.05f, 0.03f, 0.94f));

            var frame = UIStyle.Panel(canvas.transform, "Frame", new Vector2(0.30f, 0.34f), new Vector2(0.70f, 0.66f),
                                      Vector2.zero, Vector2.zero, UIStyle.PanelBg);
            frame.gameObject.AddComponent<Outline>().effectColor = UIStyle.TermGreenDim;

            UIStyle.Label(frame, "ТЕРМИНАЛ ЗАМКА", 26, UIStyle.TermGreenBright, TextAnchor.UpperCenter);
            _state = UIStyle.Label(frame, "", 16, UIStyle.Warning, TextAnchor.UpperCenter);
            _state.rectTransform.offsetMin = new Vector2(0f, -54f);

            _entry = UIStyle.Label(frame, "", 44, UIStyle.TermGreen, TextAnchor.MiddleCenter);
            _entry.rectTransform.offsetMin = new Vector2(0f, -10f);

            var hint = UIStyle.Label(frame, "0-9 ввод · BACKSPACE стереть · ENTER подтвердить · ESC выйти", 14,
                                     UIStyle.TermGreenDim, TextAnchor.LowerCenter);
            hint.rectTransform.offsetMax = new Vector2(0f, 10f);
        }

        void Refresh() => _entry.text = _buffer.PadRight(4, '-');

        void Update()
        {
            if (!_open || _door == null) return;

            if (Input.GetKeyDown(KeyCode.Escape)) { Hide(); return; }

            if (Input.GetKeyDown(KeyCode.Backspace) && _buffer.Length > 0)
            {
                _buffer = _buffer.Substring(0, _buffer.Length - 1);
                Refresh();
            }

            // C — смена кода (только если доступ уже выдан: владелец/после верного ввода)
            if (Input.GetKeyDown(KeyCode.C))
            {
                ulong me = Net.NetworkBridge.Host?.LocalPlayerId ?? 0UL;
                if (_lock != null && _lock.granted.Contains(me))
                {
                    _changeMode = true;
                    _buffer = "";
                    _state.text = "НОВЫЙ КОД: 4 ЦИФРЫ";
                    _state.color = UIStyle.TermGreenBright;
                    Refresh();
                }
                else
                {
                    _state.text = "СМЕНА КОДА — ТОЛЬКО С ДОСТУПОМ";
                    _state.color = UIStyle.DangerRed;
                }
            }

            // цифры — из inputString (ловит и numpad, и основную строку)
            string typed = Input.inputString;
            for (int i = 0; i < typed.Length && _buffer.Length < 4; i++)
            {
                char c = typed[i];
                if (c >= '0' && c <= '9') { _buffer += c; Refresh(); }
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Submit();
        }

        void Submit()
        {
            if (_buffer.Length < 4)
            {
                _state.text = "КОД: 4 ЦИФРЫ";
                _state.color = UIStyle.Warning;
                return;
            }

            string code = _buffer;
            _buffer = "";
            Refresh();

            if (_changeMode)
            {
                _changeMode = false;
                Net.DeployNet.RequestSetCode(_door, code);
                _state.text = "КОД ОБНОВЛЁН (только у тебя)";
                _state.color = UIStyle.TermGreen;
                return;
            }

            _state.text = "ПРОВЕРКА…";
            _state.color = UIStyle.TermGreenDim;
            Net.DeployNet.RequestCode(_door, code);      // ответ придёт через RPC: ON GRANTED / ON DENIED
        }

        // ================== ответы сервера (Net/DeployNet.cs) ==================

        /// <summary>Сервер подтвердил код/ключ: доступ выдан, дверь открывается.</summary>
        public static void OnGranted(DoorDeployable door)
        {
            if (_instance == null || !_instance._open || _instance._door != door) return;
            _instance._state.text = "ДОСТУП РАЗРЕШЁН";
            _instance._state.color = UIStyle.TermGreen;
            _instance._buffer = "";
            _instance.Hide();
        }

        /// <summary>Сервер отказал (неверный код / нет ключа / заперто).</summary>
        public static void OnDenied(DoorDeployable door, string reason)
        {
            if (_instance == null) return;
            if (_instance._open && _instance._door == door)
            {
                _instance._state.text = string.IsNullOrEmpty(reason) ? "ОТКАЗАНО" : reason;
                _instance._state.color = UIStyle.DangerRed;
                _instance._buffer = "";
                _instance.Refresh();
                return;
            }
            // терминал закрыт — пишем в тост (например, ключа нет)
            HudRuntime.ShowToast(reason, 2.5f);
        }
    }
}
