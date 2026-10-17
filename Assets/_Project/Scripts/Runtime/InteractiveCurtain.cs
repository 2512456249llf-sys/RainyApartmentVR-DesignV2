using UnityEngine;

namespace RainyApartmentVR
{
    // 保留现有布料材质，用轻量弹性网格模拟垂坠、褶皱和拉帘后的摆动。
    public sealed class InteractiveCurtain : MonoBehaviour
    {
        public Transform grabPoint;
        public float grabHeight = 1.45f;
        public float grabRadius = .18f;
        public float openAmount;

        private MeshFilter filter;
        private Mesh workingMesh;
        private Vector3[] restVertices;
        private Vector3[] vertices;
        private VRHandPose[] hands;
        private VRHandPose holder;
        private float width;
        private float height;
        private float desiredOpen;
        private float openVelocity;
        private float sway;
        private float swayVelocity;
        private float heldAt;
        private float handAt;

        public bool IsHeld => holder;

        private void Awake()
        {
            filter = GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) { enabled = false; return; }
            workingMesh = Instantiate(filter.sharedMesh);
            workingMesh.name = filter.sharedMesh.name + "_运行时布料";
            workingMesh.MarkDynamic();
            filter.mesh = workingMesh;
            restVertices = workingMesh.vertices;
            vertices = new Vector3[restVertices.Length];
            width = workingMesh.bounds.size.x;
            height = workingMesh.bounds.size.y;
            desiredOpen = Mathf.Clamp01(openAmount);
            hands = FindObjectsByType<VRHandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (!grabPoint)
            {
                var point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                point.name = "窗帘抓握点";
                point.transform.SetParent(transform, false);
                point.transform.localScale = Vector3.one * .065f;
                var collider = point.GetComponent<SphereCollider>();
                collider.isTrigger = true;
                grabPoint = point.transform;
            }
        }

        public void SetOpen01(float value)
        {
            desiredOpen = Mathf.Clamp01(value);
        }

        private void Update()
        {
            if (!workingMesh || !grabPoint) return;
            float rightEdge = width * .5f - openAmount * (width - .17f);
            grabPoint.localPosition = new Vector3(rightEdge, Mathf.Min(grabHeight, height - .22f), .07f);
            if (holder && (!holder.gameObject.activeInHierarchy || holder.GripValue < .35f)) holder = null;
            if (!holder && hands != null)
            {
                foreach (var hand in hands)
                {
                    if (!hand || !hand.gameObject.activeInHierarchy || hand.GripValue < .68f) continue;
                    if (Vector3.Distance(hand.transform.position, grabPoint.position) > grabRadius) continue;
                    holder = hand;
                    heldAt = openAmount;
                    handAt = transform.InverseTransformPoint(hand.transform.position).x;
                    break;
                }
            }
            if (holder)
            {
                float now = transform.InverseTransformPoint(holder.transform.position).x;
                desiredOpen = Mathf.Clamp01(heldAt + (handAt - now) / Mathf.Max(width - .17f, .2f));
            }
            var indicator = grabPoint.GetComponent<Renderer>();
            if (indicator)
            {
                bool nearby = holder;
                if (!nearby && hands != null)
                    foreach (var hand in hands)
                        if (hand && hand.gameObject.activeInHierarchy &&
                            Vector3.Distance(hand.transform.position, grabPoint.position) < .42f) { nearby = true; break; }
                indicator.enabled = nearby;
            }
            float previous = openAmount;
            openAmount = Mathf.SmoothDamp(openAmount, desiredOpen, ref openVelocity, .12f);
            float movement = (openAmount - previous) / Mathf.Max(Time.deltaTime, .001f);
            float wind = Mathf.Sin(Time.time * .9f + transform.position.z * .4f) * .012f;
            sway = Mathf.SmoothDamp(sway, -movement * .075f + wind, ref swayVelocity, .55f);
            Deform();
        }

        private void Deform()
        {
            float visibleWidth = width - openAmount * (width - .17f);
            for (int i = 0; i < vertices.Length; i++)
            {
                var rest = restVertices[i];
                float u = Mathf.Clamp01(rest.x / width + .5f);
                float loose = Mathf.Clamp01(1f - rest.y / height);
                float fold = Mathf.Sin(u * Mathf.PI * 22f) * (.08f + .04f * openAmount);
                float ripple = Mathf.Sin(Time.time * 1.25f + u * 6f + rest.y * .7f) * .018f * loose;
                vertices[i] = new Vector3(
                    -width * .5f + u * visibleWidth + sway * loose * loose * Mathf.Sin(u * Mathf.PI),
                    rest.y - .035f * loose * Mathf.Sin(u * Mathf.PI),
                    fold + (sway * .65f + ripple) * loose);
            }
            workingMesh.vertices = vertices;
            workingMesh.RecalculateNormals();
            workingMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (workingMesh) Destroy(workingMesh);
        }
    }
}
