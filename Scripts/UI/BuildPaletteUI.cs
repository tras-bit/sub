// ============================================================================
//  SUBSISTENCE — UI/BuildPaletteUI.cs
//  Радиальное меню постройки (удержание Q): выбор элемента и тира, стоимость,
//  апгрейд/ремонт молотком, подсветка недоступного. Плюс панель «что я строю».
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Subsistence.Building;
using Subsistence.Core;

namespace Subsistence.UI
{
    public class BuildPaletteUI : MonoBehaviour
    {
        Canvas _canvas;
        RectTransform _root;
        Text _info;
        readonly List<Text> _rows = new List<Text>(8);
        int _selected;

        BuildController _build;

        void Start()
        {
            var player = HudRuntime.Instance?.player;
            if (player != null) _build = player.GetComponent<BuildController>();
            BuildUI();
            _root.gameObject.SetActive(false);
        }

        void BuildUI()
        {
            _canvas = UIStyle.CreateCanvas("BuildPaletteCanvas", 110, out _);
            _root = UIStyle.Panel(_canvas.transform, "BuildPalette", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  new Vector2(-220, -220), new Vector2(220, 220), new Color(0.02f, 0.03f, 0.02f, 0.9f));

            UIStyle.Label(_root, "ПОСТРОЙКА (Q)", 20, UIStyle.BackroomsYellow);
            for (int i = 0; i < 12; i++)
            {
                var t = UIStyle.Label(_root, "", 16, UIStyle.TermGreen, TextAnchor.UpperLeft);
                t.rectTransform.offsetMin = new Vector2(16, 0);
                t.rectTransform.offsetMax = new Vector2(-16, -40 - i * 21);
                _rows.Add(t);
            }
            _info = UIStyle.Label(_root, "", 15, UIStyle.TermGreenDim, TextAnchor.LowerLeft);
            _info.rectTransform.offsetMin = new Vector2(16, 30);
        }

        void Update()
        {
            if (_build == null)
            {
                var player = HudRuntime.Instance?.player;
                if (player != null) _build = player.GetComponent<BuildController>();
                return;
            }

            bool open = Input.GetKey(KeyCode.Q);
            if (_root.gameObject.activeSelf != open) _root.gameObject.SetActive(open);
            if (!open) return;

            var inv = _build.inventory;

            // выбор: 1-9, 0, «-», «=», «[», «]», «\» — 15 элементов (ответ 9в)
            var keys = new[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
                               KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
                               KeyCode.Minus, KeyCode.Equals, KeyCode.LeftBracket, KeyCode.RightBracket,
                               KeyCode.Backslash };
            for (int i = 0; i < keys.Length; i++)
                if (Input.GetKeyDown(keys[i])) _selected = i;

            var pieces = new[]
            {
                BuildPieceType.Foundation, BuildPieceType.FoundationTriangle, BuildPieceType.Wall,
                BuildPieceType.Doorway, BuildPieceType.Window, BuildPieceType.Floor,
                BuildPieceType.FloorTriangle, BuildPieceType.Stairs, BuildPieceType.Roof,
                BuildPieceType.Ramp, BuildPieceType.HighWall, BuildPieceType.Pillar,
                BuildPieceType.RampCorner, BuildPieceType.Railing, BuildPieceType.Shutters
            };
            var names = new[] { "Фундамент", "Фундамент-треуг.", "Стена", "Дверной проём", "Окно", "Перекрытие",
                                "Перекрытие-треуг.", "Лестница", "Крыша", "Пандус", "Высокая стена", "Столб",
                                "Рампа-угол", "Перила", "Ставни" };

            for (int i = 0; i < pieces.Length; i++)
            {
                var cost = BuildCosts.PlacementCost(pieces[i], _build.currentTier);
                bool can = inv != null && inv.CanAfford(cost);
                string costText = "";
                for (int c = 0; c < cost.Count; c++) costText += $"{ItemDatabase.NameOf(cost[c].id)} {cost[c].amount} ";
                _rows[i].text = $"<color={(i == _selected ? "#ffd23f" : can ? "#39ff6a" : "#ff6b5e")}>[{i + 1}] {names[i]}</color>  <size=13>{costText}</size>";
            }
            _info.text = $"Тир: {BuildGrades.Get(_build.currentTier).id}\n"
                       + "R — повернуть  •  ЛКМ — поставить  •  киянка: ПКМ — апгрейд/ремонт, удержание ПКМ — снести\n"
                       + "Мягкая сторона — та, с которой апгрейдили: по ней ближний бой бьёт ×2.5";
        }

        /// <summary>Апгрейд блока молотком (ПКМ) — вызывается из BuildController/игрока.</summary>
        public static void TryUpgradeLookedAt(Camera cam, PlayerInventory inv)
        {
            if (cam == null) return;
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 4f, Layers.Mask(Layers.Buildable), QueryTriggerInteraction.Ignore)) return;
            var block = hit.collider.GetComponentInParent<BuildBlock>();
            if (block == null) return;

            var next = BuildGrades.Next(block.tier);
            if (next == block.tier) { HudRuntime.ShowToast("Максимальный тир"); return; }
            var cost = BuildCosts.UpgradeCost(block.piece, next);
            if (inv == null || !inv.CanAfford(cost)) { HudRuntime.ShowToast("Не хватает ресурсов на апгрейд"); return; }
            inv.Pay(cost);
            block.Upgrade(next);
            HudRuntime.ShowToast($"Апгрейд до {BuildGrades.Get(next).id}");
        }
    }
}
