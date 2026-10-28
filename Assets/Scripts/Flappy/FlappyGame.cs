using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Flappy
{
    /// NEON FLUX - the whole game in one component.
    ///
    /// It adopts whatever the scene already provides (camera, ship, rails, stars)
    /// and builds anything that is missing, so it runs identically whether you
    /// open the baked NeonFlux scene or just hit Play on an empty one.
    public class FlappyGame : MonoBehaviour
    {
        public enum GState { Ready, Playing, Dead }

        public static FlappyGame Instance { get; private set; }

        public GState State { get; private set; } = GState.Ready;
        public float WorldSpeed { get; private set; }

        // ---------------- tuning ----------------
        const float BaseSpeed     = 4.6f;
        const float MaxSpeed      = 9.6f;
        const float SpeedPerPoint = 0.16f;
        const float GapBase       = 3.35f;
        const float GapMin        = 2.15f;
        const float GapPerPoint   = 0.035f;
        const float PillarWidth   = 1.25f;
        const float Spacing       = 3.5f;
        const float RailSize      = 0.42f;
        const int   BobFromScore  = 12;
        const int   StarCount     = 110;
        const string BestKey      = "NeonFlux.Best";
        const string CoinKey      = "NeonFlux.Coins";

        // ---------------- framing ----------------
        public float HalfH { get { return cam != null ? cam.orthographicSize : 5f; } }
        public float HalfW { get { return HalfH * (cam != null ? Mathf.Max(0.35f, cam.aspect) : 1.6f); } }
        /// Vertical space the ship may actually use, i.e. inside the rails.
        public float PlayHalfH { get { return HalfH - RailSize; } }
        public float DespawnX { get { return -HalfW - 4f; } }
        float SpawnX { get { return HalfW + PillarWidth; } }
        Vector3 SpawnPoint { get { return new Vector3(-HalfW * 0.50f, 0f, 0f); } }

        // ---------------- scene refs ----------------
        Camera cam;
        FlappyBird bird;
        Transform obstacleRoot;
        Transform railTop, railBottom;
        Material pillarMat, coinMat, shipMat, railMat, starMat;

        readonly List<Transform> stars = new List<Transform>();
        readonly List<float> starDepth = new List<float>();

        // ---------------- run state ----------------
        int score, coins, best, bankedCoins;
        float distanceSinceSpawn, deadTimer, readyTimer;
        float lastAspect;
        Color paletteNow = Neon.Magenta;

        Vector3 camBase;
        float shakeTime, shakeDur, shakeMag;
        float flashTime;

        AudioSource audioSrc;
        AudioClip clipFlap, clipPoint, clipCoin, clipHit;

        Texture2D px;
        GUIStyle bigStyle, midStyle, smallStyle, leftStyle;

        // =========================================================
        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            if (Application.isMobilePlatform)
            {
                Screen.orientation = ScreenOrientation.Portrait;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            SetupCamera();
            ApplyFraming();

            pillarMat = Neon.Make("NeonPillar", Neon.Magenta);
            coinMat   = Neon.Make("NeonCoin",   Neon.Gold);
            shipMat   = Neon.Make("NeonShip",   Neon.Cyan);
            railMat   = Neon.Make("NeonRail",   Neon.Magenta, Neon.Glow * 0.8f);
            starMat   = Neon.Make("NeonStar",   Neon.Star,    1.1f);

            SetupBloom();
            SetupRails();
            SetupStars();
            SetupShip();
            SetupObstacleRoot();
            SetupAudio();

            best = PlayerPrefs.GetInt(BestKey, 0);
            bankedCoins = PlayerPrefs.GetInt(CoinKey, 0);

            EnterReady();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------- setup ----------------
        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Neon.Background;
            camBase = cam.transform.position;

            if (FindFirstObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
        }

        /// Keeps the playfield usable on both a 16:9 Game view and a 9:19 phone:
        /// on narrow screens we zoom out so the world never gets too cramped.
        void ApplyFraming()
        {
            if (cam == null) return;
            float aspect = Mathf.Max(0.35f, cam.aspect);
            float size = 5f;
            if (size * aspect < 3.4f) size = 3.4f / aspect;
            cam.orthographicSize = size;
            lastAspect = cam.aspect;
            LayoutRails();
        }

        void SetupBloom()
        {
            // Bloom is what sells "neon": unlit + HDR colour + bloom = glow.
            try
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;

                if (FindFirstObjectByType<Volume>() == null)
                {
                    var go = new GameObject("NeonFlux Post");
                    go.transform.SetParent(transform, false);
                    var vol = go.AddComponent<Volume>();
                    vol.isGlobal = true;
                    vol.priority = 100f;

                    var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    profile.name = "NeonFluxProfile";
                    var bloom = profile.Add<Bloom>(true);
                    bloom.active = true;
                    bloom.threshold.Override(0.85f);
                    bloom.intensity.Override(1.35f);
                    bloom.scatter.Override(0.72f);
                    vol.profile = profile;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Neon Flux] Bloom unavailable, running without glow: " + e.Message);
            }
        }

        void SetupRails()
        {
            var existing = GameObject.Find("Rails");
            Transform root = existing != null ? existing.transform : new GameObject("Rails").transform;

            railTop = root.Find("RailTop");
            railBottom = root.Find("RailBottom");
            if (railTop == null) railTop = Neon.Cube("RailTop", root, railMat).transform;
            if (railBottom == null) railBottom = Neon.Cube("RailBottom", root, railMat).transform;

            // sharedMaterial, not material: railMat is a runtime instance we recolour
            // every frame, and assigning .material would silently clone it.
            railTop.GetComponent<MeshRenderer>().sharedMaterial = railMat;
            railBottom.GetComponent<MeshRenderer>().sharedMaterial = railMat;
            LayoutRails();
        }

        void LayoutRails()
        {
            if (railTop == null || railBottom == null) return;
            float w = HalfW * 2f + 2f;
            float y = HalfH - RailSize * 0.5f;
            railTop.position = new Vector3(0f, y, 1f);
            railBottom.position = new Vector3(0f, -y, 1f);
            railTop.localScale = new Vector3(w, RailSize, 0.4f);
            railBottom.localScale = new Vector3(w, RailSize, 0.4f);
        }

        void SetupStars()
        {
            var existing = GameObject.Find("Stars");
            Transform root = existing != null ? existing.transform : new GameObject("Stars").transform;

            NeonBuild.Fill(root, starMat, StarCount, HalfW, HalfH);

            foreach (Transform t in root)
            {
                stars.Add(t);
                starDepth.Add(UnityEngine.Random.Range(0.10f, 0.62f));
                var r = t.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = starMat;
            }
        }

        void SetupShip()
        {
            bird = FindFirstObjectByType<FlappyBird>();
            if (bird == null) bird = BuildShip();
            bird.ResetTo(SpawnPoint);
        }

        FlappyBird BuildShip()
        {
            return NeonBuild.Ship(
                SpawnPoint,
                shipMat,
                Neon.Make("NeonCore", Color.white, 3.2f),
                Neon.Make("NeonTrail", Neon.Cyan, Neon.Glow * 0.9f)
            ).GetComponent<FlappyBird>();
        }

        void SetupObstacleRoot()
        {
            var existing = GameObject.Find("Obstacles");
            obstacleRoot = existing != null ? existing.transform : new GameObject("Obstacles").transform;
        }

        void SetupAudio()
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            clipFlap  = Tone("flap",  480f,  760f, 0.09f, 0.30f, false);
            clipPoint = Tone("point", 680f, 1020f, 0.10f, 0.28f, false);
            clipCoin  = Tone("coin",  950f, 1650f, 0.14f, 0.26f, false);
            clipHit   = Tone("hit",   220f,   60f, 0.32f, 0.40f, true);
        }

        /// A tiny swept-frequency blip rendered straight into a sample buffer, so
        /// the project needs no audio files at all.
        AudioClip Tone(string name, float startHz, float endHz, float dur, float vol, bool square)
        {
            try
            {
                const int rate = 44100;
                int n = Mathf.Max(16, (int)(rate * dur));
                var clip = AudioClip.Create(name, n, 1, rate, false);
                var data = new float[n];
                double phase = 0.0;
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / n;
                    phase += 2.0 * Math.PI * Mathf.Lerp(startHz, endHz, t) / rate;
                    float s = (float)Math.Sin(phase);
                    if (square) s = s >= 0f ? 1f : -1f;
                    data[i] = s * Mathf.Exp(-4f * t) * Mathf.Clamp01(t / 0.008f) * vol;
                }
                clip.SetData(data, 0);
                return clip;
            }
            catch { return null; }
        }

        void PlayClip(AudioClip c)
        {
            if (c != null && audioSrc != null) audioSrc.PlayOneShot(c);
        }

        // ---------------- loop ----------------
        void Update()
        {
            float dt = Time.deltaTime;
            if (cam != null && !Mathf.Approximately(cam.aspect, lastAspect)) ApplyFraming();

            bool tap = GetTap();

            switch (State)
            {
                case GState.Ready:
                    readyTimer += dt;
                    if (tap) StartGame();
                    break;

                case GState.Playing:
                    if (tap) { bird.Flap(); PlayClip(clipFlap); }

                    WorldSpeed = Mathf.Min(MaxSpeed, BaseSpeed + score * SpeedPerPoint);
                    distanceSinceSpawn += WorldSpeed * dt;
                    if (distanceSinceSpawn >= Spacing)
                    {
                        distanceSinceSpawn -= Spacing;
                        SpawnObstacle(SpawnX);
                    }
                    break;

                case GState.Dead:
                    deadTimer += dt;
                    WorldSpeed = Mathf.Max(0f, WorldSpeed - 9f * dt);   // world coasts to a stop
                    if (tap && deadTimer > 0.55f) Restart();
                    break;
            }

            UpdatePalette(dt);
            UpdateStars(dt);
            UpdateShake(dt);
            if (flashTime > 0f) flashTime -= dt;
        }

        bool GetTap()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)) return true;
            var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) return true;
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) return true;
            return false;
        }

        void UpdatePalette(float dt)
        {
            Color target = State == GState.Ready ? Neon.Magenta : Neon.AtScore(score);
            paletteNow = Color.Lerp(paletteNow, target, 2.5f * dt);
            Neon.Tint(pillarMat, paletteNow);
            Neon.Tint(railMat, paletteNow, Neon.Glow * 0.8f);
        }

        void UpdateStars(float dt)
        {
            const float drift = 1.3f;
            float w = HalfW, h = HalfH;
            for (int i = 0; i < stars.Count; i++)
            {
                var t = stars[i];
                if (t == null) continue;
                Vector3 p = t.position;
                p.x -= (WorldSpeed + drift) * starDepth[i] * dt;
                if (p.x < -w - 0.3f)
                {
                    p.x = w + 0.3f;
                    p.y = UnityEngine.Random.Range(-h, h);
                }
                t.position = p;
            }
        }

        void UpdateShake(float dt)
        {
            if (cam == null) return;
            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                float k = shakeDur > 0f ? Mathf.Clamp01(shakeTime / shakeDur) : 0f;
                Vector2 o = UnityEngine.Random.insideUnitCircle * (shakeMag * k * k);
                cam.transform.position = camBase + new Vector3(o.x, o.y, 0f);
            }
            else if (cam.transform.position != camBase)
            {
                cam.transform.position = camBase;
            }
        }

        void Shake(float dur, float mag)
        {
            shakeDur = dur;
            shakeTime = dur;
            shakeMag = mag;
        }

        // ---------------- state ----------------
        void EnterReady()
        {
            State = GState.Ready;
            WorldSpeed = 0f;
            readyTimer = 0f;
            paletteNow = Neon.Magenta;
            if (bird != null) bird.ResetTo(SpawnPoint);
        }

        public void StartGame()
        {
            State = GState.Playing;
            score = 0;
            coins = 0;
            distanceSinceSpawn = Spacing * 0.35f;
            WorldSpeed = BaseSpeed;
            bird.BeginPlay();
            PlayClip(clipFlap);
        }

        public void Restart()
        {
            foreach (var s in FindObjectsByType<ObstacleScroller>(FindObjectsSortMode.None)) Destroy(s.gameObject);
            foreach (var f in FindObjectsByType<Fragment>(FindObjectsSortMode.None)) Destroy(f.gameObject);
            deadTimer = 0f;
            EnterReady();
        }

        public void AddScore()
        {
            score++;
            PlayClip(clipPoint);
            flashTime = 0.12f;
        }

        public void CollectCoin(Vector3 pos)
        {
            coins++;
            score += 2;
            PlayClip(clipCoin);
            Burst(pos, coinMat, 10, 4.5f, 0.14f, 0.5f);
        }

        public void OnPlayerDied()
        {
            if (State != GState.Playing) return;
            State = GState.Dead;
            deadTimer = 0f;
            PlayClip(clipHit);
            Shake(0.45f, 0.5f);
            Burst(bird.transform.position, shipMat, 26, 6.5f, 0.18f, 0.7f);
            bird.StopTrail();

            if (score > best)
            {
                best = score;
                PlayerPrefs.SetInt(BestKey, best);
            }
            bankedCoins += coins;
            PlayerPrefs.SetInt(CoinKey, bankedCoins);
            PlayerPrefs.Save();
        }

        // ---------------- spawning ----------------
        public void SpawnObstacle(float x)
        {
            float gap = Mathf.Max(GapMin, GapBase - score * GapPerPoint);
            float bob = score >= BobFromScore
                ? Mathf.Min(0.85f, (score - BobFromScore) * 0.05f)
                : 0f;

            // Keep every gap reachable: leave room for the rails and the bob travel.
            float edge = Mathf.Max(0f, PlayHalfH - gap * 0.5f - 0.45f - bob);
            float gapCenter = UnityEngine.Random.Range(-edge, edge);

            var root = new GameObject("Obstacle");
            root.transform.SetParent(obstacleRoot);
            root.transform.position = new Vector3(x, 0f, 0f);

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;

            var scroller = root.AddComponent<ObstacleScroller>();
            scroller.bobAmplitude = bob;
            scroller.bobSpeed = UnityEngine.Random.Range(1.1f, 1.9f);

            float far = HalfH + 4f;
            float topEdge = gapCenter + gap * 0.5f;
            float botEdge = gapCenter - gap * 0.5f;
            MakePillar(root.transform, (topEdge + far) * 0.5f, far - topEdge);
            MakePillar(root.transform, (botEdge - far) * 0.5f, far + botEdge);

            var gate = new GameObject("ScoreGate");
            gate.transform.SetParent(root.transform);
            gate.transform.localPosition = new Vector3(0f, gapCenter, 0f);
            var gc = gate.AddComponent<BoxCollider>();
            gc.isTrigger = true;
            gc.size = new Vector3(0.6f, gap, 3f);
            gate.AddComponent<ScoreGate>();

            if (UnityEngine.Random.value < 0.5f)
            {
                var coin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                coin.name = "Coin";
                coin.transform.SetParent(root.transform);
                coin.transform.localPosition =
                    new Vector3(0f, gapCenter + UnityEngine.Random.Range(-gap * 0.22f, gap * 0.22f), 0f);
                coin.transform.localScale = Vector3.one * 0.42f;
                coin.GetComponent<MeshRenderer>().sharedMaterial = coinMat;
                coin.GetComponent<BoxCollider>().isTrigger = true;
                coin.AddComponent<Coin>();
            }
        }

        void MakePillar(Transform parent, float centerY, float height)
        {
            if (height <= 0.05f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Pillar";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, centerY, 0f);
            go.transform.localScale = new Vector3(PillarWidth, height, 0.8f);
            go.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;
            go.GetComponent<BoxCollider>().isTrigger = true;
            go.AddComponent<Hazard>();
        }

        void Burst(Vector3 pos, Material mat, int count, float speed, float size, float life)
        {
            for (int i = 0; i < count; i++)
            {
                var go = Neon.Cube("frag", null, mat);
                var f = go.AddComponent<Fragment>();
                Vector3 v = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(speed * 0.3f, speed);
                v.z = 0f;
                f.Init(pos, v,
                    UnityEngine.Random.Range(size * 0.6f, size),
                    UnityEngine.Random.Range(life * 0.6f, life));
            }
        }

        // ---------------- HUD ----------------
        void EnsureStyles()
        {
            if (px == null)
            {
                px = new Texture2D(1, 1);
                px.SetPixel(0, 0, Color.white);
                px.Apply();
            }
            if (bigStyle != null) return;

            int h = Screen.height;
            bigStyle = new GUIStyle(GUI.skin.label);
            bigStyle.alignment = TextAnchor.MiddleCenter;
            bigStyle.fontStyle = FontStyle.Bold;
            bigStyle.fontSize = Mathf.RoundToInt(h * 0.085f);

            midStyle = new GUIStyle(GUI.skin.label);
            midStyle.alignment = TextAnchor.MiddleCenter;
            midStyle.fontStyle = FontStyle.Bold;
            midStyle.fontSize = Mathf.RoundToInt(h * 0.040f);

            smallStyle = new GUIStyle(GUI.skin.label);
            smallStyle.alignment = TextAnchor.MiddleCenter;
            smallStyle.fontSize = Mathf.RoundToInt(h * 0.026f);

            leftStyle = new GUIStyle(GUI.skin.label);
            leftStyle.alignment = TextAnchor.MiddleLeft;
            leftStyle.fontStyle = FontStyle.Bold;
            leftStyle.fontSize = Mathf.RoundToInt(h * 0.032f);
        }

        void Box(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, px);
            GUI.color = old;
        }

        void Text(Rect r, string s, GUIStyle st, Color c)
        {
            st.normal.textColor = new Color(0f, 0f, 0f, 0.65f);
            GUI.Label(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), s, st);
            st.normal.textColor = c;
            GUI.Label(r, s, st);
        }

        void OnGUI()
        {
            EnsureStyles();

            // GUI space is top-left origin; Screen.safeArea is bottom-left.
            Rect safe = Screen.safeArea;
            float x = safe.x;
            float w = safe.width;
            float y = Screen.height - safe.y - safe.height;
            float h = safe.height;

            if (flashTime > 0f)
                Box(new Rect(0f, 0f, Screen.width, Screen.height), new Color(1f, 1f, 1f, flashTime * 0.5f));

            if (State == GState.Ready)
            {
                float pulse = 0.65f + Mathf.Sin(readyTimer * 3f) * 0.35f;
                Text(new Rect(x, y + h * 0.20f, w, h * 0.14f), "NEON FLUX", bigStyle, paletteNow);
                Text(new Rect(x, y + h * 0.33f, w, h * 0.06f), "an infinite one-tap flyer", smallStyle,
                     new Color(1f, 1f, 1f, 0.55f));
                Text(new Rect(x, y + h * 0.60f, w, h * 0.07f), "TAP  /  SPACE  /  CLICK", midStyle,
                     new Color(1f, 1f, 1f, pulse));
                Text(new Rect(x, y + h * 0.88f, w, h * 0.05f),
                     "BEST  " + best + "        COINS  " + bankedCoins, smallStyle,
                     new Color(1f, 1f, 1f, 0.6f));
            }
            else if (State == GState.Playing)
            {
                Text(new Rect(x, y + h * 0.06f, w, h * 0.13f), score.ToString(), bigStyle, Color.white);
                Box(new Rect(x + w * 0.04f, y + h * 0.035f, w * 0.30f, h * 0.05f), new Color(0f, 0f, 0f, 0.35f));
                Text(new Rect(x + w * 0.07f, y + h * 0.035f, w * 0.30f, h * 0.05f),
                     "COINS  " + coins, leftStyle, Neon.Gold);
            }
            else
            {
                Box(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
                Box(new Rect(x + w * 0.10f, y + h * 0.26f, w * 0.80f, h * 0.38f), new Color(0.05f, 0.01f, 0.12f, 0.92f));

                if (score >= best && score > 0)
                    Text(new Rect(x, y + h * 0.29f, w, h * 0.05f), "NEW BEST!", smallStyle, Neon.Gold);

                Text(new Rect(x, y + h * 0.36f, w, h * 0.10f), "GAME OVER", bigStyle, paletteNow);
                Text(new Rect(x, y + h * 0.47f, w, h * 0.06f), "SCORE   " + score, midStyle, Color.white);
                Text(new Rect(x, y + h * 0.53f, w, h * 0.06f), "BEST    " + best, midStyle,
                     new Color(1f, 1f, 1f, 0.75f));
                Text(new Rect(x, y + h * 0.59f, w, h * 0.05f), "COINS   " + coins, smallStyle, Neon.Gold);

                if (deadTimer > 0.55f)
                {
                    float pulse = 0.55f + Mathf.Sin(deadTimer * 5f) * 0.45f;
                    Text(new Rect(x, y + h * 0.72f, w, h * 0.06f), "TAP TO RETRY", midStyle,
                         new Color(paletteNow.r, paletteNow.g, paletteNow.b, pulse));
                }
            }
        }
    }
}
