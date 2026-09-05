using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns a random block from <see cref="blockPrefabs"/> in the sky, hands
/// keyboard control to it, and waits until it has landed on the ground or the
/// stack. Once it lands, the manager counts down <see cref="spawnDelay"/>
/// seconds and spawns the next one.
/// </summary>
public class GameManagerTetris : MonoBehaviour
{
    [Header("Blocks")]
    [Tooltip("Prefabs from Assets/Prefab/building blocks. One is picked at random per spawn.")]
    [SerializeField] private List<GameObject> blockPrefabs = new List<GameObject>();

    [Header("Spawning")]
    [Tooltip("Seconds to wait AFTER the previous block lands before spawning the next one.")]
    [Min(0f)]
    [FormerlySerializedAs("spawnInterval")]
    [SerializeField] private float spawnDelay = 2f;

    [Tooltip("Safety net: if a block never lands (it fell out of the world, or the player keeps steering it), " +
             "give up after this many seconds and spawn anyway. 0 = wait forever.")]
    [Min(0f)]
    [SerializeField] private float maxWaitForLanding = 15f;

    [Tooltip("Optional spawn anchor. If empty, spawnPosition below is used instead.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Used when no spawn point is assigned.")]
    [SerializeField] private Vector2 spawnPosition = new Vector2(0f, 6f);

    [Tooltip("Random horizontal offset applied around the spawn point (0 = always the same column).")]
    [SerializeField] private float spawnXJitter = 0f;

    [Tooltip("Spawn the first block immediately instead of waiting one delay.")]
    [SerializeField] private bool spawnOnStart = true;

    [Tooltip("Parent for the spawned blocks, purely to keep the hierarchy tidy.")]
    [SerializeField] private Transform blocksParent;

    [Header("Block control settings")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float rotationSpeed = 180f;

    [Header("Block fall settings")]
    [Tooltip("Gravity multiplier applied to every spawned block. Lower = slower fall, more thinking time. 1 = normal gravity.")]
    [SerializeField] private float gravityScale = 0.35f;

    [Tooltip("Maximum downward speed in units per second, so blocks never accelerate out of control. 0 = no limit.")]
    [SerializeField] private float maxFallSpeed = 2f;

    [Tooltip("Downward speed while the player holds S / down arrow to drop the block early.")]
    [SerializeField] private float softDropSpeed = 8f;

    [Header("Landing detection")]
    [Tooltip("A block counts as resting below this linear speed, in units per second.")]
    [SerializeField] private float settleSpeedThreshold = 0.25f;

    [Tooltip("A block counts as resting below this spin speed, in degrees per second.")]
    [SerializeField] private float settleAngularThreshold = 20f;

    [Tooltip("How long a block must stay still, while touching something, before it counts as landed.")]
    [SerializeField] private float settleTime = 0.25f;

    [Tooltip("Take keyboard control away from a block as soon as it lands.")]
    [SerializeField] private bool releaseControlOnLanding = true;

    [Header("Bounds")]
    [SerializeField] private bool clampHorizontally = true;
    [SerializeField] private float minX = -8f;
    [SerializeField] private float maxX = 8f;

    private readonly List<BlockController> spawnedBlocks = new List<BlockController>();
    private BlockController activeBlock;
    private bool waitingForLanding;
    private float landingWaitTimer;
    private float spawnTimer;
    private bool isRunning = true;

    /// <summary>Seconds between a block landing and the next one spawning. Editable in the inspector and at runtime.</summary>
    public float SpawnDelay
    {
        get => spawnDelay;
        set => spawnDelay = Mathf.Max(0f, value);
    }

    /// <summary>The block the player is currently steering, or null.</summary>
    public BlockController ActiveBlock => activeBlock;

    /// <summary>Every block spawned so far, oldest first.</summary>
    public IReadOnlyList<BlockController> SpawnedBlocks => spawnedBlocks;

    /// <summary>True while the manager is waiting for the current block to come to rest.</summary>
    public bool IsWaitingForLanding => waitingForLanding;

    private void Start()
    {
        if (blockPrefabs == null || blockPrefabs.Count == 0)
        {
            Debug.LogWarning($"{nameof(GameManagerTetris)}: no block prefabs assigned, nothing will spawn.", this);
            return;
        }

        if (spawnOnStart)
            SpawnBlock();
        else
            spawnTimer = spawnDelay;
    }

    private void OnDisable()
    {
        // Do not leave a dangling subscription on a block that outlives this manager.
        if (activeBlock != null)
            activeBlock.Landed -= OnBlockLanded;
    }

    private void Update()
    {
        if (!isRunning || blockPrefabs == null || blockPrefabs.Count == 0)
            return;

        if (waitingForLanding)
        {
            WaitForLanding();
            return;
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
            SpawnBlock();
    }

    /// <summary>
    /// Nothing to do until the block reports it has landed, except watch the
    /// safety timeout so a block lost off-screen cannot stall the game.
    /// </summary>
    private void WaitForLanding()
    {
        if (activeBlock == null)
        {
            // The block was destroyed while falling; treat that as a landing.
            BeginSpawnCountdown();
            return;
        }

        if (maxWaitForLanding <= 0f)
            return;

        landingWaitTimer += Time.deltaTime;
        if (landingWaitTimer >= maxWaitForLanding)
            activeBlock.ForceLanded();
    }

    /// <summary>Called by the active block the moment it comes to rest.</summary>
    private void OnBlockLanded(BlockController block)
    {
        block.Landed -= OnBlockLanded;

        if (block != activeBlock)
            return;

        BeginSpawnCountdown();
    }

    private void BeginSpawnCountdown()
    {
        waitingForLanding = false;
        landingWaitTimer = 0f;
        spawnTimer = spawnDelay;
    }

    /// <summary>Spawns a random block right now and starts waiting for it to land.</summary>
    public void SpawnBlock()
    {
        GameObject prefab = PickRandomPrefab();
        if (prefab == null)
        {
            // Nothing to spawn: retry after the normal delay instead of every frame.
            spawnTimer = spawnDelay;
            return;
        }

        Vector3 position = spawnPoint != null ? spawnPoint.position : (Vector3)spawnPosition;
        if (spawnXJitter > 0f)
            position.x += Random.Range(-spawnXJitter, spawnXJitter);

        GameObject instance = Instantiate(prefab, position, Quaternion.identity, blocksParent);

        BlockController controller = instance.GetComponent<BlockController>();
        if (controller == null)
            controller = instance.AddComponent<BlockController>();

        controller.Configure(moveSpeed, rotationSpeed, gravityScale, maxFallSpeed, softDropSpeed,
            settleSpeedThreshold, settleAngularThreshold, settleTime, releaseControlOnLanding,
            clampHorizontally, minX, maxX);

        // Only the newest block listens to the keyboard.
        if (activeBlock != null)
        {
            activeBlock.Landed -= OnBlockLanded;
            activeBlock.ReleaseControl();
        }

        controller.TakeControl();
        controller.Landed += OnBlockLanded;

        activeBlock = controller;
        spawnedBlocks.Add(controller);

        waitingForLanding = true;
        landingWaitTimer = 0f;
    }

    private GameObject PickRandomPrefab()
    {
        // Skip empty slots so a half-filled list in the inspector still works.
        for (int attempt = 0; attempt < blockPrefabs.Count; attempt++)
        {
            GameObject candidate = blockPrefabs[Random.Range(0, blockPrefabs.Count)];
            if (candidate != null)
                return candidate;
        }

        Debug.LogWarning($"{nameof(GameManagerTetris)}: block prefab list contains only empty entries.", this);
        return null;
    }

    /// <summary>Stops spawning and drops control of the current block.</summary>
    public void StopSpawning()
    {
        isRunning = false;
        if (activeBlock != null)
            activeBlock.ReleaseControl();
    }

    /// <summary>Resumes spawning after <see cref="StopSpawning"/>.</summary>
    public void ResumeSpawning()
    {
        isRunning = true;
        if (!waitingForLanding)
            spawnTimer = spawnDelay;
    }

    /// <summary>Removes every spawned block and starts over.</summary>
    public void ResetBlocks()
    {
        if (activeBlock != null)
            activeBlock.Landed -= OnBlockLanded;

        foreach (BlockController block in spawnedBlocks)
        {
            if (block != null)
                Destroy(block.gameObject);
        }

        spawnedBlocks.Clear();
        activeBlock = null;
        waitingForLanding = false;
        landingWaitTimer = 0f;
        spawnTimer = spawnDelay;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = spawnPoint != null ? spawnPoint.position : (Vector3)spawnPosition;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, 0.25f);
        if (spawnXJitter > 0f)
            Gizmos.DrawLine(origin + Vector3.left * spawnXJitter, origin + Vector3.right * spawnXJitter);

        if (clampHorizontally)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(minX, origin.y + 1f, 0f), new Vector3(minX, origin.y - 20f, 0f));
            Gizmos.DrawLine(new Vector3(maxX, origin.y + 1f, 0f), new Vector3(maxX, origin.y - 20f, 0f));
        }
    }
}
