using System.Collections;
using UnityEngine;

namespace RainyApartmentVR
{
    // 驱动人物的待机、移动和姿态；何时调用由对话模型动态决定。
    [DefaultExecutionOrder(90)]
    public sealed class CharacterBehaviour : MonoBehaviour
    {
        public Animator animator;
        public VoiceDialogue dialogue;
        public CharacterVoicePerformance performance;
        public float arriveDistance = 0.9f;
        public float followDistance = 1.9f;
        public float wanderRadius = 3.2f;
        public float wanderMin = 10f;
        public float wanderMax = 22f;
        public bool wanderEnabled = false;
        public bool useRootMotion = true;
        public float manualSpeed = 1.15f;
        public float moveCooldown = 22f;
        [Tooltip("走近玩家的冷却秒数（比自主走动短，保证玩家喊过来能快速响应）")]
        public float approachCooldown = 5f;
        public float walkAroundRadius = 2f;

        private Vector3 home;
        private float nextMoveAllowed;
        private float nextApproachAllowed;
        private Vector3 target;
        private bool hasTarget;
        private bool walking;
        private bool sitting;
        private bool crouching;
        private bool followMode;
        private bool invitingPetting;
        private bool handHeld;
        private Vector3 handMotion;
        private float nextWander;
        private Transform player;

        public bool IsWalking => walking;
        public bool IsSitting => sitting;
        public bool IsCrouching => crouching;
        public bool IsPostureLocked => sitting || crouching || (GetComponent<CharacterPrayer>() && GetComponent<CharacterPrayer>().IsActive);
        public bool IsInvitingPetting => invitingPetting;
        public bool IsHandHeld => handHeld;

        private void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>(true);
            if (!dialogue) dialogue = GetComponent<VoiceDialogue>();
            if (!performance) performance = GetComponent<CharacterVoicePerformance>();
            home = transform.position;
            nextWander = Time.time + Random.Range(wanderMin, wanderMax);
            // Animator 在人物子物体上，根物体收不到 OnAnimatorMove；位移统一由此脚本处理。
            if (animator) animator.applyRootMotion = false;
        }

        private void Update()
        {
            if (!player && Camera.main) player = Camera.main.transform;

            walking = hasTarget && !IsPostureLocked;
            if (walking)
            {
                var flat = transform.position - target;
                flat.y = 0f;
                if (flat.magnitude <= (handHeld ? .68f : arriveDistance)) hasTarget = false;
            }
            walking = hasTarget && !IsPostureLocked;

            animator.SetFloat("Speed", walking ? 1f : 0f);

            if (walking)
            {
                var dir = target - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.02f)
                {
                    var facing = handHeld && handMotion.sqrMagnitude > .04f ? handMotion : dir;
                    var want = Quaternion.LookRotation(facing.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-Time.deltaTime * 5f));
                    float stopDistance = handHeld ? .68f : arriveDistance;
                    var step = Mathf.Min(manualSpeed * (handHeld ? 1.4f : 1f) * Time.deltaTime, Mathf.Max(0f, dir.magnitude - stopDistance));
                    transform.position += dir.normalized * step;
                }
            }

            if (followMode && !handHeld && player && !walking && !IsPostureLocked)
            {
                var distance = Vector3.Distance(player.position, transform.position);
                if (distance > followDistance * 1.35f) MoveTo(player.position);
            }

            if (wanderEnabled && !handHeld && !walking && !IsPostureLocked && !followMode && Time.time > nextWander)
                PickWanderTarget(wanderRadius);

            if (performance)
            {
                performance.proceduralBody = !walking && !sitting && !(GetComponent<CharacterPrayer>() && GetComponent<CharacterPrayer>().IsActive);
                performance.facePlayer = !walking && !sitting;
            }
        }

        private void PickWanderTarget(float radius)
        {
            nextWander = Time.time + Random.Range(wanderMin, wanderMax);
            for (int attempt = 0; attempt < 5; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(radius * 0.45f, radius);
                var candidate = home + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
                var from = transform.position + Vector3.up * 1.1f;
                var to = candidate + Vector3.up * 1.1f;
                if (Physics.Raycast(from, (to - from).normalized, out var hit, Vector3.Distance(from, to)))
                    continue;
                target = candidate;
                hasTarget = true;
                return;
            }
        }

        public void MoveTo(Vector3 position)
        {
            if (handHeld) return;
            if (IsPostureLocked) Stand();
            invitingPetting = false;
            target = position;
            hasTarget = true;
            followMode = false;
        }

        public void ComeHere()
        {
            if (!player && Camera.main) player = Camera.main.transform;
            if (!player) return;
            MoveTo(player.position);
        }

        public bool CanMoveAutonomously => !handHeld && !walking && !IsPostureLocked && Time.time >= nextMoveAllowed;

        // 模型自主决定走近玩家；带冷却，避免一直黏着。
        public bool ApproachPlayer()
        {
            if (handHeld) return false;
            // 走近通常是玩家明确要求的：冷却比自主走动短，正在走动时也允许立刻改道来玩家身边。
            if (Time.time < nextApproachAllowed) return false;
            if (!player && Camera.main) player = Camera.main.transform;
            if (!player) return false;
            nextApproachAllowed = Time.time + approachCooldown;
            nextMoveAllowed = Time.time + moveCooldown;
            MoveTo(player.position);
            return true;
        }

        // 模型自主决定在原位附近换个地方站；带冷却、幅度小。
        public bool WalkAround()
        {
            if (!CanMoveAutonomously) return false;
            nextMoveAllowed = Time.time + moveCooldown;
            invitingPetting = false;
            followMode = false;
            home = transform.position;
            PickWanderTarget(walkAroundRadius);
            return hasTarget;
        }

        public void ComeForPetting()
        {
            if (!player && Camera.main) player = Camera.main.transform;
            if (!player) return;
            var position = player.position;
            position.y = transform.position.y;
            MoveTo(position);
            invitingPetting = true;
        }

        public void Sit()
        {
            var prayer = GetComponent<CharacterPrayer>();
            if (prayer && prayer.IsActive) { prayer.Rise(); return; }
            if (handHeld) return;
            if (sitting) return;
            GetComponent<CharacterPrayer>()?.Rise();
            crouching = false;
            hasTarget = false;
            followMode = false;
            sitting = true;
            animator.speed = 1f;
            animator.SetBool("Sitting", true);
            animator.SetTrigger("Sit");
        }

        public bool Crouch()
        {
            var prayer = GetComponent<CharacterPrayer>();
            if (prayer && prayer.IsActive) return false;
            if (handHeld || crouching || !GetComponent<CharacterCrouchPose>()) return false;
            sitting = false;
            crouching = true;
            hasTarget = false;
            walking = false;
            followMode = false;
            invitingPetting = false;
            animator.speed = 1f;
            animator.SetBool("Sitting", false);
            animator.ResetTrigger("Sit");
            animator.ResetTrigger("Stand");
            animator.CrossFadeInFixedTime(animator.GetBool("Speaking") ? "Talk" : "Idle", .2f, 0);
            return true;
        }

        public void Stand()
        {
            if (!IsPostureLocked) return;
            GetComponent<CharacterPrayer>()?.Rise();
            bool wasSitting = sitting;
            sitting = false;
            crouching = false;
            hasTarget = false;
            animator.speed = 1f;
            animator.SetBool("Sitting", false);
            if (wasSitting) animator.SetTrigger("Stand");
        }

        public void Stop()
        {
            hasTarget = false;
            followMode = false;
            invitingPetting = false;
    }

        public void Follow()
        {
            if (handHeld) return;
            invitingPetting = false;
            followMode = true;
            if (IsPostureLocked) Stand();
        }

        public void Unfollow()
        {
            followMode = false;
        }

        // 玩家牵手期间，移动目标只由真实手部位置更新，避免语音动作抢夺控制。
        public void BeginHandHold()
        {
            if (IsPostureLocked) Stand();
            handHeld = true;
            followMode = false;
            invitingPetting = false;
            animator.speed = 1f;
        }

        public void UpdateHandHoldTarget(Vector3 handPosition, Vector3 motion)
        {
            if (!handHeld) return;
            handPosition.y = transform.position.y;
            handMotion = Vector3.Lerp(handMotion, Vector3.ProjectOnPlane(motion, Vector3.up), 1f - Mathf.Exp(-Time.deltaTime * 8f));
            Vector3 delta = handPosition - transform.position;
            if (delta.sqrMagnitude > .85f * .85f)
            {
                target = handPosition;
                hasTarget = true;
            }
            else hasTarget = false;
        }

        public void EndHandHold()
        {
            handHeld = false;
            hasTarget = false;
            handMotion = Vector3.zero;
        }

        public void Talk()
        {
            animator.speed = 1f;
            SetSpeaking(true);
        }

        public void Secret()
        {
            hasTarget = false;
            followMode = false;
            animator.speed = 1f;
            if (!animator.runtimeAnimatorController) return;
            animator.SetBool("SecretPlaying", true);
            animator.ResetTrigger("Talk");
            animator.SetTrigger("Talk");
            StartCoroutine(ReleaseSecretAnimationLock());
        }

        private IEnumerator ReleaseSecretAnimationLock()
        {
            float enterDeadline = Time.time + 2f;
            while (animator && Time.time < enterDeadline && !animator.GetCurrentAnimatorStateInfo(0).IsName("Secret"))
                yield return null;
            if (!animator) yield break;
            var secretState = animator.GetCurrentAnimatorStateInfo(0);
            float finishDeadline = Time.time + Mathf.Max(3f, secretState.length * 1.15f);
            while (Time.time < finishDeadline)
            {
                secretState = animator.GetCurrentAnimatorStateInfo(0);
                if (!secretState.IsName("Secret") || secretState.normalizedTime >= .9f) break;
                yield return null;
            }
            if (!animator || !animator.runtimeAnimatorController) yield break;
            animator.SetBool("SecretPlaying", false);
            secretState = animator.GetCurrentAnimatorStateInfo(0);
            if (secretState.IsName("Secret"))
            {
                string nextState = walking ? "Walk" : animator.GetBool("Speaking") ? "Talk" : "Idle";
                animator.CrossFade(nextState, .18f, 0);
            }
        }

        public void SetSpeaking(bool value)
        {
            if (!animator || !animator.runtimeAnimatorController) return;
            animator.SetBool("Speaking", value);
        }

    }
}

