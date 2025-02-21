using System;
using UnityEngine;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// Carom code.
    /// </summary>
    public partial class PoolStateManager {
        private void HandleJapanese4BallScoring(int otherBallID)
        {
            if (logger)
                logger._Log(name, nameof(HandleJapanese4BallScoring));
            
            if (firstHitBallThisTurn == 0)
                firstHitBallThisTurn = otherBallID;
            else if (secondBallHitThisTurn == 0)
            {
                if (otherBallID == firstHitBallThisTurn)
                    return;

                secondBallHitThisTurn = otherBallID;

                OnLocalCaromPoint(ballTransforms[otherBallID]);
            }
            else if (thirdBallHitThisTurn == 0)
            {
                if (otherBallID == firstHitBallThisTurn || otherBallID == secondBallHitThisTurn)
                {
                    return;
                }

                thirdBallHitThisTurn = otherBallID;

                OnLocalCaromPoint(ballTransforms[otherBallID]);
            }
        }

        private void HandleKorean4BallScoring(int otherBallID)
        {
            if (logger)
                logger._Log(name, nameof(HandleKorean4BallScoring));
            
            if (otherBallID == 9)
            {
                if (isMadeFoul)
                    return;

                isMadeFoul = true;
                scores[Convert.ToUInt32(isTeam2Turn)]--;

                if (scores[Convert.ToUInt32(isTeam2Turn)] < 0)
                    scores[Convert.ToUInt32(isTeam2Turn)] = 0;

                SpawnMinusOne(ballTransforms[otherBallID]);
            }
            else if (firstHitBallThisTurn == 0)
                firstHitBallThisTurn = otherBallID;
            else if (otherBallID != firstHitBallThisTurn)
            {
                if (secondBallHitThisTurn != 0) 
                    return;

                secondBallHitThisTurn = otherBallID;
                OnLocalCaromPoint(ballTransforms[otherBallID]);
            }
        }
        
        private void ReportFourBallScore()
        {
            if (logger)
                logger._Log(name, nameof(ReportFourBallScore));

            poolMenu._SetScore(false, scores[0]);
            poolMenu._SetScore(true, scores[1]);

            if (slogger)
                slogger.OscReportScoresUpdated(scores[0] >= 10 || scores[1] >= 10, turnID, scores[0], isFoul, scores[1]);
        }
        
        private void OnLocalCaromPoint(Transform ball)
        {
            if (logger)
                logger._Log(name, nameof(OnLocalCaromPoint));
            
            isMadePoint = true;
            mainSrc.PlayOneShot(pointMadeSfx, 1.0f);

            scores[Convert.ToUInt32(isTeam2Turn)]++;

            if (scores[Convert.ToUInt32(isTeam2Turn)] > 10)
                scores[Convert.ToUInt32(isTeam2Turn)] = 10;

            SpawnPlusOne(ball);
        }
        
        private void SpawnPlusOne(Transform ball)
        {
            if (logger)
                logger._Log(name, nameof(SpawnPlusOne));

            var emitParams = new ParticleSystem.EmitParams();
            emitParams.position = ball.position;
            plusOneParticleSystem.Emit(emitParams, 1);
        }

        private void SpawnMinusOne(Transform ball)
        {
            if (logger)
                logger._Log(name, nameof(SpawnMinusOne));

            var emitParams = new ParticleSystem.EmitParams();
            emitParams.position = ball.position;
            minusOneParticleSystem.Emit(emitParams, 1);
        }
        
        private void Initialize4Ball()
        {
            if (logger)
                logger._Log(name, nameof(Initialize4Ball));
            
            ballsArePocketed = new[] {false, true, false, false, true, true, true, true, true, false, true, true, true, true, true, true};

            currentBallPositions[0] = new Vector3(-SPOT_CAROM_X, 0.0f, 0.0f);
            currentBallPositions[9] = new Vector3(SPOT_CAROM_X, 0.0f, 0.0f);
            currentBallPositions[2] = new Vector3(SPOT_POSITION_X, 0.0f, 0.0f);
            currentBallPositions[3] = new Vector3(-SPOT_POSITION_X, 0.0f, 0.0f);

            currentBallVelocities[0] = Vector3.zero;
            currentBallVelocities[9] = Vector3.zero;
            currentBallVelocities[2] = Vector3.zero;
            currentBallVelocities[3] = Vector3.zero;

            currentAngularVelocities[0] = Vector3.zero;
            currentAngularVelocities[9] = Vector3.zero;
            currentAngularVelocities[2] = Vector3.zero;
            currentAngularVelocities[3] = Vector3.zero;

            SetFourBallColours();
        }
        
        private Vector3 GetFourBallCueStartingPosition(bool team)
        {
            if (logger)
                logger._Log(name, nameof(GetFourBallCueStartingPosition));
            
            return team ? new Vector3(SPOT_CAROM_X, 0.0f, 0.0f) : new Vector3(-SPOT_CAROM_X, 0.0f, 0.0f);
        }
        
        private void CalculateCaromBallPhysics(int id)
        {
            // Only uncomment the below if you're debugging, as this is called repeatedly each frame whilst simulating.
            // if (logger)
            //     logger._Log(name, nameof(CalculateCaromBallPhysics));
            
            var a = currentBallPositions[id];

            // Setup major regions
            var zx = Mathf.Sign(a.x);
            var zz = Mathf.Sign(a.z);

            if (a.x * zx > pR.x)
            {
                currentBallPositions[id].x = pR.x * zx;
                ApplyCushionBounce(id, Vector3.left * zx);
            }

            if (a.z * zz > pO.z)
            {
                currentBallPositions[id].z = pO.z * zz;
                ApplyCushionBounce(id, Vector3.back * zz);
            }
        }
        
        private void HandleCaromEndOfTurn()
        {
            if (logger)
                logger._Log(name, nameof(HandleCaromEndOfTurn));
            
            if (fourBallCueLeftTable)
            {
                currentBallPositions[0] = GetFourBallCueStartingPosition(isTeam2Turn);
                currentBallVelocities[0] = Vector3.zero;
                currentAngularVelocities[0] = Vector3.zero;

                // Best effort attempt to place the ball somewhere
                if (IsCueContacting())
                {
                    currentBallPositions[0] = GetFourBallCueStartingPosition(!isTeam2Turn);

                    if (IsCueContacting())
                    {
                        currentBallPositions[0] = Vector3.zero;

                        // Let the player fix it
                        isFoul = true;
                    }
                }

                fourBallCueLeftTable = false;
            }
        }
        
        private void ApplyCaromTableColour()
        {
            if (logger)
                logger._Log(name, nameof(ApplyCaromTableColour));
            
            if (!isTeam2Turn)
            {
                tableSrcColour = pointerColour0;
                cueRenderObjs[0].materials[0].SetColor(uniformCueColour, pointerColour0);
                cueRenderObjs[1].materials[0].SetColor(uniformCueColour, pointerColour1 * 0.5f);
            }
            else
            {
                tableSrcColour = pointerColour1;
                cueRenderObjs[0].materials[0].SetColor(uniformCueColour, pointerColour0 * 0.5f);
                cueRenderObjs[1].materials[0].SetColor(uniformCueColour, pointerColour1);
            }
        }
        
        private void InitializeCaromVisuals()
        {
            if (logger)
                logger._Log(name, nameof(InitializeCaromVisuals));
            
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

            for (var i = 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                switch(i) {
                    case 0:
                    case 2:
                    case 3:
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
    }
}

