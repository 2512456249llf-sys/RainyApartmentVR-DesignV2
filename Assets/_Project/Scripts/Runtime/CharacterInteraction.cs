using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace RainyApartmentVR
{
    // 使用原始骨架做安全的程序化站姿与拥抱，不播放会拉扭蒙皮的外部动作。
    public sealed class CharacterInteraction : MonoBehaviour
    {
        [Header("拥抱触发")]
        public float minimumDistance = .58f;
        public float maximumDistance = 1.55f;
        public float facingAngle = 58f;
        public float cooldown = 7f;

        private Animator animator;
        private Transform spine;
        private Transform chest;
        private Transform leftUpper;
        private Transform leftLower;
        private Transform leftHand;
        private Transform rightUpper;
        private Transform rightLower;
        private Transform rightHand;
        private Quaternion spineRest;
        private Quaternion chestRest;
        private Quaternion leftUpperRest;
        private Quaternion leftLowerRest;
        private Quaternion rightUpperRest;
        private Quaternion rightLowerRest;
        private Transform player;
        private float hugStarted = -10f;
        private float nextAllowed;
        private float bothGripHeld;

        public bool IsHugging => Time.time - hugStarted < 4.5f;

        private void Awake()
        {
            PreparePose();
        }

        public void PreparePose()
        {
            animator = GetComponentInChildren<Animator>(true);
            if (!animator || !animator.avatar || !animator.avatar.isHuman)
            {
                enabled = false;
                return;
            }
            // 保留已经安装的待机、走路和说话动作控制器。
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (!spine || !chest || !leftUpper || !leftLower || !leftHand || !rightUpper || !rightLower || !rightHand)
            {
                enabled = false;
                return;
            }
            spineRest = spine.localRotation;
            chestRest = chest.localRotation;
            leftUpperRest = leftUpper.localRotation;
            leftLowerRest = leftLower.localRotation;
            rightUpperRest = rightUpper.localRotation;
            rightLowerRest = rightLower.localRotation;
        }

        private void Update()
        {
            if (!player && Camera.main) player = Camera.main.transform;
            if (!player || Time.time < nextAllowed || !CanHug()) { bothGripHeld = 0f; return; }

            bool keyboard = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            bool leftGrip = Grip(XRNode.LeftHand) > .68f;
            bool rightGrip = Grip(XRNode.RightHand) > .68f;
            bothGripHeld = leftGrip && rightGrip ? bothGripHeld + Time.deltaTime : 0f;
            if (keyboard || bothGripHeld > .22f) BeginHug();
        }

        private static float Grip(XRNode node)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            return device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float value) ? value : 0f;
        }

        private bool CanHug()
        {
            if (GetComponent<CharacterBehaviour>() && !CharacterPoseGate.CanTouch(gameObject)) return false;
            var handHold = GetComponent<CharacterHandHold>();
            if (handHold && handHold.IsHolding) return false;
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance < minimumDistance || distance > maximumDistance) return false;
            if (Vector3.Angle(transform.forward, toPlayer) > facingAngle) return false;
            Vector3 playerForward = player.forward; playerForward.y = 0f;
            return playerForward.sqrMagnitude < .01f || Vector3.Angle(playerForward, -toPlayer) < 70f;
        }

        public void BeginHug()
        {
            if (Time.time < nextAllowed || !player || !CanHug()) return;
            hugStarted = Time.time;
            nextAllowed = Time.time + cooldown;
            bothGripHeld = 0f;
            var face = GetComponent<CharacterFace>();
            if (face) face.TalkFor(1.4f);
            Debug.Log("男主：拥抱动作开始");
        }

        private void LateUpdate()
        {
            var prayer = GetComponent<CharacterPrayer>();
            if (prayer && prayer.IsActive) return;
            if (!leftUpper) return;
            if (animator && animator.runtimeAnimatorController && !IsHugging) return;
            float elapsed = Time.time - hugStarted;
            float amount = 0f;
            if (elapsed >= 0f && elapsed < 4.5f)
            {
                if (elapsed < .75f) amount = Smooth(elapsed / .75f);
                else if (elapsed < 3.25f) amount = 1f;
                else amount = 1f - Smooth((elapsed - 3.25f) / 1.25f);
            }
            EvaluatePose(amount, Mathf.Sin(Time.time * 1.55f) * 1.15f);
        }

        public void EvaluatePose(float amount, float breathing = 0f)
        {
            if (!leftUpper) PreparePose();
            if (!leftUpper) return;
            spine.localRotation = spineRest;
            chest.localRotation = chestRest;
            leftUpper.localRotation = leftUpperRest;
            leftLower.localRotation = leftLowerRest;
            rightUpper.localRotation = rightUpperRest;
            rightLower.localRotation = rightLowerRest;
            spine.localRotation *= Quaternion.Euler(breathing, 0f, 0f);
            chest.localRotation *= Quaternion.Euler(amount * 7f, 0f, 0f);

            PoseArm(true, amount);
            PoseArm(false, amount);
        }

        public string PoseDiagnostics()
        {
            if (!leftUpper) PreparePose();
            return "左肩=" + transform.InverseTransformPoint(leftUpper.position).ToString("F3") +
                   " 左肘=" + transform.InverseTransformPoint(leftLower.position).ToString("F3") +
                   " 左手=" + transform.InverseTransformPoint(leftHand.position).ToString("F3") +
                   " 右肩=" + transform.InverseTransformPoint(rightUpper.position).ToString("F3") +
                   " 右肘=" + transform.InverseTransformPoint(rightLower.position).ToString("F3") +
                   " 右手=" + transform.InverseTransformPoint(rightHand.position).ToString("F3");
        }

        private void PoseArm(bool left, float hug)
        {
            Transform upper = left ? leftUpper : rightUpper;
            Transform lower = left ? leftLower : rightLower;
            Transform hand = left ? leftHand : rightHand;
            float side = left ? -1f : 1f;
            Vector3 shoulder = upper.position;
            float bodyCenterX = (transform.InverseTransformPoint(leftUpper.position).x + transform.InverseTransformPoint(rightUpper.position).x) * .5f;
            Vector3 relaxedElbow = transform.TransformPoint(new Vector3(bodyCenterX + side * .25f, 1.09f, .04f));
            Vector3 relaxedHand = transform.TransformPoint(new Vector3(bodyCenterX + side * .28f, .84f, .12f));
            Vector3 openElbow = transform.TransformPoint(new Vector3(bodyCenterX + side * .50f, 1.35f, .30f));
            Vector3 openHand = transform.TransformPoint(new Vector3(bodyCenterX + side * .68f, 1.42f, .60f));
            Vector3 wrapElbow = transform.TransformPoint(new Vector3(bodyCenterX + side * .45f, 1.31f, .36f));
            Vector3 wrapHand = transform.TransformPoint(new Vector3(bodyCenterX + side * .24f, 1.28f, .66f));
            float openPhase = hug <= .55f ? Smooth(hug / .55f) : 1f - Smooth((hug - .55f) / .45f);
            Vector3 elbowTarget = Vector3.Lerp(relaxedElbow, Vector3.Lerp(wrapElbow, openElbow, openPhase), hug);
            Vector3 handTarget = Vector3.Lerp(relaxedHand, Vector3.Lerp(wrapHand, openHand, openPhase), hug);
            AimBone(upper, lower, elbowTarget - shoulder);
            AimBone(lower, hand, handTarget - lower.position);
        }

        private static void AimBone(Transform bone, Transform child, Vector3 desiredDirection)
        {
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < .00001f || desiredDirection.sqrMagnitude < .00001f) return;
            bone.rotation = Quaternion.FromToRotation(current.normalized, desiredDirection.normalized) * bone.rotation;
        }

        private static float Smooth(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private void OnGUI()
        {
            if (!player || IsHugging || !CanHug()) return;
            string prompt = XRSettings.isDeviceActive ? "同时握紧左右手柄：拥抱" : "按 E：拥抱";
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, normal = { textColor = Color.white } };
            GUI.Label(new Rect(Screen.width * .5f - 180f, Screen.height * .76f, 360f, 44f), prompt, style);
        }
    }
}
