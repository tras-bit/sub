// ============================================================================
//  SUBSISTENCE — Player/Transport.cs
//  Транспорт (решение 26_transport): «самокаты, тележки для лута, вагонетки,
//  лифты на станции». Механика намеренно «тяжёлая» — всё ездит по земле,
//  шумит (NoiseSystem) и привлекает монстров, как и должно в Backrooms.
//   • LootCart      — тележка для лута (24 слота), тянется за игроком
//   • Scooter       — самокат: быстрее пешком, но громкий и без разгона в воде
//   • Minecart      — вагонетка: ходит по рельсам электростанции, возит игрока и лут
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Building;

namespace Subsistence.Player
{
    /// <summary>Тележка для лута: тянется за игроком, внутри — контейнер на 24 слота.</summary>
    public class LootCart : BuildDeployable
    {
        public int slots = 24;
        public ItemContainer storage;
        public float pullSpeed = 4.6f;
        public float followDistance = 2.2f;

        PlayerController _puller;

        void Awake()
        {
            itemId = string.IsNullOrEmpty(itemId) ? "cart.loot" : itemId;
            requiresPrivilege = false;
            health = 400f;
            storage = new ItemContainer("Тележка", slots);
        }

        public override void OnUse(PlayerController player)
        {
            if (_puller == player)                     // повторное использование рядом → открыть
            {
                if (Vector3.Distance(player.transform.position, transform.position) < 3f)
                {
                    Subsistence.UI.InventoryUI.OpenContainer(storage);
                    return;
                }
                ReleaseCart();
                return;
            }
            _puller = player;
            Subsistence.UI.HudRuntime.ShowToast("Тележка прицеплена (E рядом — открыть, E в стороне — отпустить)");
        }

        void ReleaseCart()
        {
            _puller = null;
            Subsistence.UI.HudRuntime.ShowToast("Тележка отпущена");
        }

        void Update()
        {
            if (_puller == null) return;
            float dist = Vector3.Distance(_puller.transform.position, transform.position);
            if (dist <= followDistance) return;

            var target = _puller.transform.position - _puller.transform.forward * followDistance;
            target.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, target, pullSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(_puller.transform.forward), 4f * Time.deltaTime);

            // Скрип тележки — слышно далеко
            if (Time.frameCount % 20 == 0) AI.NoiseSystem.Emit(transform.position, 12f, AI.NoiseType.Footstep);
        }
    }

    /// <summary>Самокат: +70 % к скорости, шумный, не работает в глубокой воде.</summary>
    public class Scooter : BuildDeployable
    {
        public float speedMultiplier = 1.7f;
        public float minWaterDepth = 0.45f;      // глубже — ехать нельзя

        PlayerController _rider;
        float _cooldown;

        void Awake()
        {
            itemId = string.IsNullOrEmpty(itemId) ? "scooter" : itemId;
            requiresPrivilege = false;
            health = 150f;
        }

        public override void OnUse(PlayerController player)
        {
            if (_rider == player) { Dismount(); return; }
            if (player.survival != null && player.survival.WaterDepth > minWaterDepth)
            {
                Subsistence.UI.HudRuntime.ShowToast("Слишком глубоко для самоката");
                return;
            }
            _rider = player;
            _rider.externalSpeedMult *= speedMultiplier;
            Subsistence.UI.HudRuntime.ShowToast("Самокат: поехали (E — сойти)");
        }

        void Dismount()
        {
            if (_rider != null) _rider.externalSpeedMult /= speedMultiplier;
            _rider = null;
            Subsistence.UI.HudRuntime.ShowToast("Самокат оставлен");
        }

        void OnDestroy() { Dismount(); }

        void Update()
        {
            if (_rider == null) return;
            _cooldown -= Time.deltaTime;

            // ставим самокат под ноги и катаем вместе с игроком
            transform.position = _rider.transform.position + _rider.transform.forward * 0.35f + Vector3.down * 0.85f;
            transform.rotation = Quaternion.LookRotation(_rider.transform.forward);

            if (_cooldown <= 0f)
            {
                _cooldown = 0.6f;
                AI.NoiseSystem.Emit(transform.position, 20f, AI.NoiseType.Footstep);   // колёса по кафелю
            }
        }
    }

    /// <summary>Вагонетка: ходит по рельсам станции. Возит игрока и 24 слота лута.</summary>
    public class Minecart : BuildDeployable
    {
        public Vector3[] railPath = new Vector3[0];
        public float speed = 6.5f;
        public ItemContainer storage;
        public int slots = 24;

        int _index;
        bool _moving;
        PlayerController _rider;

        void Awake()
        {
            itemId = string.IsNullOrEmpty(itemId) ? "minecart" : itemId;
            requiresPrivilege = false;
            health = 500f;
            storage = new ItemContainer("Вагонетка", slots);
        }

        public void SetPath(Vector3[] path) { railPath = path ?? new Vector3[0]; }

        public override void OnUse(PlayerController player)
        {
            if (_moving) { Stop(); return; }
            if (railPath.Length < 2)   // без рельсов — просто контейнер
            {
                Subsistence.UI.InventoryUI.OpenContainer(storage);
                return;
            }
            _rider = player;
            _moving = true;
            Subsistence.UI.HudRuntime.ShowToast("Вагонетка поехала (E — стоп)");
        }

        void Stop()
        {
            _moving = false;
            _rider = null;
            Subsistence.UI.HudRuntime.ShowToast("Вагонетка остановлена");
        }

        void Update()
        {
            if (!_moving || railPath.Length < 2) return;

            var target = railPath[Mathf.Clamp(_index, 0, railPath.Length - 1)];
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, target) < 0.4f)
            {
                _index++;
                if (_index >= railPath.Length) { _index = 0; }   // кольцо по станции
            }

            if (_rider != null)
            {
                _rider.transform.position = transform.position + Vector3.up * 0.9f;
                if (Time.frameCount % 15 == 0) AI.NoiseSystem.Emit(transform.position, 34f, AI.NoiseType.Footstep);   // грохот рельсов
            }
        }
    }

    /// <summary>Раскладка транспорта по уровням (вызывает RuntimeBootstrap при старте мира).</summary>
    public static class TransportSpawner
    {
        public static void Populate(List<World.GeneratedLevel> levels)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                var lvl = levels[i];
                if (lvl.theme == LevelTheme.PowerStation) SpawnStation(levels[i]);
                else SpawnCartsAndScooters(levels[i]);
            }
        }

        static void SpawnCartsAndScooters(World.GeneratedLevel lvl)
        {
            int n = Mathf.Min(8, lvl.playerSpawns.Count);
            for (int k = 0; k < n; k++)
            {
                var basePos = lvl.playerSpawns[k];
                Create<LootCart>("LootCart", basePos + new Vector3(1.8f, 0f, 0.6f));
                if (k % 2 == 0) Create<Scooter>("Scooter", basePos + new Vector3(-1.8f, 0f, 1.2f));
            }
        }

        static void SpawnStation(World.GeneratedLevel lvl)
        {
            if (lvl.playerSpawns.Count == 0) return;

            // Кольцевые рельсы вокруг входа на станцию + две вагонетки
            var center = lvl.playerSpawns[0];
            var ring = new Vector3[12];
            for (int k = 0; k < ring.Length; k++)
            {
                float a = k * Mathf.PI * 2f / ring.Length;
                ring[k] = center + new Vector3(Mathf.Cos(a) * 26f, 0.15f, Mathf.Sin(a) * 26f);
            }
            for (int k = 0; k < 2; k++)
            {
                var cart = Create<Minecart>("Minecart", ring[(k * 6) % ring.Length]);
                cart.SetPath(ring);
            }
            for (int k = 0; k < 4; k++) Create<LootCart>("LootCart", center + new Vector3(2.4f * k, 0f, 3f));
            Create<Scooter>("Scooter", center + new Vector3(-2.5f, 0f, 2f));
        }

        static T Create<T>(string name, Vector3 pos) where T : BuildDeployable
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(pos.x, pos.y + 0.4f, pos.z);
            go.layer = Layers.Deployable;

            // Коллайдер-прокси: по нему работают рейкасты и «E» (поэтому куб остаётся всегда).
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = name == "Scooter" ? new Vector3(0.5f, 1.0f, 1.4f)
                                    : name == "Minecart" ? new Vector3(1.1f, 0.7f, 1.8f)
                                    : new Vector3(1.0f, 0.8f, 1.4f);
            body.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            body.layer = Layers.Deployable;

            // Модель из Blender (Models/Props): PR_scooter / PR_loot_cart / PR_minecart.
            string model = name == "Scooter" ? World.ModelLibrary.Names.Scooter
                         : name == "Minecart" ? World.ModelLibrary.Names.Minecart
                         : World.ModelLibrary.Names.LootCart;
            float height = name == "Scooter" ? 1.15f : name == "Minecart" ? 1.35f : 1.05f;

            var visual = World.ModelLibrary.AttachFitted(model, go.transform, height, UnityEngine.Random.Range(0f, 360f));
            if (visual != null)
            {
                var r = body.GetComponent<MeshRenderer>();
                if (r != null) r.enabled = false;      // куб превращается в невидимый прокси
            }

            return go.AddComponent<T>();
        }
    }
}
