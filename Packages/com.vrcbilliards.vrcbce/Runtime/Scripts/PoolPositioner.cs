using UdonSharp;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// Handles the green icon that indicates the pool cue can be moved.
    /// </summary>
    
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