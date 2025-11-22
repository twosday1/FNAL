using UnityEngine;

/// <summary>
/// Leia: opportunity-based movement; when moving she'll step forward or backward by one spot.
/// Backward movement can be toggled via the inspector.
/// </summary>
public class LeiaAction : MovementAgentBase
{
    [Header("Leia specific")]
    [Tooltip("Allow stepping backward (toward lower index) when a movement opportunity succeeds.")]
    [SerializeField] private bool allowBackward = true;

    protected override void TryMove()
    {
        if (movementSpots == null || movementSpots.Length <= 1) return;

        bool chooseBackward = allowBackward && Random.value < 0.5f;
        int last = movementSpots.Length - 1;
        int target = currentIndex;

        if (chooseBackward)
        {
            // if at start, flip to forward so Leia moves if possible
            target = (currentIndex == 0) ? Mathf.Min(currentIndex + 1, last) : currentIndex - 1;
        }
        else
        {
            // if at end, flip to backward
            target = (currentIndex == last) ? Mathf.Max(currentIndex - 1, 0) : currentIndex + 1;
        }

        if (target == currentIndex)
        {
            if (debugLogs) Debug.Log($"[{name}] No movement possible from index {currentIndex}.");
            return;
        }

        // Use MoveToIndex so base handles arrival (kill spot) logic
        MoveToIndex(target);
    }
}
