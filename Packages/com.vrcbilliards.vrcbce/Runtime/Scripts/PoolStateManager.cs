using UdonSharp;
using UnityEngine;
using VRC.Udon.Common.Enums;

// ReSharper disable InconsistentNaming

// ReSharper disable CheckNamespace

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
public partial class PoolStateManager : DebuggableUdon
{
    #if UNITY_ANDROID
       private const float MAX_SIMULATION_TIME_PER_FRAME = 0.05f; // max time to process per frame on quest (~4)
#else
        public float MAX_SIMULATION_TIME_PER_FRAME = 0.1f; // max time to process per frame on pc (~8)
#endif
        public float TIME_PER_STEP = 0.0125f; // time step in seconds per iteration
        private float BALL_DIAMETER_SQUARED_MINUS_EPSILON;
        private float ballRadius;
        private float ONE_OVER_BALL_RADIUS;
        public float EARTH_GRAVITY = 9.80665f; // Earth's gravitational acceleration
        private float BALL_DIAMETER_SQUARED;
        private float BALL_RADIUS_SQUARED;
        public float MASS_OF_BALL = 0.165f; // Weight of ball in kg
        private float POCKET_INNER_RADIUS_SQUARED;
        public Vector3 CONTACT_POINT = new Vector3(0.0f, -0.03f, 0.0f);

        // Cue input tracking
        private const float SIN_A = 0.28078832987f;
        private const float COS_A = 0.95976971915f;
        private const float F = 1.72909790282f;

        private float accumulatedTime;

        private float tableWidthMinusHeight;
        private Vector3 vA;
        private Vector3 vB;
        private Vector3 vC;
        private Vector3 vD;
        private Vector3 vX;
        private Vector3 vY;
        private Vector3 vZ;
        private Vector3 vW;
        private Vector3 pK;
        private Vector3 pL;
        private Vector3 pN;
        private Vector3 pO;
        private Vector3 pP;
        private Vector3 pQ;
        private Vector3 pR;
        private Vector3 pT;
        private Vector3 pS;
        private Vector3 vAvD;
        private Vector3 vAvDNormal;
        private Vector3 vBvY;
        private Vector3 vBvYNormal;
        private Vector3 vCvZNormal;
        private Vector3 vAvBNormal = new Vector3(0.0f, 0.0f, -1.0f);
        private Vector3 vCvWNormal = new Vector3(-1.0f, 0.0f, 0.0f);
        private Vector3 signPos = new Vector3(0.0f, 1.0f, 0.0f);

        private bool enableVerboseLogging = false;
        private bool isCueOutOfBounds;
        private bool IsCueInPlay => !isCueOutOfBounds && !ballsArePocketed[0];

        private void CalculateTableCollisionConstants()
        {
            BALL_DIAMETER_SQUARED_MINUS_EPSILON = Mathf.Pow(ballDiameter, 2) - 0.0002f;
            ballRadius = ballDiameter / 2;
            ONE_OVER_BALL_RADIUS = 1 / (ballDiameter / 2);
            BALL_DIAMETER_SQUARED = Mathf.Pow(ballDiameter, 2);
            BALL_RADIUS_SQUARED = Mathf.Pow(ballDiameter / 2, 2);

            POCKET_INNER_RADIUS_SQUARED = Mathf.Pow(pocketInnerRadius, 2);

            tableWidthMinusHeight = tableWidth - tableHeight;
            // Major source vertices
            vA.x = pocketOuterRadius * 0.92f;
            vA.z = tableHeight;

            vB.x = tableWidth - pocketOuterRadius;
            vB.z = tableHeight;

            vC.x = tableWidth;
            vC.z = tableHeight - pocketOuterRadius;

            vD.x = vA.x - 0.016f;
            vD.z = vA.z + 0.060f;

            // Aux points
            vX = vD + Vector3.forward;
            vW = vC;
            vW.z = 0.0f;

            vY = vB;
            vY.x += 1.0f;
            vY.z += 1.0f;

            vZ = vC;
            vZ.x += 1.0f;
            vZ.z += 1.0f;

            // Normals
            vAvD = vD - vA;
            vAvD = vAvD.normalized;
            vAvDNormal.x = -vAvD.z;
            vAvDNormal.z = vAvD.x;

            vBvY = vB - vY;
            vBvY = vBvY.normalized;
            vBvYNormal.x = -vBvY.z;
            vBvYNormal.z = vBvY.x;

            vCvZNormal = -vBvYNormal;

            // Minkowski difference
            pN = vA;
            pN.z -= ballRadius;

            pL = vD + vAvDNormal * ballRadius;

            pK = vD;
            pK.x -= ballRadius;

            pO = vB;
            pO.z -= ballRadius;
            pP = vB + vBvYNormal * ballRadius;
            pQ = vC + vCvZNormal * ballRadius;

            pR = vC;
            pR.x -= ballRadius;

            pT = vX;
            pT.x -= ballRadius;

            pS = vW;
            pS.x -= ballRadius;

            pS = vW;
            pS.x -= ballRadius;
        }

        // Update loop-scoped handler for cue-locked functionality (warming up and hitting the ball).
        // Non-pure. Returns a Vector3 as it can modify the exact position the cue tip is at.
        private Vector3 AimAndHitCueBall(Vector3 copyOfLocalSpacePositionOfCueTip, Vector3 cueballPosition)
        {
            float sweepTimeBall = Vector3.Dot(cueballPosition - localSpacePositionOfCueTipLastFrame,
                cueLocalForwardDirection);

            // Check for potential skips due to low frame rate
            if (sweepTimeBall > 0.0f && sweepTimeBall <
                (localSpacePositionOfCueTipLastFrame - copyOfLocalSpacePositionOfCueTip).magnitude)
            {
                copyOfLocalSpacePositionOfCueTip =
                    localSpacePositionOfCueTipLastFrame + cueLocalForwardDirection * sweepTimeBall;
            }

            // Hit condition is when cuetip is gone inside ball
            if ((copyOfLocalSpacePositionOfCueTip - cueballPosition).sqrMagnitude < BALL_RADIUS_SQUARED)
            {
                Vector3 force = copyOfLocalSpacePositionOfCueTip - localSpacePositionOfCueTipLastFrame;

                HitBallWithCue(cueTip.transform.forward, Mathf.Min(force.magnitude / Time.fixedDeltaTime, 999.0f));
            }

            return copyOfLocalSpacePositionOfCueTip;
        }

        private void HitBallWithCue(Vector3 shotDirection, float velocity)
        {
            //shotDirection = tableSurface.rotation * shotDirection;

            Vector3 q = tableSurface.InverseTransformDirection(shotDirection); // direction of cue in surface space
            Vector3 o = ballTransforms[0].localPosition; // location of ball in surface

            Vector3 up = tableSurface.up;

            Vector3 j = -Vector3.ProjectOnPlane(q, up); // project cue direction onto table surface, gives us j
            Vector3 i = Vector3.Cross(j, up);

            Plane jkPlane = new Plane(i, o);

            Debug.DrawLine(o, o + i, Color.red, 15f);

            Vector3 Q = raySphereOutput; // point of impact in surface space

            float a = jkPlane.GetDistanceToPoint(Q);
            float b = Q.y - o.y;
            float c = Mathf.Sqrt(Mathf.Max(0, Mathf.Pow(ballRadius, 2) - Mathf.Pow(a, 2) - Mathf.Pow(b, 2)));

            float adj = Mathf.Sqrt(Mathf.Pow(q.x, 2) + Mathf.Pow(q.z, 2));
            float opp = q.y;
            float theta = -Mathf.Atan(opp / adj);

            float cosTheta = Mathf.Cos(theta);
            float sinTheta = Mathf.Sin(theta);

            float k_CUE_MASS = 0.5f; // kg
            float F = 2 * MASS_OF_BALL * velocity / (1 + MASS_OF_BALL / k_CUE_MASS + 5 / (2 * ballRadius) *
                (Mathf.Pow(a, 2) + Mathf.Pow(b, 2) * Mathf.Pow(cosTheta, 2) + Mathf.Pow(c, 2) * Mathf.Pow(sinTheta, 2) -
                 2 * b * c * cosTheta * sinTheta));

            float I = 2f / 5f * MASS_OF_BALL * Mathf.Pow(ballRadius, 2);
            Vector3 v = new Vector3(0, -F / MASS_OF_BALL * cosTheta, -F / MASS_OF_BALL * sinTheta);
            Vector3 w = 1 / I * new Vector3(-c * F * sinTheta + b * F * cosTheta, a * F * sinTheta, -a * F * cosTheta);

            // the paper is inconsistent here. either w.x is inverted (i.e. the i axis points right instead of left) or b is inverted (which means F is wrong too)
            // for my sanity I'm going to assume the former
            w.x = -w.x;

            float m_e = 0.02f;

            // https://billiards.colostate.edu/physics_articles/Alciatore_pool_physics_article.pdf
            float alpha = -Mathf.Atan(
                (5f / 2f * a / ballRadius * Mathf.Sqrt(Mathf.Max(0, 1f - Mathf.Pow(a / ballRadius, 2)))) /
                (1 + MASS_OF_BALL / m_e + 5f / 2f * (1f - Mathf.Pow(a / ballRadius, 2)))
            ) * 180 / Mathf.PI;

            // rewrite to the axis we expect
            v = new Vector3(-v.x, v.z, -v.y);
            w = new Vector3(w.x, -w.z, w.y);

            if (v.y > 0)
            {
                // no scooping
                v.y = 0;
            }
            else if (v.y < 0)
            {
                // the ball must not be under the cue after one time step
                var k_MIN_HORIZONTAL_VEL = (ballRadius - c) / TIME_PER_STEP;

                if (v.z < k_MIN_HORIZONTAL_VEL)
                {
                    // not enough strength to be a jump shot
                    v.y = 0;
                }
                else
                {
                    // dampen y velocity because the table will eat a lot of energy (we're driving the ball straight into it)
                    v.y = -v.y * 0.35f;
                }
            }

            // translate
            Quaternion r = Quaternion.FromToRotation(Vector3.back, j);
            v = r * v;
            w = r * w;

            // apply squirt
            v = Quaternion.AngleAxis(alpha, tableSurface.up) * v;

            // done
            currentBallVelocities[0] = v;
            currentAngularVelocities[0] = w;

            HandleCueBallHit();
        }

        // TODO: This is a single-use function we can refactor. Note that its use is to equate a bool,
        // so it's more acceptable to hold on to.
        private bool IsIntersectingWithSphere(Vector3 start, Vector3 dir, Vector3 sphere)
        {
            Vector3 nrm = dir.normalized;
            Vector3 h = sphere - start;
            float lf = Vector3.Dot(nrm, h);
            float s = BALL_RADIUS_SQUARED - Vector3.Dot(h, h) + (lf * lf);

            if (s < 0.0f)
            {
                return false;
            }

            s = Mathf.Sqrt(s);

            if (lf < s)
            {
                if (lf + s >= 0)
                {
                    s = -s;
                }
                else
                {
                    return false;
                }
            }

            raySphereOutput = start + (nrm * (lf - s));
            return true;
        }

        /// <summary>
        /// Is cue touching another ball?
        /// </summary>
        private bool IsCueContacting()
        {
            switch(gameMode) {
                case GameMode.EightBall:
                    // Check all
                    for (int i = 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
                    {
                        if (ballsArePocketed[i])
                            continue;

                        if ((currentBallPositions[0] - currentBallPositions[i]).sqrMagnitude < BALL_DIAMETER_SQUARED)
                            return true;
                    }
                    
                    return false;
                case GameMode.NineBall:
                    // Only check to 9 ball
                    for (int i = 1; i <= 9; i++)
                    {
                        if (ballsArePocketed[i])
                        {
                            continue;
                        }

                        if ((currentBallPositions[0] - currentBallPositions[i]).sqrMagnitude < BALL_DIAMETER_SQUARED)
                        {
                            return true;
                        }
                    }

                    return false;
                case GameMode.KoreanCarom:
                case GameMode.JapaneseCarom:
                    if ((currentBallPositions[0] - currentBallPositions[9]).sqrMagnitude < BALL_DIAMETER_SQUARED)
                        return true;

                    if ((currentBallPositions[0] - currentBallPositions[2]).sqrMagnitude < BALL_DIAMETER_SQUARED)
                        return true;

                    if ((currentBallPositions[0] - currentBallPositions[3]).sqrMagnitude < BALL_DIAMETER_SQUARED)
                        return true;

                    return false;
                case GameMode.ThreeCushionCarom:
                    if ((currentBallPositions[0] - currentBallPositions[9]).sqrMagnitude < BALL_DIAMETER_SQUARED)
                        return true;

                    if ((currentBallPositions[0] - currentBallPositions[2]).sqrMagnitude < BALL_DIAMETER_SQUARED)
                        return true;

                    return false;
            }

            return false;
        }

        private void AdvancePhysicsStep()
        {
            var ballsMoving = false;

            // Cue angular velocity
            var moved = new bool[NUMBER_OF_SIMULATED_BALLS];

            if (IsCueInPlay)
            {
                if (currentBallPositions[0].y < 0)
                {
                    currentBallPositions[0].y = -currentBallPositions[0].y * 0.35f; // bounce with restitution
                    currentBallVelocities[0].y = -currentBallVelocities[0].y * 0.35f;

                    // Nullify small bounces
                    if (currentBallVelocities[0].y < 0.0025f)
                    {
                        currentBallPositions[0].y = 0;
                        currentBallVelocities[0].y = 0;
                    }
                }

                // Apply movement
                var deltaPos = CalculateCueBallDeltaPosition();
                currentBallPositions[0] += deltaPos;
                moved[0] = deltaPos != Vector3.zero;

                ballsMoving |= StepOneBall(0, moved);
            }

            // Run main simulation / inter-ball collision

            for (var i = 1; i < ballsArePocketed.Length; i++)
            {
                if (ballsArePocketed[i])
                    continue;

                currentBallVelocities[i].y = 0;
                currentBallPositions[i].y = 0;

                var deltaPos = currentBallVelocities[i] * TIME_PER_STEP;
                currentBallPositions[i] += deltaPos;
                moved[i] = deltaPos != Vector3.zero;

                ballsMoving |= StepOneBall(i, moved);
            }

            // Check if simulation has settled
            if (!ballsMoving && turnIsRunning && !_preventEndOfTurn)
            {
                turnIsRunning = false;

                // Make sure we only run this from the client who initiated the move
                if (isSimulatedByUs)
                    HandleEndOfTurn();

                // Check if there was a network update on hold
                if (!isUpdateLocked)
                    return;

                isUpdateLocked = false;

                ReadNetworkData();

                return;
            }

            if (
                IsCueInPlay &&
                currentBallPositions[0].y > ballRadius &&
                (Mathf.Abs(currentBallPositions[0].x) > tableWidth + 0.0001f ||
                 Mathf.Abs(currentBallPositions[0].z) > tableHeight + 0.0001f)
            )
            {
                HandleCueBallOffTable();
            }

            switch (gameMode) {
                case GameMode.EightBall:
                case GameMode.NineBall:
                    if (moved[0] && IsCueInPlay)
                        CalculatePoolBallPhysics(0);
                    
                    // Run edge collision
                    for (var i = 1; i < ballsArePocketed.Length; i++)
                    {
                        if (moved[i] && !ballsArePocketed[i])
                            CalculatePoolBallPhysics(i);
                    }
                    
                    if (moved[0] && IsCueInPlay)
                        CheckIfBallsArePocketed(0);

                    // Run triggers
                    for (var i = 1; i < ballsArePocketed.Length; i++)
                    {
                        if (moved[i] && !ballsArePocketed[i])
                            CheckIfBallsArePocketed(i);
                    }

                    break;
                case GameMode.KoreanCarom:
                case GameMode.JapaneseCarom:
                    if (moved[0] && IsCueInPlay)
                        CalculateCaromBallPhysics(0);
                    if (moved[9])
                        CalculateCaromBallPhysics(9);
                    if (moved[2])
                        CalculateCaromBallPhysics(2);
                    if (moved[3])
                        CalculateCaromBallPhysics(3);

                    break;
                case GameMode.ThreeCushionCarom:
                    if (moved[0] && IsCueInPlay)
                        CalculateCaromBallPhysics(0);
                    if (moved[9])
                        CalculateCaromBallPhysics(9);
                    if (moved[2])
                        CalculateCaromBallPhysics(2);

                    break;
            }
        }

        private Vector3 CalculateCueBallDeltaPosition()
        {
            if (!IsCueInPlay)
                return Vector3.zero;
            
            // Get what will be the next position
            var originalDelta = currentBallVelocities[0] * TIME_PER_STEP;

            var norm = currentBallVelocities[0].normalized;

            // Closest found values
            var minlf = float.MaxValue;
            var minid = 0;
            float mins = 0;

            // Loop balls look for collisions
            for (var i = 1; i < ballsArePocketed.Length; i++)
            {
                if (ballsArePocketed[i])
                    continue;

                var h = currentBallPositions[i] - currentBallPositions[0];
                var lf = Vector3.Dot(norm, h);
                if (lf < 0f)
                    continue;

                var s = BALL_DIAMETER_SQUARED_MINUS_EPSILON - Vector3.Dot(h, h) + lf * lf;

                if (s < 0.0f)
                    continue;

                if (!(lf < minlf))
                    continue;

                minlf = lf;
                minid = i;
                mins = s;
            }

            if (minid <= 0)
                return originalDelta;

            var nmag = minlf - Mathf.Sqrt(mins);

            // Assign new position if got appropriate magnitude
            if (nmag * nmag < originalDelta.sqrMagnitude)
                return norm * nmag;

            return originalDelta;
        }

        private bool StepOneBall(int id, bool[] moved)
        {
            GameObject ball = ballTransforms[id].gameObject;

            bool isBallMoving = false;

            // no point updating velocity if ball isn't moving
            if (currentBallVelocities[id] != Vector3.zero || currentAngularVelocities[id] != Vector3.zero)
            {
                isBallMoving = UpdateVelocity(id, ball);
            }

            moved[id] |= isBallMoving;

            // check for collisions. a non-moving ball might be collided by a moving one
            for (var i = id + 1; i < ballsArePocketed.Length; i++)
            {
                if (ballsArePocketed[i])
                {
                    continue;
                }

                Vector3 delta = currentBallPositions[i] - currentBallPositions[id];
                var dist = delta.sqrMagnitude;

                if (!(dist < BALL_DIAMETER_SQUARED))
                {
                    continue;
                }

                dist = Mathf.Sqrt(dist);
                Vector3 normal = delta / dist;

                // static resolution
                Vector3 res = (ballDiameter - dist) * normal;
                currentBallPositions[i] += res;
                currentBallPositions[id] -= res;
                moved[i] = true;
                moved[id] = true;

                Vector3 velocityDelta = currentBallVelocities[id] - currentBallVelocities[i];

                float dot = Vector3.Dot(velocityDelta, normal);

                // Dynamic resolution (Cr is assumed to be (1)+1.0)

                Vector3 reflection = normal * dot;
                currentBallVelocities[id] -= reflection;
                currentBallVelocities[i] += reflection;

                HandleBallCollision(id, i, reflection);
            }

            return isBallMoving;
        }

        private bool UpdateVelocity(int id, GameObject ball)
        {
            bool ballMoving = false;

            // Since v1.5.0
            Vector3 V = currentBallVelocities[id];
            Vector3 VwithoutY = new Vector3(V.x, 0, V.z);
            Vector3 W = currentAngularVelocities[id];
            Vector3 cv;

            // Equations derived from: http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.89.4627&rep=rep1&type=pdf
            // 
            // R: Contact location with ball and floor aka: (0,-r,0)
            // µₛ: Slipping friction coefficient
            // µᵣ: Rolling friction coefficient
            // i: Up vector aka: (0,1,0)
            // g: Planet Earth's gravitation acceleration ( 9.80665 )
            // 
            // Relative contact velocity (marlow):
            //   c = v + R✕ω
            //
            // Ball is classified as 'rolling' or 'slipping'. Rolling is when the relative velocity is none and the ball is
            // said to be in pure rolling motion
            //
            // When ball is classified as rolling:
            //   Δv = -µᵣ∙g∙Δt∙(v/|v|)
            //
            // Angular momentum can therefore be derived as:
            //   ωₓ = -vᵤ/R
            //   ωᵧ =  0
            //   ωᵤ =  vₓ/R
            //
            // In the slipping state:
            //   Δω = ((-5∙µₛ∙g)/(2/R))∙Δt∙i✕(c/|c|)
            //   Δv = -µₛ∙g∙Δt(c/|c|)

            if (currentBallPositions[id].y < 0.001)
            {
                // Relative contact velocity of ball and table
                cv = VwithoutY + Vector3.Cross(CONTACT_POINT, W);
                float cvMagnitude = cv.magnitude;

                // Rolling is achieved when cv's length is approaching 0
                // The epsilon is quite high here because of the fairly large timestep we are working with
                if (cvMagnitude <= 0.1f)
                {
                    //V += -k_F_ROLL * k_GRAVITY * k_FIXED_TIME_STEP * V.normalized;
                    // (baked):
                    V += -0.00122583125f * VwithoutY.normalized;

                    // Calculate rolling angular velocity
                    W.x = -V.z * ONE_OVER_BALL_RADIUS;

                    if (0.3f > Mathf.Abs(W.y))
                    {
                        W.y = 0.0f;
                    }
                    else
                    {
                        W.y -= Mathf.Sign(W.y) * 0.3f;
                    }

                    W.z = V.x * ONE_OVER_BALL_RADIUS;

                    // Stopping scenario
                    if (V.sqrMagnitude < 0.0001f && W.magnitude < 0.04f)
                    {
                        W = Vector3.zero;
                        V = Vector3.zero;
                    }
                    else
                    {
                        ballMoving = true;
                    }
                }
                else // Slipping
                {
                    Vector3 nv = cv / cvMagnitude;

                    // Angular slipping friction
                    //W += ((-5.0f * k_F_SLIDE * k_GRAVITY)/(2.0f * 0.03f)) * k_FIXED_TIME_STEP * Vector3.Cross( Vector3.up, nv );
                    // (baked):
                    W += -2.04305208f * Vector3.Cross(Vector3.up, nv);

                    //V += -k_F_SLIDE * k_GRAVITY * k_FIXED_TIME_STEP * nv;
                    // (baked):
                    V += -0.024516625f * nv;

                    ballMoving = true;
                }
            }
            else
            {
                ballMoving = true;
            }

            if (currentBallPositions[id].y > 0) // small epsilon to apply gravity
                V.y -= EARTH_GRAVITY * TIME_PER_STEP;
            else
                V.y = 0;

            currentAngularVelocities[id] = W;
            currentBallVelocities[id] = V;

            ball.transform.Rotate(this.transform.TransformDirection(W.normalized),
                W.magnitude * TIME_PER_STEP * -Mathf.Rad2Deg, Space.World);

            return ballMoving;
        }

        private void CalculatePoolBallPhysics(int id)
        {
            Vector3 nToVNormalized;
            Vector3 a_to_v;
            float dot;

            var ballUnderTest = currentBallPositions[id];

            signPos.x = Mathf.Sign(ballUnderTest.x);
            signPos.z = Mathf.Sign(ballUnderTest.z);

            ballUnderTest = Vector3.Scale(ballUnderTest, signPos);

            if (ballUnderTest.x > vA.x) // Major Regions
            {
                if (ballUnderTest.x > ballUnderTest.z + tableWidthMinusHeight) // Minor B
                {
                    if (ballUnderTest.z < tableHeight - pocketOuterRadius)
                    {
                        // Region H
                        if (ballUnderTest.x > tableWidth - ballRadius)
                        {
                            // Static resolution
                            ballUnderTest.x = tableWidth - ballRadius;

                            // Dynamic
                            ApplyCushionBounce(id, Vector3.Scale(vCvWNormal, signPos));
                        }
                    }
                    else
                    {
                        a_to_v = ballUnderTest - vC;

                        if (Vector3.Dot(a_to_v, vBvY) > 0.0f)
                        {
                            // Region I ( VORONI )
                            if (a_to_v.magnitude < ballRadius)
                            {
                                // Static resolution
                                nToVNormalized = a_to_v.normalized;
                                ballUnderTest = vC + nToVNormalized * ballRadius;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(nToVNormalized, signPos));
                            }
                        }
                        else
                        {
                            // Region J
                            a_to_v = ballUnderTest - pQ;

                            if (Vector3.Dot(vCvZNormal, a_to_v) < 0.0f)
                            {
                                // Static resolution
                                dot = Vector3.Dot(a_to_v, vBvY);
                                ballUnderTest = pQ + dot * vBvY;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(vCvZNormal, signPos));
                            }
                        }
                    }
                }
                else // Minor A
                {
                    if (ballUnderTest.x < vB.x)
                    {
                        // Region A
                        if (ballUnderTest.z > pN.z)
                        {
                            // Velocity based A->C delegation ( scuffed CCD )
                            a_to_v = ballUnderTest - vA;
                            var _V = Vector3.Scale(currentBallVelocities[id], signPos);
                            var V = new Vector3(-_V.z, 0.0f, _V.x);
                            
                            if (ballUnderTest.z > vA.z)
                            {
                                if (Vector3.Dot(V, a_to_v) > 0.0f)
                                {
                                    // Region C ( Delegated )
                                    a_to_v = ballUnderTest - pL;

                                    // Static resolution
                                    dot = Vector3.Dot(a_to_v, vAvD);
                                    ballUnderTest = pL + dot * vAvD;

                                    // Dynamic
                                    ApplyCushionBounce(id, Vector3.Scale(vAvDNormal, signPos));
                                }
                                else
                                {
                                    // Static resolution
                                    ballUnderTest.z = pN.z;

                                    // Dynamic
                                    ApplyCushionBounce(id, Vector3.Scale(vAvBNormal, signPos));
                                }
                            }
                            else
                            {
                                // Static resolution
                                ballUnderTest.z = pN.z;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(vAvBNormal, signPos));
                            }
                        }
                    }
                    else
                    {
                        a_to_v = ballUnderTest - vB;

                        if (Vector3.Dot(a_to_v, vBvY) > 0.0f)
                        {
                            // Region F ( VERONI )
                            if (a_to_v.magnitude < ballRadius)
                            {
                                // Static resolution
                                nToVNormalized = a_to_v.normalized;
                                ballUnderTest = vB + nToVNormalized * ballRadius;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(nToVNormalized, signPos));
                            }
                        }
                        else
                        {
                            // Region G
                            a_to_v = ballUnderTest - pP;

                            if (Vector3.Dot(vBvYNormal, a_to_v) < 0.0f)
                            {
                                // Static resolution
                                dot = Vector3.Dot(a_to_v, vBvY);
                                ballUnderTest = pP + dot * vBvY;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(vBvYNormal, signPos));
                            }
                        }
                    }
                }
            }
            else
            {
                a_to_v = ballUnderTest - vA;

                if (Vector3.Dot(a_to_v, vAvD) > 0.0f)
                {
                    a_to_v = ballUnderTest - vD;

                    if (Vector3.Dot(a_to_v, vAvD) > 0.0f)
                    {
                        if (ballUnderTest.z > pK.z)
                        {
                            // Region E
                            if (ballUnderTest.x > pK.x)
                            {
                                // Static resolution
                                ballUnderTest.x = pK.x;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(vCvWNormal, signPos));
                            }
                        }
                        else
                        {
                            // Region D ( VORONI )
                            if (a_to_v.magnitude < ballRadius)
                            {
                                // Static resolution
                                nToVNormalized = a_to_v.normalized;
                                ballUnderTest = vD + nToVNormalized * ballRadius;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(nToVNormalized, signPos));
                            }
                        }
                    }
                    else
                    {
                        // Region C
                        a_to_v = ballUnderTest - pL;

                        if (Vector3.Dot(vAvDNormal, a_to_v) < 0.0f)
                        {
                            // Static resolution
                            dot = Vector3.Dot(a_to_v, vAvD);
                            ballUnderTest = pL + dot * vAvD;

                            // Dynamic
                            ApplyCushionBounce(id, Vector3.Scale(vAvDNormal, signPos));
                        }
                    }
                }
                else
                {
                    // Region B ( VORONI )
                    if (a_to_v.magnitude < ballRadius)
                    {
                        // Static resolution
                        nToVNormalized = a_to_v.normalized;
                        ballUnderTest = vA + nToVNormalized * ballRadius;

                        // Dynamic
                        ApplyCushionBounce(id, Vector3.Scale(nToVNormalized, signPos));
                    }
                }
            }

            currentBallPositions[id] = Vector3.Scale(ballUnderTest, signPos);
        }

        private void ApplyCushionBounce(int id, Vector3 n)
        {
            // Don't bounce if the cushions obviously could not collide.
            if (currentBallPositions[id].y > ballRadius)
                return;

            // Reject bounce if velocity is going the same way as normal
            // this state means we tunneled, but it happens only on the corner
            // vertexes
            Vector3 currentBallVelocity = currentBallVelocities[id];
            if (Vector3.Dot(currentBallVelocity, n) > 0.0f)
                return;

            // Rotate V, W to be in the reference frame of cushion
            Quaternion rq = Quaternion.AngleAxis(Mathf.Atan2(-n.z, -n.x) * Mathf.Rad2Deg, Vector3.up);
            Quaternion rb = Quaternion.Inverse(rq);
            Vector3 V = rq * currentBallVelocity;
            Vector3 W = rq * currentAngularVelocities[id];

            Vector3 V1 = new Vector3(-V.x * F - 0.00240675711f * W.z, 0f,
                0.71428571428f * V.z + 0.00857142857f * (W.x * SIN_A - W.y * COS_A) - V.z);

            var s_x = V.x * SIN_A + W.z;
            var s_z = -V.z - W.y * COS_A + W.x * SIN_A;

            var k = s_z * 0.71428571428f;
            var c = V.x * COS_A;
            Vector3 W1 = new Vector3(k * SIN_A, k * COS_A, 15.625f * (-s_x * 0.04571428571f + c * 0.0546021744f));
            
            // Unrotate result
            currentBallVelocities[id] += rb * V1;
            currentAngularVelocities[id] += rb * W1;

            if (id == 0)
                cushionsHitThisTurn++;
        }

        private void CheckIfBallsArePocketed(int id)
        {
            Vector3 A = currentBallPositions[id];
            Vector3 absA = new Vector3(Mathf.Abs(A.x), A.y, Mathf.Abs(A.z));

            if (
                (absA - cornerPocket).sqrMagnitude < POCKET_INNER_RADIUS_SQUARED ||
                (absA - middlePocket).sqrMagnitude < POCKET_INNER_RADIUS_SQUARED ||
                absA.z > middlePocket.z ||
                absA.z > -absA.x + cornerPocket.x + cornerPocket.z
            )
            {
                TriggerPocketBall(id);

                currentBallVelocities[id] = Vector3.zero;
                currentAngularVelocities[id] = Vector3.zero;
            }
        }

        private bool _preventEndOfTurn;

        private void HandleCueBallOffTable()
        {
            if (logger)
                logger._Log(name, "HandleCueBallOffTable");
            
            // Only run this once.
            if (!IsCueInPlay)
                return;
            
            isCueOutOfBounds = true;
            
            if (IsCarom)
                fourBallCueLeftTable = true;
            else
                ballsArePocketed[0] = true;

            HandleFoulEffects();

            // VFX ( make ball move )
            var body = ballRigidbodies[0];
            body.isKinematic = false;
            body.velocity = transform.TransformVector(new Vector3(
                currentBallVelocities[0].x,
                currentBallVelocities[0].y,
                currentBallVelocities[0].z
            ));

            if (!cueBallController)
                return;

            cueBallController._EnableDonking(this, 3);
            _preventEndOfTurn = true;
        }

        public void _AllowEndOfTurn()
        {
            _preventEndOfTurn = false;
        }

        private void TriggerPocketBall(int id)
        {
            var total = 0;

            // Get total for X positioning
            foreach (var pocketed in ballsArePocketed)
            {
                if (pocketed)
                    total++;
            }

            // place ball on the rack
            currentBallPositions[id] = Vector3.zero + (float) total * ballDiameter * Vector3.zero;

            // This is where we actually save the pocketed/non-pocketed state of balls.
            ballsArePocketed[id] = true;

            bool success = false;

            // isOpen is only ever false in blackball - so this covers any ball sunk in 9 ball, which is correct (in
            // our simplified 9-ball, the only way to trigger a foul is to foul the cue ball or not hit the lowest-count
            // ball first when shooting.
            if (isOpen && id > 1)
                success = true;
            // it is blue's turn
            else if ((isTeam2Turn && isTeam2Blue || !isTeam2Turn && !isTeam2Blue) && id > 1 && id < 9)
                success = true;
            // it is orange's turn
            else if (id >= 9)
                success = true;

            HandleBallSunk(success);

            if (id == 0)
                isCueOutOfBounds = true;

            // VFX ( make ball move )
            var body = ballRigidbodies[id];
            body.isKinematic = false;
            body.velocity = transform.TransformVector(new Vector3(
                currentBallVelocities[id].x,
                currentBallVelocities[id].y,
                currentBallVelocities[id].z
            ));
        }
    }
}