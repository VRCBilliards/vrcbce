
using UdonSharp;
using UnityEngine;

namespace VRCBilliards
{
    public class PoolOtherHand : UdonSharpBehaviour
    {
        public GameObject objPrimary;
        private PoolCue usPrimary;

        private Vector3 originalDelta;
        private bool isHolding;
        public bool isOtherBeingHeld;

        public void Start()
        {
            usPrimary = objPrimary.GetComponent<PoolCue>();
            OnDrop();
        }

        public void Update()
        {
            // Pseudo-parented while it left is let go
            if (!isHolding && isOtherBeingHeld)
            {
                gameObject.transform.position = objPrimary.transform.TransformPoint(originalDelta);
            }
        }

        public override void OnPickupUseDown()
        {
            usPrimary.LockOther();
        }

        public override void OnPickupUseUp()
        {
            usPrimary.UnlockOther();
        }

        public override void OnPickup()
        {
            isHolding = true;
        }

        public override void OnDrop()
        {
            originalDelta = objPrimary.transform.InverseTransformPoint(gameObject.transform.position);

            // Clamp within 1 meters in case something got messed up
            if (originalDelta.sqrMagnitude > 0.6084f)
            {
                originalDelta = originalDelta.normalized * 0.78f;
            }

            isHolding = false;
            usPrimary.UnlockOther();
        }
    }
}