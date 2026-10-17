using System.Collections.Generic;
using UniVRM10;
using UnityEngine;

namespace RainyApartmentVR
{
    public enum VoiceDialogueState
    {
        Idle,
        Listen,
        Thinking,
        TalkNormal,
        TalkGentle,
        TalkShy,
        TalkNervous,
        TalkHappy,
        TalkSad,
        TalkSurprised,
        TalkAngry,
        TalkConfused
    }

    // 只驱动现有人形骨骼和 VRM 自带表情，不修改模型或绑定。
    // 动作来自一张有限词表：模型只能点名动作，具体怎么动由这里决定，不会扭坏角色。
    [DefaultExecutionOrder(100)]
    public sealed class CharacterVoicePerformance : MonoBehaviour
    {
        public AudioSource speechAudio;
        public VoiceDialogueState state = VoiceDialogueState.Idle;
        public bool proceduralBody = true;
        public bool facePlayer = true;
        [Min(1f)] public float playerAttentionRange = 3.5f;

        private Quaternion headAxisCorrection;
        private Quaternion smoothHeadWorld;
        private Quaternion smoothNeckWorld;
        private Quaternion headInput, headOutput, neckInput, neckOutput, chestInput, chestOutput;
        private bool poseApplied;
        private bool attentionInitialized;
        private float attentionWeight;
        private float clipHeadFreedom;
        private float smoothChestLean;
        private CharacterActionLibrary actionLibrary;

        private sealed class ScheduledAction
        {
            public bool fromTouch;
            public string name;
            public float start;
            public float duration;
        }

        private Vrm10Instance vrm;
        private Animator animator;
        private CharacterInteraction hug;
        private CharacterBehaviour behaviour;
        private Transform neck;
        private Transform head;
        private Transform chest;
        private Transform leftShoulder;
        private Transform rightShoulder;
        private Transform gazeTarget;
        private Quaternion neckRest;
        private Quaternion headRest;
        private Quaternion chestRest;
        private Quaternion leftShoulderRest;
        private Quaternion rightShoulderRest;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Quaternion rightUpperArmRest;
        private Quaternion rightLowerArmRest;
        private Quaternion leftUpperArmRest;
        private Quaternion leftLowerArmRest;
        private Vector3 rightUpperArmDir;
        private Vector3 rightLowerArmDir;
        private Vector3 leftUpperArmDir;
        private Vector3 leftLowerArmDir;
        private Transform player;
        private float nextBlink;
        private float blinkStarted = -1f;
        private float nextLookAway;
        private float lookAwayUntil;
        private float lookSide;
        private float nextGesture;
        private float gestureUntil;
        private float gestureSide;
        private float seed;
        private float mouth;
        private float happy;
        private float relaxed;
        private float sad;
        private float surprise;
        private float angry;
        private float browUp;
        private float browSorrow;
        private float eyeWide;
        private float mouthSmall;
        private float pettingTouchUntil;
        private SkinnedMeshRenderer faceRenderer;
        private readonly Dictionary<string, int> faceShapes = new Dictionary<string, int>();
        private readonly float[] visemes = new float[5];
        private readonly float[] audioSamples = new float[256];
        private readonly List<ScheduledAction> schedule = new List<ScheduledAction>();

        public void SetState(VoiceDialogueState value) => state = value;

        public void RegisterPettingTouch() => pettingTouchUntil = Time.time + 1.6f;

        public void EnqueueActions(IEnumerable<string> names)
        {
            if (names == null) return;
            float at = Time.time + .15f;
            foreach (var raw in names)
            {
                var name = (raw ?? "").Trim().ToLowerInvariant().Replace(" ", "_");
                if (name.Length == 0 || name == "none") continue;
                schedule.Add(new ScheduledAction { name = name, start = at, duration = ActionDuration(name) });
                at += ActionStagger(name);
            }
        }

        public void EnqueueTouchAction(string name)
        {
            schedule.Add(new ScheduledAction { name = name, start = Time.time + .15f, duration = ActionDuration(name), fromTouch = true });
        }

        private static float ActionDuration(string name)
        {
            switch (name)
            {
                case "wave": return 2.2f;
                case "nod":
                case "shake": return 1.4f;
                case "blink": return .4f;
                default: return 1.6f;
            }
        }

        private static float ActionStagger(string name)
        {
            switch (name)
            {
                case "wave": return .6f;
                case "blink": return .3f;
                default: return .5f;
            }
        }

        private float Weight(string name)
        {
            float best = 0f;
            float now = Time.time;
            for (int i = 0; i < schedule.Count; i++)
            {
                var item = schedule[i];
                if (item.name != name) continue;
                float t = now - item.start;
                if (t < 0f || t > item.duration) continue;
                best = Mathf.Max(best, Envelope(t, item.duration));
            }
            return best;
        }

        private static float Envelope(float t, float duration)
        {
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .22f));
            float fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - (duration - .35f)) / .35f));
            return rise * fall;
        }

        private void Awake()
        {
            vrm = GetComponentInChildren<Vrm10Instance>(true);
            animator = GetComponentInChildren<Animator>(true);
            hug = GetComponent<CharacterInteraction>();
            behaviour = GetComponent<CharacterBehaviour>();
            actionLibrary = GetComponent<CharacterActionLibrary>();
            if (!vrm || !animator || !animator.avatar || !animator.avatar.isHuman)
            {
                Debug.LogWarning("人物语音动作未启用：没有找到有效的人形骨架或 VRM 表情。");
                enabled = false;
                return;
            }
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (neck) neckRest = neck.localRotation;
            if (head) headRest = head.localRotation;
            // 骨骼本身的轴向不一定是朝前，记录模型静止姿势的朝前基准。
            if (head) headAxisCorrection = Quaternion.Inverse(transform.rotation) * head.rotation;
            if (chest) chestRest = chest.localRotation;
            if (leftShoulder) leftShoulderRest = leftShoulder.localRotation;
            if (rightShoulder) rightShoulderRest = rightShoulder.localRotation;
            if (rightUpperArm) rightUpperArmRest = rightUpperArm.localRotation;
            if (rightLowerArm) rightLowerArmRest = rightLowerArm.localRotation;
            if (leftUpperArm) leftUpperArmRest = leftUpperArm.localRotation;
            if (leftLowerArm) leftLowerArmRest = leftLowerArm.localRotation;
            if (rightUpperArm && rightLowerArm) rightUpperArmDir = (rightLowerArm.position - rightUpperArm.position).normalized;
            if (rightLowerArm && rightHand) rightLowerArmDir = (rightHand.position - rightLowerArm.position).normalized;
            if (leftUpperArm && leftLowerArm) leftUpperArmDir = (leftLowerArm.position - leftUpperArm.position).normalized;
            if (leftLowerArm && leftHand) leftLowerArmDir = (leftHand.position - leftLowerArm.position).normalized;
            seed = Random.value * 100f;
            nextBlink = Time.time + Random.Range(1.6f, 3.8f);
            nextLookAway = Time.time + Random.Range(3.5f, 6.5f);
            nextGesture = Time.time + Random.Range(2f, 4f);
            gazeTarget = new GameObject("人物视线目标_运行时").transform;
            vrm.LookAtTargetType = VRM10ObjectLookAt.LookAtTargetTypes.SpecifiedTransform;
            vrm.LookAtTarget = gazeTarget;
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh && renderer.sharedMesh.blendShapeCount > 0)
                {
                    faceRenderer = renderer;
                    break;
                }
            }
            if (faceRenderer)
            {
                foreach (var shape in new[]
                {
                    "Fcl_BRW_Sorrow", "Fcl_BRW_Surprised", "Fcl_BRW_Angry", "Fcl_EYE_Spread",
                    "Fcl_MTH_Small", "Fcl_MTH_Large", "Fcl_MTH_Up", "Fcl_MTH_Down", "Fcl_MTH_Sorrow"
                })
                {
                    int index = faceRenderer.sharedMesh.GetBlendShapeIndex(shape);
                    if (index >= 0) faceShapes[shape] = index;
                }
                Debug.Log("面部可动表情键：" + faceRenderer.sharedMesh.blendShapeCount + " 个，已接管 " + faceShapes.Count + " 个细节键。");
            }
        }

        private void SetFaceShape(string name, float weight)
        {
            if (!faceRenderer) return;
            if (!faceShapes.TryGetValue(name, out int index)) return;
            faceRenderer.SetBlendShapeWeight(index, Mathf.Clamp01(weight) * 100f);
        }

        private void Update()
        {
            RestoreProceduralPose();
            if ((behaviour && (behaviour.IsWalking || behaviour.IsPostureLocked || behaviour.IsHandHeld)) ||
                (actionLibrary && actionLibrary.IsPlayingClip) || (hug && hug.IsHugging))
            {
                // 指令动作开始后撤销尚未结束的触摸微动作，坐姿说话的普通表现不受影响。
                schedule.RemoveAll(item => item.fromTouch);
                touchLookUntil = 0;
            }
            if (!player && Camera.main) player = Camera.main.transform;
            schedule.RemoveAll(item => Time.time > item.start + item.duration + .2f);

            if (Time.time >= nextBlink && blinkStarted < 0f) blinkStarted = Time.time;
            if (Weight("blink") > .4f && blinkStarted < 0f) blinkStarted = Time.time;
            if (Weight("brighten") > .4f && blinkStarted < 0f && Random.value < .3f) blinkStarted = Time.time;
            float blink = 0f;
            if (blinkStarted >= 0f)
            {
                float phase = (Time.time - blinkStarted) / RandomBlinkDuration();
                if (phase >= 1f)
                {
                    blinkStarted = -1f;
                    nextBlink = Time.time + Random.Range(2.1f, 5.7f);
                }
                else blink = Mathf.Sin(phase * Mathf.PI);
            }

            float forcedAway = Weight("look_away");
            float forcedBack = Weight("look_player");
            float shyAway = Weight("shy");
            if (Time.time >= nextLookAway)
            {
                lookAwayUntil = Time.time + Random.Range(.35f, .9f);
                lookSide = Random.value < .5f ? -1f : 1f;
                nextLookAway = Time.time + Random.Range(state == VoiceDialogueState.TalkShy ? 1.5f : 4f,
                    state == VoiceDialogueState.TalkShy ? 3.5f : 8f);
            }
            if (player && gazeTarget)
            {
                float away = Time.time < lookAwayUntil ? 1f : 0f;
                away = Mathf.Max(away, forcedAway);
                away = Mathf.Max(away, shyAway * .7f);
                if (forcedBack > .3f) away = Mathf.Min(away, 1f - forcedBack);
                if (state == VoiceDialogueState.TalkShy) away = Mathf.Max(away, .35f);
                else if (state == VoiceDialogueState.TalkNervous) away = Mathf.Max(away, .18f);
                gazeTarget.position = player.position + player.right * (lookSide * away * .58f)
                    + Vector3.down * (state == VoiceDialogueState.TalkShy ? .12f : shyAway * .10f);
            }

            float targetMouth = speechAudio && speechAudio.isPlaying ? SpeechLevel() : 0f;
            mouth = Mathf.Lerp(mouth, targetMouth, 1f - Mathf.Exp(-Time.deltaTime * 18f));

            float eHappy = 0f, eRelaxed = 0f, eSad = 0f, eSurprise = 0f, eAngry = 0f;
            float eBrowUp = 0f, eBrowDown = 0f, eEyeWide = 0f, eMouthSmall = 0f;
            switch (state)
            {
                case VoiceDialogueState.TalkGentle:
                    eHappy = .35f; eRelaxed = .35f; break;
                case VoiceDialogueState.TalkHappy:
                    eHappy = .9f; eRelaxed = .2f; eEyeWide = .35f; break;
                case VoiceDialogueState.TalkShy:
                    eSad = .22f; eHappy = .15f; eRelaxed = .1f; eBrowDown = .55f; eMouthSmall = .65f; break;
                case VoiceDialogueState.TalkNervous:
                    eSad = .18f; eSurprise = .22f; eBrowUp = .45f; eEyeWide = .3f; break;
                case VoiceDialogueState.TalkConfused:
                    eSurprise = .3f; eBrowUp = .7f; eEyeWide = .2f; break;
                case VoiceDialogueState.TalkSurprised:
                    eSurprise = .95f; eBrowUp = .55f; eEyeWide = .85f; break;
                case VoiceDialogueState.TalkSad:
                    eSad = .85f; eBrowDown = .8f; eMouthSmall = .3f; break;
                case VoiceDialogueState.TalkAngry:
                    eAngry = .85f; eBrowDown = .2f; break;
            }
            float rate = Time.deltaTime * 2.2f;
            happy = Mathf.MoveTowards(happy, eHappy, rate);
            relaxed = Mathf.MoveTowards(relaxed, eRelaxed, rate);
            sad = Mathf.MoveTowards(sad, eSad, rate);
            surprise = Mathf.MoveTowards(surprise, eSurprise, rate);
            angry = Mathf.MoveTowards(angry, eAngry, rate);
            browUp = Mathf.MoveTowards(browUp, eBrowUp, rate);
            browSorrow = Mathf.MoveTowards(browSorrow, eBrowDown, rate);
            eyeWide = Mathf.MoveTowards(eyeWide, eEyeWide, rate);
            mouthSmall = Mathf.MoveTowards(mouthSmall, eMouthSmall, rate);

            float smile = Weight("smile");
            float bright = Weight("brighten");
            if (smile > 0f) happy = Mathf.Max(happy, smile * .5f);
            if (bright > 0f)
            {
                happy = Mathf.Max(happy, bright * .55f);
                eyeWide = Mathf.Max(eyeWide, bright * .8f);
            }
            float frown = Mathf.Max(Weight("shy") * .8f, shyAway * .5f);
            float touched = Mathf.Clamp01((pettingTouchUntil - Time.time) / .45f);
            happy = Mathf.Max(happy, touched * .38f);
            relaxed = Mathf.Max(relaxed, touched * .3f);

            if (vrm)
            {
                var expressions = vrm.Runtime.Expression;
                expressions.SetWeight(ExpressionKey.Blink, blink);
                expressions.SetWeight(ExpressionKey.Happy, Mathf.Clamp01(happy));
                expressions.SetWeight(ExpressionKey.Relaxed, Mathf.Clamp01(relaxed));
                expressions.SetWeight(ExpressionKey.Sad, Mathf.Clamp01(sad + shyAway * .1f));
                expressions.SetWeight(ExpressionKey.Surprised, Mathf.Clamp01(surprise));
                expressions.SetWeight(ExpressionKey.Angry, Mathf.Clamp01(angry));

                // 口型：音量决定幅度，A/I/U/E/O 之间缓慢游走，说话不再只是单纯张合
                float center = Mathf.Repeat(Time.time * 1.25f + seed * 0.31f, 5f);
                for (int i = 0; i < 5; i++)
                {
                    float distance = Mathf.Abs(center - i);
                    distance = Mathf.Min(distance, 5f - distance);
                    float want = Mathf.Max(0f, 1.15f - distance) * mouth;
                    visemes[i] = Mathf.Lerp(visemes[i], want, 1f - Mathf.Exp(-Time.deltaTime * 14f));
                }
                expressions.SetWeight(ExpressionKey.Aa, Mathf.Clamp01(visemes[0]));
                expressions.SetWeight(ExpressionKey.Ih, Mathf.Clamp01(visemes[1]));
                expressions.SetWeight(ExpressionKey.Ou, Mathf.Clamp01(visemes[2]));
                expressions.SetWeight(ExpressionKey.Ee, Mathf.Clamp01(visemes[3]));
                expressions.SetWeight(ExpressionKey.Oh, Mathf.Clamp01(visemes[4]));
            }
            SetFaceShape("Fcl_BRW_Surprised", browUp);
            SetFaceShape("Fcl_BRW_Sorrow", Mathf.Max(browSorrow, frown));
            SetFaceShape("Fcl_EYE_Spread", eyeWide);
            SetFaceShape("Fcl_MTH_Small", mouthSmall);
            SetFaceShape("Fcl_MTH_Large", surprise * .5f);
            SetFaceShape("Fcl_MTH_Sorrow", sad * .45f);
            SetFaceShape("Fcl_MTH_Up", Mathf.Max(happy * .35f, angry * .25f));
            SetFaceShape("Fcl_MTH_Down", sad * .35f + angry * .3f);
        }

        private float RandomBlinkDuration() => .15f + Mathf.Repeat(seed * .013f, .055f);

        private float SpeechLevel()
        {
            if (!speechAudio || !speechAudio.clip) return 0f;
            if (speechAudio.clip.name == "角色实时声音")
            {
                speechAudio.GetOutputData(audioSamples, 0);
                float liveSum = 0f;
                for (int i = 0; i < audioSamples.Length; i++) liveSum += audioSamples[i] * audioSamples[i];
                return Mathf.Clamp01(Mathf.Sqrt(liveSum / audioSamples.Length) * 5f);
            }
            var clip = speechAudio.clip;
            int position = Mathf.Clamp(speechAudio.timeSamples, 0, Mathf.Max(0, clip.samples - audioSamples.Length));
            if (!clip.GetData(audioSamples, position)) return .18f;
            float sum = 0f;
            for (int i = 0; i < audioSamples.Length; i++) sum += audioSamples[i] * audioSamples[i];
            return Mathf.Clamp01(Mathf.Sqrt(sum / audioSamples.Length) * 5f);
        }

        private static void Aim(Transform bone, Quaternion rest, Vector3 restDir, Vector3 targetDir, float weight)
        {
            if (!bone) return;
            if (weight <= .001f) return;
            var delta = Quaternion.FromToRotation(restDir, targetDir.normalized);
            var parentRotation = bone.parent ? bone.parent.rotation : Quaternion.identity;
            var current = bone.localRotation;
            var local = Quaternion.Inverse(parentRotation) * delta * parentRotation * current;
            bone.localRotation = Quaternion.Slerp(current, local, weight);
        }

        private float touchLookUntil;
        private Vector3 touchLookLocal;
        private readonly Dictionary<Transform, Quaternion> seatedInputs = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Quaternion> seatedOutputs = new Dictionary<Transform, Quaternion>();
        public void LookAtTouch(Vector3 position)
        {
            touchLookLocal = transform.InverseTransformPoint(position);
            touchLookUntil = Time.time + 2f;
        }
        private void SeatedOffset(Transform bone, Vector3 angles)
        {
            if (!bone) return;
            seatedInputs[bone] = bone.localRotation;
            bone.localRotation *= Quaternion.Euler(angles);
            seatedOutputs[bone] = bone.localRotation;
        }
        private void LateUpdate()
        {
            bool seated = behaviour && behaviour.IsSitting && animator &&
                !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName("SitDown") &&
                animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= .97f;
            if (behaviour && behaviour.IsPostureLocked && !seated) return;
            // 起身时坐姿锁已经解除，仍须把过渡和起身片段留给原动画。
            if (animator && ((animator.IsInTransition(0) && (animator.GetNextAnimatorStateInfo(0).IsName("SitDown") || animator.GetNextAnimatorStateInfo(0).IsName("StandUp"))) || animator.GetCurrentAnimatorStateInfo(0).IsName("StandUp") ||
                (animator.GetCurrentAnimatorStateInfo(0).IsName("SitDown") && !seated))) return;
            var faceTouch = GetComponent<CharacterFaceTouch>();
            if (faceTouch && faceTouch.IsHolding) return;
            bool talking = state >= VoiceDialogueState.TalkNormal;
            if (talking && Time.time >= nextGesture)
            {
                gestureUntil = Time.time + Random.Range(.55f, 1.1f);
                gestureSide = Random.value < .5f ? -1f : 1f;
                nextGesture = Time.time + Random.Range(2.2f, 5f);
            }
            float gesture = talking && Time.time < gestureUntil ? Mathf.Sin((gestureUntil - Time.time) * 3.2f) : 0f;
            float t = Time.time + seed;

            if (!seated && player && facePlayer && proceduralBody)
            {
                var flat = player.position - transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > .05f && flat.sqrMagnitude < 25f)
                {
                    var want = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-Time.deltaTime * 1.4f));
                }
            }
            float shyDown = state == VoiceDialogueState.TalkShy ? 4.5f : 0f;

            float aTilt = Weight("tilt_head") + Weight("tilt");
            float aDown = Weight("head_down") + Weight("look_down") + Weight("sad");
            float aNod = Weight("nod") + Weight("nod_head");
            float aShake = Weight("shake") + Weight("shake_head");
            float aShy = Weight("shy");
            float aLean = Weight("lean_in") + Weight("lean") + Weight("approach");
            float aWave = Weight("wave");
            float aHands = Weight("hand_chest") + Weight("hands_together");

            float listenNod = state == VoiceDialogueState.Listen ? Mathf.Sin(t * 1.7f) * 1.2f : 0f;
            bool pettingPose = behaviour && behaviour.IsInvitingPetting && !behaviour.IsWalking;
            float headPitch = listenNod + aDown * 11f + aShy * 8f + aNod * Mathf.Sin(t * 8.5f) * 7f + shyDown;
            float headYaw = aShake * Mathf.Sin(t * 8.5f) * 7f;
            float headRoll = aTilt * 9f;
            float chestLean = seated ? (talking ? 1f + Mathf.Sin(t * 1.8f) * .45f : 0f) : proceduralBody ? (talking ? 2.5f : 0f) + (pettingPose ? 8f : 0f) + aLean * 6f : 0f;
            smoothChestLean = Mathf.Lerp(smoothChestLean, chestLean, 1f - Mathf.Exp(-Time.deltaTime * 4f));
            if (chest)
            {
                chestInput = chest.localRotation;
                chest.localRotation *= Quaternion.Euler(-smoothChestLean, 0f, 0f);
                chestOutput = chest.localRotation;
            }
            ApplyPlayerAttention(headPitch + (pettingPose ? 5f : 0f), headYaw, headRoll);
            if (seated)
            {
                // 髋部和腿完全保留坐姿，仅叠加轻微上肢动作。
                if (talking)
                {
                    float sway = Mathf.Sin(t * 2.1f);
                    SeatedOffset(leftShoulder, new Vector3(sway * .8f, 0, sway * .6f));
                    SeatedOffset(rightShoulder, new Vector3(-sway * .7f, 0, sway * .5f));
                    SeatedOffset(leftUpperArm, new Vector3(gesture * 1.5f, 0, 0));
                    SeatedOffset(rightUpperArm, new Vector3(-gesture * 1.5f, 0, 0));
                }
                return;
            }
            if (!proceduralBody) return;
            if (!hug || !hug.IsHugging)
            {
                // 待机与动作片段先决定肩膀姿势；只在说话手势发生时叠加小偏移。
                if (Mathf.Abs(gesture) > .001f)
                {
                    if (leftShoulder) leftShoulder.localRotation *= Quaternion.Euler(gesture * 2.4f, 0f, gesture * gestureSide * 2.5f);
                    if (rightShoulder) rightShoulder.localRotation *= Quaternion.Euler(gesture * 2.0f, 0f, -gesture * gestureSide * 2.2f);
                }
            }

            // 已有动作片段负责手臂；只叠加头部、表情和轻微肩部变化，避免把说话手势压回 T 姿。
            if (animator && animator.runtimeAnimatorController) return;
            Vector3 up = transform.up;
            Vector3 right = transform.right;
            Vector3 forward = transform.forward;
            float waveWeight = Mathf.Clamp01(Mathf.Max(aWave, aHands * .8f));
            bool armsFree = !hug || !hug.IsHugging;
            // 只有做动作时才接管手臂；平时完全交给动画，避免把待机姿势拧变形
            if (armsFree && waveWeight > .001f)
            {
                float swing = Mathf.Sin(Time.time * 9f) * .18f * aWave;
                Vector3 waveUpper = (up * .52f + right * .64f + forward * .24f + right * swing).normalized;
                Vector3 waveLower = (up * .84f + right * .16f + forward * .38f + right * swing * .6f).normalized;
                Aim(rightUpperArm, rightUpperArmRest, rightUpperArmDir, waveUpper, waveWeight);
                Aim(rightLowerArm, rightLowerArmRest, rightLowerArmDir, waveLower, waveWeight);
                if (aHands > .5f)
                {
                    Aim(leftUpperArm, leftUpperArmRest, leftUpperArmDir, (up * .30f - right * .55f + forward * .62f).normalized, aHands);
                    Aim(leftLowerArm, leftLowerArmRest, leftLowerArmDir, (up * .45f - right * .30f + forward * .78f).normalized, aHands);
                }
            }
        }

        private void RestoreProceduralPose()
        {
            foreach (var pair in seatedInputs)
                if (pair.Key && seatedOutputs.TryGetValue(pair.Key, out var output) && Quaternion.Angle(pair.Key.localRotation, output) < .01f)
                    pair.Key.localRotation = pair.Value;
            seatedInputs.Clear(); seatedOutputs.Clear();
            if (!poseApplied) return;
            // 动画暂停（例如坐下）时也不能让上一帧的偏移继续累乘。
            // 若其他系统已经写入新姿势则尊重它，只移除仍由本组件持有的姿势。
            if (head && Quaternion.Angle(head.localRotation, headOutput) < .01f) head.localRotation = headInput;
            if (neck && Quaternion.Angle(neck.localRotation, neckOutput) < .01f) neck.localRotation = neckInput;
            if (chest && Quaternion.Angle(chest.localRotation, chestOutput) < .01f) chest.localRotation = chestInput;
            poseApplied = false;
        }

        private void ApplyPlayerAttention(float pitchGesture, float yawGesture, float rollGesture)
        {
            if (!head) return;
            headInput = head.localRotation;
            if (neck) neckInput = neck.localRotation;
            if (!attentionInitialized)
            {
                smoothHeadWorld = head.rotation;
                if (neck) smoothNeckWorld = neck.rotation;
                attentionInitialized = true;
            }
            float targetWeight = 0f;
            float yaw = 0f, pitch = 0f;
            if (player || Time.time < touchLookUntil)
            {
                Vector3 target = Time.time < touchLookUntil ? transform.TransformPoint(touchLookLocal) : player.position;
                if (gazeTarget && Time.time < touchLookUntil) gazeTarget.position = target;
                Vector3 local = transform.InverseTransformDirection(target - head.position);
                float horizontal = new Vector2(local.x, local.z).magnitude;
                float distance = local.magnitude;
                yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                pitch = Mathf.Atan2(-local.y, Mathf.Max(.1f, horizontal)) * Mathf.Rad2Deg;
                // 近处一直关注玩家；边缘平滑淡出，身后不强行反拧脖子。
                float range = Mathf.Max(1f, playerAttentionRange);
                float proximity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(range, range + .75f, distance));
                float cone = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(65f, 105f, Mathf.Abs(yaw)));
                targetWeight = proximity * cone * .94f;
            }
            float follow = 1f - Mathf.Exp(-Time.deltaTime * 6f);
            attentionWeight = Mathf.Lerp(attentionWeight, targetWeight, follow);
            Quaternion look = transform.rotation * Quaternion.Euler(Mathf.Clamp(pitch, -18f, 30f), Mathf.Clamp(yaw, -55f, 55f), 0f);
            Quaternion desiredHead = look * headAxisCorrection;
            if (neck)
            {
                Quaternion correction = desiredHead * Quaternion.Inverse(head.rotation);
                Quaternion desiredNeck = Quaternion.Slerp(Quaternion.identity, correction, attentionWeight * .28f) * neck.rotation;
                smoothNeckWorld = Quaternion.Slerp(smoothNeckWorld, desiredNeck, follow);
                neck.rotation = smoothNeckWorld;
                neckOutput = neck.localRotation;
            }
            Quaternion animatedHead = head.rotation;
            // 正在点头等片段中保留少量动画权重，不把动作库的头部表现完全抹掉。
            clipHeadFreedom = Mathf.Lerp(clipHeadFreedom, actionLibrary && actionLibrary.IsPlayingClip ? .15f : 0f, follow);
            Quaternion targetHead = Quaternion.Slerp(animatedHead, desiredHead, attentionWeight * (1f - clipHeadFreedom));
            // 微动作仍可叠加，但不因开始或结束说话而突然改变跟随权重。
            targetHead *= Quaternion.Euler(pitchGesture, yawGesture, rollGesture);
            smoothHeadWorld = Quaternion.Slerp(smoothHeadWorld, targetHead, follow);
            head.rotation = smoothHeadWorld;
            headOutput = head.localRotation;
            poseApplied = true;
        }

        private void OnDisable() => RestoreProceduralPose();

        private void OnDestroy()
        {
            if (gazeTarget) Destroy(gazeTarget.gameObject);
        }
    }
}
