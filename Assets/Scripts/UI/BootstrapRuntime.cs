using UnityEngine;

namespace AeroTerra.UI
{
    /// <summary>
    /// Auto-creates the persistent singletons on first scene load so the game
    /// runs even if scenes were made by hand without manager objects.
    /// </summary>
    public static class BootstrapRuntime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (Core.GameManager.Instance == null)
                new GameObject("GameManager").AddComponent<Core.GameManager>();
            if (AeroTerra.Input.InputManager.Instance == null)
                new GameObject("InputManager").AddComponent<AeroTerra.Input.InputManager>();
            if (Core.AudioManager.Instance == null)
                new GameObject("AudioManager").AddComponent<Core.AudioManager>();
        }
    }
}
