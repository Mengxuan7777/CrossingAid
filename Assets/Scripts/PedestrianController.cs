using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PedestrianController : MonoBehaviour, ISpawnable
{
    [HideInInspector] public Transform[] waypoints;

    [Header("Movement")]
    public float speed = 1.2f;
    [Tooltip("Random speed variation applied each spawn (± this amount).")]
    public float speedVariance = 0.2f;
    public float waypointReachDistance = 0.5f;

    [Header("Path Randomness")]
    [Tooltip("Max XZ offset applied to each waypoint position per spawn (m). Keeps pedestrians off identical paths.")]
    public float waypointRandomRadius = 0.6f;

    public event Action OnDestinationReached;
    public Transform[] Waypoints { set => waypoints = value; }

    private NavMeshAgent _agent;
    private int _currentIndex;
    private Vector3[] _offsetPositions;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        _currentIndex = 0;
        _agent.speed = speed + UnityEngine.Random.Range(-speedVariance, speedVariance);

        if (waypoints == null || waypoints.Length == 0) return;

        BuildOffsetPositions();

        if (!_agent.Warp(_offsetPositions[0])) return;
        _agent.isStopped = false;
        SetDestination(0);
    }

    private void OnDisable()
    {
        if (_agent.isActiveAndEnabled)
            _agent.isStopped = true;
    }

    private void Update()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        if (_agent.pathPending) return;
        if (_agent.remainingDistance > waypointReachDistance) return;

        _currentIndex++;
        if (_currentIndex >= waypoints.Length)
        {
            OnDestinationReached?.Invoke();
            return;
        }

        SetDestination(_currentIndex);
    }

    private void BuildOffsetPositions()
    {
        _offsetPositions = new Vector3[waypoints.Length];
        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector2 rand = UnityEngine.Random.insideUnitCircle * waypointRandomRadius;
            _offsetPositions[i] = waypoints[i].position + new Vector3(rand.x, 0f, rand.y);
        }
    }

    private void SetDestination(int index)
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = false;
        _agent.SetDestination(_offsetPositions[index]);
    }
}
