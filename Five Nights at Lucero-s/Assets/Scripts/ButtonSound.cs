using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Helper to wire a UI Button to a ButtonSound and support pointer-down -> footstep loop behavior.
/// - Attach to the same GameObject as a UI Button, or assign Button and ButtonSound in the Inspector.
/// - Automatically hooks Button.onClick to play a single sound (or PlayWalk if configured).
/// - Implements IPointerDownHandler/IPointerUpHandler to start/stop continuous walking sound.
/// - Adds a short hold threshold so quick clicks don't start/stop a loop (fixes single-play vs repeated-play conflict).
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonSoundBinder : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    [Tooltip("Button to wire (auto-assigned from this GameObject if empty).")]
    public Button button;
    [Tooltip("ButtonSound instance to control (auto-find in parents/children if empty).")]
    public ButtonSound buttonSound;

    [Header("Click behavior")]
    [Tooltip("If true, clicking the UI Button will call ButtonSound.PlayWalk(); otherwise it calls Play().")]
    public bool clickPlaysWalk = false;

    [Header("Pointer-hold behaviour")]
    [Tooltip("When true, holding pointer down longer than holdThreshold starts a continuous walk loop via StartWalkLoop().")]
    public bool pointerHoldStartsLoop = true;
    [Tooltip("Seconds the pointer must be held before the continuous walk loop starts.")]
    public float holdThreshold = 0.2f;
    [Tooltip("Interval (seconds) used when starting walk loop from pointer-hold. Leave <=0 to use ButtonSound.walkInterval.")]
    public float pointerHoldIntervalOverride = 0f;

    [Header("Debug")]
    public bool debugLogs = false;

    // internal
    private Coroutine pointerHoldCoroutine;
    private bool pointerIsDown;

    void Reset()
    {
        button = GetComponent<Button>();
        if (buttonSound == null)
            buttonSound = GetComponentInParent<ButtonSound>() ?? GetComponentInChildren<ButtonSound>();
    }

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (buttonSound == null)
            buttonSound = GetComponentInParent<ButtonSound>() ?? GetComponentInChildren<ButtonSound>();
    }

    void OnEnable()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
    }

    void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClick);
        }
    }

    private void OnButtonClick()
    {
        if (buttonSound == null)
        {
            if (debugLogs) Debug.LogWarning("[ButtonSoundBinder] No ButtonSound assigned.");
            return;
        }

        if (clickPlaysWalk)
        {
            if (debugLogs) Debug.Log("[ButtonSoundBinder] Button clicked -> PlayWalk()");
            buttonSound.PlayWalk();
        }
        else
        {
            if (debugLogs) Debug.Log("[ButtonSoundBinder] Button clicked -> Play()");
            buttonSound.Play();
        }
    }

    // Pointer down: start a delayed check to enter continuous loop only if held long enough.
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!pointerHoldStartsLoop || buttonSound == null) return;

        pointerIsDown = true;
        if (pointerHoldCoroutine != null) StopCoroutine(pointerHoldCoroutine);
        pointerHoldCoroutine = StartCoroutine(PointerHoldStarter());
    }

    // Pointer up: cancel pending hold check and stop any running loop
    public void OnPointerUp(PointerEventData eventData)
    {
        pointerIsDown = false;
        if (pointerHoldCoroutine != null)
        {
            StopCoroutine(pointerHoldCoroutine);
            pointerHoldCoroutine = null;
        }

        // If a loop was started, stop it. Safe to call regardless.
        if (!pointerHoldStartsLoop || buttonSound == null) return;
        if (debugLogs) Debug.Log("[ButtonSoundBinder] PointerUp -> StopWalk()");
        buttonSound.StopWalk();
    }

    private IEnumerator PointerHoldStarter()
    {
        float waited = 0f;
        while (waited < holdThreshold)
        {
            if (!pointerIsDown)
                yield break;
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        // still held => start loop
        if (!pointerIsDown) yield break;

        float interval = pointerHoldIntervalOverride > 0f ? pointerHoldIntervalOverride : buttonSound.walkInterval;
        if (debugLogs) Debug.Log($"[ButtonSoundBinder] PointerHold threshold reached -> StartWalkLoop(interval={interval})");
        buttonSound.StartWalkLoop(interval);
        pointerHoldCoroutine = null;
    }

    // Public helpers for manual wiring if preferred
    public void PlayOnce() => buttonSound?.Play();
    public void PlayWalkOnce() => buttonSound?.PlayWalk();
    public void StartWalk(float interval) => buttonSound?.StartWalkLoop(interval);
    public void StopWalk() => buttonSound?.StopWalk();
}

/// <summary>
/// ButtonSound: actual implementation that plays a single sound or repeated "walking" sounds.
/// - Assign an AudioSource or AudioClip (or both). Prefer AudioSource for better control.
/// - Use Play() for a single tap sound, PlayWalk()/PlayWalkTimes/StartWalkLoop/StopWalk for repeated footstep-like playback.
/// </summary>
public class ButtonSound : MonoBehaviour
{
    [Header("Playback")]
    [Tooltip("Optional AudioSource to use. If null a one-shot will be played via AudioSource.PlayClipAtPoint.")]
    public AudioSource audioSource;

    [Tooltip("Clip to play when the button is pressed. Required if no AudioSource.clip is set.")]
    public AudioClip clip;

    [Tooltip("Volume for PlayClipAtPoint (0..1). Ignored when using audioSource.PlayOneShot if audioSource exists.")]
    [Range(0f, 1f)]
    public float oneShotVolume = 1f;

    [Header("Walking (repeated) settings")]
    [Tooltip("Default number of times to play when calling PlayWalk().")]
    public int walkRepeatCount = 4;
    [Tooltip("Default seconds between plays when calling PlayWalk(). Lower = faster footsteps.")]
    public float walkInterval = 0.4f;
    [Tooltip("If true, PlayWalk will start a loop that repeats until StopWalk() is called.")]
    public bool walkLoop = false;

    [Header("Randomization (optional)")]
    [Tooltip("Randomize pitch per play for natural variation.")]
    public bool randomizePitch = false;
    public float pitchMin = 0.95f;
    public float pitchMax = 1.05f;

    [Header("Debug")]
    public bool debugLogs = false;

    // runtime
    private Coroutine walkCoroutine;

    /// <summary>
    /// Play a single sound immediately.
    /// </summary>
    public void Play()
    {
        // prefer explicit AudioSource if provided
        if (audioSource != null)
        {
            AudioClip toPlay = clip != null ? clip : audioSource.clip;
            if (toPlay == null)
            {
                if (debugLogs) Debug.LogWarning("[ButtonSound] No clip assigned and audioSource.clip is null.");
                return;
            }

            float originalPitch = audioSource.pitch;
            if (randomizePitch)
                audioSource.pitch = Random.Range(pitchMin, pitchMax);

            audioSource.PlayOneShot(toPlay);

            if (randomizePitch)
                audioSource.pitch = originalPitch;

            if (debugLogs) Debug.Log("[ButtonSound] Played via AudioSource (one-shot).");
            return;
        }

        // fallback to PlayClipAtPoint (creates a temporary audio source at camera position)
        if (clip != null)
        {
            Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(clip, pos, oneShotVolume);
            if (debugLogs) Debug.Log("[ButtonSound] Played via PlayClipAtPoint.");
        }
        else
        {
            if (debugLogs) Debug.LogWarning("[ButtonSound] No clip assigned to play.");
        }
    }

    /// <summary>
    /// Play the walking sound using inspector defaults. If walkLoop is true this starts a loop
    /// and returns immediately — call StopWalk() to stop.
    /// </summary>
    public void PlayWalk()
    {
        if (walkLoop)
        {
            StartWalkLoop(walkInterval);
        }
        else
        {
            PlayWalkTimes(walkRepeatCount, walkInterval);
        }
    }

    /// <summary>
    /// Play the walking sound a specific number of times at the specified interval (seconds).
    /// </summary>
    public void PlayWalkTimes(int repeatCount, float interval)
    {
        StopWalk(); // ensure only one coroutine active
        walkCoroutine = StartCoroutine(WalkRoutine(repeatCount, interval, loop: false));
    }

    /// <summary>
    /// Start continuous walking loop with given interval (seconds between plays).
    /// Call StopWalk() to end.
    /// </summary>
    public void StartWalkLoop(float interval)
    {
        StopWalk();
        walkCoroutine = StartCoroutine(WalkRoutine(-1, interval, loop: true));
    }

    /// <summary>
    /// Stop any running walking coroutine.
    /// </summary>
    public void StopWalk()
    {
        if (walkCoroutine != null)
        {
            StopCoroutine(walkCoroutine);
            walkCoroutine = null;
            if (debugLogs) Debug.Log("[ButtonSound] Walk stopped.");
        }
    }

    private IEnumerator WalkRoutine(int repeatCount, float interval, bool loop)
    {
        if (interval <= 0f)
            interval = 0.01f;

        if (debugLogs)
            Debug.Log($"[ButtonSound] WalkRoutine started (repeatCount={repeatCount}, interval={interval}, loop={loop})");

        do
        {
            int remaining = repeatCount;
            // -1 repeatCount => infinite handled by loop=true; otherwise play repeatCount times
            while (loop || remaining > 0)
            {
                // play one footstep
                if (audioSource != null)
                {
                    AudioClip toPlay = clip != null ? clip : audioSource.clip;
                    if (toPlay != null)
                    {
                        float originalPitch = audioSource.pitch;
                        if (randomizePitch)
                            audioSource.pitch = Random.Range(pitchMin, pitchMax);

                        audioSource.PlayOneShot(toPlay);

                        if (randomizePitch)
                            audioSource.pitch = originalPitch;
                    }
                    else if (debugLogs)
                    {
                        Debug.LogWarning("[ButtonSound] WalkRoutine: audioSource has no clip and no clip assigned.");
                    }
                }
                else
                {
                    if (clip != null)
                    {
                        Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                        AudioSource.PlayClipAtPoint(clip, pos, oneShotVolume);
                    }
                    else if (debugLogs)
                    {
                        Debug.LogWarning("[ButtonSound] WalkRoutine: no clip assigned to play.");
                    }
                }

                if (!loop)
                    remaining--;

                yield return new WaitForSeconds(interval);
            }
            // if not looping, exit; otherwise repeat again forever until StopWalk()
        } while (loop);

        walkCoroutine = null;
        if (debugLogs) Debug.Log("[ButtonSound] WalkRoutine finished.");
    }
}
