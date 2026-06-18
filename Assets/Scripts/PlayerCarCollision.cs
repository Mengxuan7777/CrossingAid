using System.Collections;
using System.Globalization;
using UnityEngine;

public class PlayerCarCollision : MonoBehaviour
{
    [Header("References")]
    public Transform playerOrigin;
    public TrialSequencer trialSequencer;
    public ExperimentController experimentController;

    [Header("Detection")]
    [Tooltip("Layer(s) assigned to car GameObjects.")]
    public LayerMask carLayerMask;
    [Tooltip("Radius around the player origin that counts as a hit (m).")]
    public float hitRadius = 0.6f;

    [Header("Feedback")]
    [Tooltip("Seconds to show a brief non-blocking hit message.")]
    public float messageDuration = 1.5f;
    [Tooltip("Cooldown after a hit before another can be counted — avoids double-logging the same collision across frames.")]
    public float hitCooldown = 1.5f;

    private int _hitCountThisTrial;
    private bool _inCooldown;

    public void ResetHit()
    {
        _hitCountThisTrial = 0;
        _inCooldown = false;
    }

    private void Update()
    {
        if (_inCooldown || playerOrigin == null) return;
        Collider[] hits = Physics.OverlapSphere(playerOrigin.position, hitRadius, carLayerMask);
        if (hits.Length > 0)
            StartCoroutine(HandleHit(hits[0].gameObject.name));
    }

    private IEnumerator HandleHit(string carName)
    {
        _inCooldown = true;
        _hitCountThisTrial++;

        experimentController?.WriteCustomEvent("CarHit", $"car={carName},count={_hitCountThisTrial.ToString(CultureInfo.InvariantCulture)}");
        trialSequencer?.ShowMessage("Hit by a car!", messageDuration);

        Debug.Log($"[PlayerCarCollision] Hit by '{carName}' — count this trial: {_hitCountThisTrial}.");
        yield return new WaitForSeconds(hitCooldown);
        _inCooldown = false;
    }
}
