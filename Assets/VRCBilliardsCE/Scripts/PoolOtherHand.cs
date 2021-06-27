
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace VRCBilliards
{
    public class PoolOtherHand : UdonSharpBehaviour
    {
        public GameObject objPrimary;
        private PoolCue cue;

        private bool isHolding;
        public bool isOtherBeingHeld;

        private Vector3 originalOffset;
        private Transform originalParent;

        public void Start()
        {
            originalOffset = transform.localPosition;
            originalParent = transform.parent;

            cue = objPrimary.GetComponent<PoolCue>();
            OnDrop();
        }

        public override void OnPickup()
        {
            isHolding = true;

            if (Networking.LocalPlayer.IsUserInVR())
            {
                originalParent = transform.parent;
                transform.parent = transform.parent.parent;
            }
        }

        public override void OnDrop()
        {
            isHolding = false;

            if (Networking.LocalPlayer.IsUserInVR())
            {
                transform.parent = originalParent;
            }
        }

        public void Respawn()
        {
            if (originalOffset != new Vector3())
            {
                transform.localPosition = originalOffset;
            }
        }
    }
}