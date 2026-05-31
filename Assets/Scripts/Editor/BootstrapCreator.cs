#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PixelShoot.Ads;
using PixelShoot.Data;
using PixelShoot.Shop;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// One-click scene wire-up for the meta layer (Ads, Shop, Interstitial cadence,
    /// Coins config). Creates dummy SO assets under <c>Assets/_Game/Configs</c> and
    /// <c>Assets/_Game/Offers</c> if they don't already exist, drops a single
    /// <c>[PixelShoot Bootstrap]</c> GameObject into the active scene and wires
    /// everything together with sensible defaults.
    /// </summary>
    public static class BootstrapCreator
    {
        private const string ConfigsDir = "Assets/_Game/Configs";
        private const string OffersDir  = "Assets/_Game/Offers";

        [MenuItem("PixelShoot/Create Scene Bootstrap")]
        public static void CreateBootstrap()
        {
            EnsureDir(ConfigsDir);
            EnsureDir(OffersDir);

            // 1) Configs — re-use existing assets if any so the user's tuning isn't clobbered.
            var coinsConfig   = LoadOrCreate<CoinsConfig>($"{ConfigsDir}/CoinsConfig.asset", null);
            var interConfig   = LoadOrCreate<InterstitialConfig>($"{ConfigsDir}/InterstitialConfig.asset", null);
            var basicOffer    = LoadOrCreate<BasicOffer>($"{OffersDir}/BasicStarter.asset", cd =>
            {
                SetPrivateField(cd, "offerId",       "basic_starter");
                SetPrivateField(cd, "productId",     "com.pixelshoot.basic");
                SetPrivateField(cd, "displayName",   "Starter Pack");
                SetPrivateField(cd, "grantedCoins",  5000);
            });

            // 2) Bootstrap GameObject. If one already exists, reuse it.
            var existing = GameObject.Find("[PixelShoot Bootstrap]");
            var go = existing != null ? existing : new GameObject("[PixelShoot Bootstrap]");
            if (existing == null) Undo.RegisterCreatedObjectUndo(go, "Create PixelShoot Bootstrap");
            else                 Undo.RegisterCompleteObjectUndo(go, "Configure PixelShoot Bootstrap");

            var ads   = go.GetComponent<AdsManager>()             ?? go.AddComponent<AdsManager>();
            var shop  = go.GetComponent<ShopManager>()            ?? go.AddComponent<ShopManager>();
            var inter = go.GetComponent<InterstitialController>() ?? go.AddComponent<InterstitialController>();

            // 3) Wire serialized refs via SerializedObject so Undo + dirty-flagging work.
            var soShop = new SerializedObject(shop);
            var offersList = soShop.FindProperty("offers");
            if (offersList.arraySize == 0)
            {
                offersList.arraySize = 1;
                offersList.GetArrayElementAtIndex(0).objectReferenceValue = basicOffer;
            }
            soShop.ApplyModifiedProperties();

            var soInter = new SerializedObject(inter);
            var configProp = soInter.FindProperty("config");
            if (configProp != null && configProp.objectReferenceValue == null)
                configProp.objectReferenceValue = interConfig;
            soInter.ApplyModifiedProperties();

            // 4) Feed coinsConfig into LevelLoader and LevelEndUIController if present, so
            //    you don't have to drag them by hand for the dummy setup.
            TryAssignCoinsAndInterstitial(coinsConfig, inter);

            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
            EditorUtility.SetDirty(go);

            Debug.Log("[PixelShoot] Bootstrap ready. Created/reused:\n" +
                      $"  • {AssetDatabase.GetAssetPath(coinsConfig)}\n" +
                      $"  • {AssetDatabase.GetAssetPath(interConfig)}\n" +
                      $"  • {AssetDatabase.GetAssetPath(basicOffer)}\n" +
                      $"  • GameObject '{go.name}' in scene '{go.scene.name}'.");
        }

        // ── helpers ─────────────────────────────────────────────────────
        private static T LoadOrCreate<T>(string path, Action<T> init) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            if (init != null) init(asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void TryAssignCoinsAndInterstitial(CoinsConfig coins, InterstitialController interCtrl)
        {
#if UNITY_2023_1_OR_NEWER
            var loaders = UnityEngine.Object.FindObjectsByType<PixelShoot.Game.LevelLoader>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var uis     = UnityEngine.Object.FindObjectsByType<PixelShoot.UI.LevelEndUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var loaders = UnityEngine.Object.FindObjectsOfType<PixelShoot.Game.LevelLoader>();
            var uis     = UnityEngine.Object.FindObjectsOfType<PixelShoot.UI.LevelEndUIController>();
#endif
            foreach (var loader in loaders)
            {
                var so = new SerializedObject(loader);
                var p = so.FindProperty("coinsConfig");
                if (p != null && p.objectReferenceValue == null)
                {
                    p.objectReferenceValue = coins;
                    so.ApplyModifiedProperties();
                }
            }
            foreach (var ui in uis)
            {
                var so = new SerializedObject(ui);
                var coinsProp = so.FindProperty("coinsConfig");
                if (coinsProp != null && coinsProp.objectReferenceValue == null)
                    coinsProp.objectReferenceValue = coins;
                var interProp = so.FindProperty("interstitial");
                if (interProp != null && interProp.objectReferenceValue == null)
                    interProp.objectReferenceValue = interCtrl;
                so.ApplyModifiedProperties();
            }
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            var name = Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetPrivateField(object target, string field, object value)
        {
            var t = target.GetType();
            while (t != null)
            {
                var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) { f.SetValue(target, value); return; }
                t = t.BaseType;
            }
        }
    }
}
#endif
