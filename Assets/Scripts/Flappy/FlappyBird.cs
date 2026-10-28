using UnityEngine;

namespace Flappy
{
    /// The player ship.
    ///
    /// Deliberately *kinematic*: we integrate gravity by hand instead of letting
    /// the physics engine do it. Flappy-style games need an exact, repeatable arc,
    /// and a dynamic Rigidbody with interpolation enabled will overwrite whatever
    /// we write to transform.rotation every frame. The Rigidbody is kept only so
    /// that trigger events fire against the obstacles.
    [RequireComponent(typeof(Rigidbody))]
    public class FlappyBird : MonoBehaviour
    {
        public float gravity = 36f;
        public float flapForce = 11f;
        public float terminalVelocity = -19f;

        Rigidbody rb;
        Transform visual;
        TrailRenderer trail;

        float vy;
        float tilt;
        float readyY;

        public float VerticalSpeed { get { return vy; } }

        void Awake()
        {
            Init();
        }

        /// FlappyGame.Awake can call ResetTo before our own Awake has run (Awake
        /// order across objects is not guaranteed), so setup is safe to repeat.
        void Init()
        {
            if (rb != null) return;

            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;

            if (GetComponent<Collider>() == null)
                gameObject.AddComponent<SphereCollider>().radius = 0.34f;

            visual = transform.Find("Visual");
            if (visual == null) visual = transform;
            trail = GetComponentInChildren<TrailRenderer>();
        }

        /// Drop the ship back on its starting perch and idle.
        public void ResetTo(Vector3 spawn)
        {
            Init();
            transform.position = spawn;
            readyY = spawn.y;
            vy = 0f;
            tilt = 0f;
            visual.localRotation = Quaternion.identity;
            if (trail != null) { trail.Clear(); trail.emitting = false; }
        }

        public void BeginPlay()
        {
            vy = 0f;
            if (trail != null) { trail.Clear(); trail.emitting = true; }
            Flap();
        }

        public void Flap()
        {
            vy = flapForce;
        }

        public void StopTrail()
        {
            if (trail != null) trail.emitting = false;
        }

        void Update()
        {
            var g = FlappyGame.Instance;
            if (g == null) return;

            float dt = Time.deltaTime;
            Vector3 p = transform.position;

            switch (g.State)
            {
                case FlappyGame.GState.Ready:
                    // Gentle idle bob so the menu is not a static screen.
                    p.y = readyY + Mathf.Sin(Time.time * 3.2f) * 0.28f;
                    tilt = Mathf.Sin(Time.time * 3.2f) * 7f;
                    break;

                case FlappyGame.GState.Playing:
                {
                    vy = Mathf.Max(terminalVelocity, vy - gravity * dt);
                    p.y += vy * dt;

                    // Nose follows velocity: the classic flappy tell.
                    tilt = Mathf.LerpAngle(tilt, Mathf.Clamp(vy * 4.5f, -72f, 32f), 10f * dt);

                    float limit = g.PlayHalfH - 0.34f;
                    if (p.y > limit || p.y < -limit)
                    {
                        p.y = Mathf.Clamp(p.y, -limit, limit);
                        transform.position = p;
                        visual.localRotation = Quaternion.Euler(0f, 0f, tilt);
                        g.OnPlayerDied();
                        return;
                    }
                    break;
                }

                case FlappyGame.GState.Dead:
                    // Tumble out of the world rather than blinking away.
                    vy = Mathf.Max(terminalVelocity, vy - gravity * 0.55f * dt);
                    p.y += vy * dt;
                    tilt = Mathf.LerpAngle(tilt, -110f, 5f * dt);
                    break;
            }

            transform.position = p;
            visual.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        void OnTriggerEnter(Collider other)
        {
            var g = FlappyGame.Instance;
            if (g == null || g.State != FlappyGame.GState.Playing) return;

            if (other.GetComponent<Hazard>() != null)
            {
                g.OnPlayerDied();
                return;
            }

            var gate = other.GetComponent<ScoreGate>();
            if (gate != null && !gate.used)
            {
                gate.used = true;
                g.AddScore();
                return;
            }

            var coin = other.GetComponent<Coin>();
            if (coin != null)
            {
                g.CollectCoin(coin.transform.position);
                Destroy(coin.gameObject);
            }
        }
    }
}
