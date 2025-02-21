using UnityEngine;
using VRC.SDKBase;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// Carom code.
    /// </summary>
    public partial class PoolStateManager { 
        private void InitializePocketedStateKoreanJapaneseCarom()
        {
            ballsArePocketed = new bool[NUMBER_OF_SIMULATED_BALLS];
            for (int i = 0; i < ballsArePocketed.Length; i++)
            {
                switch (i)
                {
                    case 0:
                    case 2:
                    case 3:
                    case 9:
                        ballsArePocketed[i] = false;

                        continue;
                    default:
                        ballsArePocketed[i] = true;

                        continue;
                }
            }
        }
        
        public void _ForceReset()
        {
            if (logger)
            {
                logger._Log(name, "ForceReset");
            }

            if (Networking.IsInstanceOwner)
            {
                Networking.SetOwner(localPlayer, gameObject);
                _Reset(ResetReason.InstanceOwnerReset);
            }
            else if (
                networkingLocalPlayerID == player1ID || networkingLocalPlayerID == player2ID ||
                networkingLocalPlayerID == player3ID || networkingLocalPlayerID == player4ID
            )
            {
                Networking.SetOwner(localPlayer, gameObject);
                _Reset(ResetReason.PlayerReset);
            }
            else if (
                (player1ID > 0 && !VRCPlayerApi.GetPlayerById(player1ID).IsValid()) ||
                (player2ID > 0 && !VRCPlayerApi.GetPlayerById(player2ID).IsValid()) ||
                (player3ID > 0 && !VRCPlayerApi.GetPlayerById(player3ID).IsValid()) ||
                (player4ID > 0 && !VRCPlayerApi.GetPlayerById(player4ID).IsValid())
            )
            {
                Networking.SetOwner(localPlayer, gameObject);
                _Reset(ResetReason.InvalidState);
            }
            else if (logger)
            {
                logger._Error(name, "Cannot reset table: you do not have permission");
            }
        }
        
        public void _UnlockTable()
        {
            if (logger)
            {
                logger._Log(name, "UnlockTable");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isTableLocked = false;
            RefreshNetworkData(false);
        }

        public void _LockTable()
        {
            if (logger)
            {
                logger._Log(name, "LockTable");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isTableLocked = true;
            RefreshNetworkData(false);
        }

        public void _JoinGame(int playerNumber)
        {
            if (logger)
            {
                logger._Log(name, $"JoinGame: {playerNumber}");
            }

            Networking.SetOwner(localPlayer, gameObject);
            localPlayerID = playerNumber;

            switch (playerNumber)
            {
                case 0:
                    player1ID = networkingLocalPlayerID;
                    EnableCustomBallColorSlider(true);
                    break;
                case 1:
                    player2ID = networkingLocalPlayerID;
                    EnableCustomBallColorSlider(true);
                    break;
                case 2:
                    player3ID = networkingLocalPlayerID;
                    EnableCustomBallColorSlider(true);
                    break;
                case 3:
                    player4ID = networkingLocalPlayerID;
                    EnableCustomBallColorSlider(true);
                    break;
                default:
                    return;
            }

            RefreshNetworkData(false);

            if (slogger)
            {
                slogger.OscReportJoinedTeam(playerNumber);
            }
        }

        public void _LeaveGame()
        {
            if (logger)
            {
                logger._Log(name, "LeaveGame");
            }

            Networking.SetOwner(localPlayer, gameObject);

            switch (localPlayerID)
            {
                case 0:
                    player1ID = 0;
                    break;
                case 1:
                    player2ID = 0;
                    break;
                case 2:
                    player3ID = 0;
                    break;
                case 3:
                    player4ID = 0;
                    break;
                default:
                    return;
            }

            localPlayerID = -1;

            RefreshNetworkData(false);

            //akalink added, makes the color panel not able to be interacted with
            EnableCustomBallColorSlider(false);
            //end
        }
        
        public void _IncreaseTimer()
        {
            if (logger)
            {
                logger._Log(name, "IncreaseTimer");
            }

            Networking.SetOwner(localPlayer, gameObject);
            timerSecondsPerShot += 5;
            RefreshNetworkData(false);

            if (timerSecondsPerShot >= 60)
            {
                timerSecondsPerShot = 60;
            }
        }

        public void _DecreaseTimer()
        {
            if (logger)
            {
                logger._Log(name, "DecreaseTimer");
            }

            Networking.SetOwner(localPlayer, gameObject);
            timerSecondsPerShot -= 5;

            if (timerSecondsPerShot <= 0)
            {
                timerSecondsPerShot = 0;
            }

            RefreshNetworkData(false);
        }

        public void _SelectTeams()
        {
            if (logger)
            {
                logger._Log(name, "SelectTeams");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isTeams = true;
            RefreshNetworkData(false);
        }

        public void _DeselectTeams()
        {
            if (logger)
            {
                logger._Log(name, "DeselectTeams");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isTeams = false;
            RefreshNetworkData(false);
        }

        public void _EnableGuideline()
        {
            if (logger)
            {
                logger._Log(name, "EnableGuideline");
            }

            Networking.SetOwner(localPlayer, gameObject);
            guideLineEnabled = true;
            RefreshNetworkData(false);
        }

        public void _DisableGuideline()
        {
            if (logger)
            {
                logger._Log(name, "DisableGuideline");
            }

            Networking.SetOwner(localPlayer, gameObject);
            guideLineEnabled = false;
            RefreshNetworkData(false);
        }


        /// <summary>
        /// Initialize new match as the host.
        /// </summary>
        public void _StartNewGame()
        {
            if (logger)
                logger._Log(name, "StartNewGame");

            mainSrc.enabled = true;

            if (!isGameInMenus)
                return;

            gameWasReset = false;
            gameID++;
            turnID = 0;
            _isGameBreak = true;
            isPlayerAllowedToPlay = true;

            isTeam2Turn = false;
            oldIsTeam2Turn = false;

            // Following is overrides of NewGameLocal, for game STARTER only
            turnIsRunning = false;
            isOpen = true;
            isGameInMenus = false;
            poolCues[0].tableIsActive = true;
            poolCues[1].tableIsActive = true;

            isTeam2Blue = false;
            isTeam2Winner = false;
            isFoul = false;
            isGameOver = false;

            ApplyTableColour(false);

            Networking.SetOwner(localPlayer, gameObject);

            RefreshNetworkData(false);
        }
        
        public void _Select8Ball()
        {
            if (logger)
                logger._Log(name, "Select8Ball");

            Networking.SetOwner(localPlayer, gameObject);

            gameMode = 0u;
            RefreshNetworkData(false);
        }

        public void _Select9Ball()
        {
            if (logger)
                logger._Log(name, "Select9Ball");

            Networking.SetOwner(localPlayer, gameObject);

            gameMode = GameMode.NineBall;

            RefreshNetworkData(false);
        }

        public void _Select4BallJapanese()
        {
            if (logger)
                logger._Log(name, "Select4BallJapanese");

            Networking.SetOwner(localPlayer, gameObject);

            gameMode = GameMode.JapaneseCarom;

            RefreshNetworkData(false);
        }

        public void _SelectThreeCushionCarom()
        {
            if (logger)
                logger._Log(name, "SelectThreeCushionCarom");

            Networking.SetOwner(localPlayer, gameObject);

            gameMode = GameMode.ThreeCushionCarom;

            RefreshNetworkData(false);
        }


        public void _Select4BallKorean()
        {
            if (logger)
                logger._Log(name, "Select4BallKorean");

            Networking.SetOwner(localPlayer, gameObject);

            gameMode = GameMode.KoreanCarom;

            RefreshNetworkData(false);
        }
    }
}

