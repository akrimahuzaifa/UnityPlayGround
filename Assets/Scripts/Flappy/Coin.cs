using UnityEngine;

namespace Flappy
{
    // Collectible bonus. Flips like a coin for readability; a single axis keeps
    // the bolt emblem upright.
    public class Coin : MonoBehaviour
    {
        void Update()
        {
            transform.Rotate(0f, 240f * Time.deltaTime, 0f, Space.Self);
        }
    }
}
