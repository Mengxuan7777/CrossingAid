using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class XRPlayerMover : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Bind to <XRController>{LeftHand}/thumbstick")]
    public InputActionProperty moveAction;

    [Tooltip("Bind to <XRController>{RightHand}/thumbstick")]
    public InputActionProperty turnAction;

    [Tooltip("Stick magnitude below this threshold is ignored.")]
    [Range(0f, 0.5f)]
    public float deadzone = 0.1f;

    [Header("Speed")]
    [Tooltip("Speed when stick is barely past the deadzone (m/s).")]
    public float minSpeed = 0.2f;

    [Tooltip("Speed when stick is fully pressed (m/s). Average pedestrian ~1.4 m/s.")]
    public float maxSpeed = 2f;

    [Tooltip("Power curve exponent. 1 = linear. 2 = quadratic (speed ramps faster at high pressure). 3 = more aggressive.")]
    [Range(1f, 4f)]
    public float speedCurveExponent = 2f;

    [Tooltip("Rotation speed in degrees per second at full stick deflection.")]
    public float turnSpeed = 15f;

    public float gravity = -9.81f;

    [Header("References")]
    [Tooltip("Assign the XR Camera so movement follows head direction.")]
    public Transform cameraTransform;

    private CharacterController _cc;
    private float _verticalVelocity;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        if (moveAction.action != null) moveAction.action.Enable();
        if (turnAction.action != null) turnAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction.action != null) moveAction.action.Disable();
        if (turnAction.action != null) turnAction.action.Disable();
    }

    private void Update()
    {
        if (moveAction.action == null || cameraTransform == null) return;

        Vector2 input = moveAction.action.ReadValue<Vector2>();
        float pressure = input.magnitude;

        if (_cc.isGrounded)
            _verticalVelocity = -0.5f;
        else
            _verticalVelocity += gravity * Time.deltaTime;

        Vector3 move = Vector3.zero;

        if (pressure > deadzone)
        {
            float t = Mathf.InverseLerp(deadzone, 1f, pressure);
            float speed = Mathf.Lerp(minSpeed, maxSpeed, Mathf.Pow(t, speedCurveExponent));

            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 direction = (forward * input.y + right * input.x).normalized;
            move = direction * speed;
        }

        move.y = _verticalVelocity;
        _cc.Move(move * Time.deltaTime);

        if (turnAction.action != null)
        {
            float turnInput = turnAction.action.ReadValue<Vector2>().x;
            if (Mathf.Abs(turnInput) > deadzone)
                transform.Rotate(0f, turnInput * turnSpeed * Time.deltaTime, 0f, Space.World);
        }
    }
}
