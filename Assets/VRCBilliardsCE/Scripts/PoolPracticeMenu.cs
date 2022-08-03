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
        public void _UpdateMenu(int currentTurn, int latestTurn)
        {
            undoButton.interactable = currentTurn > 0;
            redoButton.interactable = currentTurn < latestTurn;
            undoStatusText.text = $"{latestTurn - currentTurn} turn(s) behind";
            replayShotButton.interactable = currentTurn > 0;
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