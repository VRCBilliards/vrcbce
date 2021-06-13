
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

    [HideInInspector]
    public bool isGamemodeMenuSwitched;

    public FairlySadPanda.Utilities.Logger logger;

    public void SwitchGamemodeState()
    {
        //Switches the Animation of the Toggle Slider thingy.
        uiGamemodeToggle.SetBool("Toggle", !uiGamemodeToggle.GetBool("Toggle"));

        bool ModeSelect = uiGamemodeToggle.GetBool("Toggle");

        if (ModeSelect)
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

    public void SwitchGamemodeMenu()
    {
        isGamemodeMenuSwitched = !isGamemodeMenuSwitched;

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

    }

    public void SwitchGuideMode()
    {
        uiGuideToggle.SetBool("Toggle", !uiGuideToggle.GetBool("Toggle"));

        bool isGuide = uiGuideToggle.GetBool("Toggle");

        if (isGuide)
        {
            poolMenu.EnableGuideline();
        }
        else
        {
            poolMenu.DisableGuideline();
        }
    }

    public void SwitchTeams()
    {
        uiTeamToggle.SetBool("Toggle", !uiTeamToggle.GetBool("Toggle"));

        bool isGuide = uiTeamToggle.GetBool("Toggle");

        if (isGuide)
        {
            poolMenu.DeselectTeams();
        }
        else
        {
            poolMenu.SelectTeams();
        }
    }
}
