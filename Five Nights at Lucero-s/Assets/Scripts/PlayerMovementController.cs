using UnityEngine;

public class PlayerMovementController : MonoBehaviour
{
    [Header("Target Positions & Rotations")]
    public Vector3[] targetPositions;
    public Vector3[] targetEulerAngles;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    private bool isMoving = false;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 endPosition;
    private Quaternion endRotation;
    private float moveProgress = 0f;

    public void MoveToTarget(int index)
    {
        Debug.Log($"MoveToTarget called with index: {index}");
        Debug.Log($"targetPositions.Length: {targetPositions?.Length}, targetEulerAngles.Length: {targetEulerAngles?.Length}");
        if (targetPositions != null && targetEulerAngles != null &&
            index >= 0 && index < targetPositions.Length && index < targetEulerAngles.Length)
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
            endPosition = targetPositions[index];
            endRotation = Quaternion.Euler(targetEulerAngles[index]);
            moveProgress = 0f;
            isMoving = true;
        }
        else
        {
            Debug.LogWarning("Invalid index or arrays not set up correctly.");
        }
    }

    private void Update()
    {
        if (isMoving)
        {
            moveProgress += Time.deltaTime * moveSpeed;
            transform.position = Vector3.Lerp(startPosition, endPosition, moveProgress);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, moveProgress);

            if (moveProgress >= 1f)
            {
                transform.position = endPosition;
                transform.rotation = endRotation;
                isMoving = false;
            }
        }
    }
}