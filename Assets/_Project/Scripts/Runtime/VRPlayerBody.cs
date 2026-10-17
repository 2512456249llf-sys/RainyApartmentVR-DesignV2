using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace RainyApartmentVR
{
    // 只读取头显和双手追踪，不反向移动相机；无需额外腰部或脚部追踪器。
    [DefaultExecutionOrder(500)]
    public sealed class VRPlayerBody : MonoBehaviour
    {
        public Animator avatarAnimator;
        public Transform headTarget;
        public Transform leftHandTarget;
        public Transform rightHandTarget;
        public Transform trackingOrigin;
        public VRHandPose leftTouchHand;
        public VRHandPose rightTouchHand;
        public Vector3 wristOffset = new Vector3(0f, 0f, -.025f);
        [Tooltip("相对真实握持追踪点的小幅校准，不再把手腕当作握点")]
        public Vector3 gripOffset = new Vector3(0f, -.005f, -.01f);
        public Renderer firstPersonUpperTorso;
        public bool autoCalibrateHeight = true;

        private sealed class Arm
        {
            public Transform upper, lower, wrist;
            public Quaternion wristCorrection;
            public Transform[] fingers;
            public Quaternion[] fingerRest;
            public Vector3[] curlAxes;
            public Vector3 palmLocal;
        }

        private sealed class Leg
        {
            public Transform upper, lower, foot;
            public Vector3 restFoot;
            public Quaternion footCorrection;
            public Vector3 planted, stepFrom, stepTo;
            public float stepProgress = 1f;
            public bool plantedValid;
        }

        private readonly Dictionary<Transform, Quaternion> restRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Vector3> restPositions = new Dictionary<Transform, Vector3>();
        private Transform head, neck, leftEye, rightEye;
        private Transform spine, chest;
        private Quaternion headCorrection;
        private Vector3 eyeOffset;
        private Arm left, right;
        private Leg leftLeg, rightLeg;
        private float restEyeHeight;
        private bool calibrated;
        private float calibratedHeight;
        private float calibrationAfter;
        private bool initialized;
        private Vector3 previousHeadPosition, moveVelocity;
        private bool hasPreviousHead;
        private float crouchWeight;
        private bool leftGripConfirmed, rightGripConfirmed;

        public Vector3 LeftWristPosition => left != null && left.wrist ? left.wrist.position : transform.position;
        public Vector3 RightWristPosition => right != null && right.wrist ? right.wrist.position : transform.position;
        public Vector3 LeftTrackedWrist => WristDestination(left, leftHandTarget, leftTouchHand);
        public Vector3 RightTrackedWrist => WristDestination(right, rightHandTarget, rightTouchHand);
        public Vector3 LeftPalmPosition => left != null && left.wrist ? left.wrist.TransformPoint(left.palmLocal) : transform.position;
        public Vector3 RightPalmPosition => right != null && right.wrist ? right.wrist.TransformPoint(right.palmLocal) : transform.position;
        public Vector3 LeftTrackedGrip => GripTarget(leftHandTarget, leftTouchHand).position;
        public Vector3 RightTrackedGrip => GripTarget(rightHandTarget, rightTouchHand).position;
        public float CrouchWeight => crouchWeight;
        public Vector3 EyePosition => leftEye && rightEye ? (leftEye.position + rightEye.position) * .5f : head.TransformPoint(eyeOffset);

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (initialized) return;
            if (!avatarAnimator) avatarAnimator = GetComponentInChildren<Animator>(true);
            if (!avatarAnimator || !avatarAnimator.isHuman)
            {
                Debug.LogWarning("玩家身体未启用：玩家模型没有可用的人形骨架。");
                enabled = false;
                return;
            }
            avatarAnimator.runtimeAnimatorController = null;
            avatarAnimator.applyRootMotion = false;
            avatarAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = avatarAnimator.GetBoneTransform((HumanBodyBones)i);
                if (!bone) continue;
                restRotations[bone] = bone.localRotation;
                restPositions[bone] = bone.localPosition;
            }
            head = avatarAnimator.GetBoneTransform(HumanBodyBones.Head);
            neck = avatarAnimator.GetBoneTransform(HumanBodyBones.Neck);
            spine = avatarAnimator.GetBoneTransform(HumanBodyBones.Spine);
            chest = avatarAnimator.GetBoneTransform(HumanBodyBones.Chest);
            leftEye = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftEye);
            rightEye = avatarAnimator.GetBoneTransform(HumanBodyBones.RightEye);
            if (!head) { enabled = false; return; }
            eyeOffset = leftEye && rightEye ? head.InverseTransformPoint((leftEye.position + rightEye.position) * .5f) : new Vector3(0f, .06f, .065f);
            restEyeHeight = transform.InverseTransformPoint(EyePosition).y;
            headCorrection = Quaternion.Inverse(transform.rotation) * head.rotation;
            left = MakeArm(true);
            right = MakeArm(false);
            leftLeg = MakeLeg(true);
            rightLeg = MakeLeg(false);
            calibrationAfter = Time.unscaledTime + .8f;
            initialized = true;
        }

        private Leg MakeLeg(bool isLeft)
        {
            var leg = new Leg
            {
                upper = avatarAnimator.GetBoneTransform(isLeft ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg),
                lower = avatarAnimator.GetBoneTransform(isLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg),
                foot = avatarAnimator.GetBoneTransform(isLeft ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot)
            };
            if (leg.foot)
            {
                leg.restFoot = transform.InverseTransformPoint(leg.foot.position);
                leg.footCorrection = Quaternion.Inverse(transform.rotation) * leg.foot.rotation;
            }
            return leg;
        }

        private Arm MakeArm(bool isLeft)
        {
            var arm = new Arm
            {
                upper = avatarAnimator.GetBoneTransform(isLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm),
                lower = avatarAnimator.GetBoneTransform(isLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm),
                wrist = avatarAnimator.GetBoneTransform(isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand)
            };
            int start = (int)(isLeft ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            arm.fingers = new Transform[15];
            arm.fingerRest = new Quaternion[15];
            arm.curlAxes = new Vector3[15];
            var middle = avatarAnimator.GetBoneTransform(isLeft ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            var index = avatarAnimator.GetBoneTransform(isLeft ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var little = avatarAnimator.GetBoneTransform(isLeft ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            if (!arm.wrist || !arm.upper || !arm.lower) return arm;
            Vector3 forward = middle ? (middle.position - arm.wrist.position).normalized : transform.right * (isLeft ? -1f : 1f);
            Vector3 up = index && little ? Vector3.Cross(forward, index.position - little.position).normalized * (isLeft ? 1f : -1f) : transform.up;
            if (up.sqrMagnitude < .1f) up = transform.up;
            Quaternion palmFrame = Quaternion.LookRotation(forward, up);
            arm.wristCorrection = Quaternion.Inverse(palmFrame) * arm.wrist.rotation;
            // 手柄的握持原点对应掌心，不是手腕；指根位于按键/扳机附近。
            arm.palmLocal = arm.wrist.InverseTransformPoint(Vector3.Lerp(arm.wrist.position,
                middle ? middle.position : arm.wrist.position + forward * .08f, .65f));
            Vector3 bendAxis = palmFrame * Vector3.right;
            for (int i = 0; i < 15; i++)
            {
                var finger = avatarAnimator.GetBoneTransform((HumanBodyBones)(start + i));
                arm.fingers[i] = finger;
                if (!finger) continue;
                arm.fingerRest[i] = finger.localRotation;
                arm.curlAxes[i] = Quaternion.Inverse(finger.rotation) * bendAxis;
            }
            return arm;
        }

        private void LateUpdate() => ApplyTracking(Time.deltaTime);

        public void ApplyTracking(float deltaTime)
        {
            Initialize();
            if (!initialized || !headTarget) return;
            // 恢复自己持有的身体骨骼，头发和衣摆弹簧骨不在该清单内。
            foreach (var pair in restRotations) pair.Key.localRotation = pair.Value;
            foreach (var pair in restPositions) pair.Key.localPosition = pair.Value;
            var headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool canCalibrate = !Application.isPlaying || Application.isBatchMode ||
                Time.unscaledTime >= calibrationAfter && headDevice.isValid &&
                headDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
            if (autoCalibrateHeight && canCalibrate && trackingOrigin && restEyeHeight > .5f)
            {
                float height = headTarget.position.y - trackingOrigin.position.y;
                if (height > 1.25f && height < 2.15f && (!calibrated || height > calibratedHeight + .12f))
                {
                    float scale = Mathf.Clamp(height / restEyeHeight, .75f, 1.45f);
                    transform.localScale = Vector3.one * scale;
                    calibrated = true;
                    calibratedHeight = height;
                    Debug.Log("玩家身体已按当前头显高度校准，身高比例=" + scale.ToString("F3"));
                }
            }
            float standingHeight = calibrated ? calibratedHeight : restEyeHeight * transform.lossyScale.y;
            float currentHeight = headTarget.position.y - (trackingOrigin ? trackingOrigin.position.y : 0f);
            float wantCrouch = Mathf.Clamp01((standingHeight - currentHeight - .08f) / .65f);
            crouchWeight = Mathf.Lerp(crouchWeight, wantCrouch, 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) * 12f));
            Vector3 forward = Vector3.ProjectOnPlane(headTarget.forward, Vector3.up);
            if (forward.sqrMagnitude > .02f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(forward, Vector3.up),
                    1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) * 7f));
            // 屈膝时从脊柱分摊前倾，眼睛对齐后骨盆会自然后移，而不是整具站姿下降。
            if (spine) spine.rotation = Quaternion.AngleAxis(crouchWeight * 18f, transform.right) * spine.rotation;
            if (chest) chest.rotation = Quaternion.AngleAxis(crouchWeight * 9f, transform.right) * chest.rotation;
            Quaternion look = headTarget.rotation * headCorrection;
            if (neck)
            {
                Quaternion correction = look * Quaternion.Inverse(head.rotation);
                neck.rotation = Quaternion.Slerp(Quaternion.identity, correction, .25f) * neck.rotation;
            }
            head.rotation = look;
            // 对齐模型眼睛与头显，镜头永远由原追踪系统控制。
            transform.position += headTarget.position - EyePosition;
            Vector3 horizontal = Vector3.ProjectOnPlane(headTarget.position, Vector3.up);
            Vector3 delta = hasPreviousHead ? horizontal - previousHeadPosition : Vector3.zero;
            bool teleported = delta.magnitude > .8f;
            Vector3 velocity = !teleported && deltaTime > .0001f ? delta / deltaTime : Vector3.zero;
            moveVelocity = Vector3.Lerp(moveVelocity, Vector3.ClampMagnitude(velocity, 3f),
                1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) * 10f));
            previousHeadPosition = horizontal;
            hasPreviousHead = true;
            if (teleported) { leftLeg.plantedValid = false; rightLeg.plantedValid = false; }
            ApplyArm(left, leftHandTarget, leftTouchHand, true);
            ApplyArm(right, rightHandTarget, rightTouchHand, false);
            ApplyLeg(leftLeg, rightLeg, deltaTime);
            ApplyLeg(rightLeg, leftLeg, deltaTime);
            if (firstPersonUpperTorso)
                firstPersonUpperTorso.shadowCastingMode = crouchWeight > .35f
                    ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }

        private void ApplyLeg(Leg leg, Leg other, float deltaTime)
        {
            if (leg == null || !leg.upper || !leg.lower || !leg.foot || !trackingOrigin) return;
            Vector3 ideal = transform.TransformPoint(leg.restFoot);
            // 蹲身导致骨盆后移，但双脚应继续留在头显下方的地面。
            ideal += transform.forward * crouchWeight * .14f;
            ideal.y = trackingOrigin.position.y + leg.restFoot.y * transform.lossyScale.y;
            if (!leg.plantedValid || Vector3.Distance(leg.planted, ideal) > .8f)
            {
                leg.planted = ideal;
                leg.plantedValid = true;
                leg.stepProgress = 1f;
            }
            leg.planted.y = ideal.y;
            float threshold = moveVelocity.magnitude > .12f ? .12f : .20f;
            if (leg.stepProgress >= 1f && (other == null || other.stepProgress >= 1f) &&
                Vector3.ProjectOnPlane(ideal - leg.planted, Vector3.up).magnitude > threshold)
            {
                leg.stepFrom = leg.planted;
                leg.stepTo = ideal + Vector3.ClampMagnitude(moveVelocity * .14f, .18f);
                leg.stepProgress = 0f;
            }
            Vector3 destination = leg.planted;
            if (leg.stepProgress < 1f)
            {
                leg.stepProgress = Mathf.Min(1f, leg.stepProgress + Mathf.Max(0f, deltaTime) / .28f);
                destination = Vector3.Lerp(leg.stepFrom, leg.stepTo, Mathf.SmoothStep(0f, 1f, leg.stepProgress));
                destination.y = ideal.y + Mathf.Sin(leg.stepProgress * Mathf.PI) * .075f * (1f - crouchWeight * .65f);
                if (leg.stepProgress >= 1f) leg.planted = leg.stepTo;
            }
            HumanoidLegPose.Solve(leg.upper, leg.lower, leg.foot, destination, transform.forward);
            leg.foot.rotation = transform.rotation * leg.footCorrection;
        }

        private Pose GripTarget(Transform target, VRHandPose touchHand)
        {
            if (!target) return new Pose(transform.position, transform.rotation);
            // 旧手的偏移只服务于旧外观；不能叠加在新模型的握持追踪点上。
            Transform grip = touchHand && target == touchHand.transform ? target.parent : target;
            if (!grip) grip = target;
            Vector3 correction = touchHand && (target == touchHand.transform || target == touchHand.transform.parent)
                ? touchHand.ContactDisplacement : Vector3.zero;
            Pose pose = new Pose(grip.position, grip.rotation);
            // 射线节点的复合绑定优先读瞄准姿态；身体单独读真实握持姿态，不改射线交互。
            if (Application.isPlaying && !Application.isBatchMode && touchHand && grip == touchHand.transform.parent)
            {
                var device = InputDevices.GetDeviceAtXRNode(touchHand.IsLeftHand ? XRNode.LeftHand : XRNode.RightHand);
                if (device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked &&
                    device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position) &&
                    device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
                {
                    Transform origin = grip.parent ? grip.parent : trackingOrigin;
                    pose = origin ? new Pose(origin.TransformPoint(position), origin.rotation * rotation) : new Pose(position, rotation);
                    bool confirmed = touchHand.IsLeftHand ? leftGripConfirmed : rightGripConfirmed;
                    if (!confirmed)
                    {
                        Debug.Log((touchHand.IsLeftHand ? "左手" : "右手") + "已使用真实握持追踪；与原射线节点距离=" +
                            Vector3.Distance(pose.position, grip.position).ToString("F3") + "米。");
                        if (touchHand.IsLeftHand) leftGripConfirmed = true; else rightGripConfirmed = true;
                    }
                }
            }
            return new Pose(pose.position + pose.rotation * gripOffset + correction, pose.rotation);
        }

        private Vector3 WristDestination(Arm arm, Transform target, VRHandPose touchHand)
        {
            Pose grip = GripTarget(target, touchHand);
            if (arm == null || !arm.wrist) return grip.position;
            Quaternion wristRotation = grip.rotation * arm.wristCorrection;
            return grip.position - wristRotation * Vector3.Scale(arm.palmLocal, arm.wrist.lossyScale);
        }

        private void ApplyArm(Arm arm, Transform target, VRHandPose touchHand, bool isLeft)
        {
            if (arm == null || !arm.upper || !arm.lower || !arm.wrist || !target) return;
            Pose gripPose = GripTarget(target, touchHand);
            Vector3 destination = WristDestination(arm, target, touchHand);
            Vector3 shoulder = arm.upper.position;
            Vector3 reach = destination - shoulder;
            float upperLength = Vector3.Distance(shoulder, arm.lower.position);
            float lowerLength = Vector3.Distance(arm.lower.position, arm.wrist.position);
            if (upperLength < .01f || lowerLength < .01f || reach.sqrMagnitude < .0001f) return;
            // 轻微伸展避免追踪到的手臂比模型长时，虚拟手落后于手柄。
            float stretch = Mathf.Clamp(reach.magnitude / ((upperLength + lowerLength) * .995f), 1f, 1.15f);
            arm.lower.localPosition *= stretch;
            arm.wrist.localPosition *= stretch;
            upperLength *= stretch;
            lowerLength *= stretch;
            float distance = Mathf.Clamp(reach.magnitude, .025f, (upperLength + lowerLength) * .995f);
            Vector3 direction = reach.normalized;
            Vector3 pole = transform.right * (isLeft ? -.75f : .75f) - transform.up * .65f - transform.forward * .25f;
            Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
            if (bend.sqrMagnitude < .01f) bend = Vector3.ProjectOnPlane(transform.forward, direction).normalized;
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float side = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Aim(arm.upper, arm.lower, shoulder + direction * along + bend * side);
            Aim(arm.lower, arm.wrist, shoulder + direction * distance);
            arm.wrist.rotation = gripPose.rotation * arm.wristCorrection;
            // 三点追踪优先保证实际可见的手和手柄一致，极端伸手时允许前臂轻微伸长。
            arm.wrist.position = destination;
            float grip = touchHand ? touchHand.GripValue : 0f;
            float trigger = 0f;
            var device = InputDevices.GetDeviceAtXRNode(isLeft ? XRNode.LeftHand : XRNode.RightHand);
            if (device.isValid) device.TryGetFeatureValue(CommonUsages.trigger, out trigger);
            for (int i = 0; i < arm.fingers.Length; i++)
            {
                if (!arm.fingers[i]) continue;
                float curl = i / 3 == 1 ? trigger : grip;
                if (i < 3) curl *= .55f;
                arm.fingers[i].localRotation = arm.fingerRest[i] * Quaternion.AngleAxis(curl * (i % 3 == 0 ? 62f : 48f), arm.curlAxes[i]);
            }
        }

        private static void Aim(Transform bone, Transform child, Vector3 point)
        {
            Vector3 from = child.position - bone.position;
            Vector3 to = point - bone.position;
            if (from.sqrMagnitude > .0001f && to.sqrMagnitude > .0001f)
                bone.rotation = Quaternion.FromToRotation(from, to) * bone.rotation;
        }
    }
}
