using UnityEngine;
using UnityEngine.UI;

public class GlowEffect : MonoBehaviour
{
    private Image buttonImage;
    private Vector3 originalScale;
    private Color originalColor;

    public float glowIntensity = 0.2f; // Intensity of RGB change
    public float scaleIntensity = 1.2f; // Scale multiplier on trigger enter

    private void Start()
    {
        buttonImage = GetComponent<Image>();
        if (buttonImage == null)
        {
            Debug.LogWarning($"{gameObject.name}: No Image component found for GlowEffect.");
            return;
        }

        // Store the original scale and color
        originalScale = transform.localScale;
        originalColor = buttonImage.color;

        Debug.Log($"{gameObject.name}: GlowEffect initialized.");
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("TriggerBox"))
        {
            Debug.Log($"{gameObject.name} entered TriggerBox.");
            ApplyGlowEffect();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("TriggerBox"))
        {
            Debug.Log($"{gameObject.name} exited TriggerBox.");
            ResetGlowEffect();
        }
    }

    private void ApplyGlowEffect()
    {
        if (buttonImage == null) return;

        // Increase RGB values for the glow effect
        Color newColor = buttonImage.color;
        newColor.r = Mathf.Clamp01(originalColor.r + glowIntensity);
        newColor.g = Mathf.Clamp01(originalColor.g + glowIntensity);
        newColor.b = Mathf.Clamp01(originalColor.b + glowIntensity);
        buttonImage.color = newColor;

        // Increase scale for visual effect
        transform.localScale = originalScale * scaleIntensity;

        Debug.Log($"{gameObject.name}: Glow effect applied.");
    }

    private void ResetGlowEffect()
    {
        if (buttonImage == null) return;

        // Reset RGB and scale to original
        buttonImage.color = originalColor;
        transform.localScale = originalScale;

        Debug.Log($"{gameObject.name}: Glow effect reset.");
    }
}
