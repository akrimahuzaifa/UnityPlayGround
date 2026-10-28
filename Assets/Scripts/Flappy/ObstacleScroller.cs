using UnityEngine;

namespace Flappy
{
    /// Scrolls one obstacle group left at the current world speed and despawns it
    /// offscreen. Later in a run the group also drifts vertically, which is what
    /// turns the back half of the game into something harder than plain Flappy.
    public class ObstacleScroller : MonoBehaviour
    {
        public float bobAmplitude;
        public float bobSpeed = 1.5f;

        float phase;
        float baseY;

        void Start()
        {
            baseY = transform.position.y;
            phase = Random.Range(0f, Mathf.PI * 2f);
        }

        void Update()
        {
            var g = FlappyGame.Instance;
            if (g == null) return;

            Vector3 p = transform.position;
            p.x -= g.WorldSpeed * Time.deltaTime;
            if (bobAmplitude > 0f)
                p.y = baseY + Mathf.Sin(Time.time * bobSpeed + phase) * bobAmplitude;
            transform.position = p;

            if (p.x < g.DespawnX) Destroy(gameObject);
        }
    }
}
