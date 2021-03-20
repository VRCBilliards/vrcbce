using UdonSharp;
using VRC.Udon;

namespace VRCBilliards
{
    public class ActivateUdonEvent : UdonSharpBehaviour
    {
        public UdonBehaviour behaviour;
        public string eventName;
        public bool isNetworkedOwner;
        public bool isNetworkedAll;

        public override void Interact()
        {
            if (behaviour)
            {
                if (isNetworkedOwner)
                {
                    behaviour.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, eventName);
                }
                else if (isNetworkedAll)
                {
                    behaviour.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, eventName);
                }
                else
                {
                    behaviour.SendCustomEvent(eventName);
                }
            }
        }
    }
}