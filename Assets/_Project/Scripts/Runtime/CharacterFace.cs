using UnityEngine;

namespace RainyApartmentVR
{
    public class CharacterFace : MonoBehaviour
    {
        [Header("口型")]
        public bool talking;
        [Range(0f, 1f)] public float talkAmount = 1f;

        [Header("眨眼")]
        public float blinkMin = 2.6f;
        public float blinkMax = 5.2f;

        [Header("入场问候")]
        public bool greetOnStart = true;
        public float greetDelay = 3.5f;
        public float greetTalkSeconds = 2.4f;

        [Header("走近问候")]
        public float greetRadius = 2.6f;
        public float greetCooldown = 16f;

        SkinnedMeshRenderer face;
        Animator animator;
        int blinkL = -1;
        int blinkR = -1;
        int mouth = -1;
        float nextBlink;
        float blinkStart = -1f;
        float talkSeed;
        float greetAt = -1f;
        float talkUntil = -1f;
        float nextGreet = -1f;
        Transform player;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>(true);
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name == "output") { face = r; break; }
            }
            if (!face || !face.sharedMesh)
            {
                enabled = false;
                return;
            }
            blinkL = face.sharedMesh.GetBlendShapeIndex("blink_L");
            blinkR = face.sharedMesh.GetBlendShapeIndex("blink_R");
            mouth = face.sharedMesh.GetBlendShapeIndex("mouth_open");
            talkSeed = Random.value * 10f;
            nextBlink = Time.time + Random.Range(1.0f, 2.2f);
            if (greetOnStart) greetAt = Time.time + greetDelay;
            nextGreet = Time.time + 6f;
        }

        public void Greet()
        {
            Greet(greetTalkSeconds);
        }

        public void Greet(float talkSeconds)
        {
            if (talkSeconds > 0f) talkUntil = Time.time + talkSeconds;
            Debug.Log("男主：近距离问候");
        }

        public void TalkFor(float seconds) => talkUntil = Mathf.Max(talkUntil, Time.time + seconds);

        void Update()
        {
            var now = Time.time;
            if (greetAt > 0f && now >= greetAt)
            {
                greetAt = -1f;
                Greet();
            }

            if (!player)
            {
                var cam = Camera.main;
                if (cam) player = cam.transform;
            }
            if (player && now >= nextGreet)
            {
                var playerPos = new Vector3(player.position.x, 0f, player.position.z);
                var selfPos = new Vector3(transform.position.x, 0f, transform.position.z);
                if (Vector3.Distance(playerPos, selfPos) < greetRadius)
                {
                    nextGreet = now + greetCooldown;
                    Greet();
                }
            }

            var b = 0f;
            if (blinkStart < 0f)
            {
                if (now >= nextBlink) blinkStart = now;
            }
            else
            {
                var t = (now - blinkStart) / 0.15f;
                if (t >= 1f)
                {
                    blinkStart = -1f;
                    nextBlink = now + Random.Range(blinkMin, blinkMax);
                }
                else b = Mathf.Sin(t * Mathf.PI);
            }
            if (blinkL >= 0) face.SetBlendShapeWeight(blinkL, b * 100f);
            if (blinkR >= 0) face.SetBlendShapeWeight(blinkR, b * 100f);

            var talkingNow = talking || now < talkUntil;
            var m = talkingNow ? (0.25f + 0.75f * Mathf.PerlinNoise(now * 7.5f, talkSeed)) * talkAmount : 0f;
            if (mouth >= 0)
            {
                var cur = face.GetBlendShapeWeight(mouth);
                face.SetBlendShapeWeight(mouth, Mathf.Lerp(cur, m * 100f, Time.deltaTime * 14f));
            }
        }
    }
}
