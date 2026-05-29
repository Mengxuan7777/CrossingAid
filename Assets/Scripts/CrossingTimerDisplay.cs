using UnityEngine;

public class CrossingTimerDisplay : MonoBehaviour
{
    [Header("Signal")]
    [Tooltip("Vehicle direction that crosses this pedestrian path. Timer counts down while this direction is Red.")]
    public SignalDirection crossingVehicleDirection = SignalDirection.NorthSouth;

    [Header("Segments")]
    [Tooltip("Assign segment GameObjects in order. Green drains from index 0 toward the last segment as time runs out.")]
    public GameObject[] segments;

    [Header("Materials")]
    [Tooltip("Applied to segments while time still remains.")]
    public Material greenMaterial;

    [Tooltip("Applied to segments whose time has expired.")]
    public Material defaultMaterial;

    [Header("Logger (optional)")]
    public EyeTrackingLogger logger;

    private Renderer[] _renderers;
    private bool _isSafe = false;
    private float _phaseStartTime;
    private float _totalDuration;
    private bool _started = false;

    private void Start()
    {
        _renderers = new Renderer[segments != null ? segments.Length : 0];
        for (int i = 0; i < _renderers.Length; i++)
            if (segments[i] != null)
                _renderers[i] = segments[i].GetComponent<Renderer>();

        Subscribe();
        _started = true;
        ApplyMaterialToAll(defaultMaterial);
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
        if (ctrl == null) return;

        _totalDuration = ctrl.GetSafeCrossingDuration();

        if (crossingVehicleDirection == SignalDirection.NorthSouth)
            ctrl.OnNorthSouthChanged += OnSignalChanged;
        else
            ctrl.OnEastWestChanged += OnSignalChanged;

        OnSignalChanged(ctrl.GetState(crossingVehicleDirection));
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
        _isSafe = state == VehicleLightState.Red;

        if (_isSafe)
        {
            _phaseStartTime = Time.time;
            ApplyMaterialToAll(greenMaterial);
            logger?.WriteCustomEvent("CrossingTimerStart", _renderers.Length.ToString());
        }
        else
        {
            ApplyMaterialToAll(defaultMaterial);
            logger?.WriteCustomEvent("CrossingTimerEnd", "");
        }
    }

    private void Update()
    {
        if (!_isSafe || _renderers == null || _renderers.Length == 0) return;

        float fraction = Mathf.Clamp01((Time.time - _phaseStartTime) / _totalDuration);

        // Green count decreases from full to zero; green stays at the high-index end,
        // draining from index 0 upward as time runs out.
        int greenCount = Mathf.CeilToInt((1f - fraction) * _renderers.Length);

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].material = i >= _renderers.Length - greenCount ? greenMaterial : defaultMaterial;
        }
    }

    private void ApplyMaterialToAll(Material mat)
    {
        if (_renderers == null || mat == null) return;
        foreach (var r in _renderers)
            if (r != null) r.material = mat;
    }
}
