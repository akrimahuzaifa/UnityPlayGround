using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Flappy.EditorTools
{
    /// Bakes a real, inspectable NeonFlux scene so the game is visible in the
    /// Scene view without pressing Play, and registers it as build scene 0.
    ///
    /// The scene is authored in an *additive* scene and closed again afterwards,
    /// so building it never disturbs whatever you currently have open.
    public static class NeonFluxSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/NeonFlux.unity";
        const string MatDir    = "Assets/Materials/NeonFlux";
        const int    StarCount = 110;
        const float  RailSize  = 0.42f;
        const float  HalfH     = 5f;
        const float  HalfW     = 8.9f;

        [MenuItem("Neon Flux/Rebuild Playable Scene", priority = 0)]
        public static void RebuildFromMenu()
        {
            if (Build()) Debug.Log("[Neon Flux] Scene rebuilt at " + ScenePath);
        }

        [MenuItem("Neon Flux/Open Playable Scene", priority = 1)]
        public static void OpenFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && !Build()) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// Runs once on a fresh project so no manual setup step is ever required.
        [InitializeOnLoadMethod]
        static void AutoBuildOnce()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
                if (!Build()) return;

                Debug.Log("[Neon Flux] Created " + ScenePath + " and set it as build scene 0.");

                if (!SceneManager.GetActiveScene().isDirty)
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                else
                    Debug.Log("[Neon Flux] Your open scene has unsaved changes, so it was left alone. " +
                              "Use Neon Flux > Open Playable Scene when you are ready.");
            };
        }

        static bool Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

            if (SceneManager.GetActiveScene().path == ScenePath)
            {
                Debug.LogWarning("[Neon Flux] NeonFlux.unity is the open scene. " +
                                 "Open a different scene first, then rebuild.");
                return false;
            }

            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets/Materials", "NeonFlux");

            Material shipMat  = Mat("Ship",      Neon.Cyan,    Neon.Glow);
            Material panelMat = Mat("ShipPanel", Neon.Cyan,    0.6f);
            Material coreMat  = Mat("Core",      Color.white,  3.2f);
            Material trailMat = Mat("Trail",     Neon.Cyan,    Neon.Glow * 0.9f);
            Material railMat  = Mat("Rail",      Neon.Magenta, Neon.Glow * 0.8f);
            Material starMat  = Mat("Star",      Neon.Star,    1.1f);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var roots = new List<GameObject>();

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = HalfH;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Neon.Background;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            roots.Add(camGo);

            var rails = NeonBuild.Rails(railMat);
            LayoutRails(rails.transform);
            roots.Add(rails);

            roots.Add(NeonBuild.Stars(starMat, StarCount, HalfW, HalfH));
            roots.Add(NeonBuild.Ship(new Vector3(-HalfW * 0.5f, 0f, 0f), shipMat, panelMat, coreMat, trailMat));
            roots.Add(new GameObject("Obstacles"));

            var game = new GameObject("NeonFlux");
            game.AddComponent<FlappyGame>();
            roots.Add(game);

            // Objects are born into the *active* scene, so move them across.
            foreach (var r in roots) SceneManager.MoveGameObjectToScene(r, scene);

            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            if (saved) RegisterBuildScene();
            return saved;
        }

        static void LayoutRails(Transform root)
        {
            float w = HalfW * 2f + 2f;
            float y = HalfH - RailSize * 0.5f;
            var scale = new Vector3(w, RailSize, 0.4f);

            var top = root.Find("RailTop");
            if (top != null) { top.position = new Vector3(0f, y, 1f); top.localScale = scale; }

            var bottom = root.Find("RailBottom");
            if (bottom != null) { bottom.position = new Vector3(0f, -y, 1f); bottom.localScale = scale; }
        }

        static void RegisterBuildScene()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == ScenePath);
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        static Material Mat(string file, Color c, float glow)
        {
            string path = MatDir + "/Neon" + file + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Neon.UnlitShader());
                AssetDatabase.CreateAsset(m, path);
            }
            Neon.Tint(m, c, glow);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
