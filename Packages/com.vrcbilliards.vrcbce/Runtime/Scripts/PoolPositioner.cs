using UdonSharp;

namespace VRCBilliards
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PoolPositioner : UdonSharpBehaviour
    {
        public BasePoolStateManager gameStateManager;

        public override void OnDrop()
        {
            gameStateManager.PlaceBall();
        }
    }
}