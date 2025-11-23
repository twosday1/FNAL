using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [Tooltip("Draw debug ray for this many seconds).")]
    public float debugDrawTime = 0.15f;

    [Header("Hold behavior")]
    [Tooltip("When true, holding the key will repeatedly emit flashes at `holdInterval` seconds.")]
    public bool allowHoldToFlash = false;
    [Tooltip("Interval between flashes while holding (seconds).")]
    public float holdInterval = 0.5f;

    [Header("Debug")]
    public bool debugLogs = false;

    [Header("Battery")]
    [Tooltip("How long (seconds) the flashlight lasts from full to empty.")]
    public float batteryDuration = 10f;
    [Tooltip("On-screen timer (TextMeshPro) showing remaining battery / recharge progress.")]
    public TextMeshProUGUI batteryTimerText;

    [Header("Recharge")]
    [Tooltip("Seconds the recharge process takes after pressing the recharge button in cameras.")]
    public float rechargeDuration = 5f;
    [Tooltip("UI Button (assign the camera UI button). CameraSwitcher also toggles this GameObject active when in cameras.")]
    public Button rechargeButton;
    [Tooltip("Reference to the CameraSwitcher to verify camera view state.")]
    public CameraSwitcher cameraSwitcher;

    [Header("Audio (Activation)")]
    [Tooltip("AudioSource used to play the activation/deactivation sound. If null the script will try to find one on the object/children/parents.")]
    public AudioSource activationAudioSource;
    [Tooltip("Optional override clip to play when pressing/releasing the flashlight key/button. If null, activationAudioSource.clip is used.")]
    public AudioClip activationClip;
    [Range(0f, 1f)]
    public float activationVolume = 1f;
    [Tooltip("Randomize pitch for activation sound")]
    public bool randomizeActivationPitch = false;
    public float pitchMin = 0.95f;
    public float pitchMax = 1.05f;

    [Header("UI Text Templates")]
    [Tooltip("Format for battery remaining text. Use {time} placeholder.")]
    public string batteryTextFormat = "Battery: {time}";
    [Tooltip("Text when battery is empty.")]
    public string batteryEmptyText = "Battery: EMPTY";
    [Tooltip("Format for recharging text. Use {time} placeholder.")]
    public string rechargingTextFormat = "Recharging: {time}";
    [Tooltip("Label for the recharge button when available.")]
    public string rechargeButtonLabel = "Recharge";
    [Tooltip("Label format for the recharge button while recharging. Use {time} placeholder.")]
    public string rechargeButtonRechargingFormat = "Recharging: {time}";

    [Tooltip("Optional: Text component inside the recharge button (TextMeshPro). If not set the script will try to locate one under the button.")]
    public TextMeshProUGUI rechargeButtonLabelTMP;
    [Tooltip("Optional: Legacy UI Text fallback if you are not using TextMeshPro.")]
    public Text rechargeButtonLabelLegacy;

    // internal
    private bool wasOnLastFrame = false;
    private bool wasRawInputLastFrame = false;
    private float holdTimer = 0f;

    // battery state
    private float batteryRemaining;
    private bool batteryDepleted = false;

    // recharge state
    private bool isRecharging = false;
    private float rechargeTimer = 0f;

    void Reset()
    {
        // Try to assign a main camera by default
        aimCamera = Camera.main;
        batteryRemaining = batteryDuration;
    }

    void Start()
    {
        if (batteryDuration <= 0f) batteryDuration = 1f;
        batteryRemaining = batteryDuration;

        // Ensure battery HUD is visible initially
        if (batteryTimerText != null)
            batteryTimerText.gameObject.SetActive(true);

        // Ensure recharge button starts non-interactable unless camera view (actual interactability updated in Update)
        if (rechargeButton != null) rechargeButton.interactable = false;

        // Auto-find button label components if not assigned
        if (rechargeButton != null)
        {
            if (rechargeButtonLabelTMP == null)
                rechargeButtonLabelTMP = rechargeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (rechargeButtonLabelTMP == null && rechargeButtonLabelLegacy == null)
                rechargeButtonLabelLegacy = rechargeButton.GetComponentInChildren<Text>();

            // initialize label
            UpdateRechargeButtonLabel();
        }

        // Auto-find an AudioSource if none assigned (convenience)
        if (activationAudioSource == null)
            activationAudioSource = GetComponent<AudioSource>() ?? GetComponentInChildren<AudioSource>() ?? GetComponentInParent<AudioSource>();
    }

    void Update()
    {
        if (flashlight == null)
            return;

        // Raw input (key state)
        bool rawInput = Input.GetKey(activationKey);

        // Determine if flashlight should be on this frame
        bool canUseFlashlight = !batteryDepleted && !isRecharging;
        bool isOn = rawInput && canUseFlashlight;

        // Play activation sound on raw rising edge (press)
        if (rawInput && !wasRawInputLastFrame)
        {
            PlayActivationSound();
        }

        // Play sound when flashlight is toggled off (was on last frame, now off)
        if (!isOn && wasOnLastFrame)
        {
            PlayActivationSound();
        }

        // Ensure flashlight cannot be held on when recharging or depleted
        flashlight.enabled = isOn;

        // Rising edge registers one immediate flash (only when flashlight actually turns on)
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

        // Battery drain: only when flashlight is actually on
        if (isOn)
        {
            batteryRemaining -= Time.deltaTime;
            if (batteryRemaining <= 0f)
            {
                batteryRemaining = 0f;
                batteryDepleted = true;
                flashlight.enabled = false;
                if (debugLogs) Debug.Log("[FlashlightToggle] Battery depleted.");
            }
        }

        // Update HUD battery timer text and recharge button state/label
        UpdateBatteryText();

        // Update recharge button state (CameraSwitcher controls GameObject active).
        if (rechargeButton != null)
        {
            bool shouldBeInteractable = !isRecharging && (cameraSwitcher == null || cameraSwitcher.InCameraView);
            rechargeButton.interactable = shouldBeInteractable;
        }

        // reset hold state when not holding
        if (!rawInput)
            holdTimer = 0f;

        // remember previous on/raw states
        wasOnLastFrame = isOn;
        wasRawInputLastFrame = rawInput;
    }

    private void PlayActivationSound()
    {
        // If an AudioSource is assigned prefer PlayOneShot on that source
        if (activationAudioSource != null)
        {
            AudioClip toPlay = activationClip != null ? activationClip : activationAudioSource.clip;
            if (toPlay == null)
            {
                if (debugLogs) Debug.LogWarning("[FlashlightToggle] No activation clip assigned.");
                return;
            }

            float originalPitch = activationAudioSource.pitch;
            if (randomizeActivationPitch)
                activationAudioSource.pitch = Random.Range(pitchMin, pitchMax);

            activationAudioSource.PlayOneShot(toPlay, activationVolume);

            if (randomizeActivationPitch)
                activationAudioSource.pitch = originalPitch;

            if (debugLogs) Debug.Log("[FlashlightToggle] Played activation sound via AudioSource.");
            return;
        }

        // Fallback: if no AudioSource assigned, use PlayClipAtPoint (2D at camera position)
        if (activationClip != null)
        {
            Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(activationClip, pos, activationVolume);
            if (debugLogs) Debug.Log("[FlashlightToggle] Played activation clip via PlayClipAtPoint.");
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[FlashlightToggle] No activation AudioSource or clip assigned.");
        }
    }

    private void UpdateBatteryText()
    {
        // Ensure button label always updated (countdown appears on button while recharging)
        UpdateRechargeButtonLabel();

        if (batteryTimerText == null)
        {
            return;
        }

        // When recharging the countdown should ONLY appear on the button.
        // Hide or show the HUD battery text accordingly.
        if (isRecharging)
        {
            // hide HUD timer while recharging so the countdown is only visible on the button
            if (batteryTimerText.gameObject.activeSelf)
                batteryTimerText.gameObject.SetActive(false);
            return;
        }
        else
        {
            // ensure HUD timer is visible when not recharging
            if (!batteryTimerText.gameObject.activeSelf)
                batteryTimerText.gameObject.SetActive(true);
        }

        if (batteryDepleted)
        {
            batteryTimerText.text = batteryEmptyText;
        }
        else
        {
            batteryTimerText.text = batteryTextFormat.Replace("{time}", FormatTime(batteryRemaining));
        }
    }

    private void UpdateRechargeButtonLabel()
    {
        if (rechargeButton == null) return;

        string label;
        if (isRecharging)
            label = rechargeButtonRechargingFormat.Replace("{time}", FormatTime(rechargeTimer));
        else
            label = rechargeButtonLabel;

        if (rechargeButtonLabelTMP != null)
        {
            rechargeButtonLabelTMP.text = label;
        }
        else if (rechargeButtonLabelLegacy != null)
        {
            rechargeButtonLabelLegacy.text = label;
        }
        else
        {
            // Try to find a label dynamically if not cached
            var tmp = rechargeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) { rechargeButtonLabelTMP = tmp; rechargeButtonLabelTMP.text = label; return; }
            var txt = rechargeButton.GetComponentInChildren<Text>();
            if (txt != null) { rechargeButtonLabelLegacy = txt; rechargeButtonLabelLegacy.text = label; return; }
        }
    }

    private string FormatTime(float t)
    {
        t = Mathf.Max(0f, t);
        int minutes = Mathf.FloorToInt(t / 60f);
        int seconds = Mathf.FloorToInt(t % 60f);
        if (minutes > 0)
            return $"{minutes}:{seconds:00}";
        return $"{seconds}";
    }

    private void DoFlash()
    {
        // If battery already dead or recharging, don't flash
        if (batteryDepleted || isRecharging) return;

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

    // Called by the recharge Button OnClick in camera UI
    public void OnRechargeButtonPressed()
    {
        // Only allow recharge when not already recharging and when in camera view
        if (isRecharging) return;
        if (cameraSwitcher != null && !cameraSwitcher.InCameraView) return;

        StartCoroutine(RechargeCoroutine());
    }

    private System.Collections.IEnumerator RechargeCoroutine()
    {
        isRecharging = true;
        rechargeTimer = Mathf.Max(0.01f, rechargeDuration);

        // Disable the recharge button while recharging
        if (rechargeButton != null)
            rechargeButton.interactable = false;

        // Ensure flashlight is off while recharging
        if (flashlight != null) flashlight.enabled = false;

        while (rechargeTimer > 0f)
        {
            // If player leaves camera view while recharging, abort and require retry
            if (cameraSwitcher != null && !cameraSwitcher.InCameraView)
            {
                if (debugLogs) Debug.Log("[FlashlightToggle] Recharge aborted - left camera view.");
                // abort recharge: restore UI and state, keep batteryRemaining unchanged
                isRecharging = false;
                rechargeTimer = 0f;

                // Ensure HUD timer is visible again
                if (batteryTimerText != null && !batteryTimerText.gameObject.activeSelf)
                    batteryTimerText.gameObject.SetActive(true);

                // Update UI/labels now
                UpdateBatteryText();
                if (rechargeButton != null)
                    rechargeButton.interactable = false; // cameraSwitcher will hide the button anyway

                yield break;
            }

            // keep UI updated every frame (UpdateBatteryText will also run each Update)
            UpdateBatteryText();
            rechargeTimer -= Time.deltaTime;
            yield return null;
        }

        // Finished recharging successfully (player stayed in cameras)
        isRecharging = false;
        batteryRemaining = batteryDuration;
        batteryDepleted = false;

        // Update UI
        UpdateBatteryText();

        if (rechargeButton != null)
            rechargeButton.interactable = false; // remains non-interactable until camera view toggles or user presses again

        if (debugLogs) Debug.Log("[FlashlightToggle] Recharge complete.");
    }
}