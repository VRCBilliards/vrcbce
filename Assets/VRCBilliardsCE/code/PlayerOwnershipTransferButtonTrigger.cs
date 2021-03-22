using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;

namespace VRCBilliards
{
    public class PlayerOwnershipTransferButtonTrigger : UdonSharpBehaviour
    {
        public UdonBehaviour behaviour;
        public string eventName;
        public string playerObjectName;

        public override void Interact()
        {
            Networking.LocalPlayer.TakeOwnership(gameObject);
        }

        public override void OnOwnershipTransferred()
        {
            behaviour.SetProgramVariable(playerObjectName, Networking.GetOwner(gameObject));
            behaviour.SendCustomEvent(eventName);
        }
    }
}