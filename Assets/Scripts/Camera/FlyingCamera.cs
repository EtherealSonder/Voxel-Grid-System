using UnityEngine;
using UnityEngine.InputSystem;

public class FlyingCamera : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float fastMultiplier = 3f;

    [Header("Look")]
    public float mouseSensitivity = 0.15f;
    public bool lockCursor = true;

    private const float Padding = 0.1f;

    private float yaw;
    private float pitch;

    private Keyboard keyboard;
    private Mouse mouse;

    private void Awake()
    {
        keyboard = Keyboard.current;
        mouse = Mouse.current;
    }

    private void Start()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (keyboard == null || mouse == null)
            return;

        if (WorldManager.Instance == null || WorldManager.Instance.gridVisualizer == null)
            return;

        HandleLook();
        HandleMovement();
        ClampToWorldBounds();
    }

    private void HandleLook()
    {
        Vector2 delta = mouse.delta.ReadValue();

        yaw += delta.x * mouseSensitivity;
        pitch -= delta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -85f, 85f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleMovement()
    {
        float speed = moveSpeed;
        if (keyboard.leftShiftKey.isPressed)
            speed *= fastMultiplier;

        Vector3 move = Vector3.zero;

        if (keyboard.wKey.isPressed) move += transform.forward;
        if (keyboard.sKey.isPressed) move -= transform.forward;
        if (keyboard.dKey.isPressed) move += transform.right;
        if (keyboard.aKey.isPressed) move -= transform.right;

        if (keyboard.spaceKey.isPressed) move += Vector3.up;
        if (keyboard.leftCtrlKey.isPressed) move -= Vector3.up;

        transform.position += move.normalized * speed * Time.deltaTime;
    }

    private void ClampToWorldBounds()
    {
        GridVisualizer world = WorldManager.Instance.gridVisualizer;

        Bounds localBounds = world.LocalBounds;

        Vector3 localPos = world.transform.InverseTransformPoint(transform.position);

        localPos.x = Mathf.Clamp(localPos.x, Padding, localBounds.size.x - Padding);
        localPos.y = Mathf.Clamp(localPos.y, Padding, localBounds.size.y - Padding);
        localPos.z = Mathf.Clamp(localPos.z, Padding, localBounds.size.z - Padding);

        transform.position = world.transform.TransformPoint(localPos);
    }
}
