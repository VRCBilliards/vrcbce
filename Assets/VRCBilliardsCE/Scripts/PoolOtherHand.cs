
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

        private bool isLocked;
        private Vector3 lockLocation;

        public void Start()
        {
            if (Networking.LocalPlayer == null)
            {
                gameObject.SetActive(false);
                return;
            }

            originalOffset = transform.position;
            originalParent = transform.parent;

            cue = objPrimary.GetComponent<PoolCue>();
            OnDrop();
        }

        public void Update()
        {
            if (isLocked)
            {
                transform.position = lockLocation;
            }
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

        public override void OnPickupUseDown()
        {
            lockLocation = transform.position;
            isLocked = true;
        }

        public override void OnPickupUseUp()
        {
            isLocked = false;
        }

        public void _Respawn()
        {
            transform.position = originalOffset;
        }
    }
}