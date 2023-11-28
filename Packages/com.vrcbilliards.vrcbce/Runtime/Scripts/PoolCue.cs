using UdonSharp;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDKBase;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// The code that handles the pool cue. This script is contained on the pickup that is on the lower end of the
    /// shaft.
    /// </summary>
    
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PoolCue : UdonSharpBehaviour
    {
        public Logger logger;
        
        public PoolOtherHand otherHand;
        private Transform targetTransform;

        private Vector3 cueRespawnPosition;
        private Vector3 otherHandRespawnPosition;

        public Transform cueParent;

        public PoolStateManager poolStateManager;

        /// <summary>
        /// Pickup Components
        /// </summary>
        private VRC_Pickup thisPickup;
        private VRC_Pickup targetPickup;

        private Vector3 positionAtStartOfArming;
        private Vector3 normalizedLineOfCueWhenArmed;

        private Vector3 offsetBetweenArmedPositions;

        /// <summary>
        /// Is cue ready for hit and rotation locked
        /// </summary>
        private bool isArmed;
        private bool locked;

        private Collider ownCollider;
        private Collider targetCollider;
        
        private bool localPlayerIsInDesktopTopDownView;

        private Vector3 oldTargetPos;

        private Vector3 vectorOne = Vector3.one;
        private Vector3 vectorZero = Vector3.zero;
        private Quaternion upwardsRotation = Quaternion.Euler(-90, 0, 0);
        private Quaternion startingRotation;

        private bool startupCompleted;

        [HideInInspector]
        public bool tableIsActive;

        private bool isInVR;

        public void Start()
        {
            cueRespawnPosition = transform.localPosition;
            startingRotation = cueParent.localRotation;
            otherHandRespawnPosition = otherHand.transform.localPosition;
            
            _Respawn(true);

            isInVR = Networking.LocalPlayer.IsUserInVR();
        }

        public void _Respawn(bool disableCue)
        {
            if (!Networking.LocalPlayer.IsValid())
            {
                gameObject.SetActive(false);
                return;
            }

            ownCollider = GetComponent<Collider>();
            targetTransform = otherHand.transform;
            targetCollider = targetTransform.GetComponent<Collider>();

            if (!targetCollider)
            {
                if (logger)
                {
                    logger._Error(gameObject.name, "PoolCue: Start: target is missing a collider. Aborting cue setup.");
                }
                
                gameObject.SetActive(false);
                return;
            }

            ResetTarget();

            thisPickup = (VRC_Pickup) gameObject.GetComponent(typeof(VRC_Pickup));
            if (!thisPickup)
            {
                if (logger)
                {
                    logger._Error(gameObject.name, "PoolCue: Start: this object is missing a VRC_Pickup script. Aborting cue setup.");
                }
                
                gameObject.SetActive(false);
                return;
            }

            targetPickup = (VRC_Pickup) targetTransform.GetComponent(typeof(VRC_Pickup));
            if (!targetPickup)
            {
                if (logger)
                {
                    logger._Error(gameObject.name, "PoolCue: Start: target object is missing a VRC_Pickup script. Aborting cue setup.");
                }

                gameObject.SetActive(false);
                return;
            }

            if (disableCue)
            {
                _DenyAccess();
            }

            transform.localPosition = cueRespawnPosition;
            cueParent.localPosition = transform.localPosition;
            transform.localRotation = startingRotation;
            targetTransform.localPosition = otherHandRespawnPosition;
            cueParent.LookAt(targetTransform.position);

            startupCompleted = true;
        }

        public void LateUpdate()
        {
            if (!startupCompleted)
            {
                return;
            }

            if (!tableIsActive)
            {
                return;
            }

            // Put cue in hand
            if (localPlayerIsInDesktopTopDownView)
            {
                return;
            }
            
            if (isArmed)
            {
                offsetBetweenArmedPositions = transform.position - positionAtStartOfArming;

                // Pull the cue backwards or forwards on the locked cue's line based on how far away the locking cue handle has been moved since locking.
                cueParent.position = positionAtStartOfArming + (normalizedLineOfCueWhenArmed * Vector3.Dot(offsetBetweenArmedPositions, normalizedLineOfCueWhenArmed));
            }
            else if(thisPickup.currentPlayer != null)
            {
                var lerpPercent = Time.deltaTime * 16.0f;
                cueParent.position = Vector3.Lerp(cueParent.position, transform.position, lerpPercent);

                if (thisPickup.currentPlayer.IsUserInVR())
                {
                    cueParent.LookAt(Vector3.Lerp(oldTargetPos, targetTransform.position, lerpPercent));
                }
                else
                {
                    cueParent.rotation = upwardsRotation;
                }
            }
            else
            {
                cueParent.position = transform.position;
                cueParent.LookAt(targetTransform.position);
            }

            oldTargetPos = targetTransform.position;
        }

        public override void OnPickupUseDown()
        {
            if (!isInVR)
            {
                return;
            }
            
            isArmed = true;
            positionAtStartOfArming = transform.position;
            normalizedLineOfCueWhenArmed = (targetTransform.position - positionAtStartOfArming).normalized;
            poolStateManager._StartHit();
        }

        public override void OnPickupUseUp()
        {
            isArmed = false;
            poolStateManager._EndHit();
        }

        public override void OnPickup()
        {
            if (thisPickup.currentPlayer.playerId == Networking.LocalPlayer.playerId)
            {
                // Not sure if this is necessary to do both since we pickup this one, but just to be safe
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                Networking.SetOwner(Networking.LocalPlayer, otherHand.gameObject); // Varneon: One .gameObject is fine during pickup event
            }

            if (!startupCompleted)
            {
                return;
            }

            if (thisPickup.currentPlayer.IsUserInVR())    // We dont need other hand to be availible for desktop player
            {
                targetTransform.localScale = vectorOne;
            }
            else
            {
                GetComponent<MeshRenderer>().enabled = false;
                otherHand.GetComponent<MeshRenderer>().enabled = false;
            }

            targetTransform.localScale = vectorOne; //TODO: This code is defective.
            targetCollider.enabled = true;

            poolStateManager._LocalPlayerPickedUpCue();
            targetPickup.pickupable = true;
        }

        public override void OnDrop()
        {
            if (!startupCompleted)
            {
                return;
            }

            ResetTarget();
            targetPickup.Drop();

            if (!isInVR)
            {
                GetComponent<MeshRenderer>().enabled = true;
                otherHand.GetComponent<MeshRenderer>().enabled = true;
                poolStateManager._OnPutDownCueLocally();
                _Respawn(false);
            }

            poolStateManager._LocalPlayerDroppedCue();
            targetPickup.pickupable = false;
        }

        /// <summary>
        /// Set if local player can hold onto cue grips
        /// </summary>
        public void _AllowAccess()
        {
            ownCollider.enabled = true;
            targetCollider.enabled = true;
        }

        /// <summary>
        /// Set if local player should not hold onto cue grips
        /// </summary>
        public void _DenyAccess()
        {
            ownCollider.enabled = false;
            targetCollider.enabled = false;

            // Force user to drop it
            thisPickup.Drop();
            targetPickup.Drop();
        }

        private void ResetTarget()
        {
            targetTransform.localScale = vectorZero;
            targetCollider.enabled = false;
        }
        
        public void _EnteredFlatscreenPlayerCamera()
        {
            localPlayerIsInDesktopTopDownView = true;
        }

        public void _LeftFlatscreenPlayerCamera()
        {
            localPlayerIsInDesktopTopDownView = false;
        }
    }
}