using UnityEngine;

namespace RainyApartmentVR
{
    // 只约束被牵住的一只手；躯干位移继续由现有走路状态驱动。
    [DefaultExecutionOrder(180)]
    public sealed class CharacterHandHold : MonoBehaviour
    {
        public float grabRadius = .19f;
        public float releaseGrip = .32f;
        public float releaseDistance = 2.2f;
        public Transform leftGrabPoint;
        public Transform rightGrabPoint;

        private Animator animator;
        private CharacterBehaviour behaviour;
        private VRHandPose[] playerHands;
        private VRHandPose heldBy;
        private Transform upper;
        private Transform lower;
        private Transform wrist;
        private Vector3 lastHandPosition;

        public bool IsHolding => heldBy;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>(true);
            behaviour = GetComponent<CharacterBehaviour>();
            playerHands = FindObjectsByType<VRHandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (!animator || !animator.isHuman)
            {
                Debug.LogWarning("人物牵手：没有可用的人形骨架，已停用牵手。");
                enabled = false;
                return;
            }
            EnsureGrabPoint(ref leftGrabPoint, HumanBodyBones.LeftHand, "左手牵握点");
            EnsureGrabPoint(ref rightGrabPoint, HumanBodyBones.RightHand, "右手牵握点");
        }

        private void EnsureGrabPoint(ref Transform point, HumanBodyBones bone, string name)
        {
            if (point) return;
            Transform hand = animator.GetBoneTransform(bone);
            if (!hand) return;
            var marker = new GameObject(name);
            marker.transform.SetParent(hand, false);
            marker.transform.localPosition = Vector3.zero;
            var sphere = marker.AddComponent<SphereCollider>();
            sphere.radius = grabRadius;
            sphere.isTrigger = true;
            var hint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hint.name = "牵手提示";
            hint.transform.SetParent(marker.transform, false);
            hint.transform.localScale = Vector3.one * .05f;
            var hintCollider = hint.GetComponent<Collider>();
            hintCollider.enabled = false;
            Destroy(hintCollider);
            var renderer = hint.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader)
            {
                var material = new Material(shader);
                material.color = new Color(.9f, .72f, .46f);
                renderer.material = material;
            }
            renderer.enabled = false;
            point = marker.transform;
        }

        private void Update()
        {
            if (heldBy)
            {
                if (!heldBy.gameObject.activeInHierarchy || heldBy.IsHoldingObject || heldBy.GripValue < releaseGrip ||
                    Vector3.Distance(heldBy.transform.position, transform.position) > releaseDistance)
                {
                    Release();
                    return;
                }
                Vector3 handPosition = heldBy.transform.position;
                Vector3 motion = (handPosition - lastHandPosition) / Mathf.Max(Time.deltaTime, .001f);
                lastHandPosition = handPosition;
                if (behaviour) behaviour.UpdateHandHoldTarget(handPosition, motion);
                UpdateHints();
                return;
            }
            if (playerHands == null || playerHands.Length == 0)
                playerHands = FindObjectsByType<VRHandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            UpdateHints();
            foreach (var hand in playerHands)
            {
                if (!hand || !hand.gameObject.activeInHierarchy || hand.IsHoldingObject || hand.GripValue < .68f) continue;
                Transform point = ClosestPoint(hand.transform.position);
                if (!point || Vector3.Distance(hand.transform.position, point.position) > grabRadius) continue;
                Hold(hand, point == leftGrabPoint);
                break;
            }
        }

        private Transform ClosestPoint(Vector3 position)
        {
            if (!leftGrabPoint) return rightGrabPoint;
            if (!rightGrabPoint) return leftGrabPoint;
            return (position - leftGrabPoint.position).sqrMagnitude <=
                   (position - rightGrabPoint.position).sqrMagnitude ? leftGrabPoint : rightGrabPoint;
        }

        private void Hold(VRHandPose playerHand, bool left)
        {
            if (!CharacterPoseGate.CanTouch(gameObject)) return;
            upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            wrist = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (!upper || !lower || !wrist) return;
            heldBy = playerHand;
            lastHandPosition = playerHand.transform.position;
            if (behaviour) behaviour.BeginHandHold();
        }

        private void UpdateHints()
        {
            SetHint(leftGrabPoint);
            SetHint(rightGrabPoint);
        }

        private void SetHint(Transform point)
        {
            if (!point) return;
            var renderer = point.GetComponentInChildren<Renderer>(true);
            if (!renderer) return;
            bool nearby = false;
            if (playerHands != null)
                foreach (var hand in playerHands)
                    if (hand && hand.gameObject.activeInHierarchy &&
                        Vector3.Distance(hand.transform.position, point.position) < .45f)
                    { nearby = true; break; }
            renderer.enabled = nearby;
        }

        private void LateUpdate()
        {
            if (!heldBy || !upper || !lower || !wrist) return;
            Vector3 shoulder = upper.position;
            Vector3 destination = heldBy.transform.position;
            float upperLength = Vector3.Distance(upper.position, lower.position);
            float lowerLength = Vector3.Distance(lower.position, wrist.position);
            if (upperLength < .01f || lowerLength < .01f) return;
            Vector3 reach = destination - shoulder;
            float distance = Mathf.Clamp(reach.magnitude, .08f, (upperLength + lowerLength) * .96f);
            Vector3 direction = reach.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(lower.position - shoulder, direction).normalized;
            if (bend.sqrMagnitude < .01f)
                bend = Vector3.ProjectOnPlane(transform.forward, direction).normalized;
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float side = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 elbowTarget = shoulder + direction * along + bend * side;
            Aim(upper, lower, elbowTarget);
            Aim(lower, wrist, shoulder + direction * distance);
        }

        private static void Aim(Transform bone, Transform child, Vector3 target)
        {
            Vector3 current = child.position - bone.position;
            Vector3 desired = target - bone.position;
            if (current.sqrMagnitude < .0001f || desired.sqrMagnitude < .0001f) return;
            bone.rotation = Quaternion.FromToRotation(current, desired) * bone.rotation;
        }

        public void Release()
        {
            heldBy = null;
            if (behaviour) behaviour.EndHandHold();
        }

        private void OnDisable()
        {
            if (heldBy) Release();
        }
    }
}
