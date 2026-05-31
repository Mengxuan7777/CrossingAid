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
        StartCoroutine(SignalLoop());
    }

    private IEnumerator SignalLoop()
    {
        // Both vehicle directions start Red — initialize ped signals to Walk for both crossings.
        // SetNorthSouth(Green) below will immediately flip northSouthCrossing to DontWalk.
        SetPedGroup(northSouthCrossingPedLights, PedestrianLightState.Walk);
        SetPedGroup(eastWestCrossingPedLights, PedestrianLightState.Walk);

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