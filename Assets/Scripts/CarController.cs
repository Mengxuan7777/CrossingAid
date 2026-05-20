using System;
using UnityEngine;

public class CarController : MonoBehaviour, ISpawnable
{
    [HideInInspector] public Transform[] waypoints;
    [HideInInspector] public int stopLineWaypointIndex = 1;
    [HideInInspector] public SignalDirection signalDirection;

    [Header("Movement")]
    public float speed = 5f;
    public float waypointReachDistance = 0.5f;

    public event Action OnDestinationReached;
    public Transform[] Waypoints { set => waypoints = value; }

    private int _currentIndex;
    private bool _waitingAtStopLine;

    private void OnEnable()
    {
        _currentIndex = 0;
        _waitingAtStopLine = false;
        if (waypoints != null && waypoints.Length > 0)
            transform.position = waypoints[0].position;

        var ctrl = IntersectionSignalController.Instance;
        if (ctrl == null) return;
        if (signalDirection == SignalDirection.NorthSouth)
            ctrl.OnNorthSouthChanged += OnSignalChanged;
        else
            ctrl.OnEastWestChanged += OnSignalChanged;
    }

    private void OnDisable()
    {
        var ctrl = IntersectionSignalController.Instance;
        if (ctrl == null) return;
        if (signalDirection == SignalDirection.NorthSouth)
            ctrl.OnNorthSouthChanged -= OnSignalChanged;
        else
            ctrl.OnEastWestChanged -= OnSignalChanged;
    }

    private void Update()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        if (_waitingAtStopLine) return;
        MoveTowardWaypoint();
    }

    private void MoveTowardWaypoint()
    {
        Transform target = waypoints[_currentIndex];
        Vector3 delta = target.position - transform.position;
        float dist = delta.magnitude;

        if (dist >= waypointReachDistance)
        {
            Vector3 dir = delta / dist;
            transform.position += dir * speed * Time.deltaTime;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);
            return;
        }

        if (_currentIndex == stopLineWaypointIndex && !CanProceed())
        {
            _waitingAtStopLine = true;
            return;
        }

        _currentIndex++;
        if (_currentIndex >= waypoints.Length)
            OnDestinationReached?.Invoke();
    }

    private bool CanProceed()
    {
        var ctrl = IntersectionSignalController.Instance;
        if (ctrl == null) return true;
        return ctrl.GetState(signalDirection) == VehicleLightState.Green;
    }

    private void OnSignalChanged(VehicleLightState state)
    {
        if (_waitingAtStopLine && state == VehicleLightState.Green)
            _waitingAtStopLine = false;
    }
}
