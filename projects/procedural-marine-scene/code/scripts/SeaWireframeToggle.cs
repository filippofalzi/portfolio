using UnityEngine;

public class SeaWireframeToggle : MonoBehaviour
{
    [Header("References")]
    public Material seaMaterial;

    [Header("Controls")]
    public KeyCode toggleKey = KeyCode.F2;

    [Header("State")]
    public bool wireframe;

    static readonly int WireframeId = Shader.PropertyToID("_Wireframe");

    void Start()
    {
        Apply();
    }

    void Update()
    {
        if (seaMaterial == null)
            return;

        if (Input.GetKeyDown(toggleKey))
        {
            wireframe = !wireframe;
            Apply();
        }
    }

    void Apply()
    {
        if (seaMaterial == null)
            return;

        seaMaterial.SetFloat(WireframeId, wireframe ? 1f : 0f);
    }
}
