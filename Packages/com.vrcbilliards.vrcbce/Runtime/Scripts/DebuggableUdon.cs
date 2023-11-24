using UdonSharp;
using UnityEngine;

// ReSharper disable CheckNamespace

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// Overarching script for UdonSharpBehaviours that need nicer debug tools than Unity normally allows.
    /// </summary>
    public abstract class DebuggableUdon : UdonSharpBehaviour
    {
        /**
         * All of this code is derived from this series: https://dev-tut.com/2022/unity-debug/
         */
        
#region Circle        
        public static void DrawArc(float startAngle, float endAngle, Vector3 position, Quaternion orientation, float radius, Color color, bool drawChord = false, bool drawSector = false, int arcSegments = 32)
        {
            float arcSpan = Mathf.DeltaAngle(startAngle, endAngle);
         
            // Since Mathf.DeltaAngle returns a signed angle of the shortest path between two angles, it 
            // is necessary to offset it by 360.0 degrees to get a positive value
            if (arcSpan <= 0)
            {
                arcSpan += 360.0f;
            }
         
            // angle step is calculated by dividing the arc span by number of approximation segments
            float angleStep = (arcSpan / arcSegments) * Mathf.Deg2Rad;
            float stepOffset = startAngle * Mathf.Deg2Rad;
         
            // stepStart, stepEnd, lineStart and lineEnd variables are declared outside of the following for loop
            float stepStart = 0.0f;
            float stepEnd = 0.0f;
            Vector3 lineStart = Vector3.zero;
            Vector3 lineEnd = Vector3.zero;
         
            // arcStart and arcEnd need to be stored to be able to draw segment chord
            Vector3 arcStart = Vector3.zero;
            Vector3 arcEnd = Vector3.zero;
         
            // arcOrigin represents an origin of a circle which defines the arc
            Vector3 arcOrigin = position;
         
            for (int i = 0; i < arcSegments; i++)
            {
                // Calculate approximation segment start and end, and offset them by start angle
                stepStart = angleStep * i + stepOffset;
                stepEnd = angleStep * (i + 1) + stepOffset;
         
                lineStart.x = Mathf.Cos(stepStart);
                lineStart.y = Mathf.Sin(stepStart);
                lineStart.z = 0.0f;
         
                lineEnd.x = Mathf.Cos(stepEnd);
                lineEnd.y = Mathf.Sin(stepEnd);
                lineEnd.z = 0.0f;
         
                // Results are multiplied so they match the desired radius
                lineStart *= radius;
                lineEnd *= radius;
         
                // Results are multiplied by the orientation quaternion to rotate them 
                // since this operation is not commutative, result needs to be
                // reassigned, instead of using multiplication assignment operator (*=)
                lineStart = orientation * lineStart;
                lineEnd = orientation * lineEnd;
         
                // Results are offset by the desired position/origin 
                lineStart += position;
                lineEnd += position;
         
                // If this is the first iteration, set the chordStart
                if (i == 0)
                {
                    arcStart = lineStart;
                }
         
                // If this is the last iteration, set the chordEnd
                if(i == arcSegments - 1)
                {
                    arcEnd = lineEnd;
                }
         
                Debug.DrawLine(lineStart, lineEnd, color);
            }
         
            if (drawChord)
            {
                Debug.DrawLine(arcStart, arcEnd, color);
            }
            
            if (drawSector)
            {
                Debug.DrawLine(arcStart, arcOrigin, color);
                Debug.DrawLine(arcEnd, arcOrigin, color);
            }
        }

        public static void DrawCircle(Vector3 position, Quaternion orientation, float radius, Color color, int arcSegments)
        {
            DrawArc(0, 360, position, orientation, radius, color, false, false, arcSegments);
        }
#endregion        
#region Triangle
        // Draw a triangle defined by three points
        public static void DrawTriangle(Vector3 pointA, Vector3 pointB, Vector3 pointC, Color color)
        {
            // Connect pointA and pointB
            Debug.DrawLine(pointA, pointB, color);
 
            // Connect pointB and pointC
            Debug.DrawLine(pointB, pointC, color);
 
            // Connect pointC and pointA
            Debug.DrawLine(pointC, pointA, color);
        }
        
        public static void DrawTriangle(Vector3 pointA, Vector3 pointB, Vector3 pointC, Vector3 offset, Quaternion orientation, Color color)
        {
            pointA = offset + orientation * pointA;
            pointB = offset + orientation * pointB;
            pointC = offset + orientation * pointC;
 
            DrawTriangle(pointA, pointB, pointC, color);
        }
        
        public static void DrawTriangle(Vector3 origin, Quaternion orientation, float baseLength, float height, Color color)
        {
            Vector3 pointA = Vector3.right * baseLength * 0.5f;
            Vector3 pointC = Vector3.left * baseLength * 0.5f;
            Vector3 pointB = Vector3.up * height;
 
            DrawTriangle(pointA, pointB, pointC, origin, orientation, color);
        }
        #endregion
#region Rectangle
        public static void DrawQuad(Vector3 pointA, Vector3 pointB, Vector3 pointC, Vector3 pointD, Color color)
        {
            // Draw lines between the points
            Debug.DrawLine(pointA, pointB, color);
            Debug.DrawLine(pointB, pointC, color);
            Debug.DrawLine(pointC, pointD, color);
            Debug.DrawLine(pointD, pointA, color);
        }
        
        public static void DrawRectangle(Vector3 position, Quaternion orientation, Vector2 extent, Color color)
        {
            Vector3 rightOffset = Vector3.right * extent.x * 0.5f;
            Vector3 upOffset = Vector3.up * extent.y * 0.5f;
 
            Vector3 offsetA = orientation * (rightOffset + upOffset);
            Vector3 offsetB = orientation * (-rightOffset + upOffset);
            Vector3 offsetC = orientation * (-rightOffset - upOffset);
            Vector3 offsetD = orientation * (rightOffset - upOffset);
 
            DrawQuad(position + offsetA,
                position + offsetB,
                position + offsetC,
                position + offsetD, 
                color);
        }
        
        // Draw a rectangle defined by two points, origin and orientation
        public static void DrawRectangle(Vector2 point1, Vector2 point2, Vector3 origin, Quaternion orientation, Color color)
        {
            // Calculate extent as a distance between point1 and point2
            float extentX = Mathf.Abs(point1.x - point2.x);
            float extentY = Mathf.Abs(point1.y - point2.y);
 
            // Calculate rotated axes
            Vector3 rotatedRight = orientation * Vector3.right;
            Vector3 rotatedUp = orientation * Vector3.up;
         
            // Calculate each rectangle point
            Vector3 pointA = origin + rotatedRight * point1.x + rotatedUp * point1.y;
            Vector3 pointB = pointA + rotatedRight * extentX;
            Vector3 pointC = pointB + rotatedUp * extentY;
            Vector3 pointD = pointA + rotatedUp * extentY;
 
            DrawQuad(pointA, pointB, pointC, pointD, color);
        }
#endregion
#region Sphere
            public static void DrawSphere(Vector3 position, Quaternion orientation, float radius, Color color, int segments = 4)
            {
                if(segments < 2)
                {
                    segments = 2;
                }
             
                int doubleSegments = segments * 2;
                     
                // Draw meridians
             
                float meridianStep = 180.0f / segments;
             
                for (int i = 0; i < segments; i++)
                {
                    DrawCircle(position, orientation * Quaternion.Euler(0, meridianStep * i, 0), radius, color, doubleSegments);
                }
             
                // Draw parallels
             
                Vector3 verticalOffset = Vector3.zero;
                float parallelAngleStep = Mathf.PI / segments;
                float stepRadius = 0.0f;
                float stepAngle = 0.0f;
             
                for (int i = 1; i < segments; i++)
                {
                    stepAngle = parallelAngleStep * i;
                    verticalOffset = (orientation * Vector3.up) * Mathf.Cos(stepAngle) * radius;
                    stepRadius = Mathf.Sin(stepAngle) * radius;
             
                    DrawCircle(position + verticalOffset, orientation * Quaternion.Euler(90.0f, 0, 0), stepRadius, color, doubleSegments);
                }
            }
#endregion
#region Cuboids
        public static void DrawBox(Vector3 position, Quaternion orientation, Vector3 size, Color color)
        {
            Vector3 offsetX = orientation * Vector3.right * size.x * 0.5f;
            Vector3 offsetY = orientation * Vector3.up * size.y * 0.5f;
            Vector3 offsetZ = orientation * Vector3.forward * size.z * 0.5f;
         
            Vector3 pointA = -offsetX + offsetY;
            Vector3 pointB = offsetX + offsetY;
            Vector3 pointC = offsetX - offsetY;
            Vector3 pointD = -offsetX - offsetY;
         
            DrawRectangle(position - offsetZ, orientation, new Vector2(size.x, size.y), color);
            DrawRectangle(position + offsetZ, orientation, new Vector2(size.x, size.y), color);
         
            Debug.DrawLine(pointA - offsetZ, pointA + offsetZ, color);
            Debug.DrawLine(pointB - offsetZ, pointB + offsetZ, color);
            Debug.DrawLine(pointC - offsetZ, pointC + offsetZ, color);
            Debug.DrawLine(pointD - offsetZ, pointD + offsetZ, color);
        }

        public static void DrawCube(Vector3 position, Quaternion orientation, float size, Color color)
        {
            DrawBox(position, orientation, Vector3.one * size, color);
        }
#endregion        
#region Cylinder
        public static void DrawCylinder(Vector3 position, Quaternion orientation, float height, float radius, Color color, bool drawFromBase = true)
        {
            Vector3 localUp = orientation * Vector3.up;
            Vector3 localRight = orientation * Vector3.right;
            Vector3 localForward = orientation * Vector3.forward;
         
            Vector3 basePositionOffset = drawFromBase ? Vector3.zero : (localUp * height * 0.5f);
            Vector3 basePosition = position - basePositionOffset;
            Vector3 topPosition = basePosition + localUp * height;
                 
            Quaternion circleOrientation = orientation * Quaternion.Euler(90, 0, 0);
         
            Vector3 pointA = basePosition + localRight * radius;
            Vector3 pointB = basePosition + localForward * radius;
            Vector3 pointC = basePosition - localRight * radius;
            Vector3 pointD = basePosition - localForward * radius;
         
            Debug.DrawRay(pointA, localUp * height, color);
            Debug.DrawRay(pointB, localUp * height, color);
            Debug.DrawRay(pointC, localUp * height, color);
            Debug.DrawRay(pointD, localUp * height, color);
         
            DrawCircle(basePosition, circleOrientation, radius, color, 32);
            DrawCircle(topPosition, circleOrientation, radius, color, 32);
        }
#endregion
#region Capsule
        public static void DrawCapsule(Vector3 position, Quaternion orientation, float height, float radius, Color color, bool drawFromBase = true)
        {
            // Clamp the radius to a half of the capsule's height
            radius = Mathf.Clamp(radius, 0, height * 0.5f);
            Vector3 localUp = orientation * Vector3.up;
            Quaternion arcOrientation = orientation * Quaternion.Euler(0, 90, 0);
         
            Vector3 basePositionOffset = drawFromBase ? Vector3.zero : (localUp * height * 0.5f);
            Vector3 baseArcPosition = position + localUp * radius - basePositionOffset;
            DrawArc(180, 360, baseArcPosition, orientation, radius, color);
            DrawArc(180, 360, baseArcPosition, arcOrientation, radius, color);
         
            float cylinderHeight = height - radius * 2.0f;
            DrawCylinder(baseArcPosition, orientation, cylinderHeight, radius, color, true);
         
            Vector3 topArcPosition = baseArcPosition + localUp * cylinderHeight;
                 
            DrawArc(0, 180, topArcPosition, orientation, radius, color);
            DrawArc(0, 180, topArcPosition, arcOrientation, radius, color);
        }
#endregion
        protected void DrawDebugPizza()
        {
            // Draw a slice
            Vector3 sliceOffset = (Vector3.up + Vector3.right) * 0.16f;
            DrawArc(20, 75, transform.position + sliceOffset, transform.rotation, 1.0f, Color.yellow, false, true);
            DrawArc(20, 75, transform.position + sliceOffset, transform.rotation, 0.85f, Color.yellow, false, true);
 
            // Draw the rest of the pie
            DrawArc(75, 20, transform.position, transform.rotation, 1.0f, Color.yellow, false, true);
            DrawArc(75, 20, transform.position, transform.rotation, 0.85f, Color.yellow, false, true);
 
            // Draw full toppings
            DrawCircle(transform.position + new Vector3(-0.4f, 0.2f,0), transform.rotation, 0.22f, Color.red, 16);
            DrawCircle(transform.position + new Vector3(0.5f, -0.35f,0), transform.rotation, 0.17f, Color.red, 16);
            DrawCircle(transform.position + new Vector3(-0.1f, -0.5f,0), transform.rotation, 0.22f, Color.red, 16);
 
            // Draw sliced toppings
            DrawArc(242, 88, transform.position + sliceOffset + new Vector3(0.2f, 0.55f, 0), transform.rotation, 0.20f, Color.red, true);
            DrawArc(88, 242, transform.position + new Vector3(0.2f, 0.55f, 0), transform.rotation, 0.20f, Color.red, true);
 
            DrawArc(29, 192, transform.position + sliceOffset + new Vector3(0.5f, 0.15f, 0), transform.rotation, 0.20f, Color.red, true);
            DrawArc(192, 29, transform.position + new Vector3(0.5f, 0.15f, 0), transform.rotation, 0.20f, Color.red, true);
        }
    }
}