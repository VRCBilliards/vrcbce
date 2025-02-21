using System;
using UnityEngine;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// Eightball code.
    /// </summary>
    public partial class PoolStateManager
    {
        private void Initialize8Ball()
        {
            ballsArePocketed = new[]
            {
                false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false
            };

            for (int i = 0, k = 0; i < 5; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    currentBallPositions[rackOrder8Ball[k++]] = new Vector3
                    (
                        SPOT_POSITION_X + (i * BALL_PL_Y) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F),
                        0.0f,
                        ((-i + (j * 2)) * BALL_PL_X) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F)
                    );

                    currentBallVelocities[k] = Vector3.zero;
                    currentAngularVelocities[k] = Vector3.zero;
                }
            }

            TeamColors();
        }
        
        private void HandleEightBallEndOfTurn(
            bool is8Sink,
            out int numberOfSunkBlues,
            out int numberOfSunkOranges,
            ref bool isCorrectBallSunk,
            ref bool isOpponentColourSunk, 
            ref bool foulCondition,
            out bool deferLossCondition,
            out bool winCondition
        )
        {
            var isBlue = !isOpen && (isTeam2Turn && isTeam2Blue || !isTeam2Turn && !isTeam2Blue);

            numberOfSunkBlues = GetNumberOfSunkBlues();
            numberOfSunkOranges = GetNumberOfSunkOranges();
            var isWrongHit = IsFirstEightBallHitFoul(firstHitBallThisTurn, numberOfSunkBlues, numberOfSunkOranges);

            // What balls got sunk this turn?
            for (var i = 2; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                if (ballsArePocketed[i] == oldBallsArePocketed[i])
                    continue;

                if (isOpen)
                    isCorrectBallSunk = true;
                else if (isBlue)
                {
                    if (i > 1 && i < 9)
                        isCorrectBallSunk = true;
                    else
                        isOpponentColourSunk = true;
                }
                else
                {
                    if (i >= 9)
                        isCorrectBallSunk = true;
                    else
                        isOpponentColourSunk = true;
                }
            }

            winCondition = isBlue ? numberOfSunkBlues == 7 && is8Sink : numberOfSunkOranges == 7 && is8Sink;

            if (isWrongHit)
            {
                if (logger)
                    logger._Log(name, "foul, wrong ball hit");

                foulCondition = true;
            }
            else if (firstHitBallThisTurn == 0)
            {
                if (logger)
                    logger._Log(name, "foul, no ball hit");

                foulCondition = true;
            }

            deferLossCondition = is8Sink;
        }
        
        private int GetNumberOfSunkOranges()
        {
            var num = 0;

            for (var i = 9; i < 16; i++)
            {
                if (ballsArePocketed[i])
                {
                    num++;
                }
            }

            return num;
        }

        private int GetNumberOfSunkBlues()
        {
            var num = 0;

            for (var i = 2; i < 9; i++)
            {
                if (ballsArePocketed[i])
                {
                    num++;
                }
            }

            return num;
        }

        private void ApplyEightBallTableColour(bool isTeam2Color)
        {
            if (!isOpen)
            {
                if (isTeam2Color)
                {
                    if (isTeam2Blue)
                    {
                        tableSrcColour = tableBlue;
                        cueRenderObjs[0].material.SetColor(uniformCueColour, tableOrange * 0.33f);
                        cueRenderObjs[1].material.SetColor(uniformCueColour, tableBlue);
                    }
                    else
                    {
                        tableSrcColour = tableOrange;
                        cueRenderObjs[0].material.SetColor(uniformCueColour, tableBlue * 0.33f);
                        cueRenderObjs[1].material.SetColor(uniformCueColour, tableOrange);
                    }
                }
                else
                {
                    if (isTeam2Blue)
                    {
                        tableSrcColour = tableOrange;
                        cueRenderObjs[0].material.SetColor(uniformCueColour, tableOrange);
                        cueRenderObjs[1].material.SetColor(uniformCueColour, tableBlue * 0.33f);
                    }
                    else
                    {
                        tableSrcColour = tableBlue;
                        cueRenderObjs[0].material.SetColor(uniformCueColour, tableBlue);
                        cueRenderObjs[1].material.SetColor(uniformCueColour, tableOrange * 0.33f);
                    }
                }
            }
            else
            {
                tableSrcColour = pointerColour2;

                cueRenderObjs[Convert.ToInt32(this.isTeam2Turn)].materials[0].SetColor(uniformCueColour, tableWhite);
                cueRenderObjs[Convert.ToInt32(!this.isTeam2Turn)].materials[0].SetColor(uniformCueColour, tableBlack);
            }
        }
        
        private void InitializeEightBallVisuals()
        {
            pointerColourErr = tableRed;
            pointerColour2 = tableWhite;

            pointerColour0 = tableBlue;
            pointerColour1 = tableOrange;

            foreach (MeshRenderer meshRenderer in ballRenderers)
            {
                meshRenderer.material.SetTexture(MainTex, sets[0]);
            }

            pointerClothColour = fabricGray;

            if (marker9ball)
            {
                marker9ball.SetActive(false);
            }

            if (pocketBlockers)
            {
                pocketBlockers.SetActive(false);
            }

            // Reset mesh filters on balls that change them
            ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[0];
            ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = nineBall;

            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                if (ballTransforms[i])
                {
                    ballTransforms[i].gameObject.SetActive(true);
                }

                if (ballShadowPosConstraints[i])
                {
                    ballShadowPosConstraints[i].gameObject.SetActive(true);
                }
            }
        }
        
        private void ReportEightBallScore()
        {
            if (logger)
            {
                logger._Log(name, "ReportEightBallScore");
            }

            int sunkBlues = 0;
            int sunkOranges = 0;

            for (int i = 2; i < 9; i++)
            {
                if (ballsArePocketed[i])
                {
                    sunkBlues++;
                }
            }

            for (int i = 9; i < 16; i++)
            {
                if (ballsArePocketed[i])
                {
                    sunkOranges++;
                }
            }

            if (isGameInMenus)
            {
                if (isTeam2Winner && ballsArePocketed[1])
                {
                    if (isTeam2Blue)
                    {
                        sunkBlues++;
                    }
                    else
                    {
                        sunkOranges++;
                    }
                }
            }

            var teamAScore = isTeam2Blue ? sunkOranges : sunkBlues;
            var teamBScore = isTeam2Blue ? sunkBlues : sunkOranges;

            poolMenu._SetScore(false, teamAScore);
            poolMenu._SetScore(true, teamBScore);

            if (slogger)
            {
                slogger.OscReportScoresUpdated(isGameOver, turnID, teamAScore, isFoul, teamBScore);
            }
        }
        
        private bool IsFirstEightBallHitFoul(int hitBallID, int numberOfSunkBlues, int numberOfSunkOranges)
        {
            if (isOpen)
                return hitBallID == 1;
            
            var isBlue = isTeam2Turn && isTeam2Blue || !isTeam2Turn && !isTeam2Blue;
            var winCondition = isBlue ? numberOfSunkBlues == 7 && ballsArePocketed[1] : numberOfSunkOranges == 7 && ballsArePocketed[1];

            // Even if you sunk black, if you hit the wrong ball first you still lose.
            if (winCondition)
                return hitBallID != 1;
            
            // The seven blue balls are stored at idx 2-8.
            if (isBlue)
                return hitBallID < 2 || hitBallID > 8;

            // The seven orange balls are stored at idx 9-15.
            return hitBallID < 9;
        }
    }
}