using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Data;
using PixelShoot.Game;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// One on-screen booster button (bottom bar). Shows the booster icon + owned count.
    /// Tapping routes to <see cref="BoosterManager.RequestBooster"/>: use it if owned,
    /// otherwise open the purchase popup.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BoosterButton : MonoBehaviour
    {
        [SerializeField] private BoosterData booster;
        [SerializeField] private BoosterManager manager;
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [Tooltip("Shows the owned count (e.g. 'x3'). {0} = count.")]
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private string countFormat = "x{0}";
        [Tooltip("Optional badge shown when the player owns none (prompt to buy). Hidden when owned.")]
        [SerializeField] private GameObject buyBadge;

        private RectTransform rt;

        private void Awake()
        {
            rt = (RectTransform)transform;
            if (button == null) button = GetComponent<Button>();
            if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(OnClick); }
            if (booster != null && iconImage != null && booster.Icon != null) iconImage.sprite = booster.Icon;
        }

        private void OnEnable()
        {
            PlayerBoosters.OnChanged += OnBoostersChanged;
            Refresh();
        }

        private void OnDisable() => PlayerBoosters.OnChanged -= OnBoostersChanged;

        private void OnBoostersChanged(string id, int _)
        {
            if (booster != null && id == booster.Id) Refresh();
        }

        private void OnClick()
        {
            if (manager != null && booster != null) manager.RequestBooster(booster, rt);
        }

        private void Refresh()
        {
            if (booster == null) return;
            int count = PlayerBoosters.Count(booster.Id);
            if (countLabel != null) countLabel.text = string.Format(countFormat, count);
            if (buyBadge != null) buyBadge.SetActive(count <= 0);
        }
    }
}
