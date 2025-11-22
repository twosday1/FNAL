using UnityEngine;

public class FlashlightToggle : MonoBehaviour
{
    [Header("Flashlight")]
    public Light flashlight; // Assign your flashlight Light component in the Inspector
    public KeyCode activationKey = KeyCode.W; // Set to your movement/flash key

    [Header("Flash detection (raycast)")]
    [Tooltip("Camera used for aiming (usually the player camera).")]
    public Camera aimCamera;
    [Tooltip("Optional origin for the flash ray (if null uses camera position).")]
    public Transform flashlightOrigin;
    [Tooltip("Distance for the flash raycast.")]
    public float flashRange = 20f;
    [Tooltip("Layers the flash can hit (include Stormy's layer).")]
    public LayerMask hitLayers = ~0;
    [Tooltip("Draw debug ray for this many seconds.")]
    public float debugDrawTime = 0.15f;

    [Header("Hold behavior")]
    [Tooltip("When true, holding the key will repeatedly emit flashes at `holdInterval` seconds.")]
    public bool allowHoldToFlash = false;
    [Tooltip("Interval between flashes while holding (seconds).")]
    public float holdInterval = 0.5f;

    [Header("Debug")]
    public bool debugLogs = false;

    // internal
    private bool wasOnLastFrame = false;
    private float holdTimer = 0f;

    void Reset()
    {
        // Try to assign a main camera by default
        aimCamera = Camera.main;
    }

    void Update()
    {
        if (flashlight == null)
            return;

        // Toggle flashlight visually (original behaviour)
        bool isOn = Input.GetKey(activationKey);
        flashlight.enabled = isOn;

        // Rising edge registers one immediate flash
        if (isOn && !wasOnLastFrame)
        {
            DoFlash();
            holdTimer = 0f;
        }

        // Optionally allow repeated flashes while holding
        if (allowHoldToFlash && isOn)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= holdInterval)
            {
                DoFlash();
                holdTimer = 0f;
            }
        }

        if (!isOn)
        {
            holdTimer = 0f;
        }

        wasOnLastFrame = isOn;
    }

    private void DoFlash()
    {
        // Determine origin and direction
        Vector3 origin = flashlightOrigin != null
            ? flashlightOrigin.position
            : (aimCamera != null ? aimCamera.transform.position : transform.position);

        Vector3 dir = aimCamera != null ? aimCamera.transform.forward : transform.forward;

        if (debugLogs)
            Debug.DrawRay(origin, dir * flashRange, Color.cyan, debugDrawTime);

        if (Physics.Raycast(origin, dir, out RaycastHit hit, flashRange, hitLayers, QueryTriggerInteraction.Collide))
        {
            if (debugLogs) Debug.Log($"Flash hit: {hit.collider.name}");

            // Generic: find any MovementAgentBase-derived component in parents and register flash
            var agent = hit.collider.GetComponentInParent<MovementAgentBase>();
            if (agent != null)
            {
                if (debugLogs) Debug.Log($"Registering flash on {agent.name} ({agent.GetType().Name})");
                agent.RegisterFlash();
            }
            else if (debugLogs)
            {
                Debug.Log("Hit object has no MovementAgentBase-derived component");
            }
        }
        else
        {
            if (debugLogs) Debug.Log("Flash missed");
        }
    }
}