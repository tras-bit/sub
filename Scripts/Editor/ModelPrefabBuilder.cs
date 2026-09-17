// ============================================================================
//  SUBSISTENCE — Editor/ModelPrefabBuilder.cs
//  Меню: «Subsistence → 7. Модели: префабы и Resources»
//
//  Что делает:
//   1) проходит по всем FBX в Assets/Subsistence/Models (**),
//   2) настраивает импортер: без анимаций/камер/света, без коллайдеров,
//      mesh compression off, global scale 1, материалы импортируются,
//   3) собирает из каждой модели префаб и кладёт его в
//      Assets/Subsistence/Resources/Models/<Имя>.prefab — именно оттуда
//      World/ModelLibrary.cs достаёт визуалы в рантайме (Resources.Load).
//
//  После этого пункта код сам начинает использовать настоящие модели:
//  монстры, транспорт, торговец, вендинг, кабина лифта, лут-контейнеры и
//  блоки постройки (с тинтом по тиру). Пока пункт не нажат — работают
//  примитивные заглушки, игра не падает.
//
//  Пункт безопасно запускать повторно: префабы перезаписываются.
// ============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Subsistence.EditorTools
{
    public static class ModelPrefabBuilder
    {
        const string ModelsRoot = "Assets/Subsistence/Models";
        const string ResourcesModels = "Assets/Subsistence/Resources/Models";

        [MenuItem("Subsistence/7. Модели: префабы и Resources", priority = 7)]
        public static void BuildModelPrefabs()
        {
            if (!Directory.Exists(ModelsRoot))
            {
                Debug.LogError($"[Subsistence] Нет папки {ModelsRoot} — нечего собирать.");
                return;
            }
            if (!Directory.Exists(ResourcesModels))
            {
                Directory.CreateDirectory(ResourcesModels);
                AssetDatabase.Refresh();
            }

            var fbx = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) fbx.Add(path);
            }
            fbx.Sort(System.StringComparer.OrdinalIgnoreCase);

            int made = 0, skipped = 0, reimported = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < fbx.Count; i++)
                {
                    string path = fbx[i];
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (ConfigureImporter(path)) reimported++;

                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model == null) { skipped++; continue; }

                    // Инстанс модели → префаб. Так в префаб попадает вся иерархия FBX.
                    var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
                    if (instance == null) { skipped++; continue; }
                    instance.name = name;

                    string dest = $"{ResourcesModels}/{name}.prefab";
                    var saved = PrefabUtility.SaveAsPrefabAsset(instance, dest, out bool ok);
                    Object.DestroyImmediate(instance);

                    if (ok && saved != null) made++; else skipped++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"<color=#39ff6a>[Subsistence]</color> Модели: префабов собрано <b>{made}</b> " +
                      $"(переимпортировано {reimported}, пропущено {skipped}) → {ResourcesModels}\n" +
                      "Дальше ничего настраивать не нужно: World/ModelLibrary подхватит их сам.");
        }

        /// <summary>Настройки импорта: статичный меш, без анимаций/камер/света, без коллайдеров.</summary>
        static bool ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return false;

            bool dirty = false;
            if (importer.importAnimation) { importer.importAnimation = false; dirty = true; }
            if (importer.animationType != ModelImporterAnimationType.None) { importer.animationType = ModelImporterAnimationType.None; dirty = true; }
            if (importer.importCameras) { importer.importCameras = false; dirty = true; }
            if (importer.importLights) { importer.importLights = false; dirty = true; }
            if (importer.importBlendShapes) { importer.importBlendShapes = false; dirty = true; }
            if (!importer.importMaterials) { importer.importMaterials = true; dirty = true; }
            if (importer.addCollider) { importer.addCollider = false; dirty = true; }   // коллайдеры даёт геймплей
            if (importer.meshCompression != ModelImporterMeshCompression.Off) { importer.meshCompression = ModelImporterMeshCompression.Off; dirty = true; }
            if (Mathf.Abs(importer.globalScale - 1f) > 0.0001f) { importer.globalScale = 1f; dirty = true; }

            if (dirty) importer.SaveAndReimport();
            return dirty;
        }

        [MenuItem("Subsistence/8. Проверить модели (отчёт)", priority = 8)]
        public static void ReportModels()
        {
            var names = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsRoot }))
                names.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));

            var ready = new List<string>();
            for (int i = 0; i < names.Count; i++)
                if (File.Exists($"{ResourcesModels}/{names[i]}.prefab")) ready.Add(names[i]);

            Debug.Log($"<color=#39ff6a>[Subsistence]</color> Моделей в проекте: <b>{names.Count}</b>, " +
                      $"готовых префабов в Resources: <b>{ready.Count}</b>.\n" +
                      (ready.Count < names.Count ? "Нажми «Subsistence → 7. Модели: префабы и Resources»." : "Всё готово."));
        }
    }
}
#endif
