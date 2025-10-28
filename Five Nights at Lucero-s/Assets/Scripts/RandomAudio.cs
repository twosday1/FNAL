using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RandomAudio - attach to a GameObject to play sounds at random times.
/// - Assign one or more AudioSources in the Inspector (or leave empty to auto-collect children sources).
/// - Optionally assign AudioClips to play; if none provided the AudioSource.clip will be used.
/// - Configurable min/max intervals, per-opportunity chance and pitch/volume randomization.
/// - Can play multiple times and supports overlapping playback.
/// 
/// New: playback can be globally disabled/enabled via ResetAndDisablePlayback() and OnLevelStart().
/// </summary>
public class RandomAudio : MonoBehaviour
{
    [Header("Sources & Clips")]
    [Tooltip("AudioSources to use. If empty, will auto-collect all AudioSource components on this GameObject and children.")]
    public AudioSource[] audioSources;

    [Tooltip("Optional clips to choose from. If empty the chosen AudioSource.clip will be used.")]
    public AudioClip[] audioClips;

    [Header("Timing")]
    [Tooltip("Minimum seconds between random play opportunities.")]
    public float minInterval = 5f;
    [Tooltip("Maximum seconds between random play opportunities.")]
    public float maxInterval = 20f;
    [Tooltip("Chance (0..1) that a play will occur at each opportunity.")]
    [Range(0f, 1f)]
    public float playChance = 0.25f;
    [Tooltip("Start the random loop automatically on Start.")]
    public bool startOnStart = true;

    [Header("Playback")]
    [Tooltip("Allow multiple simultaneous sounds from this component (unlimited when true).")]
    public bool allowOverlap = true;
    [Tooltip("Maximum number of concurrent plays (ignored when allowOverlap is true).")]
    public int maxConcurrentPlays = 3;

    [Header("Randomization")]
    [Tooltip("Randomize pitch per play.")]
    public bool randomizePitch = false;
    public float pitchMin = 0.9f;
    public float pitchMax = 1.1f;

    [Tooltip("Randomize volume per play.")]
    public bool randomizeVolume = false;
    public float volumeMin = 0.8f;
    public float volumeMax = 1f;

    [Header("Debug")]
    public bool debugLogs = false;

    // runtime
    private int concurrentPlays = 0;
    private Coroutine loopCoroutine;
    private readonly List<AudioSource> sources = new List<AudioSource>();

    // new: controls whether random playback is allowed. LevelProgression should call ResetAndDisablePlayback()
    // on scene reset/player death and call OnLevelStart() when the level actually starts.
    private bool playbackEnabled = true;

    void Start()
    {
        CollectSourcesIfNeeded();
        if (startOnStart && playbackEnabled)
            StartRandomLoop();
    }

    void OnValidate()
    {
        if (minInterval < 0f) minInterval = 0f;
        if (maxInterval < minInterval) maxInterval = minInterval;
        if (maxConcurrentPlays < 1) maxConcurrentPlays = 1;
        pitchMin = Mathf.Max(0.01f, Mathf.Min(pitchMin, pitchMax));
        pitchMax = Mathf.Max(pitchMin, pitchMax);
        volumeMin = Mathf.Clamp01(volumeMin);
        volumeMax = Mathf.Clamp(volumeMin, 0f, 1f);
    }

    /// <summary>
    /// Ensure there are sources available; if none were assigned, collect children.
    /// </summary>
    public void CollectSourcesIfNeeded()
    {
        sources.Clear();
        if (audioSources != null && audioSources.Length > 0)
        {
            foreach (var s in audioSources) if (s != null) sources.Add(s);
        }

        if (sources.Count == 0)
        {
            var found = GetComponentsInChildren<AudioSource>(includeInactive: true);
            foreach (var s in found) sources.Add(s);
        }
    }

    /// <summary>
    /// Start the randomized playback loop.
    /// </summary>
    public void StartRandomLoop()
    {
        if (!playbackEnabled) return;
        if (loopCoroutine != null) return;
        CollectSourcesIfNeeded();
        loopCoroutine = StartCoroutine(RandomLoop());
    }

    /// <summary>
    /// Stop the randomized playback loop.
    /// </summary>
    public void StopRandomLoop()
    {
        if (loopCoroutine != null)
        {
            StopCoroutine(loopCoroutine);
            loopCoroutine = null;
        }
    }

    /// <summary>
    /// Register an AudioSource at runtime.
    /// </summary>
    public void RegisterSource(AudioSource src)
    {
        if (src == null) return;
        if (!sources.Contains(src)) sources.Add(src);
    }

    /// <summary>
    /// Unregister an AudioSource at runtime.
    /// </summary>
    public void UnregisterSource(AudioSource src)
    {
        if (src == null) return;
        sources.Remove(src);
    }

    /// <summary>
    /// Force an immediate random play attempt.
    /// </summary>
    public void TriggerImmediatePlay()
    {
        if (!playbackEnabled) return;
        StartCoroutine(AttemptPlayOnce());
    }

    private IEnumerator RandomLoop()
    {
        while (true)
        {
            float wait = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(wait);

            yield return AttemptPlayOnce();
        }
    }

    private IEnumerator AttemptPlayOnce()
    {
        // new: respect global playback flag
        if (!playbackEnabled)
        {
            if (debugLogs) Debug.Log("[RandomAudio] Playback disabled - skipping attempt.");
            yield break;
        }

        if (sources.Count == 0)
        {
            CollectSourcesIfNeeded();
            if (sources.Count == 0)
            {
                if (debugLogs) Debug.Log("[RandomAudio] No AudioSources available.");
                yield break;
            }
        }

        if (Random.value > playChance)
        {
            if (debugLogs) Debug.Log("[RandomAudio] Play chance failed this opportunity.");
            yield break;
        }

        if (!allowOverlap && concurrentPlays >= maxConcurrentPlays)
        {
            if (debugLogs) Debug.Log("[RandomAudio] Max concurrent plays reached, skipping.");
            yield break;
        }

        // pick a random source that has a clip or can play
        AudioSource chosen = null;
        for (int attempts = 0; attempts < sources.Count; attempts++)
        {
            var cand = sources[Random.Range(0, sources.Count)];
            if (cand != null && cand.gameObject.activeInHierarchy && cand.enabled)
            {
                chosen = cand;
                break;
            }
        }

        if (chosen == null)
        {
            if (debugLogs) Debug.Log("[RandomAudio] No valid AudioSource found to play.");
            yield break;
        }

        // determine clip
        AudioClip clipToPlay = null;
        if (audioClips != null && audioClips.Length > 0)
        {
            clipToPlay = audioClips[Random.Range(0, audioClips.Length)];
        }
        else if (chosen.clip != null)
        {
            clipToPlay = chosen.clip;
        }

        if (clipToPlay == null)
        {
            if (debugLogs) Debug.Log("[RandomAudio] No AudioClip found to play on chosen source.");
            yield break;
        }

        // apply random pitch/volume temporarily
        float originalPitch = chosen.pitch;
        float originalVolume = chosen.volume;

        if (randomizePitch)
            chosen.pitch = Random.Range(pitchMin, pitchMax);

        if (randomizeVolume)
            chosen.volume = Random.Range(volumeMin, volumeMax);

        // Play (use PlayOneShot for no clip swap; PlayOneShot supports overlap on same source)
        chosen.PlayOneShot(clipToPlay);

        // track concurrent plays (estimate time using clip.length and pitch)
        concurrentPlays++;

        if (debugLogs) Debug.Log($"[RandomAudio] Playing '{clipToPlay.name}' on '{chosen.name}' (concurrent={concurrentPlays}).");

        float duration = clipToPlay.length / Mathf.Abs(chosen.pitch > 0.001f ? chosen.pitch : 1f);
        // Wait until clip is (likely) finished, then restore settings and decrement counter
        yield return new WaitForSeconds(duration);

        // restore properties
        chosen.pitch = originalPitch;
        chosen.volume = originalVolume;

        concurrentPlays = Mathf.Max(0, concurrentPlays - 1);
    }

    // PUBLIC API -------------------------------------------------

    /// <summary>
    /// Called by LevelProgression when the level actually starts.
    /// Enables random playback and restarts the loop (if configured).
    /// </summary>
    public void OnLevelStart()
    {
        playbackEnabled = true;
        CollectSourcesIfNeeded();
        if (startOnStart)
            StartRandomLoop();
        if (debugLogs) Debug.Log("[RandomAudio] OnLevelStart: playback enabled.");
    }

    /// <summary>
    /// Called by LevelProgression when the level ends or the player dies.
    /// Disables random playback and stops any currently playing sounds from registered sources.
    /// </summary>
    public void ResetAndDisablePlayback()
    {
        playbackEnabled = false;
        StopRandomLoop();

        // stop currently playing sounds on all registered sources
        foreach (var s in sources)
        {
            if (s == null) continue;
            try
            {
                s.Stop();
            }
            catch { }
        }

        concurrentPlays = 0;

        if (debugLogs) Debug.Log("[RandomAudio] ResetAndDisablePlayback: playback disabled and sources stopped.");
    }
}
