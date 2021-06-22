using UdonSharp;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDKBase;

namespace VRCBilliards
{
    [RequireComponent(typeof(SphereCollider))]
    public class PoolCue : UdonSharpBehaviour
    {
        public GameObject target;
        public PoolOtherHand targetController;

        public Transform cueRespawnPosition;

        public GameObject cueParent;

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

        private bool isArmed;
        private bool locked;
        private bool isPickedUp;

        private Collider ownCollider;
        private Collider targetCollider;

        [HideInInspector]
        public bool localPlayerIsInDesktopTopDownView;

        public PositionConstraint cuePosConstraint;
        public LookAtConstraint cueLookAtConstraint;

        private VRCPlayerApi playerApi;

        public void Start()
        {
            playerApi = Networking.LocalPlayer;
            usingDesktop = !playerApi.IsUserInVR();

            ownCollider = GetComponent<Collider>();

            targetCollider = target.GetComponent<Collider>();
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

            targetPickup = (VRC_Pickup)target.GetComponent(typeof(VRC_Pickup));
            if (!targetPickup)
            {
                Debug.LogError("PoolCue: Start: target object is missing a VRC_Pickup script. Aborting cue setup.");
                gameObject.SetActive(false);
                return;
            }

            DenyAccess();
        }

        public void LateUpdate()
        {
            // Put cue in hand
            if (!localPlayerIsInDesktopTopDownView)
            {
                if (isArmed)
                {
                    offsetBetweenArmedPositions = transform.position - positionAtStartOfArming; //cueMainGripOriginalPosition - positionAtStartOfArming;

                    // Pull the cue backwards or forwards on the locked cue's line based on how far away the locking cue handle has been moved since locking.
                    cueParent.transform.position = positionAtStartOfArming + (normalizedLineOfCueWhenArmed * Vector3.Dot(offsetBetweenArmedPositions, normalizedLineOfCueWhenArmed));
                }
                else if (usingDesktop && isPickedUp)
                {
                    var data = playerApi.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
                    transform.position = data.position;
                }
            }
        }

        public override void OnPickupUseDown()
        {
            if (!usingDesktop)
            {
                isArmed = true;
                cuePosConstraint.enabled = false;
                cueLookAtConstraint.enabled = false;
                positionAtStartOfArming = transform.position;
                normalizedLineOfCueWhenArmed = (target.transform.position - positionAtStartOfArming).normalized;
                poolStateManager.StartHit();
            }
        }

        public override void OnPickupUseUp()
        {
            if (!usingDesktop)
            {
                cuePosConstraint.enabled = true;
                cueLookAtConstraint.enabled = true;
            }

            isArmed = false;
            poolStateManager.EndHit();
        }

        public override void OnPickup()
        {
            if (!usingDesktop)    // We dont need other hand to be availible for desktop player
            {
                target.transform.localScale = Vector3.one;
            }

            target.transform.localScale = Vector3.one; //TODO: This code is defective.

            // Not sure if this is necessary to do both since we pickup this one, but just to be safe
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            Networking.SetOwner(Networking.LocalPlayer, target);
            targetController.isOtherBeingHeld = true;
            targetCollider.enabled = true;

            poolStateManager.LocalPlayerPickedUpCue();

            isPickedUp = true;
        }

        public override void OnDrop()
        {
            ResetTarget();

            if (usingDesktop)
            {
                poolStateManager.OnPutDownCueLocally();
                Respawn();
            }

            poolStateManager.LocalPlayerDroppedCue();
            isPickedUp = false;
        }

        /// <summary>
        /// Set if local player can hold onto cue grips
        /// </summary>
        public void AllowAccess()
        {
            ownCollider.enabled = true;
            targetCollider.enabled = true;
        }

        /// <summary>
        /// Set if local player should not hold onto cue grips
        /// </summary>
        public void DenyAccess()
        {
            ResetTarget();

            // Put back on the table
            Respawn();

            ownCollider.enabled = false;
            targetCollider.enabled = false;

            // Force user to drop it
            thisPickup.Drop();
            targetPickup.Drop();
        }

        private void Respawn()
        {
            transform.SetPositionAndRotation(cueRespawnPosition.position, cueRespawnPosition.rotation);

            if (usingDesktop)
            {
                poolStateManager.OnPutDownCueLocally();
            }
        }

        private void ResetTarget()
        {
            target.transform.localScale = Vector3.zero;
            targetController.isOtherBeingHeld = false;
            targetCollider.enabled = false;
        }

        public void EnableConstraints()
        {
            cuePosConstraint.enabled = true;
            cueLookAtConstraint.enabled = true;
        }

        public void DisableConstraints()
        {
            cuePosConstraint.enabled = false;
            cueLookAtConstraint.enabled = false;
        }
    }
}