using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace VRCBilliards
{
    public class PoolMenu : UdonSharpBehaviour
    {
        public PoolStateManager manager;

        public GameObject resetGameButton;
        public GameObject lockMenu;
        public GameObject mainMenu;
        public GameObject player1Button;
        public GameObject player2Button;
        public GameObject player3Button;
        public GameObject player4Button;
        public GameObject leaveButton;

        public TextMeshProUGUI teamsTxt;
        public TextMeshProUGUI gameModeTxt;
        public TextMeshProUGUI timer;

        public TextMeshProUGUI player1MenuText;
        public TextMeshProUGUI player2MenuText;
        public TextMeshProUGUI player3MenuText;
        public TextMeshProUGUI player4MenuText;

        public TextMeshProUGUI player1ScoreText;
        public TextMeshProUGUI player2ScoreText;
        public TextMeshProUGUI player3ScoreText;
        public TextMeshProUGUI player4ScoreText;

        public TextMeshProUGUI team1ScoreText;
        public TextMeshProUGUI team2ScoreText;

        public TextMeshProUGUI winnerText;

        private int currentGameMode;
        private bool isTeams;

        // TODO: This all needs to be secured.
        public void UnlockTable()
        {
            manager.UnlockTable();
        }

        public void LockTable()
        {
            manager.LockTable();
        }

        public void SelectTeams()
        {
            manager.SelectTeams();
        }

        public void DeselectTeams()
        {
            manager.DeselectTeams();
        }

        public void Select4BallJapanese()
        {
            manager.Select4BallJapanese();
        }

        public void Select4BallKorean()
        {
            manager.Select4BallKorean();
        }

        public void Select8Ball()
        {
            manager.Select8Ball();
        }

        public void Select9Ball()
        {
            manager.Select9Ball();
        }

        public void IncreaseTimer()
        {
            manager.IncreaseTimer();
        }

        public void DecreaseTimer()
        {
            manager.DecreaseTimer();
        }

        public void SignUpAsPlayer1()
        {
            manager.JoinGame(0);
        }

        public void SignUpAsPlayer2()
        {
            manager.JoinGame(1);
        }

        public void SignUpAsPlayer3()
        {
            manager.JoinGame(2);
        }

        public void SignUpAsPlayer4()
        {
            manager.JoinGame(3);
        }

        public void LeaveGame()
        {
            manager.LeaveGame();
        }

        public void StartGame()
        {
            manager.StartNewGame();
        }

        public void EndGame()
        {
            manager.ForceReset();
        }

        public void EnableResetButton()
        {
            resetGameButton.SetActive(true);
            lockMenu.SetActive(false);
            mainMenu.SetActive(false);

            winnerText.text = "";
        }

        public void EnableUnlockTableButton()
        {
            resetGameButton.SetActive(false);
            lockMenu.SetActive(true);
            mainMenu.SetActive(false);

            ResetScoreScreen();
        }

        public void EnableMainMenu()
        {
            resetGameButton.SetActive(false);
            lockMenu.SetActive(false);
            mainMenu.SetActive(true);
        }

        /// <summary>
        /// Recieve a new set of data from the manager that can be displayed to viewers.
        /// </summary>
        public void UpdateMainMenuView(
            bool newIsTeams,
            bool isTeam2Playing,
            int gameMode,
            bool isKorean4Ball,
            int timerMode,
            int player1ID,
            int player2ID,
            int player3ID,
            int player4ID
        )
        {
            Debug.Log($"Got a new menu update: teams {newIsTeams} team 2's turn {isTeam2Playing} game mode {gameMode} timer mode {timerMode} player 1 {player1ID} player 2 {player2ID} player 3 {player3ID} player 4 {player4ID}");

            if (newIsTeams)
            {
                teamsTxt.text = "Teams: YES";
                isTeams = true;
            }
            else
            {
                teamsTxt.text = "Teams: NO";
                isTeams = false;
            }

            currentGameMode = gameMode;

            switch (gameMode)
            {
                case 0:
                    gameModeTxt.text = "American 8-Ball";

                    break;
                case 1:
                    gameModeTxt.text = "American 9-Ball";

                    break;
                case 2:
                    if (isKorean4Ball)
                    {
                        gameModeTxt.text = "Korean 4-Ball";
                    }
                    else
                    {
                        gameModeTxt.text = "Japanese 4-Ball";
                    }

                    break;
            }

            switch (timerMode)
            {
                case 0:
                    timer.text = "No Limit";
                    break;
                case 1:
                    timer.text = "10s Limit";
                    break;
                case 2:
                    timer.text = "15s Limit";
                    break;
                case 3:
                    timer.text = "30s Limit";
                    break;
                case 4:
                    timer.text = "60s Limit";
                    break;
            }

            leaveButton.SetActive(false);
            player1Button.SetActive(false);
            player2Button.SetActive(false);
            player3Button.SetActive(false);
            player4Button.SetActive(false);

            bool found = false;

            if (player1ID > 0)
            {
                found = HandlePlayerState(player1MenuText, player1ScoreText, VRCPlayerApi.GetPlayerById(player1ID));
            }
            else
            {
                player1MenuText.text = "";
                player1ScoreText.text = "";
            }

            if (player2ID > 0)
            {
                found = HandlePlayerState(player2MenuText, player2ScoreText, VRCPlayerApi.GetPlayerById(player2ID));
            }
            else
            {
                player2MenuText.text = "";
                player2ScoreText.text = "";
            }

            if (player3ID > 0)
            {
                found = HandlePlayerState(player3MenuText, player3ScoreText, VRCPlayerApi.GetPlayerById(player3ID));
            }
            else
            {
                player3MenuText.text = "";
                player3ScoreText.text = "";
            }

            if (player4ID > 0)
            {
                found = HandlePlayerState(player4MenuText, player4ScoreText, VRCPlayerApi.GetPlayerById(player4ID));
            }
            else
            {
                player4MenuText.text = "";
                player4ScoreText.text = "";
            }

            if (!found)
            {
                player1Button.SetActive(true);
                player2Button.SetActive(true);

                if (newIsTeams)
                {
                    player3Button.SetActive(true);
                    player4Button.SetActive(true);
                }
            }
        }

        private bool HandlePlayerState(TextMeshProUGUI menuText, TextMeshProUGUI scoreText, VRCPlayerApi player)
        {
            menuText.text = player.displayName;
            scoreText.text = player.displayName;

            if (player.playerId == Networking.LocalPlayer.playerId)
            {
                leaveButton.SetActive(true);

                player1Button.SetActive(false);
                player2Button.SetActive(false);
                player3Button.SetActive(false);
                player4Button.SetActive(false);

                return true;
            }

            return false;
        }

        public void SetScore(bool isTeam2, int score)
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

        public void TeamWins(bool isTeam2)
        {
            if (isTeams)
            {
                if (isTeam2)
                {
                    winnerText.text = $"{player2ScoreText.text} and ${player4ScoreText.text} win!";
                }
                else
                {
                    winnerText.text = $"{player1ScoreText.text} and ${player3ScoreText.text} win!";
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
