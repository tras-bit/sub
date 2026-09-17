// ============================================================================
//  SUBSISTENCE — Player/Survival.cs
//  Выживание Backrooms + Rust: голод, жажда, кровотечение, инфекция, радиация,
//  РАССУДОК (главный ресурс уровней), комфорт у огня, утопление, температура.
//  Всё считается на сервере, клиент получает снапшот и предсказывает плавно.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;

namespace Subsistence.Player
{
    [Serializable]
    public struct SurvivalState
    {
        public float health;          // 0..100
        public float calories;        // 0..1000  (голод: падает)
        public float hydration;       // 0..1000  (жажда)
        public float radiation;       // 0..1000  (накапливается, снимается антирадом)
        public float wetness;         // 0..100   (мокрый = холодно + шумно)
        public float temperature;     // 37.0 — тело
        public float stamina;         // 0..100
        public byte bleeding;         // число активных кровотечений (0..5)
        public byte infections;       // число очагов инфекции
        public bool drowned;          // активно тонет
        public byte comfort;          // 0..3: у огня/в сухости/на свету

        public const float MaxCalories = 1000f, MaxHydration = 1000f, MaxRad = 1000f;

        public static SurvivalState Fresh() => new SurvivalState
        { health = 100f, calories = 750f, hydration = 750f, radiation = 0f, wetness = 0f, temperature = 37f, stamina = 100f };

        public float HealthPercent => Mathf.Clamp01(health / 100f);
        public float CaloriePercent => Mathf.Clamp01(calories / MaxCalories);
        public float HydrationPercent => Mathf.Clamp01(hydration / MaxHydration);
        public float RadPercent => Mathf.Clamp01(radiation / MaxRad);
        public bool Dead => health <= 0f;
        public bool Starving => calories <= 0f;
        public bool Dehydrated => hydration <= 0f;
    }

    /// <summary>Эффект от еды/питья/медицины. Таблица — в Consumables.</summary>
    public struct ConsumableEffect
    {
        public float calories, hydration, health, radiation, wetness;
        public byte bleedCure, infectionCure;
        public float overTimeSeconds;  // длительность (медвежатина лечит медленно, бинт — мгновенно)
        public bool poisoned;          // грязная вода / плоть монстра
        public string buffId;          // напр. "warmth", "clarity"
    }

    /// <summary>
    /// Серверный расчёт выживания. Один экземпляр на игрока. Тик 1 Гц + подшаги.
    /// </summary>
    public class SurvivalSystem : MonoBehaviour
    {
        [Header("Слои/триггеры мира")]
        public LayerMask waterMask;
        public LayerMask radiationMask;

        /// <summary>ВАЖНО: поле, а не свойство — код правит поля структуры на месте
        /// (свойство давало CS1612: «нельзя менять возвращаемое значение»).</summary>
        public SurvivalState State = SurvivalState.Fresh();
        public PlayerInventory Inventory { get; set; }

        /// <summary>Провайдер резистов — сюда подключается ArmorSystem (или обёртка над сетью).</summary>
        public Func<DamageType, float> ArmorResistProvider;

        /// <summary>Провайдер множителя «зоны уровня» (в реакторном зале радиация ×4 и т.д.).</summary>
        public Func<Vector3, float> DangerMultiplierProvider;

        public bool IsInWater { get; private set; }
        public float WaterDepth { get; private set; }
        public float RadExposureRate { get; private set; }

        public event Action<DamageType, float> Damaged;      // для UI-урона по экрану
        public event Action<string> Died;                   // причина смерти
        public event Action<ConsumableEffect> EffectApplied;

        readonly List<(ConsumableEffect fx, float ends)> _active = new List<(ConsumableEffect, float)>(4);
        float _tickAccum;
        float _bleedTickAccum;
        float _underwaterTime;
        float _starveAccum;
        float _lastDamageTime;

        // ================== ОСНОВНОЙ ТИК ==================
        void Update()
        {
            if (State.Dead) return;
            float dt = Time.deltaTime;

            UpdateEnvironment(dt);

            _tickAccum += dt;
            if (_tickAccum >= 1f)
            {
                float step = _tickAccum; _tickAccum = 0f;
                TickSurvival(step);
            }

            TickBleeding(dt);
            TickContinuousEffects(dt);
            TickDrowning(dt);
        }

        void UpdateEnvironment(float dt)
        {
            // Вода (Poolrooms)
            var probe = transform.position + Vector3.up * 1.0f;
            IsInWater = Physics.CheckSphere(probe, 0.45f, waterMask, QueryTriggerInteraction.Collide);
            WaterDepth = 0f;
            if (IsInWater)
            {
                // уровень воды берём по глобальной функции уровня (задаёт LevelGenerator)
                WaterDepth = Subsistence.World.WorldEnvironment.SampleWaterDepth(transform.position);
                var chest = transform.position + Vector3.up * 1.4f;
                bool headUnder = World.WorldEnvironment.IsUnderwater(chest);
                if (headUnder) _underwaterTime += dt; else _underwaterTime = Mathf.Max(0f, _underwaterTime - dt * 3f);
                // намокание: чем глубже — тем быстрее
                State.wetness = Mathf.Clamp(State.wetness + WaterDepth * dt * 12f, 0f, 100f);
                // вода в бассейнах прохладная → падение температуры тела
                State.temperature = Mathf.MoveTowards(State.temperature, 36.2f, dt * 0.05f);
            }
            else
            {
                _underwaterTime = 0f;
                float dryRate = 3f + State.comfort * 6f;
                State.wetness = Mathf.Max(0f, State.wetness - dt * dryRate);
                float target = 37f - (State.wetness > 60f ? 0.8f : 0f) + State.comfort * 0.15f;
                State.temperature = Mathf.MoveTowards(State.temperature, target, dt * 0.08f);
            }

            // Радиация (электростанция/реакторный зал).
            // Источники: RadVolume-триггеры + близость к ТВЭЛам. Считаем «мгновенную» дозу.
            float rad = 0f;
            var cols = Physics.OverlapSphere(transform.position, 12f, radiationMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < cols.Length; i++)
            {
                var src = cols[i].GetComponentInParent<IRadiationSource>();
                if (src != null) rad += src.RadsPerSecondAt(transform.position);
            }
            if (DangerMultiplierProvider != null) rad *= DangerMultiplierProvider(transform.position);
            RadExposureRate = rad;
        }

        void TickSurvival(float dt)
        {
            // Голод/жажда: базовый расход + модификаторы активности
            float activity = 1f + Mathf.Clamp(PlayerControllerSpeed01.Value, 0f, 1.5f);
            State.calories = Mathf.Max(0f, State.calories - 0.55f * activity * dt);       // ~ 30 мин до голода с полной шкалы
            State.hydration = Mathf.Max(0f, State.hydration - 0.75f * activity * dt);

            // Обморок/урон от голода и жажды (Rust: медленный урон)
            if (State.calories <= 0f || State.hydration <= 0f)
            {
                _starveAccum += dt;
                if (_starveAccum >= 5f)
                {
                    _starveAccum = 0f;
                    ApplyDamage(DamageType.Generic, 1.5f, "истощение");
                }
            }

            // Радиация: накопление + лучевая болезнь
            State.radiation = Mathf.Clamp(State.radiation + RadExposureRate * dt, 0f, SurvivalState.MaxRad);
            if (State.radiation > 700f)
            {
                // тяжёлая степень: тошнит, кровь, урон
                ApplyDamage(DamageType.Radiation, 4f * (State.radiation - 700f) / 300f, "лучевая болезнь");
            }
            else if (State.radiation > 250f && UnityEngine.Random.value < 0.15f * dt)
            {
                State.bleeding = (byte)Mathf.Min(5, State.bleeding + 1);   // радиация открывает кровотечения
            }
            // Естественное выведение радиации, ускоренное сытостью
            float radDecay = State.calories > 300f ? 0.35f : 0.12f;
            State.radiation = Mathf.Max(0f, State.radiation - radDecay * dt);

            // Инфекции: если есть очаг и не лечим — периодический урон и температура
            if (State.infections > 0 && UnityEngine.Random.value < 0.05f * dt)
            {
                ApplyDamage(DamageType.Generic, 1f * State.infections, "инфекция");
                State.temperature = Mathf.Min(39.5f, State.temperature + 0.1f);
            }

            // Холод: температура тела ниже 36 — отнимаем здоровье
            if (State.temperature < 36f)
                ApplyDamage(DamageType.Cold, (36f - State.temperature) * 1.2f * dt, "переохлаждение");
            // Высокая температура (лихорадка)
            if (State.temperature > 38.5f)
                ApplyDamage(DamageType.Generic, (State.temperature - 38.5f) * 1.5f * dt, "лихорадка");

            // Регенерация как в Rust: только при полной сытости и жажде
            if (State.calories > 400f && State.hydration > 400f && State.bleeding == 0 && State.infections == 0)
                State.health = Mathf.Min(100f, State.health + 0.6f * dt * (State.comfort > 0 ? 1.5f : 1f));
        }

        void TickDrowning(float dt)
        {
            if (!IsInWater) return;
            bool headUnder = World.WorldEnvironment.IsUnderwater(transform.position + Vector3.up * 1.45f);
            State.drowned = headUnder;
            // Маска ныряльщика (предмет уровня L37): запас 5 → 11 сек, урон втрое меньше
            bool mask = Inventory != null && Inventory.IsWorn("diving.mask");
            float safeSeconds = mask ? 11f : 5f;
            float dmgPerSec = mask ? 3f : 8f;
            if (headUnder && _underwaterTime > safeSeconds)
                ApplyDamage(DamageType.Drowning, dmgPerSec * dt, "утопление");
        }

        void TickBleeding(float dt)
        {
            if (State.bleeding == 0) return;
            _bleedTickAccum += dt;
            if (_bleedTickAccum >= 3f)
            {
                _bleedTickAccum = 0f;
                ApplyDamage(DamageType.Generic, 1.2f * State.bleeding, "кровотечение");
                State.hydration = Mathf.Max(0f, State.hydration - 6f * State.bleeding);
            }
        }

        void TickContinuousEffects(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var (fx, ends) = _active[i];
                float left = ends - Time.time;
                if (left <= 0f) { _active.RemoveAt(i); continue; }
                float perSecond = 1f;
                State.health = Mathf.Clamp(State.health + fx.health * perSecond * dt, 0f, 100f);
                State.calories = Mathf.Clamp(State.calories + fx.calories / Mathf.Max(1f, fx.overTimeSeconds) * dt, 0f, SurvivalState.MaxCalories);
                State.hydration = Mathf.Clamp(State.hydration + fx.hydration / Mathf.Max(1f, fx.overTimeSeconds) * dt, 0f, SurvivalState.MaxHydration);
            }
        }

        // ================== УРОН ==================
        /// <summary>Единая точка приёма урона. Сервер вызывает её напрямую, клиент — только предсказывает.</summary>
        public bool godMode;      // админ-режим (36_anticheat: инструменты админов)

        public void ApplyDamage(DamageType type, float amount, string source = null)
        {
            if (godMode) return;
            if (State.Dead || amount <= 0f) return;

            float resist = ArmorResistProvider?.Invoke(type) ?? 0f;
            // Резист брони действует на всё, кроме утопления, падения и холода
            if (type != DamageType.Drowning && type != DamageType.Cold)
                amount *= Mathf.Clamp01(1f - resist);

            State.health = Mathf.Max(0f, State.health - amount);
            _lastDamageTime = Time.time;
            Damaged?.Invoke(type, amount);

            if (State.health <= 0f) Die(source ?? type.ToString());
        }

        public void Heal(float amount)
        {
            if (State.Dead) return;
            State.health = Mathf.Min(100f, State.health + amount);
        }

        public void AddBleeding(int stacks = 1) => State.bleeding = (byte)Mathf.Clamp(State.bleeding + stacks, 0, 5);
        public void CureBleeding(int stacks = 1) => State.bleeding = (byte)Mathf.Max(0, State.bleeding - stacks);
        public void AddInfection(int stacks = 1) => State.infections = (byte)Mathf.Clamp(State.infections + stacks, 0, 3);

        public void SetComfort(int level) => State.comfort = (byte)Mathf.Clamp(level, 0, 3);

        void Die(string cause)
        {
            State.health = 0f;
            Died?.Invoke(cause);
        }

        /// <summary>Возрождение/вайп после смерти.</summary>
        public void ResetState(bool keepRadiation = false)
        {
            var rad = State.radiation;
            State = SurvivalState.Fresh();
            if (keepRadiation) State.radiation = rad;
            _active.Clear();
        }

        // ================== ПОТРЕБЛЕНИЕ ПРЕДМЕТОВ ==================
        /// <summary>Съесть/выпить/уколоть предмет из указанного слота. Возвращает true, если эффект применён.</summary>
        public bool Consume(int slotIndex)
        {
            if (Inventory == null) return false;
            var stack = Inventory.Get(slotIndex);
            if (stack == null || stack.IsEmpty) return false;
            if (!Consumables.TryGet(stack.id, out var fx)) return false;

            // Отравление (грязная вода, плоть монстра)
            if (fx.poisoned)
            {
                State.health = Mathf.Max(1f, State.health - 5f);
                AddInfection(1);
                State.hydration = Mathf.Clamp(State.hydration + (fx.hydration > 0 ? fx.hydration * 0.4f : 0f), 0f, SurvivalState.MaxHydration);
                State.calories = Mathf.Clamp(State.calories + (fx.calories > 0 ? fx.calories * 0.4f : 0f), 0f, SurvivalState.MaxCalories);
                EffectApplied?.Invoke(fx);
                ConsumeOne(slotIndex, stack);
                return true;
            }

            State.calories = Mathf.Clamp(State.calories + fx.calories, 0f, SurvivalState.MaxCalories);
            State.hydration = Mathf.Clamp(State.hydration + fx.hydration, 0f, SurvivalState.MaxHydration);
            State.radiation = Mathf.Clamp(State.radiation + fx.radiation, 0f, SurvivalState.MaxRad);
            State.wetness = Mathf.Clamp(State.wetness + fx.wetness, 0f, 100f);

            if (fx.bleedCure > 0) CureBleeding(fx.bleedCure);
            if (fx.infectionCure > 0) State.infections = (byte)Mathf.Max(0, State.infections - fx.infectionCure);

            if (fx.overTimeSeconds > 0.1f) _active.Add((fx, Time.time + fx.overTimeSeconds));
            else Heal(fx.health);

            EffectApplied?.Invoke(fx);
            ConsumeOne(slotIndex, stack);
            return true;
        }

        void ConsumeOne(int slotIndex, ItemStack stack)
        {
            var def = stack.Def;
            if (def != null && def.id == "water.bottle")
            {
                // Бутылка возвращается пустой (как в Rust)
                stack.amount--;
                if (stack.amount <= 0) Inventory.Set(slotIndex, new ItemStack("water.dirty", 1));
                else Inventory.Set(slotIndex, stack);
                return;
            }
            stack.amount--;
            Inventory.Set(slotIndex, stack.amount <= 0 ? null : stack);
        }
    }

    /// <summary>Источник радиации: реактор, ТВЭЛ, лужи охлаждающей жидкости.</summary>
    public interface IRadiationSource
    {
        float RadsPerSecondAt(Vector3 worldPos);
    }

    /// <summary>Таблица потребляемых предметов (значения подобраны под шкалы 0..1000).</summary>
    public static class Consumables
    {
        static readonly Dictionary<string, ConsumableEffect> _map = new Dictionary<string, ConsumableEffect>();

        static Consumables()
        {
            Add("can.beans", cal: 350, hyd: 20, heal: 5);
            Add("can.tuna", cal: 300, hyd: 40, heal: 3);
            Add("chocolate", cal: 150, hyd: 0, heal: 0, ot: 0.5f);
            Add("bearmeat.cooked", cal: 500, hyd: 10, heal: 20, ot: 2f);
            Add("meat.raw", cal: 300, hyd: 0, heal: 0, poisoned: true);
            Add("apple", cal: 80, hyd: 60, heal: 2);
            Add("mushroom", cal: 60, hyd: 30, heal: 0, poisoned: true);
            Add("glow.mushroom", cal: 90, hyd: 40, heal: 6, ot: 3f);   // светогриб: еда уровня L0 + крафт
            Add("water.bottle", cal: 0, hyd: 500, heal: 0);
            Add("water.dirty", cal: 0, hyd: 400, heal: 0, poisoned: true);
            Add("pool.water", cal: 0, hyd: 400, heal: 0, rad: 15f, poisoned: true);
            Add("almond.water", cal: 60, hyd: 300, heal: 8, ot: 4f);   // ключевой предмет уровней
            Add("bandage", cal: 0, hyd: 0, heal: 10, bleedCure: 1, ot: 3f);
            Add("syringe.medical", cal: 0, hyd: 0, heal: 35, ot: 0.1f);
            Add("largemedkit", cal: 0, hyd: 0, heal: 100, bleedCure: 3, infectionCure: 1, ot: 5f);
            Add("antidote", cal: 0, hyd: 0, heal: 10, rad: -400f, ot: 0.1f);
            Add("rattler.flesh", cal: 180, hyd: 0, heal: 0, poisoned: true);
        }

        static void Add(string id, float cal = 0, float hyd = 0, float heal = 0,
                        float rad = 0, bool poisoned = false, float ot = 0f, byte bleedCure = 0, byte infectionCure = 0)
        {
            _map[id] = new ConsumableEffect
            {
                calories = cal, hydration = hyd, health = heal, radiation = rad,
                poisoned = poisoned, overTimeSeconds = ot, bleedCure = bleedCure, infectionCure = infectionCure
            };
        }

        public static bool TryGet(string id, out ConsumableEffect fx) => _map.TryGetValue(id, out fx);
        public static bool IsConsumable(string id) => _map.ContainsKey(id);
    }
}
