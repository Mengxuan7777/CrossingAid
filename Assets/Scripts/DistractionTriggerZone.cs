using UnityEngine;

public class DistractionTriggerZone : MonoBehaviour
{
    [Tooltip("XR Origin transform. Checked against this zone's bounds every frame.")]
    public Transform playerOrigin;

    [Tooltip("TrialSequencer to notify when the player enters.")]
    public TrialSequencer trialSequencer;

    [Tooltip("Which distraction item this zone triggers: 0 = first message/clip, 1 = second, 2 = third.")]
    public int itemIndex = 0;

    private Collider _zone;

    private void Awake() => _zone = GetComponent<Collider>();

    private void Update()
    {
        if (playerOrigin == null || _zone == null) return;
        if (_zone.bounds.Contains(playerOrigin.position))
            trialSequencer?.TriggerDistractionItem(itemIndex);
    }
}
