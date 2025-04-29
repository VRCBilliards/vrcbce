using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Rendering;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts.Components;

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

    public enum GameMode {
        EightBall,
        NineBall,
        KoreanCarom,
        JapaneseCarom,
        ThreeCushionCarom
    }

    /// <summary>
    /// This is the base logic that governs the pool table, devoid of almost all physics code. This code is quite
    /// messy; it includes all setup, teardown, game state and replication logic, and works with all other components
    /// to allow the player to play pool.
    /// </summary>
    public partial class PoolStateManager
    {
        #region Dependencies

        public PoolMenu poolMenu;
        public PoolCue[] poolCues;
        public ScreenspaceUI pancakeUI;
        public OscSlogger slogger;
        public Logger logger;

        #endregion

        #region Networked Variables

        [UdonSynced] private bool turnIsRunning;
        [UdonSynced] private int timerSecondsPerShot;
        [UdonSynced] private bool isPlayerAllowedToPlay;
        [UdonSynced] private bool guideLineEnabled = true;

        [UdonSynced] private bool[] ballsArePocketed;
        private bool[] oldBallsArePocketed;

        [UdonSynced] private bool isTeam2Turn;
        private bool oldIsTeam2Turn;

        [UdonSynced] private bool isFoul;
        [UdonSynced] private bool isGameOver;

        [UdonSynced] private bool isOpen = true;
        private bool oldOpen;

        [UdonSynced] private bool isTeam2Blue;

        [UdonSynced] private bool isGameInMenus = true;
        private bool oldIsGameInMenus;

        [UdonSynced] private bool isTeam2Winner;
        [UdonSynced] private bool isTableLocked = true;
        [UdonSynced] private bool isTeams;

        [UdonSynced] private uint gameID;
        private uint oldGameID;

        [UdonSynced] private uint turnID;
        [UdonSynced] private bool isRepositioningCueBall;
        
        /// <summary>
        /// Is the game currently in the first shot?
        /// </summary>
        [UdonSynced] private bool _isGameBreak;

        [UdonSynced] private int[] scores = new int[2];
        [UdonSynced] private Vector3[] currentBallPositions = new Vector3[NUMBER_OF_SIMULATED_BALLS];
        [UdonSynced] private Vector3[] currentBallVelocities = new Vector3[NUMBER_OF_SIMULATED_BALLS];
        [UdonSynced] private Vector3[] currentAngularVelocities = new Vector3[NUMBER_OF_SIMULATED_BALLS];

        [UdonSynced] private GameMode gameMode;
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
        private const int NUMBER_OF_SIMULATED_BALLS = 16;

        // A small fraction designed to slightly move balls around when placed, which helps with making the table
        // less deterministic.
        private const float RANDOMIZE_F = 0.0001f;

        // These four vars dictate some placement of balls when placed.
        
        // Half-way between the centre spot and the cushion on the X axis.
        public float SPOT_POSITION_X = 0.5334f;
        // Three quarters of the way between the centre spot and the cushion on the X axis.
        public float SPOT_CAROM_X = 0.8001f;
        public float BALL_PL_X = 0.03f;
        public float BALL_PL_Y = 0.05196152422f;

        #region Desktop

        private const float DEFAULT_DESKTOP_CUE_ANGLE = 10.0f;

        // This should never be 90.0f or higher, as it puts the physics simulation into a weird state.
        private const float MAX_DESKTOP_CUE_ANGLE = 89.0f;
        private const float MIN_DESKTOP_CUE_ANGLE = 0.0f;

        #endregion

        [Tooltip("Use fake shadows? They may clash with your world's lighting.")]
        public bool fakeBallShadows = true;

        [Tooltip("Does the table model for this table have rails that guide the ball when the ball sinks?")]
        public bool tableModelHasRails;

        public Transform sunkBallsPositionRoot;
        public GameObject shadows;
        public ParticleSystem plusOneParticleSystem;
        public ParticleSystem minusOneParticleSystem;

        // Where's the surface of the table?
        public Transform tableSurface;

        public string uniformTableColour = "_EmissionColor";
        public string uniformMarkerColour = "_Color";
        public string uniformCueColour = "_EmissionColor";
        public string uniformBallColour = "_BallColour";
        public string uniformBallFloat = "_CustomColor";
        public string ballMaskToggle = "_Turnoff";

        [Tooltip(
            "Change the length of the intro ball-drop animation. If you set this to zero, the animation will not play at all.")]
        [Range(0f, 5f)]
        public float introAnimationLength = 2.0f;

        [ColorUsage(true, true)]
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

        public bool ballCustomColours;
        public ColorPicker blueTeamSliders;
        public ColorPicker orangeTeamSliders;

        private float shaderToggleFloat = 0;

        private GameObject cueTip;
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

        public MeshRenderer[] ballRenderers;

        public MeshRenderer tableRenderer;
        private Material[] tableMaterials;

        public Texture[] sets;

        public Material[] cueGrips;

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
        public AudioClip hitBallSfx;

        public ReflectionProbe tableReflection;

        public Mesh[] cueballMeshes;
        public Mesh nineBall;

        /// <summary>
        /// Player is hitting
        /// </summary>
        private bool isArmed;

        private int localPlayerID = -1;

        public GameObject desktopHitPosition;

        public GameObject desktopBase;
        
        public GameObject[] desktopCueParents;
        public UnityEngine.UI.Image tiltAmount;

        /*
         * Private variables
         */
        private AudioSource[] ballPool;

        private Transform[] ballPoolTransforms;
        private AudioSource mainSrc;

        /// <summary>
        /// We are waiting for our local simulation to finish, before we unpack data
        /// </summary>
        private bool isUpdateLocked;

        /// <summary>
        /// The first ball to be hit by cue ball
        /// </summary>
        private int firstHitBallThisTurn;

        [Range(10, 50),
         Tooltip("How many points do your players need to win a carom game? This is the same for all carom variants.")]
        public int scoreNeededToWinCarom = 10;

        private int secondBallHitThisTurn;
        private int thirdBallHitThisTurn;
        private int cushionsHitThisTurn;

        /// <summary>
        /// If the simulation was initiated by us, only set from update
        /// </summary>
        private bool isSimulatedByUs;

        /// <summary>
        /// Ball dropper timer
        /// </summary>
        private float introAnimTimer;

        private float remainingTime;
        private bool isTimerRunning;
        private bool isMadePoint;
        private bool isMadeFoul;
        private bool IsCarom => gameMode == GameMode.KoreanCarom || gameMode == GameMode.JapaneseCarom || gameMode == GameMode.ThreeCushionCarom;

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

        private Vector3 localSpacePositionOfCueTipLastFrame;
        private Vector3 cueLocalForwardDirection;
        private Vector3 cueArmedShotDirection;

        private float cueFDir;
        private Vector3 raySphereOutput;
        private int[] rackOrder8Ball = {9, 2, 10, 11, 1, 3, 4, 12, 5, 13, 14, 6, 15, 7, 8};
        private int[] rackOrder9Ball = {2, 3, 4, 5, 9, 6, 7, 8, 1};
        private int[] breakRows9ball = {0, 1, 2, 1, 0};

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
        
        private bool fourBallCueLeftTable;

        /// <summary>
        /// Have we run a network sync once? Used for situations where we need to specifically catch up a late-joiner.
        /// </summary>
        private bool hasRunSyncOnce;

        /// <summary>
        /// For clamping to table or set lower for kitchen
        /// </summary>
        private float repoMaxX;

        public CueBallOffTableController cueBallController;
        
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

        public void Start()
        {
            repoMaxX = tableWidth;

            localPlayer = Networking.LocalPlayer;
            networkingLocalPlayerID = localPlayer.playerId;
            isPlayerInVR = localPlayer.IsUserInVR();
            tableMaterials = tableRenderer.materials;

            ballsArePocketed = new bool[NUMBER_OF_SIMULATED_BALLS];
            oldBallsArePocketed = new bool[NUMBER_OF_SIMULATED_BALLS];
            
            ballRigidbodies = new Rigidbody[NUMBER_OF_SIMULATED_BALLS];
            for (var i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                ballRigidbodies[i] = ballTransforms[i].GetComponent<Rigidbody>();
            }

            ballShadowPosConstraintTransforms = new Transform[NUMBER_OF_SIMULATED_BALLS];
            for (var i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                ballShadowPosConstraintTransforms[i] = ballShadowPosConstraints[i].transform;
            }

            mainSrc = GetComponent<AudioSource>();

            if (audioSourcePoolContainer)
            {
                ballPool = audioSourcePoolContainer.GetComponentsInChildren<AudioSource>();
                ballPoolTransforms = new Transform[ballPool.Length];

                for (var i = 0; i < ballPool.Length; i++)
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
                guideline.gameObject.SetActive(false);

            if (devhit)
                devhit.SetActive(false);

            if (marker)
                marker.SetActive(false);

            if (marker9ball)
                marker9ball.SetActive(false);

            if (shadows)
                shadows.SetActive(fakeBallShadows);

            ballShadowOffset = ballTransforms[0].position.y - ballShadowPosConstraintTransforms[0].position.y;

            shadowRenders = shadows.GetComponentsInChildren<MeshRenderer>();

            timerText = poolMenu.visibleTimerDuringGame;
            timerOutputFormat = poolMenu.timerOutputFormat;
            timerCountdown = poolMenu.timerCountdown;

            desktopCameraInitialPosition = desktopCamera.transform.localPosition;
            desktopCameraInitialRotation = desktopCamera.transform.localRotation;
            initialPowerBarPos = powerBar.transform.localPosition;

            CalculateTableCollisionConstants();
        }

        public void Update()
        {
            if (isInDesktopTopDownView)
            {
                HandleUpdatingDesktopViewUI();

                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
                    OnDesktopTopDownViewExit();
            }
            else if (canEnterDesktopTopDownView)
            {
                if (Input.GetKeyDown(KeyCode.E) || lastInputUseDown)
                    OnDesktopTopDownViewStart();
            }

            if (isGameInMenus)
                return;

            // Everything below this line only runs when the game is active.

            localSpacePositionOfCueTip = tableSurface.transform.InverseTransformPoint(cueTip.transform.position);
            var copyOfLocalSpacePositionOfCueTip = localSpacePositionOfCueTip;

            // if shot is prepared for next hit
            if (isPlayerAllowedToPlay)
            {
                if (isRepositioningCueBall)
                {
                    HandleRepositioningCueBall();

                    markerMaterial.SetColor(uniformMarkerColour, IsCueContacting() ? markerNotOK : markerOK);
                }

                var cueballPosition = currentBallPositions[0];

                if (isArmed)
                    copyOfLocalSpacePositionOfCueTip = AimAndHitCueBall(copyOfLocalSpacePositionOfCueTip, cueballPosition);
                else
                    HandleGuidelinesAndAimMarkers(copyOfLocalSpacePositionOfCueTip, cueballPosition);
            }

            localSpacePositionOfCueTipLastFrame = copyOfLocalSpacePositionOfCueTip;

            // Table outline colour
            if (isGameInMenus)
                tableCurrentColour = tableSrcColour * ((Mathf.Sin(Time.timeSinceLevelLoad * 3.0f) * 0.5f) + 1.0f);
            else
                tableCurrentColour = Color.Lerp(tableCurrentColour, tableSrcColour, Time.deltaTime * 3.0f);
            
            if (isTimerRunning)
                HandleTimerCountdown();
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
                HandleIntroAnimation();

            // Run sim only if things are moving
            if (turnIsRunning)
            {
                accumulatedTime += Time.deltaTime;

                if (accumulatedTime > MAX_SIMULATION_TIME_PER_FRAME)
                    accumulatedTime = MAX_SIMULATION_TIME_PER_FRAME;

                while (accumulatedTime >= TIME_PER_STEP)
                {
                    AdvancePhysicsStep();
                    accumulatedTime -= TIME_PER_STEP;
                }
            }
            
            if (IsCueInPlay)
                ballTransforms[0].localPosition = currentBallPositions[0];
            
            for (var i = 1; i < ballsArePocketed.Length; i++)
            {
                if (!ballsArePocketed[i])
                    ballTransforms[i].localPosition = currentBallPositions[i];
            }
        }

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
            foreach (var con in ballShadowPosConstraints)
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

        // Update loop-scoped handler for introduction animation operations. Non-pure.
        private void HandleIntroAnimation()
        {
            introAnimTimer -= Time.deltaTime;

            Vector3 temp;
            float atime;
            float aitime;

            if (introAnimTimer < 0.0f)
                introAnimTimer = 0.0f;

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
                    isPlayerAllowedToPlay = false;
            }
            else
            {
                if (timerText)
                    timerText.text = timerOutputFormat.Replace("{}", Mathf.Round(remainingTime).ToString());

                if (timerCountdown)
                    timerCountdown.fillAmount = remainingTime / timerSecondsPerShot;
            }
        }

        // Update loop-scoped handler for guidelines and aim markers. Non-pure.
        private void HandleGuidelinesAndAimMarkers(Vector3 copyOfLocalSpacePositionOfCueTip, Vector3 cueballPosition)
        {
            cueLocalForwardDirection = tableSurface.transform.InverseTransformVector(cueTip.transform.forward);
            
            if (devhit)
                devhit.SetActive(false);

            if (guideline)
                guideline.gameObject.SetActive(false);

            // Get where the cue will strike the ball
            if (!IsIntersectingWithSphere(copyOfLocalSpacePositionOfCueTip, cueLocalForwardDirection, cueballPosition)) 

                return;
            if (guideLineEnabled && guideline)
                guideline.gameObject.SetActive(true);

            if (devhit)
            {
                devhit.SetActive(true);
                devhit.transform.localPosition = raySphereOutput;
            }

            cueArmedShotDirection = cueLocalForwardDirection;
            Vector3 shotVector = (cueballPosition - raySphereOutput) * 0.1f;
            
            // This is a bit of a hack; we abuse the shot vector and then divide it by 10 which gives us 
            // a mostly-accurate account of where the shot will go.
            cueArmedShotDirection += shotVector.normalized / 10;

            cueFDir = Mathf.Atan2(shotVector.z, shotVector.x);

            // Update the prediction line direction
            Transform transform1 = guideline.transform;
            transform1.localPosition = currentBallPositions[0];
            transform1.localEulerAngles = new Vector3(0.0f, -cueFDir * Mathf.Rad2Deg, 0.0f);
        }

        private void EnableCustomBallColorSlider(bool enabledState)
        {
            if (!ballCustomColours)
                return;

            if (blueTeamSliders)
                blueTeamSliders._EnableDisable(enabledState);

            if (orangeTeamSliders)
                orangeTeamSliders._EnableDisable(enabledState);
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

        public void _StartHit()
        {
            if (logger)
                logger._Log(name, "StartHit");

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
                logger._Log(name, "EndHit");

            isArmed = false;
        }

        public void PlaceBall()
        {
            if (logger)
                logger._Log(name, "PlaceBall");

            if (IsCueContacting()) 
                return;

            if (logger)
                logger._Log(name, "disabling marker because the ball hase been placed");

            isRepositioningCueBall = false;

            if (marker)
                marker.SetActive(false);

            isPlayerAllowedToPlay = true;
            isFoul = false;

            Networking.SetOwner(localPlayer, gameObject);

            // Save out position to remote clients
            RefreshNetworkData(isTeam2Turn);
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
                logger._Log(name, ToReasonString(reason));
        }

        private void OnDesktopTopDownViewStart()
        {
            if (logger)
                logger._Log(name, "OnDesktopTopDownViewStart");

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
                logger._Log(name, "OnPutDownCueLocally");

            OnDesktopTopDownViewExit();
        }

        // HandleEndOfTurn assess the table state at the end of the turn, based on what game is playing.
        private void HandleEndOfTurn()
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

            switch (gameMode) {
                case GameMode.EightBall:
                    HandleEightBallEndOfTurn(
                        is8Sink,
                        out numberOfSunkBlues,
                        out numberOfSunkOranges,
                        ref isCorrectBallSunk,
                        ref isOpponentColourSunk,
                        ref foulCondition,
                        out deferLossCondition,
                        out winCondition
                    );

                    break;
                case GameMode.NineBall:
                    HandleNineBallEndOfTurn(ref foulCondition, ref isCorrectBallSunk, out winCondition);

                    break;
                case GameMode.KoreanCarom:
                case GameMode.JapaneseCarom:
                    HandleCaromEndOfTurn();

                    isCorrectBallSunk = isMadePoint;
                    isOpponentColourSunk = isMadeFoul;
                    winCondition = scores[Convert.ToInt32(isTeam2Turn)] >= scoreNeededToWinCarom;
                    
                    break;
                case GameMode.ThreeCushionCarom:
                    HandleThreeCushionCaromEndOfTurn();
                    
                    // There's no action to take when fouling in 3CC (no marker spawn etc) so we just care if the player
                    // scored.
                    isCorrectBallSunk = isMadePoint;
                    winCondition = scores[Convert.ToInt32(isTeam2Turn)] >= scoreNeededToWinCarom;

                    break;
                default:
                    return;
            }

            // Has the player won?
            if (winCondition)
            {
                // Has the player fouled, giving the opponent the win?
                if (foulCondition)
                    OnTurnOverGameWon(!isTeam2Turn, true);
                else
                    OnTurnOverGameWon(isTeam2Turn, false);

                return;
            }
            
            // Has the player fouled catastrophically and lost?
            if (deferLossCondition)
            {
                OnTurnOverGameWon(!isTeam2Turn, true);

                return;
            }

            // Has the player fouled?
            if (foulCondition)
            {
                OnTurnOverFoul();

                return;
            }
            
            // Has the player done enough to continue their turn?
            if (isCorrectBallSunk && !isOpponentColourSunk)
            {
                // TODO: not happy with this, revise this based on blackball rules.
                if (gameMode == GameMode.EightBall && isOpen)
                {
                    if (numberOfSunkBlues != numberOfSunkOranges)
                    {
                        isTeam2Blue = numberOfSunkBlues > numberOfSunkOranges ? isTeam2Turn : !isTeam2Turn;

                        isOpen = false;
                        ApplyTableColour(isTeam2Turn);
                    }
                }

                isPlayerAllowedToPlay = true;
                _isGameBreak = false;

                RefreshNetworkData(isTeam2Turn);

                return;
            }
            
            // End the player's turn and pass the turn on.
            
            isPlayerAllowedToPlay = true;
            _isGameBreak = false;

            if (IsCarom)
            {
                Vector3 temp = currentBallPositions[0];
                currentBallPositions[0] = currentBallPositions[9];
                currentBallPositions[9] = temp;
            }

            turnID++;

            RefreshNetworkData(!isTeam2Turn);
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

        private void ReadNetworkData()
        {
            if (logger)
                logger._Log(name, "ReadNetworkData");

            UpdateScores();

            // Assume the marker is off - one of the On... functions might turn it back on again
            if (marker)
                marker.SetActive(false);

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
                gameMode,
                timerSecondsPerShot,
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
                        numberOfPlayers++;

                    if (player2ID != 0)
                        numberOfPlayers++;

                    if (player3ID != 0)
                        numberOfPlayers++;

                    if (player4ID != 0)
                        numberOfPlayers++;
                }

                isGameModePractice = localPlayerID == 0 && numberOfPlayers == 1;

                hasRunSyncOnce = true;

                return;
            }

            switch (gameMode)
            {
                case GameMode.KoreanCarom:
                case GameMode.JapaneseCarom:
                    InitializePocketedStateKoreanJapaneseCarom();
                    break;
                case GameMode.ThreeCushionCarom:
                    InitializePocketedStateThreeCushionCarom();
                    break;
                case GameMode.EightBall:
                case GameMode.NineBall:
                default:
                    break;
            }

            // Check this every read
            // Its basically 'turn start' event
            if (isPlayerAllowedToPlay)
                OnLocalTurnStart();
            else
                OnRemoteTurnStart();

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
                marker9ball.SetActive(false);

            isTimerRunning = false;
            isMadePoint = false;
            isMadeFoul = false;

            if (devhit)
                devhit.SetActive(false);

            if (guideline)
                guideline.gameObject.SetActive(false);
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
                isDesktopLocalTurn = false;

            if (gameMode == GameMode.NineBall)
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
                PlaceSunkBallsIntoRestingPlace();

            if (timerSecondsPerShot > 0 && !isTimerRunning)
                ResetTimer();

            // sanitize old cue tip location data to prevent stale data from causing unintended effects.
            localSpacePositionOfCueTipLastFrame = tableSurface.transform.InverseTransformPoint(cueTip.transform.position);
        }

        private void OnNewGameStarted()
        {
            OnRemoteNewGame();

            if (((localPlayerID >= 0) && (playerIsTeam2 == isTeam2Turn)) || isGameModePractice)
            {
                if (logger)
                    logger._Log(name, "enabling marker because it is the start of the game and we are breaking");

                isRepositioningCueBall = true;
                repoMaxX = -SPOT_POSITION_X;
                ballRigidbodies[0].isKinematic = true;

                if (!marker) 
                    return;
                
                markerTransform.localPosition = currentBallPositions[0];

                if (gameMode == GameMode.EightBall || gameMode == GameMode.NineBall)
                    marker.SetActive(true);

                ((VRC_Pickup) marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = true;

                return;
            }
            
            markerTransform.localPosition = currentBallPositions[0];

            if (gameMode == GameMode.EightBall || gameMode == GameMode.NineBall)
                marker.SetActive(true);

            ((VRC_Pickup) marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = false;
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

        private void SetFourBallColours()
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
                logger._Log(name, "ApplyTableColour");

            switch(gameMode) {
                case GameMode.EightBall:
                    ApplyEightBallTableColour(isTeam2Color);

                    break;
                case GameMode.NineBall:
                    ApplyNineBallTableColour(isTeam2Color);

                    break;
                case GameMode.KoreanCarom:
                case GameMode.JapaneseCarom:
                case GameMode.ThreeCushionCarom:
                    ApplyCaromTableColour();

                    break;
            }

            cueGrips[Convert.ToInt32(this.isTeam2Turn)].SetColor(uniformMarkerColour, gripColourActive);
            cueGrips[Convert.ToInt32(!this.isTeam2Turn)].SetColor(uniformMarkerColour, gripColourInactive);

            if (!ballCustomColours)
                return;

            if (gameMode == GameMode.NineBall)
            {
                foreach (MeshRenderer meshRenderer in ballRenderers)
                {
                    meshRenderer.material.SetFloat(ballMaskToggle, 1);
                }
            }
            else
            {
                for (var i = 2; i < NUMBER_OF_SIMULATED_BALLS; i++)
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
                    slogger.OscReportGameReset(latestResetReason);
            }
            else
            {
                poolMenu._TeamWins(isTeam2Winner);
                PlayAudioClip(winnerSfx);
            }

            if (marker9ball)
                marker9ball.SetActive(false);

            if (!tableModelHasRails || !hasRunSyncOnce)
                PlaceSunkBallsIntoRestingPlace();

            if (logger)
                logger._Log(name, "disabling marker because the game is over");

            isRepositioningCueBall = false;

            if (marker)
                marker.SetActive(false);

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
                gameMode,
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
                logger._Log(name, nameof(OnRemoteTurnChange));

            // Effects
            ApplyTableColour(isTeam2Turn);
            mainSrc.PlayOneShot(newTurnSfx, 1.0f);
            hasFoulBeenPlayedThisTurn = false;
            isCueOutOfBounds = false;

            // Register correct cuetip
            cueTip = cueTips[Convert.ToUInt32(isTeam2Turn)];

            if (IsCarom) // 4 ball
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
                        Networking.SetOwner(Networking.LocalPlayer, marker);
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
                logger._Log(name, "GrantCueAccess");

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
                logger._Log(name, "OnRemoteNewGame");

            poolMenu._EnableResetButton();

            // Cue ball
            currentBallPositions[0] = new Vector3(-SPOT_POSITION_X, 0.0f, 0.0f);
            currentBallVelocities[0] = Vector3.zero;

            // Start at spot

            switch (gameMode) {
                case GameMode.EightBall:
                    Initialize8Ball();
                    InitializeEightBallVisuals();

                    break;
                case GameMode.NineBall:
                    Initialize9Ball();
                    InitializeNineBallVisuals();

                    break;
                case GameMode.KoreanCarom:
                case GameMode.JapaneseCarom:
                    Initialize4Ball();

                    InitializeCaromVisuals();

                    break;
                case GameMode.ThreeCushionCarom:
                    InitializeThreeCushionCarom();
                    InitializeThreeCushionCaromVisuals();
                    
                    break;
            }

            if (localPlayerID >= 0)
                playerIsTeam2 = localPlayerID % 2 == 1;

            tableRenderer.material.SetColor(ClothColour, pointerClothColour);

            if (tableReflection)
                tableReflection.RenderProbe();

            ApplyTableColour(false);
            GrantCueAccess();

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
                slogger.OscReportGameStarted(localPlayerID >= 0);
        }

        /// <summary>
        /// Call at the start of each turn.
        /// Place sunk inert balls into a specific storage location.
        /// Sunk ball state is replicated, so this is network-stable.
        /// </summary>
        private void PlaceSunkBallsIntoRestingPlace()
        {
            if (logger)
                logger._Log(name, "PlaceSunkBallsIntoRestingPlace");

            var numberOfSunkBalls = 0;
            Vector3 localPosition = sunkBallsPositionRoot.localPosition;
            var posX = localPosition.x;
            var posY = localPosition.y;
            var posZ = localPosition.z;

            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                ballTransforms[i].GetComponent<Rigidbody>().isKinematic = true;

                if (!ballsArePocketed[i]) 
                    continue;
                
                ballTransforms[i].localPosition = new Vector3(posX + numberOfSunkBalls * ballDiameter, posY, posZ);
                numberOfSunkBalls++;
            }
        }

        private int GetLowestNumberedBall(bool[] balls)
        {
            // order in ballsArePocketed is assumed to be [c812345679]
            for (int i = 2; i < 9; i++)
            {
                if (!balls[i])
                    return i;
            }

            if (!balls[1])
                return 1;

            return !balls[9] ? 9 : 0;
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
                logger._Log(name, "OnTurnOverFoul");

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
                OnDesktopTopDownViewExit();
            else if (canEnterDesktopTopDownView)
                OnDesktopTopDownViewStart();
        }

        private void OnDesktopTopDownViewExit()
        {
            if (logger)
                logger._Log(name, "OnDesktopTopDownViewExit");

            isInDesktopTopDownView = false;

            if (desktopBase)
            {
                desktopBase.SetActive(false);

                if (!desktopCamera)
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

        private void HandleCueBallHit()
        {
            if (logger)
                logger._Log(name, nameof(HandleCueBallHit));

            isRepositioningCueBall = false;

            if (marker)
                marker.SetActive(false);

            if (devhit)
                devhit.SetActive(false);

            if (guideline)
                guideline.gameObject.SetActive(false);

            // Remove locks
            _EndHit();
            isPlayerAllowedToPlay = false;
            isFoul = false; // In case did not drop foul marker

            cueTipSrc.transform.SetPositionAndRotation(cueTip.transform.position, new Quaternion());
            cueTipSrc.PlayOneShot(hitBallSfx, Mathf.Clamp(currentBallVelocities[0].magnitude * 0.1f, 0f, 1f));

            // Commit changes
            turnIsRunning = true;

            // Make sure we are the network owner
            Networking.SetOwner(localPlayer, gameObject);

            RefreshNetworkData(isTeam2Turn);

            isSimulatedByUs = true;
        }

        public override void InputUse(bool value, VRC.Udon.Common.UdonInputEventArgs args)
        {
            if (!isInDesktopTopDownView)
            {
                if (canEnterDesktopTopDownView)
                    OnDesktopTopDownViewStart();

                return;
            }

            lastInputUseDown = value;
        }

        public override void InputLookHorizontal(float value, VRC.Udon.Common.UdonInputEventArgs args)
        {
            if (!isInDesktopTopDownView)
                return;

            lastLookHorizontal = value;
        }

        public override void InputLookVertical(float value, VRC.Udon.Common.UdonInputEventArgs args)
        {
            if (!isInDesktopTopDownView)
                return;

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
                desktopCamera.transform.SetLocalPositionAndRotation(desktopCameraInitialPosition, desktopCameraInitialRotation);
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

            var powerBarPos = powerBar.transform.localPosition;
            powerBarPos.x = Mathf.Lerp(initialPowerBarPos.x, topBar.transform.localPosition.x, desktopShootForce * 2.0f);
            powerBar.transform.localPosition = powerBarPos;
        }

        private void UpdateScores()
        {
            switch (gameMode) {
                case GameMode.EightBall:
                    ReportEightBallScore();

                    return;
                case GameMode.NineBall:
                    ReportNineBallScore();

                    return;
                case GameMode.KoreanCarom:
                case GameMode.JapaneseCarom:
                case GameMode.ThreeCushionCarom:
                    ReportFourBallScore();

                    return;
            }
        }

        private void ResetScores()
        {
            if (logger)
                logger._Log(name, "ResetScores");

            poolMenu._SetScore(false, 0);
            poolMenu._SetScore(true, 0);
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (!Networking.IsOwner(localPlayer, gameObject) || !player.IsValid())
                return;

            var playerID = player.playerId;

            if (playerID != player1ID && playerID != player2ID && playerID != player3ID && playerID != player4ID)
                return;

            if (isGameInMenus)
                RemovePlayerFromGame(playerID);
            else
                _Reset(ResetReason.PlayerLeft);
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
                CannotEnterDesktopTopDownView();
        }

        private void CheckIfCanEnterDesktopTopDownView()
        {
            if (!isNearTable || numberOfCuesHeldByLocalPlayer <= 0)
                return;

            canEnterDesktopTopDownView = true;

            if (pancakeUI)
                pancakeUI.gameObject.SetActive(true);
        }

        private void CannotEnterDesktopTopDownView()
        {
            canEnterDesktopTopDownView = false;

            if (pancakeUI)
                pancakeUI.gameObject.SetActive(false);
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

        private void HandleBallSunk(bool isSuccess)
        {
            mainSrc.PlayOneShot(sinkSfx, 1.0f);

            // If good pocket
            if (isSuccess)
                HandleSuccessEffects();
            else
                HandleFoulEffects();
        }

        private void HandleSuccessEffects()
        {
            // Make a bright flash
            tableCurrentColour *= 1.9f;
            PlayAudioClip(successSfx);
        }

        private bool hasFoulBeenPlayedThisTurn;

        private void HandleFoulEffects()
        {
            if (hasFoulBeenPlayedThisTurn)
                return;

            hasFoulBeenPlayedThisTurn = true;

            tableCurrentColour = pointerColourErr;
            PlayAudioClip(foulSfx);
        }

        private void HandleBallCollision(int ballID, int otherBallID, Vector3 reflection)
        {
            if (logger)
                logger._Log(name, nameof(HandleBallCollision));
            
            // Prevent sound spam if it happens
            if (currentBallVelocities[ballID].sqrMagnitude > 0 && currentBallVelocities[otherBallID].sqrMagnitude > 0)
            {
                ballPoolTransforms[ballID].position = ballTransforms[ballID].position;
                ballPool[ballID].PlayOneShot(hitsSfx[UnityEngine.Random.Range(0, hitsSfx.Length - 1)], Mathf.Clamp01(currentBallVelocities[ballID].magnitude * reflection.magnitude));
            }

            if (ballID != 0)
                return;

            switch (gameMode) {
                case GameMode.EightBall:
                    if (firstHitBallThisTurn == 0) {
                        firstHitBallThisTurn = otherBallID;

                        if (IsFirstEightBallHitFoul(firstHitBallThisTurn, GetNumberOfSunkBlues(), GetNumberOfSunkOranges()))
                            HandleFoulEffects();
                    }

                    break;
                case GameMode.NineBall:
                    if (firstHitBallThisTurn == 0) {
                        firstHitBallThisTurn = otherBallID;
                    }

                    break;
                case GameMode.KoreanCarom:
                    HandleKorean4BallScoring(otherBallID);

                    break;
                case GameMode.JapaneseCarom:
                    HandleJapanese4BallScoring(otherBallID);

                     break;
                case GameMode.ThreeCushionCarom:
                    HandleThreeCushionCaromScoring(otherBallID);

                    break;
            }
        }
        
        private void PlayAudioClip(AudioClip clip)
        {
            if (!clip)
                return;

            mainSrc.PlayOneShot(clip);
        }
    }
}