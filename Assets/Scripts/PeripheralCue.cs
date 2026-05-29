using UnityEngine;

public class PeripheralCue : MonoBehaviour
{
    [Header("References")]
    [Tooltip("XR Origin — source of the body/walking forward direction.")]
    public Transform playerOrigin;

    [Tooltip("XR Camera — source of the gaze/head forward direction.")]
    public Transform playerCamera;

    [Header("Active Zones")]
    [Tooltip("Cue only activates when the player is inside at least one of these trigger colliders.")]
    public Collider[] activeZones;

    [Header("Gaze")]
    [Tooltip("Angle in degrees between body forward and gaze forward above which the player is considered looking away.")]
    [Range(10f, 90f)]
    public float lookAwayAngleThreshold = 45f;

    [Tooltip("Seconds the player must look away before the cue appears.")]
    public float lookAwayDuration = 3f;

    [Header("Pulse")]
    [Tooltip("Pulses per second.")]
    public float pulseFrequency = 1.5f;

    [Tooltip("Scale at the smallest point of each pulse (relative to original size).")]
    [Range(0.1f, 1f)]
    public float pulseMinScale = 0.6f;

    [Tooltip("Scale at the largest point of each pulse (relative to original size).")]
    [Range(0.1f, 2f)]
    public float pulseMaxScale = 1.0f;

    [Header("Renderer")]
    public Renderer cueRenderer;

    [Header("Logger (optional)")]
    public EyeTrackingLogger logger;

    private float _lookAwayTimer;
    private bool _cueVisible;
    private Vector3 _originalScale;

    private void Start()
    {
        cueRenderer ??= GetComponent<Renderer>();
        _originalScale = transform.localScale;
        SetVisible(false);
    }

    private void Update()
    {
        if (!IsInAnyZone())
        {
            _lookAwayTimer = 0f;
            if (_cueVisible) SetVisible(false);
            return;
        }

        if (IsLookingAway())
        {
            _lookAwayTimer += Time.deltaTime;
        }
        else
        {
            _lookAwayTimer = 0f;
            if (_cueVisible) SetVisible(false);
            return;
        }

        if (_lookAwayTimer >= lookAwayDuration)
        {
            if (!_cueVisible) SetVisible(true);
            ApplyPulse();
        }
    }

    private bool IsInAnyZone()
    {
        if (activeZones == null || playerOrigin == null) return false;

        Vector3 pos = playerOrigin.position;
        foreach (var zone in activeZones)
        {
            if (zone != null && zone.enabled && zone.bounds.Contains(pos))
                return true;
        }
        return false;
    }

    private bool IsLookingAway()
    {
        if (playerOrigin == null || playerCamera == null) return false;

        Vector3 bodyForward = playerOrigin.forward;
        bodyForward.y = 0f;
        if (bodyForward.sqrMagnitude < 0.001f) return false;
        bodyForward.Normalize();

        Vector3 gazeForward = playerCamera.forward;
        gazeForward.y = 0f;
        if (gazeForward.sqrMagnitude < 0.001f) return false;
        gazeForward.Normalize();

        return Vector3.Angle(bodyForward, gazeForward) > lookAwayAngleThreshold;
    }

    private void ApplyPulse()
    {
        float t = (Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
        float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale, t);
        transform.localScale = _originalScale * scale;
    }

    private void SetVisible(bool visible)
    {
        _cueVisible = visible;
        if (cueRenderer != null)
            cueRenderer.enabled = visible;

        if (!visible)
            transform.localScale = _originalScale;

        if (visible)
            logger?.WriteCustomEvent("PeripheralCueOn", _lookAwayTimer.ToString("F2"));
        else
            logger?.WriteCustomEvent("PeripheralCueOff", "");
    }
}
