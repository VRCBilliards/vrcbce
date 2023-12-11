using UdonSharp;
using UnityEngine;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts.Components
{
    /// <summary>
    /// A class that dumps VRCBCE things that happen to a structured log stream.
    /// </summary>
    public class OscSlogger : UdonSharpBehaviour
    {
        public string oscPrefix = "[VRCBCE OSC]event={0}";
        public string oscJoinedTeam = "joinedTeam,playerID={0}";
        public string oscGameStartedPlayer = "gameStarted,isPlayer=true";
        public string oscGameStartedSpectator = "gameStarted,isPlayer=false";
        public string oscEndOfTurn = "endOfTurn,turnID={0],gameOver={1},teamAScore={2},fouled={3},teamBScore={4}";
        public string oscGameReset = "gameReset,reason={0}";

        private void OscBuildOutput(string input)
        {
            Debug.Log(string.Format(oscPrefix, input));
        }

        public void OscReportJoinedTeam(int playerID)
        {
             OscBuildOutput(string.Format(oscJoinedTeam, playerID));
        }

        public void OscReportGameStarted(bool isPlaying)
        {
             OscBuildOutput(isPlaying ? oscGameStartedPlayer : oscGameStartedSpectator);
        }

        public void OscReportEndOfTurn(bool isGameOver, uint turnID, int teamAScore, bool fouled, int teamBScore)
        {
             OscBuildOutput(string.Format(oscEndOfTurn, turnID, isGameOver, teamAScore, fouled, teamBScore));
        }

        public void OscReportGameReset(ResetReason reason)
        {
            OscBuildOutput(string.Format(oscGameReset, BasePoolStateManager.ToReasonString(reason)));
        }
    }
}