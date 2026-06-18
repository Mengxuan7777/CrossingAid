using UnityEngine;

public class ExperimentController : MonoBehaviour
{
    [Header("Core References")]
    public EyeTrackingLogger logger;
    public Transform playerRoot;       // Assign XR Origin here
    public Transform trialStartPose;   // Empty GameObject at floor level — XR Origin teleports here

    [Header("Trial Metadata")]
    public string participantId = "P001";
    public string sessionId = "S001";
    public string conditionName = "TestCondition";
    public string distractionType = "None";
    public int trialNumber = 1;

    [Header("Signal Tracking")]
    [Tooltip("Default road tracked for signal_state before the player commits to a crossing direction (see SetCrossingRoad). Updated live by ZoneTracker on Initiation zone entry.")]
    public PlayerCrossesRoad crossingRoad = PlayerCrossesRoad.NorthSouth;

    [Header("Options")]
    public bool autoStartOnPlay = false;
    public bool resetPlayerOnStartTrial = true;

    [Header("Debug Keys")]
    public bool enableDebugKeys = true;
    public KeyCode startTrialKey = KeyCode.S;
    public KeyCode endTrialKey = KeyCode.E;
    public KeyCode resetPlayerKey = KeyCode.R;
    public KeyCode dontWalkKey = KeyCode.Alpha1;
    public KeyCode walkKey = KeyCode.Alpha2;

    private bool trialStarted = false;
    private bool _armed = false;

    private void Start()
    {
        var signals = IntersectionSignalController.Instance;
        if (signals != null)
        {
            signals.OnNorthSouthCrossingChanged += OnNorthSouthSignalChanged;
            signals.OnEastWestCrossingChanged   += OnEastWestSignalChanged;
        }

        if (autoStartOnPlay)
            StartTrial();
    }

    private void OnDestroy()
    {
        var signals = IntersectionSignalController.Instance;
        if (signals != null)
        {
            signals.OnNorthSouthCrossingChanged -= OnNorthSouthSignalChanged;
            signals.OnEastWestCrossingChanged   -= OnEastWestSignalChanged;
        }
    }

    private void OnNorthSouthSignalChanged(PedestrianLightState state)
    {
        if (crossingRoad == PlayerCrossesRoad.NorthSouth)
            ApplySignalState(state);
    }

    private void OnEastWestSignalChanged(PedestrianLightState state)
    {
        if (crossingRoad == PlayerCrossesRoad.EastWest)
            ApplySignalState(state);
    }

    private void ApplySignalState(PedestrianLightState state)
    {
        if (state == PedestrianLightState.Walk) SetWalk();
        else SetDontWalk();
    }

    // Called by ZoneTracker when the player enters an Initiation zone (i.e. commits
    // to a crossing direction). Switches which pedestrian signal drives signal_state
    // and immediately logs its current value.
    public void SetCrossingRoad(PlayerCrossesRoad road)
    {
        if (road == crossingRoad) return;
        crossingRoad = road;

        var signals = IntersectionSignalController.Instance;
        if (signals == null) return;
        var direction = road == PlayerCrossesRoad.NorthSouth ? SignalDirection.NorthSouth : SignalDirection.EastWest;
        bool safe = signals.GetState(direction) == VehicleLightState.Red;
        ApplySignalState(safe ? PedestrianLightState.Walk : PedestrianLightState.DontWalk);
    }

    private void Update()
    {
        if (!enableDebugKeys) return;

        if (Input.GetKeyDown(startTrialKey))   StartTrial();
        if (Input.GetKeyDown(endTrialKey))     EndTrial("ManualEnd");
        if (Input.GetKeyDown(resetPlayerKey))  ResetPlayerToStart();
        if (Input.GetKeyDown(dontWalkKey))     SetDontWalk();
        if (Input.GetKeyDown(walkKey))         SetWalk();
    }

    // Called by TrialSequencer right before StartTrial() once it has actually loaded a
    // config and is starting a real trial. Blocks StartTrial() from firing via debug
    // keys or autoStartOnPlay before that point, which previously logged a stray warmup
    // trial with default Inspector metadata (e.g. conditionName="TestCondition").
    public void Arm() => _armed = true;

    public void StartTrial()
    {
        if (!_armed)
        {
            Debug.LogWarning("[ExperimentController] StartTrial() blocked — not armed yet. TrialSequencer arms this automatically once a config is loaded; this prevents warmup/debug trials from being logged.");
            return;
        }

        if (logger == null)
        {
            Debug.LogError("[ExperimentController] StartTrial() aborted — logger is null. Assign EyeTrackingLogger in the Inspector.");
            return;
        }

        if (trialStarted && logger.TrialActive)
        {
            Debug.LogWarning($"[ExperimentController] StartTrial() blocked — trial already active. Call EndTrial() first.");
            return;
        }

        if (resetPlayerOnStartTrial)
            ResetPlayerToStart();

        logger.ConfigureTrial(participantId, sessionId, conditionName, distractionType, trialNumber);
        logger.StartTrial();
        trialStarted = true;
        Debug.Log($"[ExperimentController] Trial {trialNumber} started — condition='{conditionName}' distraction='{distractionType}'");
    }

    public void EndTrial(string reason)
    {
        if (logger == null || !logger.TrialActive)
        {
            Debug.LogWarning($"[ExperimentController] EndTrial('{reason}') skipped — logger={(logger == null ? "null" : "ok")}, TrialActive={logger?.TrialActive}");
            return;
        }

        logger.EndTrial(reason);
        trialStarted = false;
        Debug.Log($"[ExperimentController] Trial ended — reason='{reason}'");
    }

    public void ResetPlayerToStart()
    {
        if (playerRoot == null || trialStartPose == null) return;

        CharacterController cc = playerRoot.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Rigidbody rb = playerRoot.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Place XR Origin at trialStartPose (floor level).
        // Subtract any XZ camera drift so the head centers on the start XZ position.
        Camera cam = playerRoot.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            Vector3 drift = cam.transform.position - playerRoot.position;
            drift.y = 0f;
            playerRoot.position = trialStartPose.position - drift;
        }
        else
        {
            playerRoot.position = trialStartPose.position;
        }

        if (cc != null) cc.enabled = true;

        XRPlayerMover mover = playerRoot.GetComponent<XRPlayerMover>();
        if (mover != null) mover.ResetVelocity();
    }

    public void SetDontWalk()
    {
        logger?.SetSignalState("DONT_WALK");
    }

    public void SetWalk()
    {
        logger?.SetSignalState("WALK");
    }

    public void WriteCustomEvent(string eventName, string eventValue)
    {
        logger?.WriteCustomEvent(eventName, eventValue);
    }
}
