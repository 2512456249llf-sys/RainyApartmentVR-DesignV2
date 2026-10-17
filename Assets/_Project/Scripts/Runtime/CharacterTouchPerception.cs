using UnityEngine;
namespace RainyApartmentVR {
    public sealed class CharacterTouchPerception : MonoBehaviour
    {
        private readonly TouchContactAccumulator left = new TouchContactAccumulator(), right = new TouchContactAccumulator();
        private CharacterTouchSurfaces surfaces;
        private PerceptionEventHub hub;
        private void Awake()
        {
            surfaces = GetComponent<CharacterTouchSurfaces>(); hub = GetComponent<PerceptionEventHub>();
            left.Emitted += Publish; right.Emitted += Publish;
        }
        private void Publish(TouchEvent value) { if (hub) hub.Publish(value); }
        public void Report(bool isLeft, Collider surface, Vector3 point, Vector3 movement, Vector3 normal, float time)
        {
            var accumulator = isLeft ? left : right;
            // 非待机仍可被本地触摸壳碰撞，但不发布自主反馈请求。
            if (!CharacterPoseGate.CanTouch(gameObject)) { accumulator.Reset(); return; }
            accumulator.Sample(surfaces ? surfaces.BodyPart(surface) : TouchBodyPart.None, point,
                Vector3.ProjectOnPlane(movement, normal), time, isLeft);
        }
    }
}
