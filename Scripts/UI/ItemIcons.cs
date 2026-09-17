// ============================================================================
//  SUBSISTENCE — UI/ItemIcons.cs
//  Иконки предметов: Resources.Load<Sprite>("icons/<id>"), где точки заменены на «_»
//  (icons/rifle_ak.png). Иконки рисуются офлайн (tools/make_item_icons.py) — 96×96,
//  рамка цвета редкости, силуэт предмета, никакой возни с атласами.
//
//  Как пользоваться из UI:
//      var img = go.AddComponent<Image>();
//      ItemIcons.Apply(img, itemId);          // null-иконка → Image выключается, остаётся текст
//  Кэш: 180 спрайтов по ~5 КБ — не проблема, но всё равно держим словарь и не грузим
//  повторно (Resources.Load не бесплатный).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Subsistence.Core;

namespace Subsistence.UI
{
    public static class ItemIcons
    {
        const string Root = "icons/";
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>(256);
        static readonly HashSet<string> _missing = new HashSet<string>();

        public static int Found { get; private set; }
        public static int Requested { get; private set; }

        public static Sprite Get(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_cache.TryGetValue(itemId, out var cached)) return cached;
            if (_missing.Contains(itemId)) return null;

            Requested++;
            string file = Root + itemId.Replace('.', '_');
            var sp = Resources.Load<Sprite>(file);
            if (sp == null)
            {
                // часть иконок лежит как Texture (если Unity импортировала PNG не как Sprite)
                var tex = Resources.Load<Texture2D>(file);
                if (tex != null)
                {
                    sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
            if (sp == null) { _missing.Add(itemId); return null; }

            _cache[itemId] = sp;
            Found++;
            return sp;
        }

        /// <summary>Поставить иконку в Image. Нет иконки — Image выключается (в слоте останется текст).</summary>
        /// <summary>
        /// Создать внутри слота картинку-иконку (по центру, с отступом под рамку) и повесить её на слот.
        /// Раньше метод жил в HudRuntime как приватный — из InventoryUI он был не виден (CS0103).
        /// </summary>
        public static void AttachIcon(Slot slot)
        {
            if (slot == null || slot.rt == null || slot.icon != null) return;
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(slot.rt, false);
            slot.icon = iconGo.AddComponent<Image>();
            slot.icon.raycastTarget = false;
            var r = slot.icon.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(5, 5); r.offsetMax = new Vector2(-5, -5);
            slot.icon.enabled = false;
        }

        public static bool Apply(Image img, string itemId)
        {
            if (img == null) return false;
            var sp = Get(itemId);
            if (sp == null) { img.enabled = false; return false; }
            img.sprite = sp;
            img.enabled = true;
            img.preserveAspect = true;
            return true;
        }

        /// <summary>Цвет рамки по редкости — тем же цветом подкрашиваем фон слота.</summary>
        public static Color RarityColor(string itemId)
        {
            var def = ItemDatabase.Def(itemId);
            if (def == null) return new Color(0.2f, 0.22f, 0.2f, 0.85f);
            switch (def.rarity)
            {
                case Rarity.Common: return new Color(0.16f, 0.18f, 0.16f, 0.9f);
                case Rarity.Uncommon: return new Color(0.12f, 0.22f, 0.14f, 0.9f);
                case Rarity.Rare: return new Color(0.11f, 0.17f, 0.26f, 0.9f);
                case Rarity.VeryRare: return new Color(0.20f, 0.13f, 0.25f, 0.9f);
                case Rarity.Military: return new Color(0.24f, 0.18f, 0.07f, 0.9f);
                default: return new Color(0.24f, 0.09f, 0.18f, 0.9f);      // Anomalous
            }
        }

        public static void Clear()
        {
            _cache.Clear(); _missing.Clear(); Found = 0; Requested = 0;
        }

        public static string Stats() => $"иконок {Found}/{Requested} запрошено";
    }
}
