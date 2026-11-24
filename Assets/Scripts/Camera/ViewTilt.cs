using UnityEngine;

namespace CodenameVelvet.Cameras
{
    /// <summary>
    /// Leans a sprite visual back by the camera pitch (<see cref="ViewAngle"/>) so it faces the camera squarely.
    /// Goes on the sprite child, never on the physics root. Runs in LateUpdate so it wins over the Animator.
    /// </summary>
    [DisallowMultipleComponent]
    public class ViewTilt : MonoBehaviour
    {
        private void LateUpdate()
        {
            transform.localRotation = Quaternion.Euler(ViewAngle.Pitch, 0f, 0f);
        }
    }
}
