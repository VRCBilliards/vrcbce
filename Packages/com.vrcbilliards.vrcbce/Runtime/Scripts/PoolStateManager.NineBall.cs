using System;
using UnityEngine;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// Nineball code.
    /// </summary>
    public partial class PoolStateManager
    {
        private void Initialize9Ball()
        {
            //ballPocketedState = 0xFC00u;
            ballsArePocketed = new[]
            {
                false, false, false, false, false, false, false, false, false, false, true, true, true, true, true, true
            };

            for (int i = 0, k = 0; i < 5; i++)
            {
                int rown = breakRows9ball[i];
                for (int j = 0; j <= rown; j++)
                {
                    currentBallPositions[rackOrder9Ball[k++]] = new Vector3
                    (
                        SPOT_POSITION_X + (i * BALL_PL_Y) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F),
                        0.0f,
                        ((-rown + (j * 2)) * BALL_PL_X) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F)
                    );

                    currentBallVelocities[k] = Vector3.zero;
                    currentAngularVelocities[k] = Vector3.zero;
                }
            }

            UsColors();
        }
        
        private void HandleNineBallEndOfTurn(ref bool foulCondition, ref bool isCorrectBallSunk, out bool winCondition)
        {
            // TODO: Implement rail contact requirements
            foulCondition = foulCondition ||
                            GetLowestNumberedBall(oldBallsArePocketed) != firstHitBallThisTurn ||
                            firstHitBallThisTurn == 0;

            // Win condition: Pocket 9 ball ( at anytime )
            winCondition = ballsArePocketed[9];

            // this video is hard to follow so im just gonna guess this is right
            for (int i = 1; i < 10; i++)
            {
                if (ballsArePocketed[i] != oldBallsArePocketed[i])
                {
                    isCorrectBallSunk = true;
                }
            }
        }
        
        private void ApplyNineBallTableColour(bool isTeam2Color)
        {
            int target = GetLowestNumberedBall(ballsArePocketed);
            Color color = ballColors[target];
            cueRenderObjs[Convert.ToInt32(isTeam2Color)].materials[0].SetColor(uniformCueColour, color);
            cueRenderObjs[Convert.ToInt32(!isTeam2Color)].materials[0].SetColor(uniformCueColour, tableBlack);

            tableSrcColour = color;
        }
        
        private void InitializeNineBallVisuals()
        {
            pointerColour0 = tableLightBlue;
            pointerColour1 = tableLightBlue;
            pointerColour2 = tableLightBlue;

            pointerColourErr = tableBlack; // No error effect
            pointerClothColour = fabricBlue;

            // 9 ball only uses one colourset / cloth colour

            foreach (MeshRenderer meshRenderer in ballRenderers)
            {
                meshRenderer.material.SetTexture(MainTex, sets[3]);
            }

            if (marker9ball)
                marker9ball.SetActive(true);

            if (pocketBlockers)
                pocketBlockers.SetActive(false);

            // Reset mesh filters on balls that change them
            ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[0];
            ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = nineBall;

            for (int i = 0; i <= 9; i++)
            {
                if (ballTransforms[i])
                    ballTransforms[i].gameObject.SetActive(true);

                if (ballShadowPosConstraints[i])
                    ballShadowPosConstraints[i].gameObject.SetActive(true);
            }

            for (int i = 10; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                if (ballTransforms[i])
                    ballTransforms[i].gameObject.SetActive(false);

                if (ballShadowPosConstraints[i])
                    ballShadowPosConstraints[i].gameObject.SetActive(false);
            }
        }
        
        private void ReportNineBallScore()
        {
            if (logger)
            {
                logger._Log(name, "ReportNineBallScore");
            }

            poolMenu._SetScore(false, -1);
            poolMenu._SetScore(true, -1);

            if (slogger)
            {
                bool gameOver = false;

                if (isGameInMenus)
                {
                    if (ballsArePocketed[1])
                    {
                        gameOver = true;
                    }
                }

                slogger.OscReportScoresUpdated(gameOver, turnID, -1, isFoul, -1);
            }
        }
    }
}