using System;
using System.Collections;
using UnityEngine;

public enum SignalDirection { NorthSouth, EastWest }

public class IntersectionSignalController : MonoBehaviour
{
    public static IntersectionSignalController Instance { get; private set; }

    [Header("North-South Signal Group")]
    public TrafficLightView[] northSouthLights;

    [Header("East-West Signal Group")]
    public TrafficLightView[] eastWestLights;

    [Header("Timing")]
    public float greenDuration = 10f;
    public float yellowDuration = 3f;
    public float allRedDuration = 2f;

    public event Action<VehicleLightState> OnNorthSouthChanged;
    public event Action<VehicleLightState> OnEastWestChanged;

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
    }

    private void SetEastWest(VehicleLightState state)
    {
        if (_eastWestState == state) return;
        _eastWestState = state;
        OnEastWestChanged?.Invoke(state);
        SetLightGroup(eastWestLights, state);
    }

    private void SetLightGroup(TrafficLightView[] group, VehicleLightState state)
    {
        if (group == null) return;
        for (int i = 0; i < group.Length; i++)
            group[i]?.SetState(state);
    }
}