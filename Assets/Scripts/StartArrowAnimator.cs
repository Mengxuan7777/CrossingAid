using UnityEngine;

public class StartArrowAnimator : MonoBehaviour
{
    [Header("Renderer")]
    public Renderer arrowRenderer;

    [Header("Color")]
    [Tooltip("Color at the tail (right side) of the arrow.")]
    public Color colorTail = Color.white;

    [Tooltip("Color at the tip (left side) of the arrow.")]
    public Color colorTip = new Color(1f, 1f, 1f, 0f);

    [Header("Wipe")]
    [Tooltip("How many times the fade repeats across the arrow's length.")]
    [Range(0.1f, 5f)]
    public float gradientTiling = 1f;

    [Tooltip("How fast the transparent band sweeps from the tail (right) to the tip (left).")]
    public float scrollSpeed = 0.5f;

    private Material _mat;
    private float _startTime;

    private void Start()
    {
        arrowRenderer ??= GetComponent<Renderer>();
        BuildGradientTexture();
        _startTime = Time.time;
    }

    [ContextMenu("Rebuild Gradient")]
    public void RebuildGradient() => BuildGradientTexture();

    /// <summary>Restarts the wipe animation from the beginning.</summary>
    public void Play()
    {
        _startTime = Time.time;
        enabled = true;
    }

    private void Update()
    {
        if (_mat == null) return;

        float offset = ((Time.time - _startTime) * scrollSpeed) % 1f;
        _mat.mainTextureOffset = new Vector2(offset, 0f);
    }

    private void BuildGradientTexture()
    {
        if (arrowRenderer == null) return;

        const int size = 256;
        var tex = new Texture2D(size, 1, TextureFormat.RGBA32, false);
        var pixels = new Color[size];

        for (int i = 0; i < size; i++)
        {
            // i=0 -> right/tail, i=size-1 -> left/tip,
            // so the gradient appears to point toward the tip.
            float t = (float)i / (size - 1);
            pixels[i] = Color.Lerp(colorTail, colorTip, t);
        }

        tex.SetPixels(pixels);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply();

        Material source = arrowRenderer.sharedMaterial;
        _mat = source != null ? new Material(source) : new Material(Shader.Find("Unlit/Transparent"));
        _mat.mainTexture = tex;
        _mat.mainTextureScale = new Vector2(gradientTiling, 1f);
        arrowRenderer.material = _mat;
    }
}
