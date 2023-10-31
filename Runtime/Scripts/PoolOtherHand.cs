using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace VRCBilliards
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PoolOtherHand : UdonSharpBehaviour
    {
        private bool isHolding;
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
            
            originalParent = transform.parent;

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
    }
}