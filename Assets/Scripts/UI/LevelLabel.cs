using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Drops the current level number onto a Text / TMP_Text. Plug in whichever
    /// renderer you use — both fields are optional, the script writes to whatever's set.
    /// </summary>
    public class LevelLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text tmpText;
        [Tooltip("Format string. {0} = 1-based level number (DisplayLevel).")]
        [SerializeField] private string format = "Level {0}";

        private int lastShown = -1;

        private void OnEnable() => Refresh();

        public void Refresh()
        {
            int display = PlayerProgress.DisplayLevel;
            lastShown = display;
            string s = string.Format(format, display);
            if (tmpText != null) tmpText.text = s;
        }
    }
}
