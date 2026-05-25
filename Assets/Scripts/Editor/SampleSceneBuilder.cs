#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Bullets;
using PixelShoot.Conveyor;
using PixelShoot.Data;
using PixelShoot.Game;
using PixelShoot.Grid;
using PixelShoot.Shooters;
using PixelShoot.UI;

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

            // PlayOnReserve (unlimited row, used after Play On)
            var playOnGo = new GameObject("PlayOnReserve");
            playOnGo.transform.SetParent(root.transform);
            var playOn = playOnGo.AddComponent<PlayOnReserveController>();
            var playOnSlotsRoot = new GameObject("SlotsRoot");
            playOnSlotsRoot.transform.SetParent(playOnGo.transform);
            playOnSlotsRoot.transform.localPosition = new Vector3(-6f, 0f, -10f);
            // slotsRoot.right points in world +X by default — slots will extend rightward.
            SetField(playOn, "slotsRoot", playOnSlotsRoot.transform);
            SetField(playOn, "slotSpacing", 1.1f);

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
            SetField(gameController, "playOnReserve", playOn);

            var loader = gameGo.AddComponent<LevelLoader>();
            SetField(loader, "levelData", level);
            SetField(loader, "grid", gridController);
            SetField(loader, "conveyor", conveyor);
            SetField(loader, "reserve", reserve);
            SetField(loader, "playOnReserve", playOn);
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

            // Level end UI
            BuildLevelEndUI(root.transform, gameController, conveyor);

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("Sample scene built. Press Play to test.");
        }

        private static void BuildLevelEndUI(Transform rootParent, GameController game, ConveyorController conveyor)
        {
            // Canvas + EventSystem
            var canvasGo = new GameObject("LevelEndCanvas");
            canvasGo.transform.SetParent(rootParent);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
            canvasGo.AddComponent<GraphicRaycaster>();

            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Conveyor capacity HUD (top-center)
            var hudGo = new GameObject("ConveyorCapacityText", typeof(RectTransform));
            hudGo.transform.SetParent(canvasGo.transform, false);
            var hudRt = (RectTransform)hudGo.transform;
            hudRt.anchorMin = new Vector2(0.5f, 1f);
            hudRt.anchorMax = new Vector2(0.5f, 1f);
            hudRt.pivot = new Vector2(0.5f, 1f);
            hudRt.sizeDelta = new Vector2(420, 70);
            hudRt.anchoredPosition = new Vector2(0, -30);
            var hudText = hudGo.AddComponent<Text>();
            hudText.alignment = TextAnchor.MiddleCenter;
            hudText.fontSize = 32;
            hudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hudText.color = Color.white;
            hudText.text = "Conveyor: 0 / 0";
            var hud = hudGo.AddComponent<ConveyorCapacityHUD>();
            SetField(hud, "conveyor", conveyor);
            SetField(hud, "label", hudText);

            // Success panel
            var successPanel = BuildPanel(canvasGo.transform, "SuccessPanel", "LEVEL COMPLETE", new Color(0.1f, 0.3f, 0.15f, 0.92f));
            var nextBtn = BuildButton(successPanel.transform, "NextLevelButton", "Next Level", new Vector2(0, -60));
            successPanel.SetActive(false);

            // Fail panel
            var failPanel = BuildPanel(canvasGo.transform, "FailPanel", "LEVEL FAILED", new Color(0.35f, 0.1f, 0.1f, 0.92f));
            var restartBtn = BuildButton(failPanel.transform, "RestartButton", "Restart", new Vector2(-120, -60));
            var playOnBtn = BuildButton(failPanel.transform, "PlayOnButton", "Play On", new Vector2(120, -60));
            failPanel.SetActive(false);

            // Controller
            var ui = canvasGo.AddComponent<LevelEndUIController>();
            SetField(ui, "gameController", game);
            SetField(ui, "successPanel", successPanel);
            SetField(ui, "nextLevelButton", nextBtn);
            SetField(ui, "failPanel", failPanel);
            SetField(ui, "restartButton", restartBtn);
            SetField(ui, "playOnButton", playOnBtn);
        }

        private static GameObject BuildPanel(Transform parent, string name, string title, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(520, 280);
            rt.anchoredPosition = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = bg;

            // Title text
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(go.transform, false);
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(480, 80);
            titleRt.anchoredPosition = new Vector2(0, -20);
            var txt = titleGo.AddComponent<Text>();
            txt.text = title;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = 36;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.color = Color.white;
            return go;
        }

        private static Button BuildButton(Transform panel, string name, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(panel, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200, 64);
            rt.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.18f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var t = textGo.AddComponent<Text>();
            t.text = label;
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 22;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.color = Color.white;
            return btn;
        }

        private static Material GetOrCreateLockedMaterial()
        {
            const string dir = "Assets/_Game/Materials/Shared";
            const string path = dir + "/Box_Locked.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            EnsureDir(dir);
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
