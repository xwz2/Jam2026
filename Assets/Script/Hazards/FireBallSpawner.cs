using UnityEngine;

/// <summary>
/// Dedicated spawner for <see cref="Fireball"/> hazards. On each cycle it rolls
/// a random whole number of fireballs (Count Range) and rains them from the
/// spawn area aimed at the ground, each with a random tilt of up to
/// ± Rotation Jitter degrees. The fireballs' own movement code then carries
/// them along that heading.
/// </summary>
public class FireBallSpawner : MonoBehaviour
{
    [Header("What to spawn")]
    [Tooltip("The fireball prefab (needs a Fireball component).")]
    [SerializeField] private Fireball fireballPrefab;

    [Tooltip("Parent for spawned fireballs, purely to keep the hierarchy tidy. Empty = scene root.")]
    [SerializeField] private Transform fireballsParent;

    [Header("When to spawn")]
    [Tooltip("Master switch.")]
    [SerializeField] private bool spawning = true;

    [Tooltip("Random seconds between waves: x = min, y = max.")]
    [SerializeField] private Vector2 intervalRange = new Vector2(4f, 8f);

    [Tooltip("How many fireballs each wave spawns, picked as a random whole number from min to max (inclusive).")]
    [SerializeField] private int minCount = 1;
    [SerializeField] private int maxCount = 3;

    [Header("Where to spawn")]
    [Tooltip("Bottom-left corner of the spawn rectangle. Put it above the playfield so fireballs rain down.")]
    [SerializeField] private Vector2 areaMin = new Vector2(-9f, 7f);

    [Tooltip("Top-right corner of the spawn rectangle.")]
    [SerializeField] private Vector2 areaMax = new Vector2(9f, 9f);

    [Header("Direction")]
    [Tooltip("Base heading in degrees. -90 = straight down at the ground (fireballs fly along their right axis).")]
    [SerializeField] private float baseAngle = -90f;

    [Tooltip("Random tilt added to the base heading, in degrees: each fireball gets +/- up to this much.")]
    [Min(0f)]
    [SerializeField] private float rotationJitter = 25f;

    [Header("Fireball settings")]
    [Tooltip("Random flight speed, in units per second: x = min, y = max. Each fireball rolls its own.")]
    [SerializeField] private Vector2 speedRange = new Vector2(2f, 4.5f);

    [Tooltip("Random seconds a fireball lives before fading out: x = min, y = max.")]
    [SerializeField] private Vector2 lifetimeRange = new Vector2(10f, 15f);

    [Tooltip("Random size multiplier applied to the fireball's prefab scale: x = min, y = max. " +
             "1 = authored size, 0.5 = half size. Scales the whole prefab, collider and explosion included.")]
    [SerializeField] private Vector2 sizeRange = new Vector2(0.5f, 1f);

    private float timer;

    /// <summary>Runtime switch, e.g. to pause the hazard from a game-over script.</summary>
    public bool Spawning
    {
        get => spawning;
        set => spawning = value;
    }

    private void Start()
    {
        if (fireballPrefab == null)
        {
            Debug.LogWarning($"{nameof(FireBallSpawner)}: no fireball prefab assigned; disabling.", this);
            enabled = false;
            return;
        }

        timer = Random.Range(intervalRange.x, intervalRange.y);
    }

    private void Update()
    {
        if (!spawning)
            return;

        timer -= Time.deltaTime;
        if (timer > 0f)
            return;

        timer = Random.Range(intervalRange.x, intervalRange.y);
        SpawnWave();
    }

    /// <summary>Spawns one wave right now: a random count of fireballs, each aimed down with jitter.</summary>
    public void SpawnWave()
    {
        // Int Random.Range excludes the max, so +1 makes maxCount reachable.
        int count = Random.Range(minCount, maxCount + 1);
        for (int i = 0; i < count; i++)
            SpawnOne();
    }

    private void SpawnOne()
    {
        Vector3 position = new Vector3(
            Random.Range(areaMin.x, areaMax.x),
            Random.Range(areaMin.y, areaMax.y),
            0f);

        float angle = baseAngle + Random.Range(-rotationJitter, rotationJitter);
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        Fireball fireball = Instantiate(fireballPrefab, position, rotation, fireballsParent);

        // Multiply rather than overwrite, so an artist-tuned prefab scale survives.
        float sizeCoefficient = Random.Range(sizeRange.x, sizeRange.y);
        fireball.transform.localScale *= sizeCoefficient;

        fireball.Init(
            Random.Range(speedRange.x, speedRange.y),
            Random.Range(lifetimeRange.x, lifetimeRange.y));
    }

    private void OnValidate()
    {
        minCount = Mathf.Max(1, minCount);
        maxCount = Mathf.Max(minCount, maxCount);
        if (intervalRange.y < intervalRange.x)
            intervalRange.y = intervalRange.x;
        if (lifetimeRange.y < lifetimeRange.x)
            lifetimeRange.y = lifetimeRange.x;
        speedRange.x = Mathf.Max(0.1f, speedRange.x);
        if (speedRange.y < speedRange.x)
            speedRange.y = speedRange.x;
        sizeRange.x = Mathf.Max(0.05f, sizeRange.x);
        if (sizeRange.y < sizeRange.x)
            sizeRange.y = sizeRange.x;
    }

    private void OnDrawGizmosSelected()
    {
        // Spawn rectangle.
        Gizmos.color = new Color(1f, 0.4f, 0.1f);
        Vector3 center = (Vector3)(areaMin + areaMax) * 0.5f;
        Vector3 size = (Vector3)(areaMax - areaMin);
        Gizmos.DrawWireCube(center, size);

        // The cone of possible headings, drawn from the rectangle's center.
        Vector3 baseDir = Quaternion.Euler(0f, 0f, baseAngle) * Vector3.right;
        Vector3 leftDir = Quaternion.Euler(0f, 0f, baseAngle + rotationJitter) * Vector3.right;
        Vector3 rightDir = Quaternion.Euler(0f, 0f, baseAngle - rotationJitter) * Vector3.right;
        Gizmos.DrawRay(center, baseDir * 2f);
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.4f);
        Gizmos.DrawRay(center, leftDir * 2f);
        Gizmos.DrawRay(center, rightDir * 2f);
    }
}
