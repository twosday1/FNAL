using UnityEngine;

/// <summary>
/// Simple camera flash that only controls the visual Light.
/// - Holding the configured key (default W) turns the Light on; releasing turns it off.
/// - Does NOT perform any raycasts or affect enemy state.
/// </summary>
public class CameraFlash : MonoBehaviour
{
    [Header("Flashlight")]
    [Tooltip("The Light component that visually represents the flashlight.")]
    public Light flashlight;

    [Tooltip("Key used to activate the flashlight.")]
    public KeyCode activationKey = KeyCode.W;

    [Header("Debug")]
    public bool debugLogs = false;

    void Reset()
    {
        // Try to auto-assign a Light on this object or its children in the editor.
        if (flashlight == null)
            flashlight = GetComponentInChildren<Light>();
    }

    void Update()
    {
        if (flashlight == null)
            return;

        bool isOn = Input.GetKey(activationKey);
        flashlight.enabled = isOn;

        if (debugLogs)
        {
            // Log only on state changes to avoid spam
            // (simple rising/falling edge detection)
            if (isOn && !wasOnLastFrame)
                Debug.Log("[CameraFlash] Flash ON");
            else if (!isOn && wasOnLastFrame)
                Debug.Log("[CameraFlash] Flash OFF");
        }

        wasOnLastFrame = isOn;
    }

    // internal edge tracking for debug logs
    private bool wasOnLastFrame = false;
}
