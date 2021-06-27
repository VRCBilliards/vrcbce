
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Ocsp;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRCBilliards;

public class UIAnimationManager : UdonSharpBehaviour
{
    public PoolMenu poolMenu;
    public Animator uiGamemodeToggle;
    public Animator uiGuideToggle;
    public Animator uiTeamToggle;

    public TextMeshProUGUI modeButtonText;
    public TextMeshProUGUI modeLeft;
    public TextMeshProUGUI modeRight;

    public FairlySadPanda.Utilities.Logger logger;

    [UdonSynced]
    [HideInInspector]
    public bool isGamemodeMenuSwitched;

    [UdonSynced]
    private bool modeSelect = true;
    [UdonSynced]
    private bool isGuide = true;
    [UdonSynced]
    private bool isTeams = true;

    private void UpdateSyncedVariables()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        RequestSerialization();
        OnDeserialization();
    }

    public void SwitchGamemodeState()
    {
        modeSelect = !modeSelect;
        UpdateSyncedVariables();
    }

    public void SwitchGamemodeMenu()
    {
        isGamemodeMenuSwitched = !isGamemodeMenuSwitched;
        UpdateSyncedVariables();
    }

    public void SwitchGuideMode()
    {
        isGuide = !isGuide;
        UpdateSyncedVariables();
    }

    public void SwitchTeams()
    {
        isTeams = !isTeams;
        UpdateSyncedVariables();
    }

    public override void OnDeserialization()
    {
        uiGamemodeToggle.SetBool("Toggle", modeSelect);
        uiGuideToggle.SetBool("Toggle", isGuide);
        uiTeamToggle.SetBool("Toggle", isTeams);

        if (isGamemodeMenuSwitched)
        {
            modeButtonText.text = "Switch to Traditional Menu";
            modeLeft.text = "Japanese";
            modeRight.text = "Korean";
        }
        else
        {
            modeButtonText.text = "Switch to 4 Ball Menu";
            modeLeft.text = "9 Ball";
            modeRight.text = "8 Ball";
        }

        if (isGuide)
        {
            poolMenu.EnableGuideline();
        }
        else
        {
            poolMenu.DisableGuideline();
        }

        if (isTeams)
        {
            poolMenu.DeselectTeams();
        }
        else
        {
            poolMenu.SelectTeams();
        }

        if (modeSelect)
        {
            if (isGamemodeMenuSwitched)
            {
                //For 4 Ball Japanese
                poolMenu.Select4BallJapanese();
                if (logger) logger.Log(name, "Switched to 4Ball JPN");
            }
            else
            {
                //For 8Ball
                poolMenu.Select8Ball();
                if (logger) logger.Log(name, "Switched to 8Ball");
            }
        }
        else
        {
            if (isGamemodeMenuSwitched)
            {
                poolMenu.Select4BallKorean();
                if (logger) logger.Log(name, "Switched to 4Ball KOR");
            }
            else
            {
                //For 9Ball
                poolMenu.Select9Ball();
                if (logger) logger.Log(name, "Switched to 9Ball");
            }
        }
    }
}
