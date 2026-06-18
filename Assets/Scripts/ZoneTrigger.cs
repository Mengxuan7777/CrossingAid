using UnityEngine;

public class ZoneTrigger : MonoBehaviour
{
    [Tooltip("Which zone this collider represents.")]
    public ZoneType zoneType;

    [Tooltip("Which road this zone leads to. Ignored for Approaching (shared, direction-agnostic).")]
    public PlayerCrossesRoad road;

    public string participantTag = "Player";
    public bool restrictByTag = true;

    public ZoneTracker zoneTracker;

    private void OnTriggerEnter(Collider other)
    {
        if (restrictByTag && !other.CompareTag(participantTag)) return;
        zoneTracker?.EnterZone(zoneType, road);
    }

    private void OnTriggerExit(Collider other)
    {
        if (restrictByTag && !other.CompareTag(participantTag)) return;
        zoneTracker?.ExitZone(zoneType, road);
    }
}
