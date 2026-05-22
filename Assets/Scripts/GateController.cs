using UnityEngine;

public class GateController : MonoBehaviour
{
    [Header("Signal")]
    [Tooltip("Vehicle direction that crosses this gate's pedestrian path. Gate opens when this direction is Red.")]
    public SignalDirection crossingVehicleDirection = SignalDirection.NorthSouth;

    [Header("Logger")]
    public EyeTrackingLogger logger;

    [Header("Animation")]
    [Tooltip("If assigned, fires 'GateOpen' and 'GateClose' triggers on state change.")]
    public Animator gateAnimator;

    [Tooltip("Shown when gate is closed (red / blocking).")]
    public GameObject closedVisual;

    [Tooltip("Shown when gate is open (green / passable).")]
    public GameObject openVisual;

    private bool _isOpen = false;
    private bool _started = false;

    private void Start()
    {
        Subscribe();
        _started = true;
    }

    private void OnEnable()
    {
        if (_started) Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        var ctrl = IntersectionSignalController.Instance;
        if (ctrl == null)
        {
            Debug.LogError("[GateController] IntersectionSignalController.Instance is null.", this);
            return;
        }

        if (crossingVehicleDirection == SignalDirection.NorthSouth)
            ctrl.OnNorthSouthChanged += OnSignalChanged;
        else
            ctrl.OnEastWestChanged += OnSignalChanged;

        // Set _isOpen to the opposite of the current state so the equality check
        // in OnSignalChanged always passes, guaranteeing ApplyState runs on first sync.
        VehicleLightState currentState = ctrl.GetState(crossingVehicleDirection);
        _isOpen = currentState != VehicleLightState.Red;
        OnSignalChanged(currentState);
    }

    private void Unsubscribe()
    {
        var ctrl = IntersectionSignalController.Instance;
        if (ctrl == null) return;

        if (crossingVehicleDirection == SignalDirection.NorthSouth)
            ctrl.OnNorthSouthChanged -= OnSignalChanged;
        else
            ctrl.OnEastWestChanged -= OnSignalChanged;
    }

    private void OnSignalChanged(VehicleLightState state)
    {
        bool shouldOpen = state == VehicleLightState.Red;
        if (_isOpen == shouldOpen) return;
        _isOpen = shouldOpen;

        if (_isOpen)
            ApplyState("GateOpen", "OPEN");
        else
            ApplyState("GateClose", "CLOSED");
    }

    private void ApplyState(string animatorTrigger, string loggedState)
    {
        if (gateAnimator != null)
            gateAnimator.SetTrigger(animatorTrigger);

        if (closedVisual != null) closedVisual.SetActive(loggedState == "CLOSED");
        if (openVisual != null)   openVisual.SetActive(loggedState == "OPEN");

        if (logger != null)
            logger.SetGateState(loggedState);

        Debug.Log($"[GateController] {loggedState}.", this);
    }

    public void ForceOpen()  => ApplyState("GateOpen",  "OPEN");
    public void ForceClose() => ApplyState("GateClose", "CLOSED");
}
