using UnityEngine;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// Debug code.
    /// </summary>
    public partial class PoolStateManager { 
        public bool showEditorDebugBoundaries;
        public bool showEditorDebugCarom;
        public bool showEditorDebug8ball;
        public bool showEditorDebug9Ball;
        public bool showEditorDebugThreeCushionCarom;
        
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
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(tableWidth * 2, 0, tableHeight * 2));
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(tableWidth * 2 - margin, 0, tableHeight * 2 - margin));

                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(cornerPocket, pocketInnerRadius);
                Gizmos.DrawWireSphere(middlePocket, pocketInnerRadius);
                Gizmos.DrawWireSphere(-cornerPocket, pocketInnerRadius);
                Gizmos.DrawWireSphere(-middlePocket, pocketInnerRadius);
                Gizmos.DrawWireSphere(new Vector3(-cornerPocket.x, 0, cornerPocket.z), pocketInnerRadius);
                Gizmos.DrawWireSphere(new Vector3(cornerPocket.x, 0, -cornerPocket.z), pocketInnerRadius);

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(cornerPocket, pocketOuterRadius);
                Gizmos.DrawWireSphere(middlePocket, pocketOuterRadius);
                Gizmos.DrawWireSphere(-cornerPocket, pocketOuterRadius);
                Gizmos.DrawWireSphere(-middlePocket, pocketOuterRadius);
                Gizmos.DrawWireSphere(new Vector3(-cornerPocket.x, 0, cornerPocket.z), pocketOuterRadius);
                Gizmos.DrawWireSphere(new Vector3(cornerPocket.x, 0, -cornerPocket.z), pocketOuterRadius);

                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(vA, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(vB, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(vC, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(vD, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(vX, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(vY, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(vZ, Vector3.one * 0.01f);

                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(pK, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pL, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pN, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pO, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pP, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pQ, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pR, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pS, Vector3.one * 0.01f);
                Gizmos.DrawWireCube(pT, Vector3.one * 0.01f);
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

            if (showEditorDebugThreeCushionCarom)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(new Vector3(SPOT_POSITION_X, 0, 0), BALL_PL_X);
                Gizmos.DrawWireSphere(new Vector3(SPOT_POSITION_X, 0f, 0.1825f), BALL_PL_X);
                Gizmos.DrawWireSphere(new Vector3(-SPOT_POSITION_X, 0, 0), BALL_PL_X);
            }
#endif
        }
    }
}

