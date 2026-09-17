// ============================================================================
//  SUBSISTENCE — Building/BuildHammer.cs
//  Киянка (молоток) — доводит строительство до раст-поведения:
//    • ЛКМ  — бьёт блок (урон считается в оружии, с учётом мягкой стороны);
//    • ПКМ (короткий тап) — ремонт, если блок повреждён; иначе апгрейд на тир выше;
//    • ПКМ (удержание 2.5 с) — снос с ПОЛНЫМ возвратом ресурсов, пока не прошло 10 минут
//      с постановки (после — сносить нельзя, как в Rust);
//    • при апгрейде мягкой становится сторона, с которой стоит игрок — по ней ближний
//      бой бьёт в 2.5 раза сильнее, взрывчатке разницы нет.
//  Всё, что тратит/возвращает ресурсы, делает только сервер (в офлайне — хост).
// ============================================================================
using UnityEngine;
using Subsistence.Core;
using Subsistence.Net;
using Subsistence.UI;

namespace Subsistence.Building
{
    public class BuildInteraction : MonoBehaviour
    {
        public Camera viewCamera;
        public PlayerInventory inventory;

        public float reach = 4.5f;
        public float tapSeconds = 0.5f;         // короче — «тап» (ремонт/апгрейд)
        public float demolishSeconds = 2.5f;    // дольше — снос

        float _holdStart = -1f;
        BuildBlock _aimed;
        string _lastHint;

        void Update()
        {
            if (viewCamera == null || inventory == null) return;
            if (UIState.AnyMenuOpen) { ResetHold(); return; }

            var held = inventory.ActiveItem;
            bool hammerInHand = held != null && (held.id == "hammer" || held.id == "sledgehammer");
            if (!hammerInHand) { ResetHold(); SetHint(null); return; }

            var block = LookAtBlock();
            if (block == null) { ResetHold(); SetHint(null); return; }
            _aimed = block;

            bool rmb = Subsistence.Player.PlayerInput.AimHeld;

            if (rmb && _holdStart < 0f) _holdStart = Time.time;

            if (rmb && _holdStart >= 0f)
            {
                float hold = Time.time - _holdStart;
                if (block.tier == BuildTier.Twig && hold >= demolishSeconds)
                {
                    TryDemolish(block);
                    ResetHold();
                    return;
                }
                SetHint(HintFor(block, hold));
                return;
            }

            if (!rmb && _holdStart >= 0f)
            {
                float hold = Time.time - _holdStart;
                _holdStart = -1f;
                if (hold <= tapSeconds) OnTap(block);
            }

            SetHint(HintFor(block, 0f));
        }

        BuildBlock LookAtBlock()
        {
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, reach, Layers.Mask(Layers.Buildable), QueryTriggerInteraction.Ignore))
                return null;
            return hit.collider.GetComponentInParent<BuildBlock>();
        }

        // ================== действия ==================

        void OnTap(BuildBlock block)
        {
            if (block.IsDamaged) Repair(block);
            else Upgrade(block);
        }

        /// <summary>Апгрейд на тир выше: платит игрок, тир меняет сервер; мягкая сторона — со стороны игрока.</summary>
        void Upgrade(BuildBlock block)
        {
            var next = BuildGrades.Next(block.tier);
            if (next == block.tier) { HudRuntime.ShowToast("Максимальный тир"); return; }

            var cost = BuildCosts.UpgradeCost(block.piece, next);
            if (!inventory.CanAfford(cost)) { HudRuntime.ShowToast("Не хватает ресурсов на апгрейд"); return; }
            if (!NetworkBridge.IsServer) { HudRuntime.ShowToast("Апгрейд делает сервер"); return; }

            inventory.Pay(cost);
            // Rust: мягкой становится сторона, с которой апгрейдили (внутренняя — обычно)
            block.SetSoftSide(transform.position - block.transform.position);
            block.Upgrade(next);
            Subsistence.AI.NoiseSystem.Emit(block.transform.position, 14f, Subsistence.AI.NoiseType.Building);
            Subsistence.Audio.AudioDirector.PlayAt("hammer_up", block.transform.position, 0.8f);
            HudRuntime.ShowToast($"Апгрейд до {BuildGrades.Get(next).id} · мягкая сторона: к тебе");
        }

        /// <summary>Ремонт: четверть стоимости апгрейда, восстанавливает 40% прочности.</summary>
        void Repair(BuildBlock block)
        {
            if (!NetworkBridge.IsServer) { HudRuntime.ShowToast("Ремонт делает сервер"); return; }
            var cost = BuildCosts.RepairCost(block.piece, block.tier);
            if (!inventory.CanAfford(cost)) { HudRuntime.ShowToast("Не хватает ресурсов на ремонт"); return; }

            inventory.Pay(cost);
            float before = block.HealthFraction;
            block.Repair(block.MaxHp * 0.4f);
            Subsistence.Audio.AudioDirector.PlayAt("repair_ticks", block.transform.position, 0.7f);
            HudRuntime.ShowToast($"Ремонт: {before * 100f:0}% → {block.HealthFraction * 100f:0}%");
        }

        /// <summary>Снос киянкой: возврат ресурсов, пока блок «молодой» (10 минут, как в Rust).</summary>
        void TryDemolish(BuildBlock block)
        {
            if (!NetworkBridge.IsServer) { HudRuntime.ShowToast("Снос делает сервер"); return; }
            if (!block.CanDemolish)
            {
                HudRuntime.ShowToast("Прошло больше 10 минут — снести нельзя (только взрывчаткой)");
                return;
            }
            var refund = BuildCosts.Refund(block.piece, block.tier);
            for (int i = 0; i < refund.Count; i++) inventory.TryAdd(refund[i]);

            string what = "";
            for (int i = 0; i < refund.Count; i++) what += $"{ItemDatabase.NameOf(refund[i].id)} {refund[i].amount} ";
            HudRuntime.ShowToast($"Снесено, возврат: {what}");
            Subsistence.AI.NoiseSystem.Emit(block.transform.position, 18f, Subsistence.AI.NoiseType.Building);
            Subsistence.Audio.AudioDirector.PlayLong("demolish", block.transform.position, 0.85f);
            block.Demolish();
        }

        // ================== подсказки ==================

        string HintFor(BuildBlock block, float hold)
        {
            string soft = block.hasSoftSide
                ? (block.IsSoftSideHit(transform.position - block.transform.position) ? " · мягкая сторона к тебе" : " · мягкая сторона от тебя")
                : "";
            string hp = $"Прочность {block.HealthFraction * 100f:0}% ({BuildGrades.Get(block.tier).id}){soft}";

            if (hold > 0f)
            {
                if (block.tier == BuildTier.Twig && block.CanDemolish)
                    return $"{hp}\nДержи ПКМ {hold:0.0}/{demolishSeconds:0.0} с — снести (возврат {block.DemolishSecondsLeft:0} с)";
                return hp;
            }

            if (block.IsDamaged)
            {
                var cost = BuildCosts.RepairCost(block.piece, block.tier);
                return $"{hp}\n[ПКМ] ремонт ({CostText(cost)})";
            }

            var next = BuildGrades.Next(block.tier);
            if (next == block.tier) return $"{hp}\nМаксимальный тир";
            var up = BuildCosts.UpgradeCost(block.piece, next);
            return $"{hp}\n[ПКМ] апгрейд → {BuildGrades.Get(next).id} ({CostText(up)})"
                 + (block.CanDemolish ? $"   ·   держи ПКМ — снести" : "");
        }

        static string CostText(System.Collections.Generic.List<ItemStack> cost)
        {
            string t = "";
            for (int i = 0; i < cost.Count; i++) t += $"{ItemDatabase.NameOf(cost[i].id)} {cost[i].amount} ";
            return t.TrimEnd();
        }

        void SetHint(string text)
        {
            if (_lastHint == text) return;
            _lastHint = text;
            HudRuntime.SetBuildHint(text ?? string.Empty);
        }

        void ResetHold() { _holdStart = -1f; _aimed = null; }
    }
}
