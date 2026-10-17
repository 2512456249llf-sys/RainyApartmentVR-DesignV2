using System.Collections.Generic;
using UnityEngine;

namespace RainyApartmentVR
{
    // 为 VR 手部提供略浮在皮肤外的触摸壳，并将轻抚动作转成小幅骨骼回弹。
    [DefaultExecutionOrder(200)]
    public sealed class CharacterTouchSurfaces : MonoBehaviour
    {
        private enum TouchZone { Body, Head, LeftArm, RightArm }

        private sealed class BoneSpring
        {
            public Transform bone;
            public float maximumAngle;
            public Vector3 current;
            public Vector3 target;
            public Vector3 velocity;
            public Quaternion applied = Quaternion.identity;
            public Quaternion input = Quaternion.identity;
            public Quaternion output = Quaternion.identity;
        }

        private readonly List<Collider> surfaces = new List<Collider>();
        private readonly Dictionary<Collider, TouchZone> zones = new Dictionary<Collider, TouchZone>();
        private readonly Dictionary<Collider, TouchBodyPart> parts = new Dictionary<Collider, TouchBodyPart>();
        public TouchBodyPart BodyPart(Collider surface) => surface && parts.TryGetValue(surface, out var part) ? part : TouchBodyPart.None;
        private CharacterVoicePerformance performance;
        private BoneSpring[] responseBones;

        public IReadOnlyList<Collider> Surfaces => surfaces;

        private void Awake()
        {
            performance = GetComponent<CharacterVoicePerformance>();
            var animator = GetComponentInChildren<Animator>(true);
            if (!animator || !animator.avatar || !animator.avatar.isHuman)
            {
                var root = GetComponent<CapsuleCollider>();
                if (root && root.enabled) AddSurface(root, TouchZone.Body);
                return;
            }

            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            var upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);

            if (hips && spine) AddCapsule(hips, spine, "触摸面_腰腹", .165f, TouchZone.Body);
            if (spine && chest) AddCapsule(spine, chest, "触摸面_胸口", .185f, TouchZone.Body);
            var upperTorso = upperChest ? upperChest : neck;
            if (chest && upperTorso) AddCapsule(chest, upperTorso, "触摸面_上胸", .15f, TouchZone.Body);

            // 缺少躯干骨骼时才退回角色胶囊，避免正常骨架被过宽的整身碰撞体包住。
            if (surfaces.Count == 0)
            {
                var root = GetComponent<CapsuleCollider>();
                if (root && root.enabled) AddSurface(root, TouchZone.Body);
            }

            if (head) AddHeadSurface(head);
            AddLimb(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, "左上臂", .044f, TouchZone.LeftArm);
            AddLimb(animator, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, "左前臂", .039f, TouchZone.LeftArm);
            AddLimb(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, "右上臂", .044f, TouchZone.RightArm);
            AddLimb(animator, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, "右前臂", .039f, TouchZone.RightArm);
            AddPointSurface(animator.GetBoneTransform(HumanBodyBones.LeftHand), "触摸面_左手", .045f, TouchZone.LeftArm, TouchBodyPart.Hand);
            AddPointSurface(animator.GetBoneTransform(HumanBodyBones.RightHand), "触摸面_右手", .045f, TouchZone.RightArm, TouchBodyPart.Hand);
            AddPointSurface(animator.GetBoneTransform(HumanBodyBones.LeftShoulder), "触摸面_左肩", .065f, TouchZone.Body, TouchBodyPart.Shoulder);
            AddPointSurface(animator.GetBoneTransform(HumanBodyBones.RightShoulder), "触摸面_右肩", .065f, TouchZone.Body, TouchBodyPart.Shoulder);

            responseBones = new[]
            {
                new BoneSpring { bone = head, maximumAngle = 3.5f },
                new BoneSpring { bone = neck, maximumAngle = 2.5f },
                new BoneSpring { bone = chest, maximumAngle = 4f },
                new BoneSpring { bone = spine, maximumAngle = 3f },
                new BoneSpring { bone = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), maximumAngle = 6f },
                new BoneSpring { bone = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), maximumAngle = 7f },
                new BoneSpring { bone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm), maximumAngle = 6f },
                new BoneSpring { bone = animator.GetBoneTransform(HumanBodyBones.RightLowerArm), maximumAngle = 7f },
            };
        }

        private void AddHeadSurface(Transform head)
        {
            var node = FindOrCreateNode(head, "触摸面_头部");
            node.localPosition = new Vector3(0f, .035f, .015f);
            var sphere = node.GetComponent<SphereCollider>();
            if (!sphere) sphere = node.gameObject.AddComponent<SphereCollider>();
            sphere.radius = .112f;
            sphere.isTrigger = true;
            AddSurface(sphere, TouchZone.Head);
        }

        private void AddLimb(Animator animator, HumanBodyBones startBone, HumanBodyBones endBone,
            string name, float radius, TouchZone zone)
        {
            var start = animator.GetBoneTransform(startBone);
            var end = animator.GetBoneTransform(endBone);
            if (start && end) AddCapsule(start, end, "触摸面_" + name, radius, zone);
        }

        private void AddCapsule(Transform start, Transform end, string name, float radius, TouchZone zone)
        {
            if (!start || !end) return;
            var node = FindOrCreateNode(start, name);
            node.localPosition = Vector3.zero;
            node.localRotation = Quaternion.identity;
            var segment = start.InverseTransformPoint(end.position);
            var capsule = node.GetComponent<CapsuleCollider>();
            if (!capsule) capsule = node.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = segment * .5f;
            capsule.radius = radius;
            capsule.height = segment.magnitude + radius * 2f;
            var abs = new Vector3(Mathf.Abs(segment.x), Mathf.Abs(segment.y), Mathf.Abs(segment.z));
            capsule.direction = abs.x > abs.y && abs.x > abs.z ? 0 : abs.y > abs.z ? 1 : 2;
            capsule.isTrigger = true;
            AddSurface(capsule, zone);
        }

        private void AddSurface(Collider surface, TouchZone zone)
        {
            if (!surface || surfaces.Contains(surface)) return;
            surfaces.Add(surface);
            zones[surface] = zone;
            parts[surface] = zone == TouchZone.Head ? TouchBodyPart.Head : zone == TouchZone.Body ? TouchBodyPart.Torso : TouchBodyPart.Arm;
        }
        private void AddPointSurface(Transform bone, string name, float radius, TouchZone zone, TouchBodyPart part)
        {
            if (!bone) return;
            var node = FindOrCreateNode(bone, name);
            var collider = node.GetComponent<SphereCollider>();
            if (!collider) collider = node.gameObject.AddComponent<SphereCollider>();
            collider.radius = radius; collider.isTrigger = true;
            AddSurface(collider, zone); parts[collider] = part;
        }

        private static Transform FindOrCreateNode(Transform parent, string name)
        {
            var node = parent.Find(name);
            if (node) return node;
            node = new GameObject(name).transform;
            node.SetParent(parent, false);
            return node;
        }

        public void NotifyTouched()
        {
            if (performance) performance.RegisterPettingTouch();
        }

        public void NotifyTouched(Collider surface, Vector3 handMovement, Vector3 outwardNormal, bool contactStarted)
        {
            if (!CharacterPoseGate.CanTouch(gameObject)) return;
            NotifyTouched();
            if (!surface || responseBones == null || !zones.TryGetValue(surface, out var zone)) return;

            Vector3 tangent = Vector3.ProjectOnPlane(handMovement, outwardNormal);
            Vector3 slide = transform.InverseTransformDirection(tangent);
            float onset = contactStarted ? 1f : 0f;
            switch (zone)
            {
                case TouchZone.Head:
                    AddSpringTarget(0, new Vector3(-slide.z * 22f, slide.x * 28f, -slide.x * 12f + onset * 1.1f));
                    AddSpringTarget(1, new Vector3(-slide.z * 12f, slide.x * 16f, -slide.x * 7f + onset * .6f));
                    break;
                case TouchZone.Body:
                    AddSpringTarget(2, new Vector3(-slide.z * 24f, slide.x * 30f, -slide.x * 15f + onset * .8f));
                    AddSpringTarget(3, new Vector3(-slide.z * 14f, slide.x * 18f, -slide.x * 9f + onset * .5f));
                    break;
                case TouchZone.LeftArm:
                    AddSpringTarget(4, new Vector3(slide.z * 34f, slide.x * 26f, -slide.x * 36f + onset * .9f));
                    AddSpringTarget(5, new Vector3(slide.z * 42f, slide.x * 20f, onset * .6f));
                    break;
                case TouchZone.RightArm:
                    AddSpringTarget(6, new Vector3(slide.z * 34f, slide.x * 26f, slide.x * 36f - onset * .9f));
                    AddSpringTarget(7, new Vector3(slide.z * 42f, slide.x * 20f, -onset * .6f));
                    break;
            }
        }

        private void AddSpringTarget(int index, Vector3 increment)
        {
            if (responseBones == null || index < 0 || index >= responseBones.Length) return;
            var spring = responseBones[index];
            if (spring == null || !spring.bone) return;
            spring.target = Vector3.ClampMagnitude(spring.target + increment, spring.maximumAngle);
        }

        private void LateUpdate()
        {
            if (responseBones == null) return;
            for (int i = 0; i < responseBones.Length; i++)
            {
                var spring = responseBones[i];
                if (spring == null || !spring.bone) continue;

                // 只撤回仍然存在的上一帧偏移；脸部牵动先在更新阶段撤回自己的偏移。
                if (Quaternion.Angle(spring.bone.localRotation, spring.output) < .01f) spring.bone.localRotation = spring.input;
                if (!CharacterPoseGate.CanTouch(gameObject))
                { spring.current = spring.target = spring.velocity = Vector3.zero; spring.applied = Quaternion.identity; continue; }
                spring.input = spring.bone.localRotation;
                spring.target = Vector3.MoveTowards(spring.target, Vector3.zero, 14f * Time.deltaTime);
                spring.current = Vector3.SmoothDamp(spring.current, spring.target, ref spring.velocity, .14f, Mathf.Infinity, Time.deltaTime);
                spring.applied = Quaternion.Euler(spring.current);
                spring.bone.localRotation = spring.bone.localRotation * spring.applied;
                spring.output = spring.bone.localRotation;
            }
        }

        private void OnDisable()
        {
            if (responseBones == null) return;
            foreach (var spring in responseBones)
            {
                if (spring == null || !spring.bone) continue;
                if (Quaternion.Angle(spring.bone.localRotation, spring.output) < .01f) spring.bone.localRotation = spring.input;
                spring.applied = Quaternion.identity;
            }
        }
    }
}
