using UdonSharp;
using UnityEngine;
// ReSharper disable InconsistentNaming

// ReSharper disable CheckNamespace

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// The physics code for VRCBilliards: Community Edition. This is a giant class. Here be dragons.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PoolStateManager : BasePoolStateManager
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
        public float EARTH_GRAVITY = 9.80665f;             // Earth's gravitational acceleration
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

        [SerializeField] private bool showEditorDebugBoundaries;
        [SerializeField] private bool showEditorDebugCarom;
        [SerializeField] private bool showEditorDebug8ball;
        [SerializeField] private bool showEditorDebug9Ball;
        
        public void OnDrawGizmos()
        {
#if UNITY_EDITOR
            var margin = (ballRadius * 2);
            
            CalculateTableCollisionConstants();

            Gizmos.matrix = transform.localToWorldMatrix;
            
            if (showEditorDebugBoundaries)
            {
                // The bounds of table collision, minus any further geometry.
                // Keep in mind that collision is calculated from the centre of balls.
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(TABLE_WIDTH*2, 0, TABLE_HEIGHT*2));
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(TABLE_WIDTH*2 - margin, 0, TABLE_HEIGHT*2 - margin));
                
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(CORNER_POCKET, POCKET_INNER_RADIUS);
                Gizmos.DrawWireSphere(MIDDLE_POCKET, POCKET_INNER_RADIUS);
                Gizmos.DrawWireSphere(-CORNER_POCKET, POCKET_INNER_RADIUS);
                Gizmos.DrawWireSphere(-MIDDLE_POCKET, POCKET_INNER_RADIUS);
                Gizmos.DrawWireSphere(new Vector3(-CORNER_POCKET.x, 0, CORNER_POCKET.z), POCKET_INNER_RADIUS);
                Gizmos.DrawWireSphere(new Vector3(CORNER_POCKET.x, 0, -CORNER_POCKET.z), POCKET_INNER_RADIUS);
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(CORNER_POCKET, POCKET_RADIUS);
                Gizmos.DrawWireSphere(MIDDLE_POCKET, POCKET_RADIUS);
                Gizmos.DrawWireSphere(-CORNER_POCKET, POCKET_RADIUS);
                Gizmos.DrawWireSphere(-MIDDLE_POCKET, POCKET_RADIUS);
                Gizmos.DrawWireSphere(new Vector3(-CORNER_POCKET.x, 0, CORNER_POCKET.z), POCKET_RADIUS);
                Gizmos.DrawWireSphere(new Vector3(CORNER_POCKET.x, 0, -CORNER_POCKET.z), POCKET_RADIUS);
                
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(vA, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(vB, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(vC, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(vD, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(vX, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(vY, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(vZ, Vector3.one * 0.01f);
                
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(pK,  Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pL,  Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pN,  Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pO,  Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pP,  Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pQ,  Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pR,  Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pS,  Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pT,  Vector3.one * 0.01f);   
            }

            if (showEditorDebugCarom)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(new Vector3(SPOT_POSITION_X, 0, 0), BALL_PL_X);
                Gizmos.DrawWireSphere(new Vector3(SPOT_CAROM_X, 0, 0), BALL_PL_X);
                Gizmos.DrawWireSphere(new Vector3(-SPOT_POSITION_X, 0, 0), BALL_PL_X);
                Gizmos.DrawWireSphere(new Vector3(-SPOT_CAROM_X, 0, 0), BALL_PL_X);
            }

            if (showEditorDebug8ball)
            {
                // break position
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(new Vector3(-SPOT_POSITION_X, 0, 0), BALL_PL_X);
                
                // ball positions
                Gizmos.color = Color.green;
                for (int i = 0, k = 0; i < 5; i++)
                {
                    for (int j = 0; j <= i; j++)
                    {
                        Gizmos.DrawWireSphere(new Vector3
                        (
                            SPOT_POSITION_X + (i * BALL_PL_Y) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F),
                            0.0f,
                            ((-i + (j * 2)) * BALL_PL_X) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F)
                        ), BALL_PL_X);
                    }
                }
            }

            if (showEditorDebug9Ball)
            {
                // break position
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(new Vector3(-SPOT_POSITION_X, 0, 0), BALL_PL_X);
                
                // ball positions
                Gizmos.color = Color.green;
                for (int i = 0, k = 0; i < 5; i++)
                {
                    int rown = breakRows9ball[i];
                    for (int j = 0; j <= rown; j++)
                    {
                        Gizmos.DrawWireSphere(new Vector3
                        (
                            SPOT_POSITION_X + (i * BALL_PL_Y) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F),
                            0.0f,
                            ((-rown + (j * 2)) * BALL_PL_X) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F)
                        ), BALL_PL_X);
                    }
                }
            }
#endif
        }
        
        public override void Start()
        {
            base.Start();

            CalculateTableCollisionConstants();
        }

        private void CalculateTableCollisionConstants()
        {
            BALL_DIAMETER_SQUARED_MINUS_EPSILON = Mathf.Pow(BALL_DIAMETER, 2) - 0.0002f;
            ballRadius = BALL_DIAMETER / 2;
            ONE_OVER_BALL_RADIUS = 1 / (BALL_DIAMETER/2);
            BALL_DIAMETER_SQUARED = Mathf.Pow(BALL_DIAMETER, 2);
            BALL_RADIUS_SQUARED = Mathf.Pow(BALL_DIAMETER/2, 2);
            
            POCKET_INNER_RADIUS_SQUARED = Mathf.Pow(POCKET_INNER_RADIUS, 2);

            tableWidthMinusHeight = TABLE_WIDTH - TABLE_HEIGHT;
            // Major source vertices
            vA.x = POCKET_RADIUS * 0.92f;
            vA.z = TABLE_HEIGHT;

            vB.x = TABLE_WIDTH - POCKET_RADIUS;
            vB.z = TABLE_HEIGHT;

            vC.x = TABLE_WIDTH;
            vC.z = TABLE_HEIGHT - POCKET_RADIUS;

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

        public override void Update()
        {
            base.Update();

            // Run sim only if things are moving
            if (gameIsSimulating)
            {
                accumulatedTime += Time.deltaTime;

                if (accumulatedTime > MAX_SIMULATION_TIME_PER_FRAME)
                {
                    accumulatedTime = MAX_SIMULATION_TIME_PER_FRAME;
                }

                while (accumulatedTime >= TIME_PER_STEP)
                {
                    AdvancePhysicsStep();
                    accumulatedTime -= TIME_PER_STEP;
                }
            }
        }

        // Update loop-scoped handler for cue-locked functionality (warming up and hitting the ball). Non-pure. Returns a Vector3 as it can modify the exact position the cue tip is at.
        protected override Vector3 AimAndHitCueBall(Vector3 copyOfLocalSpacePositionOfCueTip, Vector3 cueballPosition)
        {
            float sweepTimeBall = Vector3.Dot(cueballPosition - localSpacePositionOfCueTipLastFrame,
                cueLocalForwardDirection);

            // Check for potential skips due to low frame rate
            if (sweepTimeBall > 0.0f && sweepTimeBall < (localSpacePositionOfCueTipLastFrame - copyOfLocalSpacePositionOfCueTip).magnitude)
            {
                copyOfLocalSpacePositionOfCueTip = localSpacePositionOfCueTipLastFrame + cueLocalForwardDirection * sweepTimeBall;
            }

            // Hit condition is when cuetip is gone inside ball
            if ((copyOfLocalSpacePositionOfCueTip - cueballPosition).sqrMagnitude < BALL_RADIUS_SQUARED)
            {
                Vector3 force = copyOfLocalSpacePositionOfCueTip - localSpacePositionOfCueTipLastFrame;

                HitBallWithCue(cueTip.transform.forward, Mathf.Min(force.magnitude / Time.fixedDeltaTime, 999.0f));
            }

            return copyOfLocalSpacePositionOfCueTip;
        }

        protected override void HitBallWithCue(Vector3 shotDirection, float velocity)
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
            float c = Mathf.Sqrt(Mathf.Max(0,Mathf.Pow(ballRadius, 2) - Mathf.Pow(a, 2) - Mathf.Pow(b, 2)));

            float adj = Mathf.Sqrt(Mathf.Pow(q.x, 2) + Mathf.Pow(q.z, 2));
            float opp = q.y;
            float theta = -Mathf.Atan(opp / adj);

            float cosTheta = Mathf.Cos(theta);
            float sinTheta = Mathf.Sin(theta);

            float k_CUE_MASS = 0.5f; // kg
            float F = 2 * MASS_OF_BALL * velocity / (1 + MASS_OF_BALL / k_CUE_MASS + 5 / (2 * ballRadius) * (Mathf.Pow(a, 2) + Mathf.Pow(b, 2) * Mathf.Pow(cosTheta, 2) + Mathf.Pow(c, 2) * Mathf.Pow(sinTheta, 2) - 2 * b * c * cosTheta * sinTheta));

            float I = 2f / 5f * MASS_OF_BALL * Mathf.Pow(ballRadius, 2);
            Vector3 v = new Vector3(0, -F / MASS_OF_BALL * cosTheta, -F / MASS_OF_BALL * sinTheta);
            Vector3 w = 1 / I * new Vector3(-c * F * sinTheta + b * F * cosTheta, a * F * sinTheta, -a * F * cosTheta);

            // the paper is inconsistent here. either w.x is inverted (i.e. the i axis points right instead of left) or b is inverted (which means F is wrong too)
            // for my sanity I'm going to assume the former
            w.x = -w.x;

            float m_e = 0.02f;

            // https://billiards.colostate.edu/physics_articles/Alciatore_pool_physics_article.pdf
            float alpha = -Mathf.Atan(
               (5f / 2f * a / ballRadius * Mathf.Sqrt(Mathf.Max(0,1f - Mathf.Pow(a / ballRadius, 2)))) /
               (1 + MASS_OF_BALL / m_e + 5f / 2f * (1f - Mathf.Pow(a / ballRadius, 2)))
            ) * 180 / Mathf.PI;

            // rewrite to the axis we expect
            v = new Vector3(-v.x, v.z, -v.y);
            w = new Vector3(w.x, -w.z, w.y);
            
            if (v.y > 0)
            {
                Debug.Log("POOL TABLE JUMPING DEBUG: No scooping - y is " + v.y);
                
                // no scooping
                v.y = 0;
            }
            else if (v.y < 0)
            {
                // the ball must not be under the cue after one time step
                float k_MIN_HORIZONTAL_VEL = (ballRadius - c) / TIME_PER_STEP;
                
                if (v.z < k_MIN_HORIZONTAL_VEL)
                {
                    Debug.Log("POOL TABLE JUMPING DEBUG: Not enough strength to be a jump shot - y is " + v.y);
                    
                    // not enough strength to be a jump shot
                    v.y = 0;
                }
                else
                {
                    Debug.Log("POOL TABLE JUMPING DEBUG: Jump shot triggered! y is " + v.y);
                    
                    // dampen y velocity because the table will eat a lot of energy (we're driving the ball straight into it)
                    v.y = -v.y * 0.35f;
                }
            } else {
                Debug.Log("POOL TABLE JUMPING DEBUG: No jump triggered... y is " + v.y);
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
        //       so it's more acceptable to hold on to.
        protected override bool IsIntersectingWithSphere(Vector3 start, Vector3 dir, Vector3 sphere)
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
        protected override bool IsCueContacting()
        {
            // 8 ball, practice, portal
            if (gameMode != 1u)
            {
                // Check all
                for (int i = 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
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
            }
            else if (isNineBall)
            {
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
            }
            else
            {
                if ((currentBallPositions[0] - currentBallPositions[9]).sqrMagnitude < BALL_DIAMETER_SQUARED)
                {
                    return true;
                }
                if ((currentBallPositions[0] - currentBallPositions[2]).sqrMagnitude < BALL_DIAMETER_SQUARED)
                {
                    return true;
                }
                if ((currentBallPositions[0] - currentBallPositions[3]).sqrMagnitude < BALL_DIAMETER_SQUARED)
                {
                    return true;
                }
            }

            return false;
        }

        private void AdvancePhysicsStep()
        {
            bool ballsMoving = false;

            // Cue angular velocity
            bool[] moved = new bool[NUMBER_OF_SIMULATED_BALLS];

            if (!ballsArePocketed[0])
            {
                if (currentBallPositions[0].y < 0)
                {
                    currentBallPositions[0].y = 0;
                    //currentBallPositions[0].y = -currentBallPositions[0].y * 0.5f; // bounce with restitution
                }
                
                // Apply movement
                Vector3 deltaPos = CalculateCueBallDeltaPosition();
                currentBallPositions[0] += deltaPos;
                moved[0] = deltaPos != Vector3.zero;

                ballsMoving |= StepOneBall(0, moved);
            }

            // Run main simulation / inter-ball collision
            
            for (int i = 1; i < ballsArePocketed.Length; i++)
            {
                if (ballsArePocketed[i])
                {
                    continue;
                }
                
                currentBallVelocities[i].y = 0;
                currentBallPositions[i].y = 0;

                Vector3 deltaPos = currentBallVelocities[i] * TIME_PER_STEP;
                currentBallPositions[i] += deltaPos;
                moved[i] = deltaPos != Vector3.zero;

                ballsMoving |= StepOneBall(i, moved);
            }
            
            // Check if simulation has settled
            if (!ballsMoving && gameIsSimulating && !lockoutEndTurnToAllowCueBallToMove)
            {
                gameIsSimulating = false;

                // Make sure we only run this from the client who initiated the move
                if (isSimulatedByUs)
                {
                    HandleEndOfTurn();
                }

                // Check if there was a network update on hold
                if (isUpdateLocked)
                {
                    isUpdateLocked = false;

                    ReadNetworkData();
                }

                return;
            }

            bool canCueBallBounceOffCushion = currentBallPositions[0].y < ballRadius;
            
            if (isFourBall)
            {
                if (canCueBallBounceOffCushion && moved[0])
                {
                    ApplyCushionCaromBounce(0);
                }
                if (moved[9])
                {
                    ApplyCushionCaromBounce(9);
                }
                if (moved[2])
                {
                    ApplyCushionCaromBounce(2);
                }
                if (moved[3])
                {
                    ApplyCushionCaromBounce(3);
                }
            }
            else
            {
                // Run edge collision
                for (var i = 0; i < ballsArePocketed.Length; i++)
                {
                    if (moved[i] && !ballsArePocketed[i] && (i != 0 || canCueBallBounceOffCushion))
                    {
                        CalculateBallPosition(i);
                    }
                }
           }

            bool outOfBounds = false;
            if (!ballsArePocketed[0])
            {
                if (Mathf.Abs(currentBallPositions[0].x) > TABLE_WIDTH + ballRadius + 0.0001f || Mathf.Abs(currentBallPositions[0].z) > TABLE_HEIGHT + ballRadius+ 0.0001f)
                {
                    HandleCueBallOffTable();
                    outOfBounds = true;
                }
            }

            if (isFourBall)
            {
                return;
            }
            
            if (moved[0] && canCueBallBounceOffCushion && !outOfBounds)
            {
                CheckIfBallsArePocketed(0);
            }

            // Run triggers
            for (int i = 1; i < ballsArePocketed.Length; i++)
            {
                if (moved[i] && !ballsArePocketed[i])
                {
                    CheckIfBallsArePocketed(i);
                }
            }
        }
        
        private Vector3 CalculateCueBallDeltaPosition()
        {
            // Get what will be the next position
            Vector3 originalDelta = currentBallVelocities[0] * TIME_PER_STEP;

            Vector3 norm = currentBallVelocities[0].normalized;

            Vector3 h;
            float lf, s, nmag;

            // Closest found values
            float minlf = float.MaxValue;
            int minid = 0;
            float mins = 0;

            // Loop balls look for collisions
            uint ball_bit = 0x1U;

            // Loop balls look for collisions
            for (var i = 1; i < ballsArePocketed.Length; i++)
            {
                if (ballsArePocketed[i])
                {
                    continue;
                }

                h = currentBallPositions[i] - currentBallPositions[0];
                lf = Vector3.Dot(norm, h);
                if (lf < 0f)
                {
                    continue;
                }

                s = BALL_DIAMETER_SQUARED_MINUS_EPSILON - Vector3.Dot(h, h) + lf * lf;

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
        
        private bool StepOneBall(int id, bool[] moved)
        {
            GameObject ball = ballTransforms[id].gameObject;

            bool isBallMoving = false;

            // no point updating velocity if ball isn't moving
            if (currentBallVelocities[id] != Vector3.zero || currentAngularVelocities[id] != Vector3.zero)
            {
                isBallMoving = updateVelocity(id, ball);
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
                Vector3 res = (BALL_DIAMETER - dist) * normal;
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
        
        private bool updateVelocity(int id, GameObject ball)
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

            ball.transform.Rotate(this.transform.TransformDirection(W.normalized), W.magnitude * TIME_PER_STEP * -Mathf.Rad2Deg, Space.World);

            return ballMoving;
        }
        
        void CalculateBallPosition(int id)
        {
            Vector3 nToVNormalized, _V, V, a_to_v;
            float dot;

            Vector3 ballUnderTest = currentBallPositions[id];

            signPos.x = Mathf.Sign(ballUnderTest.x);
            signPos.z = Mathf.Sign(ballUnderTest.z);

            ballUnderTest = Vector3.Scale(ballUnderTest, signPos);

            if (ballUnderTest.x > vA.x) // Major Regions
            {
                if (ballUnderTest.x > ballUnderTest.z + tableWidthMinusHeight) // Minor B
                {
                    if (ballUnderTest.z < TABLE_HEIGHT - POCKET_RADIUS)
                    {
                        // Region H
                        if (ballUnderTest.x > TABLE_WIDTH - ballRadius)
                        {
                            // Static resolution
                            ballUnderTest.x = TABLE_WIDTH - ballRadius;

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
                            _V = Vector3.Scale(currentBallVelocities[id], signPos);
                            V.x = -_V.z;
                            V.y = 0.0f;
                            V.z = _V.x;

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
        
        void ApplyCushionBounce(int id, Vector3 n)
        {
            // Reject bounce if velocity is going the same way as normal
            // this state means we tunneled, but it happens only on the corner
            // vertexes
            Vector3 source_v = currentBallVelocities[id];
            if (Vector3.Dot(source_v, n) > 0.0f)
            {
                return;
            }

            // Rotate V, W to be in the reference frame of cushion
            Quaternion rq = Quaternion.AngleAxis(Mathf.Atan2(-n.z, -n.x) * Mathf.Rad2Deg, Vector3.up);
            Quaternion rb = Quaternion.Inverse(rq);
            Vector3 V = rq * source_v;
            Vector3 W = rq * currentAngularVelocities[id];

            Vector3 V1;
            Vector3 W1;
            float k, c, s_x, s_z;

            //V1.x = -V.x * ((2.0f/7.0f) * k_SINA2 + k_EP1 * k_COSA2) - (2.0f/7.0f) * k_BALL_PL_X * W.z * k_SINA;
            //V1.z = (5.0f/7.0f)*V.z + (2.0f/7.0f) * k_BALL_PL_X * (W.x * k_SINA - W.y * k_COSA) - V.z;
            //V1.y = 0.0f; 
            // (baked):
            V1.x = -V.x * F - 0.00240675711f * W.z;
            V1.z = 0.71428571428f * V.z + 0.00857142857f * (W.x * SIN_A - W.y * COS_A) - V.z;
            V1.y = 0.0f;

            // s_x = V.x * k_SINA - V.y * k_COSA + W.z;
            // (baked): y component not used:
            s_x = V.x * SIN_A + W.z;
            s_z = -V.z - W.y * COS_A + W.x * SIN_A;

            // k = (5.0f * s_z) / ( 2 * k_BALL_MASS * k_A ); 
            // (baked):
            k = s_z * 0.71428571428f;

            // c = V.x * k_COSA - V.y * k_COSA;
            // (baked): y component not used
            c = V.x * COS_A;

            W1.x = k * SIN_A;

            //W1.z = (5.0f / (2.0f * k_BALL_MASS)) * (-s_x / k_A + ((k_SINA * c * k_EP1) / k_B) * (k_COSA - k_SINA));
            // (baked):
            W1.z = 15.625f * (-s_x * 0.04571428571f + c * 0.0546021744f);
            W1.y = k * COS_A;

            // Unrotate result
            currentBallVelocities[id] += rb * V1;
            currentAngularVelocities[id] += rb * W1;

            if (id == 0) 
                cushionsHitThisTurn++;
        }
        
        void CheckIfBallsArePocketed(int id)
        {
            Vector3 A = currentBallPositions[id];
            Vector3 absA = new Vector3(Mathf.Abs(A.x), A.y, Mathf.Abs(A.z));

            if (
                (absA - CORNER_POCKET).sqrMagnitude < POCKET_INNER_RADIUS_SQUARED ||
                (absA - MIDDLE_POCKET).sqrMagnitude < POCKET_INNER_RADIUS_SQUARED ||
                absA.z > MIDDLE_POCKET.z ||
                absA.z > -absA.x + CORNER_POCKET.x + CORNER_POCKET.z
            )
            {
                TriggerPocketBall(id);
                
                currentBallVelocities[id] = Vector3.zero;
                currentAngularVelocities[id] = Vector3.zero;
            }
        }
        
        void ApplyCushionCaromBounce(int id)
        {
            float zz, zx;
            Vector3 A = currentBallPositions[id];

            // Setup major regions
            zx = Mathf.Sign(A.x);
            zz = Mathf.Sign(A.z);

            if (A.x * zx > pR.x)
            {
                currentBallPositions[id].x = pR.x * zx;
                ApplyCushionBounce(id, Vector3.left * zx);
            }

            if (A.z * zz > pO.z)
            {
                currentBallPositions[id].z = pO.z * zz;
                ApplyCushionBounce(id, Vector3.back * zz);
            }
        }

        private bool lockoutEndTurnToAllowCueBallToMove;

        public void _ReleaseWhiteBall()
        {
            lockoutEndTurnToAllowCueBallToMove = false;

            if (cueBallController)
            {
                cueBallController._DisableDonking();
            }
        }

        private void HandleCueBallOffTable()
        {
            if (isFourBall)
            {
                fourBallCueLeftTable = true;
            }
            else
            {
                ballsArePocketed[0] = true;
            }
            
            HandleFoulEffects();

            lockoutEndTurnToAllowCueBallToMove = true;
            SendCustomEventDelayedSeconds(nameof(_ReleaseWhiteBall), 5f);
            
            // VFX ( make ball move )
            Rigidbody body = ballTransforms[0].gameObject.GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.velocity = transform.TransformVector(new Vector3(
                currentBallVelocities[0].x,
                currentBallVelocities[0].y,
                currentBallVelocities[0].z
            ));

            if (cueBallController)
            {
                cueBallController._EnableDonking();
            }
        }

        private void TriggerPocketBall(int id)
        {
            var total = 0;

            // Get total for X positioning
            foreach (var pocketed in ballsArePocketed)
            {
                if (pocketed)
                {
                    total++;
                }
            }

            // place ball on the rack
            currentBallPositions[id] = Vector3.zero + (float)total * BALL_DIAMETER * Vector3.zero;

            // This is where we actually save the pocketed/non-pocketed state of balls.
            ballsArePocketed[id] = true;

            bool success = false;

            // isOpen is only ever false in blackball - so this covers any ball sunk in 9 ball, which is correct (in
            // our simplified 9-ball, the only way to trigger a foul is to foul the cue ball or not hit the lowest-count
            // ball first when shooting.
            if (isOpen && id > 1)
            {
                success = true;
            } // it is blue's turn
            else if ((isTeam2Turn && isTeam2Blue || !isTeam2Turn && !isTeam2Blue) && id > 1 && id < 9)
            {
                success = true;
            }
            // it is orange's turn
            else if (id >= 9)
            {
                success = true;
            }

            HandleBallSunk(success);

            if (id == 0)
            {
                lockoutEndTurnToAllowCueBallToMove = true;
                SendCustomEventDelayedSeconds(nameof(_ReleaseWhiteBall), 5f);
            }
            
            // VFX ( make ball move )
            Rigidbody body = ballTransforms[id].gameObject.GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.velocity = transform.TransformVector(new Vector3(
                currentBallVelocities[id].x,
                currentBallVelocities[id].y,
                currentBallVelocities[id].z
            ));
        }
    }
}
