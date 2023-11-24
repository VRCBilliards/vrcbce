using UdonSharp;
using UnityEngine;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// Handles how scrolling works in the top-down desktop view.
    /// </summary>
    [AddComponentMenu("VRCBCE/Utilities/Camera Scroller")]
    [RequireComponent(typeof(Camera))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CameraSizeScroller : UdonSharpBehaviour
    {
        [Range(0, 10f)]
        public float sensitivity = 1f;
        public float upperBound = 3f;
        public float lowerBound = 0.5f;
        private Camera requiredCamera;
        private float oldSize;

        public void Start()
        {
            requiredCamera = GetComponent<Camera>();
            oldSize = requiredCamera.orthographicSize;
        }

        public void Update()
        {
            float newSize = oldSize - Input.GetAxisRaw("Mouse ScrollWheel") * sensitivity;
            if (newSize > upperBound || newSize < lowerBound)
            {
                return;
            }

            oldSize = newSize;
            requiredCamera.orthographicSize = oldSize;
        }
    }
}
