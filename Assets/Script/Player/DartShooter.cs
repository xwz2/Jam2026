using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Fires a <see cref="Dart"/> horizontally in the direction the character is
/// facing (F key or left mouse button). If no dart prefab is assigned it builds
/// a minimal one at runtime so the mechanic is testable before art exists.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class DartShooter : MonoBehaviour
{
    [Header("Dart")]
    [Tooltip("Prefab with a Dart component. Leave empty to auto-generate a simple placeholder dart.")]
    [SerializeField] private Dart dartPrefab;

    [Tooltip("Dart flight speed, in units per second.")]
    [SerializeField] private float dartSpeed = 12f;

    [Tooltip("Seconds before a dart that hits nothing despawns.")]
    [SerializeField] private float dartLifetime = 4f;

    [Header("Firing")]
    [Tooltip("Minimum seconds between shots.")]
    [Min(0f)]
    [SerializeField] private float fireCooldown = 0.25f;

    [Tooltip("Where darts appear. If empty, they spawn from the character's center, nudged toward the muzzle side.")]
    [SerializeField] private Transform muzzle;

    [Tooltip("Horizontal offset from the character's center when no muzzle transform is set.")]
    [SerializeField] private float muzzleOffset = 0.45f;

    private PlayerController controller;
    private float lastFireTime = float.NegativeInfinity;
    private int facing = 1;
    private Dart runtimeFallbackPrefab;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (!controller.IsAlive)
            return;

        // Remember the last direction the player pushed, so the character can
        // stand still and keep shooting the way it faces.
        if (controller.MoveInput > 0.01f) facing = 1;
        else if (controller.MoveInput < -0.01f) facing = -1;

        bool firePressed =
            (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (firePressed && Time.time - lastFireTime >= fireCooldown)
            Fire();
    }

    private void Fire()
    {
        lastFireTime = Time.time;

        Vector3 origin = muzzle != null
            ? muzzle.position
            : transform.position + new Vector3(facing * muzzleOffset, 0f, 0f);

        Dart prefab = dartPrefab != null ? dartPrefab : GetOrBuildFallbackPrefab();
        Dart dart = Instantiate(prefab, origin, Quaternion.identity);
        dart.gameObject.SetActive(true);

        IgnoreOwnCollider(dart);
        dart.Launch(facing, dartSpeed, dartLifetime);
    }

    /// <summary>A dart must never explode on the character that threw it.</summary>
    private void IgnoreOwnCollider(Dart dart)
    {
        Collider2D mine = GetComponent<Collider2D>();
        Collider2D theirs = dart.GetComponent<Collider2D>();
        if (mine != null && theirs != null)
            Physics2D.IgnoreCollision(mine, theirs);
    }

    /// <summary>
    /// Placeholder dart: a thin white rectangle. Kept inactive and reused as the
    /// Instantiate template. Replace with a real prefab when art is ready.
    /// </summary>
    private Dart GetOrBuildFallbackPrefab()
    {
        if (runtimeFallbackPrefab != null)
            return runtimeFallbackPrefab;

        var go = new GameObject("Dart (runtime placeholder)");
        go.SetActive(false);
        DontDestroyOnLoad(go);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit: 16f); // white texture is 4x4 -> 0.25 units long
        renderer.color = new Color(0.95f, 0.9f, 0.3f);
        go.transform.localScale = new Vector3(1f, 0.25f, 1f);

        var collider = go.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1f, 1f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        runtimeFallbackPrefab = go.AddComponent<Dart>();
        return runtimeFallbackPrefab;
    }
}
