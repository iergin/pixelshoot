using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// One-click helpers that drop the GameObjects (managers AND scene controllers) with their
    /// scripts attached into the currently-open scene, for the additive two-scene setup. Open the
    /// target scene, run the matching menu item, then assign each component's inspector references
    /// and Save.
    ///
    /// <para>Every script is created on its own empty GameObject. It won't work until you wire its
    /// serialized references (buttons, panels, grid, etc.) in the inspector — the tool only saves
    /// you the add-component clicks and guarantees nothing is forgotten. Visual/world objects
    /// (grid, cameras, canvases, prefabs) you still place by hand.</para>
    /// </summary>
    public static class SceneSetupTool
    {
        // ── 1) InitializeScene — persistent managers, never unloaded ────────────────
        [MenuItem("PixelShoot/Scene Setup/1 — Add InitializeScene Scripts (to open scene)")]
        public static void SetupInit()
        {
            Ensure<PixelShoot.Game.SceneFlow>("[SceneFlow]");
            Ensure<PixelShoot.Game.AppBootstrap>("[AppBootstrap]");
            Ensure<PixelShoot.Ads.AdsManager>("[AdsManager]");
            Ensure<PixelShoot.Audio.AudioManager>("[AudioManager]");
            Ensure<PixelShoot.FacebookIntegration.FacebookInitializer>("[FacebookInitializer]");
            Ensure<PixelShoot.Ads.InterstitialController>("[InterstitialController]");
            Ensure<PixelShoot.Shop.NoAdsPromoController>("[NoAdsPromo]");
            Ensure<PixelShoot.UI.PopupService>("[PopupService]");
            Done("InitializeScene");
        }

        // ── 2) MainMenu — menu managers + menu UI controllers ───────────────────────
        [MenuItem("PixelShoot/Scene Setup/2 — Add MainMenu Scripts (to open scene)")]
        public static void SetupMenu()
        {
            // managers (PopupService itself lives in InitializeScene)
            Ensure<PixelShoot.Shop.ShopManager>("[ShopManager]");
            Ensure<PixelShoot.Shop.NoAdsPromoTrigger>("[NoAdsPromoTrigger (menu)]");
            // UI controllers (wire their refs after)
            Ensure<PixelShoot.UI.MainMenuController>("[MainMenuController]");
            Ensure<PixelShoot.UI.UiTransitionController>("[UiTransitionController]");
            Ensure<PixelShoot.UI.NavigationBar>("[NavigationBar]");
            // NOTE: Settings / Play / OutOfLives / Shop / NoAds panels are now BasePopup PREFABS
            // (SettingsPopup, PlayPopup, OutOfLivesPopup, ShopPopup, NoAdsPromoPopup, StarterPopup)
            // registered on PopupService in the InitializeScene — they are NOT scene objects.
            // ShopOfferButton rows live inside the Shop / promo popup prefabs.
            Done("MainMenu");
        }

        // ── 3) Game — gameplay managers + game controllers ──────────────────────────
        [MenuItem("PixelShoot/Scene Setup/3 — Add Game Scripts (to open scene)")]
        public static void SetupGame()
        {
            // managers (PopupService itself lives in InitializeScene)
            Ensure<PixelShoot.Game.KeyManager>("[KeyManager]");
            Ensure<PixelShoot.Shooters.LinkRopeController>("[LinkRopeController]");
            Ensure<PixelShoot.Shop.NoAdsPromoTrigger>("[NoAdsPromoTrigger (game)]");
            // core gameplay
            Ensure<PixelShoot.Game.LevelLoader>("[LevelLoader]");
            Ensure<PixelShoot.Game.GameController>("[GameController]");
            Ensure<PixelShoot.Grid.GridController>("[GridController]");
            Ensure<PixelShoot.Conveyor.ConveyorController>("[ConveyorController]");
            Ensure<PixelShoot.Conveyor.ReserveController>("[ReserveController]");
            Ensure<PixelShoot.Conveyor.PlayOnReserveController>("[PlayOnReserveController]");
            Ensure<PixelShoot.Conveyor.ReserveFullWarning>("[ReserveFullWarning]");
            Ensure<PixelShoot.Game.GridSheenController>("[GridSheenController]");
            Ensure<PixelShoot.Game.SheenGate>("[SheenGate]");
            Ensure<PixelShoot.Shooters.ClickInputRouter>("[ClickInputRouter]");
            // boosters
            Ensure<PixelShoot.Boosters.BoosterManager>("[BoosterManager]");
            Ensure<PixelShoot.Boosters.BoosterBarController>("[BoosterBarController]");
            Ensure<PixelShoot.Boosters.BoosterPurchaseController>("[BoosterPurchaseController]");
            Ensure<PixelShoot.Boosters.BoosterTutorialController>("[BoosterTutorialController]");
            Ensure<PixelShoot.Boosters.ClawController>("[ClawController]");
            Ensure<PixelShoot.Boosters.FillColorController>("[FillColorController]");
            Ensure<PixelShoot.Boosters.ShuffleController>("[ShuffleController]");
            // game UI / flow
            Ensure<PixelShoot.UI.LevelEndUIController>("[LevelEndUIController]");
            Ensure<PixelShoot.UI.QuitFlowController>("[QuitFlowController]");
            Ensure<PixelShoot.UI.StreakGiftController>("[StreakGiftController]");
            Ensure<PixelShoot.UI.ConveyorCapacityHUD>("[ConveyorCapacityHUD]");
            Ensure<PixelShoot.UI.GameplayInputBlocker>("[GameplayInputBlocker]");
            Ensure<PixelShoot.UI.SpecialItemTutorialController>("[SpecialItemTutorialController]");
            Ensure<PixelShoot.UI.SpotlightOverlay>("[SpotlightOverlay]");
            Ensure<PixelShoot.UI.LevelLabel>("[LevelLabel]");
            Done("Game");
        }

        // Add a fresh GameObject with component T — unless the open scene already has one.
        private static void Ensure<T>(string name) where T : Component
        {
#if UNITY_2023_1_OR_NEWER
            var existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
            var existing = Object.FindObjectOfType<T>(true);
#endif
            if (existing != null)
            {
                Debug.Log($"[SceneSetup] {typeof(T).Name} already in scene ('{existing.name}') — skipped.");
                return;
            }
            var go = new GameObject(name);
            go.AddComponent<T>();
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            Debug.Log($"[SceneSetup] Added {typeof(T).Name} on new '{name}'.", go);
        }

        private static void Done(string scene)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[SceneSetup] '{scene}' scripts ensured in the OPEN scene. " +
                      "Now assign each component's inspector references, then Save the scene.");
        }
    }
}
