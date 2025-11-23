using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public Camera mainCamera;
    public Camera secondCamera;
    public Camera thirdCamera;

    public float openDelay = 0f; // Delay before enabling the new camera
    public float closeDelay = 0f; // Delay before disabling the old camera

    [Header("UI Buttons to Hide When Switching")]
    public GameObject[] buttonsToHide; // Assign buttons to hide in Inspector

    [Header("Instant Camera Switch Buttons")]
    public GameObject buttonSecondCamera; // Assign button for second camera
    public GameObject buttonThirdCamera;  // Assign button for third camera

    [Header("Camera-only UI")]
    [Tooltip("Recharge button shown only while viewing cameras")]
    public GameObject rechargeButton; // Assign the recharge Button GameObject here

    private bool isMainActive = true;

    public bool InCameraView => !isMainActive; // true when viewing cameras (not main)

    private void Start()
    {
        if (mainCamera != null) mainCamera.enabled = true;
        if (secondCamera != null) secondCamera.enabled = false;
        if (thirdCamera != null) thirdCamera.enabled = false;

        isMainActive = true;

        // Hide instant switch buttons at start
        if (buttonSecondCamera != null) buttonSecondCamera.SetActive(false);
        if (buttonThirdCamera != null) buttonThirdCamera.SetActive(false);

        // Hide recharge button at start (only visible in camera view)
        if (rechargeButton != null) rechargeButton.SetActive(false);
    }

    public void ToggleCamera()
    {
        if (mainCamera == null || secondCamera == null)
        {
            Debug.LogWarning("Cameras not assigned!");
            return;
        }

        if (isMainActive)
        {
            StartCoroutine(SwitchCameraWithDelays(mainCamera, secondCamera, openDelay, closeDelay, false));
            Debug.Log("Will switch to second camera after openDelay and closeDelay.");
        }
        else
        {
            StartCoroutine(SwitchCameraWithDelays(secondCamera, mainCamera, openDelay, closeDelay, true));
            Debug.Log("Will switch to main camera after openDelay and closeDelay.");
        }

        isMainActive = !isMainActive;
    }

    private System.Collections.IEnumerator SwitchCameraWithDelays(Camera fromCam, Camera toCam, float openDelay, float closeDelay, bool showButtons)
    {
        yield return new WaitForSeconds(openDelay);
        if (toCam != null) toCam.enabled = true;

        // Set buttons active/inactive after the same delay as camera switch
        SetButtonsActive(showButtons);

        // Show instant switch buttons and camera-only UI based on current state
        if (!isMainActive)
        {
            if (buttonSecondCamera != null) buttonSecondCamera.SetActive(true);
            if (buttonThirdCamera != null) buttonThirdCamera.SetActive(true);
            if (rechargeButton != null) rechargeButton.SetActive(true);
        }
        else
        {
            if (buttonSecondCamera != null) buttonSecondCamera.SetActive(false);
            if (buttonThirdCamera != null) buttonThirdCamera.SetActive(false);
            if (rechargeButton != null) rechargeButton.SetActive(false);
        }

        yield return new WaitForSeconds(closeDelay);
        if (fromCam != null) fromCam.enabled = false;
    }

    private void SetButtonsActive(bool active)
    {
        if (buttonsToHide != null)
        {
            foreach (var btn in buttonsToHide)
            {
                if (btn != null)
                    btn.SetActive(active);
            }
        }
    }

    // Call these from the UI buttons for instant camera switch
    public void SwitchToSecondCameraInstant()
    {
        if (secondCamera != null)
        {
            if (mainCamera != null) mainCamera.enabled = false;
            if (thirdCamera != null) thirdCamera.enabled = false;
            secondCamera.enabled = true;

            isMainActive = false;
            SetButtonsActive(false);
            if (buttonSecondCamera != null) buttonSecondCamera.SetActive(true);
            if (buttonThirdCamera != null) buttonThirdCamera.SetActive(true);
            if (rechargeButton != null) rechargeButton.SetActive(true);
        }
    }

    public void SwitchToThirdCameraInstant()
    {
        Debug.Log("SwitchToThirdCameraInstant called");
        if (thirdCamera != null)
        {
            if (mainCamera != null) mainCamera.enabled = false;
            if (secondCamera != null) secondCamera.enabled = false;
            thirdCamera.enabled = true;

            isMainActive = false;
            SetButtonsActive(false);
            if (buttonSecondCamera != null) buttonSecondCamera.SetActive(true);
            if (buttonThirdCamera != null) buttonThirdCamera.SetActive(true);
            if (rechargeButton != null) rechargeButton.SetActive(true);
        }
    }
}