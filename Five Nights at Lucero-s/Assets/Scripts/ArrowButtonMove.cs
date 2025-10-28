using UnityEngine;

public class ArrowButtonMove : MonoBehaviour
{
    [Header("Target Settings")]
    public Vector3 targetPosition;      // Set this in the button Inspector
    public Vector3 targetEulerAngles;   // Set this in the button Inspector
    public GameObject playerOrCamera;   // Assign your player/camera here ONCE per button

    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Arrow Options")]
    public GameObject[] arrowButtons;   // Assign all arrow buttons here

    private bool isMoving = false;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 endPosition;
    private Quaternion endRotation;
    private float moveProgress = 0f;

    public void MoveToTarget()
    {
        if (playerOrCamera != null)
        {
            startPosition = playerOrCamera.transform.position;
            startRotation = playerOrCamera.transform.rotation;
            endPosition = targetPosition;
            endRotation = Quaternion.Euler(targetEulerAngles);
            moveProgress = 0f;
            isMoving = true;
        }

        // Show all arrow buttons
        foreach (GameObject arrow in arrowButtons)
        {
            if (arrow != null)
                arrow.SetActive(true);
        }

        // Hide this button
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (isMoving && playerOrCamera != null)
        {
            moveProgress += Time.deltaTime * moveSpeed;
            playerOrCamera.transform.position = Vector3.Lerp(startPosition, endPosition, moveProgress);
            playerOrCamera.transform.rotation = Quaternion.Slerp(startRotation, endRotation, moveProgress);

            if (moveProgress >= 1f)
            {
                playerOrCamera.transform.position = endPosition;
                playerOrCamera.transform.rotation = endRotation;
                isMoving = false;
            }
        }
    }
}               