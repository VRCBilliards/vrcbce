
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRCBilliards;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PoolPracticeMenu : UdonSharpBehaviour
{
    public PoolStateManager manager;
    [Header("UndoRedo")] public TextMeshProUGUI undoStatusText;
    public Button undoButton;
    public Button redoButton;
    [Header("Preview Sim")]
    public Animator previewAnimator;

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
    public void _UpdateMenu(int currentTurn, int latestTurn, bool isPractice,bool preShotMode) {
        gameObject.SetActive(isPractice);
        undoButton.interactable = currentTurn > 1;
        redoButton.interactable = currentTurn < latestTurn;
        undoStatusText.text = $"{latestTurn - currentTurn} turn(s) behind";
        previewAnimator.SetBool("Toggle",preShotMode);
    }
    public void _EnablePracticeMenu() {
        gameObject.SetActive(true);
    }
    public void _DisablePracticeMenu() {
        gameObject.SetActive(false);
    }
}
