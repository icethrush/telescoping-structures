﻿using UnityEngine;
using System.Collections.Generic;

namespace Telescopes
{
    public static class TelescopeUtils
    {
        static int segmentCount = 0;

        public static Vector3 translateAlongCircle(float curvatureAmount, float arcLength)
        {
            if (curvatureAmount > 1e-6)
            {
                float curvatureRadius = 1f / curvatureAmount;

                // Start at the bottom of the circle.
                float baseAngle = 3 * Mathf.PI / 2; // + baseRadians;

                // Compute how many radians we moved.
                float radians = arcLength * curvatureAmount;
                float finalAngle = baseAngle + radians;

                // Compute on the circle centered at (0, 0, 0),
                // and then add (0, r, 0).
                Vector3 center = Vector3.up * curvatureRadius;
                Vector3 displacement = Vector3.zero;
                displacement.z = Mathf.Cos(finalAngle) * curvatureRadius;
                displacement.y = Mathf.Sin(finalAngle) * curvatureRadius;
                displacement += center;
                return displacement;
            }
            else
            {
                Vector3 displacement = (arcLength /* + baseRadians */) * Vector3.forward;
                return displacement;
            }
        }

        public static Quaternion rotateAlongCircle(float curvatureAmount, float arcLength)
        {
            if (curvatureAmount < 1e-6) return Quaternion.identity;
            // Compute how many radians we moved.
            float radians = arcLength * curvatureAmount; // + baseRadians;

            // Now rotate the forward vector by that amount.
            Vector3 axisOfRotation = Vector3.right;
            float degrees = radians / Mathf.Deg2Rad;
            Quaternion rotation = Quaternion.AngleAxis(-degrees, axisOfRotation);
            return rotation;
        }

        public static Vector3 childBasePosition(TelescopeParameters parent, TelescopeParameters child)
        {
            Vector3 translationToBase = translateAlongCircle(parent.curvature, parent.length);
            Quaternion rotationToBase = rotateAlongCircle(parent.curvature, parent.length);
            Vector3 translationBackwards = translateAlongCircle(child.curvature, -child.length);

            translationToBase = translationToBase + (rotationToBase * translationBackwards);
            return translationToBase;
        }

        public static Quaternion childBaseRotation(TelescopeParameters parent, TelescopeParameters child)
        {
            Quaternion rotationToBase = rotateAlongCircle(parent.curvature, parent.length);
            Quaternion rotationBack = rotateAlongCircle(child.curvature, -child.length);
            return rotationBack * rotationToBase;
        }

        public static void growParentToChild(TelescopeParameters parent, TelescopeParameters child, bool shrinkFit=false)
        {
            // Compute the coordinates of the child's starting position,
            // in the parent's coordinate frame.
            Vector3 childBasePos = childBasePosition(parent, child);
            Quaternion childBaseRot = childBaseRotation(parent, child);

            Vector3 childOutward = childBaseRot * Vector3.down;

            // Compute the two inner corners of the child, when nested inside its
            // parent; these are the corners we want to bounds check.
            Vector3 retractedInner = childBasePos - (child.radius * childOutward);
            Vector3 retractedOuter = childBasePos + (child.radius * childOutward);

            // If the parent has no curvature, then the required width is just the
            // max absolute y value of the child.
            float finalRadius;

            if (parent.curvature < 1e-6)
            {
                finalRadius = Mathf.Max(Mathf.Abs(retractedInner.y), Mathf.Abs(retractedOuter.y)) + parent.thickness;
                if (!shrinkFit) finalRadius = Mathf.Max(finalRadius, parent.radius);
            }

            // Otherwise we need to compute the distance from the center of rotation to the
            // corners, and take their max distance from the center line.
            else
            {
                // Fact: The centers of rotation for the parent and the extended child both lie
                // on the line (in the ZY plane) defined by the parent's center and its far face.
                float parentRadius = 1f / parent.curvature;
                Vector3 centerOfRotation = new Vector3(0, parentRadius, 0);

                // Now we can determine what width we need to 'fatten' the parent by.
                float innerDistance = Vector3.Distance(retractedInner, centerOfRotation);
                float outerDistance = Vector3.Distance(retractedOuter, centerOfRotation);
                float innerDiff = Mathf.Abs(innerDistance - parentRadius);
                float outerDiff = Mathf.Abs(outerDistance - parentRadius);

                finalRadius = Mathf.Max(innerDiff, outerDiff) + parent.thickness;
                if (!shrinkFit) finalRadius = Mathf.Max(finalRadius, parent.radius);
            }
            float widthChange = finalRadius - parent.radius;

            parent.radius = finalRadius;
            // parent.thickness += widthChange;
        }

        public static void growChainToFit(List<TelescopeParameters> parameters, bool shrinkFit = false)
        {
            // Make a pass in reverse that grows each parent so that it is large enough
            // to contain its child.
            for (int i = parameters.Count - 1; i > 0; i--)
            {
                TelescopeUtils.growParentToChild(parameters[i - 1], parameters[i], shrinkFit);
            }

        }

        public static TelescopeParameters shrinkChildToParent(TelescopeParameters parent, TelescopeParameters child)
        {
            Debug.Log("Unimplemented");
            return child;
        }

        public static float GeodesicDistanceOnSphere(float radius, Vector3 v1, Vector3 v2)
        {
            Vector3 n1 = v1.normalized;
            Vector3 n2 = v2.normalized;

            float dot = Vector3.Dot(n1, n2);
            float angle;
            if (dot > 0.9999f) angle = 0;
            else angle = Mathf.Acos(Vector3.Dot(n1, n2));
            return radius * angle;
        }

        public static TelescopeBulb bulbOfRadius(Vector3 position, float radius)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.transform.localScale = new Vector3(2 * radius, 2 * radius, 2 * radius);
            obj.name = "bulb";
            TelescopeBulb bulb = obj.AddComponent<TelescopeBulb>();
            bulb.transform.position = position;
            bulb.SetMaterial(DesignerController.instance.defaultTelescopeMaterial);
            return bulb;
        }

        public static float ArcLengthFromChord(Vector3 v1, Vector3 v2, float curvature)
        {
            float c = Vector3.Distance(v1, v2);

            // If there is no curvature, then return the straight segment.
            if (curvature < 1e-6)
            {
                return c;
            }

            float r = 1 / curvature;
            float theta = 2 * Mathf.Asin(c / (2 * r));
            float arcLength = theta * r;

            return arcLength;
        }

        /// <summary>
        /// Compute the initial tangent to an arc of nonzero curvature,
        /// given the start point, end point, and center of rotation.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="child"></param>
        /// <param name="center"></param>
        /// <returns></returns>
        static Vector3 CurveDirectionFromParent(Vector3 parent, Vector3 child, Vector3 center)
        {
            Vector3 toParent = parent - center;
            Vector3 parentToChild = child - parent;
            toParent.Normalize();

            Vector3 planeNormal = Vector3.Cross(toParent, parentToChild).normalized;

            Vector3 parentPerp = parentToChild - (Vector3.Dot(toParent, parentToChild)) * toParent;
            Vector3 inPlanePerp = parentPerp - (Vector3.Dot(planeNormal, parentPerp)) * planeNormal;

            return inPlanePerp.normalized;
        }

        public static TelescopingSegment telescopeOfCone(Vector3 startPos, float startRadius,
            Vector3 endPos, float endRadius, float wallThickness = Constants.DEFAULT_WALL_THICKNESS)
        {
            return telescopeOfCone(startPos, startRadius, endPos, endRadius, Vector3.zero);
        }

        public static TelescopingSegment telescopeOfCone(Vector3 startPos, float startRadius,
            Vector3 endPos, float endRadius, Vector3 curvatureCenter,
            float wallThickness = Constants.DEFAULT_WALL_THICKNESS,
            bool useCurvature = false)
        {
            float curvature;
            Vector3 segmentDirection;

            if (useCurvature)
            {
                float radius = Vector3.Distance(curvatureCenter, startPos);
                curvature = 1f / radius;
                if (curvature < 1e-6)
                {
                    curvature = 0;
                    segmentDirection = endPos - startPos;
                    segmentDirection.Normalize();
                }
                else
                {
                    segmentDirection = CurveDirectionFromParent(startPos, endPos, curvatureCenter);
                }
            }
            else
            {
                curvature = 0;
                segmentDirection = endPos - startPos;
                segmentDirection.Normalize();
            }
            
            float distance = ArcLengthFromChord(startPos, endPos, curvature);

            int numShells = Mathf.CeilToInt((Mathf.Max(startRadius, endRadius) -
                Mathf.Min(startRadius, endRadius)) / wallThickness);
            if (numShells < 2) numShells = 2;

            // int numShells = Mathf.CeilToInt(distance / Mathf.Min(startRadius, endRadius));

            // Length is just the distance we need to cover divided by the number of shells.
            float lengthPerShell = distance / numShells;
            // We attempt to choose the radii such that the telescope tapers from the start
            // radius to the end radius over the given number of shells.
            float radiusStep = (startRadius - endRadius) / numShells;

            float twist = 0;

            if (curvature >= 1e-6)
            {
                // Compute twist angles
                Quaternion rotationToOrigin = Quaternion.FromToRotation(Vector3.forward, segmentDirection);
                // The "up" direction we get if we just perform this rotation.
                Vector3 untwistedUp = rotationToOrigin * Vector3.up;
                // The "up" direction we would like to have -- orthogonal direction from circle center.
                Vector3 startEnd = endPos - startPos;
                Vector3 desiredUp = startEnd - Vector3.Dot(segmentDirection, startEnd) * segmentDirection;
                desiredUp.Normalize();
                Vector3 inverseDesired = Quaternion.Inverse(rotationToOrigin) * desiredUp;

                // The angle computation doesn't work right in 3rd and 4th quadrants,
                // so work around it by doing everything in 1st and 2nd.
                if (inverseDesired.x < 0)
                {
                    inverseDesired *= -1;
                    twist = 180;
                }
                
                float angleBetween = Mathf.Atan2(Vector3.Cross(Vector3.up, inverseDesired).magnitude,
                    Vector3.Dot(Vector3.up, inverseDesired));

                twist += -angleBetween * Mathf.Rad2Deg;
            }

            List<TelescopeParameters> diffList = new List<TelescopeParameters>();

            // Create the initial shell parameters.
            TelescopeParameters initialParams = new TelescopeParameters(lengthPerShell, startRadius, wallThickness, curvature, twist);
            diffList.Add(initialParams);
            // Create all the diffs.
            for (int i = 1; i < numShells; i++)
            {
                TelescopeParameters tp = new TelescopeParameters(0, -radiusStep, wallThickness, 0, 0);
                diffList.Add(tp);
            }

            // Create a game object that will be the new segment.
            GameObject obj = new GameObject();
            obj.name = "segment" + segmentCount;
            segmentCount++;
            obj.transform.position = startPos;
            TelescopingSegment seg = obj.AddComponent<TelescopingSegment>();
            seg.material = DesignerController.instance.defaultTelescopeMaterial;
            seg.initialDirection = segmentDirection;

            seg.MakeShellsFromDiffs(diffList);
            seg.transform.position = startPos;
            
            return seg;
        }

        public static float CircleIntersectionArea(float dist, float radius1, float radius2)
        {
            float r = Mathf.Min(radius1, radius2);
            float R = Mathf.Max(radius1, radius2);

            if (dist >= r + R) return 0;
            else if (dist <= R - r)
            {
                return Mathf.PI * r * r;
            }

            float d2 = dist * dist;
            float R2 = R * R;
            float r2 = r * r;

            // Formula from http://jwilson.coe.uga.edu/EMAT6680Su12/Carreras/EMAT6690/Essay2/essay2.html

            float sector1 = R2 * Mathf.Acos(dist / (2 * R)) - (dist / 4) * Mathf.Sqrt(4 * R2 - d2);
            float sector2 = r2 * Mathf.Acos(dist / (2 * r)) - (dist / 4) * Mathf.Sqrt(4 * r2 - d2);

            return sector1 + sector2;
        }

        public static float CircleIntersectionArea(Vector3 center1, float radius1, Vector3 center2, float radius2)
        {
            float dist = Vector3.Distance(center1, center2);
            return CircleIntersectionArea(dist, radius1, radius2);
        }

        public static bool IsColinear(Vector3 pt1, Vector3 pt2, Vector3 pt3)
        {
            Vector3 v1 = pt2 - pt1;
            Vector3 v2 = pt3 - pt2;
            v1.Normalize();
            v2.Normalize();
            float dot = Vector3.Dot(v1, v2);
            return (Mathf.Abs(dot) > 0.9999f);
        }

        public static Vector3 Circumcenter(Vector3 pt1, Vector3 pt2, Vector3 pt3)
        {
            // Get the perpendicular bisector of pt1, pt2
            Vector3 v12 = pt2 - pt1;
            Vector3 v12normalized = v12.normalized;
            Vector3 v13 = pt3 - pt1;
            Vector3 v13normalized = v13.normalized;

            Vector3 v12perp = v13 - (Vector3.Dot(v13, v12normalized)) * v12normalized;
            v12perp.Normalize();

            // Get the perpendicular bisector of pt1, pt3
            Vector3 v13perp = v12 - (Vector3.Dot(v12, v13normalized)) * v13normalized;
            v13perp.Normalize();

            Vector3 v12base = (pt1 + pt2) / 2;
            Vector3 v13base = (pt1 + pt3) / 2;

            // Compute intersection of the two bisectors
            Vector3 closest1, closest2;
            Math3d.ClosestPointsOnTwoLines(out closest1, out closest2, v12base, v12perp, v13base, v13perp);

            return (closest1 + closest2) / 2;
        }
    }
}
