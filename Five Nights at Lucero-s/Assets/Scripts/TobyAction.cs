using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Toby teleports instantly to any configured movement spot at configured times.
/// - Active starting at level 3 (OnLevelStart enables him).
/// - Supports assigning a Transform from the movementSpots array as the kill spot (leave null to disable).
/// - When teleporting lands on door or kill spot, the corresponding sequence runs immediately.
/// - Uses physics (adds a Collider + kinematic Rigidbody at runtime if missing) so trigger-mode DogWalking
///   components receive OnTriggerEnter and play audio without a direct API call.
/// </summary>
public class TobyAction : MonoBehaviour
{
    [Header("Movement spots (assign in Inspector)")]
    [Tooltip("Assign at least 4 spots: Start, LivingRoomA, LivingRoomB, Door (in order).")]
    [SerializeField] private Transform[] movementSpots = new Transform[4];

    [Header("Teleport timing")]
    [SerializeField] private bool useFixedTeleportTime = true;
    [SerializeField] private float fixedTeleportTime = 30f;
    [SerializeField] private bool repeatTeleport = false;
    [SerializeField] private float teleportInterval = 30f;

    [Header("Per-level scaling (optional)")]
    [SerializeField] private float perLevelIntervalDelta = 0f;
    [SerializeField] private float minTeleportInterval = 0.5f;

    [Header("Door / kill settings")]
    [SerializeField] private float doorKillDelay = 6f;
    [SerializeField] private int requiredFlashesToReset = 2;

    [Header("Kill spot (optional)")]
    [Tooltip("Assign a Transform from the movementSpots array to act as the kill spot. Leave empty to disable.")]
    [SerializeField] private Transform killSpot = null;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    [Header("Debug / Testing helpers")]
    [SerializeField] private bool allowFlashWhenNotAtDoor = false;
    [SerializeField] private bool acceptFlashAnywhere = false;

    // runtime state
    private int currentIndex = 0;
    private int startIndex = 0;
    private int doorIndex = 3;
    private int flashCount = 0;
    private Coroutine mainRoutine;

    // movement timing runtime values
    private float currentTeleportInterval;
    private float scheduledFirstTeleportTime;
    private bool hasPerformedFirstTeleportThisLevel = false;

    // Controls whether Toby is allowed to teleport this level.
    private bool movementEnabled = false;

    void Start()
    {
        ValidateAndInit();

        // ensure Toby has a Collider + Rigidbody so trigger-based DogWalking receives OnTriggerEnter when we teleport
        EnsurePhysicsForTrigger();

        // initialize timing
        currentTeleportInterval = Mathf.Max(teleportInterval, minTeleportInterval);
        scheduledFirstTeleportTime = Mathf.Max(0f, fixedTeleportTime);

        mainRoutine = StartCoroutine(MainBehaviorLoop());
    }

    // Ensure Toby will generate trigger events when moved into a trigger collider
    private void EnsurePhysicsForTrigger()
    {
        // If there's no collider, add a small SphereCollider (non-trigger)
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = 0.5f;
            sc.isTrigger = false; // needs to be non-trigger to enter spot's trigger
            if (debugLogs) Debug.Log("[TobyAction] Added SphereCollider for trigger detection.");
        }

        // Add Rigidbody if missing so physics trigger callbacks fire; keep kinematic to avoid physics motion
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            if (debugLogs) Debug.Log("[TobyAction] Added kinematic Rigidbody for trigger detection.");
        }
        else if (!rb.isKinematic)
        {
            // make it kinematic to avoid unintended physics movement
            rb.isKinematic = true;
            if (debugLogs) Debug.Log("[TobyAction] Set existing Rigidbody to kinematic for teleport safety.");
        }
    }

    // Called by LevelProgression on level start. Toby becomes active when level >= 3.
    public void OnLevelStart()
    {
        int level = 1;
        var lp = FindObjectOfType<LevelProgression>();
        if (lp != null) level = LevelProgression.CurrentLevel;

        int extraLevels = Mathf.Max(0, level - 1);
        currentTeleportInterval = Mathf.Max(minTeleportInterval, teleportInterval + extraLevels * perLevelIntervalDelta);

        scheduledFirstTeleportTime = Mathf.Max(0f, fixedTeleportTime);
        hasPerformedFirstTeleportThisLevel = false;

        movementEnabled = level >= 3;
        if (debugLogs) Debug.Log($"[TobyAction] OnLevelStart level={level} movementEnabled={movementEnabled}");
    }

    public void ResetAndDisableMovement()
    {
        if (debugLogs) Debug.Log("[TobyAction] ResetAndDisableMovement: resetting and disabling movement");
        StopAllCoroutines();

        ValidateAndInit();
        movementEnabled = false;
        hasPerformedFirstTeleportThisLevel = false;
        currentTeleportInterval = Mathf.Max(teleportInterval, minTeleportInterval);

        mainRoutine = StartCoroutine(MainBehaviorLoop());
    }

    private void ApplyLevelScaling()
    {
        int level = 1;
        var lp = FindObjectOfType<LevelProgression>();
        if (lp != null) level = LevelProgression.CurrentLevel;

        int extraLevels = Mathf.Max(0, level - 1);
        currentTeleportInterval = Mathf.Max(minTeleportInterval, teleportInterval + extraLevels * perLevelIntervalDelta);
        if (debugLogs) Debug.Log($"[TobyAction] ApplyLevelScaling: level={level}, movementInterval={currentTeleportInterval:F2}");
    }

    private void ValidateAndInit()
    {
        if (movementSpots == null || movementSpots.Length < 1)
        {
            Debug.LogError("[TobyAction] Please assign at least 1 movement spot in the Inspector.");
            enabled = false;
            return;
        }

        startIndex = 0;
        doorIndex = Mathf.Clamp(movementSpots.Length - 1, 0, movementSpots.Length - 1);

        // validate killSpot is one of the movement spots (if assigned)
        if (killSpot != null)
        {
            bool found = false;
            for (int i = 0; i < movementSpots.Length; i++)
                if (movementSpots[i] == killSpot) { found = true; break; }
            if (!found)
            {
                if (debugLogs) Debug.LogWarning("[TobyAction] killSpot is not contained in movementSpots; clearing killSpot.");
                killSpot = null;
            }
        }

        currentIndex = startIndex;
        transform.position = movementSpots[currentIndex].position;
        flashCount = 0;
    }

    private IEnumerator MainBehaviorLoop()
    {
        while (true)
        {
            while (!movementEnabled) yield return null;

            hasPerformedFirstTeleportThisLevel = false;
            float levelTimer = 0f;

            while (movementEnabled)
            {
                if (currentIndex == doorIndex)
                {
                    if (debugLogs) Debug.Log("[TobyAction] At door: starting door sequence.");
                    yield return StartCoroutine(DoorSequence());
                    levelTimer = 0f;
                    continue;
                }

                if (IsAtKillSpot())
                {
                    if (debugLogs) Debug.Log("[TobyAction] At kill spot: starting kill-spot sequence.");
                    yield return StartCoroutine(KillSpotSequence());
                    levelTimer = 0f;
                    continue;
                }

                if (useFixedTeleportTime)
                {
                    if (!hasPerformedFirstTeleportThisLevel)
                    {
                        float remaining = scheduledFirstTeleportTime - levelTimer;
                        if (remaining > 0f)
                        {
                            yield return new WaitForSeconds(remaining);
                            levelTimer += remaining;
                        }

                        if (!movementEnabled) break;

                        PerformTeleport();
                        hasPerformedFirstTeleportThisLevel = true;

                        if (!repeatTeleport)
                        {
                            while (movementEnabled) yield return null;
                            break;
                        }
                    }
                    else
                    {
                        yield return new WaitForSeconds(currentTeleportInterval);
                        levelTimer += currentTeleportInterval;
                        if (!movementEnabled) break;
                        PerformTeleport();
                    }
                }
                else
                {
                    yield return new WaitForSeconds(currentTeleportInterval);
                    levelTimer += currentTeleportInterval;
                    if (!movementEnabled) break;
                    PerformTeleport();
                }

                yield return null;
            }

            yield return null;
        }
    }

    private void PerformTeleport()
    {
        int target = PickRandomIndexExcluding(currentIndex);
        if (debugLogs) Debug.Log($"[TobyAction] Teleporting from {currentIndex} -> {target}");
        TeleportToIndex(target);
    }

    private void TeleportToIndex(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= movementSpots.Length) return;

        transform.position = movementSpots[targetIndex].position;
        currentIndex = targetIndex;

        // Do NOT call any DogWalking API here. Rely on physics trigger: spot must have an IsTrigger collider.
        // Because Toby has a (kinematic) Rigidbody + Collider, moving him into the spot's trigger will invoke DogWalking.OnTriggerEnter.
        if (debugLogs) Debug.Log($"[TobyAction] Teleported to '{movementSpots[targetIndex].name}' (index {targetIndex}).");
    }

    private int PickRandomIndexExcluding(int exclude)
    {
        int len = movementSpots != null ? movementSpots.Length : 0;
        if (len <= 1) return 0;

        var candidates = new List<int>(len - 1);
        for (int i = 0; i < len; i++)
            if (i != exclude) candidates.Add(i);

        return candidates[Random.Range(0, candidates.Count)];
    }

    private IEnumerator DoorSequence()
    {
        flashCount = 0;
        float elapsed = 0f;

        if (debugLogs) Debug.Log("[TobyAction] DoorSequence: waiting for flashes");

        while (elapsed < doorKillDelay)
        {
            if (flashCount >= requiredFlashesToReset)
            {
                if (debugLogs) Debug.Log("[TobyAction] DoorSequence: flashed enough -> resetting to start");
                ResetToStart();
                yield break;
            }

            if (!movementEnabled)
            {
                if (debugLogs) Debug.Log("[TobyAction] DoorSequence: exiting because movement disabled");
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (flashCount >= requiredFlashesToReset)
        {
            if (debugLogs) Debug.Log("[TobyAction] DoorSequence: flashed enough before timeout -> resetting to start");
            ResetToStart();
            yield break;
        }

        if (debugLogs) Debug.Log("[TobyAction] DoorSequence: not enough flashes -> killing player");
        KillPlayer();
    }

    private IEnumerator KillSpotSequence()
    {
        flashCount = 0;
        float elapsed = 0f;

        if (debugLogs) Debug.Log("[TobyAction] KillSpotSequence: waiting for flashes");

        while (elapsed < doorKillDelay)
        {
            if (flashCount >= requiredFlashesToReset)
            {
                if (debugLogs) Debug.Log("[TobyAction] KillSpotSequence: flashed enough -> resetting to start");
                ResetToStart();
                yield break;
            }

            if (!movementEnabled)
            {
                if (debugLogs) Debug.Log("[TobyAction] KillSpotSequence: exiting because movement disabled");
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (flashCount >= requiredFlashesToReset)
        {
            if (debugLogs) Debug.Log("[TobyAction] KillSpotSequence: flashed enough before timeout -> resetting to start");
            ResetToStart();
            yield break;
        }

        if (debugLogs) Debug.Log("[TobyAction] KillSpotSequence: not enough flashes -> killing player");
        KillPlayer();
    }

    // Public method the player's flashlight code should call when it hits Toby
    public void RegisterFlash()
    {
        if (debugLogs) Debug.Log($"[TobyAction] RegisterFlash called (currentIndex={currentIndex}, doorIndex={doorIndex}, killSpot={(killSpot!=null?killSpot.name:"null")}, flashCount={flashCount})");

        // Accept flashes anywhere if configured, otherwise only at door/kill spot (or allowFlashWhenNotAtDoor for testing)
        if (!acceptFlashAnywhere && !allowFlashWhenNotAtDoor)
        {
            if (currentIndex != doorIndex && !IsAtKillSpot())
            {
                if (debugLogs) Debug.Log($"[TobyAction] RegisterFlash ignored: Toby not at door or kill spot (index {currentIndex}).");
                return;
            }
        }

        flashCount++;
        if (debugLogs) Debug.Log($"[TobyAction] RegisterFlash: flashCount = {flashCount}");

        // If accepting flashes anywhere, apply reset immediately when threshold reached
        if ((acceptFlashAnywhere || allowFlashWhenNotAtDoor) && flashCount >= requiredFlashesToReset)
        {
            if (debugLogs) Debug.Log("[TobyAction] RegisterFlash: threshold reached outside sequence -> ResetToStart()");
            ResetToStart();
        }
    }

    [ContextMenu("Force Flash (bypass door/kill check)")]
    public void ForceFlash()
    {
        if (debugLogs) Debug.Log("[TobyAction] ForceFlash invoked from inspector (bypasses door/kill check).");
        flashCount++;
    }

    private void ResetToStart()
    {
        StopAllCoroutines();
        currentIndex = startIndex;
        transform.position = movementSpots[currentIndex].position;
        flashCount = 0;

        ApplyLevelScaling();
        scheduledFirstTeleportTime = Mathf.Max(0f, fixedTeleportTime);
        hasPerformedFirstTeleportThisLevel = false;
        movementEnabled = true;

        if (debugLogs) Debug.Log("[TobyAction] ResetToStart: Toby returned to start and movement re-enabled");

        // restart main loop
        mainRoutine = StartCoroutine(MainBehaviorLoop());
    }

    private void KillPlayer()
    {
        if (debugLogs) Debug.Log("[TobyAction] KillPlayer: notifying LevelProgression or reloading scene");

        var levelProg = FindObjectOfType<LevelProgression>();
        if (levelProg != null) levelProg.OnPlayerDeath();
        else
        {
            if (debugLogs) Debug.Log("[TobyAction] KillPlayer: LevelProgression not found, reloading active scene");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // Optional helper: allow forcing Toby to a specific spot index at runtime
    public void ForceMoveToSpot(int index)
    {
        if (index < 0 || index >= movementSpots.Length) return;
        StopAllCoroutines();
        currentIndex = index;
        transform.position = movementSpots[index].position;
        flashCount = 0;

        movementEnabled = true;
        mainRoutine = StartCoroutine(MainBehaviorLoop());
    }

    // Helper to check whether Toby is currently at the configured kill spot
    private bool IsAtKillSpot()
    {
        if (killSpot == null) return false;
        if (movementSpots == null) return false;
        if (currentIndex < 0 || currentIndex >= movementSpots.Length) return false;
        return movementSpots[currentIndex] == killSpot;
    }

    // Debug helpers
    public int GetCurrentIndex() => currentIndex;
    public int GetDoorIndex() => doorIndex;
    public Transform GetKillSpotTransform() => killSpot;
    public int GetKillSpotIndex()
    {
        if (killSpot == null || movementSpots == null) return -1;
        for (int i = 0; i < movementSpots.Length; i++)
            if (movementSpots[i] == killSpot) return i;
        return -1;
    }
}
