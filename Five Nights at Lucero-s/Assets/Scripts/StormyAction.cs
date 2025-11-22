using UnityEngine;

/// <summary>
/// Stormy: opportunistic movement; can step forward/back by one spot.
/// Customize AI per-night and movement timing via the Inspector.
/// </summary>
public class StormyAction : MovementAgentBase
{
    [Header("Stormy specific")]
    [Tooltip("Allow stepping backward when a movement opportunity succeeds.")]
    [SerializeField] private bool allowBackward = true;

    protected override void TryMove()
    {
        if (movementSpots == null || movementSpots.Length <= 1) return;

        bool chooseBackward = allowBackward && Random.value < 0.5f;
        int last = movementSpots.Length - 1;
        int target = currentIndex;

        if (chooseBackward)
        {
            target = (currentIndex == 0) ? Mathf.Min(currentIndex + 1, last) : currentIndex - 1;
        }
        else
        {
            target = (currentIndex == last) ? Mathf.Max(currentIndex - 1, 0) : currentIndex + 1;
        }

        if (target == currentIndex)
        {
            if (debugLogs) Debug.Log($"[{name}] No movement possible from index {currentIndex}.");
            return;
        }

        MoveToIndex(target);
    }
}
