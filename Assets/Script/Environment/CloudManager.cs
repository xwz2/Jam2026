using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ambient cloud system. Keeps <see cref="cloudCount"/> clouds drifting
/// horizontally across a rectangular field: each cloud enters at the left or
/// right edge, floats to the opposite side at its own random speed, and is
/// recycled back to its start edge (with a fresh height and speed) when it
/// leaves the field.
///
/// Movement is pure transform animation — no Rigidbody is added or required.
/// If a cloud prefab happens to have a collider it keeps it, untouched.
/// </summary>
public class CloudManager : MonoBehaviour
{
    public enum DriftDirection
    {
        RandomPerCloud,
        LeftToRight,
        RightToLeft,
    }

    [Header("Clouds")]
    [Tooltip("Cloud prefabs; one is picked at random per cloud slot.")]
    [SerializeField] private List<GameObject> cloudPrefabs = new List<GameObject>();

    [Tooltip("How many clouds are alive at once.")]
    [Min(1)]
    [SerializeField] private int cloudCount = 6;

    [Tooltip("Random horizontal drift speed, in units per second: x = min, y = max.")]
    [SerializeField] private Vector2 speedRange = new Vector2(0.3f, 1.2f);

    [SerializeField] private DriftDirection direction = DriftDirection.RandomPerCloud;

    [Header("Field")]
    [Tooltip("Bottom-left corner of the spawn field.")]
    [SerializeField] private Vector2 fieldMin = new Vector2(-12f, 3f);

    [Tooltip("Top-right corner of the spawn field.")]
    [SerializeField] private Vector2 fieldMax = new Vector2(12f, 9f);

    [Tooltip("Extra distance past the field edge before a cloud recycles, so wide sprites fully leave view first.")]
    [Min(0f)]
    [SerializeField] private float edgePadding = 2f;

    [Tooltip("Scatter the initial clouds across the whole field so the sky is not empty at start. " +
             "Off = everything starts at the edges, strictly.")]
    [SerializeField] private bool prefillField = true;

    private class Cloud
    {
        public Transform transform;
        public float speed;
        public int direction; // +1 = rightward, -1 = leftward
    }

    private readonly List<Cloud> clouds = new List<Cloud>();

    private void Start()
    {
        if (cloudPrefabs == null || cloudPrefabs.Count == 0)
        {
            Debug.LogWarning($"{nameof(CloudManager)}: no cloud prefabs assigned; disabling.", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < cloudCount; i++)
            SpawnCloud(scatter: prefillField);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        foreach (Cloud cloud in clouds)
        {
            if (cloud.transform == null)
                continue;

            Vector3 position = cloud.transform.position;
            position.x += cloud.direction * cloud.speed * dt;

            // Fully past the far edge: recycle back to this cloud's start side
            // with a fresh height and speed, like a new cloud rolling in.
            float exitEdge = cloud.direction > 0 ? fieldMax.x + edgePadding : fieldMin.x - edgePadding;
            if (cloud.direction > 0 ? position.x > exitEdge : position.x < exitEdge)
            {
                RerollCloud(cloud, scatter: false);
                continue;
            }

            cloud.transform.position = position;
        }
    }

    private void SpawnCloud(bool scatter)
    {
        GameObject prefab = cloudPrefabs[Random.Range(0, cloudPrefabs.Count)];
        if (prefab == null)
            return;

        GameObject instance = Instantiate(prefab, transform);
        var cloud = new Cloud { transform = instance.transform };
        clouds.Add(cloud);
        RerollCloud(cloud, scatter);
    }

    private void RerollCloud(Cloud cloud, bool scatter)
    {
        cloud.direction = direction switch
        {
            DriftDirection.LeftToRight => 1,
            DriftDirection.RightToLeft => -1,
            _ => Random.value < 0.5f ? 1 : -1,
        };
        cloud.speed = Random.Range(speedRange.x, speedRange.y);

        // Enter at the start edge (left edge for rightward clouds, right edge
        // for leftward ones) — or anywhere in the field when prefilling.
        float x = scatter
            ? Random.Range(fieldMin.x, fieldMax.x)
            : (cloud.direction > 0 ? fieldMin.x - edgePadding : fieldMax.x + edgePadding);

        Vector3 position = cloud.transform.position;
        position.x = x;
        position.y = Random.Range(fieldMin.y, fieldMax.y);
        cloud.transform.position = position;
    }

    private void OnValidate()
    {
        if (speedRange.x < 0f) speedRange.x = 0f;
        if (speedRange.y < speedRange.x) speedRange.y = speedRange.x;
        if (fieldMax.x < fieldMin.x) fieldMax.x = fieldMin.x;
        if (fieldMax.y < fieldMin.y) fieldMax.y = fieldMin.y;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.8f, 1f);
        Vector3 center = (Vector3)(fieldMin + fieldMax) * 0.5f;
        Vector3 size = (Vector3)(fieldMax - fieldMin);
        Gizmos.DrawWireCube(center, size);

        // Entry edges, with the padding shown.
        Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.4f);
        Gizmos.DrawLine(new Vector3(fieldMin.x - edgePadding, fieldMin.y), new Vector3(fieldMin.x - edgePadding, fieldMax.y));
        Gizmos.DrawLine(new Vector3(fieldMax.x + edgePadding, fieldMin.y), new Vector3(fieldMax.x + edgePadding, fieldMax.y));
    }
}
