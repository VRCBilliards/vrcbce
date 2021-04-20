using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace VRCBilliards
{
    [RequireComponent(typeof(SphereCollider))]
    public class PoolCue : UdonSharpBehaviour
    {
        public GameObject target;
        public PoolOtherHand targetController;

        public GameObject cueParent;

        public PoolStateManager gameController;

        public GameObject cueTip;
        public GameObject pressE;

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
        public bool inTopDownMode;

        [HideInInspector]
        public bool useDesktop;

        public bool allowAutoSwitch = true;
        public int playerID;

        private Vector3 cueOffhandGripOriginalPosition;
        private Vector3 cueMainGripOriginalPosition;

        private Vector3 vBase;
        private Vector3 vLineNorm;

        private Vector3 vSnOff;
        private float vSnDet;

        private bool isArmed;
        private bool isHolding;
        private bool isOtherLock;

        private Vector3 cueResetPosition;
        private Vector3 targetResetPosition;

        private SphereCollider ownCollider;
        private SphereCollider targetCollider;

        public void Start()
        {
            ownCollider = GetComponent<SphereCollider>();

            targetCollider = target.GetComponent<SphereCollider>();
            if (!targetCollider)
            {
                Debug.LogError("ht8b_cue: Start: target is missing a SphereCollider. Aborting cue setup.");
                gameObject.SetActive(false);
                return;
            }

            // Match lerped positions at start
            cueMainGripOriginalPosition = gameObject.transform.position;
            cueOffhandGripOriginalPosition = target.transform.position;

            OnDrop();

            thisPickup = (VRC_Pickup)gameObject.GetComponent(typeof(VRC_Pickup));
            if (!thisPickup)
            {
                Debug.LogError("ht8b_cue: Start: this object is missing a VRC_Pickup script. Aborting cue setup.");
                gameObject.SetActive(false);
                return;
            }

            targetPickup = (VRC_Pickup)target.GetComponent(typeof(VRC_Pickup));
            if (!targetPickup)
            {
                Debug.LogError("ht8b_cue: Start: target object is missing a VRC_Pickup script. Aborting cue setup.");
                gameObject.SetActive(false);
                return;
            }

            targetResetPosition = target.transform.position;
            cueResetPosition = gameObject.transform.position;

            DenyAccess();
        }

        public void Update()
        {
            // Put cue in hand
            if (!inTopDownMode)
            {
                if (useDesktop && isHolding)
                {
                    gameObject.transform.position = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightHand);

                    // Temporary target
                    target.transform.position = gameObject.transform.position + Vector3.up;

                    // How close are we to the table?
                    Vector3 playerpos = gameController.gameObject.transform.InverseTransformPoint(Networking.LocalPlayer.GetPosition());

                    // Check turn entry
                    if ((Mathf.Abs(playerpos.x) < 2.0f) && (Mathf.Abs(playerpos.z) < 1.5f))
                    {
                        // If we're close to the table, make it so we can enter desktop top-down view.
                        VRCPlayerApi.TrackingData hmd = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                        pressE.SetActive(true);
                        pressE.transform.position = hmd.position + (hmd.rotation * Vector3.forward);

                        if (Input.GetKeyDown(KeyCode.E))
                        {
                            otherCue.inTopDownMode = true;
                            inTopDownMode = true;
                            gameController.OnDesktopTopDownViewStart();
                        }
                    }
                    else
                    {
                        pressE.SetActive(false);
                    }
                }

                cueMainGripOriginalPosition = Vector3.Lerp(cueMainGripOriginalPosition, gameObject.transform.position, Time.deltaTime * 16.0f);

                if (!isOtherLock)
                {
                    cueOffhandGripOriginalPosition = Vector3.Lerp(cueOffhandGripOriginalPosition, target.transform.position, Time.deltaTime * 16.0f);
                }

                if (isArmed)
                {
                    vSnOff = cueMainGripOriginalPosition - vBase;
                    vSnDet = Vector3.Dot(vSnOff, vLineNorm);
                    cueParent.transform.position = vBase + (vLineNorm * vSnDet);
                }
                else
                {
                    // TODO: Fix a bug where 2p on Desktop doesn't see their cue.

                    // put cue at base position	
                    cueParent.transform.position = cueMainGripOriginalPosition;
                    cueParent.transform.LookAt(cueOffhandGripOriginalPosition);
                }
            }

            // if (isHolding) // TODO: Refactor.
            // {
            //     // Clamp controllers to play boundaries while we have hold of them
            //     Vector3 temp = this.transform.localPosition;
            //     temp.x = Mathf.Clamp(temp.x, -4.0f, 4.0f);
            //     temp.y = Mathf.Clamp(temp.y, -0.8f, 1.5f);
            //     temp.z = Mathf.Clamp(temp.z, -3.25f, 3.25f);
            //     this.transform.localPosition = temp;
            //     temp = target.transform.localPosition;
            //     temp.x = Mathf.Clamp(temp.x, -4.0f, 4.0f);
            //     temp.y = Mathf.Clamp(temp.y, -0.8f, 1.5f);
            //     temp.z = Mathf.Clamp(temp.z, -3.25f, 3.25f);
            //     target.transform.localPosition = temp;
            // }
        }

        public override void OnPickupUseDown()
        {
            if (!useDesktop)    // VR only controls
            {
                isArmed = true;

                // copy target position in
                vBase = transform.position;

                // Set up line normal
                vLineNorm = (target.transform.position - vBase).normalized;

                // It should now be able to impulse ball
                gameController.StartHit();
            }
        }

        public override void OnPickupUseUp()
        {
            isArmed = false;
            gameController.EndHit();
        }

        public override void OnPickup()
        {
            if (!useDesktop)    // We dont need other hand to be availible for desktop player
            {
                target.transform.localScale = Vector3.one;
            }

            target.transform.localScale = Vector3.one; //TODO: This code is defective.

            // Register the cuetip with main game
            // gameController.cuetip = objTip; 

            // Not sure if this is necessary to do both since we pickup this one, but just to be safe
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            Networking.SetOwner(Networking.LocalPlayer, target);
            isHolding = true;
            targetController.isOtherBeingHeld = true;
            targetCollider.enabled = true;
        }

        public override void OnDrop()
        {
            target.transform.localScale = Vector3.zero;
            isHolding = false;
            targetController.isOtherBeingHeld = false;
            targetCollider.enabled = false;

            if (useDesktop)
            {
                pressE.SetActive(false);
                gameController.OnPutDownCueLocally();

                target.transform.position = targetResetPosition;
                gameObject.transform.position = cueResetPosition;
            }
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
            // Put back on the table
            target.transform.position = targetResetPosition;
            gameObject.transform.position = cueResetPosition;

            ownCollider.enabled = false;
            targetCollider.enabled = false;

            // Force user to drop it
            thisPickup.Drop();
            targetPickup.Drop();
        }

        public void LockOther()
        {
            isOtherLock = true;
        }

        public void UnlockOther()
        {
            isOtherLock = false;
        }
    }
}