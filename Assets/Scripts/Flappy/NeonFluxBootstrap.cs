using UnityEngine;

namespace Flappy
{
    /// Zero-setup entry point.
    ///
    /// Unity calls this automatically after any scene finishes loading - in the
    /// Editor when you press Play, and in a real build on startup. If the scene
    /// does not already contain a FlappyGame, we create one, and FlappyGame
    /// builds the rest of the world itself.
    ///
    /// The practical effect: the game runs from *any* scene without a single
    /// drag-and-drop. Set AutoBoot to false if you later add unrelated scenes
    /// that should not turn into Neon Flux.
    public static class NeonFluxBootstrap
    {
        public const bool AutoBoot = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!AutoBoot) return;
            if (Object.FindFirstObjectByType<FlappyGame>() != null) return;

            var go = new GameObject("NeonFlux (auto)");
            go.AddComponent<FlappyGame>();
            Debug.Log("[Neon Flux] Bootstrapped. Tap / Space / Click to fly.");
        }
    }
}
