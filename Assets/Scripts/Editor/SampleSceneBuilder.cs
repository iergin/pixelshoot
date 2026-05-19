#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PixelShoot.Bullets;
using PixelShoot.Conveyor;
using PixelShoot.Data;
using PixelShoot.Game;
using PixelShoot.Grid;
using PixelShoot.Shooters;

namespace PixelShoot.EditorTools
{
    public static class SampleSceneBuilder
    {
        private const string PrefabDir = "Assets/_Game/Prefabs";

        [MenuItem("PixelShoot/Build Sample Scene (in current scene)")]
        public static void Build()
        {
            EnsureDir(PrefabDir);

            // 1. Make sure level data + colors exist
            var level = AssetDatabase.LoadAssetAtPath<LevelData>("Assets/_Game/Levels/Level_01.asset");
            if (level == null)
            {
                SampleLevelCreator.CreateSampleLevel();
                level = AssetDatabase.LoadAssetAtPath<LevelData>("Assets/_Game/Levels/Level_01.asset");
            }

            // 2. Create runtime prefabs
            var bulletPrefab = CreateBulletPrefab();
            var boxPrefab = CreateBoxPrefab();
            var shooterPrefab = CreateShooterPrefab(bulletPrefab);
            var columnPrefab = CreateShooterColumnPrefab();

            // 3. Build scene hierarchy
            var existing = GameObject.Find("LevelRoot");
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject("LevelRoot");

            // Grid
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root.transform);
            var gridController = gridGo.AddComponent<GridController>();
            var gridRoot = new GameObject("GridRoot");
            gridRoot.transform.SetParent(gridGo.transform);
            gridRoot.transform.localPosition = Vector3.zero;
            SetField(gridController, "boxPrefab", boxPrefab);
            SetField(gridController, "gridRoot", gridRoot.transform);
            SetField(gridController, "cellSize", 1f);
            SetField(gridController, "lockedBoxMaterial", GetOrCreateLockedMaterial());

            // Conveyor
            var conveyorGo = new GameObject("Conveyor");
            conveyorGo.transform.SetParent(root.transform);
            var conveyor = conveyorGo.AddComponent<ConveyorController>();
            var pathRoot = new GameObject("PathRoot");
            pathRoot.transform.SetParent(conveyorGo.transform);
            // Loop around the grid. Path is interpolated linearly between nodes.
            // Per side: shoot-start and shoot-end (both canShoot+same side).
            // Per corner: a single no-shoot pivot. Add more pivots manually to round corners.
            const float S = 5f; // path distance from origin along each side
            CreatePathNode(pathRoot.transform, "Bottom_Start", new Vector3(-3f, 0f, -S), true,  GridSide.Bottom);
            CreatePathNode(pathRoot.transform, "Bottom_End",   new Vector3( 3f, 0f, -S), true,  GridSide.Bottom);
            CreatePathNode(pathRoot.transform, "Corner_BR",    new Vector3( S,  0f, -S), false, GridSide.Right);

            CreatePathNode(pathRoot.transform, "Right_Start",  new Vector3( S, 0f, -3f), true,  GridSide.Right);
            CreatePathNode(pathRoot.transform, "Right_End",    new Vector3( S, 0f,  3f), true,  GridSide.Right);
            CreatePathNode(pathRoot.transform, "Corner_TR",    new Vector3( S, 0f,  S),  false, GridSide.Top);

            CreatePathNode(pathRoot.transform, "Top_Start",    new Vector3( 3f, 0f,  S), true,  GridSide.Top);
            CreatePathNode(pathRoot.transform, "Top_End",      new Vector3(-3f, 0f,  S), true,  GridSide.Top);
            CreatePathNode(pathRoot.transform, "Corner_TL",    new Vector3(-S,  0f,  S), false, GridSide.Left);

            CreatePathNode(pathRoot.transform, "Left_Start",   new Vector3(-S, 0f,  3f), true,  GridSide.Left);
            CreatePathNode(pathRoot.transform, "Left_End",     new Vector3(-S, 0f, -3f), true,  GridSide.Left);
            CreatePathNode(pathRoot.transform, "Corner_BL",    new Vector3(-S, 0f, -S),  false, GridSide.Bottom); // closes loop, no shoot
            SetField(conveyor, "pathRoot", pathRoot.transform);
            SetField(conveyor, "pathSpeed", 3.0f);
            SetField(conveyor, "safeSpacing", 1.2f);

            // Reserve
            var reserveGo = new GameObject("Reserve");
            reserveGo.transform.SetParent(root.transform);
            var reserve = reserveGo.AddComponent<ReserveController>();
            var slotTransforms = new Transform[5];
            for (int i = 0; i < 5; i++)
            {
                var slot = new GameObject($"ReserveSlot_{i}");
                slot.transform.SetParent(reserveGo.transform);
                slot.transform.localPosition = new Vector3(-8f, 0f, -8f + i * 1.0f);
                slotTransforms[i] = slot.transform;
            }
            SetField(reserve, "slotTransforms", slotTransforms);

            // Columns root
            var columnsRoot = new GameObject("ColumnsRoot");
            columnsRoot.transform.SetParent(root.transform);
            columnsRoot.transform.localPosition = new Vector3(0f, 0f, -7f);

            // Game + Loader
            var gameGo = new GameObject("Game");
            gameGo.transform.SetParent(root.transform);
            var gameController = gameGo.AddComponent<GameController>();
            SetField(gameController, "grid", gridController);
            SetField(gameController, "conveyor", conveyor);
            SetField(gameController, "reserve", reserve);

            var loader = gameGo.AddComponent<LevelLoader>();
            SetField(loader, "levelData", level);
            SetField(loader, "grid", gridController);
            SetField(loader, "conveyor", conveyor);
            SetField(loader, "reserve", reserve);
            SetField(loader, "gameController", gameController);
            SetField(loader, "shooterPrefab", shooterPrefab);
            SetField(loader, "columnPrefab", columnPrefab);
            SetField(loader, "columnsRoot", columnsRoot.transform);
            SetField(loader, "columnSpacing", 1.4f);

            gameGo.AddComponent<ClickInputRouter>();

            // Camera
            var camGo = GameObject.Find("Main Camera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }
            camGo.transform.position = new Vector3(0f, 16f, -12f);
            camGo.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            // Light
            if (GameObject.Find("Directional Light") == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // Ground (visual aid, just below the play plane so it doesn't z-fight)
            if (GameObject.Find("Ground") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.localScale = Vector3.one * 4f;
                ground.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("Sample scene built. Press Play to test.");
        }

        private static Material GetOrCreateLockedMaterial()
        {
            const string path = "Assets/_Game/Materials/Box_Locked.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            EnsureDir("Assets/_Game/Materials");
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var color = new Color(0.42f, 0.42f, 0.46f);
            var mat = new Material(shader) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject CreateBulletPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bullet";
            go.transform.localScale = Vector3.one * 0.25f;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var bullet = go.AddComponent<Bullet>();
            SetField(bullet, "meshRenderer", go.GetComponent<MeshRenderer>());
            var path = $"{PrefabDir}/Bullet.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static Box CreateBoxPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Box";
            go.transform.localScale = Vector3.one * 0.9f;
            var box = go.AddComponent<Box>();
            SetField(box, "meshRenderer", go.GetComponent<MeshRenderer>());

            // Color hint dot — small sphere centered on top of the cube. Visible only while Locked.
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "ColorDot";
            Object.DestroyImmediate(dot.GetComponent<Collider>());
            dot.transform.SetParent(go.transform);
            dot.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            dot.transform.localScale = Vector3.one * 0.28f;
            SetField(box, "colorDot", dot.GetComponent<MeshRenderer>());

            var path = $"{PrefabDir}/Box.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Box>();
        }

        private static Shooter CreateShooterPrefab(GameObject bulletPrefabRoot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Shooter";
            go.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            // collider stays for click ray
            var shooter = go.AddComponent<Shooter>();

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(go.transform);
            muzzle.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            SetField(shooter, "meshRenderer", go.GetComponent<MeshRenderer>());
            SetField(shooter, "muzzle", muzzle.transform);
            SetField(shooter, "bulletPrefab", bulletPrefabRoot.GetComponent<Bullet>());

            var path = $"{PrefabDir}/Shooter.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Shooter>();
        }

        private static ShooterColumn CreateShooterColumnPrefab()
        {
            var go = new GameObject("ShooterColumn");
            go.AddComponent<ShooterColumn>();
            var path = $"{PrefabDir}/ShooterColumn.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ShooterColumn>();
        }

        private static void CreatePathNode(Transform parent, string name, Vector3 pos, bool canShoot, GridSide side)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var node = go.AddComponent<ConveyorPathNode>();
            SetField(node, "isCanShoot", canShoot);
            SetField(node, "targetSide", side);
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            var name = Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var t = target.GetType();
            while (t != null)
            {
                var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) { f.SetValue(target, value); return; }
                t = t.BaseType;
            }
            Debug.LogError($"Field '{fieldName}' not found on {target.GetType().Name}");
        }
    }
}
#endif
