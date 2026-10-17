using UnityEngine;
namespace RainyApartmentVR.Tests
{
    // 在所有姿态组件和人物模型处理完后采样，避免更新阶段的临时撤销姿态造成误判。
    [DefaultExecutionOrder(14000)]
    public sealed class ContinuationPoseProbe : MonoBehaviour
    {
        public Animator animator;
        public float hipHeight, leftFootHeight, rightFootHeight, leftToeHeight, rightToeHeight;
        public Quaternion headRotation;
        public float neckLength;
        public float riseMinimumFoot = float.PositiveInfinity;
        public Transform paintedAnchor;
        public LineRenderer paintedLine;
        public Vector3 paintedLocalPoint;
        public float attachmentError;
        private void LateUpdate()
        {
            if (!animator) return;
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            hipHeight = animator.GetBoneTransform(HumanBodyBones.Hips).position.y;
            leftFootHeight = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y;
            rightFootHeight = animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y;
            var leftToe = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            var rightToe = animator.GetBoneTransform(HumanBodyBones.RightToes);
            leftToeHeight = leftToe ? leftToe.position.y : leftFootHeight;
            rightToeHeight = rightToe ? rightToe.position.y : rightFootHeight;
            headRotation = head.localRotation;
            neckLength = Vector3.Distance(head.position, neck.position);
            var prayer = GetComponent<CharacterPrayer>();
            if (prayer && prayer.IsRising) riseMinimumFoot = Mathf.Min(riseMinimumFoot, leftFootHeight, rightFootHeight, leftToeHeight, rightToeHeight);
            if (paintedAnchor && paintedLine && paintedLine.positionCount > 0)
                attachmentError = Vector3.Distance(paintedAnchor.TransformPoint(paintedLocalPoint), paintedLine.GetPosition(0));
        }
    }
}
