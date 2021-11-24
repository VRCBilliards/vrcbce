
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRCBilliards;
using UnityEngine.UI;

namespace VRCBilliards
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PoolPracticeMenu : UdonSharpBehaviour
    {
        public PoolStateManager manager;
        [Header("UndoRedo")] public TextMeshProUGUI undoStatusText;
        public Button undoButton;
        public Button redoButton;
        [Header("Preview Sim")] public Animator previewAnimator;
        public TextMeshProUGUI previewText;
        public Animator previewCueAnimator;
        public TextMeshProUGUI previewCueText;
        [Header("Replay Shot")] public Button replayShotButton;

        private void Start()
        {
            if (manager == null)
                manager = transform.parent.parent.GetComponentInChildren<PoolStateManager>();
        }
        public void _UndoTurn()
        {
            manager._UndoTurn();
        }

        public void _RedoTurn()
        {
            manager._RedoTurn();
        }

        public void _ReplayPhysics()
        {
            manager._ReplayPhysics();
        }

        public void _SwitchPreShotMode()
        {
            manager._SwitchPreShotMode();
        }

        public void _SwitchPreShotCueMode()
        {
            manager._SwitchPreShotCueMode();

        }
        public void _UpdateMenu(int currentTurn, int latestTurn, bool isPractice, bool preShotMode, bool previewOnlyCueBall)
        {
            undoButton.interactable = currentTurn > 0;
            redoButton.interactable = currentTurn < latestTurn;
            undoStatusText.text = $"{latestTurn - currentTurn} turn(s) behind";
            if (gameObject.activeSelf) previewAnimator.SetBool("Toggle", preShotMode);
            previewText.text = $"Preview {(preShotMode? "On": "Off")}";
            replayShotButton.interactable = currentTurn > 0;
            if (gameObject.activeSelf) previewCueAnimator.SetBool("Toggle", previewOnlyCueBall);
            previewCueText.text = $"Previewing {(previewOnlyCueBall ? "Cue" : "All")} Balls";
        }

        public void _EnablePracticeMenu()
        {
            gameObject.SetActive(true);
        }

        public void _DisablePracticeMenu()
        {
            gameObject.SetActive(false);
        }
    }
}
