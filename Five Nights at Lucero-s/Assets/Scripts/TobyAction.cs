using UnityEngine;

/// <summary>
/// Toby: random-target movement on opportunity attempts (per-night AI levels supported).
/// Maintains prior behavior of being enabled only from night 3 by default (overrides OnLevelStart).
/// </summary>
public class TobyAction : MovementAgentBase
{
    [Header("Toby specific")]
    [Tooltip("Toby historically becomes active starting at night 3. You can override this in code or with LevelProgression.")]
    [SerializeField] private int activeFromNight = 3;

    protected override void TryMove()
    {
        if (movementSpots == null || movementSpots.Length <= 1) return;

        // pick a random index different from current
        int target = currentIndex;
        if (movementSpots.Length == 2)
        {
            target = 1 - currentIndex; // flip if only two spots
        }
        else
        {
            do
            {
                target = Random.Range(0, movementSpots.Length);
            } while (target == currentIndex);
        }

        // Use MoveToIndex so arrival/kill-spot handling is centralized
        MoveToIndex(target);
    }

    public override void OnLevelStart()
    {
        base.OnLevelStart();
        int level = 1;
        var lp = FindObjectOfType<LevelProgression>();
        if (lp != null) level = LevelProgression.CurrentLevel;

        // Enforce Toby activation starting at activeFromNight unless user wants different behaviour
        movementEnabled = level >= activeFromNight;
        if (debugLogs) Debug.Log($"[{name}] (Toby) OnLevelStart override: level={level}, movementEnabled={movementEnabled}");
    }
}
