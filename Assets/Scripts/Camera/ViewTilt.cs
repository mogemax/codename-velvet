using UnityEngine;
using UnityEngine.Rendering;

namespace CodenameVelvet.Cameras
{
    /// <summary>
    /// Leans a sprite visual back by the camera pitch (<see cref="ViewAngle"/>) so it faces the camera squarely.
    /// Goes on the sprite child, never on the physics root. Runs in LateUpdate so it wins over the Animator, then
    /// turns again for each camera about to render: in split screen one camera may be turned and the rest not.
    /// </summary>
    [DisallowMultipleComponent]
    public class ViewTilt : MonoBehaviour
    {
        private float yaw;

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += FaceCamera;
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= FaceCamera;
        private void LateUpdate() => Face(0f);

        private void FaceCamera(ScriptableRenderContext context, Camera cam)
        {
            float cameraYaw = cam.cameraType == CameraType.Game ? cam.transform.eulerAngles.y : 0f;
            if (!Mathf.Approximately(cameraYaw, yaw)) Face(cameraYaw);
        }

        private void Face(float newYaw)
        {
            yaw = newYaw;
            transform.localRotation = Quaternion.Euler(ViewAngle.Pitch, yaw, 0f);
        }
    }
}
