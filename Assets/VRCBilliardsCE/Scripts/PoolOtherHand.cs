
using UdonSharp;
using UnityEngine;

namespace VRCBilliards
{
    public class PoolOtherHand : UdonSharpBehaviour
    {
        public GameObject objPrimary;
        private PoolCue cue;

        private Vector3 originalDelta;
        private bool isHolding;
        public bool isOtherBeingHeld;

        public void Start()
        {
            cue = objPrimary.GetComponent<PoolCue>();
            OnDrop();
        }

        public void Update()
        {
            if (!isHolding && isOtherBeingHeld)
            {
                gameObject.transform.position = objPrimary.transform.TransformPoint(originalDelta);
            }
        }

        public override void OnPickupUseDown()
        {
            //cue.Lock();
        }

        public override void OnPickupUseUp()
        {
            //cue.Unlock();
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
            //cue.Unlock();
        }
    }
}