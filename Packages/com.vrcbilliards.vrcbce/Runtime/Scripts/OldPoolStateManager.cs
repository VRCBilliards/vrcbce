using UdonSharp;
using UnityEngine;
// ReSharper disable InconsistentNaming

// ReSharper disable CheckNamespace

namespace VRCBilliards
{
    /// <summary>
    /// Main Behaviour for VRCBilliards: Community Edition. This is a giant class. Here be dragons.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class OldPoolStateManager : BasePoolStateManager
    {
        #region Constants

        /*
         * Constants
         */

        /// <summary>
        /// Maximum steps/frame (8). Note: for Android this was originally designed to be replaced with a value of 0.075.
        /// </summary>
        private const float MAX_DELTA = 0.1f;

        // Physics calculation constants (measurements are in meters)

        /// <summary>
        /// time step in seconds per iteration
        /// </summary>
        private const float FIXED_TIME_STEP = 0.0125f;

        /// <summary>
        /// 1 over ball radius
        /// </summary>
        private const float BALL_1_OR = 33.3333333333f;

        /// <summary>
        /// ball radius squared
        /// </summary>
        private const float BALL_RADIUS_SQUARED = 0.0009f;

        /// <summary>
        /// ball diameter squared
        /// </summary>
        private const float BALL_DSQR = 0.0036f;

        /// <summary>
        /// ball diameter squared plus epsilon
        /// </summary>
        private const float BALL_DIAMETER_SQUARED_PLUS_EPSILON = 0.003598f;

        /// <summary>
        /// Full diameter of pockets (exc ball radi)
        /// </summary>
        private const float POCKET_RADIUS = 0.09f;

        /// <summary>
        /// 1 over root 2 (normalize +-1,+-1 vector)
        /// </summary>
        private const float ONE_OVER_ROOT_TWO = 0.70710678118f;

        /// <summary>
        /// 1 over root 5 (normalize +-1,+-2 vector)
        /// </summary>
        private const float ONE_OVER_ROOT_FIVE = 0.4472135955f;

        /// <summary>
        /// How far back (roughly) do pockets absorb balls after this point
        /// </summary>
        private const float POCKET_DEPTH = 0.04f;

        /// <summary>
        /// Friction coefficient of sliding
        /// </summary>
        private const float F_SLIDE = 0.2f;

        /// <summary>
        /// Vectors cannot be const.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private Vector3 CONTACT_POINT = new Vector3(0.0f, -0.03f, 0.0f);

        private const float SIN_A = 0.28078832987f;
        private const float COS_A = 0.95976971915f;
        private const float F = 1.72909790282f;

        private const float DEFAULT_FORCE_MULTIPLIER = 1.5f;

        private Vector3 vectorZero = Vector3.zero;

        /// <summary>
        /// Tracker variable to see if balls are still on the go
        /// </summary>
        private bool ballsMoving;

        #endregion

        [Tooltip("If enabled, worldspace table scales beyond 1 in x or z will increase the force of hits to compensate, making it easier for regular-sized avatars to play.")]
        public bool scaleHitForceWithScaleBeyond1;

        [Tooltip("This value scales the clamp applied to the velocity of pocketted balls. Raising this will make pockets look less artificial at the cost of increasing the chance high-velocity balls will fly out of the table.")]
        public float pocketVelocityClamp = 1.0f;

        /// <summary>
        /// A value intended to accomodate for resized tables, to make them possible to use without using an avatar with
        /// an equivalent height.
        /// </summary>
        private float forceMultiplier;

        private float accumulation;

        public override void Start()
        {
            base.Start();

            Vector3 scale = baseObject.transform.lossyScale;

            if (scaleHitForceWithScaleBeyond1 && scale.x > 1f || scale.z > 1f)
            {
                float scaler = scale.x > scale.z ? scale.x : scale.z;
                forceMultiplier = DEFAULT_FORCE_MULTIPLIER * scaler;
            }
            else
            {
                forceMultiplier = DEFAULT_FORCE_MULTIPLIER;
            }
        }

        public override void Update()
        {
            base.Update();

            // Run sim only if things are moving
            if (gameIsSimulating)
            {
                accumulation += Time.deltaTime;

                if (accumulation > MAX_DELTA)
                {
                    accumulation = MAX_DELTA;
                }

                while (accumulation >= FIXED_TIME_STEP)
                {
                    AdvancePhysicsStep();
                    accumulation -= FIXED_TIME_STEP;
                }
            }
        }

        // Update loop-scoped handler for cue-locked functionality (warming up and hitting the ball). Non-pure. Returns a Vector3 as it can modify the exact position the cue tip is at.
        protected override Vector3 AimAndHitCueBall(Vector3 copyOfLocalSpacePositionOfCueTip, Vector3 cueballPosition)
        {
            float sweepTimeBall = Vector3.Dot(cueballPosition - localSpacePositionOfCueTipLastFrame,
                cueLocalForwardDirection);

            // Check for potential skips due to low frame rate
            if (sweepTimeBall > 0.0f && sweepTimeBall <
                (localSpacePositionOfCueTipLastFrame - copyOfLocalSpacePositionOfCueTip).magnitude)
            {
                copyOfLocalSpacePositionOfCueTip = localSpacePositionOfCueTipLastFrame +
                                                   (cueLocalForwardDirection * sweepTimeBall);
            }

            // Hit condition is when cuetip is gone inside ball
            if ((copyOfLocalSpacePositionOfCueTip - cueballPosition).sqrMagnitude < BALL_RADIUS_SQUARED)
            {
                Vector3 horizontalForce =
                    copyOfLocalSpacePositionOfCueTip - localSpacePositionOfCueTipLastFrame;
                horizontalForce.y = 0.0f;

                HitBallWithCue(cueArmedShotDirection, forceMultiplier * (horizontalForce.magnitude / Time.deltaTime));
            }

            return copyOfLocalSpacePositionOfCueTip;
        }

        protected override void HitBallWithCue(Vector3 shotDirection, float velocity)
        {
            // Clamp velocity input to 20 m/s ( moderate break speed )
            currentBallVelocities[0] = shotDirection * Mathf.Min(velocity, 20.0f);
            currentAngularVelocities[0] = Vector3.Cross((raySphereOutput - currentBallPositions[0]) * BALL_1_OR, cueLocalForwardDirection * velocity) * -50.0f;

            HandleCueBallHit();
        }
        
                // A correction function that tries to mitigate for discrete physics steps with a low framerate.
        // It has limited success, but is better than nothing.
        private bool WillCueBallCollide()
        {
            // Get what will be the next position
            Vector3 originalDelta = currentBallVelocities[0] * FIXED_TIME_STEP;
            Vector3 norm = currentBallVelocities[0].normalized;

            Vector3 ballDelta;
            float collisionDistance, s, nmag;

            // Closest found values
            float minCollisionDistance = 9999999.0f;
            bool collided = false;
            float mins = 0;

            // Loop balls look for collisions
            for (int i = 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                if (ballsArePocketed[i])
                {
                    continue;
                }

                ballDelta = currentBallPositions[i] - currentBallPositions[0];
                collisionDistance = Vector3.Dot(norm, ballDelta);
                s = BALL_DIAMETER_SQUARED_PLUS_EPSILON - Vector3.Dot(ballDelta, ballDelta) + (collisionDistance * collisionDistance);

                if (s < 0.0f)
                {
                    continue;
                }

                if (collisionDistance >= minCollisionDistance)
                {
                    continue;
                }

                minCollisionDistance = collisionDistance;
                mins = s;

                collided = true;
            }

            if (collided)
            {
                nmag = minCollisionDistance - Mathf.Sqrt(mins);

                // Assign new position if got appropriate magnitude
                if (nmag * nmag < originalDelta.sqrMagnitude)
                {
                    currentBallPositions[0] += norm * nmag;
                    return true;
                }
            }

            return false;
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
                    if ((currentBallPositions[0] - currentBallPositions[i]).sqrMagnitude < BALL_DSQR)
                    {
                        return true;
                    }
                }
            }
            else // 9 ball
            {
                // Only check to 9 ball
                for (int i = 1; i <= 9; i++)
                {
                    if ((currentBallPositions[0] - currentBallPositions[i]).sqrMagnitude < BALL_DSQR)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// CUE ACTIONS
        /// <summary>
        /// Player is holding input trigger
        /// </summary>

        /// <summary>
        /// Player stopped holding input trigger
        /// </summary>


        /// <summary>
        /// Player was moving cueball, place it down
        /// </summary>
       

        /// <summary>
        /// Completely reset state
        /// </summary>
        

        

        /// <summary>
        /// Cue put down local
        /// </summary>

        private void AdvancePhysicsStep()
        {
            ballsMoving = false;

            // Cue angular velocity
            if (!ballsArePocketed[0]) // If cueball is not sunk
            {
                if (!WillCueBallCollide())
                {
                    // Apply movement
                    currentBallPositions[0] += currentBallVelocities[0] * FIXED_TIME_STEP;
                }

                AdvanceSimulationForBall(0);
            }

            // Run main simulation / inter-ball collision
            for (int i = 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                if (!ballsArePocketed[i]) // If the ball in question is not sunk
                {
                    currentBallPositions[i] += currentBallVelocities[i] * FIXED_TIME_STEP;

                    AdvanceSimulationForBall(i);
                }
            }

            // Check if simulation has settled
            if (!ballsMoving && gameIsSimulating)
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

            if (isFourBall)
            {
                BounceBallOffCushionIfApplicable(0);
                BounceBallOffCushionIfApplicable(2);
                BounceBallOffCushionIfApplicable(3);
                BounceBallOffCushionIfApplicable(9);

                return;
            }

            // Run edge collision
            for (int index = 0; index < NUMBER_OF_SIMULATED_BALLS; index++)
            {
                if (ballsArePocketed[index])
                {
                    continue;
                }

                float signZ, signX, zk, zw, d, k, i, j, l, r;
                Vector3 currentBallPosition, N;

                currentBallPosition = currentBallPositions[index];

                // REGIONS
                /*
                    *  QUADS:							SUBSECTION:				SUBSECTION:
                    *    zx, zy:							zz:						zw:
                    *
                    *  o----o----o  +:  1			\_________/				\_________/
                    *  | -+ | ++ |  -: -1		     |	    /		              /  /
                    *  |----+----|					  -  |  +   |		      -     /   |
                    *  | -- | +- |						  |	   |		          /  +  |
                    *  o----o----o						  |      |             /       |
                    *
                    */

                // Setup major regions
                signX = Mathf.Sign(currentBallPosition.x);
                signZ = Mathf.Sign(currentBallPosition.z);

                // within pocket regions
                if ((currentBallPosition.z * signZ > (TABLE_HEIGHT - POCKET_RADIUS)) && (currentBallPosition.x * signX > (TABLE_WIDTH - POCKET_RADIUS) || currentBallPosition.x * signX < POCKET_RADIUS))
                {
                    // Subregions
                    zw = currentBallPosition.z * signZ > (currentBallPosition.x * signX) - TABLE_WIDTH + TABLE_HEIGHT ? 1.0f : -1.0f;

                    // Normalization / line coefficients change depending on sub-region
                    if (currentBallPosition.x * signX > TABLE_WIDTH * 0.5f)
                    {
                        zk = 1.0f;
                        r = ONE_OVER_ROOT_TWO;
                    }
                    else
                    {
                        zk = -2.0f;
                        r = ONE_OVER_ROOT_FIVE;
                    }

                    // Collider line EQ
                    d = signX * signZ * zk; // Coefficient
                    k = (-(TABLE_WIDTH * Mathf.Max(zk, 0.0f)) + (POCKET_RADIUS * zw * Mathf.Abs(zk)) +
                         TABLE_HEIGHT) * signZ; // Constant

                    // Check if colliding
                    l = zw * signZ;
                    if (currentBallPosition.z * l > ((currentBallPosition.x * d) + k) * l)
                    {
                        // Get line normal
                        N.x = signX * zk;
                        N.z = -signZ;
                        N.y = 0.0f;
                        N *= zw * r;

                        // New position
                        i = ((currentBallPosition.x * d) + currentBallPosition.z - k) / (2.0f * d);
                        j = (i * d) + k;

                        currentBallPositions[index].x = i;
                        currentBallPositions[index].z = j;

                        // Reflect velocity
                        ApplyBounceCushion(index, N);
                    }
                }
                else // edges
                {
                    if (currentBallPosition.x * signX > TABLE_WIDTH)
                    {
                        currentBallPositions[index].x = TABLE_WIDTH * signX;
                        ApplyBounceCushion(index, Vector3.left * signX);
                    }

                    if (currentBallPosition.z * signZ > TABLE_HEIGHT)
                    {
                        currentBallPositions[index].z = TABLE_HEIGHT * signZ;
                        ApplyBounceCushion(index, Vector3.back * signZ);
                    }
                }
            }

            // Run triggers
            for (int i = 0; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                if (!ballsArePocketed[i])
                {
                    float zz, zx;
                    Vector3 A = currentBallPositions[i];

                    // Setup major regions
                    zx = Mathf.Sign(A.x);
                    zz = Mathf.Sign(A.z);

                    // It's in a pocket
                    if (
                        A.z * zz > TABLE_HEIGHT + POCKET_DEPTH ||
                        A.z * zz > (A.x * -zx) + TABLE_WIDTH + TABLE_HEIGHT + POCKET_DEPTH
                    )
                    {
                        int total = 0;

                        // Get total for X positioning
                        for (int j = 1; j < (isNineBall ? 10 : NUMBER_OF_SIMULATED_BALLS); j++)
                        {
                            total += ballsArePocketed[j] ? 1 : 0;
                        }

                        // This is where we actually save the pocketed/non-pocketed state of balls.
                        ballsArePocketed[i] = true;

                        bool success = false;

                        if (i == 0)
                        {
                            // do nothing
                        }
                        else if (isOpen && i > 1)
                        {
                            success = true;
                        } // it is blue's turn
                        else if ((isTeam2Turn && isTeam2Blue || !isTeam2Turn && !isTeam2Blue) && i > 1 && i < 9)
                        {
                            success = true;
                        }
                        // it is orange's turn
                        else if (i >= 9)
                        {
                            success = true;
                        }

                        HandleBallSunk(success);

                        // VFX ( make ball move )
                        Rigidbody body = ballTransforms[i].GetComponent<Rigidbody>();
                        body.isKinematic = false;

                        body.velocity = baseObject.transform.rotation * new Vector3(
                            Mathf.Clamp(currentBallVelocities[i].x, (pocketVelocityClamp * -1), pocketVelocityClamp),
                            0.0f,
                            Mathf.Clamp(currentBallVelocities[i].z, (pocketVelocityClamp * -1), pocketVelocityClamp)
                        );

                        if (i != 0)
                        {
                            // set this for later
                            currentBallPositions[i].x = -0.9847f + (total * BALL_DIAMETER);
                            currentBallPositions[i].z = 0.768f;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Apply cushion bounce
        /// </summary>
        /// <param name="id"></param>
        /// <param name="N"></param>
        private void ApplyBounceCushion(int id, Vector3 N)
        {
            // Mathematical expressions derived from: https://billiards.colostate.edu/physics_articles/Mathavan_IMechE_2010.pdf
            //
            // (Note): subscript gamma, u, are used in replacement of Y and Z in these expressions because
            // unicode does not have them.
            //
            // f = 2/7
            // f₁ = 5/7
            //
            // Velocity delta:
            //   Δvₓ = −vₓ∙( f∙sin²θ + (1+e)∙cos²θ ) − Rωᵤ∙sinθ
            //   Δvᵧ = 0
            //   Δvᵤ = f₁∙vᵤ + fR( ωₓ∙sinθ - ωᵧ∙cosθ ) - vᵤ
            //
            // Aux:
            //   Sₓ = vₓ∙sinθ - vᵧ∙cosθ+ωᵤ
            //   Sᵧ = 0
            //   Sᵤ = -vᵤ - ωᵧ∙cosθ + ωₓ∙cosθ
            //
            //   k = (5∙Sᵤ) / ( 2∙mRA );
            //   c = vₓ∙cosθ - vᵧ∙cosθ
            //
            // Angular delta:
            //   ωₓ = k∙sinθ
            //   ωᵧ = k∙cosθ
            //   ωᵤ = (5/(2m))∙(-Sₓ / A + ((sinθ∙c∙(e+1)) / B)∙(cosθ - sinθ));
            //
            // These expressions are in the reference frame of the cushion, so V and ω inputs need to be rotated

            // Reject bounce if velocity is going the same way as normal
            // this state means we tunneled, but it happens only on the corner
            // vertexes
            Vector3 source_v = currentBallVelocities[id];
            if (Vector3.Dot(source_v, N) > 0.0f)
            {
                return;
            }

            // Rotate V, W to be in the reference frame of cushion
            Quaternion rq = Quaternion.AngleAxis(Mathf.Atan2(-N.z, -N.x) * Mathf.Rad2Deg, Vector3.up);
            Quaternion rb = Quaternion.Inverse(rq);
            Vector3 V = rq * source_v;
            Vector3 W = rq * currentAngularVelocities[id];

            Vector3 V1;
            Vector3 W1;
            float k, c, s_x, s_z;

            //V1.x = -V.x * ((2.0f/7.0f) * SINA2 + EP1 * COSA2) - (2.0f/7.0f) * BALL_PL_X * W.z * SINA;
            //V1.z = (5.0f/7.0f)*V.z + (2.0f/7.0f) * BALL_PL_X * (W.x * SINA - W.y * COSA) - V.z;
            //V1.y = 0.0f;
            // (baked):
            V1.x = (-V.x * F) - (0.00240675711f * W.z);
            V1.z = (0.71428571428f * V.z) + (0.00857142857f * ((W.x * SIN_A) - (W.y * COS_A))) - V.z;
            V1.y = 0.0f;

            // s_x = V.x * SINA - V.y * COSA + W.z;
            // (baked): y component not used:
            s_x = (V.x * SIN_A) + W.z;
            s_z = -V.z - (W.y * COS_A) + (W.x * SIN_A);

            // k = (5.0f * s_z) / ( 2 * BALL_MASS * A );
            // (baked):
            k = s_z * 0.71428571428f;

            // c = V.x * COSA - V.y * COSA;
            // (baked): y component not used
            c = V.x * COS_A;

            W1.x = k * SIN_A;

            //W1.z = (5.0f / (2.0f * BALL_MASS)) * (-s_x / A + ((SINA * c * EP1) / B) * (COSA - SINA));
            // (baked):
            W1.z = 15.625f * ((-s_x * 0.04571428571f) + (c * 0.0546021744f));
            W1.y = k * COS_A;

            // Unrotate result
            currentBallVelocities[id] += rb * V1;
            currentAngularVelocities[id] += rb * W1;
        }

        /// <summary>
        /// Pocketless table
        /// </summary>
        /// <param name="id"></param>
        private void BounceBallOffCushionIfApplicable(int id)
        {
            float zz, zx;
            Vector3 A = currentBallPositions[id];

            // Setup major regions
            zx = Mathf.Sign(A.x);
            zz = Mathf.Sign(A.z);

            if (A.x * zx > TABLE_WIDTH)
            {
                currentBallPositions[id].x = TABLE_WIDTH * zx;
                ApplyBounceCushion(id, Vector3.left * zx);
            }

            if (A.z * zz > TABLE_HEIGHT)
            {
                currentBallPositions[id].z = TABLE_HEIGHT * zz;
                ApplyBounceCushion(id, Vector3.back * zz);
            }
        }

        /// <summary>
        /// Advance simulation 1 step for ball id
        /// </summary>
        /// <param name="ballID"></param>
        private void AdvanceSimulationForBall(int ballID)
        {
            Vector3 V = currentBallVelocities[ballID];
            Vector3 W = currentAngularVelocities[ballID];

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

            // Relative contact velocity of ball and table
            Vector3 cv = V + Vector3.Cross(CONTACT_POINT, W);

            // Rolling is achieved when cv's length is approaching 0
            // The epsilon is quite high here because of the fairly large timestep we are working with
            if (cv.magnitude <= 0.1f)
            {
                //V += -F_ROLL * GRAVITY * FIXED_TIME_STEP * V.normalized;
                // (baked):
                V += -0.00122583125f * V.normalized;

                // Calculate rolling angular velocity
                W.x = -V.z * BALL_1_OR;

                if (0.3f > Mathf.Abs(W.y))
                {
                    W.y = 0.0f;
                }
                else
                {
                    W.y -= Mathf.Sign(W.y) * 0.3f;
                }

                W.z = V.x * BALL_1_OR;

                // Stopping scenario
                if (V.magnitude < 0.01f && W.magnitude < 0.2f)
                {
                    W = vectorZero;
                    V = vectorZero;
                }
                else
                {
                    ballsMoving = true;
                }
            }
            else // Slipping
            {
                Vector3 nv = cv.normalized;

                // Angular slipping friction
                //W += ((-5.0f * F_SLIDE * 9.8f)/(2.0f * 0.03f)) * FIXED_TIME_STEP * Vector3.Cross( Vector3.up, nv );
                // (baked):
                W += -2.04305208f * Vector3.Cross(Vector3.up, nv);
                V += -F_SLIDE * 9.8f * FIXED_TIME_STEP * nv;

                ballsMoving = true;
            }

            currentAngularVelocities[ballID] = W;
            currentBallVelocities[ballID] = V;

            // FSP [22/03/21]: Use the base object's rotation as a factor in the axis. This stops the balls spinning incorrectly.
            ballTransforms[ballID].Rotate((baseObject.transform.rotation * W).normalized,
                W.magnitude * FIXED_TIME_STEP * -Mathf.Rad2Deg, Space.World);

            // ball/ball collisions
            for (int i = ballID + 1; i < NUMBER_OF_SIMULATED_BALLS; i++)
            {
                // If the ball has been pocketed it cannot be collided with.
                if (ballsArePocketed[i])
                {
                    continue;
                }

                Vector3 delta = currentBallPositions[i] - currentBallPositions[ballID];
                float dist = delta.magnitude;

                if (dist >= BALL_DIAMETER)
                {
                    continue;
                }

                Vector3 normal = delta / dist;

                Vector3 velocityDelta = currentBallVelocities[ballID] - currentBallVelocities[i];

                float dot = Vector3.Dot(velocityDelta, normal);

                if (dot > 0.0f)
                {
                    Vector3 reflection = normal * dot;
                    currentBallVelocities[ballID] -= reflection;
                    currentBallVelocities[i] += reflection;
                    
                    HandleBallCollision(ballID, i, reflection);
                }
            }
        }
    }
}