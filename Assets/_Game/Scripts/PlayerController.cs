using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Простейшее FPS-перемещение: WASD + mouse-look. Работает только в режиме Explore.
/// Камера — дочерний Transform (наклоняется по X, тело поворачивается по Y).
/// Ввод читаем напрямую через New Input System, без action-обёрток (быстрее для джема).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float lookSensitivity = 0.12f;
    [SerializeField] private float gravity = -12f;

    [Header("Звук шагов")]
    [Tooltip("Зациклённый звук шагов — играет, пока игрок идёт по земле.")]
    [SerializeField] private AudioSource footstepsSource;

    private CharacterController controller;
    private float pitch;
    private float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.Mode != GameMode.Explore)
        {
            SetFootsteps(false);
            return;
        }

        Look();
        Move();
    }

    void Look()
    {
        Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        transform.Rotate(Vector3.up, delta.x * lookSensitivity);

        pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -85f, 85f);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        Vector2 input = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed) input.x -= 1f;
        }

        Vector3 move = Vector3.ClampMagnitude(transform.right * input.x + transform.forward * input.y, 1f) * moveSpeed;

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);

        SetFootsteps(input.sqrMagnitude > 0.01f && controller.isGrounded);
    }

    void SetFootsteps(bool walking)
    {
        if (footstepsSource == null) return;
        if (walking && !footstepsSource.isPlaying) footstepsSource.Play();
        else if (!walking && footstepsSource.isPlaying) footstepsSource.Pause();
    }
}
