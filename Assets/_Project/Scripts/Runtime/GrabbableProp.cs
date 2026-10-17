using UnityEngine;

namespace RainyApartmentVR
{
    // 可拿取、随身携带、再放下的物品。用现成的 E 键 / 手柄射线交互，桌面和 VR 都能用。
    public sealed class GrabbableProp : MonoBehaviour, IPlayerInteractable
    {
        public float holdDistance = 0.55f;
        public float followSharpness = 14f;
        public float turnSharpness = 6f;
        public Vector3 holdOffset = new Vector3(0f, -0.12f, 0f);
        // 松手后不掉落、原地浮空，方便任意摆放（相框、洗衣粉等装饰物用）。
        public bool stayWhereReleased = true;
        // 抓在手里时是否强制转向玩家正前方；关掉则保持相对朝向，便于摆放。
        public bool faceHolder = false;

        private Rigidbody body;
        private Transform holder;
        private bool held;
        private Quaternion holdGrabRotation = Quaternion.identity;

        public bool IsHeld => held;
        public Transform Holder => holder;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public void Interact()
        {
            if (held) Drop();
            else PickUp();
        }

        public void PickUp()
        {
            var camera = Camera.main;
            if (!camera) return;
            holder = camera.transform;
            held = true;
            holdGrabRotation = Quaternion.Inverse(holder.rotation) * transform.rotation;
            if (body)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        public void Drop()
        {
            held = false;
            holder = null;
            if (body)
            {
                if (stayWhereReleased)
                {
                    // 留在原处浮空，不下落
                    body.isKinematic = true;
                    body.useGravity = false;
                }
                else
                {
                    body.isKinematic = false;
                    body.useGravity = true;
                }
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            GetComponent<PersistentPlacement>()?.SaveNow();
        }

        private void FixedUpdate()
        {
            if (!held || !holder) return;
            var target = holder.position + holder.rotation * (Vector3.forward * holdDistance + holdOffset);
            var wanted = faceHolder
                ? Quaternion.LookRotation(holder.forward, Vector3.up) * Quaternion.Euler(0f, 180f, 0f)
                : holder.rotation * holdGrabRotation;
            if (body)
            {
                body.MovePosition(Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-followSharpness * Time.fixedDeltaTime)));
                body.MoveRotation(Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-turnSharpness * Time.fixedDeltaTime)));
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-followSharpness * Time.fixedDeltaTime));
                transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-turnSharpness * Time.fixedDeltaTime));
            }
        }
    }
}
