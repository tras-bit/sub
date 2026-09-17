// ============================================================================
//  SUBSISTENCE — Crafting/WorkbenchSystem.cs
//  Три уровня верстаков, как в Rust:
//   1) Близость: крафт рецепта N-го тира возможен только в радиусе верстака N.
//      Раньше nearbyBenchLevel никто не выставлял (кроме кнопки E на верстаке), из-за
//      чего всё, что выше 0 тира, было недоступно — теперь радиус считается каждый кадр.
//   2) Апгрейда на месте НЕТ (ответ 1б): чтобы получить верстак выше — снести старый
//      киянкой и построить новый. Старый при сносе возвращает ресурсы (10 минут).
//   3) Скорость крафта растёт с тиром (ответ 3в): T1 ×1, T2 ×2, T3 ×3.
//   4) Верстак — физический объект: сломали/унесли — крафт недоступен, очередь не растёт.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.UI;
using Subsistence.Building;      // Workbench — деплой верстака из Player/Deployables.cs

namespace Subsistence.Crafting
{
    /// <summary>Компонент на физическом верстаке: уровень, радиус, скорость.</summary>
    public class WorkbenchStation : MonoBehaviour
    {
        public int level = 1;
        public float radius = 4.0f;

        static readonly List<WorkbenchStation> _all = new List<WorkbenchStation>(16);
        public static IReadOnlyList<WorkbenchStation> All => _all;

        /// <summary>Скорость крафта (ответ 3в): T1 ×1, T2 ×2, T3 ×3.</summary>
        public float SpeedMult => level <= 1 ? 1.0f : level == 2 ? 2.0f : 3.0f;

        void OnEnable()
        {
            if (!_all.Contains(this)) _all.Add(this);
            SyncCraftingSystem();
        }

        void OnDisable()
        {
            _all.Remove(this);
            if (CraftingSystem.Instance != null) CraftingSystem.Instance.RecomputeBench();
        }

        /// <summary>Пересчёт ближайшего верстака для локального игрока.</summary>
        public static void RecomputeFor(Vector3 pos, out int bestLevel, out float bestSpeed)
        {
            bestLevel = 0; bestSpeed = 1f;
            for (int i = 0; i < _all.Count; i++)
            {
                var s = _all[i];
                if (s == null || !s.isActiveAndEnabled) continue;
                if ((s.transform.position - pos).sqrMagnitude > s.radius * s.radius) continue;
                if (s.level > bestLevel) { bestLevel = s.level; bestSpeed = s.SpeedMult; }
            }
        }

        /// <summary>Есть ли рядом верстак нужного уровня (используется при заказе крафта).</summary>
        public static bool HasLevelNear(Vector3 pos, int needLevel)
        {
            RecomputeFor(pos, out int lvl, out _);
            return lvl >= needLevel;
        }

        /// <summary>
        /// Апгрейда на месте больше нет (ответ 1б — отменяет механику v0.2.0 WB1→WB2→WB3).
        /// Метод оставлен, чтобы старые вызовы не «оживили» механику: всегда отказ с подсказкой.
        /// </summary>
        public bool TryUpgrade(PlayerInventory inv)
        {
            ExplainNoUpgrade();
            return false;
        }

        /// <summary>Подсказка: как получить верстак выше (снос + постройка).</summary>
        public void ExplainNoUpgrade()
        {
            if (level >= 3)
                HudRuntime.ShowToast("Это верстак 3 уровня — выше некуда");
            else
                HudRuntime.ShowToast($"Апгрейда на месте нет: снеси киянкой и построй верстак {level + 1} уровня");
        }

        void SyncCraftingSystem()
        {
            var wb = GetComponent<Workbench>();
            if (wb != null && wb.level != level) wb.level = level;
            if (CraftingSystem.Instance != null) CraftingSystem.Instance.RecomputeBench();
        }
    }

    /// <summary>Тикер: держит nearbyBenchLevel/скорость у локального клиента и пишет в HUD.</summary>
    public class WorkbenchSystem : MonoBehaviour
    {
        public static WorkbenchSystem Instance { get; private set; }

        public int CurrentLevel { get; private set; }
        public float CurrentSpeed { get; private set; } = 1f;

        Transform _player;
        float _timer;
        float _hintCooldown;

        void Awake()
        {
            Instance = this;
            if (CraftingSystem.Instance != null) CraftingSystem.Instance.RecomputeBench();
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 0.2f;                                  // 5 раз в секунду — дёшево и отзывчиво

            if (_player == null)
            {
                var pc = FindObjectOfType<Subsistence.Player.PlayerController>();
                if (pc != null) _player = pc.transform;
                else return;
            }

            WorkbenchStation.RecomputeFor(_player.position, out int lvl, out float spd);
            if (lvl != CurrentLevel)
            {
                CurrentLevel = lvl; CurrentSpeed = spd;
                if (CraftingSystem.Instance != null) CraftingSystem.Instance.SetBenchProximity(lvl, spd);
                if (lvl > 0) HudRuntime.ShowToast($"Верстак {lvl} уровня рядом — крафт до {lvl} тира, скорость ×{spd:0.0#} (чужой тоже подходит)");
            }
        }

        /// <summary>Подсказка над верстаком (как в Rust: «E — крафт», «E — улучшить»).</summary>
        public void ShowHint(string text)
        {
            if (_hintCooldown > Time.time) return;
            _hintCooldown = Time.time + 1.5f;
            HudRuntime.SetBuildHint(text);
        }
    }
}
