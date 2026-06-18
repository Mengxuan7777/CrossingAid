using System.Collections;
using UnityEngine;

public class CrossingFinishZone : MonoBehaviour
{
    [Tooltip("XR Origin transform. The collider that triggers this zone must belong to this object (e.g. its CharacterController).")]
    public Transform playerOrigin;

    [Tooltip("TrialSequencer to call NextTrial() on.")]
    public TrialSequencer trialSequencer;

    [Tooltip("Seconds to wait after the player arrives before the next trial begins.")]
    public float delayBeforeNextTrial = 2f;

    private bool _waiting = false;

    private void OnEnable()
    {
        _waiting = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_waiting) return;
        if (playerOrigin != null && other.transform != playerOrigin) return;

        _waiting = true;
        StartCoroutine(Advance());
    }

    // Called by TrialSequencer after the new trial has fully started, so the zone can fire again.
    public void Unlock()
    {
        _waiting = false;
    }

    private IEnumerator Advance()
    {
        Debug.Log($"[CrossingFinishZone] Player arrived — waiting {delayBeforeNextTrial}s before next trial.");
        yield return new WaitForSeconds(delayBeforeNextTrial);
        if (trialSequencer == null)
        {
            Debug.LogError("[CrossingFinishZone] trialSequencer is null — assign TrialSequencer in the Inspector.");
            _waiting = false;
            yield break;
        }
        trialSequencer.NextTrial();
        // _waiting stays true until TrialSequencer calls Unlock() after the new trial starts.
    }
}
