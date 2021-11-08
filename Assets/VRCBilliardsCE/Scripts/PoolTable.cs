
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace VRCBilliards
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class PoolTable : UdonSharpBehaviour
    {
        /// <summary>
        /// The Main pool table object mesh
        /// </summary>
        public MeshRenderer poolTableMesh;
        /// <summary>
        /// Contaains the mesh of the 4 ball pocket blockers.
        /// </summary>
        [Tooltip("Contaains the mesh of the 4 ball pocket blockers.")]
        public GameObject fourBallBlockers;

        /// <summary>
        /// Contains the reflection probe of the table.
        /// </summary>
        [Tooltip("Contains the reflection probe of the table.")]
        public Transform reflectionProbePositionOverride;

        /// <summary>
        /// Where sunk balls are positioned in the table.
        /// </summary>
        [Tooltip("Where sunk balls are positioned in the table.")]
        public Transform sunkBallPosition;

        /// <summary>
        /// Where the Main Menu, Practice Menu, and Unlock Button are positioned in the table.
        /// </summary>
        [Header("UI Targets")]
        [Tooltip("Where the Main Menu, Practice Menu, and Unlock Button are positioned in the table.")]
        public Transform mainMenuTarget;

        /// <summary>
        /// Where the Score Board is positioned in the table.
        /// </summary>
        [Tooltip("Where the Score Board is positioned in the table.")]
        public Transform scoreBoardTarget;

        /// <summary>
        /// Where the Secondary Score Board, End Game Button, and Hide UI Button are positioned in the table.
        /// </summary>
        [Tooltip("Where the Secondary Score Board, End Game Button, and Hide UI Button are positioned in the table.")]
        public Transform scoreBoardTarget2;
        /// <summary>
        /// Where each cue stick should be positioned in the table.
        /// </summary>
        [Header("Cue Stick Targets")]
        [Tooltip("Where each cue stick should be positioned in the table.")]
        public Transform[] cueStickTargets = new Transform[2];
        /// <summary>
        /// Should shadows be cast on the balls? Not recommended for glass/transparent tables.
        /// </summary>
        [Header("Table Options")]
        [Tooltip("Should shadows be cast on the balls? Not recommended for glass/transparent tables.")]
        public bool useFakeShadows = true;
        /// <summary>
        /// Does the table have ball return rails? If so the ball will roll down them
        /// </summary>
        [Tooltip("Does the table have ball return rails? If so the ball will roll down them.")]
        public bool tabelHasRails = false;
        /// <summary>
        /// This value scales the clamp applied to the velocity of pocketted balls. Raising this will make pockets look less artificial at the cost of increasing the chance high-velocity balls will fly out of the table.
        /// </summary>
        [Tooltip("This value scales the clamp applied to the velocity of pocketted balls. Raising this will make pockets look less artificial at the cost of increasing the chance high-velocity balls will fly out of the table.")]
        public float pocketVelocityClamp = 1;
        
        [Header("Table Audio Overrides")]
        public AudioClip introSfx;
        public AudioClip sinkSfx;
        public AudioClip[] hitsSfx;
        public AudioClip newTurnSfx;
        public AudioClip pointMadeSfx;
        public AudioClip spinSfx;
        public AudioClip spinStopSfx;
        public AudioClip hitBallSfx;

        [Header("Shader Parameters")] public string uniformTableColor = "_EmissionColor";
    }
}

