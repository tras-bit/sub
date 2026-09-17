// ============================================================================
//  SUBSISTENCE — Editor/AutoSetup.cs
//  Автонастройка при ПЕРВОМ открытии проекта в Unity: нажимать ничего не надо.
//    слои/теги/физика → HDRP-ассет → префабы моделей → сцена Levels.unity →
//    items.json → диагностика. Работает один раз (маркер в ProjectSettings).
//  Повторить вручную: меню «Subsistence → 0. Автонастройка проекта».
// ============================================================================
#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Subsistence.EditorTools
{
    [InitializeOnLoad]
    public static class AutoSetup
    {
        const string MarkerPath = "ProjectSettings/Subsistence.autosetup";
        const string ScenePath = "Assets/Subsistence/Scenes/Levels.unity";

        static AutoSetup()
        {
            // В batchmode (сборка через BUILD_WINDOWS.bat) всё делает ProjectBootstrap.BuildAll.
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += TryAutoRun;
        }

        static void TryAutoRun()
        {
            try
            {
                if (File.Exists(MarkerPath)) return;                  // уже настроено
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    EditorApplication.delayCall += TryAutoRun;         // ждём импорт/компиляцию
                    return;
                }
                if (EditorApplication.timeSinceStartup < 6.0)
                {
                    EditorApplication.delayCall += TryAutoRun;         // даём проекту «устаканиться»
                    return;
                }

                Debug.Log("<color=#39ff6a>[Subsistence]</color> первая настройка проекта — " +
                          "идёт автоматически (~1-3 минуты, Unity может подтормаживать)...");
                RunAll();
                File.WriteAllText(MarkerPath, DateTime.Now.ToString("u"));
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Subsistence] автонастройка прервалась: " + e.Message +
                                 "\nЗапусти вручную: меню Subsistence → «0. Автонастройка проекта».");
            }
        }

        [MenuItem("Subsistence/0. Автонастройка проекта", priority = 0)]
        public static void RunAll()
        {
            Step("1. настройка проекта (слои/теги/физика/ввод)", ProjectBootstrap.ConfigureProject);
            Step("4. экспорт items.json", ProjectBootstrap.ExportItemsJson);
            Step("5. HDRP-ассет", ProjectBootstrap.CreateHdrpAsset);
            Step("5b. HDRP Global Settings", EnsureHdrpGlobalSettings);
            Step("7. префабы моделей (все FBX: оружие, вещи уровней, монстры)", ModelPrefabBuilder.BuildModelPrefabs);
            Step("2. сцена мира", ProjectBootstrap.CreateWorldScene);
            Step("6. диагностика", ProjectBootstrap.ValidateProject);

            // Сцена Levels.unity должна быть открыта — тогда сразу можно жать Play.
            try
            {
                var active = SceneManager.GetActiveScene();
                if (File.Exists(ScenePath) && active.path != ScenePath)
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Subsistence] сцену открой вручную: Assets/Subsistence/Scenes/Levels.unity (" + e.Message + ")");
            }

            Debug.Log("<color=#39ff6a>[Subsistence]</color> ГОТОВО. Сцена Levels.unity открыта — жми Play. " +
                      "Нужен exe: меню Subsistence → «Собрать Windows-билд» (или BUILD_WINDOWS.bat).");
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

        // HDRP в 2022.3 хочет ещё и HDRP Global Settings. Создаём через рефлексию,
        // чтобы скрипт компилировался даже если пакет HDRP ещё не подтянулся.
        static void EnsureHdrpGlobalSettings()
        {
            var t = Type.GetType("UnityEngine.Rendering.HighDefinition.HDRenderPipelineGlobalSettings, " +
                                 "Unity.RenderPipelines.HighDefinition.Runtime");
            if (t == null)
            {
                Debug.LogWarning("[Subsistence] HDRP не найден — проверь Packages/manifest.json (14.0.12).");
                return;
            }
            var ensure = t.GetMethod("Ensure", BindingFlags.Public | BindingFlags.Static);
            if (ensure == null)
            {
                Debug.Log("[Subsistence] HDRP Global Settings: создадутся сами. Если материалы розовые — " +
                          "Window → Rendering → HDRP Wizard → Fix All.");
                return;
            }
            ensure.Invoke(null, null);
            Debug.Log("[Subsistence] HDRP Global Settings — ок");
        }
    }
}
#endif
