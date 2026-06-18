using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class ZoneTracker : MonoBehaviour
{
    public static ZoneTracker Instance { get; private set; }

    [Header("References")]
    public ExperimentController experimentController;
    public EyeTrackingLogger logger;

    public ZoneType CurrentZone { get; private set; } = ZoneType.Approaching;
    public PlayerCrossesRoad CurrentRoad { get; private set; } = PlayerCrossesRoad.NorthSouth;

    [Tooltip("Minimum seconds between two Enter (or two Exit) calls for the same zone+road to count as distinct events. Filters out collider-boundary jitter where OnTriggerEnter/Exit fires repeatedly within the same physics tick (e.g. overlapping/adjacent colliders).")]
    public float minReentryInterval = 0.5f;

    // Keyed by logical (zone, road) — multiple physical colliders sharing the same
    // road tag are tracked as one logical zone, since the metric of interest is
    // "how many times did the player attempt this direction," not which physical segment.
    private readonly Dictionary<(ZoneType, PlayerCrossesRoad), int> _visitIndex = new Dictionary<(ZoneType, PlayerCrossesRoad), int>();
    private readonly Dictionary<(ZoneType, PlayerCrossesRoad), double> _enterTime = new Dictionary<(ZoneType, PlayerCrossesRoad), double>();
    private readonly Dictionary<(ZoneType, PlayerCrossesRoad), double> _lastEnterTime = new Dictionary<(ZoneType, PlayerCrossesRoad), double>();
    private readonly Dictionary<(ZoneType, PlayerCrossesRoad), double> _lastExitTime = new Dictionary<(ZoneType, PlayerCrossesRoad), double>();

    private void Awake()
    {
        Instance = this;
    }

    public void ResetForNewTrial()
    {
        _visitIndex.Clear();
        _enterTime.Clear();
        _lastEnterTime.Clear();
        _lastExitTime.Clear();
        CurrentZone = ZoneType.Approaching;
    }

    public void EnterZone(ZoneType zone, PlayerCrossesRoad road)
    {
        var key = (zone, road);
        double now = Time.unscaledTimeAsDouble;

        if (_lastEnterTime.TryGetValue(key, out double lastEnter) && now - lastEnter < minReentryInterval)
            return; // collider-boundary jitter — same physical visit, not a new one
        _lastEnterTime[key] = now;

        int visit = _visitIndex.TryGetValue(key, out int v) ? v + 1 : 1;
        _visitIndex[key] = visit;
        _enterTime[key] = now;

        CurrentZone = zone;
        CurrentRoad = road;

        string roadLabel = zone == ZoneType.Approaching ? "NA" : road.ToString();
        logger?.WriteCustomEvent("ZoneEnter", $"zone={zone},road={roadLabel},visit={visit}");

        if (zone == ZoneType.Initiation)
        {
            experimentController?.SetCrossingRoad(road);
        }
    }

    public void ExitZone(ZoneType zone, PlayerCrossesRoad road)
    {
        var key = (zone, road);
        double now = Time.unscaledTimeAsDouble;

        if (_lastExitTime.TryGetValue(key, out double lastExit) && now - lastExit < minReentryInterval)
            return; // collider-boundary jitter
        _lastExitTime[key] = now;

        int visit = _visitIndex.TryGetValue(key, out int v) ? v : 1;
        double duration = _enterTime.TryGetValue(key, out double enter) ? now - enter : 0.0;

        string roadLabel = zone == ZoneType.Approaching ? "NA" : road.ToString();
        string durationStr = duration.ToString("F2", CultureInfo.InvariantCulture);
        logger?.WriteCustomEvent("ZoneExit", $"zone={zone},road={roadLabel},visit={visit},duration={durationStr}");
    }
}
