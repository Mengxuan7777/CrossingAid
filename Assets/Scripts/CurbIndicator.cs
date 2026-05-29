using UnityEngine;

public class CurbIndicator : MonoBehaviour
{
    [Header("Signal")]
    [Tooltip("Vehicle direction that crosses this pedestrian path. Curb turns green when this direction is Red.")]
    public SignalDirection crossingVehicleDirection = SignalDirection.NorthSouth;

    [Header("Renderer")]
    public Renderer curbRenderer;
    [Tooltip("Index into curbRenderer.materials[] to swap. 0 if the curb has a single material.")]
    public int materialIndex = 0;

    [Header("Materials")]
    [Tooltip("Applied when safe to cross (vehicle signal is Red).")]
    public Material safeMaterial;

    [Tooltip("Applied when not safe to cross (vehicle signal is Green or Yellow).")]
    public Material unsafeMaterial;

    [Header("Logger (optional)")]
    public EyeTrackingLogger logger;

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
        if (ctrl == null) return;

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
        bool safe = state == VehicleLightState.Red;
        ApplyMaterial(safe ? safeMaterial : unsafeMaterial, safe ? "SAFE" : "UNSAFE");
    }

    private void ApplyMaterial(Material mat, string loggedState)
    {
        if (curbRenderer != null && mat != null)
        {
            Material[] mats = curbRenderer.materials;
            mats[materialIndex] = mat;
            curbRenderer.materials = mats;
        }

        if (logger != null)
            logger.WriteCustomEvent("CurbState", loggedState);
    }
}
