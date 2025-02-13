using System;
using System.Runtime.Remoting.Messaging;
using UdonSharp;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using VRC.SDK3.Components;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts.Components;

// ReSharper disable SpecifyACultureInStringConversionExplicitly
// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// The reasons why the table can be reset
    /// </summary>
    public enum ResetReason
    {
        InstanceOwnerReset,
        PlayerReset,
        InvalidState,
        PlayerLeft
    }

    /// <summary>
    /// This is the base logic that governs the pool table, devoid of almost all physics code. This code is quite
    /// messy; it includes all setup, teardown, game state and replication logic, and works with all other components
    /// to allow the player to play pool.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public abstract class BasePoolStateManager : DebuggableUdon
    {
        #region Dependencies

        public PoolMenu poolMenu;
        public PoolCue[] poolCues;
        public ScreenspaceUI pancakeUI;
        public OscSlogger slogger;
        public Logger logger;

        #endregion

        #region Networked Variables

        [UdonSynced] protected bool turnIsRunning;
        [UdonSynced] private int timerSecondsPerShot;
        [UdonSynced] private bool isPlayerAllowedToPlay;
        [UdonSynced] private bool guideLineEnabled = true;

        [UdonSynced] protected bool[] ballsArePocketed;
        private bool[] oldBallsArePocketed;

        [UdonSynced] protected bool isTeam2Turn;
        private bool oldIsTeam2Turn;

        [UdonSynced] private bool isFoul;
        [UdonSynced] private bool isGameOver;

        [UdonSynced] protected bool isOpen = true;
        private bool oldOpen;

        [UdonSynced] protected bool isTeam2Blue;

        [UdonSynced] private bool isGameInMenus = true;
        private bool oldIsGameInMenus;

        [UdonSynced] private bool isTeam2Winner;
        [UdonSynced] private bool isTableLocked = true;
        [UdonSynced] private bool isTeams;

        [UdonSynced] private uint gameID;
        private uint oldGameID;

        [UdonSynced] private uint turnID;
        [UdonSynced] private bool isRepositioningCueBall;

        [UdonSynced] private int[] scores = new int[2];
        [UdonSynced] protected Vector3[] currentBallPositions = new Vector3[NUMBER_OF_SIMULATED_BALLS];
        [UdonSynced] protected Vector3[] currentBallVelocities = new Vector3[NUMBER_OF_SIMULATED_BALLS];
        [UdonSynced] protected Vector3[] currentAngularVelocities = new Vector3[NUMBER_OF_SIMULATED_BALLS];

        // TODO: These gameMode vars are sloppy and are a good candidate for a refactor into one value, and really
        // this can just be a string.
        [UdonSynced] protected uint gameMode; // 0 = 8ball, 1 = 9ball, 2 = Carom
        [UdonSynced] private bool isKorean; // False = Japanese 4-Ball Carom, True = Korean 4-Ball Carom
        [UdonSynced] private bool isThreeCushionCarom;
        [UdonSynced] private int player1ID;
        [UdonSynced] private int player2ID;
        [UdonSynced] private int player3ID;
        [UdonSynced] private int player4ID;
        [UdonSynced] private bool gameWasReset;
        [UdonSynced] private ResetReason latestResetReason;

        #endregion

        #region Table Dimensions

        /// <summary>
        /// These numbers determine the exact size of your table, and will correspond to the blue, yellow and red
        /// guidance lines you can see in the editor. Theoretically, as long as your table is a cuboid and has six
        /// pockets, we support it.
        /// </summary>

        // Note the WIDTH and HEIGHT are halved from their real values - this is due to the reliance of the physics code
        // working in four quadrants to detect collisions.
        [Tooltip("How wide is the table? (Meters/2)")]
        public float tableWidth = 1.1f;

        [Tooltip("How long is the table? (Meters/2)")]
        public float tableHeight = 0.64f;

        [Tooltip("Where are the corner pockets located??")]
        public Vector3 cornerPocket = new Vector3(1.135f, 0.0f, 0.685f);

        [Tooltip("Where are the two pockets located?")]
        public Vector3 middlePocket = new Vector3(0.0f, 0.0f, 0.72f);

        [Tooltip("What's the radius of the hole that the pocket makes in the cushions? (Meters)")]
        public float pocketOuterRadius = 0.11f;

        [Tooltip("What's the radius of the pocket holes? (Meters)")]
        public float pocketInnerRadius = 0.078f;

        [Tooltip("How wide are the table's balls? (Meters)")]
        public float ballDiameter = 0.06f;

        #endregion

        // The number of balls we simulate - this const allows us to increase the number we support in the future.
        protected const int NUMBER_OF_SIMULATED_BALLS = 16;

        // A small fraction designed to slightly move balls around when placed, which helps with making the table
        // less deterministic.
        protected const float RANDOMIZE_F = 0.0001f;

        // These four consts dictate some placement of balls when placed.
        public float SPOT_POSITION_X = 0.5334f;
        public float SPOT_CAROM_X = 0.8001f;
        public float BALL_PL_X = 0.03f;
        public float BALL_PL_Y = 0.05196152422f;

        #region Desktop

        private const float DEFAULT_DESKTOP_CUE_ANGLE = 10.0f;

        // This should never be 90.0f or higher, as it puts the physics simulation into a weird state.
        private const float MAX_DESKTOP_CUE_ANGLE = 89.0f;
        private const float MIN_DESKTOP_CUE_ANGLE = 0.0f;

        #endregion

        /*[Header("Options")]*/ [Tooltip("Use fake shadows? They may clash with your world's lighting.")]
        public bool fakeBallShadows = true;

        [Tooltip("Does the table model for this table have rails that guide the ball when the ball sinks?")]
        public bool tableModelHasRails;

        /*[Header("Important Objects")]*/ public Transform sunkBallsPositionRoot;
        public GameObject shadows;
        public ParticleSystem plusOneParticleSystem;

        public ParticleSystem minusOneParticleSystem;

        // Where's the surface of the table?
        public Transform tableSurface;

        /*[Header("Shader Information")]*/ public string uniformTableColour = "_EmissionColor";
        public string uniformMarkerColour = "_Color";
        public string uniformCueColour = "_EmissionColor";
        public string uniformBallColour = "_BallColour";
        public string uniformBallFloat = "_CustomColor";
        public string ballMaskToggle = "_Turnoff";

        [Tooltip(
            "Change the length of the intro ball-drop animation. If you set this to zero, the animation will not play at all.")]
        [Range(0f, 5f)]
        public float introAnimationLength = 2.0f;

        /*[Header("Table Colours")]*/ [ColorUsage(true, true)]
        public Color tableBlue = new Color(0.0f, 0.5f, 1.5f, 1.0f);

        [ColorUsage(true, true)] public Color tableOrange = new Color(1.5f, 0.5f, 0.0f, 1.0f);
        [ColorUsage(true, true)] public Color tableRed = new Color(1.4f, 0.0f, 0.0f, 1.0f);
        [ColorUsage(true, true)] public Color tableWhite = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        [ColorUsage(true, true)] public Color tableBlack = new Color(0.01f, 0.01f, 0.01f, 1.0f);
        [ColorUsage(true, true)] public Color tableYellow = new Color(2.0f, 1.0f, 0.0f, 1.0f);
        [ColorUsage(true, true)] public Color tableLightBlue = new Color(0.45f, 0.9f, 1.5f, 1.0f);
        public Color markerOK = new Color(0.0f, 1.0f, 0.0f, 1.0f);
        public Color markerNotOK = new Color(1.0f, 0.0f, 0.0f, 1.0f);
        public Color gripColourActive = new Color(0.0f, 0.5f, 1.1f, 1.0f);
        public Color gripColourInactive = new Color(0.34f, 0.34f, 0.34f, 1.0f);
        public Color fabricGray = new Color(0.3f, 0.3f, 0.3f, 1.0f);
        public Color fabricBlue = new Color(0.1f, 0.6f, 1.0f, 1.0f);
        public Color fabricGreen = new Color(0.15f, 0.75f, 0.3f, 1.0f);

        [ColorUsage(true, true)] private Color[] ballColors = new Color[NUMBER_OF_SIMULATED_BALLS];

        /*[Header("Colour Options")]*/ public bool ballCustomColours;
        public ColorPicker blueTeamSliders;
        public ColorPicker orangeTeamSliders;

        private float shaderToggleFloat = 0;

        /*[Header("Cues")]*/ protected GameObject cueTip;
        public GameObject[] cueTips;
        public MeshRenderer[] cueRenderObjs;
        private Material[] cueMaterials = new Material[2];

        /// <summary>
        /// The balls that are used by the table.
        /// The order of the balls is as follows: cue, black, all blue in ascending order, then all orange in ascending order.
        /// If the order of the balls is incorrect, gameplay will not proceed correctly.
        /// </summary>
        /*[Header("Table Objects")]*/
        [Tooltip(
            "The balls that are used by the table." +
            "\nThe order of the balls is as follows: cue, black, all blue in ascending order, then all orange in ascending order." +
            "\nIf the order of the balls is incorrect, gameplay will not proceed correctly."
        )]
        public Transform[] ballTransforms;

        private Rigidbody[] ballRigidbodies;

        [Tooltip("The shadow object for each ball")]
        public PositionConstraint[] ballShadowPosConstraints;

        private Transform[] ballShadowPosConstraintTransforms;
        public ShotGuideController guideline;
        public GameObject devhit;
        public GameObject[] playerTotems;
        public GameObject marker;
        private Material markerMaterial;
        public GameObject marker9ball;
        public GameObject pocketBlockers;

        /*[Header("Materials")]*/ public MeshRenderer[] ballRenderers;

        public MeshRenderer tableRenderer;
        private Material[] tableMaterials;

        public Texture[] sets;

        public Material[] cueGrips;

        /*[Header("Audio")]*/ public GameObject audioSourcePoolContainer;
        public AudioSource cueTipSrc;
        public AudioClip introSfx;
        public AudioClip sinkSfx;

        /// <summary>
        /// The SFX that plays when a good thing happens (sinking a ball correctly, etc)
        /// </summary>
        public AudioClip successSfx;

        /// <summary>
        /// The SFX that plays when a foul occurs
        /// </summary>
        public AudioClip foulSfx;

        /// <summary>
        /// The SFX that plays when someone wins
        /// </summary>
        public AudioClip winnerSfx;

        public AudioClip[] hitsSfx;
        public AudioClip newTurnSfx;
        public AudioClip pointMadeSfx;
        public AudioClip hitBallSfx;

        /*[Header("Reflection Probes")]*/ public ReflectionProbe tableReflection;

        /*[Header("Meshes")]*/ public Mesh[] cueballMeshes;
        public Mesh nineBall;

        /// <summary>
        /// Player is hitting
        /// </summary>
        private bool isArmed;

        private int localPlayerID = -1;

        /*[Header("Desktop Stuff")]*/ public GameObject desktopHitPosition;

        public GameObject desktopBase;

        //public GameObject desktopQuad;
        public GameObject[] desktopCueParents;
        public UnityEngine.UI.Image tiltAmount;

        /*
         * Private variables
         */
        private AudioSource[] ballPool;

        private Transform[] ballPoolTransforms;
        private AudioSource mainSrc;
        private UdonBehaviour udonChips;

        /// <summary>
        /// We are waiting for our local simulation to finish, before we unpack data
        /// </summary>
        protected bool isUpdateLocked;

        /// <summary>
        /// The first ball to be hit by cue ball
        /// </summary>
        private int firstHitBallThisTurn;

        // TODO: This would be a good option.

        [Range(10, 50),
         Tooltip("How many points do your players need to win a carom game? This is the same for all carom variants.")]
        public int scoreNeededToWinCarom = 10;

        private int secondBallHitThisTurn;
        private int thirdBallHitThisTurn;
        protected int cushionsHitThisTurn;

        /// <summary>
        /// If the simulation was initiated by us, only set from update
        /// </summary>
        protected bool isSimulatedByUs;

        /// <summary>
        /// Ball dropper timer
        /// </summary>
        private float introAnimTimer;

        private float remainingTime;
        private bool isTimerRunning;
        private bool isMadePoint;
        private bool isMadeFoul;

        protected bool is8Ball;
        protected bool isNineBall;
        protected bool isFourBall;

        /// <summary>
        /// Game should run in practice mode
        /// </summary>
        private bool isGameModePractice;

        private bool isInDesktopTopDownView;

        /// <summary>
        /// Interpreted value
        /// </summary>
        private bool playerIsTeam2;

        /// <summary>
        /// Runtime target colour
        /// </summary>
        private Color tableSrcColour = new Color(1.0f, 1.0f, 1.0f, 1.0f);

        /// <summary>
        /// Runtime actual colour
        /// </summary>
        private Color tableCurrentColour = new Color(1.0f, 1.0f, 1.0f, 1.0f);

        /// <summary>
        /// Team 0
        /// </summary>
        private Color pointerColour0;

        /// <summary>
        /// Team 1
        /// </summary>
        private Color pointerColour1;

        /// <summary>
        /// No team / open / 9 ball
        /// </summary>
        private Color pointerColour2;

        private Color pointerColourErr;
        private Color pointerClothColour;
        private Vector3 desktopAimPoint = new Vector3(0.0f, 2.0f, 0.0f);
        private Vector3 desktopHitPoint = new Vector3(0.0f, 0.0f, 0.0f);
        private float desktopAngle = DEFAULT_DESKTOP_CUE_ANGLE;
        public float desktopAngleIncrement = 15f;
        private bool isDesktopShootingIn;
        private bool isDesktopSafeRemove = true;
        private Vector3 desktopShootVector;
        private Vector3 desktopSafeRemovePoint;

        private bool isDesktopLocalTurn;
        private bool isEnteringDesktopModeThisFrame;

        /// <summary>
        /// Cue input tracking
        /// </summary>
        private Vector3 localSpacePositionOfCueTip;

        protected Vector3 localSpacePositionOfCueTipLastFrame;
        protected Vector3 cueLocalForwardDirection;
        protected Vector3 cueArmedShotDirection;

        private float cueFDir;
        protected Vector3 raySphereOutput;
        protected int[] rackOrder8Ball = {9, 2, 10, 11, 1, 3, 4, 12, 5, 13, 14, 6, 15, 7, 8};
        protected int[] rackOrder9Ball = {2, 3, 4, 5, 9, 6, 7, 8, 1};
        protected int[] breakRows9ball = {0, 1, 2, 1, 0};

        private float ballShadowOffset;
        private MeshRenderer[] shadowRenders;

        private bool isPlayerInVR;
        private VRCPlayerApi localPlayer;
        private int networkingLocalPlayerID;
        private Transform markerTransform;

        private Camera desktopCamera;

        private TMPro.TextMeshProUGUI timerText;
        private string timerOutputFormat;
        private UnityEngine.UI.Image timerCountdown;

        private UInt32 oldDesktopCue;
        private UInt32 newDesktopCue;

        /// <summary>
        /// Have we run a network sync once? Used for situations where we need to specifically catch up a late-joiner.
        /// </summary>
        private bool hasRunSyncOnce;

        /// <summary>
        /// For clamping to table or set lower for kitchen
        /// </summary>
        protected float repoMaxX;

        private float desktopClampX;
        private float desktopClampY;

        public CueBallOffTableController cueBallController;

        private int PlayerCount
        {
            get
            {
                int count = 0;
                if (player1ID > 0) count++;
                if (player2ID > 0) count++;
                if (player3ID > 0) count++;
                if (player4ID > 0) count++;
                return count;
            }
        }

        public virtual void Start()
        {
            repoMaxX = tableWidth;
            desktopClampX = tableWidth;
            desktopClampY = tableHeight;

            localPlayer = Networking.LocalPlayer;
            networkingLocalPlayerID = localPlayer.playerId;
            isPlayerInVR = localPlayer.IsUserInVR();
            tableMaterials = tableRenderer.materials;

            ballsArePocketed = new bool[ballTransforms.Length];
            oldBallsArePocketed = new bool[ballTransforms.Length];
            ballRigidbodies = new Rigidbody[ballTransforms.Length];
            for (int i = 0; i < ballRigidbodies.Length; i++)
            {
                ballRigidbodies[i] = ballTransforms[i].GetComponent<Rigidbody>();
            }

            ballShadowPosConstraintTransforms = new Transform[ballShadowPosConstraints.Length];
            for (int i = 0; i < ballShadowPosConstraints.Length; i++)
            {
                ballShadowPosConstraintTransforms[i] = ballShadowPosConstraints[i].transform;
            }

            mainSrc = GetComponent<AudioSource>();

            if (audioSourcePoolContainer)
            {
                ballPool = audioSourcePoolContainer.GetComponentsInChildren<AudioSource>();
                ballPoolTransforms = new Transform[ballPool.Length];

                for (int i = 0; i < ballPool.Length; i++)
                {
                    ballPoolTransforms[i] = ballPool[i].transform;
                }
            }

            desktopCamera = desktopBase.GetComponentInChildren<Camera>();
            desktopCamera.enabled = false;

            markerTransform = marker.transform;

            CopyGameStateToOldState();

            markerMaterial = marker.GetComponent<MeshRenderer>().material;
            cueMaterials[0] = cueRenderObjs[0].material;
            cueMaterials[1] = cueRenderObjs[1].material;

            cueMaterials[0].SetColor(uniformCueColour, tableBlack);
            cueMaterials[1].SetColor(uniformCueColour, tableBlack);

            cueTip = cueTips[0];

            if (tableReflection)
            {
                tableReflection.gameObject.SetActive(true);
                tableReflection.mode = ReflectionProbeMode.Realtime;
                tableReflection.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                tableReflection.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                tableReflection.RenderProbe();
            }

            if (guideline)
            {
                guideline.gameObject.SetActive(false);
            }

            if (devhit)
            {
                devhit.SetActive(false);
            }

            if (marker)
            {
                marker.SetActive(false);
            }

            if (marker9ball)
            {
                marker9ball.SetActive(false);
            }

            if (shadows)
            {
                shadows.SetActive(fakeBallShadows);
            }

            ballShadowOffset = ballTransforms[0].position.y - ballShadowPosConstraintTransforms[0].position.y;

            shadowRenders = shadows.GetComponentsInChildren<MeshRenderer>();

            timerText = poolMenu.visibleTimerDuringGame;
            timerOutputFormat = poolMenu.timerOutputFormat;
            timerCountdown = poolMenu.timerCountdown;

            desktopCameraInitialPosition = desktopCamera.transform.localPosition;
            desktopCameraInitialRotation = desktopCamera.transform.localRotation;
            initialPowerBarPos = powerBar.transform.localPosition;
        }

        public virtual void Update()
        {
            if (isInDesktopTopDownView)
            {
                HandleUpdatingDesktopViewUI();

                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
                {
                    OnDesktopTopDownViewExit();
                }
            }
            else if (canEnterDesktopTopDownView)
            {
                if (Input.GetKeyDown(KeyCode.E) || lastInputUseDown)
                {
                    OnDesktopTopDownViewStart();
                }
            }

            if (isGameInMenus)
            {
                return;
            }

            // Everything below this line only runs when the game is active.

            for (int i = 0; i < ballsArePocketed.Length; i++)
            {
                if (!ballsArePocketed[i])
                {
                    ballTransforms[i].localPosition = currentBallPositions[i];
                }
            }

            localSpacePositionOfCueTip = tableSurface.transform.InverseTransformPoint(cueTip.transform.position);
            Vector3 copyOfLocalSpacePositionOfCueTip = localSpacePositionOfCueTip;

            // if shot is prepared for next hit
            if (isPlayerAllowedToPlay)
            {
                if (isRepositioningCueBall)
                {
                    HandleRepositioningCueBall();

                    if (IsCueContacting())
                    {
                        markerMaterial.SetColor(uniformMarkerColour, markerNotOK);
                    }
                    else
                    {
                        markerMaterial.SetColor(uniformMarkerColour, markerOK);
                    }
                }

                Vector3 cueballPosition = currentBallPositions[0];

                if (isArmed)
                {
                    copyOfLocalSpacePositionOfCueTip =
                        AimAndHitCueBall(copyOfLocalSpacePositionOfCueTip, cueballPosition);
                }
                else
                {
                    HandleGuidelinesAndAimMarkers(copyOfLocalSpacePositionOfCueTip, cueballPosition);
                }
            }

            localSpacePositionOfCueTipLastFrame = copyOfLocalSpacePositionOfCueTip;

            // Table outline colour
            if (isGameInMenus)
            {
                // Flashing if we won
                tableCurrentColour = tableSrcColour * ((Mathf.Sin(Time.timeSinceLevelLoad * 3.0f) * 0.5f) + 1.0f);
            }
            else
            {
                tableCurrentColour = Color.Lerp(tableCurrentColour, tableSrcColour, Time.deltaTime * 3.0f);
            }

            //float timePercentage;

            if (isTimerRunning)
            {
                HandleTimerCountdown();
            }
            else
            {
                timerText.text = "";
                timerCountdown.fillAmount = 0f;
            }

            foreach (Material tableMaterial in tableMaterials)
            {
                tableMaterial.SetColor(uniformTableColour, tableCurrentColour);
            }

            // Run the intro animation. Do not run the animation if this is our first sync!
            if (hasRunSyncOnce && introAnimTimer > 0.0f)
            {
                HandleIntroAnimation();
            }
        }

        protected abstract bool IsCueContacting();

        private void HandleRepositioningCueBall()
        {
            Vector3 temp = markerTransform.localPosition;
            temp.x = Mathf.Clamp(temp.x, -tableWidth, repoMaxX);
            temp.z = Mathf.Clamp(temp.z, -tableHeight, tableHeight);
            temp.y = 0.0f;

            currentBallPositions[0] = temp;
            ballTransforms[0].localPosition = temp;

            markerTransform.localPosition = ballTransforms[0].localPosition;
            markerTransform.localRotation = Quaternion.identity;
        }

        public void _ReEnableShadowConstraints()
        {
            foreach (PositionConstraint con in ballShadowPosConstraints)
            {
                con.constraintActive = true;
            }
        }

        public override void OnDeserialization()
        {
            // A somewhat loose way of handling if the pool cues need to run their Update() loops.
            if (isGameInMenus)
            {
                poolCues[0].tableIsActive = false;
                poolCues[1].tableIsActive = false;
            }
            else
            {
                poolCues[0].tableIsActive = true;
                poolCues[1].tableIsActive = true;
            }

            // Check if local simulation is in progress, the event will fire off later when physics
            // are settled by the client.
            if (turnIsRunning)
            {
                isUpdateLocked = true;

                return;
            }

            // We are free to read this update
            ReadNetworkData();
        }

        public void _UnlockTable()
        {
            if (logger)
            {
                logger._Log(name, "UnlockTable");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isTableLocked = false;
            RefreshNetworkData(false);
        }

        public void _LockTable()
        {
            if (logger)
            {
                logger._Log(name, "LockTable");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isTableLocked = true;
            RefreshNetworkData(false);
        }

        public void _JoinGame(int playerNumber)
        {
            if (logger)
            {
                logger._Log(name, $"JoinGame: {playerNumber}");
            }

            Networking.SetOwner(localPlayer, gameObject);
            localPlayerID = playerNumber;

            switch (playerNumber)
            {
                case 0:
                    player1ID = networkingLocalPlayerID;
                    EnableCustomBallColorSlider(true);
                    break;
                case 1:
                    player2ID = networkingLocalPlayerID;
                    EnableCustomBallColorSlider(true);
                    break;
                case 2:
                    player3ID = networkingLocalPlayerID;
                    EnableCustomBallColorSlider(true);
                    break;
                case 3:
                    player4ID = networkingLocalPlayerID;
                    EnableCustomBallColorSlider(true);
                    break;
                default:
                    return;
            }

            RefreshNetworkData(false);

            if (slogger)
            {
                slogger.OscReportJoinedTeam(playerNumber);
            }
        }

        public void _LeaveGame()
        {
            if (logger)
            {
                logger._Log(name, "LeaveGame");
            }

            Networking.SetOwner(localPlayer, gameObject);

            switch (localPlayerID)
            {
                case 0:
                    player1ID = 0;
                    break;
                case 1:
                    player2ID = 0;
                    break;
                case 2:
                    player3ID = 0;
                    break;
                case 3:
                    player4ID = 0;
                    break;
                default:
                    return;
            }

            localPlayerID = -1;

            RefreshNetworkData(false);

            //akalink added, makes the color panel not able to be interacted with
            EnableCustomBallColorSlider(false);
            //end
        }

        // Update loop-scoped handler for introduction animation operations. Non-pure.
        private void HandleIntroAnimation()
        {
            introAnimTimer -= Time.deltaTime;

            Vector3 temp;
            float atime;
            float aitime;

            if (introAnimTimer < 0.0f)
            {
                introAnimTimer = 0.0f;
            }

            // Cueball drops late
            Transform ball = ballTransforms[0];
            temp = ball.localPosition;
            float height = ball.position.y - ballShadowOffset;

            atime = Mathf.Clamp(introAnimTimer - 0.33f, 0.0f, 1.0f);
            aitime = 1.0f - atime;
            temp.y = Mathf.Abs(Mathf.Cos(atime * 6.29f)) * atime * 0.5f;
            ball.localPosition = temp;

            Vector3 scale = new Vector3(aitime, aitime, aitime);
            ball.localScale = scale;

            PositionConstraint posCon = ballShadowPosConstraints[0];
            posCon.constraintActive = false;
            Transform posConTrans = ballShadowPosConstraintTransforms[0];
            Vector3 position = ball.position;
            posConTrans.position = new Vector3(position.x, height, position.z);
            posConTrans.localScale = scale;

            MeshRenderer r = shadowRenders[0];
            Material material = r.material;
            Color c = material.color;
            material.color = new Color(c.r, c.g, c.b, aitime);

            for (int i = 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                ball = ballTransforms[i];

                temp = ball.localPosition;
                height = ball.position.y - ballShadowOffset;

                atime = Mathf.Clamp(introAnimTimer - 0.84f - (i * 0.03f), 0.0f, 1.0f);
                aitime = 1.0f - atime;

                temp.y = Mathf.Abs(Mathf.Cos(atime * 6.29f)) * atime * 0.5f;
                ball.localPosition = temp;

                scale = new Vector3(aitime, aitime, aitime);
                ball.localScale = scale;

                posCon = ballShadowPosConstraints[i];
                posCon.constraintActive = false;
                posConTrans = ballShadowPosConstraintTransforms[i];
                Vector3 position1 = ball.position;
                posConTrans.position = new Vector3(position1.x, height, position1.z);
                posConTrans.localScale = scale;

                r = shadowRenders[i];
                var material1 = r.material;
                c = material1.color;
                material1.color = new Color(c.r, c.g, c.b, aitime);
            }
        }

        // Update loop-scoped handler of timer countdown functionality. Non-pure.
        private void HandleTimerCountdown()
        {
            remainingTime -= Time.deltaTime;

            if (remainingTime < 0.0f)
            {
                isTimerRunning = false;

                // We are holding the stick so propogate the change
                if (Networking.GetOwner(playerTotems[Convert.ToInt32(isTeam2Turn)]) == localPlayer)
                {
                    Networking.SetOwner(localPlayer, gameObject);
                    OnTurnOverFoul();
                }
                else
                {
                    // All local players freeze until next target
                    // can pick up and propogate timer end
                    isPlayerAllowedToPlay = false;
                }

                //timePercentage = 0.0f;
            }
            else
            {
                if (timerText)
                {
                    timerText.text = timerOutputFormat.Replace("{}", Mathf.Round(remainingTime).ToString());
                }

                if (timerCountdown)
                {
                    timerCountdown.fillAmount = remainingTime / timerSecondsPerShot;
                }
            }
        }

        protected abstract bool IsIntersectingWithSphere(Vector3 start, Vector3 dir, Vector3 sphere);

        // Update loop-scoped handler for guidelines and aim markers. Non-pure.
        private void HandleGuidelinesAndAimMarkers(Vector3 copyOfLocalSpacePositionOfCueTip, Vector3 cueballPosition)
        {
            cueLocalForwardDirection = tableSurface.transform.InverseTransformVector(cueTip.transform.forward);

            // Get where the cue will strike the ball
            if (IsIntersectingWithSphere(copyOfLocalSpacePositionOfCueTip, cueLocalForwardDirection,
                    cueballPosition))
            {
                if (guideLineEnabled && guideline)
                {
                    guideline.gameObject.SetActive(true);
                }

                if (devhit)
                {
                    devhit.SetActive(true);
                    devhit.transform.localPosition = raySphereOutput;
                }

                cueArmedShotDirection = cueLocalForwardDirection;
                // cueArmedShotDirection.y = 0.0f;

                if (!isInDesktopTopDownView)
                {
                    // Compute deflection in VR mode
                    Vector3 scuffdir = cueballPosition - raySphereOutput;
                    //scuffdir.y = 0.0f;
                    cueArmedShotDirection += scuffdir.normalized * 0.17f;
                }

                cueFDir = Mathf.Atan2(cueArmedShotDirection.z, cueArmedShotDirection.x);

                // Update the prediction line direction
                Transform transform1 = guideline.transform;
                transform1.localPosition = currentBallPositions[0];
                transform1.localEulerAngles = new Vector3(0.0f, -cueFDir * Mathf.Rad2Deg, 0.0f);
            }
            else
            {
                if (devhit)
                {
                    devhit.SetActive(false);
                }

                if (guideline)
                {
                    guideline.gameObject.SetActive(false);
                }
            }
        }

        protected abstract Vector3 AimAndHitCueBall(Vector3 copyOfLocalSpacePositionOfCueTip, Vector3 cueballPosition);

        private void EnableCustomBallColorSlider(bool enabledState)
        {
            if (!ballCustomColours)
            {
                return;
            }

            if (blueTeamSliders)
            {
                blueTeamSliders._EnableDisable(enabledState);
            }

            if (orangeTeamSliders)
            {
                orangeTeamSliders._EnableDisable(enabledState);
            }
        }

        private void RemovePlayerFromGame(int playerID)
        {
            Networking.SetOwner(localPlayer, gameObject);

            switch (playerID)
            {
                case 0:
                    player1ID = 0;
                    break;
                case 1:
                    player2ID = 0;
                    break;
                case 2:
                    player3ID = 0;
                    break;
                case 3:
                    player4ID = 0;
                    break;
                default:
                    return;
            }

            RefreshNetworkData(false);
        }

        public void _IncreaseTimer()
        {
            if (logger)
            {
                logger._Log(name, "IncreaseTimer");
            }

            Networking.SetOwner(localPlayer, gameObject);
            timerSecondsPerShot += 5;
            RefreshNetworkData(false);

            if (timerSecondsPerShot >= 60)
            {
                timerSecondsPerShot = 60;
            }
        }

        public void _DecreaseTimer()
        {
            if (logger)
            {
                logger._Log(name, "DecreaseTimer");
            }

            Networking.SetOwner(localPlayer, gameObject);
            timerSecondsPerShot -= 5;

            if (timerSecondsPerShot <= 0)
            {
                timerSecondsPerShot = 0;
            }

            RefreshNetworkData(false);
        }

        public void _SelectTeams()
        {
            if (logger)
            {
                logger._Log(name, "SelectTeams");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isTeams = true;
            RefreshNetworkData(false);
        }

        public void _DeselectTeams()
        {
            if (logger)
            {
                logger._Log(name, "DeselectTeams");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isTeams = false;
            RefreshNetworkData(false);
        }

        public void _EnableGuideline()
        {
            if (logger)
            {
                logger._Log(name, "EnableGuideline");
            }

            Networking.SetOwner(localPlayer, gameObject);
            guideLineEnabled = true;
            RefreshNetworkData(false);
        }

        public void _DisableGuideline()
        {
            if (logger)
            {
                logger._Log(name, "DisableGuideline");
            }

            Networking.SetOwner(localPlayer, gameObject);
            guideLineEnabled = false;
            RefreshNetworkData(false);
        }


        /// <summary>
        /// Initialize new match as the host.
        /// </summary>
        public void _StartNewGame()
        {
            if (logger)
            {
                logger._Log(name, "StartNewGame");
            }

            mainSrc.enabled = true;

            if (!isGameInMenus)
            {
                return;
            }

            gameWasReset = false;
            gameID++;
            turnID = 0;
            isPlayerAllowedToPlay = true;

            isTeam2Turn = false;
            oldIsTeam2Turn = false;

            // Following is overrides of NewGameLocal, for game STARTER only
            turnIsRunning = false;
            isOpen = true;
            isGameInMenus = false;
            poolCues[0].tableIsActive = true;
            poolCues[1].tableIsActive = true;

            isTeam2Blue = false;
            isTeam2Winner = false;
            isFoul = false;
            isGameOver = false;

            ApplyTableColour(false);

            Networking.SetOwner(localPlayer, gameObject);

            RefreshNetworkData(false);
        }


        public void _Select8Ball()
        {
            if (logger)
            {
                logger._Log(name, "Select8Ball");
            }

            Networking.SetOwner(localPlayer, gameObject);

            gameMode = 0u;
            RefreshNetworkData(false);
        }

        public void _Select9Ball()
        {
            if (logger)
            {
                logger._Log(name, "Select9Ball");
            }

            Networking.SetOwner(localPlayer, gameObject);

            gameMode = 1u;
            RefreshNetworkData(false);
        }

        public void _Select4BallJapanese()
        {
            if (logger)
            {
                logger._Log(name, "Select4BallJapanese");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isKorean = false;
            isThreeCushionCarom = false;

            gameMode = 2u;
            RefreshNetworkData(false);
        }

        public void _SelectThreeCushionCarom()
        {
            if (logger)
            {
                logger._Log(name, "SelectThreeCushionCarom");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isKorean = false;
            isThreeCushionCarom = true;

            gameMode = 2u;
            RefreshNetworkData(false);
        }


        public void _Select4BallKorean()
        {
            if (logger)
            {
                logger._Log(name, "Select4BallKorean");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isKorean = true;
            isThreeCushionCarom = false;

            gameMode = 2u;
            RefreshNetworkData(false);
        }

        private void Initialize8Ball()
        {
            ballsArePocketed = new[]
            {
                false, false, false, false, false, false, false, false, false, false, false, false, false, false, false,
                false
            };

            for (int i = 0, k = 0; i < 5; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    currentBallPositions[rackOrder8Ball[k++]] = new Vector3
                    (
                        SPOT_POSITION_X + (i * BALL_PL_Y) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F),
                        0.0f,
                        ((-i + (j * 2)) * BALL_PL_X) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F)
                    );

                    currentBallVelocities[k] = Vector3.zero;
                    currentAngularVelocities[k] = Vector3.zero;
                }
            }

            TeamColors();
        }

        private void Initialize9Ball()
        {
            //ballPocketedState = 0xFC00u;
            ballsArePocketed = new[]
            {
                false, false, false, false, false, false, false, false, false, false, true, true, true, true, true, true
            };

            for (int i = 0, k = 0; i < 5; i++)
            {
                int rown = breakRows9ball[i];
                for (int j = 0; j <= rown; j++)
                {
                    currentBallPositions[rackOrder9Ball[k++]] = new Vector3
                    (
                        SPOT_POSITION_X + (i * BALL_PL_Y) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F),
                        0.0f,
                        ((-rown + (j * 2)) * BALL_PL_X) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F)
                    );

                    currentBallVelocities[k] = Vector3.zero;
                    currentAngularVelocities[k] = Vector3.zero;
                }
            }

            UsColors();
        }

        private Vector3 GetFourBallCueStartingPosition(bool team)
        {
            return team ? new Vector3(SPOT_CAROM_X, 0.0f, 0.0f) : new Vector3(-SPOT_CAROM_X, 0.0f, 0.0f);
        }

        private void Initialize4Ball()
        {
            //ballPocketedState = 0xFDF2u;
            ballsArePocketed = new[]
                {false, true, false, false, true, true, true, true, true, false, true, true, true, true, true, true};

            currentBallPositions[0] = new Vector3(-SPOT_CAROM_X, 0.0f, 0.0f);
            currentBallPositions[9] = new Vector3(SPOT_CAROM_X, 0.0f, 0.0f);
            currentBallPositions[2] = new Vector3(SPOT_POSITION_X, 0.0f, 0.0f);
            currentBallPositions[3] = new Vector3(-SPOT_POSITION_X, 0.0f, 0.0f);

            currentBallVelocities[0] = Vector3.zero;
            currentBallVelocities[9] = Vector3.zero;
            currentBallVelocities[2] = Vector3.zero;
            currentBallVelocities[3] = Vector3.zero;

            currentAngularVelocities[0] = Vector3.zero;
            currentAngularVelocities[9] = Vector3.zero;
            currentAngularVelocities[2] = Vector3.zero;
            currentAngularVelocities[3] = Vector3.zero;

            FourBallColors();
        }

        public void _StartHit()
        {
            if (logger)
            {
                logger._Log(name, "StartHit");
            }

            // lock aim variables
            bool isOurTurn = ((localPlayerID >= 0) && (playerIsTeam2 == isTeam2Turn)) || isGameModePractice;

            if (isOurTurn)
            {
                isArmed = true;
            }
        }

        public void _EndHit()
        {
            if (logger)
            {
                logger._Log(name, "EndHit");
            }

            isArmed = false;
        }

        public void PlaceBall()
        {
            if (logger)
            {
                logger._Log(name, "PlaceBall");
            }

            if (!IsCueContacting())
            {
                if (logger)
                {
                    logger._Log(name, "disabling marker because the ball hase been placed");
                }

                isRepositioningCueBall = false;

                if (marker)
                {
                    marker.SetActive(false);
                }

                isPlayerAllowedToPlay = true;
                isFoul = false;

                Networking.SetOwner(localPlayer, gameObject);

                // Save out position to remote clients
                RefreshNetworkData(isTeam2Turn);
            }
        }

        public void _ForceReset()
        {
            if (logger)
            {
                logger._Log(name, "ForceReset");
            }

            if (Networking.IsInstanceOwner)
            {
                Networking.SetOwner(localPlayer, gameObject);
                _Reset(ResetReason.InstanceOwnerReset);
            }
            else if (
                networkingLocalPlayerID == player1ID || networkingLocalPlayerID == player2ID ||
                networkingLocalPlayerID == player3ID || networkingLocalPlayerID == player4ID
            )
            {
                Networking.SetOwner(localPlayer, gameObject);
                _Reset(ResetReason.PlayerReset);
            }
            else if (
                (player1ID > 0 && !VRCPlayerApi.GetPlayerById(player1ID).IsValid()) ||
                (player2ID > 0 && !VRCPlayerApi.GetPlayerById(player2ID).IsValid()) ||
                (player3ID > 0 && !VRCPlayerApi.GetPlayerById(player3ID).IsValid()) ||
                (player4ID > 0 && !VRCPlayerApi.GetPlayerById(player4ID).IsValid())
            )
            {
                Networking.SetOwner(localPlayer, gameObject);
                _Reset(ResetReason.InvalidState);
            }
            else if (logger)
            {
                logger._Error(name, "Cannot reset table: you do not have permission");
            }
        }

        private void _Reset(ResetReason reason)
        {
            isGameInMenus = true;
            poolCues[0].tableIsActive = false;
            poolCues[1].tableIsActive = false;
            isPlayerAllowedToPlay = false;
            turnIsRunning = false;
            isTeam2Turn = false;
            gameWasReset = true;
            isGameOver = true;
            latestResetReason = reason;

            RefreshNetworkData(false);

            if (logger)
            {
                logger._Log(name, ToReasonString(reason));
            }
        }

        private void OnDesktopTopDownViewStart()
        {
            if (logger)
            {
                logger._Log(name, "OnDesktopTopDownViewStart");
            }

            isInDesktopTopDownView = true;
            isEnteringDesktopModeThisFrame = true;

            if (desktopBase)
            {
                desktopBase.SetActive(true);
                desktopCamera.enabled = true;
            }

            // Lock player in place
            localPlayer.Immobilize(true);

            poolCues[0]._EnteredFlatscreenPlayerCamera();
            poolCues[1]._EnteredFlatscreenPlayerCamera();

            poolMenu._EnteredFlatscreenPlayerCamera(desktopCamera.transform);
            pancakeUI._EnterDesktopTopDownView(desktopCamera);
        }

        public void _OnPutDownCueLocally()
        {
            if (logger)
            {
                logger._Log(name, "OnPutDownCueLocally");
            }

            OnDesktopTopDownViewExit();
        }

        protected bool fourBallCueLeftTable;

        // HandleEndOfTurn assess the table state at the end of the turn, based on what game is playing.
        protected void HandleEndOfTurn()
        {
            isSimulatedByUs = false;

            // We are updating the game state so make sure we are network owner
            Networking.SetOwner(Networking.LocalPlayer, gameObject);

            var isCorrectBallSunk = false;
            var isOpponentColourSunk = false;
            var winCondition = false;
            var foulCondition = ballsArePocketed[0];
            var deferLossCondition = false;
            var is8Sink = ballsArePocketed[1];
            var numberOfSunkBlues = 0;
            var numberOfSunkOranges = 0;

            if (is8Ball) // Standard 8 ball
            {
                var isBlue = !isOpen && (isTeam2Turn && isTeam2Blue || !isTeam2Turn && !isTeam2Blue);

                numberOfSunkBlues = GetNumberOfSunkBlues();
                numberOfSunkOranges = GetNumberOfSunkOranges();
                var isWrongHit = IsFirstEightBallHitFoul(firstHitBallThisTurn, numberOfSunkBlues, numberOfSunkOranges);

                // What balls got sunk this turn?
                for (var i = 2; i < NUMBER_OF_SIMULATED_BALLS; i++)
                {
                    if (ballsArePocketed[i] == oldBallsArePocketed[i])
                        continue;

                    if (isOpen)
                        isCorrectBallSunk = true;
                    else if (isBlue)
                    {
                        if (i > 1 && i < 9)
                            isCorrectBallSunk = true;
                        else
                            isOpponentColourSunk = true;
                    }
                    else
                    {
                        if (i >= 9)
                            isCorrectBallSunk = true;
                        else
                            isOpponentColourSunk = true;
                    }
                }

                winCondition = isBlue ? numberOfSunkBlues == 7 && is8Sink : numberOfSunkOranges == 7 && is8Sink;

                if (isWrongHit)
                {
                    if (logger)
                        logger._Log(name, "foul, wrong ball hit");

                    foulCondition = true;
                }
                else if (firstHitBallThisTurn == 0)
                {
                    if (logger)
                        logger._Log(name, "foul, no ball hit");

                    foulCondition = true;
                }

                deferLossCondition = is8Sink;
            }
            else if (isNineBall) // 9 ball
            {
                var isWrongHit = GetLowestNumberedBall(oldBallsArePocketed) != firstHitBallThisTurn;
                foulCondition = foulCondition || isWrongHit || firstHitBallThisTurn == 0;

                // Win condition: Pocket 9 ball ( at anytime )
                winCondition = ballsArePocketed[9];

                // this video is hard to follow so im just gonna guess this is right
                for (int i = 1; i < 10; i++)
                {
                    if (ballsArePocketed[i] != oldBallsArePocketed[i])
                    {
                        isCorrectBallSunk = true;
                    }
                }

                // TODO: Implement rail contact requirement
            }
            else if (isFourBall) // 4 ball
            {
                if (fourBallCueLeftTable)
                {
                    currentBallPositions[0] = GetFourBallCueStartingPosition(isTeam2Turn);
                    currentBallVelocities[0] = Vector3.zero;
                    currentAngularVelocities[0] = Vector3.zero;

                    // Best effort attempt to place the ball somewhere
                    if (IsCueContacting())
                    {
                        currentBallPositions[0] = GetFourBallCueStartingPosition(!isTeam2Turn);

                        if (IsCueContacting())
                        {
                            currentBallPositions[0] = Vector3.zero;

                            // Let the player fix it
                            isFoul = true;
                        }
                    }

                    fourBallCueLeftTable = false;
                }

                isCorrectBallSunk = isMadePoint;
                isOpponentColourSunk = isMadeFoul;
                winCondition = scores[Convert.ToInt32(isTeam2Turn)] >= scoreNeededToWinCarom;
            }
            else
                return;

            if (winCondition)
            {
                if (foulCondition)
                    OnTurnOverGameWon(!isTeam2Turn, true);
                else
                    OnTurnOverGameWon(isTeam2Turn, false);
            }
            else if (deferLossCondition)
                OnTurnOverGameWon(!isTeam2Turn, true);
            else if (foulCondition)
                OnTurnOverFoul();
            else if (isCorrectBallSunk && !isOpponentColourSunk)
            {
                if (is8Ball && isOpen)
                {
                    if (numberOfSunkBlues != numberOfSunkOranges)
                    {
                        isTeam2Blue = (numberOfSunkBlues > numberOfSunkOranges) ? isTeam2Turn : !isTeam2Turn;

                        isOpen = false;
                        ApplyTableColour(isTeam2Turn);
                    }
                }

                isPlayerAllowedToPlay = true;

                RefreshNetworkData(isTeam2Turn);
            }
            else
            {
                isPlayerAllowedToPlay = true;

                if (isFourBall)
                {
                    Vector3 temp = currentBallPositions[0];
                    currentBallPositions[0] = currentBallPositions[9];
                    currentBallPositions[9] = temp;
                }

                turnID++;

                RefreshNetworkData(!isTeam2Turn);
            }
        }

        private int GetNumberOfSunkOranges()
        {
            var num = 0;

            for (var i = 9; i < 16; i++)
            {
                if (ballsArePocketed[i])
                {
                    num++;
                }
            }

            return num;
        }

        private int GetNumberOfSunkBlues()
        {
            var num = 0;

            for (var i = 2; i < 9; i++)
            {
                if (ballsArePocketed[i])
                {
                    num++;
                }
            }

            return num;
        }

        private void RefreshNetworkData(bool newIsTeam2Playing)
        {
            if (logger)
            {
                logger._Log(name, "RefreshNetworkData");
            }

            isTeam2Turn = newIsTeam2Playing;

            Networking.SetOwner(localPlayer, gameObject);
            RequestSerialization();
            ReadNetworkData();
        }

        protected void ReadNetworkData()
        {
            if (logger)
            {
                logger._Log(name, "ReadNetworkData");
            }

            UpdateScores();

            // Assume the marker is off - one of the On... functions might turn it back on again
            if (marker)
            {
                marker.SetActive(false);
            }

            if (gameID > oldGameID && !isGameInMenus)
                OnNewGameStarted();
            else if (isTeam2Turn != oldIsTeam2Turn)
                OnRemoteTurnChange();
            else if (!oldIsGameInMenus && isGameInMenus)
                OnRemoteGameOver();

            if (oldOpen && !isOpen)
                ApplyTableColour(isTeam2Turn);

            CopyGameStateToOldState();

            if (isTableLocked)
                poolMenu._EnableUnlockTableButton();
            else if (!isGameInMenus)
                poolMenu._EnableResetButton();
            else
                poolMenu._EnableMainMenu();

            poolMenu._UpdateMainMenuView(
                isTeams,
                isTeam2Turn,
                (int) gameMode,
                isKorean,
                isThreeCushionCarom,
                (int) timerSecondsPerShot,
                player1ID,
                player2ID,
                player3ID,
                player4ID,
                guideLineEnabled
            );

            if (isGameInMenus)
            {
                var numberOfPlayers = 0;

                if (!isTableLocked)
                {
                    if (player1ID != 0)
                    {
                        numberOfPlayers++;
                    }

                    if (player2ID != 0)
                    {
                        numberOfPlayers++;
                    }

                    if (player3ID != 0)
                    {
                        numberOfPlayers++;
                    }

                    if (player4ID != 0)
                    {
                        numberOfPlayers++;
                    }
                }

                isGameModePractice = localPlayerID == 0 && numberOfPlayers == 1;

                hasRunSyncOnce = true;

                return;
            }

            if (isFourBall)
            {
                ballsArePocketed = new[]
                {
                    false, true, false, false, true, true, true, true, true, false, true, true, true, true, true, true
                };
            }

            // Check this every read
            // Its basically 'turn start' event
            if (isPlayerAllowedToPlay)
            {
                OnLocalTurnStart();
            }
            else
            {
                OnRemoteTurnStart();
            }

            // Start of turn so we've hit nothing
            firstHitBallThisTurn = 0;
            secondBallHitThisTurn = 0;
            thirdBallHitThisTurn = 0;
            cushionsHitThisTurn = 0;

            hasRunSyncOnce = true;
        }

        private void OnRemoteTurnStart()
        {
            if (marker9ball)
            {
                marker9ball.SetActive(false);
            }

            isTimerRunning = false;
            isMadePoint = false;
            isMadeFoul = false;

            if (devhit)
            {
                devhit.SetActive(false);
            }

            if (guideline)
            {
                guideline.gameObject.SetActive(false);
            }
        }

        private void OnLocalTurnStart()
        {
            if (((localPlayerID >= 0) && (playerIsTeam2 == isTeam2Turn)) || isGameModePractice)
            {
                // Update for desktop
                isDesktopLocalTurn = true;

                // Reset hit point
                desktopHitPoint = Vector3.zero;
            }
            else
            {
                isDesktopLocalTurn = false;
            }

            if (isNineBall)
            {
                int target = GetLowestNumberedBall(ballsArePocketed);

                if (marker9ball)
                {
                    marker9ball.SetActive(true);
                    marker9ball.transform.localPosition = currentBallPositions[target];
                }

                ApplyTableColour(isTeam2Turn);
            }

            if (!tableModelHasRails || !hasRunSyncOnce)
            {
                PlaceSunkBallsIntoRestingPlace();
            }

            if (timerSecondsPerShot > 0 && !isTimerRunning)
            {
                ResetTimer();
            }

            // sanitize old cue tip location data to prevent stale data from causing unintended effects.
            localSpacePositionOfCueTipLastFrame =
                tableSurface.transform.InverseTransformPoint(cueTip.transform.position);
        }

        private void OnNewGameStarted()
        {
            OnRemoteNewGame();

            if (((localPlayerID >= 0) && (playerIsTeam2 == isTeam2Turn)) || isGameModePractice)
            {
                if (logger)
                {
                    logger._Log(name, "enabling marker because it is the start of the game and we are breaking");
                }

                isRepositioningCueBall = true;
                repoMaxX = -SPOT_POSITION_X;
                ballRigidbodies[0].isKinematic = true;

                if (marker)
                {
                    markerTransform.localPosition = currentBallPositions[0];
                    if (!isFourBall)
                    {
                        marker.SetActive(true);
                    }

                    ((VRC_Pickup) marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = true;
                }
            }
            else
            {
                markerTransform.localPosition = currentBallPositions[0];

                if (!isFourBall)
                    marker.SetActive(true);

                ((VRC_Pickup) marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = false;
            }
        }


        public void OnDisable()
        {
            // Disabling the table means we're in an identical state to a late-joiner - the table will have progressed
            // since we last ran a sync. Ergo, we should tell the table that it needs to handle things as if the player
            // is a late-joiner.
            hasRunSyncOnce = false;
        }

        private void TeamColors()
        {
            ballColors[0] = Color.white;
            ballColors[1] = Color.black;
            for (int i = 2; i < 9; i++)
            {
                ballColors[i] = tableBlue;
            }

            for (int i = 9; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                ballColors[i] = tableOrange;
            }
        }

        private void UsColors()
        {
            // Set colors
            ballColors[0] = Color.white;
            ballColors[1] = Color.black;
            ballColors[2] = Color.yellow;
            ballColors[3] = Color.blue;
            ballColors[4] = Color.red;
            ballColors[5] = Color.magenta;
            ballColors[6] = new Color(1, 0.6f, 0, 1);
            ballColors[7] = Color.green;
            ballColors[8] = new Color(0.59f, 0.29f, 0, 1);
            ballColors[9] = Color.yellow;
            ballColors[10] = Color.blue;
            ballColors[11] = Color.red;
            ballColors[12] = Color.magenta;
            ballColors[13] = new Color(1, 0.6f, 0, 1);
            ballColors[14] = Color.green;
            ballColors[15] = new Color(0.59f, 0.29f, 0, 1);
        }

        private void FourBallColors() // will be used later. Doesnt do much yet
        {
            ballColors[0] = Color.white;
            ballColors[9] = Color.yellow;
            ballColors[2] = Color.red;
            ballColors[3] = Color.red;
        }

        /// <summary>
        /// Updates table colour target to appropriate player colour
        /// </summary>
        private void ApplyTableColour(bool isTeam2Color)
        {
            if (logger)
            {
                logger._Log(name, "ApplyTableColour");
            }

            if (isFourBall)
            {
                if (!this.isTeam2Turn)
                {
                    tableSrcColour = pointerColour0;
                    cueRenderObjs[0].materials[0].SetColor(uniformCueColour, pointerColour0);
                    cueRenderObjs[1].materials[0].SetColor(uniformCueColour, pointerColour1 * 0.5f);
                }
                else
                {
                    tableSrcColour = pointerColour1;
                    cueRenderObjs[0].materials[0].SetColor(uniformCueColour, pointerColour0 * 0.5f);
                    cueRenderObjs[1].materials[0].SetColor(uniformCueColour, pointerColour1);
                }
            }
            else if (isNineBall)
            {
                int target = GetLowestNumberedBall(ballsArePocketed);
                Color color = ballColors[target];
                cueRenderObjs[Convert.ToInt32(isTeam2Color)].materials[0].SetColor(uniformCueColour, color);
                cueRenderObjs[Convert.ToInt32(!isTeam2Color)].materials[0].SetColor(uniformCueColour, tableBlack);

                tableSrcColour = color;
            }
            else if (!isOpen)
            {
                if (isTeam2Color)
                {
                    if (isTeam2Blue)
                    {
                        tableSrcColour = tableBlue;
                        cueRenderObjs[0].material.SetColor(uniformCueColour, tableOrange * 0.33f);
                        cueRenderObjs[1].material.SetColor(uniformCueColour, tableBlue);
                    }
                    else
                    {
                        tableSrcColour = tableOrange;
                        cueRenderObjs[0].material.SetColor(uniformCueColour, tableBlue * 0.33f);
                        cueRenderObjs[1].material.SetColor(uniformCueColour, tableOrange);
                    }
                }
                else
                {
                    if (isTeam2Blue)
                    {
                        tableSrcColour = tableOrange;
                        cueRenderObjs[0].material.SetColor(uniformCueColour, tableOrange);
                        cueRenderObjs[1].material.SetColor(uniformCueColour, tableBlue * 0.33f);
                    }
                    else
                    {
                        tableSrcColour = tableBlue;
                        cueRenderObjs[0].material.SetColor(uniformCueColour, tableBlue);
                        cueRenderObjs[1].material.SetColor(uniformCueColour, tableOrange * 0.33f);
                    }
                }
            }
            else
            {
                tableSrcColour = pointerColour2;

                cueRenderObjs[Convert.ToInt32(this.isTeam2Turn)].materials[0].SetColor(uniformCueColour, tableWhite);
                cueRenderObjs[Convert.ToInt32(!this.isTeam2Turn)].materials[0].SetColor(uniformCueColour, tableBlack);
            }

            cueGrips[Convert.ToInt32(this.isTeam2Turn)].SetColor(uniformMarkerColour, gripColourActive);
            cueGrips[Convert.ToInt32(!this.isTeam2Turn)].SetColor(uniformMarkerColour, gripColourInactive);

            if (!ballCustomColours)
            {
                return;
            }

            if (isFourBall || isNineBall)
            {
                foreach (MeshRenderer meshRenderer in ballRenderers)
                {
                    meshRenderer.material.SetFloat(ballMaskToggle, 1);
                }
            }
            else
            {
                for (var i = 2; i < ballTransforms.Length; i++)
                {
                    if (i < 9) //9 is where it switches to stripes
                    {
                        ballRenderers[i].material.SetFloat(ballMaskToggle, 0);
                        ballRenderers[i].material.SetColor(uniformBallColour, tableBlue);
                        ballRenderers[i].material.SetFloat(uniformBallFloat, shaderToggleFloat);
                    }
                    else
                    {
                        ballRenderers[i].material.SetFloat(ballMaskToggle, 0);
                        ballRenderers[i].material.SetColor(uniformBallColour, tableOrange);
                        ballRenderers[i].material.SetFloat(uniformBallFloat, shaderToggleFloat);
                    }
                }
            }
        }

        private void SpawnPlusOne(Transform ball)
        {
            if (logger)
            {
                logger._Log(name, "SpawnPlusOne");
            }

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            emitParams.position = ball.position;
            plusOneParticleSystem.Emit(emitParams, 1);
        }

        private void SpawnMinusOne(Transform ball)
        {
            if (logger)
            {
                logger._Log(name, "SpawnMinusOne");
            }

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            emitParams.position = ball.position;
            minusOneParticleSystem.Emit(emitParams, 1);
        }

        private void ResetTimer()
        {
            if (logger)
            {
                logger._Log(name, "ResetTimer");
            }

            if (timerSecondsPerShot > 0)
            {
                remainingTime = timerSecondsPerShot;
                isTimerRunning = true;
            }
            else
            {
                isTimerRunning = false;
            }
        }

        private void OnLocalCaromPoint(Transform ball)
        {
            isMadePoint = true;
            mainSrc.PlayOneShot(pointMadeSfx, 1.0f);

            scores[Convert.ToUInt32(isTeam2Turn)]++;

            if (scores[Convert.ToUInt32(isTeam2Turn)] > 10)
            {
                scores[Convert.ToUInt32(isTeam2Turn)] = 10;
            }

            SpawnPlusOne(ball);
        }

        /// <summary>
        /// End of the game. Both with/loss
        /// </summary>
        private void OnRemoteGameOver()
        {
            if (logger)
            {
                logger._Log(name, "OnRemoteGameOver");
            }

            ApplyTableColour(isTeam2Winner);

            if (gameWasReset)
            {
                poolMenu._GameWasReset(latestResetReason);

                foreach (var cue in poolCues)
                {
                    cue._Respawn(true);
                }

                if (slogger)
                {
                    slogger.OscReportGameReset(latestResetReason);
                }
            }
            else
            {
                poolMenu._TeamWins(isTeam2Winner);
                PlayAudioClip(winnerSfx);
            }

            if (marker9ball)
            {
                marker9ball.SetActive(false);
            }

            if (!tableModelHasRails || !hasRunSyncOnce)
            {
                PlaceSunkBallsIntoRestingPlace();
            }

            if (logger)
            {
                logger._Log(name, "disabling marker because the game is over");
            }

            isRepositioningCueBall = false;

            if (marker)
            {
                marker.SetActive(false);
            }

            // Remove any access rights
            localPlayerID = -1;
            GrantCueAccess();

            player1ID = 0;
            player2ID = 0;
            player3ID = 0;
            player4ID = 0;

            poolMenu._UpdateMainMenuView(
                isTeams,
                isTeam2Turn,
                (int) gameMode,
                isKorean,
                isThreeCushionCarom,
                timerSecondsPerShot,
                player1ID,
                player2ID,
                player3ID,
                player4ID,
                guideLineEnabled
            );

            poolMenu._EnableMainMenu();

            foreach (var cue in poolCues)
            {
                cue._Respawn(true);
            }

            EnableCustomBallColorSlider(false);
        }

        private void OnRemoteTurnChange()
        {
            if (logger)
                logger._Log(name, "OnRemoteTurnChange");

            // Effects
            ApplyTableColour(isTeam2Turn);
            mainSrc.PlayOneShot(newTurnSfx, 1.0f);
            hasFoulBeenPlayedThisTurn = false;

            // Register correct cuetip
            cueTip = cueTips[Convert.ToUInt32(isTeam2Turn)];

            if (isFourBall) // 4 ball
            {
                if (!isTeam2Turn)
                {
                    if (logger)
                        logger._Log(name, "0 ball is 0 mesh, 9 ball is 1 mesh");

                    ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[0];
                    ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[1];
                }
                else
                {
                    if (logger)
                        logger._Log(name, "0 ball is 0 mesh, 9 ball is 1 mesh");

                    ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[0];
                    ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[1];
                }
            }
            else
            {
                // White was pocketed
                if (ballsArePocketed[0])
                {
                    currentBallPositions[0] = Vector3.zero;
                    currentBallVelocities[0] = Vector3.zero;
                    currentAngularVelocities[0] = Vector3.zero;

                    ballsArePocketed[0] = false;
                }
            }

            if (isFoul && marker)
            {
                marker.SetActive(true);
                markerTransform.localPosition = currentBallPositions[0];
                ((VRC_Pickup) marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = false;
                isRepositioningCueBall = true;
                ballRigidbodies[0].isKinematic = true;
                repoMaxX = tableWidth;
                
                if (logger)
                    logger._Log(name, "Enabling marker - there was a foul last turn");
                
                if (localPlayerID >= 0 && (playerIsTeam2 == isTeam2Turn || isGameModePractice))
                {
                    ((VRC_Pickup) marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = true;
                        
                    // Cope with a marker that might not be synced
                    VRCObjectSync comp = marker.GetComponent<VRCObjectSync>();
                    if (Utilities.IsValid(comp) && Networking.IsOwner(gameObject))
                    {
                        Networking.SetOwner(Networking.LocalPlayer, marker);
                        // Move the marker to where the cue ball is, and then teleport it there. This tells VRC to
                        // move the marker remotely, avoiding weird network sync problems.
                        // comp.TeleportTo(marker.transform);
                    }
                }
            }

            // Force timer reset
            if (timerSecondsPerShot > 0)
                ResetTimer();
        }

        /// <summary>
        /// Grant cue access if we are playing
        /// </summary>
        private void GrantCueAccess()
        {
            if (logger)
            {
                logger._Log(name, "GrantCueAccess");
            }

            if (localPlayerID > -1)
            {
                if (isGameModePractice)
                {
                    poolCues[0]._AllowAccess();
                    poolCues[1]._AllowAccess();
                }
                else if (!playerIsTeam2) // Local player is 1, or 3
                {
                    poolCues[0]._AllowAccess();
                    poolCues[1]._DenyAccess();
                }
                else // Local player is 0, or 2
                {
                    poolCues[1]._AllowAccess();
                    poolCues[0]._DenyAccess();
                }
            }
            else
            {
                poolCues[0]._DenyAccess();
                poolCues[1]._DenyAccess();
            }
        }

        // The generic new game setup that runs for everyone.
        private void OnRemoteNewGame()
        {
            if (logger)
            {
                logger._Log(name, "OnRemoteNewGame");
            }

            poolMenu._EnableResetButton();

            is8Ball = gameMode == 0u;
            isNineBall = gameMode == 1u;
            isFourBall = gameMode == 2u;

            // Cue ball
            currentBallPositions[0] = new Vector3(-SPOT_POSITION_X, 0.0f, 0.0f);
            currentBallVelocities[0] = Vector3.zero;

            // Start at spot

            if (isNineBall) // 9 ball
            {
                Initialize9Ball();
            }
            else if (isFourBall) // 4 ball
            {
                Initialize4Ball();
            }
            else // Normal 8 ball modes
            {
                Initialize8Ball();
            }

            if (localPlayerID >= 0)
            {
                playerIsTeam2 = localPlayerID % 2 == 1;
            }

            if (isNineBall) // 9 Ball / USA colours
            {
                pointerColour0 = tableLightBlue;
                pointerColour1 = tableLightBlue;
                pointerColour2 = tableLightBlue;

                pointerColourErr = tableBlack; // No error effect
                pointerClothColour = fabricBlue;

                // 9 ball only uses one colourset / cloth colour

                foreach (MeshRenderer meshRenderer in ballRenderers)
                {
                    meshRenderer.material.SetTexture(MainTex, sets[3]);
                }
            }
            else if (isFourBall)
            {
                pointerColour0 = tableWhite;
                pointerColour1 = tableYellow;

                // Should not be used
                pointerColour2 = tableRed;
                pointerColourErr = tableRed;

                foreach (MeshRenderer meshRenderer in ballRenderers)
                {
                    meshRenderer.material.SetTexture(MainTex, sets[2]);
                }

                pointerClothColour = fabricGreen;
            }
            else // Standard 8 ball derivatives
            {
                pointerColourErr = tableRed;
                pointerColour2 = tableWhite;

                pointerColour0 = tableBlue;
                pointerColour1 = tableOrange;

                foreach (MeshRenderer meshRenderer in ballRenderers)
                {
                    meshRenderer.material.SetTexture(MainTex, sets[0]);
                }

                pointerClothColour = fabricGray;
            }

            tableRenderer.material.SetColor(ClothColour, pointerClothColour);

            if (tableReflection)
            {
                tableReflection.RenderProbe();
            }

            ApplyTableColour(false);
            GrantCueAccess();

            if (isNineBall) // 9 ball specific
            {
                if (marker9ball)
                {
                    marker9ball.SetActive(true);
                }
            }
            else
            {
                if (marker9ball)
                {
                    marker9ball.SetActive(false);
                }
            }

            if (isFourBall) // 4 ball specific
            {
                if (pocketBlockers)
                {
                    pocketBlockers.SetActive(true);
                }

                scores[0] = 0;
                scores[1] = 0;

                // Reset mesh filters on balls that change them
                ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[0];
                ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[1];
            }
            else
            {
                if (pocketBlockers)
                {
                    pocketBlockers.SetActive(false);
                }

                // Reset mesh filters on balls that change them
                ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[0];
                ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = nineBall;
            }

            if (isNineBall)
            {
                for (int i = 0; i <= 9; i++)
                {
                    if (ballTransforms[i])
                    {
                        ballTransforms[i].gameObject.SetActive(true);
                    }

                    if (ballShadowPosConstraints[i])
                    {
                        ballShadowPosConstraints[i].gameObject.SetActive(true);
                    }
                }

                for (int i = 10; i < NUMBER_OF_SIMULATED_BALLS; i++)
                {
                    if (ballTransforms[i])
                    {
                        ballTransforms[i].gameObject.SetActive(false);
                    }

                    if (ballShadowPosConstraints[i])
                    {
                        ballShadowPosConstraints[i].gameObject.SetActive(false);
                    }
                }
            }
            else if (isFourBall)
            {
                for (int i = 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
                {
                    if (ballTransforms[i])
                    {
                        ballTransforms[i].gameObject.SetActive(false);
                    }

                    if (ballShadowPosConstraints[i])
                    {
                        ballShadowPosConstraints[i].gameObject.SetActive(false);
                    }
                }

                if (ballTransforms[0])
                {
                    ballTransforms[0].gameObject.SetActive(true);
                }

                if (ballTransforms[2])
                {
                    ballTransforms[2].gameObject.SetActive(true);
                }

                if (ballTransforms[3])
                {
                    ballTransforms[3].gameObject.SetActive(true);
                }

                if (ballTransforms[9])
                {
                    ballTransforms[9].gameObject.SetActive(true);
                }

                if (ballShadowPosConstraints[0])
                {
                    ballShadowPosConstraints[0].gameObject.SetActive(true);
                }

                if (ballShadowPosConstraints[2])
                {
                    ballShadowPosConstraints[2].gameObject.SetActive(true);
                }

                if (ballShadowPosConstraints[3])
                {
                    ballShadowPosConstraints[3].gameObject.SetActive(true);
                }

                if (ballShadowPosConstraints[9])
                {
                    ballShadowPosConstraints[9].gameObject.SetActive(true);
                }
            }
            else
            {
                for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
                {
                    if (ballTransforms[i])
                    {
                        ballTransforms[i].gameObject.SetActive(true);
                    }

                    if (ballShadowPosConstraints[i])
                    {
                        ballShadowPosConstraints[i].gameObject.SetActive(true);
                    }
                }
            }

            // Effects - don't run this if this is the first network sync, as we may be catching up.
            if (hasRunSyncOnce)
            {
                introAnimTimer = introAnimationLength;
                if (introAnimTimer > 0.0f)
                {
                    mainSrc.PlayOneShot(introSfx, 1.0f);
                    SendCustomEventDelayedSeconds(nameof(_ReEnableShadowConstraints), introAnimationLength);
                }
            }

            isTimerRunning = false;

            poolCues[0]._LeftFlatscreenPlayerCamera();
            poolCues[1]._LeftFlatscreenPlayerCamera();

            // Make sure that we run a pass on rigidbodies to ensure they are off.
            PlaceSunkBallsIntoRestingPlace();

            OnRemoteTurnChange();

            if (slogger)
            {
                slogger.OscReportGameStarted(localPlayerID >= 0);
            }
        }

        /// <summary>
        /// Call at the start of each turn.
        /// Place sunk inert balls into a specific storage location.
        /// Sunk ball state is replicated, so this is network-stable.
        /// </summary>
        private void PlaceSunkBallsIntoRestingPlace()
        {
            if (logger)
            {
                logger._Log(name, "PlaceSunkBallsIntoRestingPlace");
            }

            var numberOfSunkBalls = 0;
            Vector3 localPosition = sunkBallsPositionRoot.localPosition;
            var posX = localPosition.x;
            var posY = localPosition.y;
            var posZ = localPosition.z;

            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                ballTransforms[i].GetComponent<Rigidbody>().isKinematic = true;

                if (ballsArePocketed[i])
                {
                    ballTransforms[i].localPosition = new Vector3(posX + numberOfSunkBalls * ballDiameter, posY, posZ);
                    numberOfSunkBalls++;
                }
            }
        }

        private int GetLowestNumberedBall(bool[] balls)
        {
            // order in ballsArePocketed is assumed to be [c812345679]
            for (int i = 2; i < 9; i++)
            {
                if (!balls[i])
                {
                    return i;
                }
            }

            if (!balls[1])
            {
                return 1;
            }
            else if (!balls[9])
            {
                return 9;
            }

            return 0;
        }

        private void OnTurnOverGameWon(bool newIsTeam2Winner, bool causedByFoul)
        {
            if (logger)
                logger._Log(name, "OnTurnOverGameWon");

            isGameInMenus = true;
            poolCues[0].tableIsActive = false;
            poolCues[1].tableIsActive = false;

            isTeam2Winner = newIsTeam2Winner;
            isFoul = causedByFoul;
            isGameOver = true;

            RefreshNetworkData(isTeam2Turn);
        }

        private void OnTurnOverFoul()
        {
            if (logger)
            {
                logger._Log(name, "OnTurnOverFoul");
            }

            isFoul = true;
            isPlayerAllowedToPlay = true;
            turnID++;

            RefreshNetworkData(!isTeam2Turn);
        }

        /// <summary>
        /// Copy current values to previous values
        /// </summary>
        private void CopyGameStateToOldState()
        {
            oldOpen = isOpen;
            oldIsGameInMenus = isGameInMenus;
            oldGameID = gameID;
            oldIsTeam2Turn = isTeam2Turn;

            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                oldBallsArePocketed[i] = ballsArePocketed[i];
            }
        }

        public void _ToggleDesktopUITopDownView()
        {
            if (isInDesktopTopDownView)
            {
                OnDesktopTopDownViewExit();
            }
            else if (canEnterDesktopTopDownView)
            {
                OnDesktopTopDownViewStart();
            }

            Debug.Log("amogus");
        }

        private void OnDesktopTopDownViewExit()
        {
            if (logger)
            {
                logger._Log(name, "OnDesktopTopDownViewExit");
            }

            isInDesktopTopDownView = false;

            if (desktopBase)
            {
                desktopBase.SetActive(false);

                if (desktopCamera == null)
                {
                    desktopCamera = desktopBase.GetComponentInChildren<Camera>();
                }

                desktopCamera.enabled = false;
            }

            poolCues[0]._LeftFlatscreenPlayerCamera();
            poolCues[1]._LeftFlatscreenPlayerCamera();

            Networking.LocalPlayer.Immobilize(false);

            if (isGameModePractice)
            {
                foreach (var cue in poolCues)
                {
                    cue._Respawn(false);
                }
            }

            poolMenu._LeftFlatscreenPlayerCamera();
            pancakeUI._ExitDesktopTopDownView();
        }

        protected abstract void HitBallWithCue(Vector3 shotDirection, float velocity);

        protected void HandleCueBallHit()
        {
            if (logger)
            {
                logger._Log(name, "disabling marker because the ball hase been hit");
            }

            isRepositioningCueBall = false;

            if (marker)
            {
                marker.SetActive(false);
            }

            if (devhit)
            {
                devhit.SetActive(false);
            }

            if (guideline)
            {
                guideline.gameObject.SetActive(false);
            }

            // Remove locks
            _EndHit();
            isPlayerAllowedToPlay = false;
            isFoul = false; // In case did not drop foul marker

            float vol = Mathf.Clamp(currentBallVelocities[0].magnitude * 0.1f, 0f, 1f);

            cueTipSrc.transform.SetPositionAndRotation(cueTip.transform.position, new Quaternion());
            cueTipSrc.PlayOneShot(hitBallSfx, vol);

            // Commit changes
            turnIsRunning = true;

            // Make sure we are the network owner
            Networking.SetOwner(localPlayer, gameObject);

            RefreshNetworkData(isTeam2Turn);

            isSimulatedByUs = true;
        }

        // TODO: Yes this should be at the top of the file but I've not worked on this code for 24 months and am lazy.
        private Vector3 desktopCameraInitialPosition;
        private Quaternion desktopCameraInitialRotation;
        private float lastLookHorizontal;
        private float lastLookVertical;
        private bool lastInputUseDown;
        private float inputHeldDownTime;
        private float desktopShootForce;
        private const float desktopShotPowerMult = 0.15f;
        public GameObject powerBar;
        public GameObject topBar;
        public Vector3 initialPowerBarPos;

        public override void InputUse(bool value, VRC.Udon.Common.UdonInputEventArgs args)
        {
            if (!isInDesktopTopDownView)
            {
                if (canEnterDesktopTopDownView)
                {
                    OnDesktopTopDownViewStart();
                }

                return;
            }

            lastInputUseDown = value;
        }

        public override void InputLookHorizontal(float value, VRC.Udon.Common.UdonInputEventArgs args)
        {
            if (!isInDesktopTopDownView)
            {
                return;
            }

            lastLookHorizontal = value;
        }

        public override void InputLookVertical(float value, VRC.Udon.Common.UdonInputEventArgs args)
        {
            if (!isInDesktopTopDownView)
            {
                return;
            }

            lastLookVertical = value;
        }

        // TODO: Single use function, but it short-circuits so cannot be easily put into its using function.
        private void HandleUpdatingDesktopViewUI()
        {
#if UNITY_ANDROID
                lastLookHorizontal *= 3;
                lastLookVertical *= 3;
#endif

            if (isEnteringDesktopModeThisFrame)
            {
                isEnteringDesktopModeThisFrame = false;

                return;
            }

            if (Input.GetKey(KeyCode.W))
                desktopHitPoint += Vector3.forward * Time.deltaTime;

            if (Input.GetKey(KeyCode.S))
                desktopHitPoint += Vector3.back * Time.deltaTime;

            if (Input.GetKey(KeyCode.A))
                desktopHitPoint += Vector3.left * Time.deltaTime;

            if (Input.GetKey(KeyCode.D))
                desktopHitPoint += Vector3.right * Time.deltaTime;

            if (Input.GetKey(KeyCode.UpArrow))
                lastLookVertical =
                    desktopAngleIncrement *
                    Time.deltaTime;

            if (Input.GetKey(KeyCode.DownArrow))
                lastLookVertical =
                    -desktopAngleIncrement *
                    Time.deltaTime;

            if (Input.GetKey(KeyCode.LeftArrow))
                lastLookHorizontal = -0.25f;

            if (Input.GetKey(KeyCode.RightArrow))
                lastLookHorizontal = 0.25f;

            var dir = desktopAimPoint - currentBallPositions[0];
            dir = Quaternion.Euler(new Vector3(0f, lastLookHorizontal, 0f)) * dir;
            desktopAimPoint = dir + currentBallPositions[0];

            if (!isDesktopLocalTurn)
            {
                desktopCamera.transform.SetLocalPositionAndRotation(desktopCameraInitialPosition,
                    desktopCameraInitialRotation);
            }
            else
            {
                Vector3 ncursor = desktopAimPoint;
                ncursor.y = 0.0f;
                Vector3 delta = ncursor - currentBallPositions[0];
                newDesktopCue = Convert.ToUInt32(isTeam2Turn);
                GameObject cue = desktopCueParents[newDesktopCue];

                if (isGameModePractice && newDesktopCue != oldDesktopCue)
                {
                    poolCues[oldDesktopCue]._Respawn(false);
                    oldDesktopCue = newDesktopCue;
                }

                if (lastInputUseDown)
                {
                    inputHeldDownTime = Mathf.Clamp(inputHeldDownTime, 0.0f, 0.5f);

                    if (!isDesktopShootingIn)
                    {
                        isDesktopShootingIn = true;

                        // Create shooting vector
                        desktopShootVector = delta.normalized;

                        // Create copy of cursor for later
                        desktopSafeRemovePoint = desktopAimPoint;

                        // Unlock cursor position from table
                        desktopClampX = Mathf.Infinity;
                        desktopClampY = Mathf.Infinity;
                    }

                    // Calculate shoot amount;
                    desktopShootForce += Time.deltaTime * desktopShotPowerMult;
                    isDesktopSafeRemove = desktopShootForce < 0.0f;
                    desktopShootForce = Mathf.Clamp(desktopShootForce, 0.0f, 0.5f);

                    // Set delta back to dkShootVector
                    delta = desktopShootVector;
                }
                else
                {
                    inputHeldDownTime = 0;

                    // Trigger shot
                    if (isDesktopShootingIn)
                    {
                        // Shot cancel
                        if (!isDesktopSafeRemove)
                        {
                            isDesktopLocalTurn = false;
                            HitBallWithCue(cueTip.transform.forward, Mathf.Pow(desktopShootForce * 2.0f, 1.4f) * 7.0f);
                        }

                        // Restore cursor position
                        desktopAimPoint = desktopSafeRemovePoint;
                        desktopClampX = tableWidth;
                        desktopClampY = tableHeight;

                        // 1-frame override to fix rotation
                        delta = desktopShootVector;
                    }

                    isDesktopShootingIn = false;
                    desktopShootForce = 0.0f;
                }

                desktopAngle = Mathf.Clamp(desktopAngle + lastLookVertical, MIN_DESKTOP_CUE_ANGLE,
                    MAX_DESKTOP_CUE_ANGLE);

                if (tiltAmount)
                    tiltAmount.fillAmount =
                        Mathf.InverseLerp(MIN_DESKTOP_CUE_ANGLE, MAX_DESKTOP_CUE_ANGLE, desktopAngle);

                // Clamp in circle
                if (desktopHitPoint.magnitude > 0.90f)
                    desktopHitPoint = desktopHitPoint.normalized * 0.9f;

                desktopHitPosition.transform.localPosition = desktopHitPoint;

                // Create rotation
                Quaternion xr = Quaternion.AngleAxis(desktopAngle, Vector3.right);
                Quaternion r = Quaternion.AngleAxis(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, Vector3.up);

                Vector3 worldHit = new Vector3(desktopHitPoint.x * BALL_PL_X, desktopHitPoint.z * BALL_PL_X,
                    -0.89f - desktopShootForce);

                cue.transform.localRotation = r * xr;
                cue.transform.position =
                    tableSurface.transform.TransformPoint(currentBallPositions[0] + (r * xr * worldHit));

                desktopCamera.transform.position = cue.transform.position;
                desktopCamera.transform.localRotation = cue.transform.localRotation;
            }

            Vector3 powerBarPos = powerBar.transform.localPosition;
            powerBarPos.x = Mathf.Lerp(initialPowerBarPos.x, topBar.transform.localPosition.x,
                desktopShootForce * 2.0f);
            powerBar.transform.localPosition = powerBarPos;
        }

        private void UpdateScores()
        {
            if (isFourBall)
            {
                ReportFourBallScore();

                return;
            }

            if (isNineBall)
            {
                ReportNineBallScore();

                return;
            }

            ReportEightBallScore();
        }

        private void ReportEightBallScore()
        {
            if (logger)
            {
                logger._Log(name, "ReportEightBallScore");
            }

            int sunkBlues = 0;
            int sunkOranges = 0;

            for (int i = 2; i < 9; i++)
            {
                if (ballsArePocketed[i])
                {
                    sunkBlues++;
                }
            }

            for (int i = 9; i < 16; i++)
            {
                if (ballsArePocketed[i])
                {
                    sunkOranges++;
                }
            }

            if (isGameInMenus)
            {
                if (isTeam2Winner && ballsArePocketed[1])
                {
                    if (isTeam2Blue)
                    {
                        sunkBlues++;
                    }
                    else
                    {
                        sunkOranges++;
                    }
                }
            }

            var teamAScore = isTeam2Blue ? sunkOranges : sunkBlues;
            var teamBScore = isTeam2Blue ? sunkBlues : sunkOranges;

            poolMenu._SetScore(false, teamAScore);
            poolMenu._SetScore(true, teamBScore);

            if (slogger)
            {
                slogger.OscReportScoresUpdated(isGameOver, turnID, teamAScore, isFoul, teamBScore);
            }
        }

        private void ReportNineBallScore()
        {
            if (logger)
            {
                logger._Log(name, "ReportNineBallScore");
            }

            poolMenu._SetScore(false, -1);
            poolMenu._SetScore(true, -1);

            if (slogger)
            {
                bool gameOver = false;

                if (isGameInMenus)
                {
                    if (ballsArePocketed[1])
                    {
                        gameOver = true;
                    }
                }

                slogger.OscReportScoresUpdated(gameOver, turnID, -1, isFoul, -1);
            }
        }

        private void ReportFourBallScore()
        {
            if (logger)
            {
                logger._Log(name, "ReportFourBallScore");
            }

            poolMenu._SetScore(false, scores[0]);
            poolMenu._SetScore(true, scores[1]);

            if (slogger)
            {
                slogger.OscReportScoresUpdated(scores[0] >= 10 || scores[1] >= 10, turnID, scores[0], isFoul,
                    scores[1]);
            }
        }

        private void ResetScores()
        {
            if (logger)
            {
                logger._Log(name, "ResetScores");
            }

            poolMenu._SetScore(false, 0);
            poolMenu._SetScore(true, 0);
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (!Networking.IsOwner(localPlayer, gameObject) || !player.IsValid())
            {
                return;
            }

            int playerID = player.playerId;

            if (playerID != player1ID && playerID != player2ID && playerID != player3ID &&
                playerID != player4ID)
            {
                return;
            }

            if (isGameInMenus)
            {
                RemovePlayerFromGame(playerID);
            }
            else
            {
                _Reset(ResetReason.PlayerLeft);
            }
        }

        /// <Summary> Is the local player near the table? </Summary>
        private bool isNearTable;

        /// <Summary> Can the local player enter the desktop top-down view? </Summary>
        private bool canEnterDesktopTopDownView;

        [HideInInspector] public ushort numberOfCuesHeldByLocalPlayer;

        private int ClothColour = Shader.PropertyToID("_ClothColour");
        private int MainTex = Shader.PropertyToID("_MainTex");

        public void _LocalPlayerEnteredAreaNearTable()
        {
            if (isPlayerInVR)
            {
                return;
            }

            isNearTable = true;
            CheckIfCanEnterDesktopTopDownView();
        }

        /// <Summary> Is the local player near the table? </Summary>
        public void _LocalPlayerLeftAreaNearTable()
        {
            isNearTable = false;
            CannotEnterDesktopTopDownView();
        }

        public void _LocalPlayerPickedUpCue()
        {
            numberOfCuesHeldByLocalPlayer++;
            CheckIfCanEnterDesktopTopDownView();
        }

        public void _LocalPlayerDroppedCue()
        {
            numberOfCuesHeldByLocalPlayer--;
            if (numberOfCuesHeldByLocalPlayer == 0)
            {
                CannotEnterDesktopTopDownView();
            }
        }

        private void CheckIfCanEnterDesktopTopDownView()
        {
            if (!isNearTable || numberOfCuesHeldByLocalPlayer <= 0)
            {
                return;
            }

            canEnterDesktopTopDownView = true;

            if (pancakeUI)
            {
                pancakeUI.gameObject.SetActive(true);
            }
        }

        private void CannotEnterDesktopTopDownView()
        {
            canEnterDesktopTopDownView = false;

            if (pancakeUI)
            {
                pancakeUI.gameObject.SetActive(false);
            }
        }

        public static string ToReasonString(ResetReason reason)
        {
            switch (reason)
            {
                case ResetReason.InvalidState:
                    return "The table was in an invalid state and has been reset";
                case ResetReason.PlayerLeft:
                    return "A player left, so the table was reset.";
                case ResetReason.InstanceOwnerReset:
                    return "The instance owner reset the table.";
                case ResetReason.PlayerReset:
                    return "A player has reset the table";
                default:
                    return "No reason";
            }
        }

        protected void HandleBallSunk(bool isSuccess)
        {
            mainSrc.PlayOneShot(sinkSfx, 1.0f);

            // If good pocket
            if (isSuccess)
                HandleSuccessEffects();
            else
                HandleFoulEffects();
        }

        protected void HandleSuccessEffects()
        {
            // Make a bright flash
            tableCurrentColour *= 1.9f;
            PlayAudioClip(successSfx);
        }

        private bool hasFoulBeenPlayedThisTurn;

        protected void HandleFoulEffects()
        {
            if (hasFoulBeenPlayedThisTurn)
                return;

            hasFoulBeenPlayedThisTurn = true;

            tableCurrentColour = pointerColourErr;
            PlayAudioClip(foulSfx);
        }

        protected void HandleBallCollision(int ballID, int otherBallID, Vector3 reflection)
        {
            // Prevent sound spam if it happens
            if (currentBallVelocities[ballID].sqrMagnitude > 0 && currentBallVelocities[otherBallID].sqrMagnitude > 0)
            {
                int clip = UnityEngine.Random.Range(0, hitsSfx.Length - 1);
                float vol = Mathf.Clamp01(currentBallVelocities[ballID].magnitude * reflection.magnitude);
                ballPoolTransforms[ballID].position = ballTransforms[ballID].position;
                ballPool[ballID].PlayOneShot(hitsSfx[clip], vol);
            }

            if (ballID != 0)
            {
                return;
            }

            if (gameMode == 2u)
            {
                if (isKorean)
                {
                    HandleKorean4BallScoring(otherBallID);
                }
                else if (isThreeCushionCarom)
                {
                    HandleThreeCushionCarom(otherBallID);
                }
                else
                {
                    HandleJapanese4BallScoring(otherBallID);
                }
            }
            else if (firstHitBallThisTurn == 0)
            {
                firstHitBallThisTurn = otherBallID;

                if (is8Ball && IsFirstEightBallHitFoul(firstHitBallThisTurn, GetNumberOfSunkBlues(),
                        GetNumberOfSunkOranges()))
                    HandleFoulEffects();
            }
        }

        private bool IsFirstEightBallHitFoul(int hitBallID, int numberOfSunkBlues, int numberOfSunkOranges)
        {
            if (isOpen)
                return hitBallID == 1;
            
            var isBlue = isTeam2Turn && isTeam2Blue || !isTeam2Turn && !isTeam2Blue;
            var winCondition = isBlue ? numberOfSunkBlues == 7 && ballsArePocketed[1] : numberOfSunkOranges == 7 && ballsArePocketed[1];

            // Even if you sunk black, if you hit the wrong ball first you still lose.
            if (winCondition)
                return hitBallID != 1;
            
            // The seven blue balls are stored at idx 2-8.
            if (isBlue)
                return hitBallID < 2 || hitBallID > 8;

            // The seven orange balls are stored at idx 9-15.
            return hitBallID < 9;
        }

        private void HandleJapanese4BallScoring(int otherBallID)
        {
            if (firstHitBallThisTurn == 0)
            {
                firstHitBallThisTurn = otherBallID;
            }
            else if (secondBallHitThisTurn == 0)
            {
                if (otherBallID == firstHitBallThisTurn)
                {
                    return;
                }

                secondBallHitThisTurn = otherBallID;

                Debug.Log(
                    $"Scoring a point due to hitting balls {firstHitBallThisTurn} and {secondBallHitThisTurn}");
                OnLocalCaromPoint(ballTransforms[otherBallID]);
            }
            else if (thirdBallHitThisTurn == 0)
            {
                if (otherBallID == firstHitBallThisTurn || otherBallID == secondBallHitThisTurn)
                {
                    return;
                }

                thirdBallHitThisTurn = otherBallID;
                Debug.Log(
                    $"Scoring a point due to hitting balls {firstHitBallThisTurn} and {secondBallHitThisTurn} and {thirdBallHitThisTurn}");
                OnLocalCaromPoint(ballTransforms[otherBallID]);
            }
        }

        private void HandleKorean4BallScoring(int otherBallID)
        {
            if (otherBallID == 9)
            {
                if (isMadeFoul)
                {
                    return;
                }

                isMadeFoul = true;
                scores[Convert.ToUInt32(isTeam2Turn)]--;

                if (scores[Convert.ToUInt32(isTeam2Turn)] < 0)
                {
                    scores[Convert.ToUInt32(isTeam2Turn)] = 0;
                }

                SpawnMinusOne(ballTransforms[otherBallID]);
            }
            else if (firstHitBallThisTurn == 0)
            {
                firstHitBallThisTurn = otherBallID;
            }
            else if (otherBallID != firstHitBallThisTurn)
            {
                if (secondBallHitThisTurn == 0)
                {
                    secondBallHitThisTurn = otherBallID;
                    OnLocalCaromPoint(ballTransforms[otherBallID]);
                }
            }
        }

        private void HandleThreeCushionCarom(int otherBallID)
        {
            // Three cushion billiards is a normal carom game with one exception: you need to hit THREE cushions during
            // your carom shot (before you hit the second object ball) for a score to count. It is an extremely hard
            // game.

            if (otherBallID == 9)
            {
                if (isMadeFoul)
                {
                    return;
                }

                // Note we do not penalize in this game. This game is already stupidly hard.
                isMadeFoul = true;
            }
            else if (firstHitBallThisTurn == 0)
            {
                firstHitBallThisTurn = otherBallID;
            }
            else if (otherBallID != firstHitBallThisTurn && secondBallHitThisTurn == 0)
            {
                secondBallHitThisTurn = otherBallID;

                if (cushionsHitThisTurn < 3)
                {
                    Debug.Log(
                        $"Not scoring a point despite hitting {firstHitBallThisTurn} and {secondBallHitThisTurn}: only hit {cushionsHitThisTurn} cushions");

                    return;
                }

                Debug.Log(
                    $"Scoring a point by hitting {firstHitBallThisTurn} and {secondBallHitThisTurn}: hit {cushionsHitThisTurn} cushions");

                OnLocalCaromPoint(ballTransforms[otherBallID]);
            }
        }

        private void PlayAudioClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            mainSrc.PlayOneShot(clip);
        }
    }
}