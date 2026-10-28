using UnityEngine;

namespace Flappy
{
    /// Construction of the set pieces, shared by the runtime bootstrap and the
    /// Editor scene baker so the two can never drift apart.
    public static class NeonBuild
    {
        /// Ship meshes modelled in Blender (Art/Blender/NeonShip.blend): cyan hull,
        /// dark panel lines, and a white-hot canopy and engine.
        public const string ShipMeshes = "NeonFlux/NeonShip";

        /// A neon jet with a trail from its engine.
        /// Falls back to a cube dart if the ship meshes are missing.
        public static GameObject Ship(Vector3 pos, Material shipMat, Material panelMat, Material coreMat, Material trailMat)
        {
            var root = new GameObject("Ship");
            root.transform.position = pos;
            root.AddComponent<SphereCollider>().radius = 0.34f;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            Mesh hull = null, panel = null, core = null;
            foreach (var m in Resources.LoadAll<Mesh>(ShipMeshes))
            {
                if (m.name == "ShipHull") hull = m;
                else if (m.name == "ShipPanel") panel = m;
                else if (m.name == "ShipCore") core = m;
            }

            Transform trailHost = visual;
            if (hull != null)
            {
                Neon.Model("Hull", visual, hull, shipMat);
                if (panel != null) Neon.Model("Panel", visual, panel, panelMat);
                if (core != null) Neon.Model("Core", visual, core, coreMat);

                trailHost = new GameObject("Exhaust").transform;
                trailHost.SetParent(visual, false);
                trailHost.localPosition = new Vector3(-0.52f, -0.01f, 0f);
            }
            else
            {
                var body = Neon.Cube("Body", visual, shipMat);
                body.transform.localScale = new Vector3(0.86f, 0.34f, 0.34f);

                var nose = Neon.Cube("Nose", visual, shipMat);
                nose.transform.localPosition = new Vector3(0.42f, 0f, 0f);
                nose.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                nose.transform.localScale = Vector3.one * 0.28f;

                var coreCube = Neon.Cube("Core", visual, coreMat);
                coreCube.transform.localScale = new Vector3(0.30f, 0.20f, 0.36f);
            }

            var trail = trailHost.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.34f;
            trail.startWidth = 0.18f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.04f;
            trail.numCapVertices = 3;
            trail.sharedMaterial = trailMat;
            trail.emitting = false;

            // Adds the Rigidbody via RequireComponent.
            root.AddComponent<FlappyBird>();
            return root;
        }

        public static GameObject Rails(Material railMat)
        {
            var root = new GameObject("Rails");
            Neon.Cube("RailTop", root.transform, railMat);
            Neon.Cube("RailBottom", root.transform, railMat);
            return root;
        }

        public static GameObject Stars(Material starMat, int count, float halfW, float halfH)
        {
            var root = new GameObject("Stars");
            Fill(root.transform, starMat, count, halfW, halfH);
            return root;
        }

        /// Coin meshes modelled in Blender (Art/Blender/NeonCoin.blend): a glowing rim
        /// and lightning bolt, plus a dim face so the two read apart when drawn unlit.
        public const string CoinMeshes = "NeonFlux/NeonCoin";

        static Mesh coinRim, coinFace;
        static bool coinMeshesLoaded;

        /// A collectible coin with a trigger collider.
        /// Falls back to a cube if the coin meshes are missing.
        public static GameObject Coin(Transform parent, Material rimMat, Material faceMat)
        {
            if (!coinMeshesLoaded)
            {
                coinMeshesLoaded = true;
                foreach (var m in Resources.LoadAll<Mesh>(CoinMeshes))
                {
                    if (m.name == "CoinRim") coinRim = m;
                    else if (m.name == "CoinFace") coinFace = m;
                }
            }

            GameObject go;
            if (coinRim != null)
            {
                go = Neon.Model("Coin", parent, coinRim, rimMat);
                if (coinFace != null) Neon.Model("Face", go.transform, coinFace, faceMat);
                go.AddComponent<SphereCollider>().radius = 0.5f;
            }
            else
            {
                go = Neon.Cube("Coin", parent, rimMat);
                go.AddComponent<BoxCollider>();
            }
            go.GetComponent<Collider>().isTrigger = true;
            go.AddComponent<Coin>();
            return go;
        }

        /// Star meshes modelled in Blender (Art/Blender/NeonStars.blend). Loaded from
        /// Resources so the runtime bootstrap gets them in builds too.
        public const string StarMeshes = "NeonFlux/NeonStars";

        /// Tops the parallax field up to <paramref name="count"/> stars.
        /// Falls back to cubes if the star meshes are missing.
        public static void Fill(Transform root, Material starMat, int count, float halfW, float halfH)
        {
            var meshes = Resources.LoadAll<Mesh>(StarMeshes);
            for (int i = root.childCount; i < count; i++)
            {
                GameObject s;
                if (meshes.Length > 0)
                {
                    s = Neon.Model("Star", root, meshes[Random.Range(0, meshes.Length)], starMat);
                    s.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f));
                    // Spiky outlines cover less area than a cube, so they run larger.
                    s.transform.localScale = Vector3.one * Random.Range(0.12f, 0.28f);
                }
                else
                {
                    s = Neon.Cube("Star", root, starMat);
                    s.transform.localScale = Vector3.one * Random.Range(0.04f, 0.13f);
                }
                s.transform.position = new Vector3(
                    Random.Range(-halfW, halfW),
                    Random.Range(-halfH, halfH),
                    Random.Range(2f, 7f));
            }
        }
    }
}
