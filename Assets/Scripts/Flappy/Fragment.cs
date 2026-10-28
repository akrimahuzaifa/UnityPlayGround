using UnityEngine;

namespace Flappy
{
    // Lightweight self-managed debris cube: flies out, spins, shrinks, then dies.
    public class Fragment : MonoBehaviour
    {
        Vector3 vel;
        float life, maxLife, spin, baseSize;

        public void Init(Vector3 pos, Vector3 velocity, float size, float lifetime)
        {
            transform.position = pos;
            vel = velocity;
            baseSize = size;
            maxLife = life = lifetime;
            spin = Random.Range(-360f, 360f);
            transform.localScale = Vector3.one * size;
            transform.rotation = Random.rotation;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0f) { Destroy(gameObject); return; }

            vel += Vector3.down * (12f * dt);   // gravity
            vel *= (1f - 1.5f * dt);            // drag
            transform.position += vel * dt;
            transform.Rotate(spin * dt, spin * dt, 0f, Space.Self);

            float k = life / maxLife;
            transform.localScale = Vector3.one * (baseSize * k);
        }
    }
}
