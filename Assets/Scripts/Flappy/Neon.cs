using UnityEngine;

namespace Flappy
{
    /// Shared look-and-feel for Neon Flux.
    ///
    /// Everything in the game is drawn with an *unlit* material whose colour is
    /// pushed above 1.0 (HDR). URP's bloom only blooms pixels brighter than its
    /// threshold, so that overbright colour is what turns a plain cube into a
    /// glowing neon bar. No textures, no imported art.
    public static class Neon
    {
        /// How far above 1.0 we push colours so bloom catches them.
        public const float Glow = 2.4f;

        public static readonly Color Background = new Color(0.035f, 0.010f, 0.090f);
        public static readonly Color Cyan       = new Color(0.00f, 1.00f, 0.96f);
        public static readonly Color Magenta    = new Color(1.00f, 0.18f, 0.55f);
        public static readonly Color Gold       = new Color(1.00f, 0.82f, 0.25f);
        public static readonly Color Violet     = new Color(0.62f, 0.36f, 1.00f);
        public static readonly Color Lime       = new Color(0.45f, 1.00f, 0.40f);
        public static readonly Color Orange     = new Color(1.00f, 0.45f, 0.15f);
        public static readonly Color Star       = new Color(0.55f, 0.70f, 1.00f);

        /// Cycled as the score climbs, so the world keeps changing colour.
        public static readonly Color[] Palette = { Magenta, Violet, Cyan, Lime, Gold, Orange };

        const float PointsPerColour = 7f;

        /// Smoothly walks the palette as the player scores.
        public static Color AtScore(int score)
        {
            float t = Mathf.Max(0, score) / PointsPerColour;
            int i = Mathf.FloorToInt(t) % Palette.Length;
            int j = (i + 1) % Palette.Length;
            // Hold each colour for a while, then blend quickly into the next.
            float f = Mathf.Clamp01((t - Mathf.Floor(t) - 0.6f) / 0.4f);
            return Color.Lerp(Palette[i], Palette[j], f);
        }

        public static Shader UnlitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        public static Material Make(string name, Color c)
        {
            return Make(name, c, Glow);
        }

        public static Material Make(string name, Color c, float glow)
        {
            var m = new Material(UnlitShader());
            m.name = name;
            Tint(m, c, glow);
            return m;
        }

        /// Writes an HDR colour into whichever property the resolved shader uses.
        public static void Tint(Material m, Color c, float glow)
        {
            if (m == null) return;
            var hdr = new Color(c.r * glow, c.g * glow, c.b * glow, c.a);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdr);
            if (m.HasProperty("_Color"))     m.SetColor("_Color", hdr);
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", hdr);
        }

        public static void Tint(Material m, Color c) { Tint(m, c, Glow); }

        /// Primitive without the collider we do not want, parented and named.
        public static GameObject Cube(string name, Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// Imported mesh (no collider), parented and named.
        public static GameObject Model(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (mat != null) r.sharedMaterial = mat;
            return go;
        }
    }
}
