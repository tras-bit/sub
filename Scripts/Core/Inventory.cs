// ============================================================================
//  SUBSISTENCE — Core/Inventory.cs
//  Инвентарь как в Rust: 6 хотбар + 24 рюкзак + 6 слотов одежды, стаки,
//  quick-move (Shift+ЛКМ), «забрать всё», разделение стека, выброс, крафт-очередь.
//  Контейнеры (ящики, трупы, шкафы) — те же правила, другая ёмкость.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Subsistence.Core
{
    /// <summary>Любой контейнер предметов: рюкзак игрока, ящик, труп, печь, верстак.</summary>
    public interface IItemContainer
    {
        string ContainerName { get; }
        int SlotCount { get; }
        ItemStack Get(int index);
        void Set(int index, ItemStack stack);
        bool CanAccept(ItemStack stack);          // напр. печь — только топливо/руда
        event Action<IItemContainer> Changed;
    }

    /// <summary>Базовый контейнер с сетевым «dirty»-флагом (для снапшотов).</summary>
    public class ItemContainer : IItemContainer
    {
        public string ContainerName { get; protected set; }
        public ItemStack[] Slots { get; protected set; }
        public int SlotCount => Slots.Length;
        readonly HashSet<ItemCategory> _filter = new HashSet<ItemCategory>();

        /// <summary>Битовая маска изменённых слотов — сервер шлёт только дельту.</summary>
        public ulong DirtyMask { get; private set; }
        public int Version { get; private set; }

        public event Action<IItemContainer> Changed;

        public ItemContainer(string name, int slots, params ItemCategory[] allowed)
        {
            ContainerName = name;
            Slots = new ItemStack[slots];
            if (allowed != null) foreach (var c in allowed) _filter.Add(c);
        }

        public ItemStack Get(int i) => (i < 0 || i >= Slots.Length) ? null : Slots[i];

        public virtual void Set(int i, ItemStack s)
        {
            if (i < 0 || i >= Slots.Length) return;
            Slots[i] = (s != null && s.IsEmpty) ? null : s;
            MarkDirty(i);
        }

        protected void MarkDirty(int i)
        {
            if (i >= 0 && i < 64) DirtyMask |= 1UL << i;
            else DirtyMask = ulong.MaxValue;
            Version++;
            Changed?.Invoke(this);
        }

        public void ClearDirty() => DirtyMask = 0;

        /// <summary>Событие Changed можно вызывать только из ItemContainer — наследники зовут это.</summary>
        protected void RaiseChanged() => Changed?.Invoke(this);

        public virtual bool CanAccept(ItemStack s) => s != null && !s.IsEmpty && (_filter.Count == 0 || _filter.Contains(s.Def?.category ?? ItemCategory.Misc));

        public bool IsEmpty
        {
            get { for (int i = 0; i < Slots.Length; i++) if (Slots[i] != null && !Slots[i].IsEmpty) return false; return true; }
        }

        public virtual int CountOf(string id)
        {
            int n = 0;
            for (int i = 0; i < Slots.Length; i++) if (Slots[i] != null && Slots[i].id == id) n += Slots[i].amount;
            return n;
        }

        /// <summary>Добавить предмет(ы). Возвращает остаток, который не влез (0 = всё влезло).</summary>
        public int TryAdd(ItemStack stack)
        {
            if (stack == null || stack.IsEmpty || !CanAccept(stack)) return stack?.amount ?? 0;
            var def = stack.Def;
            int max = def?.stackSize ?? 1;

            // 1) сначала докладываем в существующие стаки
            if (max > 1)
            {
                for (int i = 0; i < Slots.Length && stack.amount > 0; i++)
                {
                    var s = Slots[i];
                    if (s == null || s.id != stack.id || s.amount >= max) continue;
                    if (!s.CanStackWith(stack)) continue;
                    int can = Mathf.Min(max - s.amount, stack.amount);
                    s.amount += can; stack.amount -= can;
                    MarkDirty(i);
                }
            }
            // 2) затем в свободные слоты
            for (int i = 0; i < Slots.Length && stack.amount > 0; i++)
            {
                if (Slots[i] != null && !Slots[i].IsEmpty) continue;
                int put = Mathf.Min(max, stack.amount);
                var copy = stack.Clone(); copy.amount = put;
                Set(i, copy);
                stack.amount -= put;
            }
            return stack.amount;
        }

        public bool TryAddAmount(string id, int amount)
        {
            var s = new ItemStack(id, amount);
            return TryAdd(s) == 0;
        }

        /// <summary>Списать количество. true — списано полностью.</summary>
        public bool RemoveAmount(string id, int amount)
        {
            if (CountOf(id) < amount) return false;
            for (int i = 0; i < Slots.Length && amount > 0; i++)
            {
                var s = Slots[i];
                if (s == null || s.id != id) continue;
                int take = Mathf.Min(s.amount, amount);
                s.amount -= take; amount -= take;
                if (s.amount <= 0) Set(i, null); else MarkDirty(i);
            }
            return true;
        }

        /// <summary>Список затрат крафта/постройки: (id, amount).</summary>
        public bool CanAfford(IList<ItemStack> cost)
        {
            if (cost == null) return true;
            for (int i = 0; i < cost.Count; i++)
                if (CountOf(cost[i].id) < cost[i].amount) return false;
            return true;
        }

        public bool Pay(IList<ItemStack> cost)
        {
            if (!CanAfford(cost)) return false;
            for (int i = 0; i < cost.Count; i++) RemoveAmount(cost[i].id, cost[i].amount);
            return true;
        }

        public int FirstEmptySlot()
        {
            for (int i = 0; i < Slots.Length; i++) if (Slots[i] == null || Slots[i].IsEmpty) return i;
            return -1;
        }

        /// <summary>Обмен двух слотов (drag&drop, в т.ч. между контейнерами).</summary>
        public virtual void Swap(int a, int b)
        {
            var ta = Get(a); var tb = Get(b);
            Set(a, tb); Set(b, ta);
        }

        /// <summary>Разделить стек пополам (правая кнопка в Rust).</summary>
        public ItemStack SplitHalf(int index)
        {
            var s = Get(index);
            if (s == null || s.amount < 2) return null;
            int half = s.amount / 2;
            s.amount -= half;
            MarkDirty(index);
            var copy = s.Clone(); copy.amount = half;
            return copy;
        }

        /// <summary>Быстрый перенос: Shift+ЛКМ — в первый подходящий слот другого контейнера.</summary>
        public static bool QuickMove(ItemContainer from, int fromIndex, ItemContainer to)
        {
            var s = from.Get(fromIndex);
            if (s == null || s.IsEmpty) return false;
            if (!to.CanAccept(s)) return false;
            int left = to.TryAdd(s);
            if (left == 0) { from.Set(fromIndex, null); return true; }
            if (left < s.amount) { s.amount = left; from.Set(fromIndex, s); return true; } // частично
            return false;
        }

        /// <summary>«Забрать всё» из контейнера в инвентарь (как Loot All в Rust).</summary>
        public int TransferAllFrom(ItemContainer src)
        {
            int moved = 0;
            for (int i = 0; i < src.SlotCount; i++)
            {
                var s = src.Get(i);
                if (s == null || s.IsEmpty) continue;
                if (!CanAccept(s)) continue;
                int left = TryAdd(s);
                if (left == 0) { src.Set(i, null); moved++; }
                else if (left < s.amount) { s.amount = left; src.Set(i, s); moved++; }
            }
            return moved;
        }

        /// <summary>Сортировка: ресурсы → еда → медицина → патроны → оружие → броня → прочее.</summary>
        public void SortByCategory()
        {
            var list = new List<ItemStack>();
            for (int i = 0; i < Slots.Length; i++) if (Slots[i] != null && !Slots[i].IsEmpty) list.Add(Slots[i]);
            list.Sort((a, b) => Order(a.Def).CompareTo(Order(b.Def)));
            for (int i = 0; i < Slots.Length; i++) Slots[i] = i < list.Count ? list[i] : null;
            MarkDirty(-1);
        }

        static int Order(ItemDef d)
        {
            if (d == null) return 99;
            switch (d.category)
            {
                case ItemCategory.Resource: return 0;
                case ItemCategory.Food:
                case ItemCategory.Drink: return 1;
                case ItemCategory.Medical: return 2;
                case ItemCategory.Ammo: return 3;
                case ItemCategory.Weapon:
                case ItemCategory.Tool: return 4;
                case ItemCategory.Attachment: return 5;
                case ItemCategory.Armor: return 6;
                case ItemCategory.Building:
                case ItemCategory.Deployable: return 7;
                case ItemCategory.Explosive: return 8;
                default: return 9;
            }
        }

        /// <summary>Серийное сохранение в JSON (сейв игрока/сундука).</summary>
        public string Serialize()
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append('[');
            for (int i = 0; i < Slots.Length; i++)
            {
                if (i > 0) sb.Append(',');
                var s = Slots[i];
                sb.Append(s == null || s.IsEmpty ? "null" : JsonUtility.ToJson(s));
            }
            sb.Append(']');
            return sb.ToString();
        }
    }

    /// <summary>
    /// Инвентарь игрока: 0..5 хотбар, 6..29 рюкзак, 30..35 одежда (Head,Face,Chest,Legs,Hands,Feet).
    /// Одежда хранится в отдельных слотах, но «броня» суммирует резисты по всем надетым.
    /// </summary>
    public class PlayerInventory : ItemContainer
    {
        public const int HotbarSlots = Balance.HotbarSlots;              // 6
        public const int BackpackSlots = Balance.BackpackSlots;          // 24
        public const int ArmorSlots = 6;
        public const int TotalSlots = HotbarSlots + BackpackSlots + ArmorSlots; // 36

        public int ActiveHotbarIndex { get; private set; }

        readonly Dictionary<EquipSlot, int> _equipMap = new Dictionary<EquipSlot, int>
        {
            { EquipSlot.Head,  30 }, { EquipSlot.Face, 31 }, { EquipSlot.Chest, 32 },
            { EquipSlot.Legs,  33 }, { EquipSlot.Hands, 34 }, { EquipSlot.Feet, 35 }
        };

        public const int BackpackStart = HotbarSlots;                    // 6
        public const int ArmorStart = HotbarSlots + BackpackSlots;       // 30

        public PlayerInventory() : base("Backpack", TotalSlots) { }

        public event Action<int> ActiveSlotChanged;

        public ItemStack ActiveItem => Get(ActiveHotbarIndex);

        public void SelectHotbar(int index)
        {
            index = Mathf.Clamp(index, 0, HotbarSlots - 1);
            if (index == ActiveHotbarIndex) return;
            ActiveHotbarIndex = index;
            ActiveSlotChanged?.Invoke(index);
            RaiseChanged();
        }

        public void ScrollHotbar(float delta)
        {
            if (Mathf.Abs(delta) < 0.01f) return;
            int dir = delta > 0 ? 1 : -1;
            int n = (ActiveHotbarIndex + dir + HotbarSlots) % HotbarSlots;
            SelectHotbar(n);
        }

        /// <summary>Надеть предмет (перетаскивание в слот брони). Возвращает вытесненный предмет.</summary>
        public ItemStack Equip(EquipSlot slot, ItemStack item)
        {
            if (!_equipMap.TryGetValue(slot, out int idx)) return item;
            var prev = Get(idx);
            Set(idx, item);
            return prev;
        }

        public ItemStack GetEquipped(EquipSlot slot) => _equipMap.TryGetValue(slot, out int i) ? Get(i) : null;

        /// <summary>Надет ли предмет (маска ныряльщика, диэлектрические перчатки) — для эффектов.</summary>
        public bool IsWorn(string itemId)
        {
            foreach (var kv in _equipMap)
            {
                var st = Get(kv.Value);
                if (st != null && st.id == itemId && st.amount > 0) return true;
            }
            return false;
        }

        /// <summary>Есть в карманах или надет — «всё равно чем» (Rust-проверки экипировки).</summary>
        public bool Owns(string itemId) => CountOf(itemId) > 0 || IsWorn(itemId);

        public IEnumerable<ItemStack> AllEquipped()
        {
            foreach (var kv in _equipMap)
            {
                var s = Get(kv.Value);
                if (s != null && !s.IsEmpty) yield return s;
            }
        }

        /// <summary>Собрать всё в один рюкзак (перед смертью — чтобы высыпать лут-мешок).</summary>
        public List<ItemStack> AllItems()
        {
            var res = new List<ItemStack>(TotalSlots);
            for (int i = 0; i < SlotCount; i++) { var s = Get(i); if (s != null && !s.IsEmpty) res.Add(s); }
            return res;
        }

        public void ClearAll()
        {
            for (int i = 0; i < SlotCount; i++) Set(i, null);
            ActiveHotbarIndex = 0;
        }
    }
}
