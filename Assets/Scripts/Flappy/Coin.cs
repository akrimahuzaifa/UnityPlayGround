using UnityEngine;

namespace Flappy
{
    // Collectible bonus. Flips like a coin for readability.
    public class Coin : MonoBehaviour
    {
        void Update()
        {
            transform.Rotate(0f, 300f * Time.deltaTime, 90f * Time.deltaTime, Space.Self);
        }
    }
}
