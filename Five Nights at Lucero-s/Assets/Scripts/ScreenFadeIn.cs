using UnityEngine;
using UnityEngine.UI;

public class ScreenFadeIn : MonoBehaviour
{
    public Image fadeImage;
    public float fadeDuration = 1f;

    private void Awake()
    {
        if (fadeImage == null)
            fadeImage = GetComponent<Image>();
        SetAlpha(1f); // Start fully black
    }

    private void Start()
    {
        FadeIn();
    }

    public void FadeIn()
    {
        fadeImage.gameObject.SetActive(true);
        StartCoroutine(FadeInCoroutine());
    }

    public System.Collections.IEnumerator FadeInCoroutine()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            SetAlpha(alpha);
            yield return null;
        }
        SetAlpha(0f);
        fadeImage.gameObject.SetActive(false);
    }

    public void FadeOut()
    {
        fadeImage.gameObject.SetActive(true);
        StartCoroutine(FadeOutCoroutine());
    }

    public System.Collections.IEnumerator FadeOutCoroutine()
    {
        fadeImage.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            SetAlpha(alpha);
            yield return null;
        }
        SetAlpha(1f);
    }

    // Now public for use by other scripts
    public void SetAlpha(float alpha)
    {
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = alpha;
            fadeImage.color = c;
        }
    }
}