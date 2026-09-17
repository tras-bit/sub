// ============================================================================
//  SUBSISTENCE — Player/PlayerController.cs
//  FPS-контроллер: бег/ходьба/присед/лёж, выносливость, плавание (бассейны),
//  лазание по лестницам, звук шагов (материал поверхности → монстры слышат),
//  покачивание камеры, факт «сидит/лежит» → влияет на разброс оружия.
//  Готов к сети: MoveState отправляется квантованным пакетом, сервер валидирует.
// ============================================================================
using System;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Net;

namespace Subsistence.Player
{
    /// <summary>Ввод. Одна точка чтения — легко переключить на Input System/Rebind/мобилки.</summary>
    public static class PlayerInput
    {
        public static bool FireHeld, FirePressed, AimHeld, ReloadPressed, JumpPressed, InteractPressed, InteractHeld;
        public static bool CrouchHeld, PronePressed, SprintHeld, DropPressed, InventoryPressed, ConsumePressed, CraftPressed;
        public static bool BuildRotatePressed;
        public static float MoveX, MoveY, LookX, LookY, ScrollDelta;
        public static bool ChatPressed, MapPressed, PausePressed, VoiceHeld;

        /// <summary>Собирает ввод один раз за кадр (вызывается из PlayerController.Update).</summary>
        public static void Poll(bool uiCaptured)
        {
            if (uiCaptured || Cursor.lockState != CursorLockMode.Locked)
            {
                FireHeld = FirePressed = AimHeld = false;
                MoveX = MoveY = LookX = LookY = 0f;
                InteractPressed = InteractHeld = false;
            }
            else
            {
                MoveX = Input.GetAxisRaw("Horizontal");
                MoveY = Input.GetAxisRaw("Vertical");
                LookX = Input.GetAxisRaw("Mouse X");
                LookY = Input.GetAxisRaw("Mouse Y");
                FireHeld = Input.GetButton("Fire1");
                FirePressed = Input.GetButtonDown("Fire1");
                AimHeld = Input.GetButton("Fire2");
                ReloadPressed = Input.GetKeyDown(KeyCode.R);
                JumpPressed = Input.GetKeyDown(KeyCode.Space);
                InteractHeld = Input.GetKey(KeyCode.E);
                InteractPressed = Input.GetKeyDown(KeyCode.E);
                CrouchHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
                PronePressed = Input.GetKeyDown(KeyCode.Z);
                SprintHeld = Input.GetKey(KeyCode.LeftShift) && !AimHeld;
                DropPressed = Input.GetKeyDown(KeyCode.G);
                ConsumePressed = Input.GetKeyDown(KeyCode.F);
                BuildRotatePressed = Input.GetKeyDown(KeyCode.R);
            }
            ScrollDelta = Input.mouseScrollDelta.y;
            InventoryPressed = Input.GetKeyDown(KeyCode.Tab);
            CraftPressed = Input.GetKeyDown(KeyCode.Q);
            MapPressed = Input.GetKeyDown(KeyCode.M);
            PausePressed = Input.GetKeyDown(KeyCode.Escape);
            ChatPressed = Input.GetKeyDown(KeyCode.Return);
            VoiceHeld = Input.GetKey(KeyCode.V);
        }
    }

    /// <summary>Состояние позы — влияет на разброс, скорость, шум, силуэт для монстров.</summary>
    public static class PlayerControllerState
    {
        public static bool IsCrouching;
        public static bool IsProne;
        public static bool IsSprinting;
        public static bool IsSwimming;
        public static bool IsGrounded;

        /// <summary>Множитель разброса (Rust: стоя 1.0 / сидя ~0.7 / лёжа ~0.5 / бег ~2.2).</summary>
        public static float SpreadMultiplier
        {
            get
            {
                float m = 1f;
                if (IsProne) m *= 0.5f;
                else if (IsCrouching) m *= 0.7f;
                if (IsSprinting) m *= 2.2f;
                if (IsSwimming) m *= 3.0f;
                return m;
            }
        }
    }

    /// <summary>Нормализованная скорость (0..1.5) — нужна системам голода/жажды и шуму.</summary>
    public static class PlayerControllerSpeed01
    {
        public static float Value;
    }

    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetEntity, Combat.IDestructible
    {
        [Header("Движение")]
        public float walkSpeed = 3.1f;        // Rust: ~3.0 м/с
        public float sprintSpeed = 5.5f;
        public float crouchSpeed = 1.6f;
        public float proneSpeed = 0.9f;
        public float swimSpeed = 2.6f;
        public float jumpHeight = 1.1f;
        public float gravity = -19.6f;
        public float maxStamina = 100f;
        public float staminaDrain = 6f;
        public float staminaRegen = 9f;

        [Header("Камера")]
        public Transform cameraPivot;
        public Camera viewCamera;
        public float mouseSensitivity = 2.2f;
        public float maxPitch = 89f;
        public float bobAmount = 0.035f;
        public float bobSpeed = 9f;

        [Header("Прочее")]
        public PlayerInventory inventory;

        /// <summary>Внешний множитель скорости (транспорт: самокат 1.7, вода/грязь — снижение).</summary>
        [System.NonSerialized] public float externalSpeedMult = 1f;
        public SurvivalSystem survival;
        public LayerMask groundMask;
        public LayerMask waterMask;

        public float Stamina { get; private set; } = 100f;
        public Vector3 Velocity { get; private set; }
        public bool IsGroundedNow { get; private set; }

        CharacterController _cc;
        float _pitch;
        float _bobTimer;
        Vector3 _spawnPoint;
        float _footstepTimer;
        float _lastMoveSendTime;

        public event Action<float> Footstep;      // громкость шага — монстры слышат

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _spawnPoint = transform.position;
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        }

        void Update()
        {
            bool uiOpen = Subsistence.UI.UIState.AnyMenuOpen;
            PlayerInput.Poll(uiOpen);
            if (uiOpen) { UpdateCameraOnly(); return; }

            HandleLook();
            HandleMove();
            HandleInteraction();
            HandleHotbar();
            UpdateFootsteps();
            SendMovePacket();
        }

        void HandleLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            float sens = mouseSensitivity * (Subsistence.UI.UIState.AimSensitivityMult);
            float yaw = PlayerInput.LookX * sens;
            _pitch = Mathf.Clamp(_pitch - PlayerInput.LookY * sens, -maxPitch, maxPitch);

            // Отдача оружия добавляется к повороту камеры
            var wc = GetComponentInChildren<Combat.WeaponController>();
            Vector2 recoil = wc != null ? wc.recoilAccum : Vector2.zero;

            transform.Rotate(0f, yaw + recoil.x * 0.4f, 0f, Space.Self);
            _pitch = Mathf.Clamp(_pitch - recoil.y * 0.35f, -maxPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(_pitch + recoil.y * 0.35f, recoil.x * 0.35f, 0f);
        }

        void UpdateCameraOnly() { }

        void HandleMove()
        {
            bool inWater = survival != null && survival.IsInWater;
            PlayerControllerState.IsSwimming = inWater && survival.WaterDepth > 1.2f;

            Vector3 input = new Vector3(PlayerInput.MoveX, 0f, PlayerInput.MoveY);
            if (input.sqrMagnitude > 1f) input.Normalize();

            // Поза
            if (PlayerInput.PronePressed) PlayerControllerState.IsProne = !PlayerControllerState.IsProne;
            PlayerControllerState.IsCrouching = PlayerInput.CrouchHeld || (PlayerControllerState.IsCrouching && !PlayerInput.JumpPressed);
            if (PlayerInput.JumpPressed) PlayerControllerState.IsProne = false;

            float speedMult = 1f;
            var armor = GetComponent<Combat.ArmorSystem>();
            if (armor != null) speedMult = armor.MoveSpeedMultiplier;

            float baseSpeed = PlayerControllerState.IsProne ? proneSpeed
                            : PlayerControllerState.IsCrouching ? crouchSpeed
                            : PlayerInput.SprintHeld && Stamina > 5f ? sprintSpeed : walkSpeed;
            if (PlayerControllerState.IsSwimming) baseSpeed = swimSpeed;

            PlayerControllerState.IsSprinting = PlayerInput.SprintHeld && !PlayerControllerState.IsCrouching && !inWater && input.sqrMagnitude > 0.1f;
            if (PlayerControllerState.IsSprinting || (inWater && input.sqrMagnitude > 0.1f))
                Stamina = Mathf.Max(0f, Stamina - staminaDrain * Time.deltaTime * (inWater ? 1.6f : 1f));
            else
                Stamina = Mathf.Min(maxStamina, Stamina + staminaRegen * Time.deltaTime);
            if (Stamina <= 0f) baseSpeed = Mathf.Min(baseSpeed, walkSpeed * 0.6f);

            // 18_weather: аномалии погоды (конденсат/прилив/пар) слегка замедляют движение
            float weatherMult = World.WeatherSystem.SpeedMultiplier;
            Vector3 move = (transform.right * input.x + transform.forward * input.z) * (baseSpeed * speedMult * externalSpeedMult * weatherMult);

            // Вертикаль
            IsGroundedNow = _cc.isGrounded;
            PlayerControllerState.IsGrounded = IsGroundedNow;
            if (inWater)
            {
                float depth = survival.WaterDepth;
                move.y = PlayerInput.JumpPressed ? 2.2f : (depth > 2.2f ? -0.4f * Time.deltaTime : -0.6f * Time.deltaTime);
                Velocity = new Vector3(move.x, move.y, move.z);
                _cc.Move(Velocity * Time.deltaTime);
            }
            else
            {
                if (IsGroundedNow && Velocity.y < 0f) Velocity = new Vector3(Velocity.x, -2f, Velocity.z);
                if (IsGroundedNow && PlayerInput.JumpPressed && Stamina > 10f)
                {
                    Velocity = new Vector3(0f, Mathf.Sqrt(jumpHeight * -2f * gravity), 0f);
                    Stamina -= 8f;
                }
                Velocity += Vector3.up * gravity * Time.deltaTime;
                Vector3 delta = new Vector3(move.x, 0f, move.z) + new Vector3(0f, Velocity.y, 0f);
                _cc.Move(delta * Time.deltaTime);
            }

            // Камера-высота по позе
            float targetEye = PlayerControllerState.IsProne ? 0.55f : PlayerControllerState.IsCrouching ? 1.05f : 1.62f;
            var cp = cameraPivot.localPosition;
            float bob = 0f;
            if (IsGroundedNow && new Vector2(move.x, move.z).magnitude > 0.5f)
            {
                _bobTimer += Time.deltaTime * bobSpeed * (PlayerControllerState.IsSprinting ? 1.45f : 1f);
                bob = Mathf.Sin(_bobTimer) * bobAmount * (PlayerControllerState.IsSprinting ? 1.4f : 1f);
                float bobSide = Mathf.Cos(_bobTimer * 0.5f) * bobAmount * 0.6f;
                cameraPivot.localPosition = Vector3.Lerp(cp, new Vector3(bobSide, targetEye + bob, 0f), Time.deltaTime * 10f);
            }
            else cameraPivot.localPosition = Vector3.Lerp(cp, new Vector3(0f, targetEye, 0f), Time.deltaTime * 8f);

            PlayerControllerSpeed01.Value = new Vector2(move.x, move.z).magnitude / Mathf.Max(0.1f, sprintSpeed);

            // Сеть: сервер валидирует скорость (античит уже готов — CommandValidator)
            InterestGrid.Update(this);
        }

        void HandleInteraction()
        {
            if (!PlayerInput.InteractPressed) return;
            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, Balance.MaxInteractDistance, ~0, QueryTriggerInteraction.Collide)) return;

            // 1) контейнеры лута
            var loot = hit.collider.GetComponentInParent<World.LootContainer>();
            if (loot != null)
            {
                if (loot.requiresKeycard && !HasKeycard(loot))
                {
                    Subsistence.UI.HudRuntime.ShowToast("Нужна ключ-карта");
                    return;
                }
                Subsistence.UI.InventoryUI.OpenLoot(loot);
                return;
            }
            // 2) деплои (TC, верстак, печь, кровать)
            var deployable = hit.collider.GetComponentInParent<Building.BuildDeployable>();
            if (deployable != null) { deployable.OnUse(this); return; }
            // 3) предмет на полу
            var pickup = hit.collider.GetComponentInParent<World.ItemPickup>();
            if (pickup != null) { pickup.TryPickup(inventory); return; }
        }

        bool HasKeycard(World.LootContainer loot)
        {
            if (loot == null) return false;
            return inventory.CountOf("tool.keycard.green") > 0
                || inventory.CountOf("tool.keycard.blue") > 0
                || inventory.CountOf("tool.keycard.red") > 0;
        }

        void HandleHotbar()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) inventory.SelectHotbar(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) inventory.SelectHotbar(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) inventory.SelectHotbar(2);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) inventory.SelectHotbar(3);
            else if (Input.GetKeyDown(KeyCode.Alpha5)) inventory.SelectHotbar(4);
            else if (Input.GetKeyDown(KeyCode.Alpha6)) inventory.SelectHotbar(5);
            else if (Mathf.Abs(PlayerInput.ScrollDelta) > 0.01f) inventory.ScrollHotbar(PlayerInput.ScrollDelta);

            if (PlayerInput.ConsumePressed && survival != null)
                survival.Consume(inventory.ActiveHotbarIndex);
            if (PlayerInput.DropPressed)
            {
                var item = inventory.ActiveItem;
                if (item != null && !item.IsEmpty)
                {
                    // сетевая механика: удалённый клиент просит сервер, хост/офлайн создают сами
                    World.WorldDeposits.DropLooseItem(item.id, item.amount, transform.position + transform.forward * 1.2f);
                    inventory.Set(inventory.ActiveHotbarIndex, null);
                }
            }
        }

        void UpdateFootsteps()
        {
            if (!IsGroundedNow) return;
            float speed = new Vector3(Velocity.x, 0f, Velocity.z).magnitude;
            if (speed < 0.4f) return;
            _footstepTimer -= Time.deltaTime * (PlayerControllerState.IsCrouching ? 0.55f : PlayerControllerState.IsSprinting ? 1.5f : 1f);
            if (_footstepTimer > 0f) return;
            _footstepTimer = 0.55f;
            float loudness = PlayerControllerState.IsCrouching ? 8f : PlayerControllerState.IsSprinting ? 30f : 18f;
            if (survival != null && survival.IsInWater) loudness *= 1.8f;   // по воде слышно далеко
            Footstep?.Invoke(loudness);
            AI.NoiseSystem.Emit(transform.position, loudness, AI.NoiseType.Footstep);
        }

        void SendMovePacket()
        {
            if (!NetworkBridge.IsClient) return;
            if (Time.time - _lastMoveSendTime < 1f / 30f) return;
            _lastMoveSendTime = Time.time;
            var w = BufferWriter.Rent(16);
            w.WritePosition(transform.position, -100f, 1000f);
            w.WriteAngles(transform.rotation);
            w.WriteBool(PlayerControllerState.IsCrouching);
            w.WriteBool(PlayerControllerState.IsProne);
            NetworkBridge.Command(ClientCommand.Move, w.ToArray(), reliable: false);
            BufferWriter.Return(w);
        }

        public override void WriteSnapshot(BufferWriter w)
        {
            w.WritePosition(transform.position, -100f, 1000f);
            w.WriteAngles(transform.rotation);
            w.WriteByte((byte)((PlayerControllerState.IsCrouching ? 1 : 0) | (PlayerControllerState.IsProne ? 2 : 0) |
                                (PlayerControllerState.IsSwimming ? 4 : 0) | (PlayerControllerState.IsSprinting ? 8 : 0)));
            w.WriteUShort((ushort)Mathf.Clamp(Mathf.RoundToInt(survival != null ? survival.State.health : 100f), 0, 300));
        }

        public override void ReadSnapshot(BufferReader r)
        {
            _interp.Push(NetworkBridge.Host?.ServerTime ?? Time.time, r.ReadPosition(-100f, 1000f), r.ReadAngles(), Velocity);
            r.ReadByte();
            r.ReadUShort();
        }

        readonly SnapshotInterpolator _interp = new SnapshotInterpolator();

        // ================== УРОН / СМЕРТЬ ==================
        public bool IsAlive => survival == null || !survival.State.Dead;

        public void ApplyDamage(DamageType type, float amount, Vector3 from, HitRegion region)
        {
            if (survival == null) return;

            // Урон по чужому игроку считает только сервер: на клиенте ApplyDamage по чужому
            // объекту вызывать нельзя (клиент о попадании лишь сообщает — CombatNet.TryPlayerHit).
            if (!Subsistence.Net.NetworkBridge.IsServer && !Subsistence.Net.DeployNet.IsLocal(this)) return;

            var armor = GetComponent<Combat.ArmorSystem>();
            armor?.Recalculate();

            // Диэлектрические перчатки (предмет уровня L3): ток бьёт на четверть
            if (type == DamageType.Electric && inventory != null && inventory.IsWorn("rubber.gloves"))
                amount *= 0.25f;

            // Резина и химзащита не спасают от утопления, а вот ожог током L0-вещей — да
            if ((type == DamageType.Electric || type == DamageType.Fire) && inventory != null && inventory.IsWorn("hazmatsuit"))
                amount *= 0.7f;
            survival.ApplyDamage(type, amount, "урон");
            armor?.WearArmor(type, region, amount);

            // Кровотечение от пуль/порезов (как в Rust: без бинта можно истечь)
            if ((type == DamageType.Bullet || type == DamageType.Slash || type == DamageType.Bite || type == DamageType.Stab)
                && UnityEngine.Random.value < Mathf.Clamp01(amount / 60f))
                survival.AddBleeding(1);

            Subsistence.UI.HudRuntime.FlashDamage(type);
        }

        /// <summary>Вызывается PlayerEntity после смерти: гасим позы, скорость и стан.
        /// Инвентарь и выживание к этому моменту уже сброшены (мешок ушёл в мир).</summary>
        public void ResetAfterDeath()
        {
            PlayerControllerState.IsProne = false;
            PlayerControllerState.IsCrouching = false;
            PlayerControllerState.IsSprinting = false;
            PlayerControllerState.IsSwimming = false;
            PlayerControllerState.IsGrounded = false;
            Stamina = maxStamina;
            _pitch = 0f;
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
            externalSpeedMult = 1f;
            PlayerControllerSpeed01.Value = 0f;
        }

        public void Respawn(Vector3 pos)
        {
            transform.position = pos;
            _spawnPoint = pos;
            Velocity = Vector3.zero;
            Stamina = maxStamina;
            survival?.ResetState();
        }

        public Vector3 SpawnPoint => _spawnPoint;
        public ulong OwnerId => NetworkBridge.Host?.LocalPlayerId ?? 0UL;
    }
}
