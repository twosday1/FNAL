using UnityEngine;
using UnityEngine.UI;

public class ScreenFadeIn : MonoBehaviour
{
    public Image fadeImage;
    public float fadeDuration = 1f;

    // When true the fade image is forced black and won't be deactivated by FadeInCoroutine.
    private bool forcedBlack = false;

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
        if (fadeImage == null) return;
        forcedBlack = false;
        fadeImage.gameObject.SetActive(true);
        StartCoroutine(FadeInCoroutine());
    }

    public System.Collections.IEnumerator FadeInCoroutine()
    {
        if (fadeImage == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            SetAlpha(alpha);
            yield return null;
        }

        // Only hide the image if it is not being forced black
        if (!forcedBlack)
        {
            SetAlpha(0f);
            fadeImage.gameObject.SetActive(false);
        }
        else
        {
            SetAlpha(1f);
            fadeImage.gameObject.SetActive(true);
        }
    }

    public void FadeOut()
    {
        if (fadeImage == null) return;
        fadeImage.gameObject.SetActive(true);
        StartCoroutine(FadeOutCoroutine());
    }

    public System.Collections.IEnumerator FadeOutCoroutine()
    {
        if (fadeImage == null) yield break;

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

    // Force the screen fully black immediately and make sure the fade image is on top.
    // This stops any fading coroutines and keeps the image active until ReleaseForcedBlack is called.
    public void ForceBlackImmediate(int sortingOrder = 10000)
    {
        StopAllCoroutines();
        forcedBlack = true;

        if (fadeImage == null)
            fadeImage = GetComponent<Image>();
        if (fadeImage == null) return;

        // Ensure the image is full-screen
        var rt = fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        // Force color to opaque black
        fadeImage.color = new Color(0f, 0f, 0f, 1f);
        fadeImage.gameObject.SetActive(true);

        // Ensure the canvas sorts on top
        var canvas = fadeImage.canvas;
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }
    }

    // Release the forced black state and optionally fade back in.
    public void ReleaseForcedBlack(bool fadeBackIn = true)
    {
        forcedBlack = false;
        if (fadeBackIn)
            FadeIn();
        else
        {
            if (fadeImage != null)
            {
                SetAlpha(0f);
                fadeImage.gameObject.SetActive(false);
            }
        }
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