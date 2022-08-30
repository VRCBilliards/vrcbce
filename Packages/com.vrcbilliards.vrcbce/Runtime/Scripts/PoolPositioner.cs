using UdonSharp;

namespace VRCBilliards
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    public class PoolPositioner : UdonSharpBehaviour
    {
        public PoolStateManager gameStateManager;

        public override void OnDrop()
        {
            gameStateManager.PlaceBall();
        }
    }
}