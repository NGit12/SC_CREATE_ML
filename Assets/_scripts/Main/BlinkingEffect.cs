using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BlinkingEffect : MonoBehaviour
{
    private Coroutine blinkingCoroutine;
    private Image buttonImage;

    private void Awake()
    {
        buttonImage = GetComponent<Image>();
    }

    public void StartBlinking()
    {
        if (blinkingCoroutine == null)
        {
            blinkingCoroutine = StartCoroutine(Blink());
        }
    }

    public void StopBlinking()
    {
        if (blinkingCoroutine != null)
        {
            StopCoroutine(blinkingCoroutine);
            blinkingCoroutine = null;

            // Reset to original color when blinking stops
            if (buttonImage != null)
            {
                buttonImage.color = Color.white;
            }
        }
    }

    private IEnumerator Blink()
    {
        if (buttonImage == null) yield break;

        Color originalColor = Color.white;
        Color blinkColor = Color.yellow;

        while (true)
        {
            buttonImage.color = blinkColor;
            yield return new WaitForSeconds(0.5f);
            buttonImage.color = originalColor;
            yield return new WaitForSeconds(0.5f);
        }
    }
}