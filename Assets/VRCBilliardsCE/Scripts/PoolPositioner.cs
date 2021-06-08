
using UdonSharp;

namespace VRCBilliards
{
    public class PoolPositioner : UdonSharpBehaviour
    {
        public PoolStateManager gameStateManager;

        public override void OnDrop()
        {
            gameStateManager.PlaceBall();
        }
    }
}