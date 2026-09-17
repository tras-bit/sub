// ============================================================================
//  SUBSISTENCE — Core/SaveSystem.cs
//  Сохранение мира (чего раньше не было вообще: база, прогресс и лут жили один сеанс).
//
//  Что сохраняется:
//    • игрок: позиция, поворот, здоровье/голод/жажда/радиация/усталость, рюкзак целиком
//      (с износом, патронами в магазине, обвесами, метками ключей), активный слот;
//    • исследования: изученные чертежи (TechTree);
//    • постройки: каждый блок (элемент, тир, поворот, прочность, владелец, база, время постановки);
//    • деплои: верстаки/печи/ящики/двери/мешки/турели — с содержимым и состоянием
//      (код замка, id ключа, уровень верстака, остаток респавнов мешка);
//    • шкафы (TC): владелец, список авторизованных, содержимое;
//    • лут-ящики мира: что уже вылутано и когда зареспавнится;
//    • мир: сид, погода (событие + таймер).
//
//  Как играть:
//    F5 — сохранить, F9 — загрузить, F10 — удалить сохранение. Автосейв — раз в 5 минут
//    и при выходе из игры (только на сервере/хосте: клиент мир не сохраняет).
//    Если сохранение есть, при старте консоль загрузки пишет об этом и ждёт F9.
//
//  Формат: JSON в Application.persistentDataPath/Subsistence/<слот>.json — человекочитаемо,
//  легко чинится руками, не ломается при обновлении игры (поле version).
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Subsistence.Building;
using Subsistence.Crafting;
using Subsistence.Net;
using Subsistence.Player;
using Subsistence.World;

namespace Subsistence.Core
{
    // ---------------------------------------------------------------- данные

    [Serializable]
    public class SavStack
    {
        public string id;
        public int amount;
        public float durability;
        public float condition = 1f;
        public int ammoInMag;
        public int metaTag;
        public string skinId;
        public List<string> attachments = new List<string>(2);

        public static SavStack From(ItemStack s)
        {
            if (s == null || s.IsEmpty) return null;
            var v = new SavStack
            {
                id = s.id, amount = s.amount, durability = s.durability, condition = s.condition,
                ammoInMag = s.ammoInMag, metaTag = s.metaTag, skinId = s.skinId
            };
            if (s.attachments != null && s.attachments.Count > 0) v.attachments = new List<string>(s.attachments);
            return v;
        }

        public ItemStack ToStack()
        {
            var s = new ItemStack(id, Mathf.Max(1, amount))
            {
                durability = durability, condition = condition <= 0f ? 1f : condition,
                ammoInMag = ammoInMag, metaTag = metaTag, skinId = skinId
            };
            if (attachments != null)
                for (int i = 0; i < attachments.Count; i++) if (!string.IsNullOrEmpty(attachments[i])) s.attachments.Add(attachments[i]);
            return s;
        }
    }

    [Serializable]
    public class SavContainer
    {
        public int slots;
        public List<SavStack> items = new List<SavStack>();     // null-элемент = пустой слот

        public static SavContainer From(ItemContainer c)
        {
            if (c == null) return null;
            var v = new SavContainer { slots = c.SlotCount };
            for (int i = 0; i < c.SlotCount; i++) v.items.Add(SavStack.From(c.Get(i)));
            return v;
        }

        public void ApplyTo(ItemContainer c)
        {
            if (c == null || items == null) return;
            int n = Mathf.Min(c.SlotCount, items.Count);
            for (int i = 0; i < n; i++) c.Set(i, items[i] == null ? null : items[i].ToStack());
        }
    }

    [Serializable]
    public class SavBlock
    {
        public int piece, tier, rot;
        public float x, y, z, hp, placedAt;
        public ulong owner;
        public uint buildingId;
    }

    [Serializable]
    public class SavDeploy
    {
        public string itemId;
        public float x, y, z, yaw, health;
        public ulong owner;
        public uint buildingId;
        public int level;              // верстак
        public SavContainer storage;   // ящик / шкаф / мешок / карго
        public SavContainer storage2;  // вторая ёмкость (печь: выход)
        public int keyId;              // дверь: id ключа
        public string code;            // дверь: код замка
        public bool locked;
        public int resetsLeft;         // спальник
        public bool isBed;
    }

    [Serializable]
    public class SavCupboard
    {
        public float x, y, z;
        public ulong owner;
        public uint buildingId;
        public List<ulong> authorized = new List<ulong>();
        public SavContainer storage;
    }

    [Serializable]
    public class SavLoot
    {
        public float x, y, z;
        public string tableId;
        public int kind;
        public bool looted;
        public float respawnIn;        // сколько секунд осталось до респавна (0 — не ждёт)
        public SavContainer items;
    }

    [Serializable]
    public class SavPlayer
    {
        public float x, y, z, yaw;
        public float health, calories, hydration, radiation, wetness, stamina;
        public int hotbar;
        public SavContainer inventory;
        public List<string> known = new List<string>();
    }

    [Serializable]
    public class WorldSave
    {
        public int version = SaveSystem.Version;
        public string savedAt;
        public string gameVersion = "1.0.0-alpha";
        public int seed;
        public string weatherKind;
        public float weatherLeft;
        public SavPlayer player = new SavPlayer();
        public List<SavBlock> blocks = new List<SavBlock>();
        public List<SavDeploy> deploys = new List<SavDeploy>();
        public List<SavCupboard> cupboards = new List<SavCupboard>();
        public List<SavLoot> loot = new List<SavLoot>();
    }

    // ---------------------------------------------------------------- система

    public static class SaveSystem
    {
        public const int Version = 1;
        public const string AutoSlot = "auto";
        public const string ManualSlot = "manual";

        public static string Dir => Path.Combine(Application.persistentDataPath, "Subsistence");
        public static string PathFor(string slot) => Path.Combine(Dir, slot + ".json");

        public static bool Has(string slot) => File.Exists(PathFor(slot));
        public static bool HasAny => Has(AutoSlot) || Has(ManualSlot);

        public static string Info(string slot)
        {
            var path = PathFor(slot);
            if (!File.Exists(path)) return null;
            try
            {
                var w = JsonUtility.FromJson<WorldSave>(File.ReadAllText(path));
                var t = File.GetLastWriteTime(path);
                return $"{slot}: {t:dd.MM HH:mm} · блоков {w.blocks.Count} · деплоев {w.deploys.Count} · " +
                       $"ящиков {w.loot.Count} · изучено {w.player.known.Count}";
            }
            catch { return $"{slot}: файл повреждён ({path})"; }
        }

        public static void Delete(string slot)
        {
            var p = PathFor(slot);
            if (File.Exists(p)) File.Delete(p);
        }

        // ------------------------------------------------------------ сохранение

        /// <summary>Сохранить мир. Только сервер/хост — клиент состояние мира не пишет.</summary>
        public static bool Save(string slot = AutoSlot)
        {
            if (!NetworkBridge.IsServer)
            {
                UI.HudRuntime.ShowToast("Мир сохраняет хост (в офлайне — ты и есть хост)");
                return false;
            }

            var data = new WorldSave
            {
                savedAt = DateTime.Now.ToString("u"),
                seed = Runtime.RuntimeBootstrap.SeedValue
            };

            // --- игрок ---
            var pc = UnityEngine.Object.FindObjectOfType<PlayerController>();
            if (pc != null)
            {
                data.player.x = pc.transform.position.x;
                data.player.y = pc.transform.position.y;
                data.player.z = pc.transform.position.z;
                data.player.yaw = pc.transform.eulerAngles.y;

                var surv = pc.GetComponent<SurvivalSystem>();
                if (surv != null)
                {
                    var st = surv.State;                       // статистика — в структуре SurvivalState
                    data.player.health = st.health;
                    data.player.calories = st.calories;
                    data.player.hydration = st.hydration;
                    data.player.radiation = st.radiation;
                    data.player.wetness = st.wetness;
                    data.player.stamina = st.stamina;
                }

                if (pc.inventory != null)
                {
                    data.player.hotbar = pc.inventory.ActiveHotbarIndex;
                    data.player.inventory = SavContainer.From(pc.inventory);
                }
                data.player.known = TechTree.ExportKnown(pc.OwnerId);
            }

            // --- постройки ---
            var blocks = UnityEngine.Object.FindObjectsOfType<BuildBlock>();
            for (int i = 0; i < blocks.Length; i++)
            {
                var b = blocks[i];
                if (b == null) continue;
                data.blocks.Add(new SavBlock
                {
                    piece = (int)b.piece, tier = (int)b.tier, rot = Mathf.RoundToInt(b.transform.eulerAngles.y / 90f),
                    x = b.transform.position.x, y = b.transform.position.y, z = b.transform.position.z,
                    hp = b.health, owner = b.ownerId, buildingId = b.buildingId, placedAt = b.placedAt
                });
            }

            // --- деплои (с содержимым и состоянием) ---
            var deploys = UnityEngine.Object.FindObjectsOfType<BuildDeployable>();
            for (int i = 0; i < deploys.Length; i++)
            {
                var d = deploys[i];
                if (d == null || string.IsNullOrEmpty(d.itemId)) continue;
                var e = new SavDeploy
                {
                    itemId = d.itemId, x = d.transform.position.x, y = d.transform.position.y, z = d.transform.position.z,
                    yaw = d.transform.eulerAngles.y, health = d.health, owner = d.ownerId, buildingId = d.buildingId
                };

                if (d is StorageBox box) e.storage = SavContainer.From(box.storage);
                else if (d is LootCart cart) e.storage = SavContainer.From(cart.storage);
                else if (d is Workbench wb) { e.level = wb.level; }
                else if (d is Furnace fur) { e.storage = SavContainer.From(fur.input); e.storage2 = SavContainer.From(fur.output); }
                else if (d is SleepBag bag) { e.resetsLeft = bag.resetsLeft; e.isBed = bag.isBed; }
                else if (d is ToolCupboardBox tcb && tcb.cupboard != null)
                {
                    e.storage = SavContainer.From(tcb.cupboard.storage);
                }

                var door = d.GetComponent<DoorDeployable>();
                if (door != null)
                {
                    var lk = d.GetComponentInChildren<DoorLock>();
                    if (lk != null) { e.keyId = lk.keyId; e.code = lk.code; e.locked = lk.locked; }
                }
                data.deploys.Add(e);
            }

            // --- шкафы ---
            for (int i = 0; i < ToolCupboard.All.Count; i++)
            {
                var c = ToolCupboard.All[i];
                if (c == null) continue;
                var sc = new SavCupboard
                {
                    x = c.transform.position.x, y = c.transform.position.y, z = c.transform.position.z,
                    owner = c.ownerId, buildingId = c.buildingId, storage = SavContainer.From(c.storage)
                };
                foreach (var id in c.authorized) sc.authorized.Add(id);
                data.cupboards.Add(sc);
            }

            // --- лут-ящики мира ---
            for (int i = 0; i < LootContainer.All.Count; i++)
            {
                var lc = LootContainer.All[i];
                if (lc == null) continue;
                data.loot.Add(new SavLoot
                {
                    x = lc.transform.position.x, y = lc.transform.position.y, z = lc.transform.position.z,
                    tableId = lc.tableId, kind = (int)lc.kind, looted = lc.IsEmpty,
                    respawnIn = lc.RespawnAt > 0f ? Mathf.Max(0f, lc.RespawnAt - Time.time) : 0f,
                    items = lc.IsEmpty ? null : SavContainer.From(lc.Items)
                });
            }

            // --- погода ---
            var weather = WeatherSystem.Instance;
            if (weather != null)
            {
                data.weatherKind = weather.CurrentKind.ToString();
                data.weatherLeft = weather.RemainingSeconds;
            }

            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(PathFor(slot), JsonUtility.ToJson(data, true));
                UI.HudRuntime.ShowToast($"Мир сохранён ({slot}): блоков {data.blocks.Count}, деплоев {data.deploys.Count}, " +
                                        $"ящиков {data.loot.Count}");
                Debug.Log($"[Save] {PathFor(slot)} — блоков {data.blocks.Count}, деплоев {data.deploys.Count}, " +
                          $"шкафов {data.cupboards.Count}, лута {data.loot.Count}");
                return true;
            }
            catch (Exception ex)
            {
                UI.HudRuntime.ShowToast("Не удалось сохранить мир — смотри консоль");
                Debug.LogError("[Save] ошибка записи: " + ex);
                return false;
            }
        }

        // ------------------------------------------------------------ загрузка

        /// <summary>Загрузить мир: снести старое, поднять постройки/деплои/лут, вернуть игрока.</summary>
        public static bool Load(string slot = AutoSlot)
        {
            var path = PathFor(slot);
            if (!File.Exists(path)) { UI.HudRuntime.ShowToast($"Сохранение «{slot}» не найдено"); return false; }
            if (!NetworkBridge.IsServer) { UI.HudRuntime.ShowToast("Загрузить мир может только хост"); return false; }

            WorldSave data;
            try { data = JsonUtility.FromJson<WorldSave>(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                Debug.LogError("[Save] файл не читается: " + ex);
                UI.HudRuntime.ShowToast("Сохранение повреждено — начинаем новый мир");
                return false;
            }
            if (data == null) return false;
            if (data.version > Version)
                Debug.LogWarning($"[Save] сохранение новее игры (v{data.version} > v{Version}) — часть данных может пропасть");

            WipeWorld();

            // постройки
            int okBlocks = 0;
            for (int i = 0; i < data.blocks.Count; i++)
            {
                var b = data.blocks[i];
                var block = SpawnNet.ServerSpawnBlock((BuildPieceType)b.piece, (BuildTier)b.tier, b.rot,
                                                      new Vector3(b.x, b.y, b.z), b.owner, b.buildingId);
                if (block == null) continue;
                block.health = b.hp > 0f ? b.hp : block.health;
                block.placedAt = b.placedAt;
                okBlocks++;
            }

            // деплои
            int okDeploys = 0;
            for (int i = 0; i < data.deploys.Count; i++)
            {
                var e = data.deploys[i];
                var go = DeployableFactory.Create(e.itemId, new Vector3(e.x, e.y, e.z), e.yaw, e.owner, out _);
                if (go == null) continue;
                var d = go.GetComponent<BuildDeployable>();
                if (d != null)
                {
                    if (e.health > 0f) d.health = e.health;
                    d.buildingId = e.buildingId;

                    if (d is StorageBox box && e.storage != null) e.storage.ApplyTo(box.storage);
                    else if (d is LootCart cart && e.storage != null) e.storage.ApplyTo(cart.storage);
                    else if (d is Workbench wb && e.level > 0) wb.level = e.level;
                    else if (d is Furnace fur)
                    {
                        if (e.storage != null) e.storage.ApplyTo(fur.input);
                        if (e.storage2 != null) e.storage2.ApplyTo(fur.output);
                    }
                    else if (d is SleepBag bag) { bag.resetsLeft = e.resetsLeft; bag.isBed = e.isBed; }
                    else if (d is ToolCupboardBox tcb && e.storage != null && tcb.cupboard != null)
                        e.storage.ApplyTo(tcb.cupboard.storage);

                    var door = go.GetComponent<DoorDeployable>();
                    if (door != null)
                    {
                        var lk = go.GetComponentInChildren<DoorLock>();
                        if (lk != null) { lk.keyId = e.keyId; if (!string.IsNullOrEmpty(e.code)) lk.code = e.code; lk.locked = e.locked; }
                    }
                }
                okDeploys++;
            }

            // шкафы (авторизация)
            for (int i = 0; i < data.cupboards.Count; i++)
            {
                var sc = data.cupboards[i];
                var cup = FindCupboardNear(new Vector3(sc.x, sc.y, sc.z));
                if (cup == null) continue;
                cup.ownerId = sc.owner;
                if (sc.buildingId != 0) cup.buildingId = sc.buildingId;
                cup.authorized.Clear();
                for (int k = 0; k < sc.authorized.Count; k++) cup.authorized.Add(sc.authorized[k]);
                if (sc.storage != null) sc.storage.ApplyTo(cup.storage);
            }

            // лут-ящики
            int okLoot = 0;
            for (int i = 0; i < data.loot.Count; i++)
            {
                var sl = data.loot[i];
                var lc = LootContainer.FindAt(new Vector3(sl.x, sl.y, sl.z), 1.5f);
                if (lc == null) continue;                    // ящик не нашёлся (другой сид) — пропускаем
                if (sl.respawnIn > 0f) lc.RespawnAt = Time.time + sl.respawnIn;
                if (sl.items != null) sl.items.ApplyTo(lc.Items);
                else if (sl.looted) lc.Looted();
                okLoot++;
            }

            // игрок
            var pc = UnityEngine.Object.FindObjectOfType<PlayerController>();
            if (pc != null)
            {
                pc.Respawn(new Vector3(data.player.x, data.player.y, data.player.z));
                pc.transform.rotation = Quaternion.Euler(0f, data.player.yaw, 0f);
                var surv = pc.GetComponent<SurvivalSystem>();
                if (surv != null)
                {
                    var st = surv.State;                       // пишем в структуру и кладём обратно
                    st.health = data.player.health > 0f ? data.player.health : 100f;
                    st.calories = data.player.calories;
                    st.hydration = data.player.hydration;
                    st.radiation = data.player.radiation;
                    st.wetness = data.player.wetness;
                    st.stamina = data.player.stamina;
                    surv.State = st;
                }
                if (pc.inventory != null && data.player.inventory != null)
                {
                    for (int i = 0; i < pc.inventory.SlotCount; i++) pc.inventory.Set(i, null);
                    data.player.inventory.ApplyTo(pc.inventory);
                    pc.inventory.SelectHotbar(Mathf.Clamp(data.player.hotbar, 0, PlayerInventory.HotbarSlots - 1));
                }
                TechTree.ImportKnown(pc.OwnerId, data.player.known);
            }

            // погода
            var weather = WeatherSystem.Instance;
            if (weather != null && !string.IsNullOrEmpty(data.weatherKind))
                weather.Restore(data.weatherKind, data.weatherLeft);

            UI.HudRuntime.ShowToast($"Мир загружен ({slot} от {data.savedAt}): блоков {okBlocks}, деплоев {okDeploys}, ящиков {okLoot}");
            Debug.Log($"[Save] загружено: блоков {okBlocks}/{data.blocks.Count}, деплоев {okDeploys}/{data.deploys.Count}, " +
                      $"лута {okLoot}/{data.loot.Count}");
            return true;
        }

        /// <summary>Снести всё «поставленное игроком» перед загрузкой (мир уровня не трогаем).</summary>
        static void WipeWorld()
        {
            var blocks = UnityEngine.Object.FindObjectsOfType<BuildBlock>();
            for (int i = 0; i < blocks.Length; i++)
                if (blocks[i] != null) UnityEngine.Object.Destroy(blocks[i].gameObject);

            var deploys = UnityEngine.Object.FindObjectsOfType<BuildDeployable>();
            for (int i = 0; i < deploys.Length; i++)
                if (deploys[i] != null) UnityEngine.Object.Destroy(deploys[i].gameObject);

            var loose = UnityEngine.Object.FindObjectsOfType<ItemPickup>();
            for (int i = 0; i < loose.Length; i++)
                if (loose[i] != null) UnityEngine.Object.Destroy(loose[i].gameObject);

            BuildingRegistry.Clear();
        }

        static ToolCupboard FindCupboardNear(Vector3 p)
        {
            ToolCupboard best = null; float bestD = 3f;
            for (int i = 0; i < ToolCupboard.All.Count; i++)
            {
                var c = ToolCupboard.All[i];
                if (c == null) continue;
                float d = Vector3.Distance(c.transform.position, p);
                if (d <= bestD) { bestD = d; best = c; }
            }
            return best;
        }
    }

    /// <summary>
    /// Компонент на бутстрапе: горячие клавиши, автосейв и сообщение в консоль загрузки.
    /// Ставится в RuntimeBootstrap — отдельно ничего подключать не надо.
    /// </summary>
    public class SaveDirector : MonoBehaviour
    {
        public float autoSaveSeconds = 300f;      // 5 минут, как апкип в Rust
        public bool autoLoadLastOnStart = false;  // true — сразу продолжает последний мир

        float _timer;
        bool _announced;

        void Start()
        {
            _timer = autoSaveSeconds;
            if (autoLoadLastOnStart)
            {
                if (SaveSystem.Has(SaveSystem.AutoSlot)) SaveSystem.Load(SaveSystem.AutoSlot);
                return;
            }
        }

        void Update()
        {
            if (!NetworkBridge.IsServer) return;

            if (!_announced && Time.time > 12f)
            {
                _announced = true;
                if (SaveSystem.HasAny)
                    UI.HudRuntime.ShowToast("Есть сохранение мира. F9 — продолжить, F5 — сохранить сейчас, F10 — удалить");
            }

            if (Input.GetKeyDown(KeyCode.F5)) SaveSystem.Save(SaveSystem.ManualSlot);
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (!SaveSystem.Load(SaveSystem.ManualSlot) && SaveSystem.Has(SaveSystem.AutoSlot))
                    SaveSystem.Load(SaveSystem.AutoSlot);
            }
            if (Input.GetKeyDown(KeyCode.F10))
            {
                SaveSystem.Delete(SaveSystem.ManualSlot);
                SaveSystem.Delete(SaveSystem.AutoSlot);
                UI.HudRuntime.ShowToast("Сохранения удалены — следующий запуск будет новым миром");
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = autoSaveSeconds;
                SaveSystem.Save(SaveSystem.AutoSlot);       // тихо: тост покажет только результат
            }
        }

        void OnApplicationQuit()
        {
            if (NetworkBridge.IsServer) SaveSystem.Save(SaveSystem.AutoSlot);
        }
    }
}
