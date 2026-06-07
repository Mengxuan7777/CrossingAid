using UnityEngine;

public enum CueSide { Both, Left, Right }

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

    [Header("Side")]
    [Tooltip("Which physical side this cue is on.\nLeft cue pulses when player looks Right.\nRight cue pulses when player looks Left.\nBoth pulses for any look-away.")]
    public CueSide cueSide = CueSide.Both;

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

    [Header("Color")]
    [Tooltip("Color of the cue. Alpha is applied on top of the gradient fade.")]
    public Color cueColor = Color.yellow;

    [Tooltip("Which UV axis the gradient fades along. U = left-right, V = bottom-top.")]
    public enum GradientAxis { U, V }
    public GradientAxis gradientAxis = GradientAxis.U;

    [Header("Logger (optional)")]
    public EyeTrackingLogger logger;

    private float _lookAwayTimer;
    private bool _cueVisible;
    private Vector3 _originalScale;

    private void Start()
    {
        cueRenderer ??= GetComponent<Renderer>();
        _originalScale = transform.localScale;
        BuildGradientTexture();
        SetVisible(false);
    }

    private void BuildGradientTexture()
    {
        if (cueRenderer == null) return;

        const int size = 256;
        Texture2D tex;
        Color[] pixels;

        if (gradientAxis == GradientAxis.U)
        {
            tex = new Texture2D(size, 1, TextureFormat.RGBA32, false);
            pixels = new Color[size];
            for (int i = 0; i < size; i++)
            {
                float a = 1f - (float)i / (size - 1);
                a = a * a;
                pixels[i] = new Color(cueColor.r, cueColor.g, cueColor.b, cueColor.a * a);
            }
        }
        else
        {
            tex = new Texture2D(1, size, TextureFormat.RGBA32, false);
            pixels = new Color[size];
            for (int i = 0; i < size; i++)
            {
                float a = 1f - (float)i / (size - 1);
                a = a * a;
                pixels[i] = new Color(cueColor.r, cueColor.g, cueColor.b, cueColor.a * a);
            }
        }

        tex.SetPixels(pixels);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();

        Material mat = new Material(cueRenderer.sharedMaterial);
        mat.mainTexture = tex;
        cueRenderer.material = mat;
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
            if (zone == null || !zone.enabled) continue;
            Bounds b = zone.bounds;
            if (pos.x >= b.min.x && pos.x <= b.max.x &&
                pos.z >= b.min.z && pos.z <= b.max.z)
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

        if (Vector3.Angle(bodyForward, gazeForward) <= lookAwayAngleThreshold) return false;

        if (cueSide == CueSide.Both) return true;

        // Positive dot = gaze is to the right of body forward; negative = left
        Vector3 bodyRight = Vector3.Cross(Vector3.up, bodyForward);
        float dotRight = Vector3.Dot(gazeForward, bodyRight);

        return cueSide == CueSide.Right ? dotRight < 0f   // right cue: player looks left
                                        : dotRight > 0f;  // left cue:  player looks right
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
