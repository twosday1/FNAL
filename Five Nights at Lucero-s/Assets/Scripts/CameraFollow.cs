using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player; // Assign your player here

    void LateUpdate()
    {
        if (player != null)
        {
            transform.position = player.position;
            transform.rotation = player.rotation;
        }
    }
}