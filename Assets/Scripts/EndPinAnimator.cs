using UnityEngine;

public class EndPinAnimator : MonoBehaviour
{
    [Header("Float")]
    [Tooltip("How far up and down the pin moves from its starting position.")]
    public float floatAmplitude = 0.2f;

    [Tooltip("Float cycles per second.")]
    public float floatFrequency = 1f;

    [Header("Pulse")]
    [Tooltip("Scale at the smallest point of each pulse (relative to original size).")]
    public float pulseMinScale = 0.9f;

    [Tooltip("Scale at the largest point of each pulse (relative to original size).")]
    public float pulseMaxScale = 1.1f;

    [Tooltip("Pulse cycles per second.")]
    public float pulseFrequency = 1f;

    private Vector3 _originalPosition;
    private Vector3 _originalScale;
    private float _startTime;

    private void Start()
    {
        _originalPosition = transform.localPosition;
        _originalScale = transform.localScale;
        _startTime = Time.time;
    }

    /// <summary>Restarts the float/pulse cycle from the beginning.</summary>
    public void Play()
    {
        _startTime = Time.time;
        enabled = true;
    }

    private void Update()
    {
        float t = Time.time - _startTime;

        float floatOffset = Mathf.Sin(t * floatFrequency * Mathf.PI * 2f) * floatAmplitude;
        transform.localPosition = _originalPosition + Vector3.up * floatOffset;

        float pulseT = (Mathf.Sin(t * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
        float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale, pulseT);
        transform.localScale = _originalScale * scale;
    }

    private void OnDisable()
    {
        transform.localPosition = _originalPosition;
        transform.localScale = _originalScale;
    }
}
