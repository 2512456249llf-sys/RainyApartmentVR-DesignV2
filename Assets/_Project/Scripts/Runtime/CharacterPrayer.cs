using System.Collections;
using UnityEngine;
namespace RainyApartmentVR
{
    // 原片段本身包含跪拜与起身，顺向播放并保留烘焙到姿态的竖直位移。
    [DefaultExecutionOrder(250)]
    public sealed class CharacterPrayer : MonoBehaviour
    {
        public Animator animator;
        public bool IsActive { get; private set; }
        public bool IsRising { get; private set; }
        private Vector3 modelLocalPosition;
        private Coroutine sequence;
        private Transform[] feet;
        private void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>(true);
            if (animator) modelLocalPosition = animator.transform.localPosition;
            if (animator && animator.isHuman) feet = new[]
            {
                animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot),
                animator.GetBoneTransform(HumanBodyBones.LeftToes), animator.GetBoneTransform(HumanBodyBones.RightToes)
            };
        }
        public bool Begin()
        {
            var behaviour = GetComponent<CharacterBehaviour>();
            if (IsActive || !animator || !animator.isHuman || !animator.HasState(0, Animator.StringToHash("Base Layer.Prayer")) ||
                (behaviour && behaviour.IsHandHeld) || (GetComponent<CharacterInteraction>() && GetComponent<CharacterInteraction>().IsHugging)) return false;
            if (behaviour) { behaviour.Stand(); behaviour.Stop(); }
            modelLocalPosition = animator.transform.localPosition;
            IsActive = true; IsRising = false;
            animator.speed = 1;
            animator.SetBool("Praying", true); animator.SetBool("Sitting", false); animator.SetFloat("Speed", 0);
            animator.ResetTrigger("Sit"); animator.ResetTrigger("Stand");
            var layer = animator.GetLayerIndex("自然动作"); if (layer >= 0) animator.SetLayerWeight(layer, 0);
            animator.CrossFadeInFixedTime("Prayer", .35f, 0, 0);
            sequence = StartCoroutine(FinishSequence());
            return true;
        }
        public void Rise()
        {
            if (!IsActive || IsRising) return;
            IsRising = true;
            // 源片段后段就是实际起身，不能将整段倒放成再次跪下。
            var state = animator.GetCurrentAnimatorStateInfo(0);
            float from = state.IsName("Prayer") ? Mathf.Clamp(state.normalizedTime, .65f, .99f) : .65f;
            animator.CrossFade("PrayerRise", .035f, 0, from);
        }
        private IEnumerator FinishSequence()
        {
            float enterDeadline = Time.time + 2;
            while (animator && !IsPrayerState() && Time.time < enterDeadline) yield return null;
            float deadline = Time.time + 12;
            while (animator && Time.time < deadline)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (!IsPrayerState()) break;
                if (state.normalizedTime >= .65f) IsRising = true;
                if (state.normalizedTime >= .999f) break;
                yield return null;
            }
            if (animator)
            {
                animator.transform.localPosition = modelLocalPosition;
                animator.CrossFadeInFixedTime(animator.GetBool("Speaking") ? "Talk" : "Idle", .3f, 0, 0);
                yield return new WaitForSeconds(.35f);
                animator.SetBool("Praying", false);
            }
            IsActive = IsRising = false; sequence = null;
        }
        private bool IsPrayerState()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            return state.IsName("Prayer") || state.IsName("PrayerRise");
        }
        private void LateUpdate()
        {
            if (!IsActive || !animator) return;
            animator.transform.localPosition = modelLocalPosition;
            if (!IsRising || feet == null) return;
            float lowest = float.PositiveInfinity;
            foreach (var foot in feet) if (foot) lowest = Mathf.Min(lowest, foot.position.y);
            // 保留原关节姿态，整体补偿源动作短暂的几厘米地面误差。
            float lift = Mathf.Clamp(transform.position.y + .002f - lowest, 0, .12f);
            animator.transform.position += Vector3.up * lift;
        }
        private void OnDisable()
        {
            if (sequence != null) StopCoroutine(sequence); sequence = null;
            if (animator && IsActive) { animator.transform.localPosition = modelLocalPosition; animator.SetBool("Praying", false); }
            IsActive = IsRising = false;
        }
    }
}
