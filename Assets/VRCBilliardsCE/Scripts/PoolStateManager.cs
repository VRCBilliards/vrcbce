using System;
using FairlySadPanda.UsefulThings;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Rendering;
using VRC.SDKBase;
using VRC.Udon;

namespace VRCBilliards
{
    /// <summary>
    /// Main Behaviour for VRCBilliards: Community Edition. This is a giant class. Here be dragons.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PoolStateManager : UdonSharpBehaviour
    {
        /*
            ORDERING RULES (as their naming convention)
            PUBLIC_CONSTS
            PRIVATE_CONSTS
            publicVariables
            privateVariables
        */

        #region Constants

        /*
         * Constants
         */
        
        /// <summary>
        /// Maximum steps/frame (8). Note: for Android this was originally designed to be replaced with a value of 0.075.
        /// </summary>
        private const float MAX_DELTA = 0.1f;

        // Physics calculation constants (measurements are in meters)

        /// <summary>
        /// Default time step in seconds per iteration
        /// </summary>
        private const float DEFAULT_TIME_STEP = 0.0125f;
        
        /// <summary>
        /// horizontal span of table
        /// </summary>
        private const float TABLE_WIDTH = 1.0668f;

        /// <summary>
        /// vertical span of table
        /// </summary>
        private const float TABLE_HEIGHT = 0.6096f;

        /// <summary>
        /// width of ball
        /// </summary>
        private const float BALL_DIAMETER = 0.06f;

        /// <summary>
        /// break placement X
        /// </summary>
        private const float BALL_PL_X = 0.03f;

        /// <summary>
        /// Break placement Y
        /// </summary>
        private const float BALL_PL_Y = 0.05196152422f;

        /// <summary>
        /// 1 over ball radius
        /// </summary>
        private const float BALL_1OR = 33.3333333333f;

        /// <summary>
        /// ball radius squared
        /// </summary>
        private const float BALL_RADIUS_SQUARED = 0.0009f;

        /// <summary>
        /// ball diameter squared
        /// </summary>
        private const float BALL_DSQR = 0.0036f;

        /// <summary>
        /// ball diameter squared plus epsilon
        /// </summary>
        private const float BALL_DSQRPE = 0.003598f;

        /// <summary>
        /// Full diameter of pockets (exc ball radi)
        /// </summary>
        private const float POCKET_RADIUS = 0.09f;

        /// <summary>
        /// 1 over root 2 (normalize +-1,+-1 vector)
        /// </summary>
        private const float ONE_OVER_ROOT_TWO = 0.70710678118f;

        /// <summary>
        /// 1 over root 5 (normalize +-1,+-2 vector)
        /// </summary>
        private const float ONE_OVER_ROOT_FIVE = 0.4472135955f;

        private const float RANDOMIZE_F = 0.0001f;

        /// <summary>
        /// How far back (roughly) do pockets absorb balls after this point
        /// </summary>
        private const float POCKET_DEPTH = 0.04f;

        /// <summary>
        /// Friction coefficient of sliding
        /// </summary>
        private const float F_SLIDE = 0.2f;

        /// <summary>
        /// First X position of the racked balls
        /// </summary>
        private const float SPOT_POSITION_X = 0.5334f;

        /// <summary>
        /// Spot position for carom mode
        /// </summary>
        private const float SPOT_CAROM_X = 0.8001f;

        /// <summary>
        /// Rack position on Y axis
        /// </summary>
        private const float RACK_HEIGHT = -0.0702f;

        private const float CUE_VELOCITY_CLAMP = 20.0f;
        /// <summary>
        /// Number of itterations to run the preview sim.
        /// </summary>
        private const int PREVIEW_MAX_ITERATIONS_TOTAL_LIMIT = 500;
        
        /// <summary>
        /// Vectors cannot be const.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private Vector3 CONTACT_POINT = new Vector3(0.0f, -0.03f, 0.0f);

        private const float SIN_A = 0.28078832987f;
        private const float COS_A = 0.95976971915f;
        private const float F = 1.72909790282f;

        private const float DESKTOP_CURSOR_SPEED = 0.035f;

        private const float DEFAULT_FORCE_MULTIPLIER = 1.5f;

        private Vector3 vectorZero = Vector3.zero;
        private Quaternion quaternionIdentity = Quaternion.identity;

        /// <summary>
        /// Number of simulated balls in the table's domain.
        /// </summary>
        private const int NUMBER_OF_SIMULATED_BALLS = 16;

        #endregion

        /*
         * Public Variables
         */

        [Header("Options")]

        [Tooltip("Use fake shadows? Fake shadows are high-performance, but they may clash with your world's lighting.")]
        public bool fakeBallShadows = true;
        [Tooltip("Does the table model for this table have rails that guide the ball when the ball sinks?")]
        public bool tableModelHasRails;
        
        [Tooltip("Runs the simulation twice per frame at the slight cost of performance")]
        public bool isMoreAccurate = false;
        
        [Header("------")]

        [Header("Important Objects")]
        public Transform sunkBallsPositionRoot;
        public GameObject shadows;
        public ParticleSystem plusOneParticleSystem;
        public ParticleSystem minusOneParticleSystem;

        [Header("Shader Information")]
        public string uniformMarkerColour = "_Color";
        public string uniformCueColour = "_EmissionColor";
        public string uniformTableColour = "_EmissionColor";

        [Tooltip("Change the length of the intro ball-drop animation. If you set this to zero, the animation will not play at all.")]
        [Range(0f, 5f)]
        public float introAnimationLength = 2.0f;

        [Tooltip("If enabled, worldspace table scales beyond 1 in x or z will increase the force of hits to compensate, making it easier for regular-sized avatars to play.")]
        public bool scaleHitForceWithScaleBeyond1;

        [Tooltip("This value scales the clamp applied to the velocity of pocketted balls. Raising this will make pockets look less artificial at the cost of increasing the chance high-velocity balls will fly out of the table.")]
        public float pocketVelocityClamp = 1.0f;

        [Header("Table Colours")]
        [ColorUsage(true, true)] public Color tableBlue = new Color(0.0f, 0.5f, 1.5f, 1.0f);
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

        [ColorUsage(true, true)]
        private Color[] ballColors = new Color[NUMBER_OF_SIMULATED_BALLS];
        
        [Header("Cues")] public PoolCue[] poolCues;

        /// <summary>
        /// The balls that are used by the table.
        /// The order of the balls is as follows: cue, black, all blue in ascending order, then all orange in ascending order.
        /// If the order of the balls is incorrect, gameplay will not proceed correctly.
        /// </summary>
        [Header("Table Objects")]
        [Tooltip(
            "The balls that are used by the table.\nThe order of the balls is as follows: cue, black, all blue in ascending order, then all orange in ascending order.\nIf the order of the balls is incorrect, gameplay will not proceed correctly.")]
        public Transform[] ballTransforms = new Transform[NUMBER_OF_SIMULATED_BALLS];

        private Rigidbody[] ballRigidbodies = new Rigidbody[NUMBER_OF_SIMULATED_BALLS];

        [Tooltip("The shadow object for each ball")]
        public PositionConstraint[] ballShadowPosConstraints = new PositionConstraint[NUMBER_OF_SIMULATED_BALLS];

        private Transform[] ballShadowPosConstraintTransforms = new Transform[NUMBER_OF_SIMULATED_BALLS];
        private GameObject activeCueTip;
        private Transform cueTipTransform;
        public ShotGuideController guideline;
        public GameObject devhit;
        private GameObject[] poolCueGameObjects = new GameObject[2];
        private GameObject[] cueTips = new GameObject[2];
        //public GameObject gameTable;
        public GameObject marker;
        private Material markerMaterial;
        public GameObject marker9ball;
        //public GameObject tableCollisionParent;
        public GameObject pocketBlockers;
        private MeshRenderer[] cueRenderObjs = new MeshRenderer[2];

        [Header("Materials")]
        public MeshRenderer tableRenderer;
        private Material[] tableMaterials;
        private MeshRenderer[] ballRenderers = new MeshRenderer[NUMBER_OF_SIMULATED_BALLS];
        public Texture[] sets;
        
        private Material[] cueGripMaterials = new Material[2];
        //public Material markerMaterial;

        [Header("Audio")] public GameObject audioSourcePoolContainer;
        public AudioSource cueTipSrc;
        public AudioClip introSfx;
        public AudioClip sinkSfx;
        public AudioClip[] hitsSfx;
        public AudioClip newTurnSfx;
        public AudioClip pointMadeSfx;
        public AudioClip buttonSfx;
        public AudioClip spinSfx;
        public AudioClip spinStopSfx;
        public AudioClip hitBallSfx;

        [Header("Reflection Probes")] public ReflectionProbe tableReflection;

        [Header("Meshes")] 
        public Mesh fourBallCueMesh;
        private Mesh originalNineBallMesh;
        private Mesh originalCueBallMesh;
        private GameObject baseObject;
        
        
        private PoolMenu poolMenu;
        private PoolPracticeMenu poolPracticeMenu;
        /// <summary>
        /// True whilst balls are rolling
        /// </summary>
        [UdonSynced] private bool gameIsSimulating;

        /// <summary>
        /// Timer ID 2 bit	{ 0: inf, 1: 10s, 2: 15s, 3: 30s, 4: 60s, 5: undefined }
        /// </summary>
        [UdonSynced] private int timerType;

        /// <summary>
        /// Permission for player to play
        /// </summary>
        [UdonSynced] private bool isPlayerAllowedToPlay;

        /// <summary>
        /// Player is hitting
        /// </summary>
        private bool isArmed;
        
        /// <summary>
        /// Is the Pre Shot Sim Enabled?
        /// </summary>
        [UdonSynced] private bool isPreviewSimEnabled = false;
        
        /// <summary>
        /// Is the preview sim running?
        /// </summary>
        private bool isPreviewSimRunning = false;
        
        /// <summary>
        /// How far is the cuetip from the cue ball last frame?
        /// </summary>
        private float previewPreviousCueDistance = 0;
        
        /// <summary>
        /// Should trail lines be shown for the cue ball or all balls?
        /// </summary>
        [UdonSynced]private bool previewOnlyCueBall = false;
        
        /// <summary>
        /// The number of itterations run the preview sim.
        /// </summary>
        private int previewCurrentIteration = 0;
        
        // Previous States
        private bool[][] previewPocketedState = new bool[PREVIEW_MAX_ITERATIONS_TOTAL_LIMIT][];
        private Vector3[][] previewBallVelocity = new Vector3[PREVIEW_MAX_ITERATIONS_TOTAL_LIMIT][];
        private Vector3[][] previewBallPosition = new Vector3[PREVIEW_MAX_ITERATIONS_TOTAL_LIMIT][];
        private Vector3[][] previewAngularVelocities = new Vector3[PREVIEW_MAX_ITERATIONS_TOTAL_LIMIT][];
        
        /// <summary>
        /// Trail Renderers for Preview Sim
        /// </summary>
        private TrailRenderer[] previewTrails = new TrailRenderer[16];
        
        private int localPlayerID = -1;

        [UdonSynced] private bool guideLineEnabled = true;

        [Header("Desktop Stuff")] public GameObject desktopCursorObject;
        public GameObject desktopHitPosition;

        public GameObject desktopBase;

        //public GameObject desktopQuad;
        private GameObject[] desktopCueParents = new GameObject[2];
        public GameObject desktopOverlayPower;

        [Header("UI Stuff")]
        //public Text[] lobbyNames;
        /*
         * Private variables
         */
        private AudioSource[] ballPool;

        private Transform[] ballPoolTransforms;
        private AudioSource mainAudioSource;
        private UdonBehaviour udonChips;

        /// <summary>
        /// 18:0 (0xffff)	Each bit represents each ball, if it has been pocketed or not
        /// </summary>
        ///
        /* Balls
         * 0 White Cue
         * 1 8 Ball
         * 2 1 ball Solids
         * 3 2 ball
         * 4 3 ball
         * 5 4 ball
         * 6 5 ball
         * 7 6 ball
         * 8 7 ball
         * 9 9 ball //stripes
         * 10 10 ball
         * 11 11 ball
         * 12 12 ball
         * 13 13 ball
         * 14 14 ball
         * 15 15 ball
         */
        [UdonSynced] private bool[] ballPocketedState = new bool[NUMBER_OF_SIMULATED_BALLS];

        /// <summary>
        /// 19:1 (0x02)		Whos turn is it, 0 or 1
        /// </summary>
        [UdonSynced] private bool newIsTeam2Turn;

        /// <summary>
        /// 19:2 (0x04)		End-of-turn foul marker
        /// </summary>
        [UdonSynced] private bool isFoul;

        /// <summary>
        /// 19:3 (0x08)		Is the table open?
        /// </summary>
        [UdonSynced] private bool isOpen = true;

        /// <summary>
        /// 19:4 (0x10)		What colour the players have chosen
        /// </summary>
        [UdonSynced] private bool isPlayer2Solids;

        /// <summary>
        /// 19:5 (0x20)	The game isn't running
        /// </summary>
        [UdonSynced] private bool isGameInMenus = true;

        /// <summary>
        /// 19:6 (0x40)		Who won the game if sn_gameover is set
        /// </summary>
        [UdonSynced] private bool isTeam2Winner;

        /// <summary>
        /// Represents if the game is joinable
        /// </summary>
        [UdonSynced] private bool isTableLocked = true;

        /// <summary>
        /// 19:15 (0x8000)	Teams on/off (1 bit)
        /// </summary>
        [UdonSynced] private bool isTeams;

        /// <summary>
        /// 21:0 (0xffff)	Game number
        /// </summary>
        [UdonSynced] private int gameID;

        // Cached data to use when checking for update.

        private bool[] oldBallPocketedState;
        private bool oldIsTeam2Turn;
        private bool oldOpen;
        private bool oldIsGameInMenus;
        private int oldGameID;

        /// <summary>
        /// We are waiting for our local simulation to finish, before we unpack data
        /// </summary>
        private bool isUpdateLocked;

        /// <summary>
        /// The first ball to be hit by cue ball
        /// </summary>
        private int isFirstHit;

        private int isSecondHit;
        private int isThirdHit;

        /// <summary>
        /// If the simulation was initiated by us, only set from update
        /// </summary>
        private bool isSimulatedByUs;

        /// <summary>
        /// Ball dropper timer
        /// </summary>
        private float introAnimRemainingTimer;

        /// <summary>
        /// Tracker variable to see if balls are still on the go
        /// </summary>
        private bool ballsMoving;

        /// <summary>
        /// Repositioner is active
        /// </summary>
        [UdonSynced] private bool isRepositioningCueBall;

        /// <summary>
        /// For clamping to table or set lower for kitchen
        /// </summary>
        private float repoMaxX = TABLE_WIDTH;

        private float remainingTime;
        private float originalRemainingTime;
        private bool isTimerRunning;
        private bool isParticleAlive;
        private float particleTime;
        private bool isMadePoint;
        private bool isMadeFoul;

        [UdonSynced] private bool isKorean;

        [UdonSynced] private int[] scores = new int[2];

        private bool is8Ball;
        private bool isNineBall;
        private bool isFourBall;

        /// <summary>
        /// Game should run in practice mode
        /// </summary>
        private bool isGameModePractice;

        private bool isInDesktopTopDownView;

        /// <summary>
        /// Interpreted value
        /// </summary>
        private bool playerIsTeam2;

        [UdonSynced] private Vector3[] currentBallPositions = new Vector3[NUMBER_OF_SIMULATED_BALLS];
        [UdonSynced] private Vector3[] currentBallVelocities = new Vector3[NUMBER_OF_SIMULATED_BALLS];
        [UdonSynced] private Vector3[] currentAngularVelocities = new Vector3[NUMBER_OF_SIMULATED_BALLS];
        
        /// <summary>
        /// Shot Replay values
        /// </summary>
        private Vector3[] initialCueBallVelocity = new Vector3[MAX_TURNS];
        private Vector3[] initialCueBallAngularVelocity = new Vector3[MAX_TURNS];
        private bool isReplaying = false;
        /// <summary>
        /// Shot Undo System
        /// </summary>
        [UdonSynced] [HideInInspector] public int currentTurn = 0;
        [UdonSynced] [HideInInspector] public int latestTurn = 0; //for redo feature
        private const int MAX_TURNS = 1000;
        private Vector3[][] previousBallPositions = new Vector3[MAX_TURNS][];
        private bool[][] previousBallPocketedState = new bool[MAX_TURNS][];
        private bool[] previousIsOpen = new bool[MAX_TURNS];
        private bool[] previousIsTeam2Turn = new bool[MAX_TURNS];
        private bool[] previousIsPlayer2Solids = new bool[MAX_TURNS];
        private bool[] previousPlayerTeam2 = new bool[MAX_TURNS];

        /// <summary>
        /// time step in seconds per iteration
        /// </summary>
        private float FIXED_TIME_STEP = 0.0125f;

        /// <summary>
        /// Runtime target colour
        /// </summary>
        private Color tableSrcColour = new Color(1.0f, 1.0f, 1.0f, 1.0f);

        /// <summary>
        /// Runtime actual colour
        /// </summary>
        private Color tableCurrentGlowColour = new Color(1.0f, 1.0f, 1.0f, 1.0f);

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
        private Vector3 deskTopCursor = new Vector3(0.0f, 2.0f, 0.0f);
        private Vector3 desktopHitCursor = new Vector3(0.0f, 0.0f, 0.0f);
        private bool isDesktopShootingIn;
        private bool isDesktopSafeRemove = true;
        private Vector3 desktopShootVector;
        private Vector3 desktopSafeRemovePoint;
        private float desktopShootReference;
        private float desktopClampX = TABLE_WIDTH;
        private float desktopClampY = TABLE_HEIGHT;
        private bool isDesktopLocalTurn;
        private bool isEnteringDesktopModeThisFrame;

        /// <summary>
        /// Cue input tracking
        /// </summary>
        private Vector3 localSpacePositionOfCueTip;

        private Vector3 localSpacePositionOfCueTipLastFrame;
        private Vector3 cueLocalForwardDirection;
        private Vector3 cueArmedShotDirection;
        private float cueFDir;
        private Vector3 raySphereOutput;
        private int lastTimerType;
        private float minTimeSinceLastSimRun;
        private float shootAmt;
        private int[] rackOrder8Ball = { 9, 2, 10, 11, 1, 3, 4, 12, 5, 13, 14, 6, 15, 7, 8 };
        private int[] rackOrder9Ball = { 2, 3, 4, 5, 9, 6, 7, 8, 1 };
        private int[] brearows_9ball = { 0, 1, 2, 1, 0 };

        /// <summary>
        /// 19:8 (0x700)	Gamemode ID 3 bit	{ 0: 8 ball, 1: 9 ball, 2+: undefined }
        /// </summary>
        [UdonSynced] private int gameMode;

        // Additional synced data added by the port to Manual Sync

        [UdonSynced] private int player1ID;
        [UdonSynced] private int player2ID;
        [UdonSynced] private int player3ID;
        [UdonSynced] private int player4ID;

        public FairlySadPanda.UsefulThings.Logger logger; 

        [UdonSynced] private bool gameWasReset;

        private float ballShadowOffset;
        private MeshRenderer[] shadowRenders;

        private bool isPlayerInVR;
        private VRCPlayerApi localPlayer;
        private int networkingLocalPlayerID;
        private Transform markerTransform;

        private Camera desktopCamera;

        /// <summary>
        /// A value intended to accomodate for resized tables, to make them possible to use without using an avatar with
        /// an equivalent height.
        /// </summary>
        private float forceMultiplier;

        private TMPro.TextMeshProUGUI timerText;
        private string timerOutputFormat;
        private UnityEngine.UI.Image timerCountdown;

        private int oldDesktopCue;
        private int newDesktopCue;

        /// <summary>
        /// Force inflicted on non-kinematic pool balls from the center of the table outwards. Helps prevent bounce-back.
        /// </summary>
        private float repulsionForce = 0.2F;

        /// <summary>
        /// Have we run a network sync once? Used for situations where we need to specifically catch up a late-joiner.
        /// </summary>
        private bool hasRunSyncOnce;

        #region UdonChipsVariables
        // We are breaking our normal Java-like ordering rules here. UdonChips is a layer on top of regular VRCBCE code,
        // and we want the ability to contain it easily. A region is the best way of handling this.

        [Header("UdonChips")]
        [Tooltip("Enable this to integrate with UdonChips.")]
        public bool enableUdonChips = false;

        [Tooltip("The base cost of a game in UC.")]
        public float price = 100.0f;

        [Tooltip(
            "Allow for a player to add more UC to their total entry cost. If the player wins, they get a bigger reward.")]
        public bool allowRaising = true;

        [Tooltip("The basic prize a player gets for winning versus an opponent.")]
        public float prize = 200.0f;

        [Tooltip("The reward a player gets for beating themselves at pool.")]
        public float singlePlayPrize = 150.0f;

        [Tooltip("An optional sound clip to play when someone pays to play.")]
        public AudioClip paySound;

        [Tooltip("An optional sound clip to play when someone cannot afford to play.")]
        public AudioClip insufficientFundsSound;

        [UdonSynced] [HideInInspector] public int raiseCount = 1;

        private float Money
        {
            get => (float)udonChips.GetProgramVariable("money");
            set => udonChips.SetProgramVariable("money", value);
        }

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

        private bool IsSinglePlayer => !playerIsTeam2 && PlayerCount == 0;

        private float TotalPrice => price * raiseCount;

        private bool hasPaidToSignUp;

        #endregion

        private bool startHasConcluded;

        public void Start()
        {
            //Find logger if missing
            if (logger == null)
            {
                logger = transform.parent.GetComponentInChildren<FairlySadPanda.UsefulThings.Logger>();
            }
            if (transform.parent && transform.parent.parent)
            {
                baseObject = transform.parent.parent.gameObject;
            }

            if (baseObject == null)
            {
                if (logger)
                {
                    logger._Error(name, "The pool table attempted to access the root of the table, which should be an object two parents above its own transform. It was unable to do so, and the pool table will thus not function.");
                }
                else
                {
                    Debug.LogError("The pool table attempted to access the root of the table, which should be an object two parents above its own transform. It was unable to do so, and the pool table will thus not function.");
                }
                gameObject.SetActive(false);
                return;
            }

            poolMenu = baseObject.GetComponentInChildren<PoolMenu>(true);
            if (poolMenu == null)
            {
                if (logger)
                {
                    logger._Error(name, "The pool table attempted to find a PoolMenu inside the children of its base object (its parent's parent), but was unable to do so, and will not function.");
                }
                else
                {
                    Debug.LogError("The pool table attempted to find a PoolMenu inside the children of its base object (its parent's parent), but was unable to do so, and will not function.");
                }
                gameObject.SetActive(false);
                return;
            }
            poolPracticeMenu = baseObject.GetComponentInChildren<PoolPracticeMenu>(true);

            if (Networking.LocalPlayer == null)
            {
                return;
            }
            localPlayer = Networking.LocalPlayer;
            networkingLocalPlayerID = localPlayer.playerId;

            if (VRC.SDKBase.Utilities.IsValid(localPlayer))
            {
                isPlayerInVR = localPlayer.IsUserInVR();
            }

            // Find 4 ball particles
            if (plusOneParticleSystem == null) plusOneParticleSystem = transform.Find("Plus").GetComponent<ParticleSystem>();
            if (minusOneParticleSystem == null) minusOneParticleSystem = transform.Find("Minus").GetComponent<ParticleSystem>();
            // Find Markers
            if (marker == null) marker = transform.Find("marker").gameObject;
            if (devhit == null) devhit = transform.Find("dev-hit").gameObject;
            if (marker9ball == null) marker9ball = transform.Find("9ball_target").gameObject;

            SetupBalls();
            SetupShadows();
            SetupCues();
            tableMaterials = tableRenderer.materials;
            // Disable the reflection probe on Quest.
#if UNITY_ANDROID
            tableReflection = null;
#endif
            
            mainAudioSource = GetComponent<AudioSource>();

            if (audioSourcePoolContainer) // Xiexe: Use a pool for audio instead of using the PlayClipAtPoint method because PlayClipAtPoint is buggy and VRC audio controls do not modify it.
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
            markerMaterial = marker.GetComponent<MeshRenderer>().material;

            CopyGameStateToOldState();



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

            Vector3 scale = baseObject.transform.lossyScale;

            // Round the scale to 1 decimal place. If the scale is not uniform, warn the user.
            // Generally we understand that 
            if (
                Mathf.Round(scale.x * 10) != Mathf.Round(scale.y * 10) ||
                Mathf.Round(scale.y * 10) != Mathf.Round(scale.z * 10)
            )
            {
                if (logger)
                {
                    logger._Warning(name,
                        "You appear to have scaled this table in a non-uniform way. VRCBCE makes no guarantees of what might happen when you do this. May God have mercy on your soul.");
                }
                else
                {
                    Debug.LogWarning(
                        "You appear to have scaled VRCBCE in a non-uniform way. VRCBCE makes no guarantees of what might happen when you do this. May God have mercy on your soul.");
                }
            }

            if (scaleHitForceWithScaleBeyond1 && scale.x > 1f || scale.z > 1f)
            {
                float scaler = scale.x > scale.z ? scale.x : scale.z;
                forceMultiplier = DEFAULT_FORCE_MULTIPLIER * scaler;
                if (logger)
                {
                    logger._Log(name,
                        $"Due to increased scale of {scaler} , setting force multiplier of hits to {DEFAULT_FORCE_MULTIPLIER} * {scaler} = {forceMultiplier}");
                }
            }
            else
            {
                forceMultiplier = DEFAULT_FORCE_MULTIPLIER;
            }

            if (enableUdonChips)
            {
                udonChips = (UdonBehaviour)GameObject.Find("UdonChips").GetComponent(typeof(UdonBehaviour));
            }

            timerText = poolMenu.visibleTimerDuringGame;
            timerOutputFormat = poolMenu.timerOutputFormat;
            timerCountdown = poolMenu.timerCountdown;


            UsColors();
            FindTrails();
            startHasConcluded = true;
            
            if (isMoreAccurate)
            {
                FIXED_TIME_STEP = DEFAULT_TIME_STEP / 2;
            }
            else
            {
                FIXED_TIME_STEP = DEFAULT_TIME_STEP;
            }
        }

        private void SetupCues()
        {
            for (int i = 0; i < 2; i++)
            {
                cueTips[i] = poolCues[i].cueTip;
                cueRenderObjs[i] = poolCues[i].cueParent.GetComponentInChildren<MeshRenderer>();
                poolCueGameObjects[i] = poolCues[i].gameObject;
                desktopCueParents[i] = poolCues[i].cueParent.gameObject;
                cueGripMaterials[i] = poolCues[i].GetComponent<Renderer>().sharedMaterial;
                cueRenderObjs[i].material.SetColor(uniformCueColour, tableBlack);
            }
            activeCueTip = cueTips[1];
            cueTipTransform = activeCueTip.transform;
        }

        private void SetupShadows()
        {
            // Must run AFTER ball setup.
            if (shadows == null)
            {
                Transform shadowsParent = transform.Find("Shadows");
                shadows = shadowsParent.gameObject;
            }
            shadows.SetActive(fakeBallShadows);
            
            if (ballShadowPosConstraints[0] == null) ballShadowPosConstraints[0] = shadows.transform.Find("Cue Ball Shadow").GetComponent<PositionConstraint>();
            if (ballShadowPosConstraints[1] == null) ballShadowPosConstraints[1] = shadows.transform.Find("Black Ball Shadow").GetComponent<PositionConstraint>();
            for (int i = 2; i < NUMBER_OF_SIMULATED_BALLS-1; i++)
            {
                if (i == 9) i = 10;

                if (ballShadowPosConstraints[i] == null)
                {
                    ballShadowPosConstraints[i] = shadows.transform.Find("Ball " + (i-1) + " Shadow").GetComponent<PositionConstraint>();
                }
            }
            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                ballShadowPosConstraintTransforms[i] = ballShadowPosConstraints[i].transform;
            }
            ballShadowOffset = ballTransforms[0].position.y - ballShadowPosConstraintTransforms[0].position.y;
            if (logger)
            {
                logger._Log(name, $"ball shadow offset is {ballShadowOffset}");
            }
            shadowRenders = shadows.GetComponentsInChildren<MeshRenderer>();
        }

        private void SetupBalls()
        {
            //Find Balls
            if (ballTransforms[0] == null) ballTransforms[0] = transform.Find("Cue Ball");
            if (ballTransforms[1] == null) ballTransforms[1] = transform.Find("Black Ball");
            
            for (int i = 2; i < NUMBER_OF_SIMULATED_BALLS-1; i++)
            {
                if (i == 9) i = 10;
                if (ballTransforms[i] == null)
                {
                    string targetBall = "ball"+(i-1).ToString("00");
                    ballTransforms[i] = transform.Find(targetBall);
                }
            }
            originalNineBallMesh = ballTransforms[9].gameObject.GetComponent<MeshFilter>().mesh;
            originalCueBallMesh = ballTransforms[0].gameObject.GetComponent<MeshFilter>().mesh;
            
            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                ballRenderers[i] = ballTransforms[i].GetComponent<MeshRenderer>();
                ballRigidbodies[i] = ballTransforms[i].GetComponent<Rigidbody>();
            }
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

        private void FourBallColors()
        {
            ballColors[0] = Color.white;
            ballColors[9] = Color.yellow;
            ballColors[2] = Color.red;
            ballColors[3] = Color.red;

        }
        public void Update()
        {
            if (isInDesktopTopDownView)
            {
                HandleUpdatingDesktopViewUI();

                if (Input.GetKeyDown(KeyCode.E))
                {
                    OnDesktopTopDownViewExit();
                }
            }
            else if (canEnterDesktopTopDownView)
            {
                if (Input.GetKeyDown(KeyCode.E))
                {
                    _OnDesktopTopDownViewStart();
                }
            }
            
            // Run sim only if things are moving
            if (gameIsSimulating)
            {
                minTimeSinceLastSimRun += Time.deltaTime;

                if (minTimeSinceLastSimRun > MAX_DELTA)
                {
                    minTimeSinceLastSimRun = MAX_DELTA;
                }
                int isAccurate = isMoreAccurate ? 2 : 1;
                while (minTimeSinceLastSimRun >= FIXED_TIME_STEP * isAccurate)
                {
                    AdvancePhysicsStep();
                    if (isMoreAccurate) AdvancePhysicsStep();
                    minTimeSinceLastSimRun -= FIXED_TIME_STEP * isAccurate;
                }
            }
            else if (isGameInMenus)
            {
                return;
            }
            // Everything below this line only runs when the game is active.

            // Update rendering objects positions
            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                // If ball is not pocketed update positions
                if (!(ballPocketedState[i]))
                {
                    ballTransforms[i].localPosition = currentBallPositions[i];
                }
            }

            RunPreviewSim();
            localSpacePositionOfCueTip = transform.InverseTransformPoint(activeCueTip.transform.position);
            Vector3 copyOfLocalSpacePositionOfCueTip = localSpacePositionOfCueTip;

            // if shot is prepared for next hit
            if (isPlayerAllowedToPlay)
            {
                if (isRepositioningCueBall)
                {
                    // Clamp position to table / kitchen
                    Vector3 temp = markerTransform.localPosition;
                    temp.x = Mathf.Clamp(temp.x, -TABLE_WIDTH, repoMaxX);
                    temp.z = Mathf.Clamp(temp.z, -TABLE_HEIGHT, TABLE_HEIGHT);
                    temp.y = 0.0f;
                    markerTransform.localPosition = temp;
                    markerTransform.localRotation = quaternionIdentity;

                    currentBallPositions[0] = temp;
                    ballTransforms[0].localPosition = temp;

                    if (IsCueBallContactingAnotherBall())
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
                    float cueDistance = (copyOfLocalSpacePositionOfCueTip - cueballPosition).sqrMagnitude;
                    float sweepTimeBall = Vector3.Dot(cueballPosition - localSpacePositionOfCueTipLastFrame,
                        cueLocalForwardDirection);

                    // Check for potential skips due to low frame rate
                    if (sweepTimeBall > 0.0f && sweepTimeBall <
                        (localSpacePositionOfCueTipLastFrame - copyOfLocalSpacePositionOfCueTip).magnitude)
                    {
                        copyOfLocalSpacePositionOfCueTip = localSpacePositionOfCueTipLastFrame +
                                                           (cueLocalForwardDirection * sweepTimeBall);
                    }

                    // Hit condition is when cuetip is gone inside ball
                    if (cueDistance < BALL_RADIUS_SQUARED)
                    {
                        Vector3 horizontalForce =
                            copyOfLocalSpacePositionOfCueTip - localSpacePositionOfCueTipLastFrame;

                        // Compute velocity delta
                        float vel = forceMultiplier * (horizontalForce.magnitude / Time.deltaTime);

                        // Clamp velocity input to 20 m/s ( moderate break speed )
                        currentBallVelocities[0] = cueArmedShotDirection * Mathf.Min(vel, CUE_VELOCITY_CLAMP);

                        // Angular velocity: L=r(normalized)×p
                        Vector3 r = (raySphereOutput - cueballPosition) * BALL_1OR;
                        Vector3 p = cueLocalForwardDirection * vel;
                        currentAngularVelocities[0] = Vector3.Cross(r, p) * -50.0f;
                        _StoreShot();
                        HitBallWithCue();
                    }
                    else
                    {
                        StartPreviewSim(cueDistance);
                    }
                }
                else
                {
                    cueLocalForwardDirection = transform.InverseTransformVector(activeCueTip.transform.forward);

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
                        cueArmedShotDirection.y = 0.0f;

                        if (!isInDesktopTopDownView)
                        {
                            // Compute deflection in VR mode
                            Vector3 scuffdir = cueballPosition - raySphereOutput;
                            scuffdir.y = 0.0f;
                            cueArmedShotDirection += scuffdir.normalized * 0.17f;
                        }

                        cueFDir = Mathf.Atan2(cueArmedShotDirection.z, cueArmedShotDirection.x);

                        // Update the prediction line direction
                        guideline.transform.localPosition = currentBallPositions[0];
                        guideline.transform.localEulerAngles = new Vector3(0.0f, -cueFDir * Mathf.Rad2Deg, 0.0f);
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
            }

            localSpacePositionOfCueTipLastFrame = copyOfLocalSpacePositionOfCueTip;

            // Table outline colour
            if (isGameInMenus)
            {
                // Flashing if we won
                tableCurrentGlowColour = tableSrcColour * ((Mathf.Sin(Time.timeSinceLevelLoad * 3.0f) * 0.5f) + 1.0f);
            }
            else
            {
                tableCurrentGlowColour = Color.Lerp(tableCurrentGlowColour, tableSrcColour, Time.deltaTime * 3.0f);
            }

            //float timePercentage;

            if (isTimerRunning)
            {
                remainingTime -= Time.deltaTime;

                if (remainingTime < 0.0f)
                {
                    isTimerRunning = false;

                    // We are holding the stick so propogate the change
                    if (Networking.GetOwner(poolCueGameObjects[Convert.ToInt32(newIsTeam2Turn)]) == localPlayer)
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
                        timerCountdown.fillAmount = remainingTime / originalRemainingTime;
                    }
                }
            }
            else
            {
                timerText.text = "";
                timerCountdown.fillAmount = 0f;
            }

            foreach (Material tableMaterial in tableMaterials)
            {
                tableMaterial.SetColor(uniformTableColour, tableCurrentGlowColour);
            }

            // Run the intro animation. Do not run the animation if this is our first sync!
            if (hasRunSyncOnce && introAnimRemainingTimer > 0.0f)
            {
                introAnimRemainingTimer -= Time.deltaTime;

                Vector3 temp;
                float atime;
                float aitime;

                if (introAnimRemainingTimer < 0.0f)
                {
                    introAnimRemainingTimer = 0.0f;
                }

                // Cueball drops late
                Transform ball = ballTransforms[0];
                temp = ball.localPosition;
                float height = ball.position.y - ballShadowOffset;

                atime = Mathf.Clamp(introAnimRemainingTimer - 0.33f, 0.0f, 1.0f);
                aitime = 1.0f - atime;
                temp.y = Mathf.Abs(Mathf.Cos(atime * 6.29f)) * atime * 0.5f;
                ball.localPosition = temp;

                Vector3 scale = new Vector3(aitime, aitime, aitime);
                ball.localScale = scale;

                PositionConstraint posCon = ballShadowPosConstraints[0];
                posCon.constraintActive = false;
                Transform posConTrans = ballShadowPosConstraintTransforms[0];
                posConTrans.position = new Vector3(ball.position.x, height, ball.position.z);
                posConTrans.localScale = scale;

                MeshRenderer r = shadowRenders[0];
                Color c = r.material.color;
                r.material.color = new Color(c.r, c.g, c.b, aitime);

                for (int i = 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
                {
                    ball = ballTransforms[i];

                    temp = ball.localPosition;
                    height = ball.position.y - ballShadowOffset;

                    atime = Mathf.Clamp(introAnimRemainingTimer - 0.84f - (i * 0.03f), 0.0f, 1.0f);
                    aitime = 1.0f - atime;

                    temp.y = Mathf.Abs(Mathf.Cos(atime * 6.29f)) * atime * 0.5f;
                    ball.localPosition = temp;

                    scale = new Vector3(aitime, aitime, aitime);
                    ball.localScale = scale;

                    posCon = ballShadowPosConstraints[i];
                    posCon.constraintActive = false;
                    posConTrans = ballShadowPosConstraintTransforms[i];
                    posConTrans.position = new Vector3(ball.position.x, height, ball.position.z);
                    posConTrans.localScale = scale;

                    r = shadowRenders[i];
                    c = r.material.color;
                    r.material.color = new Color(c.r, c.g, c.b, aitime);
                }
            }
        }

        private void FixedUpdate()
        {
            if (isGameInMenus)
            {
                return;
            }

            Vector3 referencePosition = transform.position;

            foreach (Rigidbody poolBall in ballRigidbodies)
            {
                if (poolBall.isKinematic || !((referencePosition.y - poolBall.position.y) < 0))
                {
                    continue;
                }
                Vector3 differenceNormalized = poolBall.position - referencePosition;
                differenceNormalized = Vector3.Normalize(differenceNormalized) * repulsionForce;

                poolBall.AddForce(differenceNormalized);
            }
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
            if (gameIsSimulating)
            {
                isUpdateLocked = true;
            }
            else
            {
                // We are free to read this update
                ReadNetworkData();
            }
        }

        /// PUBLIC FUNCTIONS
        /// MENU ACCESSORS
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

            if (!Pay(TotalPrice))
            {
                return;
            }
            else
            {
                hasPaidToSignUp = true;
            }

            Networking.SetOwner(localPlayer, gameObject);
            localPlayerID = playerNumber;

            switch (playerNumber)
            {
                case 0:
                    player1ID = networkingLocalPlayerID;
                    break;
                case 1:
                    player2ID = networkingLocalPlayerID;
                    break;
                case 2:
                    player3ID = networkingLocalPlayerID;
                    break;
                case 3:
                    player4ID = networkingLocalPlayerID;
                    break;
                default:
                    return;
            }

            RefreshNetworkData(false);
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

            if (timerType < 4)
            {
                timerType++;
                RefreshNetworkData(false);
            }
        }

        public void _DecreaseTimer()
        {
            if (logger)
            {
                logger._Log(name, "DecreaseTimer");
            }

            Networking.SetOwner(localPlayer, gameObject);

            if (timerType > 0)
            {
                timerType--;
                RefreshNetworkData(false);
            }
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

            if (!isGameInMenus)
            {
                return;
            }

            gameWasReset = false;
            gameID++;
            isPlayerAllowedToPlay = true;

            OnRemoteNewGame();

            newIsTeam2Turn = false;
            oldIsTeam2Turn = false;
            OnRemoteTurnChange();

            // Following is overrides of NewGameLocal, for game STARTER only
            gameIsSimulating = false;
            isOpen = true;
            isGameInMenus = false;
            poolCues[0].tableIsActive = true;
            poolCues[1].tableIsActive = true;

            isPlayer2Solids = false;
            isTeam2Winner = false;

            // Cue ball
            currentBallPositions[0] = new Vector3(-SPOT_POSITION_X, 0.0f, 0.0f);
            currentBallVelocities[0] = vectorZero;

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

            oldBallPocketedState = ballPocketedState;

            ApplyTableColour(false);

            Networking.SetOwner(localPlayer, gameObject);

            RefreshNetworkData(false);
            if (isGameModePractice)
            {
                poolPracticeMenu._EnablePracticeMenu();
            }
            poolPracticeMenu._UpdateMenu(currentTurn,latestTurn,isGameModePractice,isPreviewSimEnabled,previewOnlyCueBall);
        }

        private void Initialize9Ball()
        {
            ballPocketedState = new bool[NUMBER_OF_SIMULATED_BALLS];
            for (int i = 10; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                ballPocketedState[i] = true;
            }
            
            for (int i = 0, k = 0; i < 5; i++)
            {
                int rown = brearows_9ball[i];
                for (int j = 0; j <= rown; j++)
                {
                    currentBallPositions[rackOrder9Ball[k++]] = new Vector3
                    (
                        SPOT_POSITION_X + (i * BALL_PL_Y) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F),
                        0.0f,
                        ((-rown + (j * 2)) * BALL_PL_X) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F)
                    );

                    currentBallVelocities[k] = vectorZero;
                    currentAngularVelocities[k] = vectorZero;
                }
            }
            UsColors();
        }

        private void Initialize4Ball()
        {
            ballPocketedState = new bool[NUMBER_OF_SIMULATED_BALLS];
            
            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                if (i != 0 && i != 2 && i != 3 && i != 9)
                {
                    ballPocketedState[i] = true;
                }
            }

            currentBallPositions[0] = new Vector3(-SPOT_CAROM_X, 0.0f, 0.0f);
            currentBallPositions[9] = new Vector3(SPOT_CAROM_X, 0.0f, 0.0f);
            currentBallPositions[2] = new Vector3(SPOT_POSITION_X, 0.0f, 0.0f);
            currentBallPositions[3] = new Vector3(-SPOT_POSITION_X, 0.0f, 0.0f);

            currentBallVelocities[0] = vectorZero;
            currentBallVelocities[9] = vectorZero;
            currentBallVelocities[2] = vectorZero;
            currentBallVelocities[3] = vectorZero;

            currentAngularVelocities[0] = vectorZero;
            currentAngularVelocities[9] = vectorZero;
            currentAngularVelocities[2] = vectorZero;
            currentAngularVelocities[3] = vectorZero;
            FourBallColors();
        }

        private void Initialize8Ball()
        {
            ballPocketedState = new bool[NUMBER_OF_SIMULATED_BALLS]; // No balls are pocketed.
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

                    currentBallVelocities[k] = vectorZero;
                    currentAngularVelocities[k] = vectorZero;
                }
            }
            TeamColors();
            //UsColors();//TODO Check if using us colors
        }

        public void _Select8Ball()
        {
            if (logger)
            {
                logger._Log(name, "Select8Ball");
            }

            Networking.SetOwner(localPlayer, gameObject);

            gameMode = 0;
            RefreshNetworkData(false);
        }

        public void _Select9Ball()
        {
            if (logger)
            {
                logger._Log(name, "Select9Ball");
            }

            Networking.SetOwner(localPlayer, gameObject);

            gameMode = 1;
            RefreshNetworkData(false);
        }

        /// <summary>
        /// Player selected Japanese 4-ball.
        /// </summary>
        public void _Select4BallJapanese()
        {
            if (logger)
            {
                logger._Log(name, "Select4BallJapanese");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isKorean = false;

            gameMode = 2;
            RefreshNetworkData(false);
        }

        /// <summary>
        /// Player selected Korean 4-ball.
        /// </summary>
        public void _Select4BallKorean()
        {
            if (logger)
            {
                logger._Log(name, "Select4BallKorean");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isKorean = true;

            gameMode = 2;
            RefreshNetworkData(false);
        }

        /// CUE ACTIONS
        /// <summary>
        /// Player is holding input trigger
        /// </summary>
        public void _StartHit()
        {
            if (logger)
            {
                logger._Log(name, "StartHit");
            }

            // lock aim variables
            bool isOurTurn = ((localPlayerID >= 0) && (playerIsTeam2 == newIsTeam2Turn)) || isGameModePractice;

            if (isOurTurn)
            {
                isArmed = true;
            }
        }

        /// <summary>
        /// Player stopped holding input trigger
        /// </summary>
        public void _EndHit()
        {
            if (logger)
            {
                logger._Log(name, "EndHit");
            }

            isArmed = false;
        }

        /// <summary>
        /// Player was moving cueball, place it down
        /// </summary>
        public void PlaceBall()
        {
            if (logger)
            {
                logger._Log(name, "PlaceBall");
            }

            if (!IsCueBallContactingAnotherBall())
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
                RefreshNetworkData(newIsTeam2Turn);
            }
        }

        /// <summary>
        /// Completely reset state
        /// </summary>
        public void _ForceReset()
        {
            if (logger)
            {
                logger._Log(name, "ForceReset");
            }

            if (
                // If you are a player
                networkingLocalPlayerID == player1ID ||
                networkingLocalPlayerID == player2ID ||
                networkingLocalPlayerID == player3ID ||
                networkingLocalPlayerID == player4ID ||
                // The game is in the menu so resetting doesn't matter much
                isGameInMenus ||
                // The game is in a running state, someone has left, and the table has entered an invalid state
                (player1ID > 0 && VRCPlayerApi.GetPlayerById(player1ID) == null) ||
                (player2ID > 0 && VRCPlayerApi.GetPlayerById(player2ID) == null) ||
                (player3ID > 0 && VRCPlayerApi.GetPlayerById(player3ID) == null) ||
                (player4ID > 0 && VRCPlayerApi.GetPlayerById(player4ID) == null)
            )
            {
                Networking.SetOwner(localPlayer, gameObject);

                _Reset();
            }
            else if (logger)
            {
                logger._Log(name, "Cannot reset table: You must be a player, or the table must be in an invalid state.");
            }
        }

        private void _Reset()
        {
            isGameInMenus = true;
            isReplaying = false;
            currentTurn = -1;
            latestTurn = -1;
            poolPracticeMenu._DisablePracticeMenu();
            poolCues[0].tableIsActive = false;
            poolCues[1].tableIsActive = false;
            isPlayerAllowedToPlay = false;
            gameIsSimulating = false;
            newIsTeam2Turn = false;
            isGameModePractice = false;
            RefreshNetworkData(newIsTeam2Turn);

            if (logger)
            {
                logger._Log(name, "Forcing a reset was successful.");
            }
        }

        public void _OnDesktopTopDownViewStart()
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

            poolCues[0].localPlayerIsInDesktopTopDownView = true;
            poolCues[1].localPlayerIsInDesktopTopDownView = true;
        }

        /// <summary>
        /// Cue put down local
        /// </summary>
        public void _OnPutDownCueLocally()
        {
            if (logger)
            {
                logger._Log(name, "OnPutDownCueLocally");
            }

            OnDesktopTopDownViewExit();
        }

        private void AdvancePhysicsStep()
        {
            ballsMoving = false;
            
            // Run main simulation / inter-ball collision
            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                if (!ballPocketedState[i]) // If the ball in question is not sunk
                {
                    currentBallPositions[i] += currentBallVelocities[i] * FIXED_TIME_STEP;

                    AdvanceSimulationForBall(i);
                }
            }

            // Check if simulation has settled
            if (!ballsMoving && gameIsSimulating)
            {
                gameIsSimulating = false;

                // Make sure we only run this from the client who initiated the move
                if (isSimulatedByUs)
                {
                    HandleEndOfTurn();
                }

                // Check if there was a network update on hold
                if (isUpdateLocked)
                {
                    isUpdateLocked = false;

                    ReadNetworkData();
                }

                return;
            }

            if (isFourBall)
            {
                BounceBallOffCushionIfApplicable(0);
                BounceBallOffCushionIfApplicable(2);
                BounceBallOffCushionIfApplicable(3);
                BounceBallOffCushionIfApplicable(9);

                return;
            }


            // Run edge collision
            for (int index = 0; index < NUMBER_OF_SIMULATED_BALLS; index++)
            {
                if (!ballPocketedState[index])
                {
                    float zy, zx, zk, zw, d, k, i, j, l, r;
                    Vector3 A, N;

                    A = currentBallPositions[index];

                    // REGIONS
                    /*
                        *  QUADS:							SUBSECTION:				SUBSECTION:
                        *    zx, zy:							zz:						zw:
                        *
                        *  o----o----o  +:  1			\_________/				\_________/
                        *  | -+ | ++ |  -: -1		     |	    /		              /  /
                        *  |----+----|					  -  |  +   |		      -     /   |
                        *  | -- | +- |						  |	   |		          /  +  |
                        *  o----o----o						  |      |             /       |
                        *
                        */

                    // Setup major regions
                    zx = Mathf.Sign(A.x);
                    zy = Mathf.Sign(A.z);

                    // within pocket regions
                    if ((A.z * zy > (TABLE_HEIGHT - POCKET_RADIUS)) &&
                        (A.x * zx > (TABLE_WIDTH - POCKET_RADIUS) || A.x * zx < POCKET_RADIUS))
                    {
                        // Subregions
                        zw = A.z * zy > (A.x * zx) - TABLE_WIDTH + TABLE_HEIGHT ? 1.0f : -1.0f;

                        // Normalization / line coefficients change depending on sub-region
                        if (A.x * zx > TABLE_WIDTH * 0.5f)
                        {
                            zk = 1.0f;
                            r = ONE_OVER_ROOT_TWO;
                        }
                        else
                        {
                            zk = -2.0f;
                            r = ONE_OVER_ROOT_FIVE;
                        }

                        // Collider line EQ
                        d = zx * zy * zk; // Coefficient
                        k = (-(TABLE_WIDTH * Mathf.Max(zk, 0.0f)) + (POCKET_RADIUS * zw * Mathf.Abs(zk)) +
                             TABLE_HEIGHT) * zy; // Konstant

                        // Check if colliding
                        l = zw * zy;
                        if (A.z * l > ((A.x * d) + k) * l)
                        {
                            // Get line normal
                            N.x = zx * zk;
                            N.z = -zy;
                            N.y = 0.0f;
                            N *= zw * r;

                            // New position
                            i = ((A.x * d) + A.z - k) / (2.0f * d);
                            j = (i * d) + k;

                            currentBallPositions[index].x = i;
                            currentBallPositions[index].z = j;

                            // Reflect velocity
                            ApplyBounceCushion(index, N);
                        }
                    }
                    else // edges
                    {
                        if (A.x * zx > TABLE_WIDTH)
                        {
                            currentBallPositions[index].x = TABLE_WIDTH * zx;
                            ApplyBounceCushion(index, Vector3.left * zx);
                        }

                        if (A.z * zy > TABLE_HEIGHT)
                        {
                            currentBallPositions[index].z = TABLE_HEIGHT * zy;
                            ApplyBounceCushion(index, Vector3.back * zy);
                        }
                    }
                }
            }

            // Run triggers
            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                if (!ballPocketedState[i])
                {
                    float zz, zx;
                    Vector3 A = currentBallPositions[i];

                    // Setup major regions
                    zx = Mathf.Sign(A.x);
                    zz = Mathf.Sign(A.z);

                    // Its in a pocket
                    if (
                        A.z * zz > TABLE_HEIGHT + POCKET_DEPTH ||
                        A.z * zz > (A.x * -zx) + TABLE_WIDTH + TABLE_HEIGHT + POCKET_DEPTH
                    )
                    {
                        int total = 0;

                        // Get total for X positioning
                        int count_extent = isNineBall ? 10 : NUMBER_OF_SIMULATED_BALLS;
                        for (int j = 1; j < count_extent; j++)
                        {
                            total += ballPocketedState[i] ? 1 : 0;
                        }

                        // set this for later
                        currentBallPositions[i].x = -0.9847f + (total * BALL_DIAMETER);
                        currentBallPositions[i].z = 0.768f;

                        // This is where we actually save the pocketed/non-pocketed state of balls.
                        ballPocketedState[i] = true;

                        mainAudioSource.PlayOneShot(sinkSfx, 1.0f);
            
                        int offset = newIsTeam2Turn ^ isPlayer2Solids ? 7 : 0; //offset for stripes
                        if (isOpen)
                        {
                            if (i > 1 && i < 16)
                            {
                                // Make a bright flash
                                tableCurrentGlowColour *= 1.9f;
                            }
                            else
                            {
                                tableCurrentGlowColour = pointerColourErr;
                            }
                        }
                        else if (i < 9 + offset && i > 1 + offset) //score was for correct team
                        {
                            // Make a bright flash
                            tableCurrentGlowColour *= 1.9f;
                        }
                        else
                        {
                            tableCurrentGlowColour = pointerColourErr;
                        }
                        
                        // VFX ( make ball move )
                        Rigidbody body = ballTransforms[i].GetComponent<Rigidbody>();
                        body.isKinematic = false;

                        body.velocity = baseObject.transform.rotation * new Vector3(
                            Mathf.Clamp(currentBallVelocities[i].x, (pocketVelocityClamp * -1), pocketVelocityClamp),
                            0.0f,
                            Mathf.Clamp(currentBallVelocities[i].z, (pocketVelocityClamp * -1), pocketVelocityClamp)
                        );
                        // Debug.Log("Ball sunk velocity, " + body.velocity);
                    }
                }
            }
        }

        private void HandleEndOfTurn()
        {
            isSimulatedByUs = false;
            _StoreEndTurn();
            // We are updating the game state so make sure we are network owner
            Networking.SetOwner(Networking.LocalPlayer, gameObject);

            // Owner state checks
            
            int offset = newIsTeam2Turn ^ isPlayer2Solids ? 7 : 0; //offset for stripes

            // Common informations
            bool isSetComplete = true;
            
            for (int i = isOpen ? 1 : (1 + offset) ; i < (isOpen ? 16 : (8 + offset)); i++)
            {
                if (!ballPocketedState[i])
                {
                    isSetComplete = false;
                    break;
                }
            }
            bool isScratch = ballPocketedState[0];
            
            // These are the resultant states we can set for each mode
            // then the rest is taken care of
            bool
                isObjectiveSink,
                isOpponentSink,
                winCondition,
                foulCondition,
                deferLossCondition
                ;

            if (is8Ball) // Standard 8 ball
            {
                bool isWrongHit = false;
                
                int previousCount = 0; // balls sunk in previous turn.
                int currentCount = 0; // balls sunk in current turn
                // Masking the for loop to a small part of the array
                int startOffset = isOpen ? 2 : 2 + offset - (isSetComplete ? 1 : 0);
                int endOffset = isOpen ? NUMBER_OF_SIMULATED_BALLS : 9 + offset;
                for (int i = startOffset ; i < endOffset; i++) // TODO: UnHaumt this loop. oldBallPocketedState is updating when it shouldnt, or current and previousCount are haumted
                {
                    if (ballPocketedState[i])
                    {
                        currentCount++;
                    }
                    if (oldBallPocketedState[i])
                    {
                        previousCount++;
                    }
                }
                isObjectiveSink = currentCount > previousCount;
                previousCount = 0;
                currentCount = 0;
                if (!isOpen)
                {
                    for (int i = 9 - offset ; i < NUMBER_OF_SIMULATED_BALLS - offset; i++)
                    {
                        if (isFirstHit == 1)
                        {
                            isWrongHit = true;
                        }
                        if (ballPocketedState[i])
                        {
                            currentCount++;
                        }
                        if (oldBallPocketedState[i])
                        {
                            previousCount++;
                        }
                        
                    }
                    isOpponentSink = currentCount > previousCount;
                }
                else
                {
                    isOpponentSink = false;
                }
                // Calculate if objective was not hit first
                bool is8Sink = ballPocketedState[1];
                winCondition = isSetComplete && is8Sink;
                foulCondition = isScratch || isWrongHit;
                deferLossCondition = is8Sink;
            }
            else if (isNineBall) // 9 ball
            {
                // Rules are from: https://www.youtube.com/watch?v=U0SbHOXCtFw

                // Rule #1: Cueball must strike the lowest number ball, first
                bool isWrongHit = GetLowestNumberedBall(oldBallPocketedState) != isFirstHit;

                // Rule #2: Pocketing cueball, is a foul

                // Win condition: Pocket 9 ball ( at anytime )
                winCondition = ballPocketedState[9];

                // this video is hard to follow so im just gonna guess this is right
                int previousCount = 0, currentCount = 0;
                for (int i = 1; i < 11; i++)
                {
                    if (ballPocketedState[i])
                    {
                        currentCount++;
                    }

                    if (oldBallPocketedState[i])
                    {
                        previousCount++;
                    }
                }
                isObjectiveSink = currentCount > previousCount;
                isOpponentSink = false;
                deferLossCondition = false;

                foulCondition = isWrongHit || isScratch;

                // TODO: Implement rail contact requirement
            }
            else if (isFourBall) // 4 ball
            {
                isObjectiveSink = isMadePoint;
                isOpponentSink = isMadeFoul;
                foulCondition = false;
                deferLossCondition = false;

                winCondition = scores[Convert.ToInt32(newIsTeam2Turn)] >= 10;
            }
            else // Unkown mode
            {
                isObjectiveSink = true;
                isOpponentSink = false;
                winCondition = false;
                foulCondition = false;
                deferLossCondition = false;

                if (ballPocketedState[1])
                {
                    isFoul = true;
                    OnRemoteTurnChange();
                }
            }

            if (winCondition)
            {
                if (foulCondition)
                {
                    // Loss
                    OnTurnOverGameWon(!newIsTeam2Turn);
                }
                else
                {
                    // Win
                    OnTurnOverGameWon(newIsTeam2Turn);
                }
            }
            else if (deferLossCondition)
            {
                // Loss
                OnTurnOverGameWon(!newIsTeam2Turn);
            }
            else if (foulCondition)
            {
                // Foul
                OnTurnOverFoul();
            }
            else if (isObjectiveSink && !isOpponentSink)
            {
                // Continue
                // Close table if it was open ( 8 ball specific )
                if (is8Ball && isOpen)
                {
                    int sunkOranges = 0;
                    int sunkBlues = 0;

                    for (int i = 2; i < 9; i++)
                    {
                        if (ballPocketedState[i])
                        {
                            sunkBlues++;
                        }

                    }

                    for (int i = 9; i < 16; i++)
                    {
                        if (ballPocketedState[i])
                        {
                            sunkOranges++;
                        }

                    }

                    if (sunkBlues != sunkOranges)
                    {
                        isPlayer2Solids = (sunkBlues > sunkOranges) ? newIsTeam2Turn : !newIsTeam2Turn;

                        isOpen = false;
                        ApplyTableColour(newIsTeam2Turn);
                    }
                }

                // Keep playing
                isPlayerAllowedToPlay = true;

                RefreshNetworkData(newIsTeam2Turn);
            }
            else
            {
                // Pass
                isPlayerAllowedToPlay = true;

                if (isFourBall)
                {
                    Vector3 temp = currentBallPositions[0];
                    currentBallPositions[0] = currentBallPositions[9];
                    currentBallPositions[9] = temp;
                }

                RefreshNetworkData(!newIsTeam2Turn);
            }
        }

        private void RefreshNetworkData(bool newIsTeam2Playing)
        {
            if (logger)
            {
                logger._Log(name, "RefreshNetworkData");
            }

            newIsTeam2Turn = newIsTeam2Playing;

            Networking.SetOwner(localPlayer, gameObject);
            RequestSerialization();
            ReadNetworkData();
        }

        /// <summary>
        /// Decode networking string
        /// </summary>
        private void ReadNetworkData()
        {
            if (logger)
            {
                logger._Log(name, "ReadNetworkData");
            }

            if (marker)
            {
                marker.SetActive(false);
            }

            // Events ==========================================================================================================

            if (gameID > oldGameID && !isGameInMenus)
            {
                OnRemoteNewGame();

                if (((localPlayerID >= 0) && (playerIsTeam2 == newIsTeam2Turn)) || isGameModePractice)
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

                        ((VRC_Pickup)marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = true;
                    }
                }
                else
                {
                    markerTransform.localPosition = currentBallPositions[0];

                    if (!isFourBall)
                    {
                        marker.SetActive(true);
                    }

                    ((VRC_Pickup)marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = false;
                }
            }

            if (newIsTeam2Turn != oldIsTeam2Turn)
            {
                OnRemoteTurnChange();
            }

            oldIsTeam2Turn = newIsTeam2Turn;

            if (oldOpen && !isOpen)
            {
                ApplyTableColour(newIsTeam2Turn);
            }

            if (!oldIsGameInMenus && isGameInMenus)
            {
                OnRemoteGameOver();
            }

            CopyGameStateToOldState();

            if (isTableLocked)
            {
                poolMenu._EnableUnlockTableButton();
                ResetScores();
            }
            else if (!isGameInMenus)
            {
                poolMenu._EnableResetButton();
                UpdateScores();
            }
            else
            {
                poolMenu._EnableMainMenu();
                ResetScores();
            }

            poolMenu._UpdateMainMenuView(
                isTeams,
                newIsTeam2Turn,
                (int)gameMode,
                isKorean,
                (int)timerType,
                player1ID,
                player2ID,
                player3ID,
                player4ID,
                guideLineEnabled);
            poolPracticeMenu._UpdateMenu(currentTurn,latestTurn,isGameModePractice,isPreviewSimEnabled,previewOnlyCueBall);
            if (isGameInMenus)
            {
                if (lastTimerType != timerType)
                {
                    mainAudioSource.PlayOneShot(spinSfx);
                    lastTimerType = timerType;
                }

                int numberOfPlayers = 0;

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

                int playerID = Networking.LocalPlayer.playerId;
                if (hasPaidToSignUp && player1ID != playerID && player2ID != playerID && player3ID != playerID &&
                    player4ID != playerID)
                {
                    hasPaidToSignUp = false;
                    PayBack(TotalPrice);
                }

                hasRunSyncOnce = true;

                return;
            }

            // Effects colliders need to be turned off when not simulating
            // to improve pickups being glitchy
            // if (gameIsSimulating)
            // {
            //     if (tableCollisionParent)
            //     {
            //         tableCollisionParent.SetActive(true);
            //     }
            // }
            // else
            // {
            //     if (tableCollisionParent)
            //     {
            //         tableCollisionParent.SetActive(false);
            //     }
            // }

            if (isFourBall)
            {
                ballPocketedState = new bool[NUMBER_OF_SIMULATED_BALLS];

                for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
                {
                    if (i != 0 && i != 2 && i != 3 && i != 9)
                    {
                        ballPocketedState[i] = true;
                    }
                }

            }

            // Check this every read
            // Its basically 'turn start' event
            if (isPlayerAllowedToPlay)
            {
                if (((localPlayerID >= 0) && (playerIsTeam2 == newIsTeam2Turn)) || isGameModePractice)
                {
                    // Update for desktop
                    isDesktopLocalTurn = true;

                    // Reset hit point
                    desktopHitCursor = vectorZero;
                }
                else
                {
                    isDesktopLocalTurn = false;
                }

                if (isNineBall)
                {
                    int target = GetLowestNumberedBall(ballPocketedState);
                    ApplyTableColour(newIsTeam2Turn);
                    if (marker9ball)
                    {
                        marker9ball.SetActive(true);
                        marker9ball.transform.localPosition = currentBallPositions[target];
                    }
                }

                if (!tableModelHasRails || !hasRunSyncOnce)
                {
                    PlaceSunkBallsIntoRestingPlace();
                }

                if (timerType > 0 && !isTimerRunning)
                {
                    ResetTimer();
                }
            }
            else
            {
                if (marker9ball)
                {
                    marker9ball.SetActive(false);
                }

                isTimerRunning = false;
                isMadePoint = false;
                isMadeFoul = false;
                isFirstHit = 0;
                isSecondHit = 0;
                isThirdHit = 0;

                if (devhit)
                {
                    devhit.SetActive(false);
                }

                if (guideline)
                {
                    guideline.gameObject.SetActive(false);
                }
            }

            hasRunSyncOnce = true;
        }

        public void OnDisable()
        {
            // Disabling the table means we're in an identical state to a late-joiner - the table will have progressed
            // since we last ran a sync. Ergo, we should tell the table that it needs to handle things as if the player
            // is a late-joiner.
            hasRunSyncOnce = false;
        }

        /// <summary>
        /// Updates table colour target to appropriate player colour
        /// </summary>
        private void ApplyTableColour(bool isTeam2Turn)
        {
            if (logger)
            {
                logger._Log(name, "ApplyTableColour");
            }

            if (isFourBall)
            {
                if (!newIsTeam2Turn)
                {
                    cueRenderObjs[0].materials[0].SetColor(uniformCueColour, pointerColour0);
                    cueRenderObjs[1].materials[0].SetColor(uniformCueColour, pointerColour1 * 0.5f);
                }
                else
                {
                    cueRenderObjs[0].materials[0].SetColor(uniformCueColour, pointerColour0 * 0.5f);
                    cueRenderObjs[1].materials[0].SetColor(uniformCueColour, pointerColour1);
                }

                tableSrcColour = tableBlack;
            }
            else if (isNineBall)
            {
                int target = GetLowestNumberedBall(ballPocketedState);
                Color color = ballColors[target];
                cueRenderObjs[Convert.ToInt32(newIsTeam2Turn)].materials[0].SetColor(uniformCueColour, color);
                cueRenderObjs[Convert.ToInt32(!newIsTeam2Turn)].materials[0].SetColor(uniformCueColour, tableBlack);

                tableSrcColour = color;
            }
            else if (!isOpen)
            {
                if (isTeam2Turn)
                {
                    if (isPlayer2Solids)
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
                    if (isPlayer2Solids)
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

                cueRenderObjs[Convert.ToInt32(newIsTeam2Turn)].materials[0].SetColor(uniformCueColour, tableWhite);
                cueRenderObjs[Convert.ToInt32(!newIsTeam2Turn)].materials[0].SetColor(uniformCueColour, tableBlack);
            }

            cueGripMaterials[Convert.ToInt32(newIsTeam2Turn)].SetColor(uniformMarkerColour, gripColourActive);
            cueGripMaterials[Convert.ToInt32(!newIsTeam2Turn)].SetColor(uniformMarkerColour, gripColourInactive);
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

            if (timerType != 0)
            {
                switch (timerType)
                {
                    case 1:
                        remainingTime = 10f;
                        originalRemainingTime = 10f;
                        break;
                    case 2:
                        remainingTime = 15f;
                        originalRemainingTime = 15f;
                        break;
                    case 3:
                        remainingTime = 30f;
                        originalRemainingTime = 30f;
                        break;
                    case 4:
                        remainingTime = 60f;
                        originalRemainingTime = 60f;
                        break;
                }

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
            mainAudioSource.PlayOneShot(pointMadeSfx, 1.0f);

            scores[Convert.ToInt32(newIsTeam2Turn)]++;

            if (scores[Convert.ToInt32(newIsTeam2Turn)] > 10)
            {
                scores[Convert.ToInt32(newIsTeam2Turn)] = 10;
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

            hasPaidToSignUp = false;
            GiveRewards();

            if (gameWasReset)
            {
                poolMenu._GameWasReset();
            }
            else
            {
                poolMenu._TeamWins(isTeam2Winner);
            }

            if (marker9ball)
            {
                marker9ball.SetActive(false);
            }

            // if (tableCollisionParent)
            // {
            //     tableCollisionParent.SetActive(false);
            // }

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

            raiseCount = 1;

            poolMenu._UpdateMainMenuView(
                isTeams,
                newIsTeam2Turn,
                (int)gameMode,
                isKorean,
                (int)timerType,
                player1ID,
                player2ID,
                player3ID,
                player4ID,
                guideLineEnabled);
            poolPracticeMenu._UpdateMenu(currentTurn,latestTurn,isGameModePractice,isPreviewSimEnabled, previewOnlyCueBall);

            poolMenu._EnableMainMenu();
            poolPracticeMenu._DisablePracticeMenu();
            
            poolCues[0]._Respawn();
            poolCues[1]._Respawn();
        }

        private void OnRemoteTurnChange()
        {
            if (logger)
            {
                logger._Log(name, "OnRemoteTurnChange");
            }

            // Effects
            ApplyTableColour(newIsTeam2Turn);
            mainAudioSource.PlayOneShot(newTurnSfx, 1.0f);

            // Register correct cuetip
            activeCueTip = cueTips[Convert.ToInt32(newIsTeam2Turn)];

            bool isOurTurn = ((localPlayerID >= 0) && (playerIsTeam2 == newIsTeam2Turn)) || isGameModePractice;
            if (isFourBall) // 4 ball
            {
                if (!newIsTeam2Turn)
                {
                    if (logger != null)
                    {
                        logger._Log(name, "0 ball is 0 mesh, 9 ball is 1 mesh");
                    }

                    ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = originalCueBallMesh;
                    ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = fourBallCueMesh;
                }
                else
                {
                    if (logger != null)
                    {
                        logger._Log(name, "0 ball is 0 mesh, 9 ball is 1 mesh");
                    }

                    ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = originalCueBallMesh;
                    ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = originalNineBallMesh;
                }
            }
            else
            {
                // White was pocketed
                if (ballPocketedState[0])
                {
                    currentBallPositions[0] = vectorZero;
                    currentBallVelocities[0] = vectorZero;
                    currentAngularVelocities[0] = vectorZero;

                    ballPocketedState[0] = false;
                }
            }

            if (isFoul)
            {
                if (isOurTurn)
                {
                    isRepositioningCueBall = true;
                    ballRigidbodies[0].isKinematic = true;
                    repoMaxX = TABLE_WIDTH;

                    if (logger)
                    {
                        logger._Log(name, "Enabling marker because it is our turn and there was a foul last turn");
                    }

                    if (marker)
                    {
                        marker.SetActive(true);
                        ((VRC_Pickup)marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = true;
                        markerTransform.localPosition = currentBallPositions[0];
                    }
                }
                else
                {
                    marker.SetActive(true);
                    markerTransform.localPosition = currentBallPositions[0];
                    ((VRC_Pickup)marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = false;
                }
            }

            // Force timer reset
            if (timerType > 0)
            {
                ResetTimer();
            }
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

            is8Ball = gameMode == 0;
            isNineBall = gameMode == 1;
            isFourBall = gameMode == 2;

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

                foreach (MeshRenderer renderer in ballRenderers)
                {
                    renderer.material.SetTexture("_MainTex", sets[3]);
                }
            }
            else if (isFourBall)
            {
                pointerColour0 = tableWhite;
                pointerColour1 = tableYellow;

                // Should not be used
                pointerColour2 = tableRed;
                pointerColourErr = tableRed;

                foreach (MeshRenderer renderer in ballRenderers)
                {
                    renderer.material.SetTexture("_MainTex", sets[2]);
                }

                pointerClothColour = fabricGreen;
            }
            else // Standard 8 ball derivatives
            {
                pointerColourErr = tableRed;
                pointerColour2 = tableWhite;

                pointerColour0 = tableBlue;
                pointerColour1 = tableOrange;

                foreach (MeshRenderer renderer in ballRenderers)
                {
                    renderer.material.SetTexture("_MainTex", sets[0]);
                }

                pointerClothColour = fabricGray;
            }

            tableRenderer.material.SetColor("_ClothColour", pointerClothColour);

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
                ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = originalCueBallMesh;
                ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = originalNineBallMesh;
            }
            else
            {
                if (pocketBlockers)
                {
                    pocketBlockers.SetActive(false);
                }

                // Reset mesh filters on balls that change them
                ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = originalCueBallMesh;
                ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = originalNineBallMesh;
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
                introAnimRemainingTimer = introAnimationLength;
                if (introAnimRemainingTimer > 0.0f)
                {
                    mainAudioSource.PlayOneShot(introSfx, 1.0f);
                    SendCustomEventDelayedSeconds(nameof(_ReEnableShadowConstraints), introAnimationLength);
                    SendCustomEventDelayedSeconds(nameof(_StoreEndTurn), introAnimationLength);

                }
            }

            isTimerRunning = false;

            poolCues[0].localPlayerIsInDesktopTopDownView = false;
            poolCues[1].localPlayerIsInDesktopTopDownView = false;

            if (!localPlayer.IsUserInVR())
            {
                poolCues[0].usingDesktop = true;
                poolCues[1].usingDesktop = true;
            }
            else
            {
                poolCues[0].usingDesktop = false;
                poolCues[1].usingDesktop = false;
            }

            if (IsSinglePlayer)
            {
                PayBack(price * (raiseCount - 1));
            }

            // Make sure that we run a pass on rigidbodies to ensure they are off.
            PlaceSunkBallsIntoRestingPlace();
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
            int numberOfSunkBalls = 0;
            float posX = sunkBallsPositionRoot.localPosition.x;
            float posY = sunkBallsPositionRoot.localPosition.y;
            float posZ = sunkBallsPositionRoot.localPosition.z;

            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                ballTransforms[i].GetComponent<Rigidbody>().isKinematic = true;

                if (ballPocketedState[i])
                {
                    ballTransforms[i].localPosition = new Vector3(posX + numberOfSunkBalls * BALL_DIAMETER, posY, posZ);
                    numberOfSunkBalls++;
                }
            }
        }

        /// <summary>
        /// Is cue touching another ball?
        /// </summary>
        private bool IsCueBallContactingAnotherBall()
        {
            // 8 ball, practice, portal
            if (gameMode != 1u)
            {
                // Check all
                for (int i = 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
                {
                    if ((currentBallPositions[0] - currentBallPositions[i]).sqrMagnitude < BALL_DSQR)
                    {
                        return true;
                    }
                }
            }
            else // 9 ball
            {
                // Only check to 9 ball
                for (int i = 1; i <= 9; i++)
                {
                    if ((currentBallPositions[0] - currentBallPositions[i]).sqrMagnitude < BALL_DSQR)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Apply cushion bounce
        /// </summary>
        /// <param name="id"></param>
        /// <param name="N"></param>
        private void ApplyBounceCushion(int id, Vector3 N)
        {
            // Mathematical expressions derived from: https://billiards.colostate.edu/physics_articles/Mathavan_IMechE_2010.pdf
            //
            // (Note): subscript gamma, u, are used in replacement of Y and Z in these expressions because
            // unicode does not have them.
            //
            // f = 2/7
            // f₁ = 5/7
            //
            // Velocity delta:
            //   Δvₓ = −vₓ∙( f∙sin²θ + (1+e)∙cos²θ ) − Rωᵤ∙sinθ
            //   Δvᵧ = 0
            //   Δvᵤ = f₁∙vᵤ + fR( ωₓ∙sinθ - ωᵧ∙cosθ ) - vᵤ
            //
            // Aux:
            //   Sₓ = vₓ∙sinθ - vᵧ∙cosθ+ωᵤ
            //   Sᵧ = 0
            //   Sᵤ = -vᵤ - ωᵧ∙cosθ + ωₓ∙cosθ
            //
            //   k = (5∙Sᵤ) / ( 2∙mRA );
            //   c = vₓ∙cosθ - vᵧ∙cosθ
            //
            // Angular delta:
            //   ωₓ = k∙sinθ
            //   ωᵧ = k∙cosθ
            //   ωᵤ = (5/(2m))∙(-Sₓ / A + ((sinθ∙c∙(e+1)) / B)∙(cosθ - sinθ));
            //
            // These expressions are in the reference frame of the cushion, so V and ω inputs need to be rotated

            // Reject bounce if velocity is going the same way as normal
            // this state means we tunneled, but it happens only on the corner
            // vertexes
            Vector3 source_v = currentBallVelocities[id];
            if (Vector3.Dot(source_v, N) > 0.0f)
            {
                return;
            }

            // Rotate V, W to be in the reference frame of cushion
            Quaternion rq = Quaternion.AngleAxis(Mathf.Atan2(-N.z, -N.x) * Mathf.Rad2Deg, Vector3.up);
            Quaternion rb = Quaternion.Inverse(rq);
            Vector3 V = rq * source_v;
            Vector3 W = rq * currentAngularVelocities[id];

            Vector3 V1;
            Vector3 W1;
            float k, c, s_x, s_z;

            //V1.x = -V.x * ((2.0f/7.0f) * SINA2 + EP1 * COSA2) - (2.0f/7.0f) * BALL_PL_X * W.z * SINA;
            //V1.z = (5.0f/7.0f)*V.z + (2.0f/7.0f) * BALL_PL_X * (W.x * SINA - W.y * COSA) - V.z;
            //V1.y = 0.0f;
            // (baked):
            V1.x = (-V.x * F) - (0.00240675711f * W.z);
            V1.z = (0.71428571428f * V.z) + (0.00857142857f * ((W.x * SIN_A) - (W.y * COS_A))) - V.z;
            V1.y = 0.0f;

            // s_x = V.x * SINA - V.y * COSA + W.z;
            // (baked): y component not used:
            s_x = (V.x * SIN_A) + W.z;
            s_z = -V.z - (W.y * COS_A) + (W.x * SIN_A);

            // k = (5.0f * s_z) / ( 2 * BALL_MASS * A );
            // (baked):
            k = s_z * 0.71428571428f;

            // c = V.x * COSA - V.y * COSA;
            // (baked): y component not used
            c = V.x * COS_A;

            W1.x = k * SIN_A;

            //W1.z = (5.0f / (2.0f * BALL_MASS)) * (-s_x / A + ((SINA * c * EP1) / B) * (COSA - SINA));
            // (baked):
            W1.z = 15.625f * ((-s_x * 0.04571428571f) + (c * 0.0546021744f));
            W1.y = k * COS_A;

            // Unrotate result
            currentBallVelocities[id] += rb * V1;
            currentAngularVelocities[id] += rb * W1;
        }

        /// <summary>
        /// Pocketless table
        /// </summary>
        /// <param name="id"></param>
        private void BounceBallOffCushionIfApplicable(int id)
        {
            float zz, zx;
            Vector3 A = currentBallPositions[id];

            // Setup major regions
            zx = Mathf.Sign(A.x);
            zz = Mathf.Sign(A.z);

            if (A.x * zx > TABLE_WIDTH)
            {
                currentBallPositions[id].x = TABLE_WIDTH * zx;
                ApplyBounceCushion(id, Vector3.left * zx);
            }

            if (A.z * zz > TABLE_HEIGHT)
            {
                currentBallPositions[id].z = TABLE_HEIGHT * zz;
                ApplyBounceCushion(id, Vector3.back * zz);
            }
        }

        /// <summary>
        /// Advance simulation 1 step for ball id
        /// </summary>
        /// <param name="ballID"></param>
        private void AdvanceSimulationForBall(int ballID)
        {
            // Since v1.5.0
            Vector3 V = currentBallVelocities[ballID];
            Vector3 W = currentAngularVelocities[ballID];

            // Equations derived from: http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.89.4627&rep=rep1&type=pdf
            //
            // R: Contact location with ball and floor aka: (0,-r,0)
            // µₛ: Slipping friction coefficient
            // µᵣ: Rolling friction coefficient
            // i: Up vector aka: (0,1,0)
            // g: Planet Earth's gravitation acceleration ( 9.80665 )
            //
            // Relative contact velocity (marlow):
            //   c = v + R✕ω
            //
            // Ball is classified as 'rolling' or 'slipping'. Rolling is when the relative velocity is none and the ball is
            // said to be in pure rolling motion
            //
            // When ball is classified as rolling:
            //   Δv = -µᵣ∙g∙Δt∙(v/|v|)
            //
            // Angular momentum can therefore be derived as:
            //   ωₓ = -vᵤ/R
            //   ωᵧ =  0
            //   ωᵤ =  vₓ/R
            //
            // In the slipping state:
            //   Δω = ((-5∙µₛ∙g)/(2/R))∙Δt∙i✕(c/|c|)
            //   Δv = -µₛ∙g∙Δt(c/|c|)

            // Relative contact velocity of ball and table
            Vector3 cv = V + Vector3.Cross(CONTACT_POINT, W);

            // Rolling is achieved when cv's length is approaching 0
            // The epsilon is quite high here because of the fairly large timestep we are working with
            if (cv.magnitude <= 0.1f)
            {
                //V += -F_ROLL * GRAVITY * FIXED_TIME_STEP * V.normalized;
                // (baked):
                V += -0.00122583125f * V.normalized;

                // Calculate rolling angular velocity
                W.x = -V.z * BALL_1OR;

                if (0.3f > Mathf.Abs(W.y))
                {
                    W.y = 0.0f;
                }
                else
                {
                    W.y -= Mathf.Sign(W.y) * 0.3f;
                }

                W.z = V.x * BALL_1OR;

                // Stopping scenario
                if (V.magnitude < 0.01f && W.magnitude < 0.2f)
                {
                    W = vectorZero;
                    V = vectorZero;
                }
                else
                {
                    ballsMoving = true;
                }
            }
            else // Slipping
            {
                Vector3 nv = cv.normalized;

                // Angular slipping friction
                //W += ((-5.0f * F_SLIDE * 9.8f)/(2.0f * 0.03f)) * FIXED_TIME_STEP * Vector3.Cross( Vector3.up, nv );
                // (baked):
                W += -2.04305208f * Vector3.Cross(Vector3.up, nv);
                V += -F_SLIDE * 9.8f * FIXED_TIME_STEP * nv;

                ballsMoving = true;
            }

            currentAngularVelocities[ballID] = W;
            currentBallVelocities[ballID] = V;

            // FSP [22/03/21]: Use the base object's rotation as a factor in the axis. This stops the balls spinning incorrectly.
            ballTransforms[ballID].Rotate((baseObject.transform.rotation * W).normalized,
                W.magnitude * FIXED_TIME_STEP * -Mathf.Rad2Deg, Space.World);
            
            // ball/ball collisions
            for (int i = ballID + 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {

                // If the ball has been pocketed it cannot be collided with.
                if (ballPocketedState[i])
                {
                    continue;
                }

                Vector3 delta = currentBallPositions[i] - currentBallPositions[ballID];
                float dist = delta.magnitude;

                if (dist < BALL_DIAMETER)
                {
                    Vector3 normal = delta / dist;

                    Vector3 velocityDelta = currentBallVelocities[ballID] - currentBallVelocities[i];

                    float dot = Vector3.Dot(velocityDelta, normal);

                    if (dot > 0.0f)
                    {
                        Vector3 reflection = normal * dot;
                        currentBallVelocities[ballID] -= reflection;
                        currentBallVelocities[i] += reflection;

                        // Prevent sound spam if it happens
                        if (currentBallVelocities[ballID].sqrMagnitude > 0 && currentBallVelocities[i].sqrMagnitude > 0)
                        {
                            int clip = UnityEngine.Random.Range(0, hitsSfx.Length - 1);
                            float vol = Mathf.Clamp01(currentBallVelocities[ballID].magnitude * reflection.magnitude);
                            ballPoolTransforms[ballID].position = ballTransforms[ballID].position;
                            ballPool[ballID].PlayOneShot(hitsSfx[clip], vol);
                        }

                        // First hit detected
                        if (ballID == 0)
                        {
                            if (isFourBall)
                            {
                                if (isKorean) // KR 사구 ( Sagu )
                                {
                                    if (i == 9)
                                    {
                                        if (!isMadeFoul)
                                        {
                                            isMadeFoul = true;
                                            scores[Convert.ToInt32(newIsTeam2Turn)]--;

                                            if (scores[Convert.ToInt32(newIsTeam2Turn)] < 0)
                                            {
                                                scores[Convert.ToInt32(newIsTeam2Turn)] = 0;
                                            }

                                            SpawnMinusOne(ballTransforms[i]);
                                        }
                                    }
                                    else if (isFirstHit == 0)
                                    {
                                        isFirstHit = i;
                                    }
                                    else if (i != isFirstHit)
                                    {
                                        if (isSecondHit == 0)
                                        {
                                            isSecondHit = i;
                                            OnLocalCaromPoint(ballTransforms[i]);
                                        }
                                    }
                                }
                                else // JP 四つ玉 ( Yotsudama )
                                {
                                    if (isFirstHit == 0)
                                    {
                                        isFirstHit = i;
                                    }
                                    else if (isSecondHit == 0)
                                    {
                                        if (i != isFirstHit)
                                        {
                                            isSecondHit = i;
                                            OnLocalCaromPoint(ballTransforms[i]);
                                        }
                                    }
                                    else if (isThirdHit == 0)
                                    {
                                        if (i != isFirstHit && i != isSecondHit)
                                        {
                                            isThirdHit = i;
                                            OnLocalCaromPoint(ballTransforms[i]);
                                        }
                                    }
                                }
                            }
                            else if (isFirstHit == 0)
                            {
                                isFirstHit = i;
                            }
                        }
                    }
                }
            }
        }

        // TODO: This is a single-use function we can refactor. Note that its use is to equate a bool,
        //       so it's more acceptable to hold on to.
        // ( Since v0.2.0a ) Check if we can predict a collision before move update happens to improve accuracy
        private bool IsCollisionWithCueBallInevitable()
        {
            // Get what will be the next position
            Vector3 originalDelta = currentBallVelocities[0] * FIXED_TIME_STEP;
            Vector3 norm = currentBallVelocities[0].normalized;

            Vector3 h;
            float lf, s, nmag;

            // Closest found values
            float minlf = 9999999.0f;
            int minid = 0;
            float mins = 0;


            // Loop balls look for collisions
            for (int i = 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {

                if (!ballPocketedState[i])
                {
                    continue;
                }

                h = currentBallPositions[i] - currentBallPositions[0];
                lf = Vector3.Dot(norm, h);
                s = BALL_DSQRPE - Vector3.Dot(h, h) + (lf * lf);

                if (s < 0.0f)
                {
                    continue;
                }

                if (lf < minlf)
                {
                    minlf = lf;
                    minid = i;
                    mins = s;
                }
            }

            if (minid > 0)
            {
                nmag = minlf - Mathf.Sqrt(mins);

                // Assign new position if got appropriate magnitude
                if (nmag * nmag < originalDelta.sqrMagnitude)
                {
                    currentBallPositions[0] += norm * nmag;
                    return true;
                }
            }

            return false;
        }

        // TODO: This is a single-use function we can refactor. Note that its use is to equate a bool,
        //       so it's more acceptable to hold on to.
        private bool IsIntersectingWithSphere(Vector3 start, Vector3 dir, Vector3 sphere)
        {
            Vector3 nrm = dir.normalized;
            Vector3 h = sphere - start;
            float lf = Vector3.Dot(nrm, h);
            float s = BALL_RADIUS_SQUARED - Vector3.Dot(h, h) + (lf * lf);

            if (s < 0.0f)
            {
                return false;
            }

            s = Mathf.Sqrt(s);

            if (lf < s)
            {
                if (lf + s >= 0)
                {
                    s = -s;
                }
                else
                {
                    return false;
                }
            }

            raySphereOutput = start + (nrm * (lf - s));
            return true;
        }

        /// <summary>
        /// Find the lowest numbered ball, that isnt the cue, on the table
        /// This function finds the VISUALLY represented lowest ball,
        /// since 8 has id 1, the search needs to be split
        /// </summary>
        /// <param name="field"></param>
        private int GetLowestNumberedBall(bool[] field)
        {
            for (int i = 2; i <= 8; i++)
            {
                if (!field[i])
                {
                    return i;
                }
            }

            if (!field[1]) //8 ball
            {
                return 1;
            }

            for (int i = 9; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                if (!field[i])
                {
                    return i;
                }
            }

            // ??
            return 0;
        }

        private void OnTurnOverGameWon(bool newIsTeam2Winner)
        {
            if (logger)
            {
                logger._Log(name, "OnTurnOverGameWon");
            }

            isGameInMenus = true;
            poolCues[0].tableIsActive = false;
            poolCues[1].tableIsActive = false;

            isTeam2Winner = newIsTeam2Winner;

            RefreshNetworkData(newIsTeam2Turn);
        }

        private void OnTurnOverFoul()
        {
            if (logger)
            {
                logger._Log(name, "OnTurnOverFoul");
            }

            isFoul = true;
            isPlayerAllowedToPlay = true;

            RefreshNetworkData(!newIsTeam2Turn);
        }

        /// <summary>
        /// Copy current values to previous values
        /// </summary>
        private void CopyGameStateToOldState()
        {
            oldOpen = isOpen;
            oldIsGameInMenus = isGameInMenus;
            oldGameID = gameID;
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

            poolCues[0].localPlayerIsInDesktopTopDownView = false;
            poolCues[1].localPlayerIsInDesktopTopDownView = false;

            Networking.LocalPlayer.Immobilize(false);

            if (isGameModePractice)
            {
                poolCues[0]._Respawn();
                poolCues[1]._Respawn();
            }
        }

        // TODO: Single use function, but it short-circuits so cannot be easily put into its using function.
        private void HandleUpdatingDesktopViewUI()
        {
            if (isEnteringDesktopModeThisFrame)
            {
                isEnteringDesktopModeThisFrame = false;
                return;
            }

            deskTopCursor.x = Mathf.Clamp
            (
                deskTopCursor.x + (Input.GetAxis("Mouse X") * DESKTOP_CURSOR_SPEED),
                -desktopClampX,
                desktopClampX
            );
            deskTopCursor.z = Mathf.Clamp
            (
                deskTopCursor.z + (Input.GetAxis("Mouse Y") * DESKTOP_CURSOR_SPEED),
                -desktopClampY,
                desktopClampY
            );

            if (isDesktopLocalTurn)
            {
                Vector3 ncursor = deskTopCursor;
                ncursor.y = 0.0f;
                Vector3 delta = ncursor - currentBallPositions[0];
                newDesktopCue = Convert.ToInt32(newIsTeam2Turn);
                GameObject cue = desktopCueParents[newDesktopCue];

                if (isGameModePractice && newDesktopCue != oldDesktopCue)
                {
                    poolCues[oldDesktopCue]._Respawn();
                    oldDesktopCue = newDesktopCue;
                }

                if (Input.GetButton("Fire1"))
                {
                    if (!isDesktopShootingIn)
                    {
                        isDesktopShootingIn = true;

                        // Create shooting vector
                        desktopShootVector = delta.normalized;

                        // Project reference start point
                        desktopShootReference = Vector3.Dot(desktopShootVector, ncursor);

                        // Create copy of cursor for later
                        desktopSafeRemovePoint = deskTopCursor;

                        // Unlock cursor position from table
                        desktopClampX = Mathf.Infinity;
                        desktopClampY = Mathf.Infinity;
                    }

                    // Calculate shoot amount via projection
                    shootAmt = desktopShootReference - Vector3.Dot(desktopShootVector, ncursor);
                    isDesktopSafeRemove = shootAmt < 0.0f;

                    shootAmt = Mathf.Clamp(shootAmt, 0.0f, 0.5f);

                    // Set delta back to dkShootVector
                    delta = desktopShootVector;

                    // Disable cursor in shooting mode
                    if (desktopCursorObject)
                    {
                        desktopCursorObject.SetActive(false);
                    }
                    float vel = Mathf.Pow(shootAmt * 2.0f, 1.4f) * 9.0f;
                    StartPreviewSim(shootAmt);
                }
                else
                {
                    // Trigger shot
                    if (isDesktopShootingIn)
                    {
                        // Shot cancel
                        if (!isDesktopSafeRemove)
                        {
                            // Fake hit ( kinda )
                            float vel = Mathf.Pow(shootAmt * 2.0f, 1.4f) * 9.0f;

                            currentBallVelocities[0] = desktopShootVector * vel;

                            Vector3 r_1 = (raySphereOutput - currentBallPositions[0]) * BALL_1OR;
                            Vector3 p = desktopShootVector.normalized * vel;
                            currentAngularVelocities[0] = Vector3.Cross(r_1, p) * -25.0f;
                            cue.transform.localPosition = new Vector3(2000.0f, 2000.0f, 2000.0f);
                            isDesktopLocalTurn = false;
                            _StoreShot();
                            HitBallWithCue();
                        }

                        // Restore cursor position
                        deskTopCursor = desktopSafeRemovePoint;
                        desktopClampX = TABLE_WIDTH;
                        desktopClampY = TABLE_HEIGHT;

                        // 1-frame override to fix rotation
                        delta = desktopShootVector;
                    }

                    isDesktopShootingIn = false;
                    shootAmt = 0.0f;

                    if (desktopCursorObject)
                    {
                        desktopCursorObject.SetActive(true);
                    }
                }

                if (Input.GetKey(KeyCode.W))
                {
                    desktopHitCursor += Vector3.forward * Time.deltaTime;
                }

                if (Input.GetKey(KeyCode.S))
                {
                    desktopHitCursor += Vector3.back * Time.deltaTime;
                }

                if (Input.GetKey(KeyCode.A))
                {
                    desktopHitCursor += Vector3.left * Time.deltaTime;
                }

                if (Input.GetKey(KeyCode.D))
                {
                    desktopHitCursor += Vector3.right * Time.deltaTime;
                }

                // Clamp in circle
                if (desktopHitCursor.magnitude > 0.90f)
                {
                    desktopHitCursor = desktopHitCursor.normalized * 0.9f;
                }

                desktopHitPosition.transform.localPosition = desktopHitCursor;

                // Create rotation
                Quaternion xr = Quaternion.AngleAxis(10.0f, Vector3.right);
                Quaternion r = Quaternion.AngleAxis(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, Vector3.up);

                Vector3 worldHit = new Vector3(desktopHitCursor.x * BALL_PL_X, desktopHitCursor.z * BALL_PL_X,
                    -0.89f - shootAmt);

                cue.transform.localRotation = r * xr;
                cue.transform.position =
                    gameObject.transform.TransformPoint(currentBallPositions[0] + (r * xr * worldHit));
            }

            desktopCursorObject.transform.localPosition = deskTopCursor;
            desktopOverlayPower.transform.localScale = new Vector3(1.0f - (shootAmt * 2.0f), 1.0f, 1.0f);
        }

        private void HitBallWithCue()
        {
            if (logger)
            {
                logger._Log(name, "disabling marker because the ball hase been hit");
            }
            if (logger)
            {
                logger._Log(name, "Cue Velocity: " + currentBallVelocities[0] + "  Cue Angular Velocity: " + currentAngularVelocities[0]);
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

            if (isPreviewSimEnabled)
            {
                PreviewCleanup();
            }

            // Remove locks
            _EndHit();
            isPlayerAllowedToPlay = false;
            isFoul = false; // In case did not drop foul marker

            // Commit changes
            gameIsSimulating = true;
            oldBallPocketedState = (bool[])ballPocketedState.Clone();

            // Make sure we definately are the network owner
            Networking.SetOwner(localPlayer, gameObject);

            RefreshNetworkData(newIsTeam2Turn);

            isSimulatedByUs = true;
            
            
            float vol = Mathf.Clamp(currentBallVelocities[0].magnitude * 0.1f, 0f, 0.6f);
            cueTipSrc.transform.position = cueTipTransform.position;
            cueTipSrc.PlayOneShot(hitBallSfx, vol);
        }

        private void UpdateScores()
        {
            if (logger)
            {
                logger._Log(name, "UpdateScores");
            }

            if (isFourBall)
            {
                poolMenu._SetScore(false, scores[0]);
                poolMenu._SetScore(true, scores[1]);
            }
            else if (isNineBall)
            {
                poolMenu._SetScore(false, -1);
                poolMenu._SetScore(true, -1);
            }
            else // 8 Ball
            {
                int[] counters = new int[2];


                for (int j = 2; j < 9; j++) //Solids
                {
                    if (ballPocketedState[j])
                    {
                        counters[isPlayer2Solids ? 0 : 1]++;
                    }
                }

                for (int j = 10; j < 16; j++) //Stripes
                {
                    if (ballPocketedState[j])
                    {
                        counters[isPlayer2Solids ? 1 : 0]++;
                    }
                }

                if (isGameInMenus)
                {
                    counters[isTeam2Winner ? 1 : 0] += ballPocketedState[2] ? 1 : 0;
                }

                poolMenu._SetScore(false, counters[0]);
                poolMenu._SetScore(true, counters[1]);
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
            if (!Networking.IsOwner(localPlayer, gameObject) || !VRC.SDKBase.Utilities.IsValid(player))
            {
                return;
            }

            int playerID = player.playerId;
            if (playerID == player1ID || playerID == player2ID || playerID == player3ID || playerID == player4ID)
            {
                if (isGameInMenus)
                {
                    RemovePlayerFromGame(playerID);
                }
                else
                {
                    gameWasReset = true;
                    _Reset();
                }
            }
        }

        /// <Summary> The object that contains the UI that appears when you can press E to enter the destop top-down view. </Summary>
        [Tooltip("The object that contains the UI that appears when you can press E to enter the destop top-down view")]
        public GameObject pressE;

        /// <Summary> Is the local player near the table? </Summary>
        private bool isNearTable;

        /// <Summary> Can the local player enter the desktop top-down view?
        private bool canEnterDesktopTopDownView;

        [HideInInspector] public ushort numberOfCuesHeldByLocalPlayer;

        public void _LocalPlayerEnteredAreaNearTable()
        {
            if (!isPlayerInVR)
            {
                isNearTable = true;
                CheckIfCanEnterDesktopTopDownView();
            }
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
            if (isNearTable && numberOfCuesHeldByLocalPlayer > 0)
            {
                canEnterDesktopTopDownView = true;

                if (pressE)
                {
                    pressE.SetActive(true);
                }
            }
        }

        private void CannotEnterDesktopTopDownView()
        {
            canEnterDesktopTopDownView = false;

            if (pressE)
            {
                pressE.SetActive(false);
            }
        }

        #region UdonChipsMethods

        public void _Raise()
        {
            if (!enableUdonChips || PlayerCount != 1)
            {
                return;
            }

            if (logger)
            {
                logger._Log(name, $"Raise to {price * (raiseCount + 1)}");
            }

            if (!Pay(price))
            {
                return;
            }

            Networking.SetOwner(localPlayer, gameObject);
            raiseCount++;
            RefreshNetworkData(false);
        }

        private void PlayAudioClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            mainAudioSource.PlayOneShot(clip);
        }

        private void PayBack(float value)
        {
            if (!enableUdonChips)
            {
                return;
            }

            if (logger)
            {
                logger._Log(name, $"Payback {value}");
            }

            PlayAudioClip(paySound);
            Money += value;

            if (localPlayerID == 0)
            {
                raiseCount = 1;
            }
        }

        private bool Pay(float value)
        {
            if (!enableUdonChips)
            {
                return true;
            }

            if (Money < value)
            {
                if (logger) logger._Log(name, "Insufficient funds");
                PlayAudioClip(insufficientFundsSound);
                return false;
            }

            if (logger) logger._Log(name, $"Pay {value}");

            PlayAudioClip(paySound);
            Money -= value;

            return true;
        }

        private void GiveRewards()
        {
            if (!enableUdonChips)
            {
                return;
            }

            bool isSinglePlayer = PlayerCount == 1;
            bool isWinner = playerIsTeam2 && isTeam2Winner || !playerIsTeam2 && !isTeam2Winner;
            if (!isSinglePlayer && !isWinner)
            {
                return;
            }

            float total = isSinglePlayer ? singlePlayPrize : prize * raiseCount;
            if (logger)
            {
                logger._Log(name, $"Give rewards {total}");
            }

            PlayAudioClip(paySound);
            Money += total;
        }

        #endregion
        
        #region preShotSim

        /// <summary>
        /// Finds the trail renderer for the given ball. Done this way because prefabs are annoying to update;
        /// </summary>
        private void FindTrails()
        {
            Transform trailParent = transform.Find("Trails");
            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                string targetBall = "ball"+i.ToString("00");
                previewTrails[i] = trailParent.Find(targetBall).GetComponent<TrailRenderer>();
            }
        }
        /// <summary>
        /// Resets and Sets up the preview sim for the current shot.
        /// </summary>
        /// <param name="cueDistance"></param>
        private void StartPreviewSim(float cueDistance)
        {
            if (!isPreviewSimEnabled)
            {
                return;
            }
            // Only Start the sim if the cue has moved.
            else if (Mathf.Abs(cueDistance - previewPreviousCueDistance) <= 0.01f)
            {
                return;
            }
            else if (logger)
            {
                logger._Log(name,"StartPreviewSim");
            }
            previewPreviousCueDistance = cueDistance;
            // Copy current state
            previewPocketedState[0] = (bool[]) ballPocketedState.Clone();
            previewBallPosition[0] = (Vector3[]) currentBallPositions.Clone();
            previewBallVelocity[0] = new Vector3[NUMBER_OF_SIMULATED_BALLS];
            previewAngularVelocities[0] = new Vector3[NUMBER_OF_SIMULATED_BALLS];
            
            if (previewOnlyCueBall)
            {
                previewTrails[0].transform.localPosition = previewBallPosition[0][0];
                previewTrails[0].Clear();
            }
            else
            {
                for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
                {
                    previewTrails[i].transform.localPosition = previewBallPosition[0][i];
                    previewTrails[i].material.color = ballColors[i];// todo: 4 ball colors
                    previewTrails[i].Clear();
                }
            }
            previewCurrentIteration = 0;
            isPreviewSimRunning = true;
            
            // Angular velocity: L=r(normalized)×p
            Vector3 r = (raySphereOutput - previewBallPosition[0][0]) * BALL_1OR; // Wah
            Vector3 p;
            float vel;
            if (isPlayerInVR) // Pain peko
            {
                vel = forceMultiplier *
                            (raySphereOutput * Mathf.Lerp(0f, CUE_VELOCITY_CLAMP, Mathf.InverseLerp(0f, 0.3f, cueDistance))).magnitude;
                p = cueLocalForwardDirection * vel;
                previewBallVelocity[0][0] = cueArmedShotDirection * Mathf.Min(vel, CUE_VELOCITY_CLAMP);
                previewAngularVelocities[0][0] = Vector3.Cross(r, p) * -50.0f;
            }
            else
            {
                Vector3 ncursor = deskTopCursor;
                ncursor.y = 0.0f;
                Vector3 delta = ncursor - previewBallPosition[0][0];
                shootAmt = desktopShootReference - Vector3.Dot(desktopShootVector, ncursor);
                shootAmt = Mathf.Clamp(shootAmt, 0.0f, 0.5f);
                vel = Mathf.Pow(shootAmt * 2.0f, 1.4f) * 9.0f;
                p = desktopShootVector.normalized * vel;
                previewBallVelocity[0][0] = p;
                previewAngularVelocities[0][0] = Vector3.Cross(r, p) * -25.0f;

            }
            logger._Log(name, "Asumed Velocity: " + p.ToString()+ " Asumed Angular Velocity: " + previewAngularVelocities[0][0].ToString());
        }
        /// <summary>
        /// Runs the preview sim for the current shot.
        /// </summary>
        private void RunPreviewSim()
        {
            int loops = 0;
            while (isPreviewSimRunning && loops < 5)
            {
                loops++;
                previewCurrentIteration++;
                //return previous state
                previewBallPosition[previewCurrentIteration] =
                    (Vector3[])previewBallPosition[previewCurrentIteration - 1].Clone();
                previewBallVelocity[previewCurrentIteration] =
                    (Vector3[])previewBallVelocity[previewCurrentIteration - 1].Clone();
                previewPocketedState[previewCurrentIteration] =
                    (bool[])previewPocketedState[previewCurrentIteration - 1].Clone();
                previewAngularVelocities[previewCurrentIteration] =
                    (Vector3[])previewAngularVelocities[previewCurrentIteration - 1].Clone();
                PreviewAdvancePhysicsStep();
                
                //End if sim runs too long
                if (PREVIEW_MAX_ITERATIONS_TOTAL_LIMIT - 2 < previewCurrentIteration)
                {
                    isPreviewSimRunning = false;
                    if (logger)
                    {
                        logger._Log(name, "RunPreviewSimFinished");
                    }
                    break;
                }
            }
        }

        //TODO: Merge into a single version. Preferable with ref/out in U# 1.0
        
        private void PreviewAdvancePhysicsStep()
        {
            // Run main simulation / inter-ball collision
            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                if (!previewPocketedState[previewCurrentIteration][i]) // If the ball in question is not sunk
                {
                    // Update position
                    previewBallPosition[previewCurrentIteration][i] += previewBallVelocity[previewCurrentIteration][i] * FIXED_TIME_STEP;
                    PreviewAdvanceSimulationForBall(i);
                    if (previewOnlyCueBall && i > 0)
                    {
                        continue;
                    }
                    // Update Trails
                    Vector3 pos = transform.TransformPoint(previewBallPosition[previewCurrentIteration][i]);
                    previewTrails[i].AddPosition(pos);
                    previewTrails[i].transform.position = pos;
                }
            }

            if (isFourBall)
            {
                PreviewBounceBallOffCushionIfApplicable(0);
                PreviewBounceBallOffCushionIfApplicable(2);
                PreviewBounceBallOffCushionIfApplicable(3);
                PreviewBounceBallOffCushionIfApplicable(9);

                return;
            }


            // Run edge collision
            for (int index = 0; index < NUMBER_OF_SIMULATED_BALLS; index++)
            {
                if (!previewPocketedState[previewCurrentIteration][index])
                {
                    float zy, zx, zk, zw, d, k, i, j, l, r;
                    Vector3 A, N;

                    A = previewBallPosition[previewCurrentIteration][index];

                    // REGIONS
                    /*
                        *  QUADS:							SUBSECTION:				SUBSECTION:
                        *    zx, zy:							zz:						zw:
                        *
                        *  o----o----o  +:  1			\_________/				\_________/
                        *  | -+ | ++ |  -: -1		     |	    /		              /  /
                        *  |----+----|					  -  |  +   |		      -     /   |
                        *  | -- | +- |						  |	   |		          /  +  |
                        *  o----o----o						  |      |             /       |
                        *
                        */

                    // Setup major regions
                    zx = Mathf.Sign(A.x);
                    zy = Mathf.Sign(A.z);

                    // within pocket regions
                    if ((A.z * zy > (TABLE_HEIGHT - POCKET_RADIUS)) &&
                        (A.x * zx > (TABLE_WIDTH - POCKET_RADIUS) || A.x * zx < POCKET_RADIUS))
                    {
                        // Subregions
                        zw = A.z * zy > (A.x * zx) - TABLE_WIDTH + TABLE_HEIGHT ? 1.0f : -1.0f;

                        // Normalization / line coefficients change depending on sub-region
                        if (A.x * zx > TABLE_WIDTH * 0.5f)
                        {
                            zk = 1.0f;
                            r = ONE_OVER_ROOT_TWO;
                        }
                        else
                        {
                            zk = -2.0f;
                            r = ONE_OVER_ROOT_FIVE;
                        }

                        // Collider line EQ
                        d = zx * zy * zk; // Coefficient
                        k = (-(TABLE_WIDTH * Mathf.Max(zk, 0.0f)) + (POCKET_RADIUS * zw * Mathf.Abs(zk)) +
                             TABLE_HEIGHT) * zy; // Konstant

                        // Check if colliding
                        l = zw * zy;
                        if (A.z * l > ((A.x * d) + k) * l)
                        {
                            // Get line normal
                            N.x = zx * zk;
                            N.z = -zy;
                            N.y = 0.0f;
                            N *= zw * r;

                            // New position
                            i = ((A.x * d) + A.z - k) / (2.0f * d);
                            j = (i * d) + k;

                            previewBallPosition[previewCurrentIteration][index].x = i;
                            previewBallPosition[previewCurrentIteration][index].z = j;

                            // Reflect velocity
                            PreviewApplyBounceCushion(index, N);
                        }
                    }
                    else // edges
                    {
                        if (A.x * zx > TABLE_WIDTH)
                        {
                            previewBallPosition[previewCurrentIteration][index].x = TABLE_WIDTH * zx;
                            PreviewApplyBounceCushion(index, Vector3.left * zx);
                        }

                        if (A.z * zy > TABLE_HEIGHT)
                        {
                            previewBallPosition[previewCurrentIteration][index].z = TABLE_HEIGHT * zy;
                            PreviewApplyBounceCushion(index, Vector3.back * zy);
                        }
                    }
                }
            }

            // Run triggers
            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                if (!previewPocketedState[previewCurrentIteration][i])
                {
                    float zz, zx;
                    Vector3 A = previewBallPosition[previewCurrentIteration][i];

                    // Setup major regions
                    zx = Mathf.Sign(A.x);
                    zz = Mathf.Sign(A.z);

                    // Its in a pocket
                    if (
                        A.z * zz > TABLE_HEIGHT + POCKET_DEPTH ||
                        A.z * zz > (A.x * -zx) + TABLE_WIDTH + TABLE_HEIGHT + POCKET_DEPTH
                    )
                    {
                        int total = 0;

                        // Get total for X positioning
                        int count_extent = isNineBall ? 10 : NUMBER_OF_SIMULATED_BALLS;
                        for (int j = 1; j < count_extent; j++)
                        {
                            total += previewPocketedState[previewCurrentIteration][i] ? 1 : 0;
                        }

                        // set this for later
                        previewBallPosition[previewCurrentIteration][i].x = -0.9847f + (total * BALL_DIAMETER);
                        previewBallPosition[previewCurrentIteration][i].z = 0.768f;

                        // This is where we actually save the pocketed/non-pocketed state of balls.
                        previewPocketedState[previewCurrentIteration][i] = true;
                    }
                }
            }
        }

        private void PreviewAdvanceSimulationForBall(int ballID)
        {
            // Since v1.5.0
            Vector3 V = previewBallVelocity[previewCurrentIteration][ballID];
            Vector3 W = previewAngularVelocities[previewCurrentIteration][ballID];

            // Equations derived from: http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.89.4627&rep=rep1&type=pdf
            //
            // R: Contact location with ball and floor aka: (0,-r,0)
            // µₛ: Slipping friction coefficient
            // µᵣ: Rolling friction coefficient
            // i: Up vector aka: (0,1,0)
            // g: Planet Earth's gravitation acceleration ( 9.80665 )
            //
            // Relative contact velocity (marlow):
            //   c = v + R✕ω
            //
            // Ball is classified as 'rolling' or 'slipping'. Rolling is when the relative velocity is none and the ball is
            // said to be in pure rolling motion
            //
            // When ball is classified as rolling:
            //   Δv = -µᵣ∙g∙Δt∙(v/|v|)
            //
            // Angular momentum can therefore be derived as:
            //   ωₓ = -vᵤ/R
            //   ωᵧ =  0
            //   ωᵤ =  vₓ/R
            //
            // In the slipping state:
            //   Δω = ((-5∙µₛ∙g)/(2/R))∙Δt∙i✕(c/|c|)
            //   Δv = -µₛ∙g∙Δt(c/|c|)

            // Relative contact velocity of ball and table
            Vector3 cv = V + Vector3.Cross(CONTACT_POINT, W);

            // Rolling is achieved when cv's length is approaching 0
            // The epsilon is quite high here because of the fairly large timestep we are working with
            if (cv.magnitude <= 0.1f)
            {
                //V += -F_ROLL * GRAVITY * FIXED_TIME_STEP * V.normalized;
                // (baked):
                V += -0.00122583125f * V.normalized;

                // Calculate rolling angular velocity
                W.x = -V.z * BALL_1OR;

                if (0.3f > Mathf.Abs(W.y))
                {
                    W.y = 0.0f;
                }
                else
                {
                    W.y -= Mathf.Sign(W.y) * 0.3f;
                }

                W.z = V.x * BALL_1OR;

                // Stopping scenario
                if (V.magnitude < 0.01f && W.magnitude < 0.2f)
                {
                    W = vectorZero;
                    V = vectorZero;
                }
            }
            else // Slipping
            {
                Vector3 nv = cv.normalized;

                // Angular slipping friction
                //W += ((-5.0f * F_SLIDE * 9.8f)/(2.0f * 0.03f)) * FIXED_TIME_STEP * Vector3.Cross( Vector3.up, nv );
                // (baked):
                W += -2.04305208f * Vector3.Cross(Vector3.up, nv);
                V += -F_SLIDE * 9.8f * FIXED_TIME_STEP * nv;
            }

            previewAngularVelocities[previewCurrentIteration][ballID] = W;
            previewBallVelocity[previewCurrentIteration][ballID] = V;

            // FSP [22/03/21]: Use the base object's rotation as a factor in the axis. This stops the balls spinning incorrectly.
            //ballTransforms[ballID].Rotate((baseObject.transform.rotation * W).normalized,
            //    W.magnitude * FIXED_TIME_STEP * -Mathf.Rad2Deg, Space.World);

            // ball/ball collisions
            for (int i = ballID + 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                // If the ball has been pocketed it cannot be collided with.
                if (previewPocketedState[previewCurrentIteration][i])
                {
                    continue;
                }

                Vector3 delta = previewBallPosition[previewCurrentIteration][i] -
                                previewBallPosition[previewCurrentIteration][ballID];
                float dist = delta.magnitude;

                if (dist < BALL_DIAMETER)
                {
                    Vector3 normal = delta / dist;

                    Vector3 velocityDelta = previewBallVelocity[previewCurrentIteration][ballID] -
                                            previewBallVelocity[previewCurrentIteration][i];

                    float dot = Vector3.Dot(velocityDelta, normal);

                    if (dot > 0.0f)
                    {
                        Vector3 reflection = normal * dot;
                        previewBallVelocity[previewCurrentIteration][ballID] -= reflection;
                        previewBallVelocity[previewCurrentIteration][i] += reflection;
                        
                    }
                }
            }
        }

        private bool PreviewIsCollisionWithCueBallInevitable()
        {
            // Get what will be the next position
            Vector3 originalDelta = previewBallVelocity[previewCurrentIteration][0] * FIXED_TIME_STEP;
            Vector3 norm = previewBallVelocity[previewCurrentIteration][0].normalized;

            Vector3 h;
            float lf, s, nmag;

            // Closest found values
            float minlf = 9999999.0f;
            int minid = 0;
            float mins = 0;


            // Loop balls look for collisions
            for (int i = 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                if (!previewPocketedState[previewCurrentIteration][i])
                {
                    continue;
                }

                h = previewBallPosition[previewCurrentIteration][i] - previewBallPosition[previewCurrentIteration][0];
                lf = Vector3.Dot(norm, h);
                s = BALL_DSQRPE - Vector3.Dot(h, h) + (lf * lf);

                if (s < 0.0f)
                {
                    continue;
                }

                if (lf < minlf)
                {
                    minlf = lf;
                    minid = i;
                    mins = s;
                }
            }

            if (minid > 0)
            {
                nmag = minlf - Mathf.Sqrt(mins);

                // Assign new position if got appropriate magnitude
                if (nmag * nmag < originalDelta.sqrMagnitude)
                {
                    previewBallPosition[previewCurrentIteration][0] += norm * nmag;
                    return true;
                }
            }

            return false;
        }
        private void PreviewApplyBounceCushion(int id, Vector3 N)
        {
            // Mathematical expressions derived from: https://billiards.colostate.edu/physics_articles/Mathavan_IMechE_2010.pdf
            //
            // (Note): subscript gamma, u, are used in replacement of Y and Z in these expressions because
            // unicode does not have them.
            //
            // f = 2/7
            // f₁ = 5/7
            //
            // Velocity delta:
            //   Δvₓ = −vₓ∙( f∙sin²θ + (1+e)∙cos²θ ) − Rωᵤ∙sinθ
            //   Δvᵧ = 0
            //   Δvᵤ = f₁∙vᵤ + fR( ωₓ∙sinθ - ωᵧ∙cosθ ) - vᵤ
            //
            // Aux:
            //   Sₓ = vₓ∙sinθ - vᵧ∙cosθ+ωᵤ
            //   Sᵧ = 0
            //   Sᵤ = -vᵤ - ωᵧ∙cosθ + ωₓ∙cosθ
            //
            //   k = (5∙Sᵤ) / ( 2∙mRA );
            //   c = vₓ∙cosθ - vᵧ∙cosθ
            //
            // Angular delta:
            //   ωₓ = k∙sinθ
            //   ωᵧ = k∙cosθ
            //   ωᵤ = (5/(2m))∙(-Sₓ / A + ((sinθ∙c∙(e+1)) / B)∙(cosθ - sinθ));
            //
            // These expressions are in the reference frame of the cushion, so V and ω inputs need to be rotated

            // Reject bounce if velocity is going the same way as normal
            // this state means we tunneled, but it happens only on the corner
            // vertexes
            Vector3 source_v = previewBallVelocity[previewCurrentIteration][id];
            if (Vector3.Dot(source_v, N) > 0.0f)
            {
                return;
            }

            // Rotate V, W to be in the reference frame of cushion
            Quaternion rq = Quaternion.AngleAxis(Mathf.Atan2(-N.z, -N.x) * Mathf.Rad2Deg, Vector3.up);
            Quaternion rb = Quaternion.Inverse(rq);
            Vector3 V = rq * source_v;
            Vector3 W = rq * previewAngularVelocities[previewCurrentIteration][id];

            Vector3 V1;
            Vector3 W1;
            float k, c, s_x, s_z;

            //V1.x = -V.x * ((2.0f/7.0f) * SINA2 + EP1 * COSA2) - (2.0f/7.0f) * BALL_PL_X * W.z * SINA;
            //V1.z = (5.0f/7.0f)*V.z + (2.0f/7.0f) * BALL_PL_X * (W.x * SINA - W.y * COSA) - V.z;
            //V1.y = 0.0f;
            // (baked):
            V1.x = (-V.x * F) - (0.00240675711f * W.z);
            V1.z = (0.71428571428f * V.z) + (0.00857142857f * ((W.x * SIN_A) - (W.y * COS_A))) - V.z;
            V1.y = 0.0f;

            // s_x = V.x * SINA - V.y * COSA + W.z;
            // (baked): y component not used:
            s_x = (V.x * SIN_A) + W.z;
            s_z = -V.z - (W.y * COS_A) + (W.x * SIN_A);

            // k = (5.0f * s_z) / ( 2 * BALL_MASS * A );
            // (baked):
            k = s_z * 0.71428571428f;

            // c = V.x * COSA - V.y * COSA;
            // (baked): y component not used
            c = V.x * COS_A;

            W1.x = k * SIN_A;

            //W1.z = (5.0f / (2.0f * BALL_MASS)) * (-s_x / A + ((SINA * c * EP1) / B) * (COSA - SINA));
            // (baked):
            W1.z = 15.625f * ((-s_x * 0.04571428571f) + (c * 0.0546021744f));
            W1.y = k * COS_A;

            // Unrotate result
            previewBallVelocity[previewCurrentIteration][id] += rb * V1;
            previewAngularVelocities[previewCurrentIteration][id] += rb * W1;
        }
        private void PreviewBounceBallOffCushionIfApplicable(int id)
        {
            float zz, zx;
            Vector3 A = previewBallPosition[previewCurrentIteration][id];

            // Setup major regions
            zx = Mathf.Sign(A.x);
            zz = Mathf.Sign(A.z);

            if (A.x * zx > TABLE_WIDTH)
            {
                previewBallPosition[previewCurrentIteration][id].x = TABLE_WIDTH * zx;
                PreviewApplyBounceCushion(id, Vector3.left * zx);
            }

            if (A.z * zz > TABLE_HEIGHT)
            {
                previewBallPosition[previewCurrentIteration][id].z = TABLE_HEIGHT * zz;
                PreviewApplyBounceCushion(id, Vector3.back * zz);
            }
        }
        /// <summary>
        /// Cleans up the preview trails
        /// </summary>
        private void PreviewCleanup()
        {
            if (logger)
            {
                logger._Log(name, "PreviewCleanup");
            }
            isPreviewSimRunning = false;
            foreach (TrailRenderer trail in previewTrails)
            {
                trail.material.color = tableWhite;
                trail.Clear();
            }
        }
        public void _EnablePreviewSim()
        {
            if (logger)
            {
                logger._Log(name, "EnablePreviewSim");
            }
            Networking.SetOwner(localPlayer, gameObject);
            isPreviewSimEnabled = true;
            RefreshNetworkData(false);        }

        public void _DisablePreviewSim()
        {
            if (logger)
            {
                logger._Log(name, "DisablePreviewSim");
            }
            Networking.SetOwner(localPlayer, gameObject);
            isPreviewSimEnabled = false;
            PreviewCleanup();
            RefreshNetworkData(false);
        }
        #endregion
        /// <summary>
        /// Reruns the simulation for the current turn. Result should be the same.
        /// </summary>
        public void _ReplayPhysics()
        {
            if (!gameIsSimulating && currentTurn >= 0)
            {
                if (logger)
                {
                    logger._Log(name, "ReplayPhysics");
                }
                isArmed = false;
                isReplaying = true;
                gameIsSimulating = true;
                currentAngularVelocities = new Vector3[NUMBER_OF_SIMULATED_BALLS];
                currentBallVelocities = new Vector3[NUMBER_OF_SIMULATED_BALLS];
                // Restore to state from the end of last turn.
                currentBallPositions = (Vector3[])previousBallPositions[currentTurn-1].Clone();
                currentBallVelocities[0] = initialCueBallVelocity[currentTurn];
                currentAngularVelocities[0] = initialCueBallAngularVelocity[currentTurn];
                ballPocketedState = (bool[])previousBallPocketedState[currentTurn].Clone();
            }
        }

        #region turnUndo
        /// <summary>
        /// Store the state of the game at the start of the current turn.
        /// </summary>
        public void _StoreShot()
        {
            if (!isGameModePractice || isReplaying) return;
            else if (logger)
            {
                logger._Log(name, "StoreShot");
            }
            currentTurn++;
            latestTurn = currentTurn;
            //for physics replay, store initial state that matters
            initialCueBallVelocity[currentTurn] = currentBallVelocities[0];
            initialCueBallAngularVelocity[currentTurn] = currentAngularVelocities[0];
        }

        public void _StoreEndTurn()
        {
            if (!isGameModePractice) return;
            if (isReplaying)
            {
                isReplaying = false;
                return;
            }
            else if (logger)
            {
                logger._Log(name, "StoreEndTurn");
            }
            //For turn undo
            previousIsOpen[currentTurn] = isOpen;
            previousPlayerTeam2[currentTurn] = playerIsTeam2;
            previousIsPlayer2Solids[currentTurn] = isPlayer2Solids;
            previousIsTeam2Turn[currentTurn] = newIsTeam2Turn;
            previousBallPocketedState[currentTurn] = (bool[])ballPocketedState.Clone();
            previousBallPositions[currentTurn] = (Vector3[])currentBallPositions.Clone();
        }
        /// <summary>
        /// Undo the last turn.
        /// </summary>
        public void _UndoTurn()
        {
            if (!gameIsSimulating && !isGameInMenus && currentTurn > -1 && isGameModePractice)
            {
                if (logger)
                {
                    logger._Log(name, "UndoTurn");
                }
                if (currentTurn > 0) // allow undoing the first turn
                {
                    currentTurn--;
                }
                // Return since state is not available.
                if (previousIsOpen[currentTurn] == null)
                {
                    if (logger)
                    {
                        logger._Log(name, "UndoTurn: State is not available for turn " + currentTurn);
                    }
                    return;
                }
                isOpen = previousIsOpen[currentTurn];
                playerIsTeam2 = previousPlayerTeam2[currentTurn];
                isPlayer2Solids = previousIsPlayer2Solids[currentTurn];
                newIsTeam2Turn = previousIsTeam2Turn[currentTurn];
                currentBallPositions = previousBallPositions[currentTurn];
                ballPocketedState = previousBallPocketedState[currentTurn];
                RefreshNetworkData(false);
            }
        }
        /// <summary>
        /// Go forward one turn.
        /// </summary>
        public void _RedoTurn()
        {
            if (!gameIsSimulating && !isGameInMenus && currentTurn < latestTurn && isGameModePractice)
            {
                if (logger)
                {
                    logger._Log(name,"RedoTurn");
                }
                currentTurn++;
                if (previousIsOpen[currentTurn] == null)
                {
                    if (logger)
                    {
                        logger._Log(name, "RedoTurn: State is not available for turn " + currentTurn);
                    }
                    return;
                }
                isOpen = previousIsOpen[currentTurn];
                playerIsTeam2 = previousPlayerTeam2[currentTurn];
                isPlayer2Solids = previousIsPlayer2Solids[currentTurn];
                newIsTeam2Turn = previousIsTeam2Turn[currentTurn];
                currentBallPositions = previousBallPositions[currentTurn];
                ballPocketedState = (bool[])previousBallPocketedState[currentTurn].Clone();
                RefreshNetworkData(false);
            }
        }
        /// <summary>
        /// Toggle the turn preSim status
        /// </summary>
        public void _SwitchPreShotMode()
        {
            isPreviewSimEnabled = !isPreviewSimEnabled;
            RefreshNetworkData(false);

        }

        public void _SwitchPreShotCueMode()
        {
            previewOnlyCueBall = !previewOnlyCueBall;
            RefreshNetworkData(false);
        }
        #endregion
    }
}