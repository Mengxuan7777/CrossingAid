using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PedestrianDirectionGroup
{
    public string label = "Direction";
    public Transform[] waypoints;
    public int waitWaypointIndex = 1;
    public SignalDirection crossingVehicleDirection;
    [Min(0f)] public float spawnWeight = 1f;
}

public class PedestrianSpawner : MonoBehaviour
{
    [Header("Shared Pool")]
    public GameObject[] prefabs;
    public int poolSizePerPrefab = 3;

    [Header("Direction Groups")]
    public PedestrianDirectionGroup[] groups;

    [Header("Timing")]
    public float minSpawnInterval = 2f;
    public float maxSpawnInterval = 6f;

    private Dictionary<GameObject, Queue<GameObject>> _pool;
    private GameObject _poolRoot;
    private float _timer;
    private float _nextSpawnInterval;

    private void Start()
    {
        _poolRoot = new GameObject("[PedPool]");
        _poolRoot.SetActive(false);

        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning($"[PedestrianSpawner] '{name}': no prefabs assigned.", this);
            return;
        }

        _pool = new Dictionary<GameObject, Queue<GameObject>>();
        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;
            var queue = new Queue<GameObject>();
            for (int i = 0; i < poolSizePerPrefab; i++)
            {
                var go = Instantiate(prefab, _poolRoot.transform);
                go.SetActive(false);
                queue.Enqueue(go);
            }
            _pool[prefab] = queue;
            Debug.Log($"[PedestrianSpawner] '{name}': pooled {poolSizePerPrefab}x '{prefab.name}'.", this);
        }

        if (groups == null || groups.Length == 0)
            Debug.LogWarning($"[PedestrianSpawner] '{name}': no direction groups configured.", this);

        _nextSpawnInterval = 0f;
    }

    private void Update()
    {
        if (_pool == null) return;
        _timer += Time.deltaTime;
        if (_timer >= _nextSpawnInterval)
        {
            _timer = 0f;
            _nextSpawnInterval = UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval);
            TrySpawn();
        }
    }

    private void TrySpawn()
    {
        if (groups == null || groups.Length == 0) return;

        int groupIndex = PickGroup();
        if (groupIndex < 0) return;

        var group = groups[groupIndex];

        int start = UnityEngine.Random.Range(0, prefabs.Length);
        for (int i = 0; i < prefabs.Length; i++)
        {
            var prefab = prefabs[(start + i) % prefabs.Length];
            if (prefab == null || !_pool.ContainsKey(prefab) || _pool[prefab].Count == 0) continue;
            var queue = _pool[prefab];
            SpawnFrom(queue.Dequeue(), queue, group);
            return;
        }

        Debug.LogWarning($"[PedestrianSpawner] '{name}' [{group.label}]: all pools exhausted — skipping tick.", this);
    }

    private int PickGroup()
    {
        float total = 0f;
        foreach (var g in groups)
            total += Mathf.Max(0f, g.spawnWeight);

        if (total <= 0f) return -1;

        float r = UnityEngine.Random.Range(0f, total);
        float cumulative = 0f;
        for (int i = 0; i < groups.Length; i++)
        {
            cumulative += Mathf.Max(0f, groups[i].spawnWeight);
            if (r < cumulative) return i;
        }
        return groups.Length - 1;
    }

    private void SpawnFrom(GameObject go, Queue<GameObject> returnQueue, PedestrianDirectionGroup group)
    {
        var ctrl = go.GetComponent<PedestrianController>();
        if (ctrl == null)
        {
            Debug.LogError($"[PedestrianSpawner] '{name}': '{go.name}' missing PedestrianController — add it to the prefab.", go);
            go.transform.SetParent(_poolRoot.transform);
            returnQueue.Enqueue(go);
            return;
        }

        // Set group config before SetActive so OnEnable reads the correct values
        ctrl.crossingVehicleDirection = group.crossingVehicleDirection;
        ctrl.waitWaypointIndex = group.waitWaypointIndex;
        ctrl.Waypoints = group.waypoints;

        go.transform.SetParent(null);
        if (group.waypoints != null && group.waypoints.Length > 0)
            go.transform.position = group.waypoints[0].position;

        var spawnable = (ISpawnable)ctrl;
        Action callback = null;
        callback = () =>
        {
            spawnable.OnDestinationReached -= callback;
            go.SetActive(false);
            go.transform.SetParent(_poolRoot.transform);
            returnQueue.Enqueue(go);
            Debug.Log($"[PedestrianSpawner] '{name}' [{group.label}]: '{go.name}' returned to pool.", this);
        };
        spawnable.OnDestinationReached += callback;

        go.SetActive(true);
        Debug.Log($"[PedestrianSpawner] '{name}' [{group.label}]: spawned '{go.name}'.", this);
    }
}
