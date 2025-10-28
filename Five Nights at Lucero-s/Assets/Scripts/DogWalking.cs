using System.Linq;
using UnityEngine;

/// <summary>
/// Play audio when an enemy arrives at this spot.
/// - Attach to a movement spot GameObject. Add an AudioSource (or let the component add/require one).
/// - Two detection modes:
///   1) Trigger mode (recommended): give this GameObject a Collider with __Is Trigger__ enabled.
///      The enemy must have a Collider and a Rigidbody (kinematic Rigidbody is fine) so OnTriggerEnter fires.
///   2) Proximity polling: disable Use Trigger and set an Enemy LayerMask + Detection Radius.
/// - Configure allowed enemy component type names (exact type name).
/// - Implements OnLevelStart() / ResetAndDisablePlayback() so LevelProgression (or other managers) can enable/disable playback.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class DogWalking : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("AudioSource used to play the spot sound. If null, the local AudioSource will be used.")]
    public AudioSource audioSource;

    [Tooltip("Optional override clip to play. If null, audioSource.clip is played.")]
    public AudioClip overrideClip;

    [Tooltip("Play only once per level (or until ResetAndDisablePlayback is called).")]
    public bool playOncePerLevel = false;

    [Header("Detection")]
    [Tooltip("When true, use the GameObject's trigger collider to detect enemies entering the spot.")]
    public bool useTrigger = true;

    [Tooltip("When not using trigger, detect enemies in this radius using the Enemy LayerMask.")]
    public float detectionRadius = 0.5f;

    [Tooltip("Layer(s) considered enemies when using proximity detection.")]
    public LayerMask enemyLayer = 0;

    [Tooltip("Names of MonoBehaviour component types that identify enemies (exact type name).")]
    public string[] enemyComponentTypeNames = new string[] { "StormyAction", "LillyAction", "LeiaAction", "TobyAction" };

    [Header("Playback control")]
    [Tooltip("Allow this spot's audio to overlap if it is already playing.")]
    public bool allowOverlap = false;

    [Tooltip("Optional delay before playing after detection (seconds).")]
    public float playDelay = 0f;

    [Header("Debug")]
    public bool debugLogs = false;

    // internal
    private bool hasPlayedThisLevel = false;
    private bool playbackEnabled = true;

    void Reset()
    {
        // Attempt to auto-assign an AudioSource on reset in editor
        audioSource = GetComponent<AudioSource>();
    }

    void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            if (debugLogs) Debug.Log("[DogWalking] No AudioSource found - added one at runtime.");
        }

        // quick validation for trigger mode
        if (useTrigger)
        {
            var col = GetComponent<Collider>();
            if (col == null || !col.isTrigger)
            {
                if (debugLogs) Debug.LogWarning($"[DogWalking] '{name}' recommends a Collider with IsTrigger = true for trigger detection.");
            }
        }
    }

    void OnEnable()
    {
        // ensure state consistent when enabled
        hasPlayedThisLevel = false;
    }

    void Update()
    {
        if (!playbackEnabled) return;
        if (useTrigger) return; // trigger mode handles detection

        // proximity polling mode
        if (playOncePerLevel && hasPlayedThisLevel) return;

        if (enemyLayer == 0)
        {
            // nothing to detect
            return;
        }

        // fast overlap check
        var colliders = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer, QueryTriggerInteraction.Collide);
        if (colliders.Length == 0) return;

        // ensure one of the colliders belongs to an allowed enemy type
        foreach (var c in colliders)
        {
            if (c == null || c.gameObject == null) continue;
            if (IsEnemy(c.gameObject))
            {
                if (debugLogs) Debug.Log($"[DogWalking] Proximity detected enemy '{c.gameObject.name}' at spot '{gameObject.name}'.");
                TryPlay();
                break;
            }
            // also try attachedRigidbody root (common when collider is on child)
            var rbRoot = c.attachedRigidbody != null ? c.attachedRigidbody.gameObject : c.transform.root.gameObject;
            if (rbRoot != null && rbRoot != c.gameObject && IsEnemy(rbRoot))
            {
                if (debugLogs) Debug.Log($"[DogWalking] Proximity detected enemy via root '{rbRoot.name}' at spot '{gameObject.name}'.");
                TryPlay();
                break;
            }
        }
    }

    // Trigger entry: more robust detection (checks collider, attachedRigidbody, and root)
    void OnTriggerEnter(Collider other)
    {
        if (!useTrigger) return;
        if (!playbackEnabled) return;
        if (playOncePerLevel && hasPlayedThisLevel) return;

        if (other == null || other.gameObject == null) return;

        // Primary: check the collider's gameObject and its parents
        if (IsEnemy(other.gameObject))
        {
            if (debugLogs) Debug.Log($"[DogWalking] Trigger detected enemy '{other.gameObject.name}' at spot '{gameObject.name}'.");
            TryPlay();
            return;
        }

        // Secondary: if collider has an attached rigidbody, check that object (common when colliders on children)
        if (other.attachedRigidbody != null)
        {
            var rbGo = other.attachedRigidbody.gameObject;
            if (rbGo != null && IsEnemy(rbGo))
            {
                if (debugLogs) Debug.Log($"[DogWalking] Trigger detected enemy via attachedRigidbody '{rbGo.name}' at spot '{gameObject.name}'.");
                TryPlay();
                return;
            }
        }

        // Tertiary: check the root of the transform hierarchy
        var rootGo = other.transform.root != null ? other.transform.root.gameObject : null;
        if (rootGo != null && rootGo != other.gameObject && IsEnemy(rootGo))
        {
            if (debugLogs) Debug.Log($"[DogWalking] Trigger detected enemy via root '{rootGo.name}' at spot '{gameObject.name}'.");
            TryPlay();
            return;
        }

        if (debugLogs) Debug.Log($"[DogWalking] Trigger entered by '{other.gameObject.name}' but no enemy component matched on spot '{gameObject.name}'.");
    }

    bool IsEnemy(GameObject go)
    {
        if (go == null) return false;

        // check by component type name on the object
        var mb = go.GetComponents<MonoBehaviour>();
        foreach (var m in mb)
        {
            if (m == null) continue;
            var typeName = m.GetType().Name;
            if (enemyComponentTypeNames.Contains(typeName))
                return true;
        }

        // check parents (in case component is on parent)
        var parent = go.transform.parent;
        while (parent != null)
        {
            var pMb = parent.GetComponents<MonoBehaviour>();
            foreach (var m in pMb)
            {
                if (m == null) continue;
                var typeName = m.GetType().Name;
                if (enemyComponentTypeNames.Contains(typeName))
                    return true;
            }
            parent = parent.parent;
        }

        return false;
    }

    private void TryPlay()
    {
        if (!playbackEnabled) return;
        if (playOncePerLevel && hasPlayedThisLevel) return;

        if (!allowOverlap && audioSource.isPlaying)
        {
            if (debugLogs) Debug.Log("[DogWalking] audio is already playing and overlap disabled - skipping.");
            return;
        }

        if (playDelay <= 0f)
        {
            PlayNow();
        }
        else
        {
            StartCoroutine(PlayDelayed(playDelay));
        }
    }

    private System.Collections.IEnumerator PlayDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        // if playback disabled while waiting, abort
        if (!playbackEnabled) yield break;
        PlayNow();
    }

    private void PlayNow()
    {
        AudioClip clip = overrideClip != null ? overrideClip : audioSource.clip;
        if (clip == null)
        {
            if (debugLogs) Debug.LogWarning($"[DogWalking] No AudioClip assigned on '{gameObject.name}'.");
            return;
        }

        // Use PlayOneShot to avoid swapping the AudioSource.clip and to allow overlap on same source
        audioSource.PlayOneShot(clip);
        hasPlayedThisLevel = true;

        if (debugLogs) Debug.Log($"[DogWalking] Playing clip '{clip.name}' on spot '{gameObject.name}'.");
    }

    // PUBLIC API for LevelProgression (or other managers) --------------------------------

    /// <summary>
    /// Called by LevelProgression when the level actually starts. Re-enables playback for this spot.
    /// </summary>
    public void OnLevelStart()
    {
        playbackEnabled = true;
        hasPlayedThisLevel = false;
        if (debugLogs) Debug.Log($"[DogWalking] OnLevelStart called for '{gameObject.name}'.");
    }

    /// <summary>
    /// Called by LevelProgression when the level ends or the player dies.
    /// Disables playback and stops currently playing sound on this AudioSource.
    /// </summary>
    public void ResetAndDisablePlayback()
    {
        playbackEnabled = false;
        hasPlayedThisLevel = false;

        if (audioSource != null && audioSource.isPlaying)
        {
            try { audioSource.Stop(); } catch { }
        }

        // stop any pending delayed play coroutines
        StopAllCoroutines();

        if (debugLogs) Debug.Log($"[DogWalking] ResetAndDisablePlayback called for '{gameObject.name}'.");
    }
}
