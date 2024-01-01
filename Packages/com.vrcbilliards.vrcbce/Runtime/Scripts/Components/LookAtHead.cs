using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts.Components
{
    [AddComponentMenu("VRCBCE/Utilities/LookAtHead"), UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LookAtHead : UdonSharpBehaviour
    {
        [Range(0f, 1.0f)]
        [SerializeField] private float lerp;
        private VRCPlayerApi player;
        
        private bool stoppedLookingAtHead;
        private Transform alternateTarget;
        
        public void Start()
        {
            player = Networking.LocalPlayer;
        }

        public void Update()
        {
            if (stoppedLookingAtHead)
            {
                transform.LookAt(alternateTarget);
                
                return;
            }
            
            Vector3 relativePos = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position - transform.position;
            Quaternion toRotation = Quaternion.LookRotation(relativePos);
            transform.rotation = Quaternion.Lerp( transform.rotation, toRotation, lerp);
        }

        public void _StartLookingAtHead()
        {
            stoppedLookingAtHead = false;
        }

        public void _StopLookingAtHead(Transform newAltTarget)
        {
            alternateTarget = newAltTarget;
            stoppedLookingAtHead = true;
        }
    }
}