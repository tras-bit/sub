// ============================================================================
//  SUBSISTENCE — Player/PlayerEntity.cs
//  Связка «игрок как сущность мира»: смерть → мешок с лутом (как в Rust),
//  респавн, тепловой комфорт у огня, синхронизация состояния в сети,
//  и подсказки при подходе к деплоям (верстак/TC/печь).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Net;

namespace Subsistence.Player
{
    public class PlayerEntity : NetEntity
    {
        public PlayerController controller;
        public SurvivalSystem survival;
        public PlayerInventory inventory;

        [Header("Респавн")]
        public float respawnDelay = 5f;
        public float respawnHealth = 100f;

        [Header("Комфорт (костёр/лампа рядом)")]
        public float comfortRadius = 6f;

        float _deathTime;
        bool _dead;
        GameObject _lootBag;
        float _comfortTimer;

        readonly Collider[] _buf = new Collider[16];

        void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (survival == null) survival = GetComponent<SurvivalSystem>();
            if (inventory == null) inventory = controller != null ? controller.inventory : GetComponent<PlayerInventory>();
            if (survival != null) survival.Died += OnDeath;
        }

        void Update()
        {
            if (_dead)
            {
                if (Time.time - _deathTime >= respawnDelay && Subsistence.Player.PlayerInput.JumpPressed)
                    Respawn();
                return;
            }
            UpdateComfort();
        }

        void UpdateComfort()
        {
            _comfortTimer += Time.deltaTime;
            if (_comfortTimer < 1f) return;
            _comfortTimer = 0f;

            int level = 0;
            int n = Physics.OverlapSphereNonAlloc(transform.position, comfortRadius, _buf, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var flames = _buf[i].GetComponentInParent<Subsistence.Combat.FirePool>();
                if (flames != null) { level = Mathf.Max(level, 2); continue; }
                var light = _buf[i].GetComponentInParent<Light>();
                if (light != null && light.enabled && light.intensity > 0.5f) level = Mathf.Max(level, 1);
            }
            // сухость тоже комфорт
            if (survival != null && survival.State.wetness < 10f) level = Mathf.Max(level, 1);
            survival?.SetComfort(level);
        }

        void OnDeath(string cause)
        {
            if (_dead) return;
            _dead = true;
            _deathTime = Time.time;
            DropLootBag(cause);
            controller?.ResetAfterDeath();
            Subsistence.UI.HudRuntime.ShowToast($"Ты погиб: {cause}. Прыжок — возрождение.", 6f);
        }

        /// <summary>Мешок с лутом: всё снаряжение падает на землю (главный «растовый» риск).</summary>
        void DropLootBag(string cause)
        {
            if (inventory == null) return;

            var pos = transform.position + Vector3.up * 0.4f;
            float yaw = UnityEngine.Random.Range(0f, 360f);

            // Удалённый клиент только сообщает о смерти: мешок собирает сервер из своего зеркала
            // (иначе игрок собрал бы мешок из предметов, которых у него на самом деле нет).
            if (NetworkBridge.IsClient && !NetworkBridge.IsServer)
            {
                InventoryNet.RequestDeath(pos, yaw);
                inventory.ClearAll();
                _lootBag = null;
                return;
            }

            // Хост и офлайн: мешок создаём сами — в сетевом мире он ещё и репликуется всем.
            var bag = SpawnNet.ServerSpawnBag(pos, yaw, NetworkBridge.Host?.LocalPlayerId ?? 0UL);
            if (bag != null)
            {
                var items = inventory.AllItems();
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item != null && !item.IsEmpty) bag.Items.TryAdd(item.Clone());
                }
                _lootBag = bag.gameObject;
            }

            inventory.ClearAll();
            Debug.Log($"[Death] {cause} → мешок с лутом на {pos}");
        }

        public void Respawn()
        {
            _dead = false;
            survival?.ResetState();
            var pos = controller != null ? controller.SpawnPoint : transform.position + Vector3.up * 2f;
            controller?.Respawn(pos);
            Subsistence.UI.HudRuntime.ShowToast("Возрождение");
        }

        public bool IsDead => _dead;

        public override void WriteSnapshot(BufferWriter w)
        {
            w.WritePosition(transform.position, -100f, 1000f);
            w.WriteAngles(transform.rotation);
            w.WriteByte((byte)(_dead ? 1 : 0));
            w.WriteUShort((ushort)Mathf.Clamp(survival != null ? Mathf.RoundToInt(survival.State.health * 10f) : 1000, 0, 65535));
        }

        public override void ReadSnapshot(BufferReader r)
        {
            transform.position = r.ReadPosition(-100f, 1000f);
            transform.rotation = r.ReadAngles();
            _dead = r.ReadByte() != 0;
            r.ReadUShort();
        }
    }

    /// <summary>Утилиты игрока: подсказки при подходе к чужим/своим объектам.</summary>
    public static class PlayerInteractions
    {
        static readonly Collider[] _buf = new Collider[8];

        /// <summary>Ищем ближайший деплой и его «подпись» для HUD-подсказки (E — открыть).</summary>
        public static string NearbyPrompt(Vector3 pos)
        {
            int n = Physics.OverlapSphereNonAlloc(pos, 3f, _buf,
                        Layers.Mask(Layers.Deployable, Layers.Loot, Layers.Ragdoll), QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var wb = _buf[i].GetComponentInParent<Subsistence.Building.Workbench>();
                if (wb != null) return $"E — верстак уровня {wb.level}";
                var sb = _buf[i].GetComponentInParent<Subsistence.Building.StorageBox>();
                if (sb != null) return "E — открыть ящик";
                var tc = _buf[i].GetComponentInParent<Subsistence.Building.ToolCupboard>();
                if (tc != null) return "E — шкаф с инструментами";
                var loot = _buf[i].GetComponentInParent<World.LootContainer>();
                if (loot != null) return "E — обыскать";
            }
            return null;
        }
    }
}
