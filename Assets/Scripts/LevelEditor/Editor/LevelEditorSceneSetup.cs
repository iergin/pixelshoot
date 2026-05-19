#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelShoot.LevelEditor.EditorTools
{
    public static class LevelEditorSceneSetup
    {
        private const string ScenePath = "Assets/_Game/Scenes/LevelEditor.unity";
        private const string CellPrefabPath = "Assets/_Game/Prefabs/LevelEditorCell.prefab";

        [MenuItem("PixelShoot/Open Level Editor Scene")]
        public static void OpenScene()
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            BuildScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("PixelShoot/Rebuild Level Editor Scene")]
        public static void RebuildScene()
        {
            if (!EditorUtility.DisplayDialog("Rebuild Level Editor",
                "This will overwrite the existing LevelEditor scene. Continue?", "Yes", "No"))
                return;
            BuildScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        private static void BuildScene()
        {
            EnsureDir("Assets/_Game");
            EnsureDir("Assets/_Game/Scenes");
            EnsureDir("Assets/_Game/Prefabs");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Editor Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
            camGo.transform.position = new Vector3(0, 16, -8);
            camGo.transform.rotation = Quaternion.Euler(60, 0, 0);

            // Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Cell prefab
            var cellPrefab = GetOrCreateCellPrefab();

            // Editor root
            var editorGo = new GameObject("LevelEditor");
            var controller = editorGo.AddComponent<LevelEditorController>();

            var gridRootGo = new GameObject("GridRoot");
            gridRootGo.transform.SetParent(editorGo.transform);
            gridRootGo.transform.localPosition = Vector3.zero;

            controller.gridRoot = gridRootGo.transform;
            controller.cellPrefab = cellPrefab;
            controller.gridSize = 30;
            controller.cellSize = 0.3f;
            controller.NewGrid();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeObject = editorGo;
            Debug.Log($"Level editor scene created at {ScenePath}.\n" +
                      "1) Create a LevelData asset (Create → PixelShoot → Level Data)\n" +
                      "2) Assign it to the LevelEditor's Target Asset field\n" +
                      "3) Paste the PALETTE block via the Palette Import section (creates ColorData + materials automatically)\n" +
                      "4) Click in the scene view to paint, optionally Auto-generate columns, then Save to asset.");
        }

        private static GameObject GetOrCreateCellPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
            if (existing != null) return existing;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "LevelEditorCell";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, CellPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            var name = Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
