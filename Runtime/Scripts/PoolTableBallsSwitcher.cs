using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PoolTableBallsSwitcher : UdonSharpBehaviour
{
    [Header("HQ & LQ balls Mesh Renderers")]
    [SerializeField] private MeshFilter[] balls;
    [Space]
    [SerializeField] private Mesh[] ballHQ;
    [SerializeField] private Mesh[] ballLQ;
    [FormerlySerializedAs("keepQuestOnLowQuality")]
    [FormerlySerializedAs("kepQuestOnLowQuality")]
    [Header("Defaults")]
    [SerializeField, Tooltip("Should Quest only use low quality balls? This will save draw calls.")] private bool questLowQualityOnly;
    [SerializeField] private bool defaultsHighQualityOnPC = true;
    [SerializeField] private bool defaultsLowQualityOnQuest = true;
    private bool isHighQuality = true;
    
    void Start()
    {
#if UNITY_ANDROID
        if (!defaultsLowQualityOnQuest) {
            return;
        }
#else
        if (defaultsHighQualityOnPC)
        {
            return;
        }
#endif        
        
        _enableLQ();
    }
    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (Networking.LocalPlayer.playerId == player.playerId)
        {
            _enableHQ();
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (Networking.LocalPlayer.playerId == player.playerId)
        {
            _enableLQ();
        }
    }

    public void _switchQuality()
    {
#if UNITY_ANDROID
        if (questLowQualityOnly) {
            return;
        }
#endif
        if (isHighQuality)
        {
            _enableLQ();
        }
        else
        {
            _enableHQ();
        }
    }

    public void _enableLQ()
    {
        for (int i = 0; i < 16; i++)
        {
            if(balls[i] != null)
            {
                if(ballLQ[i] != null)
                {
                    balls[i].mesh = ballLQ[i];
                }
            }
        }
        
        isHighQuality = false;
    }

    public void _enableHQ()
    {
        #if UNITY_ANDROID
        if (questLowQualityOnly)
        {
            return;
        }
        #endif
        
        for (int i = 0; i < 16; i++)
        {
            if (balls[i] != null)
            {
                if (ballHQ[i] != null)
                {
                    balls[i].mesh = ballHQ[i];
                }
            }
        }
        
        isHighQuality = true;
    }
}