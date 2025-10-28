using UnityEngine;
using UnityEngine.UI;

public class TextFadeOut : MonoBehaviour
{
    public Text uiText;           // Assign your UI Text in the Inspector
    public float fadeDuration = 1f; // Duration of the fade out

    private Color originalColor;

    void Start()
    {
        if (uiText == null)
            uiText = GetComponent<Text>();

        originalColor = uiText.color;
        Invoke(nameof(StartFade), 3f);
    }

    void StartFade()
    {
        StartCoroutine(FadeOut());
    }

    System.Collections.IEnumerator FadeOut()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            uiText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }
        uiText.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
    }
}