using UnityEngine;

namespace RainyApartmentVR
{
    // 共用两节腿部求解；只转动关节，不把脚骨强行拉长。
    public static class HumanoidLegPose
    {
        public static void Solve(Transform upper, Transform lower, Transform foot, Vector3 destination, Vector3 kneeForward)
        {
            if (!upper || !lower || !foot) return;
            Vector3 hip = upper.position;
            float a = Vector3.Distance(hip, lower.position), b = Vector3.Distance(lower.position, foot.position);
            Vector3 reach = destination - hip;
            if (a < .01f || b < .01f || reach.sqrMagnitude < .0001f) return;
            float distance = Mathf.Clamp(reach.magnitude, Mathf.Abs(a - b) + .001f, (a + b) * .999f);
            Vector3 direction = reach.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(kneeForward, direction).normalized;
            if (bend.sqrMagnitude < .01f) bend = Vector3.ProjectOnPlane(Vector3.up, direction).normalized;
            float along = (a * a - b * b + distance * distance) / (2f * distance);
            float side = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Aim(upper, lower, hip + direction * along + bend * side);
            Aim(lower, foot, hip + direction * distance);
        }

        private static void Aim(Transform bone, Transform child, Vector3 point)
        {
            Vector3 from = child.position - bone.position, to = point - bone.position;
            if (from.sqrMagnitude > .00001f && to.sqrMagnitude > .00001f)
                bone.rotation = Quaternion.FromToRotation(from, to) * bone.rotation;
        }
    }
}
