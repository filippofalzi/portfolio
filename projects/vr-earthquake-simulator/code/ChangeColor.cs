using UnityEngine;

public class ColorChanger : MonoBehaviour
{
    private Color originalColor;
    private MeshRenderer meshRenderer;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            // Save the  original color
            originalColor = meshRenderer.material.color;
        }
    }

    public void SetRed()
    {
        // Component turning red
        if (meshRenderer != null)
            meshRenderer.material.color = Color.red;
    }

    public void ResetColor()
    {
        if (meshRenderer != null)
        {
            // Back to the original color
            meshRenderer.material.color = originalColor;
        }
    }
}