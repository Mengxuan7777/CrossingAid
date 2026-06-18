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
    [Tooltip("Seconds before a pedestrian phase ends that the signal starts flashing.")]
    public float flashDuration = 3f;
    [Tooltip("Seconds between each flash toggle.")]
    public float flashInterval = 0.4f;

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

    // Starts (or restarts) the signal cycle so that the player's road has its
    // "walk" light (vehicle Red) right now, with secondsRemaining left before it changes.
    // nsRoadIsSafe=true  → NS is Red/Walk now (EW is Green), holds for secondsRemaining.
    // nsRoadIsSafe=false → EW is Red/Walk now (NS is Green), holds for secondsRemaining.
    public void StartCycleWithRemaining(bool nsRoadIsSafe, float secondsRemaining)
    {
        StopAllCoroutines();
        ForceSignalState(
            nsRoadIsSafe ? VehicleLightState.Red   : VehicleLightState.Green,
            nsRoadIsSafe ? VehicleLightState.Green : VehicleLightState.Red);
        StartCoroutine(SignalLoopWithInitialHold(nsRoadIsSafe, Mathf.Max(0f, secondsRemaining)));
    }

    private IEnumerator SignalLoopWithInitialHold(bool nsRoadIsSafe, float holdSeconds)
    {
        yield return new WaitForSeconds(holdSeconds);

        if (nsRoadIsSafe)
        {
            // NS is Red/Walk, EW is Green — bring EW down to Red.
            SetEastWest(VehicleLightState.Yellow);
            yield return new WaitForSeconds(yellowDuration);
            SetEastWest(VehicleLightState.Red);
            yield return new WaitForSeconds(allRedDuration);
        }
        else
        {
            // EW is Red/Walk, NS is Green — bring NS down to Red, then run EW's full green phase.
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

        // Both directions are now Red — resume the normal alternating loop, NS green first.
        yield return SignalLoopFrom(false);
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
            yield return StartCoroutine(GreenPhase(eastWestCrossingPedLights, PedestrianLightState.DontWalk));
            SetEastWest(VehicleLightState.Yellow);
            yield return StartCoroutine(YellowAndAllRedPhase(
                northSouthCrossingPedLights, PedestrianLightState.Walk,
                () => SetEastWest(VehicleLightState.Red)));
        }

        while (true)
        {
            SetNorthSouth(VehicleLightState.Green);
            SetEastWest(VehicleLightState.Red);
            // Flash NS DontWalk for the last flashDuration seconds of the green phase.
            yield return StartCoroutine(GreenPhase(northSouthCrossingPedLights, PedestrianLightState.DontWalk));

            SetNorthSouth(VehicleLightState.Yellow);
            // Flash EW Walk starting flashDuration seconds before EW turns Green.
            yield return StartCoroutine(YellowAndAllRedPhase(
                eastWestCrossingPedLights, PedestrianLightState.Walk,
                () => SetNorthSouth(VehicleLightState.Red)));

            SetEastWest(VehicleLightState.Green);
            // Flash EW DontWalk for the last flashDuration seconds of the green phase.
            yield return StartCoroutine(GreenPhase(eastWestCrossingPedLights, PedestrianLightState.DontWalk));

            SetEastWest(VehicleLightState.Yellow);
            // Flash NS Walk starting flashDuration seconds before NS turns Green.
            yield return StartCoroutine(YellowAndAllRedPhase(
                northSouthCrossingPedLights, PedestrianLightState.Walk,
                () => SetEastWest(VehicleLightState.Red)));
        }
    }

    // Waits greenDuration, flashing pedGroup for the last flashDuration seconds.
    private IEnumerator GreenPhase(PedestrianLightView[] pedGroup, PedestrianLightState state)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, greenDuration - flashDuration));
        FlashPedGroup(pedGroup, state);
        yield return new WaitForSeconds(Mathf.Min(greenDuration, flashDuration));
    }

    // Covers the Yellow + AllRed period, calling setRed() at the yellow→red boundary.
    // Starts flashing pedGroup so the flash ends exactly when SetGreen is called next.
    private IEnumerator YellowAndAllRedPhase(PedestrianLightView[] pedGroup, PedestrianLightState state, System.Action setRed)
    {
        float timeToEnd = yellowDuration + allRedDuration;
        float flashStart = Mathf.Max(0f, timeToEnd - flashDuration);

        if (flashStart < yellowDuration)
        {
            // Flash starts during Yellow.
            yield return new WaitForSeconds(flashStart);
            FlashPedGroup(pedGroup, state);
            yield return new WaitForSeconds(yellowDuration - flashStart);
            setRed();
            yield return new WaitForSeconds(allRedDuration);
        }
        else
        {
            // Flash starts during AllRed.
            yield return new WaitForSeconds(yellowDuration);
            setRed();
            yield return new WaitForSeconds(flashStart - yellowDuration);
            FlashPedGroup(pedGroup, state);
            yield return new WaitForSeconds(timeToEnd - flashStart);
        }
    }

    private void FlashPedGroup(PedestrianLightView[] group, PedestrianLightState state)
    {
        if (group == null) return;
        foreach (var light in group)
            light?.StartFlashing(state, flashInterval);
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