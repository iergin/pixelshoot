using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelShoot.Game
{
    /// <summary>
    /// Owns the additive multi-scene flow. Lives in the persistent <b>InitializeScene</b> (which is
    /// loaded first and NEVER unloaded — it holds the always-on managers). This controller loads the
    /// <see cref="mainMenuScene"/> and <see cref="gameScene"/> ADDITIVELY on top of it, swapping one
    /// content scene for the other so the InitializeScene (and its managers) stay alive throughout.
    ///
    /// <para>Because content scenes load additively, newly spawned objects must go into the right
    /// scene — every load ends with <c>SetActiveScene</c> on the freshly-loaded content scene.</para>
    ///
    /// <para>Menu / gameplay / end screens call <see cref="LoadGame"/>, <see cref="LoadMainMenu"/>,
    /// or <see cref="ReloadGame"/> through <see cref="Instance"/> — they never touch SceneManager.</para>
    /// </summary>
    public class SceneFlow : MonoBehaviour
    {
        public static SceneFlow Instance { get; private set; }

        [Tooltip("Scene name (must be in Build Settings) for the main menu.")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [Tooltip("Scene name (must be in Build Settings) for gameplay.")]
        [SerializeField] private string gameScene = "Game";
        [Tooltip("Open the main menu automatically once InitializeScene is up.")]
        [SerializeField] private bool openMenuOnStart = true;

        private bool busy;

        public bool IsGameLoaded => SceneManager.GetSceneByName(gameScene).isLoaded;
        public bool IsMenuLoaded => SceneManager.GetSceneByName(mainMenuScene).isLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (openMenuOnStart && !IsMenuLoaded && !IsGameLoaded) LoadMainMenu();
        }

        /// <summary>Swap to the Game scene (unloads the menu if it's up).</summary>
        public void LoadGame() => Swap(gameScene, mainMenuScene);

        /// <summary>Swap to the Main Menu scene (unloads the game if it's up).</summary>
        public void LoadMainMenu() => Swap(mainMenuScene, gameScene);

        /// <summary>Reload the Game scene in place (retry / next level) — stays in gameplay.</summary>
        public void ReloadGame()
        {
            if (busy) return;
            StartCoroutine(ReloadRoutine());
        }

        private void Swap(string target, string other)
        {
            if (busy) return;
            StartCoroutine(SwapRoutine(target, other));
        }

        private IEnumerator SwapRoutine(string target, string other)
        {
            busy = true;

            var otherScene = SceneManager.GetSceneByName(other);
            if (otherScene.isLoaded) yield return SceneManager.UnloadSceneAsync(otherScene);

            if (!SceneManager.GetSceneByName(target).isLoaded)
                yield return SceneManager.LoadSceneAsync(target, LoadSceneMode.Additive);

            SceneManager.SetActiveScene(SceneManager.GetSceneByName(target)); // new objects spawn here
            busy = false;
        }

        private IEnumerator ReloadRoutine()
        {
            busy = true;

            var g = SceneManager.GetSceneByName(gameScene);
            if (g.isLoaded) yield return SceneManager.UnloadSceneAsync(g);
            yield return SceneManager.LoadSceneAsync(gameScene, LoadSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(gameScene));

            busy = false;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
