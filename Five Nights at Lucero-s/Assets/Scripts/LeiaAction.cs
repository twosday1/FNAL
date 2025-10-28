using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LeiaAction : MonoBehaviour
{
    [Header("Movement spots (assign in Inspector)")]
    [Tooltip("Assign at least 4 spots: Start, LivingRoomA, LivingRoomB, Door (in order).")]
    [SerializeField] private Transform[] movementSpots = new Transform[4];

    [Header("Movement timing & chances")]
    [Tooltip("Base seconds between movement opportunities (adjustable). Timer resets after a move.")]
    [SerializeField] private float movementInterval = 10f;
    [Tooltip("Base chance to move out of the start spot (0..1). Default ~1/6).")]
    [SerializeField, Range(0f, 1f)] private float startMoveChance = 1f / 6f;
    [Tooltip("Base chance to move forward from a middle spot (0..1). Default ~1/6).")]
    [SerializeField, Range(0f, 1f)] private float forwardMoveChance = 1f / 6f;
    [Tooltip("Base chance to move backward from a middle spot (0..1). Default ~1/6).")]
    [SerializeField, Range(0f, 1f)] private float backwardMoveChance = 1f / 6f;

    [Header("Per-level scaling")]
    [Tooltip("Add this amount to each move chance per level after level 1.")]
    [SerializeField, Range(0f, 1f)] private float perLevelChanceIncrease = 0.02f;
    [Tooltip("Maximum allowed move chance (clamped 0..1).")]
    [SerializeField, Range(0f, 1f)] private float maxMoveChance = 0.95f;

    [Header("Movement interval scaling (per level)")]
    [Tooltip("Multiplier applied to movement interval for each extra level (0..1). Values <1 make Leia faster per level.")]
    [SerializeField, Range(0.1f, 1f)] private float perLevelIntervalMultiplier = 0.9f;
    [Tooltip("Minimum allowed movement interval after scaling.")]
    [SerializeField] private float minMovementInterval = 1f;

    [Header("Movement smoothing")]
    [Tooltip("Seconds it takes Leia to move between spots.")]
    [SerializeField] private float moveDuration = 0.5f;

    [Header("Door / kill settings")]
    [Tooltip("Seconds Leia waits at the door before killing the player (unless flashed).")]
    [SerializeField] private float doorKillDelay = 6f;
    [Tooltip("Number of flashlight flashes required to reset Leia when at the door.")]
    [SerializeField] private int requiredFlashesToReset = 2;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    [Header("Debug / Testing helpers")]
    [Tooltip("Allow RegisterFlash() to succeed even when Leia is not exactly at the door (useful for testing).")]
    [SerializeField] private bool allowFlashWhenNotAtDoor = false;

    // Runtime state
    private int currentIndex = 0;
    private int startIndex = 0;
    private int doorIndex = 3;
    private bool moving = false;
    private int flashCount = 0;
    private Coroutine mainRoutine;

    // stored base values so we can re-calc scaled chances
    private float baseStartMoveChance;
    private float baseForwardMoveChance;
    private float baseBackwardMoveChance;

    // Controls whether Leia is allowed to perform movement attempts.
    // False at scene start; LevelProgression must call OnLevelStart() to enable.
    private bool movementEnabled = false;

    // runtime scaled interval
    private float currentMovementInterval;

    void Start()
    {
        ValidateAndInit();

        // capture base values (in case designer tweaked them in inspector)
        baseStartMoveChance = startMoveChance;
        baseForwardMoveChance = forwardMoveChance;
        baseBackwardMoveChance = backwardMoveChance;

        // use base interval until level start; ApplyLevelScaling will set currentMovementInterval on level start
        currentMovementInterval = movementInterval;

        // Start the main loop; it will wait until movementEnabled == true
        mainRoutine = StartCoroutine(MainBehaviorLoop());
    }

    // Called by LevelProgression when the level actually starts (after "Night X" fade)
    public void OnLevelStart()
    {
        ApplyLevelScaling();
        movementEnabled = true;
        if (debugLogs) Debug.Log($"[LeiaAction] OnLevelStart: movement enabled (interval={currentMovementInterval:F2}s)");
    }

    // Called by LevelProgression when level is passed or player dies — reset Leia and prevent movement until next level start
    public void ResetAndDisableMovement()
    {
        if (debugLogs) Debug.Log("[LeiaAction] ResetAndDisableMovement: resetting and disabling movement");
        // Stop any current coroutines that would be moving Leia
        StopAllCoroutines();

        // Reset state
        ValidateAndInit(); // places Leia at start and resets flashCount
        moving = false;

        // Ensure main loop is running and waiting for movementEnabled
        movementEnabled = false;
        currentMovementInterval = movementInterval;
        mainRoutine = StartCoroutine(MainBehaviorLoop());
    }

    private void ApplyLevelScaling()
    {
        int level = 1;
        // safe guard if LevelProgression is missing
        var lp = FindObjectOfType<LevelProgression>();
        if (lp != null)
            level = LevelProgression.CurrentLevel;

        // increase per level after level 1
        int extraLevels = Mathf.Max(0, level - 1);

        startMoveChance = Mathf.Clamp(baseStartMoveChance + extraLevels * perLevelChanceIncrease, 0f, maxMoveChance);
        forwardMoveChance = Mathf.Clamp(baseForwardMoveChance + extraLevels * perLevelChanceIncrease, 0f, maxMoveChance);
        backwardMoveChance = Mathf.Clamp(baseBackwardMoveChance + extraLevels * perLevelChanceIncrease, 0f, maxMoveChance);

        // scale movement interval: multiply by multiplier^extraLevels, clamp to minimum
        currentMovementInterval = movementInterval * Mathf.Pow(perLevelIntervalMultiplier, extraLevels);
        currentMovementInterval = Mathf.Max(currentMovementInterval, minMovementInterval);

        if (debugLogs)
        {
            Debug.Log($"[LeiaAction] ApplyLevelScaling: level={level}, startChance={startMoveChance:F3}, forwardChance={forwardMoveChance:F3}, backward={backwardMoveChance:F3}, movementInterval={currentMovementInterval:F2}");
        }
    }

    private void ValidateAndInit()
    {
        if (movementSpots == null || movementSpots.Length < 4)
        {
            Debug.LogError("[LeiaAction] Please assign at least 4 movement spots in the Inspector (Start, Living1, Living2, Door).");
            enabled = false;
            return;
        }

        startIndex = 0;
        doorIndex = movementSpots.Length - 1;

        // Place Leia at the configured start immediately
        currentIndex = startIndex;
        transform.position = movementSpots[currentIndex].position;
        flashCount = 0;
    }

    private IEnumerator MainBehaviorLoop()
    {
        // Main loop that handles movement opportunities and transitions to door behavior
        while (true)
        {
            // Wait until level has started and movement is enabled
            while (!movementEnabled)
                yield return null;

            // If at door, handle door behavior separately
            if (currentIndex == doorIndex)
            {
                if (debugLogs) Debug.Log("[LeiaAction] Reached door. Starting door sequence.");
                yield return StartCoroutine(DoorSequence());
                // After door sequence ends (reset or kill), if Leia was reset to start, continue loop.
                continue;
            }

            // Wait for movement opportunity
            float waited = 0f;
            while (waited < currentMovementInterval)
            {
                // If moved by some external factor, break
                if (moving) break;
                // If level got disabled mid-wait, break to outer wait for movementEnabled
                if (!movementEnabled) break;
                waited += Time.deltaTime;
                yield return null;
            }

            // If movement disabled during the wait, go back to top to wait until enabled
            if (!movementEnabled) continue;

            // If currently moving, wait until finished
            while (moving)
                yield return null;

            // If at door due to external change, loop will handle next iteration
            if (currentIndex == doorIndex)
                continue;

            // Attempt move based on which spot we are at
            if (currentIndex == startIndex)
            {
                // Only can progress forward out of start
                if (Random.value < startMoveChance)
                {
                    int target = Mathf.Min(currentIndex + 1, doorIndex);

                    // Prevent moving to the door if Lilly is already there
                    if (target == doorIndex)
                    {
                        var lilly = FindObjectOfType<LillyAction>();
                        if (lilly != null && lilly.GetCurrentIndex() == lilly.GetDoorIndex())
                        {
                          if (debugLogs) Debug.Log("[LeiaAction] Cannot move to door: Lilly is already at the door.");
                          // Do not move to the door
                          continue;
                        }
                    }

                    if (debugLogs) Debug.Log($"[LeiaAction] Start -> moving to index {target}");
                    yield return StartCoroutine(MoveToIndex(target));
                }
                else
                {
                    if (debugLogs) Debug.Log("[LeiaAction] Start -> did not get the move roll");
                }
            }
            else
            {
                // Middle spots: independently try forward or backward (each with its own chance).
                bool moved = false;

                // Try forward first
                if (currentIndex < doorIndex && Random.value < forwardMoveChance)
                {
                    int target = Mathf.Min(currentIndex + 1, doorIndex);
                    if (debugLogs) Debug.Log($"[LeiaAction] Middle -> forward to {target}");
                    yield return StartCoroutine(MoveToIndex(target));
                    moved = true;
                }

                // If not moved yet, try backward
                if (!moved && currentIndex > startIndex && Random.value < backwardMoveChance)
                {
                    int target = Mathf.Max(currentIndex - 1, startIndex);
                    if (debugLogs) Debug.Log($"[LeiaAction] Middle -> backward to {target}");
                    yield return StartCoroutine(MoveToIndex(target));
                    moved = true;
                }

                if (!moved && debugLogs) Debug.Log("[LeiaAction] Middle -> no move this opportunity");
            }
            // After any move (or not) loop continues; movementInterval restarts by waiting again
        }
    }

    private IEnumerator MoveToIndex(int targetIndex)
    {
        if (moving) yield break;
        if (targetIndex < 0 || targetIndex >= movementSpots.Length) yield break;

        // If Leia is at the second spot and moving to the door, teleport instantly
        if (currentIndex == 2 && targetIndex == 3)
        {
            moving = true;
            transform.position = movementSpots[targetIndex].position;
            currentIndex = targetIndex;
            moving = false;
            yield break;
        }

        moving = true;
        Vector3 startPos = transform.position;
        Vector3 endPos = movementSpots[targetIndex].position;
        float elapsed = 0f;

        if (moveDuration <= 0f)
        {
            transform.position = endPos;
            currentIndex = targetIndex;
            moving = false;
            yield break;
        }

        while (elapsed < moveDuration)
        {
            // If movement gets disabled mid-move, cancel movement and yield back to waiting loop
            if (!movementEnabled)
            {
                if (debugLogs) Debug.Log("[LeiaAction] MoveToIndex cancelled because movementDisabled");
                moving = false;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        transform.position = endPos;
        currentIndex = targetIndex;
        moving = false;
    }

    private IEnumerator DoorSequence()
    {
        // Reset flash counter at entry
        flashCount = 0;
        float elapsed = 0f;

        if (debugLogs) Debug.Log("[LeiaAction] DoorSequence: waiting for flashes");

        while (elapsed < doorKillDelay)
        {
            // If flashes reach required count at any time, reset to start
            if (flashCount >= requiredFlashesToReset)
            {
            if (debugLogs) Debug.Log("[LeiaAction] DoorSequence: flashed enough -> resetting to start");
            ResetToStart();
            yield break;
            }

            // If movement has been disabled (level end or death), exit door sequence
            if (!movementEnabled)
            {
                if (debugLogs) Debug.Log("[LeiaAction] DoorSequence: exiting because movement disabled");
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Time ran out; check final flash count
        if (flashCount >= requiredFlashesToReset)
        {
            if (debugLogs) Debug.Log("[LeiaAction] DoorSequence: flashed enough before timeout -> resetting to start");
            ResetToStart();
            yield break;
        }

        if (debugLogs) Debug.Log("[LeiaAction] DoorSequence: not enough flashes -> killing player");
        KillPlayer();
        yield break;
    }

    // Public method the player's flashlight code should call when it hits Leia
    public void RegisterFlash()
{
    if (debugLogs)
        Debug.Log($"[LeiaAction] RegisterFlash called (currentIndex={currentIndex}, doorIndex={doorIndex}, flashCount={flashCount})");

    if (currentIndex != doorIndex && !allowFlashWhenNotAtDoor)
    {
        if (debugLogs) Debug.Log($"[LeiaAction] RegisterFlash ignored: Leia not at door (index {currentIndex}).");
        return; // Only count flashes at the door unless testing override is enabled
    }

    flashCount++;
    if (debugLogs) Debug.Log($"[LeiaAction] RegisterFlash: flashCount = {flashCount}");
    // door coroutine checks flashCount each frame and will ResetToStart() when threshold reached
}

    // Expose a quick test entry you can call from the Inspector context menu (bypasses door check)
    [ContextMenu("Force Flash (bypass door check)")]
    public void ForceFlash()
    {
        if (debugLogs) Debug.Log("[LeiaAction] ForceFlash invoked from inspector (bypasses door check).");
        flashCount++;
    }

    private void ResetToStart()
    {
        // Stop all behavior and reset state
        StopAllCoroutines();
        currentIndex = startIndex;
        transform.position = movementSpots[currentIndex].position;
        flashCount = 0;
        moving = false;
        if (debugLogs) Debug.Log("[LeiaAction] ResetToStart: Leia returned to start");
        // Restart main loop (it will wait for movementEnabled if disabled)
        mainRoutine = StartCoroutine(MainBehaviorLoop());
    }

    private void KillPlayer()
    {
        // Prefer notifying LevelProgression to run the death/fade UI flow.
        // Fall back to reloading the scene directly.
        if (debugLogs) Debug.Log("[LeiaAction] KillPlayer: notifying LevelProgression or reloading active scene");

        var levelProg = FindObjectOfType<LevelProgression>();
        if (levelProg != null)
        {
            levelProg.OnPlayerDeath();
        }
        else
        {
            if (debugLogs) Debug.Log("[LeiaAction] KillPlayer: LevelProgression not found, reloading active scene");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // Optional helper: allow forcing Leia to a specific spot index at runtime
    public void ForceMoveToSpot(int index)
    {
        if (index < 0 || index >= movementSpots.Length) return;
        StopAllCoroutines();
        currentIndex = index;
        transform.position = movementSpots[index].position;
        flashCount = 0;
        moving = false;
        // restart loop; movementEnabled remains whatever it was
        mainRoutine = StartCoroutine(MainBehaviorLoop());
    }

    // Debug helpers
    public int GetCurrentIndex() => currentIndex;
    public int GetDoorIndex() => doorIndex;
    public int GetFlashCount() => flashCount;

    private void OnDrawGizmos()
    {
        if (movementSpots == null) return;
        for (int i = 0; i < movementSpots.Length; i++)
        {
            if (movementSpots[i] == null) continue;
            Gizmos.color = (i == startIndex) ? Color.green : (i == doorIndex ? Color.red : Color.yellow);
            Gizmos.DrawSphere(movementSpots[i].position, 0.1f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(movementSpots[i].position + Vector3.up * 0.15f, $"[{i}]");
#endif
        }
    }
}
