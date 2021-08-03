using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace VRCBilliards
{
    public class PoolMenu : UdonSharpBehaviour
    {
        [Header("Pool State Manager")]
        public PoolStateManager manager;

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
        public TextMeshProUGUI player1ScoreText;
        public TextMeshProUGUI player2ScoreText;
        public TextMeshProUGUI player3ScoreText;
        public TextMeshProUGUI player4ScoreText;

        public TextMeshProUGUI team1ScoreText;
        public TextMeshProUGUI team2ScoreText;

        public TextMeshProUGUI winnerText;

        [Header("UdonChips Integration")]
        public string defaultEmptyplayerSlotTextWithUdonChips = "{}uc to play";

        private bool isTeams;
        private bool isSignedUpToPlay;
        private bool canStartGame;

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
                if (Utilities.IsValid(teamsTxt)) teamsTxt.text = "Teams: YES";
                isTeams = true;
            }
            else
            {
                if (Utilities.IsValid(teamsTxt)) teamsTxt.text = "Teams: NO";
                isTeams = false;
            }
            UpdateButtonColors(teamsButtons, newIsTeams ? 0 : 1);

            switch (gameMode)
            {
                case 0:
                    if (Utilities.IsValid(gameModeTxt)) gameModeTxt.text = "American 8-Ball";
                    UpdateButtonColors(gameModeButtons, 0);

                    break;
                case 1:
                    if (Utilities.IsValid(gameModeTxt)) gameModeTxt.text = "American 9-Ball";
                    UpdateButtonColors(gameModeButtons, 1);

                    break;
                case 2:
                    if (isKorean4Ball)
                    {
                        if (Utilities.IsValid(gameModeTxt)) gameModeTxt.text = "Korean 4-Ball";
                        UpdateButtonColors(gameModeButtons, 3);
                    }
                    else
                    {
                        if (Utilities.IsValid(gameModeTxt)) gameModeTxt.text = "Japanese 4-Ball";
                        UpdateButtonColors(gameModeButtons, 2);
                    }

                    break;
            }

            switch (timerMode)
            {
                case 0:
                    if (Utilities.IsValid(timer)) timer.text = noTimerText;
                    break;
                case 1:
                    if (Utilities.IsValid(timer)) timer.text = timerValueText.Replace("{}", "10");
                    break;
                case 2:
                    if (Utilities.IsValid(timer)) timer.text = timerValueText.Replace("{}", "15");
                    break;
                case 3:
                    if (Utilities.IsValid(timer)) timer.text = timerValueText.Replace("{}", "30");
                    break;
                case 4:
                    if (Utilities.IsValid(timer)) timer.text = timerValueText.Replace("{}", "60");
                    break;
            }
            if (Utilities.IsValid(timerButton)) timerButton.color = timerMode != 0 ? selectedColor : unselectedColor;
            if (Utilities.IsValid(noTimerButton)) noTimerButton.color = timerMode == 0 ? selectedColor : unselectedColor;

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
                found = HandlePlayerState(player1MenuText, player1ScoreText, VRCPlayerApi.GetPlayerById(player1ID));
            }
            else
            {
                player1MenuText.text = defaultText.Replace("{}", "1");
                player1ScoreText.text = "";
            }

            if (player2ID > 0)
            {
                found = HandlePlayerState(player2MenuText, player2ScoreText, VRCPlayerApi.GetPlayerById(player2ID));
            }
            else
            {
                player2MenuText.text = defaultText.Replace("{}", "2");
                player2ScoreText.text = "";
            }

            if (player3ID > 0)
            {
                found = HandlePlayerState(player3MenuText, player3ScoreText, VRCPlayerApi.GetPlayerById(player3ID));
            }
            else
            {
                player3MenuText.text = newIsTeams ? defaultText.Replace("{}", "3") : "";
                player3ScoreText.text = "";
            }

            if (player4ID > 0)
            {
                found = HandlePlayerState(player4MenuText, player4ScoreText, VRCPlayerApi.GetPlayerById(player4ID));
            }
            else
            {
                player4MenuText.text = newIsTeams ? defaultText.Replace("{}", "4") : "";
                player4ScoreText.text = "";
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
                if (Utilities.IsValid(guidelineStatus)) guidelineStatus.text = "Guideline On";
            }
            else
            {
                if (toggleGuideLineButtonsActive && !useUnityUI)
                {
                    guideLineDisableButton.SetActive(false);
                    guideLineEnableButton.SetActive(true);
                }

                UpdateButtonColors(guideLineButtons, 1);
                if (Utilities.IsValid(guidelineStatus)) guidelineStatus.text = "Guideline Off";
            }
        }

        private bool HandlePlayerState(TextMeshProUGUI menuText, TextMeshProUGUI scoreText, VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player))
            {
                return false;
            }
            menuText.text = player.displayName;
            scoreText.text = player.displayName;

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
                team1ScoreText.text = "";
                team2ScoreText.text = "";

                return;
            }

            if (isTeam2)
            {
                team2ScoreText.text = $"{score}";
            }
            else
            {
                team1ScoreText.text = $"{score}";
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
                    winnerText.text = $"{player2ScoreText.text} and {player4ScoreText.text} win!";
                }
                else
                {
                    winnerText.text = $"{player1ScoreText.text} and {player3ScoreText.text} win!";
                }
            }
            else
            {
                if (isTeam2)
                {
                    winnerText.text = $"{player2ScoreText.text} wins!";
                }
                else
                {
                    winnerText.text = $"{player1ScoreText.text} wins!";
                }
            }
        }

        private void ResetScoreScreen()
        {
            player1ScoreText.text = "";
            player2ScoreText.text = "";
            player3ScoreText.text = "";
            player4ScoreText.text = "";

            team1ScoreText.text = "";
            team2ScoreText.text = "";

            winnerText.text = "";
        }
    }
}
