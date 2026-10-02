using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.UI;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// One-click builder for DUMMY <see cref="RewardClaimPopup"/> + <see cref="RewardRow"/> prefabs so
    /// the reward-claim flow is testable before art is in. It lays out a plain panel with a title, a
    /// vertical rows container, and a <b>Continue</b> button, wires the script fields, and saves both
    /// prefabs. Re-run any time to regenerate; then restyle the prefabs by hand.
    ///
    /// <para>Menu: <b>Tools ▸ PixelShoot ▸ Create Reward Claim Prefabs (dummy)</b>.</para>
    /// </summary>
    public static class RewardPopupPrefabCreator
    {
        private const string Dir = "Assets/_Game/Prefabs/UI Screen/Popups";
        private const string RowPath = Dir + "/RewardRow.prefab";
        private const string PopupPath = Dir + "/Popup_RewardClaim.prefab";
        private const string FlyItemPath = Dir + "/RewardFlyItem.prefab";

        [MenuItem("Tools/PixelShoot/Create Reward Claim Prefabs (dummy)")]
        public static void Create()
        {
            System.IO.Directory.CreateDirectory(Dir);

            var rowPrefab = BuildRowPrefab();
            BuildPopupPrefab(rowPrefab);
            BuildFlyItemPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RewardPopupPrefabCreator] Created dummy prefabs:\n  {RowPath}\n  {PopupPath}\n  {FlyItemPath}\n" +
                      "Add Popup_RewardClaim to PopupService.prefabs, drop RewardFlyItem on " +
                      "RewardFlyTargets.flyItemPrefab, then restyle all three by hand.");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
        }

        // ── RewardFlyItem: one styleable flying icon ─────────────────────────
        private static void BuildFlyItemPrefab()
        {
            var root = NewUI("RewardFlyItem", null, new Vector2(90, 90));
            var img = root.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
            img.preserveAspect = true;

            var item = root.AddComponent<RewardFlyItem>();
            var so = new SerializedObject(item);
            so.FindProperty("icon").objectReferenceValue = img;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, FlyItemPath);
            Object.DestroyImmediate(root);
        }

        // ── RewardRow: icon + amount label ───────────────────────────────────
        private static RewardRow BuildRowPrefab()
        {
            var root = NewUI("RewardRow", null, new Vector2(420, 110));
            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 16;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(20, 20, 10, 10);

            var iconGo = NewUI("Icon", root.transform, new Vector2(80, 80));
            var icon = iconGo.AddComponent<Image>();
            icon.color = Color.white;
            icon.preserveAspect = true;

            var amountGo = NewUI("Amount", root.transform, new Vector2(180, 80));
            var amount = amountGo.AddComponent<TextMeshProUGUI>();
            amount.text = "x1";
            amount.fontSize = 48;
            amount.alignment = TextAlignmentOptions.MidlineLeft;
            amount.color = Color.white;

            var row = root.AddComponent<RewardRow>();
            var so = new SerializedObject(row);
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("amountLabel").objectReferenceValue = amount;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, RowPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<RewardRow>();
        }

        // ── Popup_RewardClaim: panel + title + rows + Continue ───────────────
        private static void BuildPopupPrefab(RewardRow rowPrefab)
        {
            // Full-screen root with the popup script + a CanvasGroup (BasePopup fades this).
            var root = NewUI("Popup_RewardClaim", null, Vector2.zero);
            Stretch(root.GetComponent<RectTransform>());
            var canvasGroup = root.AddComponent<CanvasGroup>();

            // Dim background (also catches taps behind the panel).
            var dim = NewUI("Dim", root.transform, Vector2.zero);
            Stretch(dim.GetComponent<RectTransform>());
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.6f);

            // Centre panel.
            var panel = NewUI("Panel", root.transform, new Vector2(640, 820));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.16f, 0.18f, 0.26f, 1f);
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 24;
            vlg.padding = new RectOffset(40, 40, 48, 48);
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            // Title.
            var titleGo = NewUI("Title", panel.transform, new Vector2(560, 90));
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "REWARDS";
            title.fontSize = 64;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;

            // Rows container (RewardClaimPopup instantiates RewardRow copies under here).
            var rows = NewUI("RowsContainer", panel.transform, new Vector2(560, 480));
            var rowsVlg = rows.AddComponent<VerticalLayoutGroup>();
            rowsVlg.childAlignment = TextAnchor.UpperCenter;
            rowsVlg.spacing = 12;
            rowsVlg.childForceExpandWidth = false;
            rowsVlg.childForceExpandHeight = false;

            // Continue button.
            var btnGo = NewUI("ContinueButton", panel.transform, new Vector2(420, 120));
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.7f, 0.35f, 1f);
            var button = btnGo.AddComponent<Button>();
            button.targetGraphic = btnImg;
            var btnLabelGo = NewUI("Label", btnGo.transform, new Vector2(420, 120));
            Stretch(btnLabelGo.GetComponent<RectTransform>());
            var btnLabel = btnLabelGo.AddComponent<TextMeshProUGUI>();
            btnLabel.text = "CONTINUE";
            btnLabel.fontSize = 48;
            btnLabel.alignment = TextAlignmentOptions.Center;
            btnLabel.color = Color.white;

            // Wire the popup script (BasePopup + RewardClaimPopup fields).
            var popup = root.AddComponent<RewardClaimPopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
            so.FindProperty("rowsContainer").objectReferenceValue = rows.transform;
            so.FindProperty("continueButton").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PopupPath);
            Object.DestroyImmediate(root);
        }

        // ── helpers ──────────────────────────────────────────────────────────
        private static GameObject NewUI(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (size != Vector2.zero) rt.sizeDelta = size;
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
