using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts.Components
{
    /// <summary>
    /// <p>A timer, so you can handle callbacks in Udon without <i>pain and suffering.</i></p>
    /// <p>Instantiate this prefab then call <c>_Set</c> to use it.</p>
    /// <p>Invalidate it with <c>_Cancel.</c></p>
    /// <p>Conclude it with <c>_Trigger.</c></p>
    /// </summary>
    [AddComponentMenu("VRCBCE/Utilities/Timer")]
    public class Timer : UdonSharpBehaviour
    {
        private UdonBehaviour target;
        private string eventName;

        public void _Set(UdonBehaviour newTarget, string newName, float time)
        {
            target = newTarget;
            eventName = newName;
            
            SendCustomEventDelayedSeconds(nameof(_Trigger), time);
        }
        
        public void _Trigger()
        {
            if (!Utilities.IsValid(target))
            {
                return;
            }
            
            target.SendCustomEvent(eventName);
                
            Destroy(gameObject);
        }

        public void _Cancel()
        {
            Destroy(gameObject);
        }
    }
}