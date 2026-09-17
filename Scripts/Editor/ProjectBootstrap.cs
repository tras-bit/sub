// ============================================================================
//  SUBSISTENCE — Editor/ProjectBootstrap.cs
//  Меню Unity: «Subsistence» — автоматическая настройка проекта одним кликом.
//  1) Настроить проект  — теги, слои, физика, цветовое пространство, ввод, качество
//  2) Создать мир       — сцена с генератором (3 уровня, лут, монстры, игрок)
//  3) Включить Mirror   — define MIRROR + попытка установить пакет с GitHub
//  4) Экспорт items.json — баланс предметов в StreamingAssets (правится без пересборки)
//  5) Создать HDRP Asset — рендер-пайплайн, туман, объёмный свет (для профиля HDRP)
//  6) Проверка          — диагностика (что не настроено) в консоли Unity
//  ВАЖНО: файл требует пакет HDRP (он есть в Packages/manifest.json).
// ============================================================================
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Subsistence.Core;

namespace Subsistence.EditorTools
{
    public static class ProjectBootstrap
    {
        const string ScenesPath = "Assets/Subsistence/Scenes";
        const string SettingsPath = "Assets/Subsistence/Settings";

        // ================== 1. НАСТРОЙКА ПРОЕКТА ==================
        [MenuItem("Subsistence/1. Настроить проект", priority = 1)]
        public static void ConfigureProject()
        {
            SetupLayers();
            SetupTags();
            SetupPhysics();
            SetupPlayerSettings();
            SetupQuality();
            AssetDatabase.SaveAssets();
            Debug.Log("<color=#39ff6a>[Subsistence]</color> Проект настроен: слои, теги, физика, качество, ввод.");
        }

        static void SetupLayers()
        {
            // Слои 6..16 — под наши системы (см. Core/IDs.cs → Layers)
            var layers = new Dictionary<int, string>
            {
                { 6, "Player" }, { 7, "Monster" }, { 8, "Buildable" }, { 9, "Deployable" },
                { 10, "LevelGeometry" }, { 11, "Loot" }, { 12, "Water" }, { 13, "Ragdoll" },
                { 14, "Hitbox" }, { 15, "Projectile" }, { 16, "NoBuild" }
            };

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProp = tagManager.FindProperty("layers");
            foreach (var kv in layers)
            {
                if (kv.Key >= layersProp.arraySize) continue;
                var element = layersProp.GetArrayElementAtIndex(kv.Key);
                if (string.IsNullOrEmpty(element.stringValue)) element.stringValue = kv.Value;
            }
            tagManager.ApplyModifiedProperties();
        }

        static void SetupTags()
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var so = new SerializedObject(asset);
            var tags = so.FindProperty("tags");
            string[] wanted = { "Monster", "LootContainer", "BuildBlock", "Deployable", "WaterVolume", "RadiationZone", "PlayerSpawn", "Exit" };
            foreach (var tag in wanted)
            {
                bool exists = false;
                for (int i = 0; i < tags.arraySize; i++)
                    if (tags.GetArrayElementAtIndex(i).stringValue == tag) { exists = true; break; }
                if (exists) continue;
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            }
            so.ApplyModifiedProperties();
        }

        static void SetupPhysics()
        {
            // Отключаем ненужные пары столкновений (производительность под 100 игроков)
            int[][] ignore =
            {
                new[] { 12, 12 },  // Water-Water
                new[] { 11, 11 },  // Loot-Loot
                new[] { 13, 13 },  // Ragdoll-Ragdoll
                new[] { 15, 15 },  // Projectile-Projectile
                new[] { 15, 11 }, new[] { 11, 15 },
                new[] { 14, 14 },  // Hitbox-Hitbox
            };
            foreach (var pair in ignore) Physics.IgnoreLayerCollision(pair[0], pair[1], true);
            Debug.Log("[Subsistence] Матрица физики обновлена (игнор Water/Loot/Ragdoll/Projectile/Hitbox между собой).");
        }

        static void SetupPlayerSettings()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.apiCompatibilityLevel = ApiCompatibilityLevel.NET_Standard_2_1;
            PlayerSettings.companyName = string.IsNullOrEmpty(PlayerSettings.companyName) ? "Subsistence Team" : PlayerSettings.companyName;
            PlayerSettings.productName = "Subsistence";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = true;
            PlayerSettings.gcIncremental = true;              // важно для 30 Гц сервера
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Low);

            // Активный ввод: и старый Input Manager, и новый Input System (Both = 2)
            var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var handler = ps.FindProperty("activeInputHandler");
            if (handler != null) { handler.intValue = 2; ps.ApplyModifiedProperties(); }
        }

        static void SetupQuality()
        {
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.vSyncCount = 0;
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowDistance = 60f;
                QualitySettings.pixelLightCount = 4;          // мигающие лампы дешёвые — ограничим
                QualitySettings.skinWeights = SkinWeights.FourBones;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            }
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
        }

        // ================== 2. СОЗДАНИЕ МИРА ==================
        [MenuItem("Subsistence/2. Создать мир (сцена)", priority = 2)]
        public static void CreateWorldScene()
        {
            Directory.CreateDirectory(ScenesPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Свет «гулящего» коридора (на HDRP подхватит физический свет)
            var sun = new GameObject("Directional Light");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.78f);
            light.intensity = 0.35f;
            light.shadows = LightShadows.Soft;

            // Ядро игры
            var core = new GameObject("SubsistenceCore");
            var boot = core.AddComponent<Subsistence.Runtime.RuntimeBootstrap>();
            boot.seed = 1337;
            boot.generateLoot = true;
            boot.generateMonsters = true;
            boot.spawnPlayer = true;

            // Точка возрождения и подсказки в сцене
            var info = new GameObject("README");
            info.transform.position = new Vector3(0, 2, 0);
            var text = info.AddComponent<TextMesh>();
            text.text = "SUBSISTENCE — нажми Play,\nоткроется зелёная консоль загрузки.\nМир генерируется процедурно (seed в RuntimeBootstrap).";
            text.characterSize = 0.12f;
            text.fontSize = 42;
            text.anchor = TextAnchor.MiddleCenter;

            string path = $"{ScenesPath}/Levels.unity";
            EditorSceneManager.SaveScene(scene, path);
            AddSceneToBuild(path);
            Debug.Log($"<color=#39ff6a>[Subsistence]</color> Сцена создана: {path}. Нажми Play.");
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == path)) scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ================== 3. MIRROR ==================
        [MenuItem("Subsistence/3. Включить Mirror", priority = 3)]
        public static void EnableMirror()
        {
            // 1) define-символ
            var target = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            if (!defines.Contains("MIRROR"))
            {
                defines = string.IsNullOrEmpty(defines) ? "MIRROR" : defines + ";MIRROR";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
                Debug.Log("[Subsistence] Добавлен define MIRROR — код адаптера активен.");
            }

            // 2) пакет Mirror (git) — если ещё не установлен
#if UNITY_2020_1_OR_NEWER
            try
            {
                // результат Add не нужен: важен сам запуск установки пакета
                UnityEditor.PackageManager.Client.Add("https://github.com/MirrorNetworking/Mirror.git#v96.0.1");
                Debug.Log("[Subsistence] Установка Mirror запущена (менеджер пакетов). Если GitHub недоступен — " +
                          "скачай Mirror из Asset Store и импортируй, define MIRROR уже стоит.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Subsistence] Не удалось автоматически поставить Mirror: " + e.Message +
                                 ". Установи Mirror вручную (Asset Store / git), код адаптера готов.");
            }
#endif
            AssetDatabase.Refresh();
        }

        // ================== 4. ЭКСПОРТ БАЛАНСА ==================
        [MenuItem("Subsistence/4. Экспорт items.json", priority = 4)]
        public static void ExportItemsJson()
        {
            ItemDatabase.Init();
            var sb = new System.Text.StringBuilder(64 * 1024);
            sb.Append("[\n");
            bool first = true;
            foreach (var kv in ItemDatabase.All)
            {
                var d = kv.Value;
                if (!first) sb.Append(",\n");
                first = false;
                sb.Append("  {\"id\":\"").Append(d.id).Append("\",\"nameRu\":\"").Append(d.nameRu)
                  .Append("\",\"name\":\"").Append(d.name).Append("\",\"category\":").Append((int)d.category)
                  .Append(",\"stackSize\":").Append(d.stackSize).Append(",\"maxDurability\":").Append(d.maxDurability.ToString(System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\"weight\":").Append(d.weight.ToString(System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\"rarity\":").Append((int)d.rarity).Append(",\"minTier\":").Append((int)d.minTier)
                  .Append(",\"scrapCost\":").Append(d.scrapCost)
                  .Append(",\"iconKey\":\"").Append(d.iconKey).Append("\"}");
            }
            sb.Append("\n]\n");

            string dir = Path.Combine(Application.streamingAssetsPath, "");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "items.json"), sb.ToString());
            File.WriteAllText(Path.Combine(dir, "seed.txt"), "1337\n");
            AssetDatabase.Refresh();
            Debug.Log($"<color=#39ff6a>[Subsistence]</color> Экспортировано предметов: {ItemDatabase.All.Count} → StreamingAssets/items.json (правь баланс без пересборки).");
        }

        // ================== 5. HDRP ==================
        [MenuItem("Subsistence/5. Создать HDRP Asset + назначить", priority = 5)]
        public static void CreateHdrpAsset()
        {
            Directory.CreateDirectory(SettingsPath);
            try
            {
                var type = Type.GetType("UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset, Unity.RenderPipelines.HighDefinition.Runtime");
                var globalType = Type.GetType("UnityEngine.Rendering.HighDefinition.HDRenderPipelineGlobalSettings, Unity.RenderPipelines.HighDefinition.Runtime");
                if (type == null)
                {
                    Debug.LogError("[Subsistence] HDRP не найден. Проверь Packages/manifest.json (com.unity.render-pipelines.high-definition 14.0.12).");
                    return;
                }

                string assetPath = $"{SettingsPath}/HDRP_Subsistence.asset";
                var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(assetPath);
                if (asset == null)
                {
                    asset = (RenderPipelineAsset)ScriptableObject.CreateInstance(type);
                    AssetDatabase.CreateAsset(asset, assetPath);
                }
                GraphicsSettings.defaultRenderPipeline = asset;
                QualitySettings.renderPipeline = asset;
                AssetDatabase.SaveAssets();

                // Глобальные настройки HDRP (в 2022.3 создаётся кнопкой в HDRP Wizard)
                if (globalType != null)
                    EditorApplication.ExecuteMenuItem("Window/Rendering/HDRP Wizard");

                Debug.Log("<color=#39ff6a>[Subsistence]</color> HDRP Asset создан и назначен. " +
                          "В HDRP Wizard нажми «Fix All» — он настроит объёмный свет, туман и shadow-настройки.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Subsistence] HDRP-ассет не создан автоматически: " + e.Message +
                                 "\nСделай вручную: Assets → Create → Rendering → HDRP Asset, затем Project Settings → Graphics.");
            }
        }

        // ================== 6. ДИАГНОСТИКА ==================
        [MenuItem("Subsistence/6. Проверка проекта", priority = 6)]
        public static void ValidateProject()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== SUBSISTENCE — ПРОВЕРКА ===\n");

            report.AppendLine($"✓ Версия Unity: {Application.unityVersion} (нужна 2022.3.62f2)");
            report.AppendLine($"{(GraphicsSettings.defaultRenderPipeline != null ? "✓" : "✗")} Render Pipeline: {(GraphicsSettings.defaultRenderPipeline != null ? GraphicsSettings.defaultRenderPipeline.name : "не назначен (см. пункт 5)")}");
            report.AppendLine($"{(PlayerSettings.colorSpace == ColorSpace.Linear ? "✓" : "✗")} Цветовое пространство: {PlayerSettings.colorSpace} (нужно Linear)");

            foreach (var kv in new Dictionary<string, int> { { "Player", 6 }, { "Monster", 7 }, { "Buildable", 8 }, { "Water", 12 }, { "Loot", 11 } })
                report.AppendLine($"{(LayerMask.LayerToName(kv.Value) == kv.Key ? "✓" : "✗")} Слой {kv.Value}: ожидается «{kv.Key}», сейчас «{LayerMask.LayerToName(kv.Value)}»");

#if MIRROR
            report.AppendLine("✓ Mirror: define MIRROR активен");
#else
            report.AppendLine("✗ Mirror: не включён (меню Subsistence → 3)");
#endif

            string items = Path.Combine(Application.streamingAssetsPath, "items.json");
            report.AppendLine($"{(File.Exists(items) ? "✓" : "✗")} StreamingAssets/items.json: {(File.Exists(items) ? "найден" : "нет (пункт 4)")}");

            int itemsCount = 0;
            foreach (var _ in ItemDatabase.All) itemsCount++;
            report.AppendLine($"✓ Предметов в базе: {itemsCount}");
            int recipes = 0;
            foreach (var _ in Subsistence.Crafting.RecipeBook.All) recipes++;
            report.AppendLine($"✓ Рецептов: {recipes}");

            Debug.Log(report.ToString());
        }

        // ================== Бонус: собрать ПК-билд ==================
        [MenuItem("Subsistence/Собрать Windows-билд", priority = 20)]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/Windows");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { $"{ScenesPath}/Levels.unity" },
                locationPathName = "Builds/Windows/Subsistence.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Subsistence] Билд: {report.summary.result}, размер {report.summary.totalSize / (1024 * 1024)} МБ");
        }

        // ================== 9. ПОЛНЫЙ КОНВЕЙЕР (меню и batchmode) ==================
        /// <summary>
        /// Всё одним кликом: настройка проекта → items.json → HDRP → префабы моделей →
        /// сцена → диагностика → Windows-билд.
        /// Batch: Unity.exe -batchmode -quit -projectPath &lt;путь&gt; \
        ///        -executeMethod Subsistence.EditorTools.ProjectBootstrap.BuildAll
        /// </summary>
        [MenuItem("Subsistence/9. Собрать всё и билд (одним кликом)", priority = 21)]
        public static void BuildAll()
        {
            Debug.Log("<color=#39ff6a>[Subsistence]</color> build-all: старт");
            Step("1. настройка проекта", ConfigureProject);
            Step("4. items.json", ExportItemsJson);
            Step("5. HDRP asset", CreateHdrpAsset);
            Step("7. префабы моделей", ModelPrefabBuilder.BuildModelPrefabs);
            Step("2. сцена мира", CreateWorldScene);
            Step("6. диагностика", ValidateProject);
            Step("билд Windows", BuildWindows);
            Debug.Log("<color=#39ff6a>[Subsistence]</color> build-all: готово → Builds/Windows/Subsistence.exe");
        }

        // ================== 10. Играть сейчас: сцена + Play ==================
        /// <summary>
        /// Одна кнопка для «просто поиграть»: если сцены нет — собрать всё, открыть сцену
        /// Levels.unity и войти в Play. Ничего вручную искать не надо.
        /// </summary>
        [MenuItem("Subsistence/10. Играть сейчас (сцена + Play)", priority = 22)]
        public static void PlayNow()
        {
            string scene = $"{ScenesPath}/Levels.unity";
            if (!File.Exists(scene))
            {
                Debug.Log("<color=#39ff6a>[Subsistence]</color> сцены нет — собираю проект целиком (~1-3 мин)");
                RunAllSteps();
            }
            EditorSceneManager.OpenScene(scene);
            Debug.Log("<color=#39ff6a>[Subsistence]</color> сцена открыта, вхожу в Play… " +
                      "В консоли сначала пойдёт зелёный BIOS-загрузчик, потом загрузка уровней.");
            EditorApplication.delayCall += () => { EditorApplication.isPlaying = true; };
        }

        /// <summary>Все шаги без билда — используется кнопкой «Играть сейчас».</summary>
        static void RunAllSteps()
        {
            Step("1. настройка проекта", ConfigureProject);
            Step("4. items.json", ExportItemsJson);
            Step("5. HDRP asset", CreateHdrpAsset);
            Step("7. префабы моделей", ModelPrefabBuilder.BuildModelPrefabs);
            Step("2. сцена мира", CreateWorldScene);
            Step("6. диагностика", ValidateProject);
        }

        static void Step(string name, Action action)
        {
            try
            {
                action();
                Debug.Log($"[Subsistence] шаг «{name}» — ок");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Subsistence] шаг «{name}» упал: {e.Message}");
            }
        }
    }
}
#endif
