using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace VRCBilliards
{
    /// <summary>
    /// Proprietary animation manager for M.O.O.N's UI
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class UIAnimationManager : UdonSharpBehaviour
    {
        public PoolMenu poolMenu;
        public Animator uiGamemodeToggle;
        public Animator uiGuideToggle;
        public Animator uiTeamToggle;

        public TextMeshProUGUI modeButtonText;
        public TextMeshProUGUI modeLeft;
        public TextMeshProUGUI modeRight;

        public FairlySadPanda.UsefulThings.Logger logger;

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

        public void _SwitchGamemodeState()
        {
            modeSelect = !modeSelect;
            UpdateSyncedVariables();
        }

        public void _SwitchGamemodeMenu()
        {
            isGamemodeMenuSwitched = !isGamemodeMenuSwitched;
            UpdateSyncedVariables();
        }

        public void _SwitchGuideMode()
        {
            isGuide = !isGuide;
            UpdateSyncedVariables();
        }

        public void _SwitchTeams()
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
                modeLeft.text = "Korean";
                modeRight.text = "Japanese";
            }
            else
            {
                modeButtonText.text = "Switch to 4 Ball Menu";
                modeLeft.text = "9 Ball";
                modeRight.text = "8 Ball";
            }

            if (isGuide)
            {
                poolMenu._EnableGuideline();
            }
            else
            {
                poolMenu._DisableGuideline();
            }

            if (isTeams)
            {
                poolMenu._DeselectTeams();
            }
            else
            {
                poolMenu._SelectTeams();
            }

            if (modeSelect)
            {
                if (isGamemodeMenuSwitched)
                {
                    //For 4 Ball Japanese
                    poolMenu._Select4BallJapanese();
                    if (logger) logger._Log(name, "Switched to 4Ball JPN");
                }
                else
                {
                    //For 8Ball
                    poolMenu._Select8Ball();
                    if (logger) logger._Log(name, "Switched to 8Ball");
                }
            }
            else
            {
                if (isGamemodeMenuSwitched)
                {
                    poolMenu._Select4BallKorean();
                    if (logger) logger._Log(name, "Switched to 4Ball KOR");
                }
                else
                {
                    //For 9Ball
                    poolMenu._Select9Ball();
                    if (logger) logger._Log(name, "Switched to 9Ball");
                }
            }
        }
    }
}
