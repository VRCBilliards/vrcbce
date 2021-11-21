using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace VRCBilliards
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PoolMenu : UdonSharpBehaviour
    {
        private PoolStateManager manager;

        [Header("Style")]
        public Color selectedColor = Color.white;
        public Color unselectedColor = Color.gray;

        [Header("Menu / Buttons")]
        public bool useUnityUI;
        public Button player1UIButton;
        public Button player2UIButton;
        public Button player3UIButton;
        public Button player4UIButton;

        public GameObject resetGameButton;
        public GameObject lockMenu;
        public GameObject mainMenu;

        public GameObject startGameButton;

        [Header("Game Mode")]
        public TextMeshProUGUI gameModeTxt;
        public Image[] gameModeButtons = { };

        [Header("Guide Line")]
        public bool toggleGuideLineButtonsActive = true;
        public GameObject guideLineEnableButton;
        public GameObject guideLineDisableButton;
        public TextMeshProUGUI guidelineStatus;
        public Image[] guideLineButtons = { };

        [Header("Timer")]
        public TextMeshProUGUI timer;
        public string noTimerText = "No Limit";
        public string timerValueText = "{}s Limit";
        public Image timerButton, noTimerButton;
        public TextMeshProUGUI visibleTimerDuringGame;
        public Image timerCountdown;
        public string timerOutputFormat = "{} seconds remaining";

        [Header("Teams")]
        public TextMeshProUGUI teamsTxt;
        public Image[] teamsButtons = { };

        [Header("Players")]
        public GameObject player1Button;
        public GameObject player2Button;
        public GameObject player3Button;
        public GameObject player4Button;
        public GameObject leaveButton;
        public string defaultEmptyPlayerSlotText = "<color=grey>Player {}</color>";
        public TextMeshProUGUI player1MenuText;
        public TextMeshProUGUI player2MenuText;
        public TextMeshProUGUI player3MenuText;
        public TextMeshProUGUI player4MenuText;

        [Header("Score")]
        [Tooltip("A specific score display that only shows during gameplay. If this is populated, the table info display will be hidden during gameplay.")]
        public GameObject inGameScoreDisplay;
        [Tooltip("The table information screen. This will be hidden during games if the in-game score display is populated.")]
        public GameObject tableInfoScreen;
        public GameObject[] scores;

        public GameObject player1Score;
        public GameObject player2Score;
        public GameObject player3Score;
        public GameObject player4Score;
        public GameObject teamAScore;
        public GameObject teamBScore;
        
        private TextMeshProUGUI[] player1Scores;
        private TextMeshProUGUI[] player2Scores;
        private TextMeshProUGUI[] player3Scores;
        private TextMeshProUGUI[] player4Scores;

        private TextMeshProUGUI[] teamAScores;
        private TextMeshProUGUI[] teamBScores;

        public TextMeshProUGUI winnerText;

        [Header("UdonChips Integration")]
        public string defaultEmptyplayerSlotTextWithUdonChips = "{}uc to play";

        private bool isTeams;
        private bool isSignedUpToPlay;
        private bool canStartGame;

        public void Start()
        {
            manager = transform.parent.GetComponentInChildren<PoolStateManager>();
            if (!manager)
            {
                Debug.LogError($"A VRCBCE menu, {name}, cannot start because it cannot find a PoolStateManager in the children of its parent!");
                gameObject.SetActive(false);
                return;
            }

            player1Scores = new TextMeshProUGUI[scores.Length];
            player2Scores = new TextMeshProUGUI[scores.Length];
            player3Scores = new TextMeshProUGUI[scores.Length];
            player4Scores = new TextMeshProUGUI[scores.Length];
            teamAScores = new TextMeshProUGUI[scores.Length];
            teamBScores = new TextMeshProUGUI[scores.Length];

            for (int i = 0; i < scores.Length; i++)
            {
                player1Scores[i] = scores[i].transform.Find(player1Score.name).GetComponent<TextMeshProUGUI>();
                player2Scores[i] = scores[i].transform.Find(player2Score.name).GetComponent<TextMeshProUGUI>();
                player3Scores[i] = scores[i].transform.Find(player3Score.name).GetComponent<TextMeshProUGUI>();
                player4Scores[i] = scores[i].transform.Find(player4Score.name).GetComponent<TextMeshProUGUI>();
                teamAScores[i] = scores[i].transform.Find(teamAScore.name).GetComponent<TextMeshProUGUI>();
                teamBScores[i] = scores[i].transform.Find(teamBScore.name).GetComponent<TextMeshProUGUI>();
            }
        }

        // TODO: This all needs to be secured.
        public void _UnlockTable()
        {
            manager._UnlockTable();
        }

        public void _LockTable()
        {
            manager._LockTable();
        }

        public void _SelectTeams()
        {
            manager._SelectTeams();
        }

        public void _DeselectTeams()
        {
            manager._DeselectTeams();
        }

        public void _Select4BallJapanese()
        {
            manager._Select4BallJapanese();
        }

        public void _Select4BallKorean()
        {
            manager._Select4BallKorean();
        }

        public void _Select8Ball()
        {
            manager._Select8Ball();
        }

        public void _Select9Ball()
        {
            manager._Select9Ball();
        }

        public void _IncreaseTimer()
        {
            manager._IncreaseTimer();
        }

        public void _DecreaseTimer()
        {
            manager._DecreaseTimer();
        }

        public void _EnableGuideline()
        {
            manager._EnableGuideline();
        }

        public void _DisableGuideline()
        {
            manager._DisableGuideline();
        }

        public void _SignUpAsPlayer1()
        {
            if (!isSignedUpToPlay)
            {
                manager._JoinGame(0);
            }
            else
            {
                manager._Raise();
            }
        }

        public void _SignUpAsPlayer2()
        {
            if (!isSignedUpToPlay)
            {
                manager._JoinGame(1);
            }
        }

        public void _SignUpAsPlayer3()
        {
            if (!isSignedUpToPlay)
            {
                manager._JoinGame(2);
            }
        }

        public void _SignUpAsPlayer4()
        {
            if (!isSignedUpToPlay)
            {
                manager._JoinGame(3);
            }
        }

        public void _LeaveGame()
        {
            manager._LeaveGame();
        }

        public void _StartGame()
        {
            if (canStartGame)
            {
                manager._StartNewGame();
            }
        }

        public void _EndGame()
        {
            if (isSignedUpToPlay)
            {
                manager._ForceReset();
            }
        }

        public void _EnableResetButton()
        {
            resetGameButton.SetActive(true);
            lockMenu.SetActive(false);
            mainMenu.SetActive(false);

            winnerText.text = "";

            if (inGameScoreDisplay)
            {
                inGameScoreDisplay.SetActive(true);

                if (tableInfoScreen)
                {
                    tableInfoScreen.SetActive(false);
                }
            }
        }

        public void _EnableUnlockTableButton()
        {
            resetGameButton.SetActive(false);
            lockMenu.SetActive(true);
            mainMenu.SetActive(false);

            ResetScoreScreen();
        }

        public void _EnableMainMenu()
        {
            resetGameButton.SetActive(false);
            lockMenu.SetActive(false);
            mainMenu.SetActive(true);
            visibleTimerDuringGame.text = "";

            if (inGameScoreDisplay)
            {
                inGameScoreDisplay.SetActive(false);
            }

            if (tableInfoScreen)
            {
                tableInfoScreen.SetActive(true);
            }
        }

        private void UpdateButtonColors(Image[] buttons, int selectedIndex)
        {
            if (buttons == null) return;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;

                buttons[i].color = i == selectedIndex ? selectedColor : unselectedColor;
            }
        }

        /// <summary>
        /// Recieve a new set of data from the manager that can be displayed to viewers.
        /// </summary>
        public void _UpdateMainMenuView(
            bool newIsTeams,
            bool isTeam2Playing,
            int gameMode,
            bool isKorean4Ball,
            int timerMode,
            int player1ID,
            int player2ID,
            int player3ID,
            int player4ID,
            bool guideline
        )
        {
            if (newIsTeams)
            {
                if (VRC.SDKBase.Utilities.IsValid(teamsTxt)) teamsTxt.text = "Teams: YES";
                isTeams = true;
            }
            else
            {
                if (VRC.SDKBase.Utilities.IsValid(teamsTxt)) teamsTxt.text = "Teams: NO";
                isTeams = false;
            }
            UpdateButtonColors(teamsButtons, newIsTeams ? 0 : 1);

            switch (gameMode)
            {
                case 0:
                    if (VRC.SDKBase.Utilities.IsValid(gameModeTxt)) gameModeTxt.text = "American 8-Ball";
                    UpdateButtonColors(gameModeButtons, 0);

                    break;
                case 1:
                    if (VRC.SDKBase.Utilities.IsValid(gameModeTxt)) gameModeTxt.text = "American 9-Ball";
                    UpdateButtonColors(gameModeButtons, 1);

                    break;
                case 2:
                    if (isKorean4Ball)
                    {
                        if (VRC.SDKBase.Utilities.IsValid(gameModeTxt)) gameModeTxt.text = "Korean 4-Ball";
                        UpdateButtonColors(gameModeButtons, 3);
                    }
                    else
                    {
                        if (VRC.SDKBase.Utilities.IsValid(gameModeTxt)) gameModeTxt.text = "Japanese 4-Ball";
                        UpdateButtonColors(gameModeButtons, 2);
                    }

                    break;
            }

            switch (timerMode)
            {
                case 0:
                    if (VRC.SDKBase.Utilities.IsValid(timer)) timer.text = noTimerText;
                    break;
                case 1:
                    if (VRC.SDKBase.Utilities.IsValid(timer)) timer.text = timerValueText.Replace("{}", "10");
                    break;
                case 2:
                    if (VRC.SDKBase.Utilities.IsValid(timer)) timer.text = timerValueText.Replace("{}", "15");
                    break;
                case 3:
                    if (VRC.SDKBase.Utilities.IsValid(timer)) timer.text = timerValueText.Replace("{}", "30");
                    break;
                case 4:
                    if (VRC.SDKBase.Utilities.IsValid(timer)) timer.text = timerValueText.Replace("{}", "60");
                    break;
            }
            if (VRC.SDKBase.Utilities.IsValid(timerButton)) timerButton.color = timerMode != 0 ? selectedColor : unselectedColor;
            if (VRC.SDKBase.Utilities.IsValid(noTimerButton)) noTimerButton.color = timerMode == 0 ? selectedColor : unselectedColor;

            leaveButton.SetActive(false);

            if (useUnityUI)
            {
                player1UIButton.interactable = false;
                player2UIButton.interactable = false;
                player3UIButton.interactable = false;
                player4UIButton.interactable = false;
            }
            else
            {
                player1Button.SetActive(false);
                player2Button.SetActive(false);
                player3Button.SetActive(false);
                player4Button.SetActive(false);
            }

            bool found = false;

            var defaultText = manager.enableUdonChips
                ? defaultEmptyplayerSlotTextWithUdonChips.Replace("{}", (manager.price * manager.raiseCount).ToString())
                : defaultEmptyPlayerSlotText;

            if (player1ID > 0)
            {
                found = HandlePlayerState(player1MenuText, player1Scores, VRCPlayerApi.GetPlayerById(player1ID));
            }
            else
            {
                player1MenuText.text = defaultText.Replace("{}", "1");

                foreach (var score in player1Scores)
                {
                    score.text = "";    
                }
            }

            if (player2ID > 0)
            {
                found = HandlePlayerState(player2MenuText, player2Scores, VRCPlayerApi.GetPlayerById(player2ID));
            }
            else
            {
                player2MenuText.text = defaultText.Replace("{}", "2");
                
                foreach (var score in player2Scores)
                {
                    score.text = "";    
                }
            }

            if (player3ID > 0)
            {
                found = HandlePlayerState(player3MenuText, player3Scores, VRCPlayerApi.GetPlayerById(player3ID));
            }
            else
            {
                player3MenuText.text = newIsTeams ? defaultText.Replace("{}", "3") : "";
                
                foreach (var score in player3Scores)
                {
                    score.text = "";    
                }
            }

            if (player4ID > 0)
            {
                found = HandlePlayerState(player4MenuText, player4Scores, VRCPlayerApi.GetPlayerById(player4ID));
            }
            else
            {
                player4MenuText.text = newIsTeams ? defaultText.Replace("{}", "4") : "";
               
                foreach (var score in player4Scores)
                {
                    score.text = "";    
                }
            }

            int id = Networking.LocalPlayer.playerId;
            if (id == player1ID || id == player2ID || id == player3ID || id == player4ID)
            {
                isSignedUpToPlay = true;

                if (id == player1ID)
                {
                    canStartGame = true;
                    startGameButton.SetActive(true);
                }
                else
                {
                    canStartGame = false;
                    startGameButton.SetActive(false);
                }
            }
            else
            {
                isSignedUpToPlay = false;
                canStartGame = false;
                startGameButton.SetActive(false);
            }

            if (!found)
            {
                if (useUnityUI)
                {
                    player1UIButton.interactable = true;
                    player2UIButton.interactable = true;

                }
                else
                {
                    player1Button.SetActive(true);
                    player2Button.SetActive(true);
                }

                if (newIsTeams)
                {
                    if (useUnityUI)
                    {
                        player3UIButton.interactable = true;
                        player4UIButton.interactable = true;
                    }
                    else
                    {
                        player3Button.SetActive(true);
                        player4Button.SetActive(true);
                    }
                }
            }

            if (guideline)
            {
                if (toggleGuideLineButtonsActive && !useUnityUI)
                {
                    guideLineDisableButton.SetActive(true);
                    guideLineEnableButton.SetActive(false);
                }

                UpdateButtonColors(guideLineButtons, 0);
                if (VRC.SDKBase.Utilities.IsValid(guidelineStatus)) guidelineStatus.text = "Guideline On";
            }
            else
            {
                if (toggleGuideLineButtonsActive && !useUnityUI)
                {
                    guideLineDisableButton.SetActive(false);
                    guideLineEnableButton.SetActive(true);
                }

                UpdateButtonColors(guideLineButtons, 1);
                if (VRC.SDKBase.Utilities.IsValid(guidelineStatus)) guidelineStatus.text = "Guideline Off";
            }
        }

        private bool HandlePlayerState(TextMeshProUGUI menuText, TextMeshProUGUI[] scores, VRCPlayerApi player)
        {
            if (!VRC.SDKBase.Utilities.IsValid(player))
            {
                return false;
            }
            menuText.text = player.displayName;

            foreach (var score in scores)
            {
                score.text = player.displayName;    
            }

            if (player.playerId == Networking.LocalPlayer.playerId)
            {
                leaveButton.SetActive(true);

                if (useUnityUI)
                {
                    player1UIButton.interactable = false;
                    player2UIButton.interactable = false;
                    player3UIButton.interactable = false;
                    player4UIButton.interactable = false;
                }
                else
                {
                    player1Button.SetActive(manager.enableUdonChips && manager.allowRaising);
                    player2Button.SetActive(false);
                    player3Button.SetActive(false);
                    player4Button.SetActive(false);

                }

                return true;
            }

            return false;
        }

        public void _SetScore(bool isTeam2, int score)
        {
            if (score < 0)
            {
                foreach (var scoreText in teamAScores)
                {
                    scoreText.text = "";  
                }
                
                foreach (var scoreText in teamBScores)
                {
                    scoreText.text = "";  
                }

                return;
            }

            if (isTeam2)
            {
                foreach (var scoreText in teamBScores)
                {
                    scoreText.text = $"{score}"; 
                }
            }
            else
            {
                foreach (var scoreText in teamAScores)
                {
                    scoreText.text = $"{score}"; 
                }
            }
        }

        public void _GameWasReset()
        {
            winnerText.text = "The game was ended!";
        }

        public void _TeamWins(bool isTeam2)
        {
            if (isTeams)
            {
                if (isTeam2)
                {
                    winnerText.text = $"{player2Scores[0].text} and {player4Scores[0].text} win!";
                }
                else
                {
                    winnerText.text = $"{player1Scores[0].text} and {player3Scores[0].text} win!";
                }
            }
            else
            {
                if (isTeam2)
                {
                    winnerText.text = $"{player2Scores[0].text} wins!";
                }
                else
                {
                    winnerText.text = $"{player1Scores[0].text} wins!";
                }
            }
        }

        private void ResetScoreScreen()
        {
            foreach (var score in player1Scores)
            {
                score.text = "";
            }
            
            foreach (var score in player2Scores)
            {
                score.text = "";
            }
            
            foreach (var score in player3Scores)
            {
                score.text = "";
            }
            
            foreach (var score in player4Scores)
            {
                score.text = "";
            }
            
            foreach (var score in teamAScores)
            {
                score.text = "";
            }
            
            foreach (var score in teamBScores)
            {
                score.text = "";
            }

            winnerText.text = "";
            
            
        }
    }
}
