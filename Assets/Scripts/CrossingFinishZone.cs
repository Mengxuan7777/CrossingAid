using System.Collections;
using UnityEngine;

public class CrossingFinishZone : MonoBehaviour
{
    [Tooltip("XR Origin transform (floor-level position used for zone detection).")]
    public Transform playerOrigin;

    [Tooltip("TrialSequencer to call NextTrial() on.")]
    public TrialSequencer trialSequencer;

    [Tooltip("Seconds to wait after the player arrives before the next trial begins.")]
    public float delayBeforeNextTrial = 2f;

    private Collider _zone;
    private bool _waiting = false;

    private void Start()
    {
        _zone = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        _waiting = false;
    }

    private void Update()
    {
        if (_waiting || playerOrigin == null || _zone == null) return;

        Vector3 pos = playerOrigin.position;
        Bounds b = _zone.bounds;
        if (pos.x >= b.min.x && pos.x <= b.max.x &&
            pos.z >= b.min.z && pos.z <= b.max.z)
        {
            _waiting = true;
            StartCoroutine(Advance());
        }
    }

    private IEnumerator Advance()
    {
        Debug.Log($"[CrossingFinishZone] Player arrived — waiting {delayBeforeNextTrial}s before next trial.");
        yield return new WaitForSeconds(delayBeforeNextTrial);
        _waiting = false;
        if (trialSequencer == null)
        {
            Debug.LogError("[CrossingFinishZone] trialSequencer is null — assign TrialSequencer in the Inspector.");
            yield break;
        }
        trialSequencer.NextTrial();
    }
}
