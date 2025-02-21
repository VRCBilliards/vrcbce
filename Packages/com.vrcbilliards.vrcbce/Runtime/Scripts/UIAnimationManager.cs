using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// Animation manager for M.O.O.N's UI.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class UIAnimationManager : UdonSharpBehaviour
    {
        public PoolMenu poolMenu;
        public Animator uiGuideToggle;
        public Animator uiTeamToggle;

        public TextMeshProUGUI modeButtonText;
        public TextMeshProUGUI modeLeft;
        public TextMeshProUGUI modeRight;

        public Logger logger;
        
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
            uiGuideToggle.SetBool("Toggle", isGuide);
            uiTeamToggle.SetBool("Toggle", isTeams);

            if (isGuide)
                poolMenu._EnableGuideline();
            else
                poolMenu._DisableGuideline();

            if (isTeams)
                poolMenu._DeselectTeams();
            else
                poolMenu._SelectTeams();
        }
    }
}
