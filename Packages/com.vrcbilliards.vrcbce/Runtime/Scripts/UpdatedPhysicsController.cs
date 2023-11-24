
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;

namespace VRCBilliards
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class UpdatedPhysicsController : UdonSharpBehaviour
    {
#if UNITY_ANDROID
   private const float MAX_SIMULATION_TIME_PER_FRAME = 0.05f; // max time to process per frame on quest (~4)
#else
        private const float MAX_SIMULATION_TIME_PER_FRAME = 0.1f; // max time to process per frame on pc (~8)
#endif
        private const float TIME_PER_STEP = 0.0125f; // time step in seconds per iteration
        private const float BALL_DIAMETER_SQUARED_PLUS_EPSILON = 0.003598f;            // ball diameter squared plus epsilon
        private const float BALL_DIAMETER = 0.06f;                // width of ball
        private const float BALL_RADIUS = 0.03f;
        private const float ONE_OVER_BALL_RADIUS = 33.3333333333f;       // 1 over ball radius
        private const float EARTH_GRAVITY = 9.80665f;             // Earth's gravitational acceleration
        private const float BALL_DIAMETER_SQUARED = 0.0036f;              // ball diameter squared
        private const float MASS_OF_BALL = 0.16f;                // Weight of ball in kg
        private const float BALL_RADIUS_SQUARED = 0.0009f;              // ball radius squared

        // ReSharper disable once InconsistentNaming
        private Vector3 CONTACT_POINT = new Vector3(0.0f, -0.03f, 0.0f);

        public Transform transformSurface;

        //private BilliardsModule table;

        private float accumulatedTime;
        private float lastTimestamp;

        private Transform[] balls;
        private Vector3[] ballPositions;
        private Vector3[] ballVelocities;
        private Vector3[] ballAngularVelocities;
        private float pocketInnerRadiusSquared;

        float tableWidth;
        float tableHeight;
        float pocketOuterRadius;
        float cushionRadius;
        private Vector3 cornerPocket;
        private Vector3 sidePocket;

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

        // Universal billiards game state trackers

        // Is the game currently running?
        private bool isGameRunning;
        // Is physics sim on or is the table in a resting state?
        private bool isPhysicsSimulationRunning;
        // Is it the local player's turn?
        private bool isPlayLocal;
        // Is the cue ball being repositioned?
        private bool isRepositioning;
        // Is the cue ball hittable - is the active cue being held?
        private bool isCueBallHittable;

        private bool[] ballPocketedState = new bool[25];

        // Cue input tracking

        private Transform currentCueTip;

        private Vector3 cueLocalPosition;
        private Vector3 cueLastLocalPosition;
        private Vector3 cue_vdir;
        private Vector3 cue_shotdir;

        const float sinA = 0.28078832987f;
        const float cosA = 0.95976971915f;
        const float f = 1.72909790282f;

        // Where did we actually hit the cue ball?
        private Vector3 raySphereOutput;

        public void _Init(
            Transform[] inBalls,
            Vector3[] inBallsPosition,
            Vector3[] inBallVelocities,
            Vector3[] inBallAngularVelocities,
            float inTableWidth,
            float inTableHeight,
            float inPocketOuterRadius,
            float inPocketInnerRadiusSquared,
            float inCushionRadius,
            Vector3 inCornerPocket,
            Vector3 inSidePocket
        )
        {
            balls = inBalls;
            ballPositions = inBallsPosition;
            ballVelocities = inBallVelocities;
            ballAngularVelocities = inBallAngularVelocities;

            tableWidth = inTableWidth;
            tableHeight = inTableHeight;
            pocketOuterRadius = inPocketOuterRadius;
            cushionRadius = inCushionRadius;
            pocketInnerRadiusSquared = inPocketInnerRadiusSquared;
            cornerPocket = inCornerPocket;
            sidePocket = inSidePocket;

            // Handy values
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
            pN.z -= cushionRadius;

            pL = vD + vAvDNormal * cushionRadius;

            pK = vD;
            pK.x -= cushionRadius;

            pO = vB;
            pO.z -= cushionRadius;
            pP = vB + vBvYNormal * cushionRadius;
            pQ = vC + vCvZNormal * cushionRadius;

            pR = vC;
            pR.x -= cushionRadius;

            pT = vX;
            pT.x -= cushionRadius;

            pS = vW;
            pS.x -= cushionRadius;

            pS = vW;
            pS.x -= cushionRadius;
        }

        public void _FixedTick()
        {
            lastTimestamp = Time.timeSinceLevelLoad;

            if (isGameRunning && currentCueTip)
            {
                TickCue();
            }

            if (!isPhysicsSimulationRunning) return;

            float newAccumulatedTime = Mathf.Clamp(accumulatedTime + Time.fixedDeltaTime, 0, MAX_SIMULATION_TIME_PER_FRAME);
            while (newAccumulatedTime >= TIME_PER_STEP)
            {
                TickOnce();
                newAccumulatedTime -= TIME_PER_STEP;
            }

            accumulatedTime = newAccumulatedTime;
        }

        // Do physics calculations related to the cue, when appropriate - calculate the guideline, etc.
        private void TickCue()
        {
            cueLocalPosition = transformSurface.InverseTransformPoint(currentCueTip.position);
            Vector3 currentCueLocalPosition = cueLocalPosition;

            // if shot is prepared for next hit
            if (isPlayLocal)
            {
                bool isContact = false;

                if (isRepositioning)
                {
                    //table.markerObj.transform.position = balls[0].transform.position;
                    isContact = isCueBallTouching();
                    //if (isContact)
                    //{
                    //    table.markerObj.GetComponent<MeshRenderer>().material.SetColor("_Color", markerColorNo);
                    //}
                    //else
                    //{
                    //    table.markerObj.GetComponent<MeshRenderer>().material.SetColor("_Color", markerColorYes);
                    //}
                }

                Vector3 cueBallPosition = ballPositions[0];

                if (isCueBallHittable && !isContact)
                {
                    float ballSweepTime = Vector3.Dot(cueBallPosition - cueLastLocalPosition, cue_vdir);

                    // Check for potential skips due to low frame rate
                    if (ballSweepTime > 0.0f && ballSweepTime < (cueLastLocalPosition - currentCueLocalPosition).magnitude)
                    {
                        currentCueLocalPosition = cueLastLocalPosition + cue_vdir * ballSweepTime;
                    }

                    // Hit condition is when cuetip is gone inside ball
                    if ((currentCueLocalPosition - cueBallPosition).sqrMagnitude < BALL_RADIUS_SQUARED)
                    {
                        Vector3 force = currentCueLocalPosition - cueLastLocalPosition;

                        // This is the juice - if the cue ball hits, convey force to the cue ball to make things start happening.
                        ApplyPhysicsToTable(currentCueTip.transform.forward, Mathf.Min(force.magnitude / Time.fixedDeltaTime, 999.0f));

                        // TODO: This is the point where the table tells the GameManager the cue ball has been hit.
                        //table._TriggerCueBallHit();
                    }
                }
                else
                {
                    cue_vdir = transform.InverseTransformVector(currentCueTip.transform.forward);

                    // Get where the cue will strike the ball
                    if (isIntersectingWithSphere(currentCueLocalPosition, cue_vdir, cueBallPosition))
                    {
                        // TODO: More UI stuff here - refactor out.
                        // if (!table.noGuidelineLocal)
                        // {
                        //     table.guideline.SetActive(true);
                        //     table.devhit.SetActive(true);
                        // }
                        // table.devhit.transform.localPosition = RaySphere_output;

                        Vector3 q = transformSurface.InverseTransformDirection(currentCueTip.transform.forward); // direction of cue in surface space
                        Vector3 o = balls[0].transform.localPosition; // location of ball in surface

                        Vector3 j = -Vector3.ProjectOnPlane(q, transformSurface.up); // project cue direction onto table surface, gives us j
                        Vector3 k = transformSurface.up;
                        Vector3 i = Vector3.Cross(j, k);

                        Plane jkPlane = new Plane(i, o);

                        Vector3 Q = raySphereOutput; // point of impact in surface space

                        float a = jkPlane.GetDistanceToPoint(Q);
                        float b = Q.y - o.y;
                        float c = Mathf.Sqrt(Mathf.Pow(BALL_RADIUS, 2) - Mathf.Pow(a, 2) - Mathf.Pow(b, 2));

                        float adj = Mathf.Sqrt(Mathf.Pow(q.x, 2) + Mathf.Pow(q.z, 2));
                        float opp = q.y;
                        float theta = -Mathf.Atan(opp / adj);

                        float cosTheta = Mathf.Cos(theta);
                        float sinTheta = Mathf.Sin(theta);

                        float V0 = 5; // probably fine, right?
                        float k_CUE_MASS = 0.5f; // kg
                        float F = 2 * MASS_OF_BALL * V0 / (1 + MASS_OF_BALL / k_CUE_MASS + 5 / (2 * BALL_RADIUS) * (Mathf.Pow(a, 2) + Mathf.Pow(b, 2) * Mathf.Pow(cosTheta, 2) + Mathf.Pow(c, 2) * Mathf.Pow(sinTheta, 2) - 2 * b * c * cosTheta * sinTheta));


                        float I = 2f / 5f * MASS_OF_BALL * Mathf.Pow(BALL_RADIUS, 2);
                        Vector3 v = new Vector3(0, -F / MASS_OF_BALL * cosTheta, -F / MASS_OF_BALL * sinTheta);
                        Vector3 w = 1 / I * new Vector3(-c * F * sinTheta + b * F * cosTheta, a * F * sinTheta, -a * F * cosTheta);

                        // the paper is inconsistent here. either w.x is inverted (i.e. the i axis points right instead of left) or b is inverted (which means F is wrong too)
                        // for my sanity I'm going to assume the former
                        w.x = -w.x;

                        float m_e = 0.02f;

                        // https://billiards.colostate.edu/physics_articles/Alciatore_pool_physics_article.pdf
                        float alpha = -Mathf.Atan(
                           (5f / 2f * a / BALL_RADIUS * Mathf.Sqrt(1f - Mathf.Pow(a / BALL_RADIUS, 2))) /
                           (1 + MASS_OF_BALL / m_e + 5f / 2f * (1f - Mathf.Pow(a / BALL_RADIUS, 2)))
                        ) * 180 / Mathf.PI;

                        // rewrite to the axis we expect
                        v = new Vector3(-v.x, v.z, -v.y);

                        // translate
                        Quaternion r = Quaternion.FromToRotation(Vector3.back, j);
                        v = r * v;
                        w = r * w;

                        // apply squirt
                        Vector3 before = v;
                        v = Quaternion.AngleAxis(alpha, transformSurface.up) * v;
                        Vector3 after = v;

                        //cue_shotdir = v;
                        //cue_fdir = Mathf.Atan2(cue_shotdir.z, cue_shotdir.x);

                        // TODO: This calls out to the GameManager saying to update the guideline with the calculated location. 1. Is there an easier way than the above physics? 2. Refactor.

                        // // Update the prediction line direction
                        // table.guideline.transform.localPosition = ballPositions[0];
                        // table.guideline.transform.localEulerAngles = new Vector3(0.0f, -cue_fdir * Mathf.Rad2Deg, 0.0f);
                    }
                    else
                    {
                        // TODO: This says "if the cue isn't targeted at the cue ball, don't display the UI for if it'll hit" - refactor.

                        // table.devhit.SetActive(false);
                        // table.guideline.SetActive(false);
                    }
                }
            }

            cueLastLocalPosition = currentCueLocalPosition;
        }

        // Run one physics iteration for all balls
        private void TickOnce()
        {
            bool ballsMoving = false;

            // Cue angular velocity
            bool[] moved = new bool[balls.Length];

            if (!ballPocketedState[0])
            {
                if (ballPositions[0].y < 0)
                {
                    ballPositions[0].y = 0;
                    ballPositions[0].y = -ballPositions[0].y * 0.5f; // bounce with restitution
                }

                // Apply movement
                Vector3 deltaPos = CalculateCueBallDeltaPosition();
                ballPositions[0] += deltaPos;
                moved[0] = deltaPos != Vector3.zero;

                ballsMoving |= StepOneBall(0, moved);
            }

            // Run main simulation / inter-ball collision

            for (int i = 1; i < ballPocketedState.Length; i++)
            {
                if (ballPocketedState[i])
                {
                    continue;
                }

                ballVelocities[i].y = 0;
                ballPositions[i].y = 0;

                Vector3 deltaPos = ballVelocities[i] * TIME_PER_STEP;
                ballPositions[i] += deltaPos;
                moved[i] = deltaPos != Vector3.zero;

                ballsMoving |= StepOneBall(i, moved);
            }

            // Check if simulation has settled
            if (!ballsMoving)
            {
                // TODO: simulation has ceased - the turn has ended! Refactor.

                // table._TriggerSimulationEnded(false);
                return;
            }

            bool canCueBallBounceOffCushion = ballPositions[0].y < BALL_RADIUS;

            // TODO: What this is trying to do is handle carom games (no pockets) - we're ignoring this at the moment - if carom physics is different, then use a different physics engine for it.
            // if (table.is4Ball)
            // {
            //     if (canCueBallBounceOffCushion && moved[0]) _phy_ball_table_carom(0);
            //     if (moved[13]) _phy_ball_table_carom(13);
            //     if (moved[14]) _phy_ball_table_carom(14);
            //     if (moved[15]) _phy_ball_table_carom(15);
            // }
            // else
            //{
            // Run edge collision
            for (int i = 0; i < ballPocketedState.Length; i++)
            {
                if (moved[i] && !ballPocketedState[i] && (i != 0 || canCueBallBounceOffCushion))
                {
                    CalculateBallPosition(i);
                }
            }
            // }

            bool outOfBounds = false;
            if (!ballPocketedState[0])
            {
                if (Mathf.Abs(ballPositions[0].x) > tableWidth + 0.1 || Mathf.Abs(ballPositions[0].z) > tableHeight + 0.1)
                {
                    // TODO: this is the system telling the GameManager that the cue ball has been pocketed.
                    // table._TriggerPocketBall(0);
                    outOfBounds = true;
                }
            }

            // if (table.is4Ball) return;

            // Run triggers
            for (int i = 0; i < ballPocketedState.Length; i++)
            {
                if (moved[i] && !ballPocketedState[i] && (i != 0 || !outOfBounds))
                {
                    if (i != 0 || canCueBallBounceOffCushion)
                    {
                        _phy_ball_pockets(i);
                    }
                }
            }
        }

        private Vector3 CalculateCueBallDeltaPosition()
        {
            // Get what will be the next position
            Vector3 originalDelta = ballVelocities[0] * TIME_PER_STEP;

            Vector3 norm = ballVelocities[0].normalized;

            Vector3 h;
            float lf, s, nmag;

            // Closest found values
            float minlf = float.MaxValue;
            int minid = 0;
            float mins = 0;

            // Loop balls look for collisions
            for (var i = 1; i < ballPocketedState.Length; i++)
            {
                if (ballPocketedState[i])
                {
                    continue;
                }

                h = ballPositions[i] - ballPositions[0];
                lf = Vector3.Dot(norm, h);
                if (lf < 0f)
                {
                    continue;
                }

                s = BALL_DIAMETER_SQUARED_PLUS_EPSILON - Vector3.Dot(h, h) + lf * lf;

                if (s < 0.0f)
                {
                    continue;
                }

                if (!(lf < minlf))
                {
                    continue;
                }

                minlf = lf;
                minid = i;
                mins = s;
            }

            if (minid <= 0)
            {
                return originalDelta;
            }

            nmag = minlf - Mathf.Sqrt(mins);

            // Assign new position if got appropriate magnitude
            if (nmag * nmag < originalDelta.sqrMagnitude)
            {
                return norm * nmag;
            }

            return originalDelta;
        }

        // Advance simulation 1 step for ball id
        private bool StepOneBall(int id, bool[] moved)
        {
            Transform ball = balls[id];

            bool isBallMoving = false;

            // no point updating velocity if ball isn't moving
            if (ballVelocities[id] != Vector3.zero || ballAngularVelocities[id] != Vector3.zero)
            {
                isBallMoving = updateVelocity(id, ball);
            }

            moved[id] |= isBallMoving;

            // check for collisions. a non-moving ball might be collided by a moving one
            for (var i = id + 1; i < ballPocketedState.Length; i++)
            {
                if (ballPocketedState[i])
                {
                    continue;
                }

                Vector3 delta = ballPositions[i] - ballPositions[id];
                var dist = delta.sqrMagnitude;

                if (!(dist < BALL_DIAMETER_SQUARED))
                {
                    continue;
                }

                dist = Mathf.Sqrt(dist);
                Vector3 normal = delta / dist;

                // static resolution
                Vector3 res = (BALL_DIAMETER - dist) * normal;
                ballPositions[i] += res;
                ballPositions[id] -= res;
                moved[i] = true;
                moved[id] = true;

                Vector3 velocityDelta = ballVelocities[id] - ballVelocities[i];

                float dot = Vector3.Dot(velocityDelta, normal);

                // Dynamic resolution (Cr is assumed to be (1)+1.0)

                Vector3 reflection = normal * dot;
                ballVelocities[id] -= reflection;
                ballVelocities[i] += reflection;

                // TODO: Ball collision occured! The code wants to proc a sound and tell the table. Refactor.

                // // Prevent sound spam if it happens
                // if (ballVelocities[id].sqrMagnitude > 0 && ballVelocities[i].sqrMagnitude > 0)
                // {
                //     ball.GetComponent<AudioSource>().PlayOneShot(hitSounds[id % 3], Mathf.Clamp01(reflection.magnitude));
                // }
                //
                // table._TriggerCollision(id, i);
            }

            return isBallMoving;
        }

        private bool updateVelocity(int id, Transform ball)
        {
            bool ballMoving = false;

            // Since v1.5.0
            Vector3 V = ballVelocities[id];
            Vector3 VwithoutY = new Vector3(V.x, 0, V.z);
            Vector3 W = ballAngularVelocities[id];
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

            if (ballPositions[id].y < 0.001)
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

            if (ballPositions[id].y > 0) // small epsilon to apply gravity
                V.y -= EARTH_GRAVITY * TIME_PER_STEP;
            else
                V.y = 0;

            ballAngularVelocities[id] = W;
            ballVelocities[id] = V;

            ball.Rotate(this.transform.TransformDirection(W.normalized), W.magnitude * TIME_PER_STEP * -Mathf.Rad2Deg, Space.World);

            return ballMoving;
        }

#if UNITY_ANDROID
#else
        [HideInInspector]
        public Vector3 dkTargetPos;            // Target for desktop aiming
#endif


        [NonSerialized] public bool outIsTouching;
        public void _IsCueBallTouching()
        {
            outIsTouching = isCueBallTouching();
        }

        private bool isCueBallTouching()
        {
            for (int i = 1; i < ballPocketedState.Length; i++)
            {
                if ((ballPositions[0] - ballPositions[i]).sqrMagnitude < BALL_DIAMETER_SQUARED)
                {
                    return true;
                }
            }

            // TODO: we assume 8 ball atm

            // if (table.is8Ball) // 8 ball
            // {
            // Check all
            // for (int i = 1; i < 16; i++)
            // {
            //     if ((ballPositions[0] - ballPositions[i]).sqrMagnitude < BALL_DIAMETER_SQUARED)
            //     {
            //         return true;
            //     }
            // }
            // }
            // else if (table.is9Ball) // 9
            // {
            //     // Only check to 9 ball
            //     for (int i = 1; i <= 9; i++)
            //     {
            //         if ((ballPositions[0] - ballPositions[i]).sqrMagnitude < BALL_DIAMETER_SQUARED)
            //         {
            //             return true;
            //         }
            //     }
            // }
            // else // 4
            // {
            //     if ((ballPositions[0] - ballPositions[9]).sqrMagnitude < BALL_DIAMETER_SQUARED)
            //     {
            //         return true;
            //     }
            //     if ((ballPositions[0] - ballPositions[2]).sqrMagnitude < BALL_DIAMETER_SQUARED)
            //     {
            //         return true;
            //     }
            //     if ((ballPositions[0] - ballPositions[3]).sqrMagnitude < BALL_DIAMETER_SQUARED)
            //     {
            //         return true;
            //     }
            // }

            return false;
        }

        void ApplyCushionBounce(int id, Vector3 n)
        {
            // Reject bounce if velocity is going the same way as normal
            // this state means we tunneled, but it happens only on the corner
            // vertexes
            Vector3 source_v = ballVelocities[id];
            if (Vector3.Dot(source_v, n) > 0.0f)
            {
                return;
            }

            // Rotate V, W to be in the reference frame of cushion
            Quaternion rq = Quaternion.AngleAxis(Mathf.Atan2(-n.z, -n.x) * Mathf.Rad2Deg, Vector3.up);
            Quaternion rb = Quaternion.Inverse(rq);
            Vector3 V = rq * source_v;
            Vector3 W = rq * ballAngularVelocities[id];

            Vector3 V1;
            Vector3 W1;
            float k, c, s_x, s_z;

            //V1.x = -V.x * ((2.0f/7.0f) * k_SINA2 + k_EP1 * k_COSA2) - (2.0f/7.0f) * k_BALL_PL_X * W.z * k_SINA;
            //V1.z = (5.0f/7.0f)*V.z + (2.0f/7.0f) * k_BALL_PL_X * (W.x * k_SINA - W.y * k_COSA) - V.z;
            //V1.y = 0.0f; 
            // (baked):
            V1.x = -V.x * f - 0.00240675711f * W.z;
            V1.z = 0.71428571428f * V.z + 0.00857142857f * (W.x * sinA - W.y * cosA) - V.z;
            V1.y = 0.0f;

            // s_x = V.x * k_SINA - V.y * k_COSA + W.z;
            // (baked): y component not used:
            s_x = V.x * sinA + W.z;
            s_z = -V.z - W.y * cosA + W.x * sinA;

            // k = (5.0f * s_z) / ( 2 * k_BALL_MASS * k_A ); 
            // (baked):
            k = s_z * 0.71428571428f;

            // c = V.x * k_COSA - V.y * k_COSA;
            // (baked): y component not used
            c = V.x * cosA;

            W1.x = k * sinA;

            //W1.z = (5.0f / (2.0f * k_BALL_MASS)) * (-s_x / k_A + ((k_SINA * c * k_EP1) / k_B) * (k_COSA - k_SINA));
            // (baked):
            W1.z = 15.625f * (-s_x * 0.04571428571f + c * 0.0546021744f);
            W1.y = k * cosA;

            // Unrotate result
            ballVelocities[id] += rb * V1;
            ballAngularVelocities[id] += rb * W1;
        }

        //
        // public void _InitConstants()
        // {
        //     tableWidth = table.k_TABLE_WIDTH;
        //     tableHeight = table.k_TABLE_HEIGHT;
        //     pocketOuterRadius = table.k_POCKET_RADIUS;
        //     cushionRadius = table.k_CUSHION_RADIUS;
        //
        //     for (int i = 0; i < table.pockets.Length; i++)
        //     {
        //         table.pockets[i].SetActive(false);
        //     }
        //     MeshCollider collider = table.table.GetComponent<MeshCollider>();
        //     if (collider != null) collider.enabled = false;
        //     collider = table.auto_pocketblockers.GetComponent<MeshCollider>();
        //     if (collider != null) collider.enabled = false;
        //
        //     // Handy values
        //     k_MINOR_REGION_CONST = table.k_TABLE_WIDTH - table.k_TABLE_HEIGHT;
        //
        //     // Major source vertices
        //     k_vA.x = table.k_POCKET_RADIUS * 0.92f;
        //     k_vA.z = table.k_TABLE_HEIGHT;
        //
        //     k_vB.x = table.k_TABLE_WIDTH - table.k_POCKET_RADIUS;
        //     k_vB.z = table.k_TABLE_HEIGHT;
        //
        //     k_vC.x = table.k_TABLE_WIDTH;
        //     k_vC.z = table.k_TABLE_HEIGHT - table.k_POCKET_RADIUS;
        //
        //     k_vD.x = k_vA.x - 0.016f;
        //     k_vD.z = k_vA.z + 0.060f;
        //
        //     // Aux points
        //     k_vX = k_vD + Vector3.forward;
        //     k_vW = k_vC;
        //     k_vW.z = 0.0f;
        //
        //     k_vY = k_vB;
        //     k_vY.x += 1.0f;
        //     k_vY.z += 1.0f;
        //
        //     k_vZ = k_vC;
        //     k_vZ.x += 1.0f;
        //     k_vZ.z += 1.0f;
        //
        //     // Normals
        //     k_vA_vD = k_vD - k_vA;
        //     k_vA_vD = k_vA_vD.normalized;
        //     k_vA_vD_normal.x = -k_vA_vD.z;
        //     k_vA_vD_normal.z = k_vA_vD.x;
        //
        //     k_vB_vY = k_vB - k_vY;
        //     k_vB_vY = k_vB_vY.normalized;
        //     k_vB_vY_normal.x = -k_vB_vY.z;
        //     k_vB_vY_normal.z = k_vB_vY.x;
        //
        //     k_vC_vZ_normal = -k_vB_vY_normal;
        //
        //     // Minkowski difference
        //     k_pN = k_vA;
        //     k_pN.z -= table.k_CUSHION_RADIUS;
        //
        //     k_pM = k_vA + k_vA_vD_normal * table.k_CUSHION_RADIUS;
        //     k_pL = k_vD + k_vA_vD_normal * table.k_CUSHION_RADIUS;
        //
        //     k_pK = k_vD;
        //     k_pK.x -= table.k_CUSHION_RADIUS;
        //
        //     k_pO = k_vB;
        //     k_pO.z -= table.k_CUSHION_RADIUS;
        //     k_pP = k_vB + k_vB_vY_normal * table.k_CUSHION_RADIUS;
        //     k_pQ = k_vC + k_vC_vZ_normal * table.k_CUSHION_RADIUS;
        //
        //     k_pR = k_vC;
        //     k_pR.x -= table.k_CUSHION_RADIUS;
        //
        //     k_pT = k_vX;
        //     k_pT.x -= table.k_CUSHION_RADIUS;
        //
        //     k_pS = k_vW;
        //     k_pS.x -= table.k_CUSHION_RADIUS;
        //
        //     k_pU = k_vY + k_vB_vY_normal * table.k_CUSHION_RADIUS;
        //     k_pV = k_vZ + k_vC_vZ_normal * table.k_CUSHION_RADIUS;
        //
        //     k_pS = k_vW;
        //     k_pS.x -= table.k_CUSHION_RADIUS;
        //
        //     // others
        //     pocketInnerRadiusSquared = table.k_INNER_RADIUS * table.k_INNER_RADIUS;
        //     cornerPocket = table.k_vE;
        //     sidePocket = table.k_vF;
        // }

        // Check pocket condition
        void _phy_ball_pockets(int id)
        {
            Vector3 A = ballPositions[id];
            Vector3 absA = new Vector3(Mathf.Abs(A.x), A.y, Mathf.Abs(A.z));

            if ((absA - cornerPocket).sqrMagnitude < pocketInnerRadiusSquared)
            {
                ballVelocities[id] = Vector3.zero;
                ballAngularVelocities[id] = Vector3.zero;
                //table._TriggerPocketBall(id);
                return;
            }

            if ((absA - sidePocket).sqrMagnitude < pocketInnerRadiusSquared)
            {
                ballVelocities[id] = Vector3.zero;
                ballAngularVelocities[id] = Vector3.zero;
                //table._TriggerPocketBall(id);
                return;
            }

            if (absA.z > sidePocket.z)
            {
                ballVelocities[id] = Vector3.zero;
                ballAngularVelocities[id] = Vector3.zero;
                //table._TriggerPocketBall(id);
                return;
            }

            if (absA.z > -absA.x + cornerPocket.x + cornerPocket.z)
            {
                ballVelocities[id] = Vector3.zero;
                ballAngularVelocities[id] = Vector3.zero;
                //table._TriggerPocketBall(id);
                return;
            }
        }

        // // Pocketless table
        // void _phy_ball_table_carom(int id)
        // {
        //     float zz, zx;
        //     Vector3 A = ballPositions[id];
        //
        //     // Setup major regions
        //     zx = Mathf.Sign(A.x);
        //     zz = Mathf.Sign(A.z);
        //
        //     if (A.x * zx > pR.x)
        //     {
        //         ballPositions[id].x = pR.x * zx;
        //         _phy_bounce_cushion(id, Vector3.left * zx);
        //     }
        //
        //     if (A.z * zz > pO.z)
        //     {
        //         ballPositions[id].z = pO.z * zz;
        //         _phy_bounce_cushion(id, Vector3.back * zz);
        //     }
        // }

        void CalculateBallPosition(int id)
        {
            Vector3 A, N, _V, V, a_to_v;
            float dot;

            A = ballPositions[id];

            signPos.x = Mathf.Sign(A.x);
            signPos.z = Mathf.Sign(A.z);

            A = Vector3.Scale(A, signPos);

            if (A.x > vA.x) // Major Regions
            {
                if (A.x > A.z + tableWidthMinusHeight) // Minor B
                {
                    if (A.z < tableHeight - pocketOuterRadius)
                    {
                        // Region H
                        if (A.x > tableWidth - cushionRadius)
                        {
                            // Static resolution
                            A.x = tableWidth - cushionRadius;

                            // Dynamic
                            ApplyCushionBounce(id, Vector3.Scale(vCvWNormal, signPos));
                        }
                    }
                    else
                    {
                        a_to_v = A - vC;

                        if (Vector3.Dot(a_to_v, vBvY) > 0.0f)
                        {
                            // Region I ( VORONI )
                            if (a_to_v.magnitude < cushionRadius)
                            {
                                // Static resolution
                                N = a_to_v.normalized;
                                A = vC + N * cushionRadius;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(N, signPos));
                            }
                        }
                        else
                        {
                            // Region J
                            a_to_v = A - pQ;

                            if (Vector3.Dot(vCvZNormal, a_to_v) < 0.0f)
                            {
                                // Static resolution
                                dot = Vector3.Dot(a_to_v, vBvY);
                                A = pQ + dot * vBvY;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(vCvZNormal, signPos));
                            }
                        }
                    }
                }
                else // Minor A
                {
                    if (A.x < vB.x)
                    {
                        // Region A
                        if (A.z > pN.z)
                        {
                            // Velocity based A->C delegation ( scuffed CCD )
                            a_to_v = A - vA;
                            _V = Vector3.Scale(ballVelocities[id], signPos);
                            V.x = -_V.z;
                            V.y = 0.0f;
                            V.z = _V.x;

                            if (A.z > vA.z)
                            {
                                if (Vector3.Dot(V, a_to_v) > 0.0f)
                                {
                                    // Region C ( Delegated )
                                    a_to_v = A - pL;

                                    // Static resolution
                                    dot = Vector3.Dot(a_to_v, vAvD);
                                    A = pL + dot * vAvD;

                                    // Dynamic
                                    ApplyCushionBounce(id, Vector3.Scale(vAvDNormal, signPos));
                                }
                                else
                                {
                                    // Static resolution
                                    A.z = pN.z;

                                    // Dynamic
                                    ApplyCushionBounce(id, Vector3.Scale(vAvBNormal, signPos));
                                }
                            }
                            else
                            {
                                // Static resolution
                                A.z = pN.z;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(vAvBNormal, signPos));
                            }
                        }
                    }
                    else
                    {
                        a_to_v = A - vB;

                        if (Vector3.Dot(a_to_v, vBvY) > 0.0f)
                        {
                            // Region F ( VERONI )
                            if (a_to_v.magnitude < cushionRadius)
                            {
                                // Static resolution
                                N = a_to_v.normalized;
                                A = vB + N * cushionRadius;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(N, signPos));
                            }
                        }
                        else
                        {
                            // Region G
                            a_to_v = A - pP;

                            if (Vector3.Dot(vBvYNormal, a_to_v) < 0.0f)
                            {
                                // Static resolution
                                dot = Vector3.Dot(a_to_v, vBvY);
                                A = pP + dot * vBvY;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(vBvYNormal, signPos));
                            }
                        }
                    }
                }
            }
            else
            {
                a_to_v = A - vA;

                if (Vector3.Dot(a_to_v, vAvD) > 0.0f)
                {
                    a_to_v = A - vD;

                    if (Vector3.Dot(a_to_v, vAvD) > 0.0f)
                    {
                        if (A.z > pK.z)
                        {
                            // Region E
                            if (A.x > pK.x)
                            {
                                // Static resolution
                                A.x = pK.x;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(vCvWNormal, signPos));
                            }
                        }
                        else
                        {
                            // Region D ( VORONI )
                            if (a_to_v.magnitude < cushionRadius)
                            {
                                // Static resolution
                                N = a_to_v.normalized;
                                A = vD + N * cushionRadius;

                                // Dynamic
                                ApplyCushionBounce(id, Vector3.Scale(N, signPos));
                            }
                        }
                    }
                    else
                    {
                        // Region C
                        a_to_v = A - pL;

                        if (Vector3.Dot(vAvDNormal, a_to_v) < 0.0f)
                        {
                            // Static resolution
                            dot = Vector3.Dot(a_to_v, vAvD);
                            A = pL + dot * vAvD;

                            // Dynamic
                            ApplyCushionBounce(id, Vector3.Scale(vAvDNormal, signPos));
                        }
                    }
                }
                else
                {
                    // Region B ( VORONI )
                    if (a_to_v.magnitude < cushionRadius)
                    {
                        // Static resolution
                        N = a_to_v.normalized;
                        A = vA + N * cushionRadius;

                        // Dynamic
                        ApplyCushionBounce(id, Vector3.Scale(N, signPos));
                    }
                }
            }

            ballPositions[id] = Vector3.Scale(A, signPos);
        }

        bool isIntersectingWithSphere(Vector3 start, Vector3 dir, Vector3 sphere)
        {
            Vector3 nrm = dir.normalized;
            Vector3 h = sphere - start;
            float lf = Vector3.Dot(nrm, h);
            float s = BALL_RADIUS_SQUARED - Vector3.Dot(h, h) + lf * lf;

            if (s < 0.0f) return false;

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

            raySphereOutput = start + nrm * (lf - s);
            return true;
        }

        public void ApplyPhysicsToTable(Vector3 cueForwards, float force)
        {
            Vector3 q = transformSurface.InverseTransformDirection(cueForwards); // direction of cue in surface space
            Vector3 o = balls[0].transform.localPosition; // location of ball in surface

            Vector3 j = -Vector3.ProjectOnPlane(q, transformSurface.up); // project cue direction onto table surface, gives us j
            Vector3 k = transformSurface.up;
            Vector3 i = Vector3.Cross(j, k);

            Plane jkPlane = new Plane(i, o);

            Vector3 Q = raySphereOutput; // point of impact in surface space

            float a = jkPlane.GetDistanceToPoint(Q);
            float b = Q.y - o.y;
            float c = Mathf.Sqrt(Mathf.Pow(BALL_RADIUS, 2) - Mathf.Pow(a, 2) - Mathf.Pow(b, 2));

            float adj = Mathf.Sqrt(Mathf.Pow(q.x, 2) + Mathf.Pow(q.z, 2));
            float opp = q.y;
            float theta = -Mathf.Atan(opp / adj);

            float cosTheta = Mathf.Cos(theta);
            float sinTheta = Mathf.Sin(theta);

            float k_CUE_MASS = 0.5f; // kg
            float F = 2 * MASS_OF_BALL * force / (1 + MASS_OF_BALL / k_CUE_MASS + 5 / (2 * BALL_RADIUS) * (Mathf.Pow(a, 2) + Mathf.Pow(b, 2) * Mathf.Pow(cosTheta, 2) + Mathf.Pow(c, 2) * Mathf.Pow(sinTheta, 2) - 2 * b * c * cosTheta * sinTheta));

            float I = 2f / 5f * MASS_OF_BALL * Mathf.Pow(BALL_RADIUS, 2);
            Vector3 v = new Vector3(0, -F / MASS_OF_BALL * cosTheta, -F / MASS_OF_BALL * sinTheta);
            Vector3 w = 1 / I * new Vector3(-c * F * sinTheta + b * F * cosTheta, a * F * sinTheta, -a * F * cosTheta);

            // the paper is inconsistent here. either w.x is inverted (i.e. the i axis points right instead of left) or b is inverted (which means F is wrong too)
            // for my sanity I'm going to assume the former
            w.x = -w.x;

            float m_e = 0.02f;

            // https://billiards.colostate.edu/physics_articles/Alciatore_pool_physics_article.pdf
            float alpha = -Mathf.Atan(
               (5f / 2f * a / BALL_RADIUS * Mathf.Sqrt(1f - Mathf.Pow(a / BALL_RADIUS, 2))) /
               (1 + MASS_OF_BALL / m_e + 5f / 2f * (1f - Mathf.Pow(a / BALL_RADIUS, 2)))
            ) * 180 / Mathf.PI;

            // rewrite to the axis we expect
            v = new Vector3(-v.x, v.z, -v.y);
            w = new Vector3(w.x, -w.z, w.y);

            Vector3 preJumpV = v;
            if (v.y > 0)
            {
                // no scooping
                v.y = 0;
            }
            else if (v.y < 0)
            {
                // the ball must not be under the cue after one time step
                float k_MIN_HORIZONTAL_VEL = (BALL_RADIUS - c) / TIME_PER_STEP;
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
            v = Quaternion.AngleAxis(alpha, transformSurface.up) * v;

            // done
            ballVelocities[0] = v;
            ballAngularVelocities[0] = w;
        }
    }
}
