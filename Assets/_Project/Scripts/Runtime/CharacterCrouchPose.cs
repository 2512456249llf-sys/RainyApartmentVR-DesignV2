using System.Collections.Generic;
using UnityEngine;

namespace RainyApartmentVR
{
    // 蹲身是本地骨架姿态，不借用坐下片段，也不暂停整个动画系统。
    [DefaultExecutionOrder(95)]
    public sealed class CharacterCrouchPose : MonoBehaviour
    {
        private CharacterBehaviour behaviour;
        private Animator animator;
        private Transform hips, spine, leftUpper, leftLower, leftFoot, rightUpper, rightLower, rightFoot;
        private float weight;
        private Vector3 hipsInput, hipsOutput;
        private readonly Dictionary<Transform, Quaternion> inputs = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Quaternion> outputs = new Dictionary<Transform, Quaternion>();
        public float Weight => weight;

        private void Awake()
        {
            behaviour = GetComponent<CharacterBehaviour>();
            animator = GetComponentInChildren<Animator>(true);
            if (!animator || !animator.isHuman) { enabled = false; return; }
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        private void Update()
        {
            // 移除上帧自己叠加的姿势，保证连续聊天或动画被剔除时不会越蹲越低。
            if (hips && (hips.localPosition - hipsOutput).sqrMagnitude < .0000001f)
                hips.localPosition = hipsInput;
            foreach (var pair in outputs)
                if (pair.Key && Quaternion.Angle(pair.Key.localRotation, pair.Value) < .01f)
                    pair.Key.localRotation = inputs[pair.Key];
            outputs.Clear();
            inputs.Clear();
        }

        private void Capture(Transform bone)
        {
            if (bone) inputs[bone] = bone.localRotation;
        }

        private void LateUpdate()
        {
            var prayer = GetComponent<CharacterPrayer>();
            if (prayer && prayer.IsActive) { weight = 0; return; }
            weight = Mathf.MoveTowards(weight, behaviour && behaviour.IsCrouching ? 1f : 0f, Time.deltaTime * 1.7f);
            if (weight <= .0001f || !hips || !leftFoot || !rightFoot) return;
            Vector3 leftPosition = leftFoot.position, rightPosition = rightFoot.position;
            Quaternion leftRotation = leftFoot.rotation, rightRotation = rightFoot.rotation;
            hipsInput = hips.localPosition;
            foreach (var bone in new[] { spine, leftUpper, leftLower, leftFoot, rightUpper, rightLower, rightFoot }) Capture(bone);
            hips.position += (Vector3.down * .48f - transform.forward * .14f) * weight;
            if (spine) spine.rotation = Quaternion.AngleAxis(20f * weight, transform.right) * spine.rotation;
            HumanoidLegPose.Solve(leftUpper, leftLower, leftFoot, leftPosition, transform.forward);
            HumanoidLegPose.Solve(rightUpper, rightLower, rightFoot, rightPosition, transform.forward);
            leftFoot.rotation = leftRotation;
            rightFoot.rotation = rightRotation;
            hipsOutput = hips.localPosition;
            foreach (var pair in inputs) outputs[pair.Key] = pair.Key.localRotation;
        }
    }
}
