using UnityEngine;
using UnityEngine.Rendering;

namespace FoxPreSlash
{
    /// <summary>
    /// Keeps the character in the neutral shot while the final VFX overlay uses
    /// the complete Cinemachine pose/lens. No character transforms are changed.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FoxPreSlashCameraStack : MonoBehaviour
    {
        public Camera mainCamera;
        public Camera characterCamera;
        public Camera effectsCamera;
        public Transform referenceSpace;
        public Vector3 referencePosition = new Vector3(0, 0, -20);
        public Quaternion referenceRotation = Quaternion.identity;
        public float referenceFieldOfView = 25.360765f;

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
            Synchronize();
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
        }

        void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // Run after Cinemachine, including Timeline scrubbing in edit mode.
            if (camera == mainCamera || camera == characterCamera || camera == effectsCamera)
                Synchronize();
        }

        public void Synchronize()
        {
            if (!mainCamera || !characterCamera || !effectsCamera || !referenceSpace)
                return;

            CopyCamera(mainCamera, effectsCamera);
            effectsCamera.transform.SetPositionAndRotation(
                mainCamera.transform.position, mainCamera.transform.rotation);
            effectsCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix;
            effectsCamera.projectionMatrix = mainCamera.projectionMatrix;

            CopyCamera(mainCamera, characterCamera);
            characterCamera.transform.SetPositionAndRotation(
                referenceSpace.TransformPoint(referencePosition),
                referenceSpace.rotation * referenceRotation);
            characterCamera.ResetWorldToCameraMatrix();
            characterCamera.usePhysicalProperties = false;
            characterCamera.fieldOfView = referenceFieldOfView;
            characterCamera.ResetProjectionMatrix();
            // Matching near/far and view-space Z allows shared depth for the
            // current X/Y translation + roll-only FoxPreSlash camera animation.
            // A future pitch/yaw/dolly shot needs its depth composition reviewed.
        }

        static void CopyCamera(Camera source, Camera destination)
        {
            int mask = destination.cullingMask;
            float depth = destination.depth;
            destination.CopyFrom(source);
            destination.cullingMask = mask;
            destination.depth = depth;
            destination.clearFlags = CameraClearFlags.Nothing;
            // Occlusion baked for the base view must not hide the fixed character.
            destination.useOcclusionCulling = false;
        }
    }
}
