using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Editor.Scripts
{
    [CustomEditor(typeof(PoolStateManager))]
    public class PoolStateManagerEditor : UnityEditor.Editor
    {
        private VisualElement _root;

        // These are named like this to match naming within UXML file and use nameof.
        private VisualElement Categories;
        private VisualElement UserProperties;
        private Label HeaderLabel;

        private const string HEADER_TEXT =
            "This is the Pool State Manager, the central script that controls VRCBCE's functionality. You shouldn't need to change many fields inside this script. \n\n" +
            "The debug settings below are of use for custom tables with different dimensions.\n\n" + "This UI was kindly provided by VowganVR.";

        private SerializedProperty _propPoolMenu;
        private SerializedProperty _propPoolCues;
        private SerializedProperty _propPancakeUI;
        private SerializedProperty _propSlogger;
        private SerializedProperty _propLogger;
        private SerializedProperty _propTableWidth;
        private SerializedProperty _propTableHeight;
        private SerializedProperty _propCornerPocket;
        private SerializedProperty _propMiddlePocket;
        private SerializedProperty _propPocketOuterRadius;
        private SerializedProperty _propPocketInnerRadius;
        private SerializedProperty _propBallDiameter;
        private SerializedProperty _propSPOT_POSITION_X;
        private SerializedProperty _propSPOT_CAROM_X;
        private SerializedProperty _propBALL_PL_X;
        private SerializedProperty _propBALL_PL_Y;
        private SerializedProperty _propFakeBallShadows;
        private SerializedProperty _propTableModelHasRails;
        private SerializedProperty _propSunkBallsPositionRoot;
        private SerializedProperty _propShadows;
        private SerializedProperty _propPlusOneParticleSystem;
        private SerializedProperty _propMinusOneParticleSystem;
        private SerializedProperty _propTableSurface;
        private SerializedProperty _propUniformTableColour;
        private SerializedProperty _propUniformMarkerColour;
        private SerializedProperty _propUniformBallColour;
        private SerializedProperty _propUniformBallFloat;
        private SerializedProperty _propBallMaskToggle;
        private SerializedProperty _propIntroAnimationLength;
        private SerializedProperty _propTableBlue;
        private SerializedProperty _propTableOrange;
        private SerializedProperty _propTableRed;
        private SerializedProperty _propTableWhite;
        private SerializedProperty _propTableBlack;
        private SerializedProperty _propTableYellow;
        private SerializedProperty _propTableLightBlue;
        private SerializedProperty _propMarkerOK;
        private SerializedProperty _propMarkerNotOK;
        private SerializedProperty _propGripColourActive;
        private SerializedProperty _propGripColourInactive;
        private SerializedProperty _propFabricGray;
        private SerializedProperty _propFabricBlue;
        private SerializedProperty _propFabricGreen;
        private SerializedProperty _propBallCustomColours;
        private SerializedProperty _propBlueTeamSliders;
        private SerializedProperty _propOrangeTeamSliders;
        private SerializedProperty _propCueTips;
        private SerializedProperty _propCueRenderObjs;
        private SerializedProperty _propBallTransforms;
        private SerializedProperty _propBallShadowPosConstraints;
        private SerializedProperty _propGuideline;
        private SerializedProperty _propDevhit;
        private SerializedProperty _propPlayerTotems;
        private SerializedProperty _propMarker;
        private SerializedProperty _propMarker9ball;
        private SerializedProperty _propPocketBlockers;
        private SerializedProperty _propBallRenderers;
        private SerializedProperty _propTableRenderer;
        private SerializedProperty _propSets;
        private SerializedProperty _propCueGrips;
        private SerializedProperty _propAudioSourcePoolContainer;
        private SerializedProperty _propCueTipSrc;
        private SerializedProperty _propIntroSfx;
        private SerializedProperty _propSinkSfx;
        private SerializedProperty _propSuccessSfx;
        private SerializedProperty _propFoulSfx;
        private SerializedProperty _propWinnerSfx;
        private SerializedProperty _propHitsSfx;
        private SerializedProperty _propNewTurnSfx;
        private SerializedProperty _propPointMadeSfx;
        private SerializedProperty _propHitBallSfx;
        private SerializedProperty _propTableReflection;
        private SerializedProperty _propCueballMeshes;
        private SerializedProperty _propNineBall;
        private SerializedProperty _propDesktopHitPosition;
        private SerializedProperty _propDesktopBase;
        private SerializedProperty _propDesktopCueParents;
        private SerializedProperty _propTiltAmount;
        private SerializedProperty _propScoreNeededToWinCarom;
        private SerializedProperty _propDesktopAngleIncrement;
        private SerializedProperty _propCueBallController;
        private SerializedProperty _propPowerBar;
        private SerializedProperty _propTopBar;
        private SerializedProperty _propInitialPowerBarPos;
        private SerializedProperty _propMAX_SIMULATION_TIME_PER_FRAME;
        private SerializedProperty _propTIME_PER_STEP;
        private SerializedProperty _propEARTH_GRAVITY;
        private SerializedProperty _propMASS_OF_BALL;
        private SerializedProperty _propCONTACT_POINT;
        private SerializedProperty _propShowEditorDebugBoundaries;
        private SerializedProperty _propShowEditorDebugCarom;
        private SerializedProperty _propShowEditorDebug8ball;
        private SerializedProperty _propShowEditorDebug9Ball;
        private SerializedProperty _propShowEditorDebugThreeCushionCarom;

        private const int FOLDOUT_PADDING_BOTTOM = 8;

        
        private void OnEnable()
        {
            #region Find Properties
            
            _propPoolMenu
                = serializedObject.FindProperty(nameof(PoolStateManager.poolMenu));
            _propPoolCues
                = serializedObject.FindProperty(nameof(PoolStateManager.poolCues));
            _propPancakeUI
                = serializedObject.FindProperty(nameof(PoolStateManager.pancakeUI));
            _propSlogger
                = serializedObject.FindProperty(nameof(PoolStateManager.slogger));
            _propLogger
                = serializedObject.FindProperty(nameof(PoolStateManager.logger));
            _propTableWidth
                = serializedObject.FindProperty(nameof(PoolStateManager.tableWidth));
            _propTableHeight
                = serializedObject.FindProperty(nameof(PoolStateManager.tableHeight));
            _propCornerPocket
                = serializedObject.FindProperty(nameof(PoolStateManager.cornerPocket));
            _propMiddlePocket
                = serializedObject.FindProperty(nameof(PoolStateManager.middlePocket));
            _propPocketOuterRadius
                = serializedObject.FindProperty(nameof(PoolStateManager.pocketOuterRadius));
            _propPocketInnerRadius
                = serializedObject.FindProperty(nameof(PoolStateManager.pocketInnerRadius));
            _propBallDiameter
                = serializedObject.FindProperty(nameof(PoolStateManager.ballDiameter));
            _propSPOT_POSITION_X
                = serializedObject.FindProperty(nameof(PoolStateManager.SPOT_POSITION_X));
            _propSPOT_CAROM_X
                = serializedObject.FindProperty(nameof(PoolStateManager.SPOT_CAROM_X));
            _propBALL_PL_X
                = serializedObject.FindProperty(nameof(PoolStateManager.BALL_PL_X));
            _propBALL_PL_Y
                = serializedObject.FindProperty(nameof(PoolStateManager.BALL_PL_Y));
            _propFakeBallShadows
                = serializedObject.FindProperty(nameof(PoolStateManager.fakeBallShadows));
            _propTableModelHasRails
                = serializedObject.FindProperty(nameof(PoolStateManager.tableModelHasRails));
            _propSunkBallsPositionRoot
                = serializedObject.FindProperty(nameof(PoolStateManager.sunkBallsPositionRoot));
            _propShadows
                = serializedObject.FindProperty(nameof(PoolStateManager.shadows));
            _propPlusOneParticleSystem
                = serializedObject.FindProperty(nameof(PoolStateManager.plusOneParticleSystem));
            _propMinusOneParticleSystem
                = serializedObject.FindProperty(nameof(PoolStateManager.minusOneParticleSystem));
            _propTableSurface
                = serializedObject.FindProperty(nameof(PoolStateManager.tableSurface));
            _propUniformTableColour
                = serializedObject.FindProperty(nameof(PoolStateManager.uniformTableColour));
            _propUniformMarkerColour
                = serializedObject.FindProperty(nameof(PoolStateManager.uniformMarkerColour));
            _propUniformBallColour
                = serializedObject.FindProperty(nameof(PoolStateManager.uniformBallColour));
            _propUniformBallFloat
                = serializedObject.FindProperty(nameof(PoolStateManager.uniformBallFloat));
            _propBallMaskToggle
                = serializedObject.FindProperty(nameof(PoolStateManager.ballMaskToggle));
            _propIntroAnimationLength
                = serializedObject.FindProperty(nameof(PoolStateManager.introAnimationLength));
            _propTableBlue
                = serializedObject.FindProperty(nameof(PoolStateManager.tableBlue));
            _propTableOrange
                = serializedObject.FindProperty(nameof(PoolStateManager.tableOrange));
            _propTableRed
                = serializedObject.FindProperty(nameof(PoolStateManager.tableRed));
            _propTableWhite
                = serializedObject.FindProperty(nameof(PoolStateManager.tableWhite));
            _propTableBlack
                = serializedObject.FindProperty(nameof(PoolStateManager.tableBlack));
            _propTableYellow
                = serializedObject.FindProperty(nameof(PoolStateManager.tableYellow));
            _propTableLightBlue
                = serializedObject.FindProperty(nameof(PoolStateManager.tableLightBlue));
            _propMarkerOK
                = serializedObject.FindProperty(nameof(PoolStateManager.markerOK));
            _propMarkerNotOK
                = serializedObject.FindProperty(nameof(PoolStateManager.markerNotOK));
            _propGripColourActive
                = serializedObject.FindProperty(nameof(PoolStateManager.gripColourActive));
            _propGripColourInactive
                = serializedObject.FindProperty(nameof(PoolStateManager.gripColourInactive));
            _propFabricGray
                = serializedObject.FindProperty(nameof(PoolStateManager.fabricGray));
            _propFabricBlue
                = serializedObject.FindProperty(nameof(PoolStateManager.fabricBlue));
            _propFabricGreen
                = serializedObject.FindProperty(nameof(PoolStateManager.fabricGreen));
            _propBallCustomColours
                = serializedObject.FindProperty(nameof(PoolStateManager.ballCustomColours));
            _propBlueTeamSliders
                = serializedObject.FindProperty(nameof(PoolStateManager.blueTeamSliders));
            _propOrangeTeamSliders
                = serializedObject.FindProperty(nameof(PoolStateManager.orangeTeamSliders));
            _propCueTips
                = serializedObject.FindProperty(nameof(PoolStateManager.cueTips));
            _propCueRenderObjs
                = serializedObject.FindProperty(nameof(PoolStateManager.cueRenderObjs));
            _propBallTransforms
                = serializedObject.FindProperty(nameof(PoolStateManager.ballTransforms));
            _propBallShadowPosConstraints
                = serializedObject.FindProperty(nameof(PoolStateManager.ballShadowPosConstraints));
            _propGuideline
                = serializedObject.FindProperty(nameof(PoolStateManager.guideline));
            _propDevhit
                = serializedObject.FindProperty(nameof(PoolStateManager.devhit));
            _propPlayerTotems
                = serializedObject.FindProperty(nameof(PoolStateManager.playerTotems));
            _propMarker
                = serializedObject.FindProperty(nameof(PoolStateManager.marker));
            _propMarker9ball
                = serializedObject.FindProperty(nameof(PoolStateManager.marker9ball));
            _propPocketBlockers
                = serializedObject.FindProperty(nameof(PoolStateManager.pocketBlockers));
            _propBallRenderers
                = serializedObject.FindProperty(nameof(PoolStateManager.ballRenderers));
            _propTableRenderer
                = serializedObject.FindProperty(nameof(PoolStateManager.tableRenderer));
            _propSets
                = serializedObject.FindProperty(nameof(PoolStateManager.sets));
            _propCueGrips
                = serializedObject.FindProperty(nameof(PoolStateManager.cueGrips));
            _propAudioSourcePoolContainer
                = serializedObject.FindProperty(nameof(PoolStateManager.audioSourcePoolContainer));
            _propCueTipSrc
                = serializedObject.FindProperty(nameof(PoolStateManager.cueTipSrc));
            _propIntroSfx
                = serializedObject.FindProperty(nameof(PoolStateManager.introSfx));
            _propSinkSfx
                = serializedObject.FindProperty(nameof(PoolStateManager.sinkSfx));
            _propSuccessSfx
                = serializedObject.FindProperty(nameof(PoolStateManager.successSfx));
            _propFoulSfx
                = serializedObject.FindProperty(nameof(PoolStateManager.foulSfx));
            _propWinnerSfx
                = serializedObject.FindProperty(nameof(PoolStateManager.winnerSfx));
            _propHitsSfx
                = serializedObject.FindProperty(nameof(PoolStateManager.hitsSfx));
            _propNewTurnSfx
                = serializedObject.FindProperty(nameof(PoolStateManager.newTurnSfx));
            _propPointMadeSfx
                = serializedObject.FindProperty(nameof(PoolStateManager.pointMadeSfx));
            _propHitBallSfx
                = serializedObject.FindProperty(nameof(PoolStateManager.hitBallSfx));
            _propTableReflection
                = serializedObject.FindProperty(nameof(PoolStateManager.tableReflection));
            _propCueballMeshes
                = serializedObject.FindProperty(nameof(PoolStateManager.cueballMeshes));
            _propNineBall
                = serializedObject.FindProperty(nameof(PoolStateManager.nineBall));
            _propDesktopHitPosition
                = serializedObject.FindProperty(nameof(PoolStateManager.desktopHitPosition));
            _propDesktopBase
                = serializedObject.FindProperty(nameof(PoolStateManager.desktopBase));
            _propDesktopCueParents
                = serializedObject.FindProperty(nameof(PoolStateManager.desktopCueParents));
            _propTiltAmount
                = serializedObject.FindProperty(nameof(PoolStateManager.tiltAmount));
            _propScoreNeededToWinCarom
                = serializedObject.FindProperty(nameof(PoolStateManager.scoreNeededToWinCarom));
            _propDesktopAngleIncrement
                = serializedObject.FindProperty(nameof(PoolStateManager.desktopAngleIncrement));
            _propCueBallController
                = serializedObject.FindProperty(nameof(PoolStateManager.cueBallController));
            _propPowerBar
                = serializedObject.FindProperty(nameof(PoolStateManager.powerBar));
            _propTopBar
                = serializedObject.FindProperty(nameof(PoolStateManager.topBar));
            _propInitialPowerBarPos
                = serializedObject.FindProperty(nameof(PoolStateManager.initialPowerBarPos));
            _propMAX_SIMULATION_TIME_PER_FRAME
                = serializedObject.FindProperty(nameof(PoolStateManager.MAX_SIMULATION_TIME_PER_FRAME));
            _propTIME_PER_STEP
                = serializedObject.FindProperty(nameof(PoolStateManager.TIME_PER_STEP));
            _propEARTH_GRAVITY
                = serializedObject.FindProperty(nameof(PoolStateManager.EARTH_GRAVITY));
            _propMASS_OF_BALL
                = serializedObject.FindProperty(nameof(PoolStateManager.MASS_OF_BALL));
            _propCONTACT_POINT
                = serializedObject.FindProperty(nameof(PoolStateManager.CONTACT_POINT));
            _propShowEditorDebugBoundaries
                = serializedObject.FindProperty(nameof(PoolStateManager.showEditorDebugBoundaries));
            _propShowEditorDebugCarom
                = serializedObject.FindProperty(nameof(PoolStateManager.showEditorDebugCarom));
            _propShowEditorDebug8ball
                = serializedObject.FindProperty(nameof(PoolStateManager.showEditorDebug8ball));
            _propShowEditorDebug9Ball
                = serializedObject.FindProperty(nameof(PoolStateManager.showEditorDebug9Ball));
            _propShowEditorDebugThreeCushionCarom
                = serializedObject.FindProperty(nameof(PoolStateManager.showEditorDebugThreeCushionCarom));
            
            #endregion
        }
        
        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();

            VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("VRCBCE_PoolStateManagerEditor");
            uxml.CloneTree(_root);

            Categories = _root.Q<VisualElement>(nameof(Categories));
            HeaderLabel = _root.Q<Label>(nameof(HeaderLabel));
            UserProperties = _root.Q<VisualElement>(nameof(UserProperties));

            HeaderLabel.text = HEADER_TEXT;

            PopulateElements();

            return _root;
        }

        /// <summary>
        /// Fill the inspector with all fields and categories.
        /// </summary>
        private void PopulateElements()
        {
            AddUserProperty(_propFakeBallShadows);
            AddUserProperty(_propTableModelHasRails);
            
            AddNewCategory("Debug Visuals", new[]
            {
                _propShowEditorDebugBoundaries,
                _propShowEditorDebugCarom,
                _propShowEditorDebug8ball,
                _propShowEditorDebug9Ball,
                _propShowEditorDebugThreeCushionCarom,
            });

            AddNewCategory("Table Settings", new[]
            {
                _propPoolMenu,
                _propPoolCues,
                _propPancakeUI,
                _propSlogger,
                _propLogger,
                _propTableWidth,
                _propTableHeight,
                _propCornerPocket,
                _propMiddlePocket,
                _propPocketOuterRadius,
                _propPocketInnerRadius,
                _propBallDiameter,
                _propSPOT_POSITION_X,
                _propSPOT_CAROM_X,
                _propBALL_PL_X,
                _propBALL_PL_Y,
            });

            AddNewCategory("Important Objects", new[]
            {
                _propSunkBallsPositionRoot,
                _propShadows,
                _propPlusOneParticleSystem,
                _propMinusOneParticleSystem,
                _propTableSurface,
            });

            AddNewCategory("Shader Information", new[]
            {
                _propUniformTableColour,
                _propUniformMarkerColour,
                _propUniformBallColour,
                _propUniformBallFloat,
                _propBallMaskToggle,
                _propIntroAnimationLength,
            });

            AddNewCategory("Table Colours", new[]
            {
                _propTableBlue,
                _propTableOrange,
                _propTableRed,
                _propTableWhite,
                _propTableBlack,
                _propTableYellow,
                _propTableLightBlue,
                _propMarkerOK,
                _propMarkerNotOK,
                _propGripColourActive,
                _propGripColourInactive,
                _propFabricGray,
                _propFabricBlue,
                _propFabricGreen,
            });

            AddNewCategory("Colour Options", new[]
            {
                _propBallCustomColours,
                _propBlueTeamSliders,
                _propOrangeTeamSliders,
                _propCueTips,
                _propCueRenderObjs,
            });

            AddNewCategory("Materials", new[]
            {
                _propBallTransforms,
                _propBallShadowPosConstraints,
                _propGuideline,
                _propDevhit,
                _propPlayerTotems,
                _propMarker,
                _propMarker9ball,
                _propPocketBlockers,
            });

            AddNewCategory("Audio", new[]
            {
                _propBallRenderers,
                _propTableRenderer,
                _propSets,
                _propCueGrips,
                _propAudioSourcePoolContainer,
                _propCueTipSrc,
                _propIntroSfx,
                _propSinkSfx,
                _propSuccessSfx,
                _propFoulSfx,
                _propWinnerSfx,
                _propHitsSfx,
                _propNewTurnSfx,
                _propPointMadeSfx,
                _propHitBallSfx,
            });

            AddNewCategory("Reflection Probes", new[]
            {
                _propTableReflection
            });

            AddNewCategory("Meshes", new[]
            {
                _propCueballMeshes,
                _propNineBall,
            });

            AddNewCategory("Desktop Stuff", new[]
            {
                _propDesktopHitPosition,
                _propDesktopBase,
                _propDesktopCueParents,
                _propTiltAmount,
                _propScoreNeededToWinCarom,
                _propDesktopAngleIncrement,
                _propCueBallController,
                _propPowerBar,
                _propTopBar,
                _propInitialPowerBarPos,
                _propMAX_SIMULATION_TIME_PER_FRAME,
                _propTIME_PER_STEP,
                _propEARTH_GRAVITY,
                _propMASS_OF_BALL,
                _propCONTACT_POINT,
            });
        }


        /// <summary>
        /// Adds a property to the top of the inspector,
        /// intended for all world creators to interact with rather than the bespoke developer settings. 
        /// </summary>
        /// <param name="property">Serialized property </param>
        private PropertyField AddUserProperty(SerializedProperty property)
        {
            PropertyField field = new PropertyField(property);
            UserProperties.Add(field);

            return field;
        }

        /// <summary>
        /// Custom foldout creation for mass property adding.
        /// </summary>
        /// <param name="label">The label used by the foldout.</param>
        /// <param name="properties">Any SerializedProperties that will be automatically added to the inspector.</param>
        /// <param name="addToRoot">Automatically add to the root of the inspector.</param>
        /// <param name="toolTip">Tooltip displayed while hovering over the Category Foldout.</param>
        private Foldout AddNewCategory(string label, SerializedProperty[] properties = null, bool addToRoot = true,
            string toolTip = "")
        {
            Foldout foldout = new Foldout
            {
                text = label,
                tooltip = toolTip,
                style = { paddingBottom = FOLDOUT_PADDING_BOTTOM, },
                viewDataKey = string.Concat(nameof(PoolStateManager), "/", label),
            };
            foldout.Q<Label>().style.unityFontStyleAndWeight = FontStyle.Bold;

            if (properties != null)
            {
                foreach (SerializedProperty prop in properties)
                    foldout.Add(new PropertyField(prop));
            }

            if (addToRoot)
                Categories.Add(foldout);
            return foldout;
        }
    }
}