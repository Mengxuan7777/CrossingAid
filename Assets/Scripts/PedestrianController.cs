using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PedestrianController : MonoBehaviour, ISpawnable
{
    [HideInInspector] public Transform[] waypoints;
    [HideInInspector] public int waitWaypointIndex = 1;
    [HideInInspector] public SignalDirection crossingVehicleDirection;

    [Header("Movement")]
    public float speed = 1.2f;
    public float waypointReachDistance = 0.5f;

    public event Action OnDestinationReached;
    public Transform[] Waypoints { set => waypoints = value; }

    private NavMeshAgent _agent;
    private int _currentIndex;
    private bool _waitingToCross;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        _currentIndex = 0;
        _waitingToCross = false;
        _agent.speed = speed;

        if (waypoints == null || waypoints.Length == 0) return;

        if (!_agent.Warp(waypoints[0].position)) return;
        _agent.isStopped = false;
        SetDestination(0);

        var ctrl = IntersectionSignalController.Instance;
        if (ctrl == null) return;
        if (crossingVehicleDirection == SignalDirection.NorthSouth)
            ctrl.OnNorthSouthChanged += OnSignalChanged;
        else
            ctrl.OnEastWestChanged += OnSignalChanged;
    }

    private void OnDisable()
    {
        if (_agent.isActiveAndEnabled)
            _agent.isStopped = true;

        var ctrl = IntersectionSignalController.Instance;
        if (ctrl == null) return;
        if (crossingVehicleDirection == SignalDirection.NorthSouth)
            ctrl.OnNorthSouthChanged -= OnSignalChanged;
        else
            ctrl.OnEastWestChanged -= OnSignalChanged;
    }

    private void Update()
    {
        if (_waitingToCross) return;
        if (waypoints == null || waypoints.Length == 0) return;
        if (_agent.pathPending) return;
        if (_agent.remainingDistance > waypointReachDistance) return;

        if (_currentIndex == waitWaypointIndex && !IsSafeToCross())
        {
            _agent.isStopped = true;
            _waitingToCross = true;
            return;
        }

        _currentIndex++;
        if (_currentIndex >= waypoints.Length)
        {
            OnDestinationReached?.Invoke();
            return;
        }

        SetDestination(_currentIndex);
    }

    private void SetDestination(int index)
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = false;
        _agent.SetDestination(waypoints[index].position);
    }

    private bool IsSafeToCross()
    {
        var ctrl = IntersectionSignalController.Instance;
        if (ctrl == null) return true;
        return ctrl.GetState(crossingVehicleDirection) == VehicleLightState.Red;
    }

    private void OnSignalChanged(VehicleLightState state)
    {
        if (_waitingToCross && state == VehicleLightState.Red)
        {
            _waitingToCross = false;
            SetDestination(_currentIndex);
        }
    }
}
