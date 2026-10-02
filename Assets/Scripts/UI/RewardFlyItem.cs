using UnityEngine;
using UnityEngine.UI;

namespace PixelShoot.UI
{
    /// <summary>
    /// One flying reward icon spawned onto the <see cref="RewardFlyTargets"/> fly layer. Making this a
    /// prefab lets you style the flyer in the editor (glow, trail, drop shadow, a child particle) and
    /// give it per-item flourish here (e.g. spin). Its PATH is driven by <see cref="RewardFlyTargets"/>
    /// (which tweens this RectTransform); this component just carries the icon and reacts to launch/land.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class RewardFlyItem : MonoBehaviour
    {
        [Tooltip("The Image whose sprite becomes the reward icon (coin / booster / life / …).")]
        [SerializeField] private Image icon;
        [Tooltip("Optional spin while flying (deg/sec). 0 = no spin. Nice on coins.")]
        [SerializeField] private float spinDegreesPerSecond = 0f;

        /// <summary>Set the reward sprite (hides the icon if null).</summary>
        public void SetSprite(Sprite sprite)
        {
            if (icon == null) return;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        /// <summary>Called by RewardFlyTargets when this flyer reaches its target — hook land VFX here.</summary>
        public void OnLanded()
        {
            // Base flyer has nothing extra; override visuals on the prefab (particles/animator) instead.
        }

        private void Update()
        {
            if (spinDegreesPerSecond != 0f)
                transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
