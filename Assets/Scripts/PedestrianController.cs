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
        _agent.isStopped = false;
        _agent.speed = speed;

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"[Pedestrian] '{name}': no waypoints assigned.", this);
            return;
        }

        _agent.Warp(waypoints[0].position);
        SetDestination(0);
        Debug.Log($"[Pedestrian] '{name}': spawned at {waypoints[0].position}, heading to waypoint 0.", this);

        var ctrl = IntersectionSignalController.Instance;
        if (ctrl == null)
        {
            Debug.LogError($"[Pedestrian] '{name}': IntersectionSignalController.Instance is null — signal events won't work.", this);
            return;
        }
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

        // Arrived at waypoints[_currentIndex]
        Debug.Log($"[Pedestrian] '{name}': reached waypoint {_currentIndex}.", this);

        if (_currentIndex == waitWaypointIndex && !IsSafeToCross())
        {
            _agent.isStopped = true;
            _waitingToCross = true;
            Debug.Log($"[Pedestrian] '{name}': waiting at curb — {crossingVehicleDirection} is not Red.", this);
            return;
        }

        _currentIndex++;
        if (_currentIndex >= waypoints.Length)
        {
            Debug.Log($"[Pedestrian] '{name}': reached destination.", this);
            OnDestinationReached?.Invoke();
            return;
        }

        SetDestination(_currentIndex);
    }

    private void SetDestination(int index)
    {
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
        Debug.Log($"[Pedestrian] '{name}': signal changed to {state} (waiting={_waitingToCross}).", this);
        if (_waitingToCross && state == VehicleLightState.Red)
        {
            _waitingToCross = false;
            Debug.Log($"[Pedestrian] '{name}': crossing now.", this);
            SetDestination(_currentIndex);
        }
    }
}
