using UnityEngine;

/// <summary>
/// Handles various camera effects including headbob, landing effects, and other movement-based animations
/// </summary>
public class CameraEffects : MonoBehaviour
{
    [Header("Headbob Settings")]
    [SerializeField] private bool enableHeadbob = true;
    [SerializeField] private float walkBobSpeed = 14f;
    [SerializeField] private float walkBobAmount = 0.05f;
    [SerializeField] private float runBobSpeed = 18f;
    [SerializeField] private float runBobAmount = 0.08f;
    [SerializeField] private float crouchBobSpeed = 8f;
    [SerializeField] private float crouchBobAmount = 0.025f;
    [SerializeField] private float bobSmoothness = 10f;
    
    [Header("Headbob Fine Tuning")]
    [SerializeField] private float horizontalBobMultiplier = 1.0f;
    [SerializeField] private float verticalBobMultiplier = 0.5f;
    [SerializeField] private float verticalBobFrequencyMultiplier = 2.0f; // Double frequency for more realistic bob
    
    [Header("Jump/Land Effects")]
    [SerializeField] private bool enableJumpLandEffects = true;
    [SerializeField] private float jumpKickAmount = 0.3f;
    [SerializeField] private float jumpKickDuration = 0.2f;
    [SerializeField] private float landShakeAmount = 0.2f;
    [SerializeField] private float landShakeDuration = 0.3f;
    [SerializeField] private float landShakeSpeed = 25f;
    [SerializeField] private AnimationCurve landShakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    
    [Header("Jump Effect Fine Tuning")]
    [SerializeField] private float jumpKickHorizontalMultiplier = 0.0f; // Side-to-side kick (X axis)
    [SerializeField] private float jumpKickVerticalMultiplier = 0.5f; // Up/down kick (Y axis)
    [SerializeField] private float jumpKickForwardMultiplier = 1.0f; // Forward/back kick (Z axis)
    
    [Header("Land Effect Fine Tuning")]
    [SerializeField] private float landShakeHorizontalMultiplier = 1.0f; // Side-to-side shake intensity
    [SerializeField] private float landShakeVerticalMultiplier = 0.5f; // Up/down shake intensity
    [SerializeField] private float landShakeForwardMultiplier = 0.0f; // Forward/back shake intensity
    
    [Header("General Settings")]
    [SerializeField] private float effectSmoothness = 8f;
    [SerializeField] private bool debugEffects = false;
    
    // Components
    private Transform cameraTransform;
    private CharacterMotor characterMotor;
    
    // Headbob state
    private float bobTimer;
    private Vector3 targetBobOffset;
    private Vector3 currentBobOffset;
    private float lastMovementMagnitude;
    
    // Jump/Land effect state
    private bool isProcessingJumpEffect;
    private bool isProcessingLandEffect;
    private float jumpEffectTimer;
    private float landEffectTimer;
    private Vector3 jumpKickOffset;
    private Vector3 landShakeOffset;
    
    // Base position
    private Vector3 baseLocalPosition;
    private bool hasInitialized;
    
    private void Start()
    {
        Initialize();
    }
    
    private void Initialize()
    {
        // Get camera transform (should be this transform)
        cameraTransform = transform;
        baseLocalPosition = cameraTransform.localPosition;
        
        // Find CharacterMotor in parent hierarchy
        characterMotor = GetComponentInParent<CharacterMotor>();
        if (characterMotor == null)
        {
            Debug.LogWarning("[CameraEffects] No CharacterMotor found in parent hierarchy. Effects may not work properly.");
            return;
        }
        
        // Subscribe to character events
        characterMotor.OnJumped += OnCharacterJumped;
        characterMotor.OnLanded += OnCharacterLanded;
        
        hasInitialized = true;
        
        if (debugEffects)
            Debug.Log("[CameraEffects] Camera effects initialized successfully");
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (characterMotor != null)
        {
            characterMotor.OnJumped -= OnCharacterJumped;
            characterMotor.OnLanded -= OnCharacterLanded;
        }
    }
    
    private void Update()
    {
        if (!hasInitialized) return;
        
        UpdateHeadbob();
        UpdateJumpLandEffects();
        ApplyAllEffects();
    }
    
    #region Headbob
    private void UpdateHeadbob()
    {
        if (!enableHeadbob || characterMotor == null)
        {
            targetBobOffset = Vector3.zero;
            return;
        }
        
        // Get movement parameters
        float currentSpeed = characterMotor.CurrentSpeed;
        Vector2 inputMagnitude = GetMovementInputMagnitude();
        bool isGrounded = characterMotor.IsGrounded;
        bool isCrouching = characterMotor.IsCrouching;
        bool isSprinting = characterMotor.IsSprinting;
        
        // Only apply headbob when moving and grounded
        if (inputMagnitude.magnitude > 0.1f && isGrounded && currentSpeed > 0.1f)
        {
            // Determine bob parameters based on movement state
            float bobSpeed, bobAmount;
            GetBobParameters(isSprinting, isCrouching, out bobSpeed, out bobAmount);
            
            // Update bob timer
            bobTimer += Time.deltaTime * bobSpeed;
            
            // Calculate bob offset with fine-tuning multipliers
            float horizontalBob = Mathf.Sin(bobTimer) * bobAmount * horizontalBobMultiplier;
            float verticalBob = Mathf.Sin(bobTimer * verticalBobFrequencyMultiplier) * bobAmount * verticalBobMultiplier;
            
            // Apply input magnitude scaling
            float movementScale = Mathf.Clamp01(inputMagnitude.magnitude);
            horizontalBob *= movementScale;
            verticalBob *= movementScale;
            
            targetBobOffset = new Vector3(horizontalBob, verticalBob, 0f);
        }
        else
        {
            // Gradually stop bobbing when not moving
            targetBobOffset = Vector3.zero;
        }
        
        // Smooth the bob offset
        currentBobOffset = Vector3.Lerp(currentBobOffset, targetBobOffset, bobSmoothness * Time.deltaTime);
    }
    
    private void GetBobParameters(bool isSprinting, bool isCrouching, out float bobSpeed, out float bobAmount)
    {
        if (isCrouching)
        {
            bobSpeed = crouchBobSpeed;
            bobAmount = crouchBobAmount;
        }
        else if (isSprinting)
        {
            bobSpeed = runBobSpeed;
            bobAmount = runBobAmount;
        }
        else
        {
            bobSpeed = walkBobSpeed;
            bobAmount = walkBobAmount;
        }
    }
    
    private Vector2 GetMovementInputMagnitude()
    {
        if (InputManager.HasInstance)
        {
            return InputManager.Instance.CurrentInput.movementInput;
        }
        return Vector2.zero;
    }
    #endregion
    
    #region Jump/Land Effects
    private void UpdateJumpLandEffects()
    {
        // Update jump effect
        if (isProcessingJumpEffect)
        {
            jumpEffectTimer += Time.deltaTime;
            float progress = jumpEffectTimer / jumpKickDuration;
            
            if (progress >= 1f)
            {
                // Jump effect finished
                isProcessingJumpEffect = false;
                jumpKickOffset = Vector3.zero;
            }
            else
            {
                // Apply jump kick curve with fine-tuning multipliers
                float kickStrength = Mathf.Sin(progress * Mathf.PI); // Sine curve for smooth kick
                float horizontalKick = jumpKickAmount * kickStrength * jumpKickHorizontalMultiplier;
                float verticalKick = -jumpKickAmount * kickStrength * jumpKickVerticalMultiplier; // Negative for downward kick
                float forwardKick = -jumpKickAmount * kickStrength * jumpKickForwardMultiplier; // Negative for backward kick
                
                jumpKickOffset = new Vector3(horizontalKick, verticalKick, forwardKick);
            }
        }
        
        // Update land effect
        if (isProcessingLandEffect)
        {
            landEffectTimer += Time.deltaTime;
            float progress = landEffectTimer / landShakeDuration;
            
            if (progress >= 1f)
            {
                // Land effect finished
                isProcessingLandEffect = false;
                landShakeOffset = Vector3.zero;
            }
            else
            {
                // Apply landing shake with fine-tuning multipliers
                float shakeIntensity = landShakeCurve.Evaluate(progress) * landShakeAmount;
                float shakeX = Mathf.Sin(Time.time * landShakeSpeed) * shakeIntensity * landShakeHorizontalMultiplier;
                float shakeY = Mathf.Sin(Time.time * landShakeSpeed * 1.1f) * shakeIntensity * landShakeVerticalMultiplier;
                float shakeZ = Mathf.Sin(Time.time * landShakeSpeed * 0.9f) * shakeIntensity * landShakeForwardMultiplier;
                
                landShakeOffset = new Vector3(shakeX, shakeY, shakeZ);
            }
        }
    }
    
    private void OnCharacterJumped()
    {
        if (!enableJumpLandEffects) return;
        
        // Start jump effect
        isProcessingJumpEffect = true;
        jumpEffectTimer = 0f;
        
        if (debugEffects)
            Debug.Log("[CameraEffects] Jump effect triggered");
    }
    
    private void OnCharacterLanded(float fallSpeed)
    {
        if (!enableJumpLandEffects) return;
        
        // Scale landing effect based on fall speed
        float effectScale = Mathf.Clamp01(fallSpeed / 10f); // Normalize fall speed
        
        // Start land effect only if fall speed is significant
        if (effectScale > 0.1f)
        {
            isProcessingLandEffect = true;
            landEffectTimer = 0f;
            
            // Scale the shake amount based on fall speed
            landShakeAmount = Mathf.Lerp(0.1f, 0.3f, effectScale);
            
            if (debugEffects)
                Debug.Log($"[CameraEffects] Land effect triggered with fall speed: {fallSpeed}, scale: {effectScale}");
        }
    }
    #endregion
    
    #region Effect Application
    private void ApplyAllEffects()
    {
        // Combine all effects
        Vector3 totalOffset = Vector3.zero;
        
        // Add headbob
        totalOffset += currentBobOffset;
        
        // Add jump/land effects
        totalOffset += jumpKickOffset;
        totalOffset += landShakeOffset;
        
        // Apply to camera position
        Vector3 targetPosition = baseLocalPosition + totalOffset;
        
        // Smooth the final position
        Vector3 currentPosition = cameraTransform.localPosition;
        Vector3 newPosition = Vector3.Lerp(currentPosition, targetPosition, effectSmoothness * Time.deltaTime);
        
        cameraTransform.localPosition = newPosition;
        
        if (debugEffects && totalOffset.magnitude > 0.001f)
        {
            Debug.Log($"[CameraEffects] Total offset: {totalOffset}, Bob: {currentBobOffset}, Jump: {jumpKickOffset}, Land: {landShakeOffset}");
        }
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// Resets all camera effects to their default state
    /// </summary>
    public void ResetAllEffects()
    {
        // Reset headbob
        bobTimer = 0f;
        targetBobOffset = Vector3.zero;
        currentBobOffset = Vector3.zero;
        
        // Reset jump/land effects
        isProcessingJumpEffect = false;
        isProcessingLandEffect = false;
        jumpEffectTimer = 0f;
        landEffectTimer = 0f;
        jumpKickOffset = Vector3.zero;
        landShakeOffset = Vector3.zero;
        
        // Reset camera position
        if (cameraTransform != null)
        {
            cameraTransform.localPosition = baseLocalPosition;
        }
        
        if (debugEffects)
            Debug.Log("[CameraEffects] All effects reset");
    }
    
    /// <summary>
    /// Enables or disables headbob effects
    /// </summary>
    public void SetHeadbobEnabled(bool enabled)
    {
        enableHeadbob = enabled;
        if (!enabled)
        {
            targetBobOffset = Vector3.zero;
            currentBobOffset = Vector3.zero;
        }
    }
    
    /// <summary>
    /// Enables or disables jump/land effects
    /// </summary>
    public void SetJumpLandEffectsEnabled(bool enabled)
    {
        enableJumpLandEffects = enabled;
        if (!enabled)
        {
            isProcessingJumpEffect = false;
            isProcessingLandEffect = false;
            jumpKickOffset = Vector3.zero;
            landShakeOffset = Vector3.zero;
        }
    }
    
    /// <summary>
    /// Triggers a custom camera shake effect (uses land effect multipliers)
    /// </summary>
    public void TriggerCustomShake(float intensity, float duration)
    {
        // Override land effect with custom shake
        isProcessingLandEffect = true;
        landEffectTimer = 0f;
        landShakeDuration = duration;
        landShakeAmount = intensity;
        
        if (debugEffects)
            Debug.Log($"[CameraEffects] Custom shake triggered: {intensity} intensity, {duration}s duration (uses land multipliers)");
    }
    
    /// <summary>
    /// Triggers a custom camera shake effect with specific axis control
    /// </summary>
    public void TriggerCustomShakeWithMultipliers(float intensity, float duration, float horizontalMult, float verticalMult, float forwardMult)
    {
        // Temporarily override multipliers for this shake
        float originalH = landShakeHorizontalMultiplier;
        float originalV = landShakeVerticalMultiplier;
        float originalF = landShakeForwardMultiplier;
        
        landShakeHorizontalMultiplier = horizontalMult;
        landShakeVerticalMultiplier = verticalMult;
        landShakeForwardMultiplier = forwardMult;
        
        // Trigger the shake
        TriggerCustomShake(intensity, duration);
        
        // Note: Multipliers will be restored when you call SetLandEffectMultipliers or manually reset them
        if (debugEffects)
            Debug.Log($"[CameraEffects] Custom shake with multipliers: H={horizontalMult:F2}, V={verticalMult:F2}, F={forwardMult:F2}");
    }
    
    /// <summary>
    /// Sets the base camera position (useful when camera height changes)
    /// </summary>
    public void UpdateBasePosition(Vector3 newBasePosition)
    {
        baseLocalPosition = newBasePosition;
        
        if (debugEffects)
            Debug.Log($"[CameraEffects] Base position updated to: {newBasePosition}");
    }
    
    /// <summary>
    /// Gets debug information about current effect states
    /// </summary>
    public string GetDebugInfo()
    {
        var info = "Camera Effects Debug:\n";
        info += $"Headbob Enabled: {enableHeadbob}\n";
        info += $"Bob Offset: {currentBobOffset}\n";
        info += $"Bob Timer: {bobTimer:F2}\n";
        info += $"Headbob Multipliers: H={horizontalBobMultiplier:F2}, V={verticalBobMultiplier:F2}, VF={verticalBobFrequencyMultiplier:F2}\n";
        info += $"Jump Effect Active: {isProcessingJumpEffect}\n";
        info += $"Land Effect Active: {isProcessingLandEffect}\n";
        info += $"Jump Offset: {jumpKickOffset}\n";
        info += $"Land Offset: {landShakeOffset}\n";
        info += $"Jump Multipliers: H={jumpKickHorizontalMultiplier:F2}, V={jumpKickVerticalMultiplier:F2}, F={jumpKickForwardMultiplier:F2}\n";
        info += $"Land Multipliers: H={landShakeHorizontalMultiplier:F2}, V={landShakeVerticalMultiplier:F2}, F={landShakeForwardMultiplier:F2}\n";
        info += $"Base Position: {baseLocalPosition}\n";
        info += $"Current Position: {(cameraTransform != null ? cameraTransform.localPosition : Vector3.zero)}\n";
        
        return info;
    }
    #endregion
    
    #region Fine Tuning API
    /// <summary>
    /// Set headbob fine-tuning multipliers at runtime
    /// </summary>
    public void SetHeadbobMultipliers(float horizontal, float vertical, float verticalFrequency)
    {
        horizontalBobMultiplier = horizontal;
        verticalBobMultiplier = vertical;
        verticalBobFrequencyMultiplier = verticalFrequency;
        
        if (debugEffects)
            Debug.Log($"[CameraEffects] Headbob multipliers updated: H={horizontal:F2}, V={vertical:F2}, VF={verticalFrequency:F2}");
    }
    
    /// <summary>
    /// Set jump effect fine-tuning multipliers at runtime
    /// </summary>
    public void SetJumpEffectMultipliers(float horizontal, float vertical, float forward)
    {
        jumpKickHorizontalMultiplier = horizontal;
        jumpKickVerticalMultiplier = vertical;
        jumpKickForwardMultiplier = forward;
        
        if (debugEffects)
            Debug.Log($"[CameraEffects] Jump multipliers updated: H={horizontal:F2}, V={vertical:F2}, F={forward:F2}");
    }
    
    /// <summary>
    /// Set land effect fine-tuning multipliers at runtime
    /// </summary>
    public void SetLandEffectMultipliers(float horizontal, float vertical, float forward)
    {
        landShakeHorizontalMultiplier = horizontal;
        landShakeVerticalMultiplier = vertical;
        landShakeForwardMultiplier = forward;
        
        if (debugEffects)
            Debug.Log($"[CameraEffects] Land multipliers updated: H={horizontal:F2}, V={vertical:F2}, F={forward:F2}");
    }
    
    /// <summary>
    /// Get current headbob multipliers
    /// </summary>
    public Vector3 GetHeadbobMultipliers()
    {
        return new Vector3(horizontalBobMultiplier, verticalBobMultiplier, verticalBobFrequencyMultiplier);
    }
    
    /// <summary>
    /// Get current jump effect multipliers
    /// </summary>
    public Vector3 GetJumpEffectMultipliers()
    {
        return new Vector3(jumpKickHorizontalMultiplier, jumpKickVerticalMultiplier, jumpKickForwardMultiplier);
    }
    
    /// <summary>
    /// Get current land effect multipliers
    /// </summary>
    public Vector3 GetLandEffectMultipliers()
    {
        return new Vector3(landShakeHorizontalMultiplier, landShakeVerticalMultiplier, landShakeForwardMultiplier);
    }
    #endregion
}
