using UdonSharp;

namespace VRCBilliards
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PoolPositioner : UdonSharpBehaviour
    {
        public PoolStateManager gameStateManager;

        public override void OnDrop()
        {
            gameStateManager.PlaceBall();
        }
    }
}