// ============================================================================
//  SUBSISTENCE — World/Transitions.cs
//  Переходы между уровнями: ЛИФТЫ ПО КЛЮЧ-КАРТАМ (решение 09_connect_levels).
//   • вверх — нужна карта целевого уровня (зелёная → Level 37, синяя → Level 3)
//   • вниз  — свободный возврат с лутом
//  Ключ-карта не расходуется (как пропуск в Rust). Лифт неубиваемый.
// ============================================================================
using System.Collections;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Player;

namespace Subsistence.World
{
    /// <summary>Лифт между уровнями. Интеракция — E, как у любого деплоя.</summary>
    public class LevelElevator : Building.BuildDeployable
    {
        public LevelTheme fromLevel;
        public LevelTheme toLevel;
        public string requiredKeycard;     // null / "" → свободный проход
        public bool goesUp = true;
        public float rideSeconds = 4.5f;

        bool _riding;

        void Awake()
        {
            itemId = "elevator";
            requiresPrivilege = false;
            health = float.MaxValue;       // лифты неубиваемые — это инфраструктура карты
            gameObject.layer = Layers.Deployable;
        }

        public void Setup(LevelTheme from, LevelTheme to, string keycard, bool up)
        {
            fromLevel = from; toLevel = to; requiredKeycard = keycard; goesUp = up;
            name = $"Elevator_{(up ? "up" : "down")}_{to}";
        }

        /// <summary>Лифт нельзя разбить ракетой — только кататься.</summary>
        public override void ApplyDamage(DamageType type, float amount, Vector3 from, HitRegion region) { }

        public override void OnUse(Subsistence.Player.PlayerController player)
        {
            if (player == null || _riding) return;

            if (!string.IsNullOrEmpty(requiredKeycard) && player.inventory.CountOf(requiredKeycard) == 0)
            {
                Subsistence.UI.HudRuntime.ShowToast($"Лифт заперт: нужна {KeycardName(requiredKeycard)}");
                return;
            }
            StartCoroutine(Ride(player));
        }

        static string KeycardName(string id)
        {
            switch (id)
            {
                case "tool.keycard.green": return "зелёная ключ-карта";
                case "tool.keycard.blue": return "синяя ключ-карта";
                case "tool.keycard.red": return "красная ключ-карта";
                default: return id;
            }
        }

        IEnumerator Ride(PlayerController player)
        {
            _riding = true;
            Subsistence.UI.HudRuntime.ShowToast(goesUp
                ? $"Лифт: подъём → {Runtime.RuntimeBootstrap.NameOf(toLevel)}"
                : $"Лифт: спуск → {Runtime.RuntimeBootstrap.NameOf(toLevel)}");
            yield return new WaitForSeconds(rideSeconds);

            var boot = Runtime.RuntimeBootstrap.Instance;
            if (boot != null) boot.TeleportPlayerTo(toLevel, boot.ArrivalPointFor(toLevel));

            _riding = false;
        }
    }
}
