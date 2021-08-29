
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace VRCBilliards {
    public class CueRespawn : UdonSharpBehaviour
    {
        public PoolCue[] poolCues;
        void Interact()
        {
            poolCues[0]._Respawn();
            poolCues[1]._Respawn();
        }
    }
}