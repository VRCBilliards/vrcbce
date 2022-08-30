using UdonSharp;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDKBase;

namespace VRCBilliards
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    public class PoolCue : UdonSharpBehaviour
    {
        public PoolOtherHand otherHand;
        private Transform targetTransform;

        private Vector3 cueRespawnPosition;
        private Vector3 otherHandRespawnPosition;

        public Transform cueParent;

        public PoolStateManager poolStateManager;

        public GameObject cueTip;

        /// <summary>
        /// The other pool cue at this table.
        /// </summary>
        public PoolCue otherCue;

        /// <summary>
        /// Pickup Components
        /// </summary>
        private VRC_Pickup thisPickup;
        private VRC_Pickup targetPickup;

        [HideInInspector]
        public bool usingDesktop;

        public bool allowAutoSwitch = true;
        public int playerID;

        private Vector3 positionAtStartOfArming;
        private Vector3 normalizedLineOfCueWhenArmed;

        private Vector3 offsetBetweenArmedPositions;

        /// <summary>
        /// Is cue ready for hit and rotation locked
        /// </summary>
        private bool isArmed;
        private bool locked;
        private bool isPickedUp;

        private Collider ownCollider;
        private Collider targetCollider;

        [HideInInspector]
        public bool localPlayerIsInDesktopTopDownView;

        private VRCPlayerApi playerApi;
        private Vector3 oldTargetPos;

        private Vector3 vectorOne = Vector3.one;
        private Vector3 vectorZero = Vector3.zero;
        private Vector3 vectorUp = Vector3.up;
        private Quaternion upwardsRotation = Quaternion.Euler(-90, 0, 0);
        private Quaternion startingRotation;

        private bool startupCompleted;

        [HideInInspector]
        public bool tableIsActive;

        private VRCPlayerApi lastPlayerHeld;

        public void Start()
        {
            if (Networking.LocalPlayer == null)
            {
                gameObject.SetActive(false);
                return;
            }

            cueRespawnPosition = transform.localPosition;
            playerApi = Networking.LocalPlayer;
            usingDesktop = !playerApi.IsUserInVR();

            ownCollider = GetComponent<Collider>();

            targetTransform = otherHand.transform;
            otherHandRespawnPosition = targetTransform.localPosition;

            targetCollider = targetTransform.GetComponent<Collider>();
            if (!targetCollider)
            {
                Debug.LogError("PoolCue: Start: target is missing a collider. Aborting cue setup.");
                gameObject.SetActive(false);
                return;
            }

            ResetTarget();

            thisPickup = (VRC_Pickup)gameObject.GetComponent(typeof(VRC_Pickup));
            if (!thisPickup)
            {
                Debug.LogError("PoolCue: Start: this object is missing a VRC_Pickup script. Aborting cue setup.");
                gameObject.SetActive(false);
                return;
            }

            targetPickup = (VRC_Pickup)targetTransform.GetComponent(typeof(VRC_Pickup));
            if (!targetPickup)
            {
                Debug.LogError("PoolCue: Start: target object is missing a VRC_Pickup script. Aborting cue setup.");
                gameObject.SetActive(false);
                return;
            }

            startingRotation = cueParent.localRotation;

            _DenyAccess();

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
            if (!localPlayerIsInDesktopTopDownView)
            {
                if (isArmed)
                {
                    offsetBetweenArmedPositions = transform.position - positionAtStartOfArming; //cueMainGripOriginalPosition - positionAtStartOfArming;

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
        }

        public override void OnPickupUseDown()
        {
            if (!usingDesktop)
            {
                isArmed = true;
                positionAtStartOfArming = transform.position;
                normalizedLineOfCueWhenArmed = (targetTransform.position - positionAtStartOfArming).normalized;
                poolStateManager._StartHit();
            }
        }

        public override void OnPickupUseUp()
        {
            isArmed = false;
            poolStateManager._EndHit();
        }

        public override void OnPickup()
        {
            lastPlayerHeld = thisPickup.currentPlayer;

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
            otherHand.isOtherBeingHeld = true;
            targetCollider.enabled = true;

            poolStateManager._LocalPlayerPickedUpCue();

            isPickedUp = true;

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

            if (usingDesktop)
            {
                GetComponent<MeshRenderer>().enabled = true;
                otherHand.GetComponent<MeshRenderer>().enabled = true;
                poolStateManager._OnPutDownCueLocally();
            }

            // We rotate the cue rather than make it track the offhand pickup when in Desktop. 
            if (!lastPlayerHeld.IsUserInVR())
            {
                _Respawn();
            }

            poolStateManager._LocalPlayerDroppedCue();
            isPickedUp = false;

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

        /// <summary>
        /// Returns the cue back to the table
        /// </summary>
        public void _Respawn()
        {
            Respawn();
        }

        private void Respawn()
        {
            transform.localPosition = cueRespawnPosition;
            cueParent.localPosition = transform.localPosition;
            transform.localRotation = startingRotation;
            targetTransform.localPosition = otherHandRespawnPosition;
            cueParent.LookAt(targetTransform.position);
        }

        private void ResetTarget()
        {
            targetTransform.localScale = vectorZero;
            otherHand.isOtherBeingHeld = false;
            targetCollider.enabled = false;
        }
    }
}