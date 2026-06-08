using System;
using System.Collections;
using UnityEngine;

public enum SignalDirection { NorthSouth, EastWest }
public enum PedestrianLightState { Walk, DontWalk }

public class IntersectionSignalController : MonoBehaviour
{
    public static IntersectionSignalController Instance { get; private set; }

    [Header("North-South Vehicle Lights")]
    public TrafficLightView[] northSouthLights;

    [Header("East-West Vehicle Lights")]
    public TrafficLightView[] eastWestLights;

    [Header("Pedestrian Lights — crossing the N-S road")]
    [Tooltip("Walk when N-S vehicles are Red.")]
    public PedestrianLightView[] northSouthCrossingPedLights;

    [Header("Pedestrian Lights — crossing the E-W road")]
    [Tooltip("Walk when E-W vehicles are Red.")]
    public PedestrianLightView[] eastWestCrossingPedLights;

    [Header("Timing")]
    public float greenDuration = 10f;
    public float yellowDuration = 3f;
    public float allRedDuration = 2f;

    public event Action<VehicleLightState> OnNorthSouthChanged;
    public event Action<VehicleLightState> OnEastWestChanged;
    public event Action<PedestrianLightState> OnNorthSouthCrossingChanged;
    public event Action<PedestrianLightState> OnEastWestCrossingChanged;

    private VehicleLightState _northSouthState = VehicleLightState.Red;
    private VehicleLightState _eastWestState = VehicleLightState.Red;

    public VehicleLightState GetState(SignalDirection direction)
    {
        return direction == SignalDirection.NorthSouth ? _northSouthState : _eastWestState;
    }

    // Total duration a vehicle direction stays Red per cycle (the pedestrian safe-crossing window).
    // = all-red buffer + other direction green + other direction yellow + all-red buffer
    public float GetSafeCrossingDuration()
    {
        return allRedDuration + greenDuration + yellowDuration + allRedDuration;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetPedGroup(northSouthCrossingPedLights, PedestrianLightState.Walk);
        SetPedGroup(eastWestCrossingPedLights, PedestrianLightState.Walk);
        StartCoroutine(SignalLoopFrom(false));
    }

    // Starts (or restarts) the signal cycle from a specific half.
    // ewGoesFirst=true  → EW turns Green first (NS stays Red — safe for NS-road crossers).
    // ewGoesFirst=false → NS turns Green first (EW stays Red — safe for EW-road crossers).
    public void StartCycleFrom(bool ewGoesFirst)
    {
        StopAllCoroutines();
        ForceSignalState(
            ewGoesFirst ? VehicleLightState.Red   : VehicleLightState.Green,
            ewGoesFirst ? VehicleLightState.Green : VehicleLightState.Red);
        StartCoroutine(SignalLoopFrom(ewGoesFirst));
    }

    // Immediately sets both signal directions and fires all dependent events/lights.
    private void ForceSignalState(VehicleLightState ns, VehicleLightState ew)
    {
        _northSouthState = ns;
        _eastWestState   = ew;
        OnNorthSouthChanged?.Invoke(ns);
        OnEastWestChanged?.Invoke(ew);
        SetLightGroup(northSouthLights, ns);
        SetLightGroup(eastWestLights, ew);
        PedestrianLightState nsPed = ns == VehicleLightState.Red ? PedestrianLightState.Walk : PedestrianLightState.DontWalk;
        PedestrianLightState ewPed = ew == VehicleLightState.Red ? PedestrianLightState.Walk : PedestrianLightState.DontWalk;
        OnNorthSouthCrossingChanged?.Invoke(nsPed);
        OnEastWestCrossingChanged?.Invoke(ewPed);
        SetPedGroup(northSouthCrossingPedLights, nsPed);
        SetPedGroup(eastWestCrossingPedLights, ewPed);
    }

    private IEnumerator SignalLoopFrom(bool ewGoesFirst)
    {
        if (ewGoesFirst)
        {
            // EW is already Green; complete the EW half before entering the main loop.
            yield return new WaitForSeconds(greenDuration);
            SetEastWest(VehicleLightState.Yellow);
            yield return new WaitForSeconds(yellowDuration);
            SetEastWest(VehicleLightState.Red);
            yield return new WaitForSeconds(allRedDuration);
        }

        while (true)
        {
            SetNorthSouth(VehicleLightState.Green);
            SetEastWest(VehicleLightState.Red);
            yield return new WaitForSeconds(greenDuration);

            SetNorthSouth(VehicleLightState.Yellow);
            yield return new WaitForSeconds(yellowDuration);

            SetNorthSouth(VehicleLightState.Red);
            yield return new WaitForSeconds(allRedDuration);

            SetEastWest(VehicleLightState.Green);
            yield return new WaitForSeconds(greenDuration);

            SetEastWest(VehicleLightState.Yellow);
            yield return new WaitForSeconds(yellowDuration);

            SetEastWest(VehicleLightState.Red);
            yield return new WaitForSeconds(allRedDuration);
        }
    }

    private void SetNorthSouth(VehicleLightState state)
    {
        if (_northSouthState == state) return;
        _northSouthState = state;
        OnNorthSouthChanged?.Invoke(state);
        SetLightGroup(northSouthLights, state);

        PedestrianLightState pedState = state == VehicleLightState.Red ? PedestrianLightState.Walk : PedestrianLightState.DontWalk;
        OnNorthSouthCrossingChanged?.Invoke(pedState);
        SetPedGroup(northSouthCrossingPedLights, pedState);
    }

    private void SetEastWest(VehicleLightState state)
    {
        if (_eastWestState == state) return;
        _eastWestState = state;
        OnEastWestChanged?.Invoke(state);
        SetLightGroup(eastWestLights, state);

        PedestrianLightState pedState = state == VehicleLightState.Red ? PedestrianLightState.Walk : PedestrianLightState.DontWalk;
        OnEastWestCrossingChanged?.Invoke(pedState);
        SetPedGroup(eastWestCrossingPedLights, pedState);
    }

    private void SetLightGroup(TrafficLightView[] group, VehicleLightState state)
    {
        if (group == null) return;
        for (int i = 0; i < group.Length; i++)
            group[i]?.SetState(state);
    }

    private void SetPedGroup(PedestrianLightView[] group, PedestrianLightState state)
    {
        if (group == null) return;
        for (int i = 0; i < group.Length; i++)
            group[i]?.SetState(state);
    }
}