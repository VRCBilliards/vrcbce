using UdonSharp;
using UnityEngine;
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
    [Header("Defaults")]
    [SerializeField] private bool kepQuestOnLowQuality = false;
    [SerializeField] private bool defaultsHighQualityOnPC = true;
    [SerializeField] private bool defaultsLowQualityOnQuest = true;
    private bool isQuest = false;
    private bool currentQuality = true;
    
    void Start()
    {
#if UNITY_ANDROID
isQuest = true;
#endif
        if (!isQuest && defaultsHighQualityOnPC)
        {
            return;
        }
        if (!defaultsLowQualityOnQuest && defaultsHighQualityOnPC)
            return;
        else
        {
            _enableLQ();
        }
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
        if (isQuest && kepQuestOnLowQuality) return;
        if (currentQuality)
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
        currentQuality = false;
    }

    public void _enableHQ()
    {
        if (isQuest && kepQuestOnLowQuality) return;
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
        currentQuality = true;
    }
}