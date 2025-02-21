using System;
using UnityEngine;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// Covers Three Cushion Carom setup, scoring, fouls, and so on.
    /// </summary>
    public partial class PoolStateManager { 
        private void InitializeThreeCushionCarom()
        {
            if (logger)
                logger._Log(name, nameof(InitializeThreeCushionCarom));
            
            InitializePocketedStateThreeCushionCarom();

            // This works for some reason, it's not clear why.
            // White should be at an offset to head.
            currentBallPositions[0] = new Vector3(SPOT_POSITION_X, 0.0f, 0.1825f);
            // Red should be at tail.
            currentBallPositions[9] = new Vector3(-SPOT_POSITION_X, 0f, 0f);
            // Yellow should be at head.
            currentBallPositions[2] = new Vector3(SPOT_POSITION_X, 0.0f, 0.0f);
            
            // For some reason yellow and red are swapped.
            
            currentBallVelocities[0] = Vector3.zero;
            currentBallVelocities[9] = Vector3.zero;
            currentBallVelocities[2] = Vector3.zero;

            currentAngularVelocities[0] = Vector3.zero;
            currentAngularVelocities[9] = Vector3.zero;
            currentAngularVelocities[2] = Vector3.zero;

            SetFourBallColours();
        }

        private void HandleThreeCushionCaromScoring(int otherBallID)
        {
            if (logger)
                logger._Log(name, nameof(HandleThreeCushionCaromScoring));
            
            // You can't score more than one point a turn.
            if (secondBallHitThisTurn != 0) 
                return;

            // You don't score if you've only hit one ball.
            if (firstHitBallThisTurn == 0)
            {
                firstHitBallThisTurn = otherBallID;
                
                return;
            }

            // You don't score if you hit the same object ball twice.
            if (otherBallID == firstHitBallThisTurn)
                return;
            
            secondBallHitThisTurn = otherBallID;

            // You don't score if you don't hit three cushions before hitting the second object ball.
            if (cushionsHitThisTurn < 3)
                return;

            // You don't score on break if you don't hit the object ball first. I.e. you must play down the table.
            if (_isGameBreak && firstHitBallThisTurn != 2)
                return;

            OnLocalCaromPoint(ballTransforms[otherBallID]);
        }

        private void InitializePocketedStateThreeCushionCarom()
        {
            if (logger)
                logger._Log(name, nameof(InitializePocketedStateThreeCushionCarom));
            
            ballsArePocketed = new bool[NUMBER_OF_SIMULATED_BALLS];
            for (int i = 0; i < ballsArePocketed.Length; i++)
            {
                switch (i)
                {
                    case 0:
                    case 2:
                    case 9:
                        ballsArePocketed[i] = false;

                        continue;
                    default:
                        ballsArePocketed[i] = true;

                        continue;
                }
            }
        }
        
        private void InitializeThreeCushionCaromVisuals()
        {
            if (logger)
                logger._Log(name, nameof(InitializeThreeCushionCaromVisuals));
            
            pointerColour0 = tableWhite;
            pointerColour1 = tableYellow;

            // Should not be used
            pointerColour2 = tableRed;
            pointerColourErr = tableRed;

            foreach (MeshRenderer meshRenderer in ballRenderers)
            {
                meshRenderer.material.SetTexture(MainTex, sets[2]);
            }

            pointerClothColour = fabricGreen;

            if (pocketBlockers)
                pocketBlockers.SetActive(true);

            scores[0] = 0;
            scores[1] = 0;

            // Reset mesh filters on balls that change them
            ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[0];
            ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[1];

            for (int i = 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                switch(i) {
                    case 0:
                    case 2:
                    case 9:
                        if (ballTransforms[i])
                            ballTransforms[i].gameObject.SetActive(true);

                        if (ballShadowPosConstraints[i])
                            ballShadowPosConstraints[i].gameObject.SetActive(true);

                        break;
                    default:
                        if (ballTransforms[i])
                            ballTransforms[i].gameObject.SetActive(false);

                        if (ballShadowPosConstraints[i])
                            ballShadowPosConstraints[i].gameObject.SetActive(false);

                        break;
                }
            }
        }

        private void HandleThreeCushionCaromEndOfTurn()
        {
            if (logger)
                logger._Log(name, nameof(HandleThreeCushionCaromEndOfTurn));
            
            // In 3CC you can return to the table, but we don't currently support that so this is a bit simplified.
            if (!fourBallCueLeftTable) 
                return;
            
            currentBallVelocities[0] = Vector3.zero;
            currentAngularVelocities[0] = Vector3.zero;
            
            // Try and go to the head spot first. If it's blocked, it goes to the blocking ball's spot.
            currentBallPositions[0] = new Vector3(-SPOT_POSITION_X, 0.0f, 0.0f);
            
            if (IsThreeCushionCueBlockedByOtherCueBall())
            {
                // Blocked by other cue; put it in the centre.
                currentBallPositions[0] = Vector3.zero;
                
                if (IsThreeCushionCueBlockedByObjectBall())
                {
                    // Blocked by object ball, put it at the foot.
                    currentBallPositions[0] = new Vector3(SPOT_POSITION_X, 0.0f, 0.0f);
                }
            } else if (IsThreeCushionCueBlockedByObjectBall())
            {
                // Blocked by object ball, put it at the foot.
                currentBallPositions[0] = new Vector3(SPOT_POSITION_X, 0.0f, 0.0f);
                
                if (IsThreeCushionCueBlockedByOtherCueBall())
                {
                    // Blocked by other cue; put it in the centre.
                    currentBallPositions[0] = Vector3.zero;
                }
            }
                
            // There's only three balls, so only two spots can be blocked at a time, so this is sufficient.
            // Although this is a foul it's not one in the pool sense, so no marker.

            fourBallCueLeftTable = false;
        }

        private bool IsThreeCushionCueBlockedByObjectBall()
        {
            return (currentBallPositions[0] - currentBallPositions[2]).sqrMagnitude < BALL_DIAMETER_SQUARED;
        }
        
        private bool IsThreeCushionCueBlockedByOtherCueBall()
        {
            return (currentBallPositions[0] - currentBallPositions[9]).sqrMagnitude < BALL_DIAMETER_SQUARED;
        }
    }
}

