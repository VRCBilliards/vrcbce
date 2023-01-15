using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;

namespace VRCBilliards
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TriggerEventTriggerer : UdonSharpBehaviour
    {
        public UdonBehaviour behaviour;
        public string onEnterEvent;
        public string onLeftEvent;

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (Networking.LocalPlayer.playerId == player.playerId)
            {
                behaviour.SendCustomEvent(onEnterEvent);
            }
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (Networking.LocalPlayer.playerId == player.playerId)
            {
                behaviour.SendCustomEvent(onLeftEvent);
            }
        }
    }
}