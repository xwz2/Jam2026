using UnityEngine;

/// <summary>
/// Fan/updraft particle area: white dots spawn inside a square region and rise
/// straight up — no sideways spread — fading out near the top of their life.
/// Drop this on an empty GameObject; it builds and configures its own
/// ParticleSystem from the fields below (re-applied live when you edit them in
/// Play mode). Purely visual: no physics, no collisions.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(ParticleSystem))]
public class FanParticles : MonoBehaviour
{
    [Header("Area")]
    [Tooltip("The square region, in world units, centered on this object. Dots are born on its BOTTOM " +
             "edge and rise through it, fading out exactly at the TOP edge.")]
    [SerializeField] private Vector2 areaSize = new Vector2(2f, 3f);

    [Header("Particles")]
    [Tooltip("Dots spawned per second along the bottom edge.")]
    [Min(0f)]
    [SerializeField] private float emissionRate = 20f;

    [Tooltip("Upward speed, in units per second. Together with the area height this sets each dot's life: " +
             "lifetime = height / speed.")]
    [Min(0.05f)]
    [SerializeField] private float riseSpeed = 2f;

    [Tooltip("Random speed variation per dot, as a fraction. 0.1 = +/-10%. Faster dots overshoot the top " +
             "slightly before fading; keep small for a tight column.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float speedJitter = 0.1f;

    [Tooltip("Dot diameter, in world units: x = min, y = max.")]
    [SerializeField] private Vector2 sizeRange = new Vector2(0.04f, 0.1f);

    [SerializeField] private Color dotColor = Color.white;

    [Header("Rendering")]
    [Tooltip("Sorting order of the dots. Negative = behind gameplay, positive = in front.")]
    [SerializeField] private int sortingOrder = 5;

    private ParticleSystem ps;
    private static Material dotMaterial;

    /// <summary>The travel square, for components (e.g. FanForce) that mirror this area.</summary>
    public Vector2 AreaSize => areaSize;

    private void OnEnable()
    {
        ps = GetComponent<ParticleSystem>();
        Apply();
    }

    /// <summary>Pushes the inspector fields into the ParticleSystem modules.</summary>
    private void Apply()
    {
        // Travel time from the bottom edge to the top edge at the base speed.
        float lifetime = areaSize.y / riseSpeed;

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startSpeed = 0f; // rise comes from velocityOverLifetime, keeping it perfectly vertical
        main.startLifetime = lifetime;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
        main.startColor = dotColor;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; // the whole column moves with the object
        main.maxParticles = Mathf.CeilToInt(emissionRate * lifetime) + 8;

        var emission = ps.emission;
        emission.rateOverTime = emissionRate;

        // Thin strip along the BOTTOM edge of the square: the fan's mouth.
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = new Vector3(0f, -areaSize.y * 0.5f, 0f);
        shape.scale = new Vector3(areaSize.x, 0.05f, 0.01f);

        // Straight up. Zero X/Z drift; small per-dot speed jitter for life.
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = 0f;
        velocity.z = 0f;
        velocity.y = new ParticleSystem.MinMaxCurve(
            riseSpeed * (1f - speedJitter),
            riseSpeed * (1f + speedJitter));

        // Fade in quickly, hold, fade out at the top so dots never pop.
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = gradient;

        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GetDotMaterial(); // shared: safe in edit mode, no material leaks
        renderer.sortingOrder = sortingOrder;
    }

    /// <summary>Round soft dot texture built once in code, so no art asset is needed.</summary>
    private static Material GetDotMaterial()
    {
        if (dotMaterial != null)
            return dotMaterial;

        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                float a = Mathf.Clamp01(1f - d * d * d); // solid core, soft edge
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        texture.Apply();

        // Sprites/Default: URP-compatible, alpha-blended, and respects particle
        // color — exactly right for soft white dots, no configuration needed.
        dotMaterial = new Material(Shader.Find("Sprites/Default"));
        dotMaterial.mainTexture = texture;
        return dotMaterial;
    }

    // Live tuning: re-apply whenever a field changes, in edit mode and Play mode alike.
    private void OnValidate()
    {
        if (ps == null)
            ps = GetComponent<ParticleSystem>();
        if (ps != null)
            Apply();
    }

    private void OnDrawGizmosSelected()
    {
        // The square region the dots travel through.
        Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
        Gizmos.DrawWireCube(transform.position, new Vector3(areaSize.x, areaSize.y, 0f));

        // Emphasize the bottom edge (the fan mouth) and the up direction.
        Vector3 bottomLeft = transform.position + new Vector3(-areaSize.x * 0.5f, -areaSize.y * 0.5f, 0f);
        Vector3 bottomRight = transform.position + new Vector3(areaSize.x * 0.5f, -areaSize.y * 0.5f, 0f);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Gizmos.DrawLine(transform.position + Vector3.down * areaSize.y * 0.5f,
                        transform.position + Vector3.up * areaSize.y * 0.5f);
    }
}
