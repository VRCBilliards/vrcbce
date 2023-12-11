using System;
using System.Runtime.Remoting.Messaging;
using UdonSharp;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Rendering;
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
#endregion

#region Table Dimensions

        /// <summary>
        /// These numbers determine the exact size of your table, and will correspond to the blue, yellow and red
        /// guidance lines you can see in the editor. Theoretically, as long as your table is a cuboid and has six
        /// pockets, we support it.
        /// </summary>
        
        // Note the WIDTH and HEIGHT are halved from their real values - this is due to the reliance of the physics code
        // working in four quadrants to detect collisions.
        public float TABLE_WIDTH = 1.1f;
        public float TABLE_HEIGHT = 0.64f; 
        public Vector3 CORNER_POCKET = new Vector3(1.135f, 0.0f, 0.685f);
        public Vector3 MIDDLE_POCKET = new Vector3(0.0f, 0.0f, 0.72f);
        public float POCKET_RADIUS = 0.11f;
        public float POCKET_INNER_RADIUS = 0.078f;
        public float BALL_DIAMETER = 0.06f;
#endregion
        // The number of balls we simulate - this const allows us to increase the number we support in the future.
        protected const int NUMBER_OF_SIMULATED_BALLS = 16;
        // A small fraction designed to slightly move balls around when placed, which helps with making the table
        // less deterministic (i.e. less "perfect breaks" etc)
        protected const float RANDOMIZE_F = 0.0001f;
        
        // These four consts dictate some placement of balls when placed.
        public float SPOT_POSITION_X = 0.5334f;
        public float SPOT_CAROM_X = 0.8001f;
        public float BALL_PL_X = 0.03f;
        public float BALL_PL_Y = 0.05196152422f;
        
#region Desktop        
        private const float DEFAULT_DESKTOP_CUE_ANGLE = 10.0f;
        protected const float DESKTOP_CURSOR_SPEED = 0.035f;
        private const float MAX_DESKTOP_CUE_ANGLE = 90.0f;
        private const float MIN_DESKTOP_CUE_ANGLE = 0.0f;
#endregion
        [Header("Options")]

        [Tooltip("Use fake shadows? They may clash with your world's lighting.")]
        public bool fakeBallShadows = true;
        [Tooltip("Does the table model for this table have rails that guide the ball when the ball sinks?")]
        public bool tableModelHasRails;

        [Header("Important Objects")]
        public Transform sunkBallsPositionRoot;
        public GameObject shadows;
        public ParticleSystem plusOneParticleSystem;
        public ParticleSystem minusOneParticleSystem;
        // Where's the surface of the table?
        public Transform tableSurface;

        [Header("Shader Information")]
        public string uniformTableColour = "_EmissionColor";
        public string uniformMarkerColour = "_Color";
        public string uniformCueColour = "_EmissionColor";
        public string uniformBallColour = "_BallColour";
        public string uniformBallFloat = "_CustomColor";
        public string ballMaskToggle = "_Turnoff";

        [Tooltip("Change the length of the intro ball-drop animation. If you set this to zero, the animation will not play at all.")]
        [Range(0f, 5f)]
        public float introAnimationLength = 2.0f;

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
        
        [Header("Colour Options")]
        public bool ballCustomColours;
        public ColorPicker blueTeamSliders;
        public ColorPicker orangeTeamSliders;

        private float shaderToggleFloat = 0;

        [Header("Cues")]
        public GameObject cueTip;
        public GameObject[] cueTips;
        private Transform cueTipTransform;
        public MeshRenderer[] cueRenderObjs;
        private Material[] cueMaterials = new Material[2];

        /// <summary>
        /// The balls that are used by the table.
        /// The order of the balls is as follows: cue, black, all blue in ascending order, then all orange in ascending order.
        /// If the order of the balls is incorrect, gameplay will not proceed correctly.
        /// </summary>
        [Header("Table Objects")]
        [Tooltip(
            "The balls that are used by the table."+
            "\nThe order of the balls is as follows: cue, black, all blue in ascending order, then all orange in ascending order."+
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

        [Header("Materials")]
        public MeshRenderer[] ballRenderers;

        public MeshRenderer tableRenderer;
        private Material[] tableMaterials;

        public Texture[] sets;

        public Material[] cueGrips;

        [Header("Audio")]
        public GameObject audioSourcePoolContainer;
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
        public AudioClip spinSfx;
        public AudioClip hitBallSfx;

        [Header("Reflection Probes")]
        public ReflectionProbe tableReflection;

        [Header("Meshes")]
        public Mesh[] cueballMeshes;
        public Mesh nineBall;

        /// <summary>
        /// True whilst balls are rolling
        /// </summary>
        [UdonSynced] protected bool gameIsSimulating;
        
        [UdonSynced] private int timerSecondsPerShot = 0;

        /// <summary>
        /// Permission for player to play
        /// </summary>
        [UdonSynced] private bool isPlayerAllowedToPlay;

        /// <summary>
        /// Player is hitting
        /// </summary>
        private bool isArmed;

        private int localPlayerID = -1;

        [UdonSynced] private bool guideLineEnabled = true;

        [Header("Desktop Stuff")] public GameObject desktopCursorObject;
        public GameObject desktopHitPosition;

        public GameObject desktopBase;

        //public GameObject desktopQuad;
        public GameObject[] desktopCueParents;
        public GameObject desktopOverlayPower;
        public UnityEngine.UI.Image tiltAmount;

        [Header("UI Stuff")]
        //public Text[] lobbyNames;
        /*
         * Private variables
         */
        private AudioSource[] ballPool;

        private Transform[] ballPoolTransforms;
        private AudioSource mainSrc;
        private UdonBehaviour udonChips;

        /// <summary>
        /// 18:0 (0xffff)	Each bit represents each ball, if it has been pocketed or not
        /// </summary>
        //[UdonSynced] private uint ballPocketedState;
        [UdonSynced] protected bool[] ballsArePocketed;

        /// <summary>
        /// 19:1 (0x02)		Whos turn is it, 0 or 1
        /// </summary>
        [UdonSynced] protected bool isTeam2Turn;

        /// <summary>
        /// 19:2 (0x04)		End-of-turn foul marker
        /// </summary>
        [UdonSynced] private bool isFoul;

        /// <summary>
        /// 19:3 (0x08)		Is the table open?
        /// </summary>
        [UdonSynced] protected bool isOpen = true;

        /// <summary>
        /// 19:4 (0x10)		What colour the players have chosen
        /// </summary>
        [UdonSynced] protected bool isTeam2Blue;

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
        [UdonSynced] private uint gameID;

        [UdonSynced] private uint turnID;

        // Cached data to use when checking for update.
        private bool[] oldBallsArePocketed;

        private bool oldIsTeam2Turn;
        private bool oldOpen;
        private bool oldIsGameInMenus;
        private uint oldGameID;

        /// <summary>
        /// We are waiting for our local simulation to finish, before we unpack data
        /// </summary>
        protected bool isUpdateLocked;

        /// <summary>
        /// The first ball to be hit by cue ball
        /// </summary>
        private int firstHitBallThisTurn;
        private int secondBallHitThisTurn;
        private int thirdBallHitThisTurn;

        /// <summary>
        /// If the simulation was initiated by us, only set from update
        /// </summary>
        protected bool isSimulatedByUs;

        /// <summary>
        /// Ball dropper timer
        /// </summary>
        private float introAnimTimer;

        /// <summary>
        /// Repositioner is active
        /// </summary>
        [UdonSynced] private bool isRepositioningCueBall;

        private float remainingTime;
        private bool isTimerRunning;
        private bool isMadePoint;
        private bool isMadeFoul;

        [UdonSynced] private bool isKorean;

        [UdonSynced] private int[] scores = new int[2];

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

        [UdonSynced] protected Vector3[] currentBallPositions = new Vector3[NUMBER_OF_SIMULATED_BALLS];
        [UdonSynced] protected Vector3[] currentBallVelocities = new Vector3[NUMBER_OF_SIMULATED_BALLS];
        [UdonSynced] protected Vector3[] currentAngularVelocities = new Vector3[NUMBER_OF_SIMULATED_BALLS];

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
        private Vector3 deskTopCursor = new Vector3(0.0f, 2.0f, 0.0f);
        private Vector3 desktopHitCursor = new Vector3(0.0f, 0.0f, 0.0f);
        private float desktopAngle = DEFAULT_DESKTOP_CUE_ANGLE;
        public float desktopAngleIncrement = 15f;
        private bool isDesktopShootingIn;
        private bool isDesktopSafeRemove = true;
        private Vector3 desktopShootVector;
        private Vector3 desktopSafeRemovePoint;
        private float desktopShootReference;

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
        private int lastTimerSeconds;
        private float shootAmt;
        protected int[] rackOrder8Ball = { 9, 2, 10, 11, 1, 3, 4, 12, 5, 13, 14, 6, 15, 7, 8 };
        protected int[] rackOrder9Ball = { 2, 3, 4, 5, 9, 6, 7, 8, 1 };
        protected int[] breakRows9ball = { 0, 1, 2, 1, 0 };

        /// <summary>
        /// 19:8 (0x700)	Gamemode ID 3 bit	{ 0: 8 ball, 1: 9 ball, 2+: undefined }
        /// </summary>
        [UdonSynced] protected uint gameMode;

        // Additional synced data added by the port to Manual Sync

        [UdonSynced] private int player1ID;
        [UdonSynced] private int player2ID;
        [UdonSynced] private int player3ID;
        [UdonSynced] private int player4ID;

        public Logger logger;

        [UdonSynced] private bool gameWasReset;

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

        [UdonSynced] private ResetReason latestResetReason;

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
            repoMaxX = TABLE_WIDTH;
            desktopClampX = TABLE_WIDTH;
            desktopClampY = TABLE_HEIGHT;
            
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

            cueTipTransform = cueTip.transform;

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
                if (Input.GetKeyDown(KeyCode.E))
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
                    copyOfLocalSpacePositionOfCueTip = AimAndHitCueBall(copyOfLocalSpacePositionOfCueTip, cueballPosition);
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
            temp.x = Mathf.Clamp(temp.x, -TABLE_WIDTH, repoMaxX);
            temp.z = Mathf.Clamp(temp.z, -TABLE_HEIGHT, TABLE_HEIGHT);
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
            if (ballCustomColours)
            {
                if ((blueTeamSliders == null) || (orangeTeamSliders == null))
                {
                    Debug.Log("At least one of color behaviours are not assigned, did you include the color panels prefab?");
                    //leaves this message if unassignment crashes the PoolStateMananger
                }
                blueTeamSliders._EnableDisable(enabledState);
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
            timerSecondsPerShot -=5;

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
            gameIsSimulating = false;
            isOpen = true;
            isGameInMenus = false;
            poolCues[0].tableIsActive = true;
            poolCues[1].tableIsActive = true;

            isTeam2Blue = false;
            isTeam2Winner = false;

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

            gameMode = 2u;
            RefreshNetworkData(false);
        }
        
        private void Initialize8Ball()
        {
            ballsArePocketed = new [] { false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false };

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
            ballsArePocketed = new [] { false, false, false, false, false, false, false, false, false, false, true, true, true, true, true, true };

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
            ballsArePocketed = new [] { false, true, false, false, true, true, true, true, true, false, true, true, true, true, true, true };

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
            gameIsSimulating = false;
            isTeam2Turn = false;
            gameWasReset = true;
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

            bool isCorrectBallSunk = false;
            bool isOpponentColourSunk = false;
            bool winCondition;
            bool foulCondition = false;
            bool deferLossCondition = false;
            bool isWrongHit = false;
            bool is8Sink = false;
            int numberOfSunkBlues = 0;
            int numberOfSunkOranges = 0;

            if (is8Ball) // Standard 8 ball
            {
                bool isBlue = !isOpen && (isTeam2Turn && isTeam2Blue || !isTeam2Turn && !isTeam2Blue);

                for (int i = 2; i < 9; i++)
                {
                    if (ballsArePocketed[i])
                    {
                        numberOfSunkBlues++;
                    }
                }

                for (int i = 9; i < 16; i++)
                {
                    if (ballsArePocketed[i])
                    {
                        numberOfSunkOranges++;
                    }
                }

                for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
                {
                    if (ballsArePocketed[i] != oldBallsArePocketed[i])
                    {
                        // white ball sunk; foul
                        if (i == 0)
                        {
                            if (logger)
                            {
                                logger._Log(name, "white ball sunk; foul");
                            }
                            foulCondition = true;
                        }
                        // black ball sunk; game over
                        else if (i == 1)
                        {
                            is8Sink = true;
                        }
                        else if (isOpen)
                        {
                            isCorrectBallSunk = true;
                        }
                        // blue ball sunk
                        else if (isBlue)
                        {
                            if (i > 1 && i < 9)
                            {
                                isCorrectBallSunk = true;
                            }
                            else
                            {
                                isOpponentColourSunk = true;
                            }
                        }
                        // orange ball sunk
                        else
                        {
                            if (i >= 9)
                            {
                                isCorrectBallSunk = true;
                            }
                            else
                            {
                                isOpponentColourSunk = true;
                            }
                        }
                    }
                }

                winCondition = isBlue ? numberOfSunkBlues == 7 && is8Sink : numberOfSunkOranges == 7 && is8Sink;

                if (!isOpen)
                {
                    // Did we hit the correct ball first?
                    if (isBlue) // if blue's turn
                    {
                        if (!(firstHitBallThisTurn == 1 && winCondition) && (firstHitBallThisTurn < 2 || firstHitBallThisTurn > 8))
                        {
                            isWrongHit = true;
                        }
                    }
                    // orange's turn
                    else
                    {
                        if (!(firstHitBallThisTurn == 1 && winCondition) && firstHitBallThisTurn < 9)
                        {
                            isWrongHit = true;
                        }
                    }
                }

                if (!foulCondition)
                {
                    if (isWrongHit)
                    {
                        if (logger)
                        {
                            logger._Log(name, "foul, wrong ball hit");
                        }
                    }
                    else if (firstHitBallThisTurn == 0)
                    {
                        if (logger)
                        {
                            logger._Log(name, "foul, no ball hit");
                        }
                    }

                    foulCondition = isWrongHit || firstHitBallThisTurn == 0;
                }

                deferLossCondition = is8Sink;
            }
            else if (isNineBall) // 9 ball
            {
                // Rules are from: https://www.youtube.com/watch?v=U0SbHOXCtFw

                // Rule #1: Cueball must strike the lowest number ball, first
                isWrongHit = GetLowestNumberedBall(oldBallsArePocketed) != firstHitBallThisTurn;

                Debug.Log("is wrong hit? " + isWrongHit);

                // Rule #2: Pocketing cueball, is a foul
                if (ballsArePocketed[0])
                {
                    Debug.Log("white pocketed");

                    foulCondition = true;
                }
                else
                {
                    foulCondition = isWrongHit || firstHitBallThisTurn == 0;

                    Debug.Log("is wrong hit? " + isWrongHit);
                    Debug.Log("first ball hit this turn " + firstHitBallThisTurn);
                }

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
                    Debug.Log("four ball cue left table");
                    
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
                            
                            // Let the players fix it
                            isFoul = true;
                        }
                    }

                    fourBallCueLeftTable = false;
                }
                
                isCorrectBallSunk = isMadePoint;
                isOpponentColourSunk = isMadeFoul;
                winCondition = scores[Convert.ToInt32(isTeam2Turn)] >= 10;
            }
            else
            {
                return;
            }

            if (winCondition)
            {
                if (foulCondition)
                {
                    // Loss
                    OnTurnOverGameWon(!isTeam2Turn);
                }
                else
                {
                    // Win
                    OnTurnOverGameWon(isTeam2Turn);
                }
            }
            else if (deferLossCondition)
            {
                // Loss
                OnTurnOverGameWon(!isTeam2Turn);
            }
            else if (foulCondition)
            {
                // Foul
                OnTurnOverFoul();
            }
            else if (isCorrectBallSunk && !isOpponentColourSunk)
            {
                // Continue
                // Close table if it was open ( 8 ball specific )
                if (is8Ball && isOpen)
                {
                    if (numberOfSunkBlues != numberOfSunkOranges)
                    {
                        isTeam2Blue = (numberOfSunkBlues > numberOfSunkOranges) ? isTeam2Turn : !isTeam2Turn;

                        isOpen = false;
                        ApplyTableColour(isTeam2Turn);
                    }
                }

                // Keep playing
                isPlayerAllowedToPlay = true;

                RefreshNetworkData(isTeam2Turn);
            }
            else
            {
                // Pass
                isPlayerAllowedToPlay = true;

                if (isFourBall)
                {
                    Debug.Log("4ball: swapping position of 0 and 9th balls");

                    Vector3 temp = currentBallPositions[0];
                    currentBallPositions[0] = currentBallPositions[9];
                    currentBallPositions[9] = temp;
                }

                turnID++;
                RefreshNetworkData(!isTeam2Turn);
            }
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

        /// <summary>
        /// Decode networking string
        /// </summary>
        protected void ReadNetworkData()
        {
            if (logger)
            {
                logger._Log(name, "ReadNetworkData");
            }

            if (marker)
            {
                marker.SetActive(false);
            }
            
            if (gameID > oldGameID && !isGameInMenus)
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

            // If the turn has changed...
            if (isTeam2Turn != oldIsTeam2Turn)
            {
                OnRemoteTurnChange();
            }

            if (oldOpen && !isOpen)
            {
                ApplyTableColour(isTeam2Turn);
            }

            // If the game has ended...
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
                isTeam2Turn,
                (int)gameMode,
                isKorean,
                (int)timerSecondsPerShot,
                player1ID,
                player2ID,
                player3ID,
                player4ID,
                guideLineEnabled
            );

            if (isGameInMenus)
            {
                lastTimerSeconds = timerSecondsPerShot;

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

                hasRunSyncOnce = true;

                return;
            }

            if (isFourBall)
            {
                ballsArePocketed = new[] { false, true, false, false, true, true, true, true, true, false, true, true, true, true, true, true };
            }

            // Check this every read
            // Its basically 'turn start' event
            if (isPlayerAllowedToPlay)
            {
                if (((localPlayerID >= 0) && (playerIsTeam2 == isTeam2Turn)) || isGameModePractice)
                {
                    // Update for desktop
                    isDesktopLocalTurn = true;

                    // Reset hit point
                    desktopHitCursor = Vector3.zero;
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

                if (devhit)
                {
                    devhit.SetActive(false);
                }

                if (guideline)
                {
                    guideline.gameObject.SetActive(false);
                }
            }

            // Start of turn so we've hit nothing
            firstHitBallThisTurn = 0;
            secondBallHitThisTurn = 0;
            thirdBallHitThisTurn = 0;

            hasRunSyncOnce = true;
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
                (int)gameMode,
                isKorean,
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
            {
                logger._Log(name, "OnRemoteTurnChange");
            }

            // Effects
            ApplyTableColour(isTeam2Turn);
            mainSrc.PlayOneShot(newTurnSfx, 1.0f);

            // Register correct cuetip
            cueTip = cueTips[Convert.ToUInt32(isTeam2Turn)];

            bool isOurTurn = ((localPlayerID >= 0) && (playerIsTeam2 == isTeam2Turn)) || isGameModePractice;
            if (isFourBall) // 4 ball
            {
                if (!isTeam2Turn)
                {
                    if (logger != null)
                    {
                        logger._Log(name, "0 ball is 0 mesh, 9 ball is 1 mesh");
                    }

                    ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[0];
                    ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[1];
                }
                else
                {
                    if (logger != null)
                    {
                        logger._Log(name, "0 ball is 0 mesh, 9 ball is 1 mesh");
                    }

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
            if (timerSecondsPerShot > 0)
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
                    ballTransforms[i].localPosition = new Vector3(posX + numberOfSunkBalls * BALL_DIAMETER, posY, posZ);
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

            // Commit changes
            gameIsSimulating = true;

            // Make sure we are the network owner
            Networking.SetOwner(localPlayer, gameObject);

            RefreshNetworkData(isTeam2Turn);

            isSimulatedByUs = true;

            float vol = Mathf.Clamp(currentBallVelocities[0].magnitude * 0.1f, 0f, 0.6f);
            cueTipSrc.transform.position = cueTipTransform.position;
            cueTipSrc.PlayOneShot(hitBallSfx, vol);
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
                newDesktopCue = Convert.ToUInt32(isTeam2Turn);
                GameObject cue = desktopCueParents[newDesktopCue];

                if (isGameModePractice && newDesktopCue != oldDesktopCue)
                {
                    poolCues[oldDesktopCue]._Respawn(false);
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
                }
                else
                {
                    // Trigger shot
                    if (isDesktopShootingIn)
                    {
                        // Shot cancel
                        if (!isDesktopSafeRemove)
                        {
                            cue.transform.localPosition = new Vector3(2000.0f, 2000.0f, 2000.0f);
                            isDesktopLocalTurn = false;
                            HitBallWithCue(cueTip.transform.forward, Mathf.Pow(shootAmt * 2.0f, 1.4f) * 7.0f);
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

                if (Input.GetKey(KeyCode.UpArrow))
                {
                    desktopAngle += desktopAngleIncrement * Time.deltaTime;
                }
                
                if (Input.GetKey(KeyCode.DownArrow))
                {
                    desktopAngle -= desktopAngleIncrement * Time.deltaTime;
                }
                
                desktopAngle = Mathf.Clamp(desktopAngle, MIN_DESKTOP_CUE_ANGLE, MAX_DESKTOP_CUE_ANGLE);

                if (tiltAmount)
                {
                    tiltAmount.fillAmount = Mathf.InverseLerp(MIN_DESKTOP_CUE_ANGLE, MAX_DESKTOP_CUE_ANGLE, desktopAngle);
                }

                // Clamp in circle
                if (desktopHitCursor.magnitude > 0.90f)
                {
                    desktopHitCursor = desktopHitCursor.normalized * 0.9f;
                }

                desktopHitPosition.transform.localPosition = desktopHitCursor;

                // Create rotation
                Quaternion xr = Quaternion.AngleAxis(desktopAngle, Vector3.right);
                Quaternion r = Quaternion.AngleAxis(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, Vector3.up);

                Vector3 worldHit = new Vector3(desktopHitCursor.x * BALL_PL_X, desktopHitCursor.z * BALL_PL_X,
                    -0.89f - shootAmt);

                cue.transform.localRotation = r * xr;
                cue.transform.position =
                    tableSurface.transform.TransformPoint(currentBallPositions[0] + (r * xr * worldHit));
            }

            desktopCursorObject.transform.localPosition = deskTopCursor;
            desktopOverlayPower.transform.localScale = new Vector3(1.0f - (shootAmt * 2.0f), 1.0f, 1.0f);
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
                
                ReportFourBallScore(false);
            }
            else if (isNineBall)
            {
                poolMenu._SetScore(false, -1);
                poolMenu._SetScore(true, -1);
                
                ReportNineBallScore(false);
            }
            else
            {
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
                
                ReportEightBallScore(false, teamAScore, teamBScore);
            }
        }

        private void ReportEightBallScore(bool gameOver, int teamAScore, int teamBScore)
        {
            if (slogger)
            {
                slogger.OscReportEndOfTurn(gameOver, turnID, teamAScore, isFoul, teamBScore);
            }
        }

        private void ReportNineBallScore(bool gameOver)
        {
            if (slogger)
            {
                slogger.OscReportEndOfTurn(gameOver, turnID, -1, isFoul, -1);
            }
        }

        private void ReportFourBallScore(bool gameOver)
        {
            if (slogger)
            {
                slogger.OscReportEndOfTurn(gameOver, turnID, scores[0], isFoul, scores[1]);
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

        public GameObject pressE;

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

            if (pressE)
            {
                pressE.SetActive(true);
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
            {
                HandleSuccessEffects();
            }
            else
            {
                HandleFoulEffects();
            }
        }

        protected void HandleSuccessEffects()
        {
            // Make a bright flash
            tableCurrentColour *= 1.9f;
            PlayAudioClip(successSfx);
        }

        protected void HandleFoulEffects()
        {
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

            if (isFourBall)
            {
                if (isKorean)
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
                else
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

                        Debug.Log($"Scoring a point due to hitting balls {firstHitBallThisTurn} and {secondBallHitThisTurn}");
                        OnLocalCaromPoint(ballTransforms[otherBallID]);
                    }
                    else if (thirdBallHitThisTurn == 0)
                    {
                        if (otherBallID == firstHitBallThisTurn || otherBallID == secondBallHitThisTurn)
                        {
                            return;
                        }

                        thirdBallHitThisTurn = otherBallID;
                        Debug.Log($"Scoring a point due to hitting balls {firstHitBallThisTurn} and {secondBallHitThisTurn} and {thirdBallHitThisTurn}");
                        OnLocalCaromPoint(ballTransforms[otherBallID]);
                    }
                }
            }
            else if (firstHitBallThisTurn == 0)
            {
                firstHitBallThisTurn = otherBallID;
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