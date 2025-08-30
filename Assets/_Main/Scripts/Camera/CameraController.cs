using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Look Settings")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;
    
    [Header("Height Settings")]
    public float eyeOffsetFromTop = 0.1f; // Distance from top of character controller
    public float heightTransitionSpeed = 10f; // Speed of camera height adjustment
    
    private float verticalRotation = 0;
    private Transform playerBody;
    private CharacterController characterController;
    private Vector3 baseLocalPosition;
    private float targetHeight;
    private CameraEffects cameraEffects;
    
    void Start()
    {
        // Lock cursor to center of screen
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Get player body (parent transform)
        playerBody = transform.parent;
        
        // Get character controller from parent
        if (playerBody != null)
        {
            characterController = playerBody.GetComponent<CharacterController>();
            if (characterController == null)
            {
                Debug.LogWarning("[CameraController] No CharacterController found on parent. Height adjustment will not work.");
            }
        }
        
        // Store initial local position as base
        baseLocalPosition = transform.localPosition;
        
        // Get camera effects component
        cameraEffects = GetComponent<CameraEffects>();
        
        // Set initial target height
        UpdateTargetHeight();
    }
    
    void Update()
    {
        HandleLookInput();
        UpdateCameraHeight();
        HandleCursorToggle();
    }
    
    private void HandleLookInput()
    {
        // Get input from InputManager
        if (!InputManager.HasInstance) return;
        
        Vector2 lookInput = InputManager.Instance.CurrentInput.lookInput;
        
        // Apply sensitivity
        lookInput *= mouseSensitivity;
        
        // Rotate player body horizontally
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * lookInput.x);
        }
        
        // Rotate camera vertically
        verticalRotation -= lookInput.y;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
        transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }
    
    private void UpdateCameraHeight()
    {
        if (characterController == null) return;
        
        // Update target height based on current character controller state
        float previousTargetHeight = targetHeight;
        UpdateTargetHeight();
        
        // Smoothly adjust camera height
        Vector3 currentPos = transform.localPosition;
        float currentHeight = currentPos.y;
        
        if (Mathf.Abs(currentHeight - targetHeight) > 0.01f)
        {
            float newHeight = Mathf.MoveTowards(currentHeight, targetHeight, heightTransitionSpeed * Time.deltaTime);
            Vector3 newPosition = new Vector3(currentPos.x, newHeight, currentPos.z);
            transform.localPosition = newPosition;
            
            // Notify camera effects of base position change
            if (cameraEffects != null && Mathf.Abs(targetHeight - previousTargetHeight) > 0.001f)
            {
                cameraEffects.UpdateBasePosition(newPosition);
            }
        }
    }
    
    private void UpdateTargetHeight()
    {
        if (characterController == null) return;
        
        // Calculate eye position: center + half height - offset from top
        float centerY = characterController.center.y;
        float halfHeight = characterController.height * 0.5f;
        targetHeight = centerY + halfHeight - eyeOffsetFromTop;
    }
    
    private void HandleCursorToggle()
    {
        // Toggle cursor lock with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? 
                CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = !Cursor.visible;
        }
    }
    
    /// <summary>
    /// Forces the camera to immediately snap to the correct height (no smooth transition)
    /// Useful when teleporting or when dramatic height changes occur
    /// </summary>
    public void ForceUpdateHeight()
    {
        if (characterController == null) return;
        
        UpdateTargetHeight();
        Vector3 currentPos = transform.localPosition;
        Vector3 newPosition = new Vector3(currentPos.x, targetHeight, currentPos.z);
        transform.localPosition = newPosition;
        
        // Notify camera effects of base position change
        if (cameraEffects != null)
        {
            cameraEffects.UpdateBasePosition(newPosition);
        }
    }
    
    /// <summary>
    /// Gets the current target height for the camera
    /// </summary>
    public float GetTargetHeight()
    {
        return targetHeight;
    }
}
