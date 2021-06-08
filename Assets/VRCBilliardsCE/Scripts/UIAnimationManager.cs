
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRCBilliards;

public class UIAnimationManager : UdonSharpBehaviour
{
    public PoolMenu poolMenu;
    public Animator UIGamemodeToggle;
    public Animator UIGuideToggle;
    public Animator UITeamToggle;

    public Text ModeButtonText;
    public Text ModeLeft;
    public Text ModeRight;

    public bool IsGamemodeMenuSwitched = false;

    [SerializeField] private bool isDebug = false;

    public void SwitchGamemodeState()
    {
        //Switches the Animation of the Toggle Slider thingy.
        UIGamemodeToggle.SetBool("Toggle", !UIGamemodeToggle.GetBool("Toggle"));

        bool ModeSelect = UIGamemodeToggle.GetBool("Toggle");

        if (ModeSelect)
        {
            if (IsGamemodeMenuSwitched)
            {
                //For 4 Ball Japanese
                poolMenu.Select4BallJapanese();
                if (isDebug) Debug.Log("Switched to 4Ball JPN");
            }
            else
            {
                //For 8Ball
                poolMenu.Select8Ball();
                if (isDebug) Debug.Log("Switched to 8Ball");
            }
        } else
        {
            if (IsGamemodeMenuSwitched)
            {
                poolMenu.Select4BallKorean();
                if (isDebug) Debug.Log("Switched to 4Ball KOR");
            }
            else
            {
                //For 9Ball
                poolMenu.Select9Ball();
                if (isDebug) Debug.Log("Switched to 9Ball");
            }
        }
    }

    public void SwitchGamemodeMenu()
    {
        IsGamemodeMenuSwitched = !IsGamemodeMenuSwitched;

        if (IsGamemodeMenuSwitched)
        {
            ModeButtonText.text = "Switch to Traditional Menu";
            ModeLeft.text = "Japanese";
            ModeRight.text = "Korean";
        } else
        {
            ModeButtonText.text = "Switch to 4 Ball Menu";
            ModeLeft.text = "9 Ball";
            ModeRight.text = "8 Ball";
        }

    }

    public void SwitchGuideMode()
    {
        UIGuideToggle.SetBool("Toggle", !UIGuideToggle.GetBool("Toggle"));

        bool isGuide = UIGuideToggle.GetBool("Toggle");

        if (isGuide)
        {
            poolMenu.EnableGuideline();
        }else
        {
            poolMenu.DisableGuideline();
        }
    }

    public void SwitchTeams()
    {
        UITeamToggle.SetBool("Toggle", !UITeamToggle.GetBool("Toggle"));

        bool isGuide = UITeamToggle.GetBool("Toggle");

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
