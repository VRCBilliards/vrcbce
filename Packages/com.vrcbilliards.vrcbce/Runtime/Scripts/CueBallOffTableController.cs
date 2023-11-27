using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// A very sensible script that allows the cue ball to donk someone (play a sound effect) if, whilst it's flying
    /// off the table, it collides with a player at sufficient speed.
    /// Consider customizing this script if you want to do interesting things with loose cue balls in your world!
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), RequireComponent(typeof(SphereCollider)), RequireComponent(typeof(AudioSource))]
    public class CueBallOffTableController : UdonSharpBehaviour
    {
        public AudioSource source;
        public Rigidbody body;
        public float requiredVelocity;
        public int layerToCollideWithWhenOffTable;
        private int originalLayer;
        
        public void _EnableDonking()
        {
            // allow it to hit players, collide with the environment, etc
            originalLayer = gameObject.layer;
            gameObject.layer = layerToCollideWithWhenOffTable;
        }

        public void _DisableDonking()
        {
            gameObject.layer = originalLayer;
        }
        
        public override void OnPlayerCollisionEnter(VRCPlayerApi player)
        {
            Debug.Log($"donked ${player.displayName}");
            
            if (source && body.velocity.magnitude > requiredVelocity)
            {
                source.Play();
            }   
        }
    }
}