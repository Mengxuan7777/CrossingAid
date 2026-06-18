using UnityEngine;

public class FootstepAudio : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the XRPlayerMover on your XR Origin — reads speed directly from joystick input.")]
    public XRPlayerMover playerMover;
    public AudioSource audioSource;
    public AudioClip[] footstepClips;

    [Header("Speed Thresholds")]
    [Tooltip("Speed (m/s) below which no footstep plays. Should match XRPlayerMover's minSpeed.")]
    public float minSpeed = 0.2f;
    [Tooltip("Speed treated as 'max' for footstep rate interpolation. Should match XRPlayerMover's maxSpeed.")]
    public float maxSpeed = 2.0f;

    [Header("Footstep Interval")]
    [Tooltip("Seconds between footsteps at the slowest audible speed.")]
    public float intervalAtMinSpeed = 0.7f;
    [Tooltip("Seconds between footsteps at max speed.")]
    public float intervalAtMaxSpeed = 0.22f;

    [Header("Pitch")]
    [Tooltip("Clip pitch at the slowest audible speed.")]
    public float pitchAtMinSpeed = 0.85f;
    [Tooltip("Clip pitch at max speed.")]
    public float pitchAtMaxSpeed = 1.2f;
    [Tooltip("Random pitch shifted ± this amount each step for naturalness.")]
    public float pitchVariance = 0.05f;

    [Header("Volume")]
    [Range(0f, 1f)]
    public float volume = 1f;

    private float _stepTimer;
    private float _speedT;

    private void Update()
    {
        if (playerMover == null || audioSource == null) return;

        float speed = playerMover.CurrentSpeed;

        if (speed < minSpeed)
        {
            _stepTimer = 0f;
            _speedT = 0f;
            return;
        }

        _speedT = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
        float interval = Mathf.Lerp(intervalAtMinSpeed, intervalAtMaxSpeed, _speedT);

        _stepTimer += Time.deltaTime;
        if (_stepTimer >= interval)
        {
            _stepTimer = 0f;
            PlayStep();
        }
    }

    private void PlayStep()
    {
        if (footstepClips == null || footstepClips.Length == 0) return;
        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
        if (clip == null) return;
        float basePitch = Mathf.Lerp(pitchAtMinSpeed, pitchAtMaxSpeed, _speedT);
        audioSource.pitch = basePitch + Random.Range(-pitchVariance, pitchVariance);
        audioSource.PlayOneShot(clip, volume);
    }
}
