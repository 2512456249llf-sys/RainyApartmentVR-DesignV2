using UnityEngine;

namespace RainyApartmentVR
{
    // 脸部轻抚沿用原触摸反馈，握住后可在颈部活动范围内带动头部。
    [DefaultExecutionOrder(280)]
    public sealed class CharacterFaceTouch : MonoBehaviour
    {
        public float grabRadius = .16f;
        public float maximumYaw = 38f;
        public float maximumPitch = 24f;
        public float maximumRoll = 18f;
        private Transform head, neck, holder, leftEye, rightEye;
        private Vector3 localFrontAxis = Vector3.forward, localUpAxis = Vector3.up;
        private VRHandPose hand;
        private VRHandPose[] hands;
        private Vector3 holdStart, desiredAngles, smoothAngles;
        private Quaternion headInput, headOutput, neckInput, neckOutput;
        private bool applied;
        public bool IsHolding => holder;
        public Vector3 CurrentAngles => smoothAngles;
        public Transform Head => head;
        public Vector3 FaceForward => head ? head.TransformDirection(localFrontAxis).normalized : transform.forward;
        public Vector3 FaceUp => head ? head.TransformDirection(localUpAxis).normalized : transform.up;
        public Vector3 FaceCenter => leftEye && rightEye ? (leftEye.position + rightEye.position) * .5f - FaceUp * .025f + FaceForward * .01f
            : head ? head.position + FaceUp * .065f + FaceForward * .085f : transform.position;

        private void Awake()
        {
            var animator = GetComponentInChildren<Animator>(true);
            if (!animator || !animator.isHuman) { enabled = false; return; }
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            if (head && neck)
            {
                Vector3 up = (head.position - neck.position).normalized;
                localUpAxis = head.InverseTransformDirection(up);
                if (leftEye && rightEye)
                {
                    Vector3 front = Vector3.ProjectOnPlane((leftEye.position + rightEye.position) * .5f - head.position, up).normalized;
                    if (front.sqrMagnitude > .1f) localFrontAxis = head.InverseTransformDirection(front);
                }
            }
            hands = FindObjectsByType<VRHandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
        public bool BeginHold(Transform gripPoint)
        {
            if (!CharacterPoseGate.CanTouch(gameObject) || !head || !gripPoint || holder || Vector3.Distance(gripPoint.position, FaceCenter) > grabRadius) return false;
            holder = gripPoint;
            holdStart = transform.InverseTransformPoint(gripPoint.position);
            GetComponent<CharacterVoicePerformance>()?.RegisterPettingTouch();
            return true;
        }
        public void Release() { holder = null; hand = null; desiredAngles = Vector3.zero; }
        private void Update()
        {
            if (applied)
            {
                if (head && Quaternion.Angle(head.localRotation, headOutput) < .01f) head.localRotation = headInput;
                if (neck && Quaternion.Angle(neck.localRotation, neckOutput) < .01f) neck.localRotation = neckInput;
                applied = false;
            }
            if (holder)
            {
                if (!holder.gameObject.activeInHierarchy || (hand && (hand.GripValue < .32f || hand.IsHoldingObject)) || Vector3.Distance(holder.position, FaceCenter) > .42f)
                { Release(); return; }
                Vector3 displacement = transform.InverseTransformPoint(holder.position) - holdStart;
                desiredAngles = new Vector3(Mathf.Clamp(-displacement.y * 180f, -maximumPitch, maximumPitch),
                    Mathf.Clamp(displacement.x * 220f, -maximumYaw, maximumYaw),
                    Mathf.Clamp(-displacement.x * 80f, -maximumRoll, maximumRoll));
                GetComponent<CharacterVoicePerformance>()?.RegisterPettingTouch();
                return;
            }
            desiredAngles = Vector3.zero;
            foreach (var candidate in hands)
            {
                if (!candidate || !candidate.isActiveAndEnabled || candidate.IsHoldingObject || candidate.GripValue < .68f) continue;
                if (BeginHold(candidate.transform)) { hand = candidate; break; }
            }
        }
        private void LateUpdate()
        {
            if (!head) return;
            if (!CharacterPoseGate.CanTouch(gameObject)) { Release(); smoothAngles = Vector3.zero; return; }
            smoothAngles = Vector3.Lerp(smoothAngles, desiredAngles, 1f - Mathf.Exp(-Time.deltaTime * (holder ? 14f : 7f)));
            headInput = head.localRotation;
            head.localRotation *= Quaternion.Euler(smoothAngles * .78f);
            headOutput = head.localRotation;
            if (neck) { neckInput = neck.localRotation; neck.localRotation *= Quaternion.Euler(smoothAngles * .22f); neckOutput = neck.localRotation; }
            applied = true;
        }
        private void OnDisable()
        {
            Release();
            if (applied && head && Quaternion.Angle(head.localRotation, headOutput) < .01f) head.localRotation = headInput;
            if (applied && neck && Quaternion.Angle(neck.localRotation, neckOutput) < .01f) neck.localRotation = neckInput;
            smoothAngles = Vector3.zero;
            applied = false;
        }
    }
}
