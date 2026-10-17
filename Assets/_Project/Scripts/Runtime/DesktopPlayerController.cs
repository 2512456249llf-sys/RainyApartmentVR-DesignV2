using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace RainyApartmentVR
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DesktopPlayerController : MonoBehaviour
    {
        [SerializeField] private Transform viewTransform;
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float gravity = -18f;

        private CharacterController characterController;
        private float verticalSpeed;
        private float pitch;
        private bool cursorCaptured;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (viewTransform == null)
            {
                Camera childCamera = GetComponentInChildren<Camera>();
                viewTransform = childCamera != null ? childCamera.transform : transform;
            }
        }

        private void Start()
        {
            SetCursorCaptured(true);
        }

        private void Update()
        {
            HandleCursor();
            HandleLook();
            HandleMovement();
        }

        public void Configure(Transform cameraTransform)
        {
            viewTransform = cameraTransform;
        }

        private void HandleCursor()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorCaptured(false);
            }
            else if (!cursorCaptured && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetCursorCaptured(true);
            }
        }

        private void HandleLook()
        {
            if (!cursorCaptured || Mouse.current == null)
            {
                return;
            }

            Vector2 mouseDelta = Mouse.current.delta.ReadValue() * lookSensitivity;
            pitch = Mathf.Clamp(pitch - mouseDelta.y, -85f, 85f);
            viewTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.Rotate(Vector3.up, mouseDelta.x, Space.World);
        }

        private void HandleMovement()
        {
            Vector2 input = Vector2.zero;
            if (Keyboard.current != null)
            {
                input.x = ReadAxis(Keyboard.current.aKey, Keyboard.current.dKey);
                input.y = ReadAxis(Keyboard.current.sKey, Keyboard.current.wKey);
            }

            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * moveSpeed;

            if (characterController.isGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = -1.5f;
            }
            else
            {
                verticalSpeed += gravity * Time.deltaTime;
            }

            characterController.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        private static float ReadAxis(KeyControl negative, KeyControl positive)
        {
            float value = 0f;
            if (negative.isPressed)
            {
                value -= 1f;
            }

            if (positive.isPressed)
            {
                value += 1f;
            }

            return value;
        }

        private void SetCursorCaptured(bool captured)
        {
            cursorCaptured = captured;
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
