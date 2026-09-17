// ============================================================================
//  SUBSISTENCE — Player/DeployPlacer.cs
//  21_deploy: постановка деплоев из инвентаря (в Rust: взял предмет → ЛКМ поставил).
//   • ящики, печи, верстаки 1-3, шкаф (TC), кровати/спальники, турели, SAM,
//     очиститель воды, стол исследований, ремонтный верстак, вендинг, тележка, самокат;
//   • призрак (ghost) с подсветкой: зелёный — можно, красный — нельзя;
//   • в радиусе чужого Tool Cupboard ставить нельзя (BuildingPrivilege.CanBuild);
//   • сервер ставит объект и списывает предмет (офлайн IsServer == true).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Net;

namespace Subsistence.Building
{
    /// <summary>Tool Cupboard как деплой: шкаф авторизует хозяина и открывает прилавок.</summary>
    public class ToolCupboardBox : BuildDeployable
    {
        public ToolCupboard cupboard;

        void Awake()
        {
            cupboard = GetComponent<ToolCupboard>();
            if (cupboard == null) cupboard = gameObject.AddComponent<ToolCupboard>();
        }

        public override void Initialize(ulong owner, uint building)
        {
            itemId = string.IsNullOrEmpty(itemId) ? "cupboard.tool" : itemId;
            base.Initialize(owner, building);
            health = 250f;                             // Rust: TC 250 HP
            cupboard.ownerId = owner;
            cupboard.buildingId = building;
            cupboard.authorized.Add(owner);
        }

        public override void OnUse(Subsistence.Player.PlayerController player)
        {
            cupboard.authorized.Add(NetworkBridge.Host?.LocalPlayerId ?? 0UL);   // хозяин всегда авторизован
            Subsistence.UI.HudRuntime.ShowToast("Tool Cupboard: авторизация выдана. Внутрь — лес/камень/металл на апкип");
            Subsistence.UI.InventoryUI.OpenContainer(cupboard.storage);
        }
    }

    /// <summary>Описание того, как предмет становится объектом в мире.</summary>
    public struct DeploySpec
    {
        public Vector3 size;        // габариты для проверки места и коллайдера-прокси
        public bool wallMounted;    // ставится на стену (двери, замки, знаки, баррикады)
        public float modelHeight;   // высота модели (для подгонки по нижней точке)
        public string model;        // имя модели в Resources/Models (может быть null)
    }

    /// <summary>Фабрика деплоев: itemId → компонент + визуал. Единая точка постановки.</summary>
    public static class DeployableFactory
    {
        /// <summary>Категория предмета — деплой? (по ней игрок получает право ставить).</summary>
        public static bool IsDeployable(string itemId)
        {
            var def = ItemDatabase.Def(itemId);
            return def != null && def.category == ItemCategory.Deployable;
        }

        public static DeploySpec SpecOf(string itemId) => SpecOf(itemId, Vector3.zero);

        /// <summary>Спека деплоя; позиция нужна, чтобы выбрать палитру уровня (ответ 5в).</summary>
        public static DeploySpec SpecOf(string itemId, Vector3 at)
        {
            var s = new DeploySpec { size = new Vector3(0.6f, 0.6f, 0.6f), modelHeight = 0.6f };
            switch (itemId)
            {
                case "woodbox": s.size = new Vector3(0.9f, 0.6f, 0.6f); s.modelHeight = 0.62f; break;
                case "box.wooden.large": s.size = new Vector3(1.0f, 0.7f, 0.7f); s.modelHeight = 0.72f; break;
                case "furnace": s.size = new Vector3(0.8f, 1.0f, 0.8f); s.modelHeight = 1.02f; break;
                case "furnace.large": s.size = new Vector3(1.6f, 1.2f, 1.2f); s.modelHeight = 1.24f; break;
                case "workbench1": s.size = new Vector3(1.2f, 1.0f, 0.8f); s.modelHeight = 1.05f; break;
                case "workbench2": s.size = new Vector3(1.4f, 1.0f, 0.9f); s.modelHeight = 1.08f; break;
                case "workbench3": s.size = new Vector3(1.6f, 1.0f, 1.0f); s.modelHeight = 1.10f; break;
                case "cupboard.tool": s.size = new Vector3(0.7f, 1.0f, 0.5f); s.modelHeight = 1.05f; break;
                case "sleepingbag": s.size = new Vector3(0.8f, 0.2f, 1.9f); s.modelHeight = 0.22f; break;
                case "bed": s.size = new Vector3(1.0f, 0.4f, 2.0f); s.modelHeight = 0.42f; break;
                case "water.purifier": s.size = new Vector3(0.7f, 1.0f, 0.7f); s.modelHeight = 1.02f; break;
                case "autoturret": s.size = new Vector3(0.6f, 1.4f, 0.6f); s.modelHeight = 1.42f; break;
                case "flameturret": s.size = new Vector3(0.6f, 1.4f, 0.6f); s.modelHeight = 1.42f; break;
                case "samsite": s.size = new Vector3(0.8f, 1.3f, 0.8f); s.modelHeight = 1.32f; break;
                case "repair.bench": s.size = new Vector3(1.0f, 1.0f, 0.8f); s.modelHeight = 1.02f; break;
                case "research.table": s.size = new Vector3(1.0f, 1.0f, 0.8f); s.modelHeight = 1.02f; break;
                case "vending.machine": s.size = new Vector3(0.9f, 1.9f, 0.7f); s.modelHeight = 1.92f; break;
                case "cart.loot": s.size = new Vector3(1.0f, 0.8f, 1.4f); s.modelHeight = 1.05f; break;
                case "scooter": s.size = new Vector3(0.6f, 1.1f, 1.5f); s.modelHeight = 1.15f; break;
                case "generator.wind.scrap": s.size = new Vector3(1.0f, 2.2f, 1.0f); s.modelHeight = 2.20f; break;
                case "sign.pictureframe": s.size = new Vector3(0.6f, 0.8f, 0.1f); s.modelHeight = 0.80f; s.wallMounted = true; break;
                case "wall.frame.cell.gate": s.size = new Vector3(1.6f, 2.1f, 0.2f); s.modelHeight = 2.10f; s.wallMounted = true; break;
                case "barricade.concrete": s.size = new Vector3(1.6f, 1.2f, 0.4f); s.modelHeight = 1.20f; s.wallMounted = true; break;
                case "barricade.metal": s.size = new Vector3(1.6f, 1.2f, 0.4f); s.modelHeight = 1.20f; s.wallMounted = true; break;
                case "door.hinged.wood": s.size = new Vector3(1.0f, 2.05f, 0.15f); s.modelHeight = 2.05f; s.wallMounted = true; break;
                case "door.hinged.metal": s.size = new Vector3(1.0f, 2.05f, 0.2f); s.modelHeight = 2.05f; s.wallMounted = true; break;
                case "door.hinged.toptier": s.size = new Vector3(1.0f, 2.05f, 0.25f); s.modelHeight = 2.05f; s.wallMounted = true; break;
                case "door.double.hinged.metal": s.size = new Vector3(2.0f, 2.05f, 0.2f); s.modelHeight = 2.05f; s.wallMounted = true; break;
                case "trap.spikes": s.size = new Vector3(1.0f, 0.3f, 1.0f); s.modelHeight = 0.32f; break;
                case "trap.bear": s.size = new Vector3(0.5f, 0.2f, 0.5f); s.modelHeight = 0.20f; break;
                case "lock.key": case "lock.code":
                    s.size = new Vector3(0.2f, 0.3f, 0.15f); s.modelHeight = 0.30f; s.wallMounted = true; break;
            }
            // У дверей модель крепится к створке внутри DoorDeployable (нужен шарнир) — общий визуал не нужен.
            s.model = (itemId != null && itemId.StartsWith("door.")) ? null : World.ModelLibrary.ForDeployableAt(itemId, at);
            return s;
        }

        /// <summary>Создаёт объект в мире. Возвращает null, если места нет.</summary>
        public static GameObject Create(string itemId, Vector3 pos, float yaw, ulong owner, out DeploySpec spec)
        {
            spec = SpecOf(itemId, pos);          // палитра уровня выбирается по месту установки

            var go = new GameObject($"Deploy_{itemId}");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.layer = Layers.Deployable;

            // Коллайдер-прокси: по нему работают рейкасты, «E» и физика расчистки.
            var proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            proxy.name = "Body";
            proxy.transform.SetParent(go.transform, false);
            proxy.transform.localScale = spec.size;
            proxy.transform.localPosition = new Vector3(0f, spec.size.y * 0.5f, 0f);
            proxy.layer = Layers.Deployable;

            BuildDeployable comp = AttachLogic(go, itemId);

            // Визуал: модель из Blender, иначе куб остаётся заглушкой (серый «макет»).
            var visual = World.ModelLibrary.AttachFitted(spec.model, go.transform, spec.modelHeight,
                                                         UnityEngine.Random.Range(0f, 360f));
            if (visual != null)
            {
                var r = proxy.GetComponent<MeshRenderer>();
                if (r != null) r.enabled = false;          // куб → невидимый прокси
            }

            if (comp != null)
            {
                comp.itemId = itemId;
                // Привязка к ближайшей базе игрока (TC) — для апкипа и гниения.
                uint building = 0u;
                for (int i = 0; i < ToolCupboard.All.Count; i++)
                {
                    var tc = ToolCupboard.All[i];
                    if (tc == null || tc.ownerId != owner) continue;
                    if (Vector3.Distance(tc.transform.position, pos) <= tc.radius) { building = tc.buildingId; break; }
                }
                comp.Initialize(owner, building);
            }
            return go;
        }

        static BuildDeployable AttachLogic(GameObject go, string itemId)
        {
            switch (itemId)
            {
                // Rust: малый ящик — 6 слотов, большой — 30 (контейнер пересоздаём: Awake уже прошёл)
                case "woodbox":
                    var sb = go.AddComponent<StorageBox>();
                    sb.slots = 6; sb.storage = new ItemContainer("Box", 6);
                    return sb;
                case "box.wooden.large":
                    var lb = go.AddComponent<StorageBox>();
                    lb.slots = 30; lb.storage = new ItemContainer("Box", 30);
                    return lb;
                case "furnace": return go.AddComponent<Furnace>();
                case "furnace.large":
                    var lf = go.AddComponent<Furnace>();
                    lf.isLarge = true; lf.inputSlots = 8; lf.outputSlots = 8;
                    lf.input = new ItemContainer("Furnace Input", 8, ItemCategory.Resource);
                    lf.output = new ItemContainer("Furnace Output", 8);
                    return lf;
                case "workbench1": { var w = go.AddComponent<Workbench>(); w.level = 1; return w; }
                case "workbench2": { var w = go.AddComponent<Workbench>(); w.level = 2; w.craftSpeedMult = 2f; return w; }
                case "workbench3": { var w = go.AddComponent<Workbench>(); w.level = 3; w.craftSpeedMult = 3f; return w; }
                case "cupboard.tool": return go.AddComponent<ToolCupboardBox>();
                case "sleepingbag": return go.AddComponent<SleepBag>();
                case "bed": { var b = go.AddComponent<SleepBag>(); b.resetsLeft = 25; return b; }
                case "water.purifier": return go.AddComponent<WaterPurifier>();
                case "autoturret":
                case "flameturret": return go.AddComponent<AutoTurret>();
                case "samsite": return go.AddComponent<SamSite>();
                case "repair.bench": return go.AddComponent<RepairBench>();
                case "research.table": return go.AddComponent<ResearchTable>();
                case "door.hinged.wood":
                case "door.hinged.metal":
                case "door.hinged.toptier":
                case "door.double.hinged.metal": return go.AddComponent<DoorDeployable>();
                case "vending.machine": return go.AddComponent<Subsistence.AI.VendingMachine>();
                case "cart.loot": return go.AddComponent<Subsistence.Player.LootCart>();
                case "scooter": return go.AddComponent<Subsistence.Player.Scooter>();
                default:
                    // Прочие деплои (двери, замки, знаки, баррикады, ловушки, генератор):
                    // пока живут как прочные объекты с владельцем — логика дверей/замков в 16_doors.
                    return go.AddComponent<BuildDeployable>();
            }
        }
    }

    /// <summary>
    /// Контроллер постановки: держишь деплой в руках → ЛКМ ставит.
    /// Призрак показывает, влезет ли объект и разрешает ли шкаф соседа.
    /// </summary>
    public class DeployPlacer : MonoBehaviour
    {
        public Camera viewCamera;
        public PlayerInventory inventory;
        public float maxDistance = 5f;

        GameObject _ghost;
        string _ghostFor;
        bool _ghostValid;
        float _lastPlace;

        void Update()
        {
            if (inventory == null || viewCamera == null) return;
            if (Subsistence.UI.UIState.AnyMenuOpen) { ClearGhost(); return; }

            var active = inventory.ActiveItem;
            string id = active == null || active.IsEmpty ? null : active.id;
            if (id == null || !DeployableFactory.IsDeployable(id)) { ClearGhost(); return; }

            // Замки не ставятся на пол — они вешаются на дверь (взял замок → ЛКМ по створке)
            if (id == "lock.code" || id == "lock.key") { HandleLockItem(id); return; }

            var spec = DeployableFactory.SpecOf(id);
            Vector3 pos;
            float yaw;
            _ghostValid = ResolvePlacement(id, spec, out pos, out yaw);
            UpdateGhost(id, spec, pos, yaw, _ghostValid);

            if (!_ghostValid || !Subsistence.Player.PlayerInput.FirePressed || Time.time - _lastPlace < 0.3f) return;

            _lastPlace = Time.time;

            // Клиент не ждёт сервер: ставит объект у себя сразу (предсказание) и просит подтверждение.
            // Платит он тоже сам — сервер только уменьшает зеркало и проверяет возможность постановки.
            if (!NetworkBridge.IsServer)
            {
                SpawnNet.RequestPlace(id, pos, yaw);
                SpawnNet.PredictDeployable(id, pos, yaw);   // видно мгновенно; сервер пришлёт netId
                inventory.RemoveAmount(id, 1);
                Subsistence.UI.HudRuntime.ShowToast($"Ставлю: {ItemDatabase.Def(id)?.nameRu ?? id}");
                return;
            }

            ulong owner = NetworkBridge.Host?.LocalPlayerId ?? 0UL;
            var placed = SpawnNet.ServerSpawnDeployable(id, pos, yaw, owner);
            if (placed == null) return;
            var placedSpec = DeployableFactory.SpecOf(id, pos);

            inventory.RemoveAmount(id, 1);
            Subsistence.UI.HudRuntime.ShowToast($"Поставлено: {ItemDatabase.Def(id)?.nameRu ?? id}");
            Debug.Log($"[Deploy] {id} → {pos} (owner {owner}, h={placedSpec.modelHeight:F2})");

            // Сеть: здесь будет Command для клиентов (Mirror RPC) — см. 30/31.
        }

        /// <summary>Вешает замок на дверь, на которую смотрит игрок.</summary>
        void HandleLockItem(string id)
        {
            ClearGhost();
            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore)) return;

            var door = hit.collider.GetComponentInParent<DoorDeployable>();
            if (door == null)
            {
                if (Subsistence.Player.PlayerInput.FirePressed)
                    Subsistence.UI.HudRuntime.ShowToast("Замок ставится на дверь (наведись на створку)");
                return;
            }
            if (door.doorLock != null)
            {
                Subsistence.UI.HudRuntime.ShowToast($"На двери уже есть {door.doorLock.Label}");
                return;
            }
            if (!Subsistence.Player.PlayerInput.FirePressed || Time.time - _lastPlace < 0.3f) return;

            _lastPlace = Time.time;

            // Клиент просит сервер: замок + ключ создаёт сервер (ключ придёт в инвентарь).
            if (!NetworkBridge.IsServer)
            {
                SpawnNet.RequestAttachLock(id, door.transform.position);
                inventory.RemoveAmount(id, 1);
                Subsistence.UI.HudRuntime.ShowToast("Замок поставлен (сервер выдаст ключ)");
                return;
            }

            ulong owner = NetworkBridge.Host?.LocalPlayerId ?? 0UL;
            var l = door.AttachLock(id, owner);
            if (l == null) return;
            inventory.RemoveAmount(id, 1);

            if (l.kind == DoorLock.Kind.Key)
            {
                // парный ключ с тем же id (в Rust ключ тоже остаётся у владельца)
                var key = new ItemStack("lock.key", 1);
                key.metaTag = l.keyId;
                inventory.TryAdd(key);
                Subsistence.UI.HudRuntime.ShowToast($"Поставлен {l.Label}. Ключ у тебя, не потеряй");
            }
            else
            {
                Subsistence.UI.HudRuntime.ShowToast($"Поставлен {l.Label}. Код: {l.code} — запомни (в Rust код можно сменить)");
            }
            Debug.Log($"[Deploy] {id} → дверь {door.itemId} ({l.Label})");
        }

        /// <summary>Точка и поворот для деплоя + проверка: место свободно и привилегия своя.</summary>
        bool ResolvePlacement(string itemId, DeploySpec spec, out Vector3 pos, out float yaw)
        {
            pos = Vector3.zero; yaw = 0f;
            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (hit.collider.GetComponentInParent<Subsistence.Player.PlayerController>() != null) return false;

            yaw = transform.eulerAngles.y;                                 // Rust: объект «смотрит» на игрока
            if (spec.wallMounted)
            {
                if (Mathf.Abs(hit.normal.y) > 0.7f) return false;         // на пол двери не ставим
                pos = hit.point + hit.normal * (spec.size.z * 0.5f + 0.02f);
                yaw = Quaternion.LookRotation(hit.normal).eulerAngles.y;
            }
            else
            {
                if (hit.normal.y < 0.5f) return false;                     // ставим на поверхность/пол
                pos = hit.point + Vector3.up * 0.02f;
            }

            // Привилегия: чужой Tool Cupboard рядом → нельзя (своя база — можно)
            ulong owner = NetworkBridge.Host?.LocalPlayerId ?? 0UL;
            if (!BuildingPrivilege.CanBuild(pos, owner, out string reason))
            {
                Subsistence.UI.HudRuntime.ShowToast(reason);
                return false;
            }

            // Место свободно? (нельзя ставить в стену/другой деплой)
            var half = spec.size * 0.5f;
            var center = pos + Vector3.up * half.y;
            var hits = Physics.OverlapBox(center, new Vector3(half.x * 0.92f, half.y * 0.92f, half.z * 0.92f),
                                          Quaternion.Euler(0f, yaw, 0f),
                                          Layers.Mask(Layers.LevelGeometry, Layers.Buildable, Layers.Deployable),
                                          QueryTriggerInteraction.Ignore);
            return hits == null || hits.Length == 0;
        }

        void UpdateGhost(string id, DeploySpec spec, Vector3 pos, float yaw, bool valid)
        {
            if (_ghost == null || _ghostFor != id)
            {
                ClearGhost();
                _ghostFor = id;
                _ghost = new GameObject($"Ghost_{id}");
                _ghost.layer = Layers.Deployable;
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "GhostBody";
                box.transform.SetParent(_ghost.transform, false);
                box.transform.localScale = spec.size;
                box.transform.localPosition = new Vector3(0f, spec.size.y * 0.5f, 0f);
                Object.Destroy(box.GetComponent<Collider>());              // призрак ничего не задевает

                // У дверей модель крепится к створке при спавне — но в призраке показываем её сразу.
                string ghostModel = spec.model;
                if (ghostModel == null && id != null && id.StartsWith("door."))
                    ghostModel = World.ModelLibrary.ForDeployableAt(id, _ghost.transform.position);
                var model = World.ModelLibrary.AttachFitted(ghostModel, _ghost.transform, spec.modelHeight, 0f);
                if (model != null)
                {
                    var r = box.GetComponent<MeshRenderer>();
                    if (r != null) r.enabled = false;
                }
            }

            _ghost.transform.position = pos;
            _ghost.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _ghost.SetActive(true);

            // Подсветка: зелёный — можно, красный — нельзя (Tint красит и модель, и куб).
            var color = valid ? new Color(0.35f, 1f, 0.45f, 1f) : new Color(1f, 0.30f, 0.25f, 1f);
            var renderers = _ghost.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++) World.ModelLibrary.Tint(renderers[i].gameObject, color);
        }

        void ClearGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null; _ghostFor = null;
        }
    }
}
