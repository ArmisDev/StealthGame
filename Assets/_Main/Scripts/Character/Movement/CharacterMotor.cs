using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Core movement controller that processes input and moves the character.
/// Bridges input system to physics and handles all movement logic.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CharacterMotor : MonoBehaviour
{
    #region Serialized Fields
    [Header("Configuration")]
    [SerializeField] private CharacterConfiguration config;
    
    [Header("Input")]
    [SerializeField] private bool useInputManager = true;
    [SerializeField] private string inputProviderName = "Player";
    
    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private bool autoFindCamera = true;
    
    [Header("Ground Detection")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private bool visualizeGroundCheck = true;
    
    [Header("Step Detection")]
    [SerializeField] private Transform stepUpperPoint;
    [SerializeField] private Transform stepLowerPoint;
    
    [Header("Debug")]
    [SerializeField] private bool debugMovement = false;
    [SerializeField] private bool showDebugInfo = false;
    #endregion
    
    #region Private Fields
    // Components
    private CharacterController characterController;
    private IInputProvider inputProvider;
    
    // Movement State
    private Vector3 velocity;
    private Vector3 moveDirection;
    private Vector3 lastMoveDirection;
    private float currentSpeed;
    private float targetSpeed;
    
    // Ground State
    private bool isGrounded;
    private bool wasGrounded;
    private float groundCheckRadius;
    private RaycastHit groundHit;
    private Vector3 groundNormal = Vector3.up;
    private float groundDistance;
    private float lastGroundedTime;
    
    // Jump State
    private bool isJumping;
    private bool jumpRequested;
    private float jumpTimeCounter;
    private int jumpCount;
    private float lastJumpTime;
    private bool jumpBuffered;
    private float jumpBufferTimer;
    private float coyoteTimeCounter;
    
    // Crouch State
    private bool isCrouching;
    private bool crouchRequested;
    private float currentHeight;
    private float targetHeight;
    private bool forcedCrouch;
    
    // Sprint State
    private bool isSprinting;
    private bool canSprint;
    private float currentStamina;
    private float staminaRegenTimer;
    
    // Slide State
    private bool isSliding;
    private float slideTimer;
    private float slideCooldownTimer;
    private Vector3 slideDirection;
    private bool slideRecoveryMode;
    private bool wasCrouchingBeforeSlide;
    
    // Input State
    private InputData currentInput;
    private Vector2 smoothedInput;
    private Vector2 inputVelocity;
    
    // Physics
    private float gravityScale = 1f;
    private Vector3 externalForces;
    private List<Vector3> forceQueue = new List<Vector3>();
    
    // Platform
    private Transform platform;
    private Vector3 platformVelocity;
    private Vector3 lastPlatformPosition;
    
    // Collision
    private CollisionFlags collisionFlags;
    private List<ControllerColliderHit> collisionHits = new List<ControllerColliderHit>();
    
    // Slope
    private bool isOnSlope;
    private float slopeAngle;
    private Vector3 slopeNormal;
    private RaycastHit slopeHit;
    #endregion
    
    #region Properties
    /// <summary>Current velocity of the character</summary>
    public Vector3 Velocity => velocity;
    
    /// <summary>Current movement speed</summary>
    public float CurrentSpeed => currentSpeed;
    
    /// <summary>Is the character on the ground?</summary>
    public bool IsGrounded => isGrounded;
    
    /// <summary>Is the character jumping?</summary>
    public bool IsJumping => isJumping;
    
    /// <summary>Is the character crouching?</summary>
    public bool IsCrouching => isCrouching;
    
    /// <summary>Is the character sprinting?</summary>
    public bool IsSprinting => isSprinting;
    
    /// <summary>Is the character sliding?</summary>
    public bool IsSliding => isSliding;
    
    /// <summary>Is the character in slide recovery (movement restricted after slide)?</summary>
    public bool IsSlideRecovering => slideRecoveryMode;
    
    /// <summary>Current ground normal</summary>
    public Vector3 GroundNormal => groundNormal;
    
    /// <summary>Current stamina percentage (0-1)</summary>
    public float StaminaPercent => config.useStamina ? currentStamina / config.maxStamina : 1f;
    
    /// <summary>Get the character controller</summary>
    public CharacterController Controller => characterController;
    
    /// <summary>Get the current configuration</summary>
    public CharacterConfiguration Configuration => config;
    #endregion
    
    #region Events
    /// <summary>Called when the character lands</summary>
    public event Action<float> OnLanded;
    
    /// <summary>Called when the character jumps</summary>
    public event Action OnJumped;
    
    /// <summary>Called when the character starts sprinting</summary>
    public event Action OnSprintStarted;
    
    /// <summary>Called when the character stops sprinting</summary>
    public event Action OnSprintEnded;
    
    /// <summary>Called when the character starts crouching</summary>
    public event Action OnCrouchStarted;
    
    /// <summary>Called when the character stops crouching</summary>
    public event Action OnCrouchEnded;
    
    /// <summary>Called when the character starts sliding</summary>
    public event Action OnSlideStarted;
    
    /// <summary>Called when the character stops sliding</summary>
    public event Action OnSlideEnded;
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
        InitializeState();
    }
    
    private void Start()
    {
        SetupInput();
        SetupGroundCheck();
        SetupCamera();
    }
    
    private void Update()
    {
        UpdateInput();
        UpdateMovement();
        UpdateGravity();
        UpdateCrouch();
        UpdateSlide();
        UpdateStamina();
        
        ApplyMovement();
        
        UpdatePlatform();
        UpdateDebug();
    }
    
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        HandleCollision(hit);
    }
    
    private void OnDrawGizmos()
    {
        if (!config || !config.showDebugGizmos) return;
        
        DrawGroundCheckGizmos();
        DrawMovementGizmos();
    }
    #endregion
    
    #region Initialization
    private void InitializeComponents()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogError("[CharacterMotor] CharacterController component not found!");
            enabled = false;
            return;
        }
        
        if (config == null)
        {
            Debug.LogWarning("[CharacterMotor] No configuration assigned! Using default values.");
        }
    }
    
    private void InitializeState()
    {
        // Set initial character height
        currentHeight = config ? config.standingHeight : 2f;
        targetHeight = currentHeight;
        characterController.height = currentHeight;
        
        // Initialize stamina
        if (config && config.useStamina)
        {
            currentStamina = config.maxStamina;
        }
        
        // Initialize physics
        velocity = Vector3.zero;
        moveDirection = Vector3.zero;
        groundNormal = Vector3.up;
    }
    
    private void SetupInput()
    {
        if (useInputManager && InputManager.HasInstance)
        {
            inputProvider = InputManager.Instance.GetProvider(inputProviderName);
            if (inputProvider == null && InputManager.Instance.ActiveProvider != null)
            {
                inputProvider = InputManager.Instance.ActiveProvider;
                Debug.Log($"[CharacterMotor] Using active input provider: {inputProvider.ProviderName}");
            }
        }
        
        if (inputProvider == null)
        {
            Debug.LogWarning("[CharacterMotor] No input provider found. Movement will not work!");
        }
    }
    
    private void SetupGroundCheck()
    {
        if (groundCheckPoint == null)
        {
            GameObject groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.parent = transform;
            groundCheck.transform.localPosition = Vector3.zero;
            groundCheckPoint = groundCheck.transform;
        }
        
        groundCheckRadius = config ? config.groundCheckRadius : 0.3f;
    }
    
    private void SetupCamera()
    {
        if (autoFindCamera && cameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
            }
        }
    }
    #endregion
    
    #region Input Handling
    private void UpdateInput()
    {
        if (!useInputManager || !InputManager.HasInstance)
        {
            currentInput = InputData.CreateEmpty();
            return;
        }
        
        // Get input from InputManager
        currentInput = InputManager.Instance.CurrentInput;
        
        // Smooth input for better feel
        Vector2 targetInput = currentInput.movementInput;
        smoothedInput = Vector2.SmoothDamp(smoothedInput, targetInput, ref inputVelocity, 0.1f);
        
        // Handle jump input
        if (currentInput.jumpPressed)
        {
            jumpRequested = true;
            jumpBuffered = true;
            jumpBufferTimer = config.jumpBufferTime;
        }
        
        // Handle crouch input
        if (currentInput.crouchPressed)
        {
            crouchRequested = !crouchRequested;
        }
        
        // Update jump buffer
        if (jumpBuffered)
        {
            jumpBufferTimer -= Time.deltaTime;
            if (jumpBufferTimer <= 0f)
            {
                jumpBuffered = false;
            }
        }
    }
    #endregion
    
    #region Movement
    private void UpdateMovement()
    {
        // Check ground state
        CheckGround();
        
        // Calculate move direction relative to camera
        Vector3 forward = cameraTransform ? cameraTransform.forward : transform.forward;
        Vector3 right = cameraTransform ? cameraTransform.right : transform.right;
        
        // Remove Y component for direction
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        
        // Calculate desired move direction
        moveDirection = (forward * smoothedInput.y + right * smoothedInput.x).normalized;
        
        // Determine target speed
        DetermineTargetSpeed();
        
        // Apply acceleration/deceleration
        ApplyAcceleration();
        
        // Handle slopes
        if (isGrounded && isOnSlope && !isJumping)
        {
            HandleSlopeMovement();
        }
        
        // Apply horizontal velocity
        Vector3 horizontalVelocity = moveDirection * currentSpeed;
        velocity.x = horizontalVelocity.x;
        velocity.z = horizontalVelocity.z;
        
        // Handle jumping
        UpdateJump();
    }
    
    private void DetermineTargetSpeed()
    {
        if (smoothedInput.magnitude < 0.01f)
        {
            targetSpeed = 0f;
            return;
        }
        
        // Prevent movement during slide recovery (must stand up first)
        if (slideRecoveryMode)
        {
            targetSpeed = 0f;
            return;
        }
        
        // Check sprint conditions
        bool canSprintNow = CanSprint();
        isSprinting = canSprintNow && currentInput.sprint && !isCrouching && !isSliding;
        
        // Get base speed from config
        targetSpeed = config.GetMovementSpeed(isSprinting, isCrouching, currentInput.secondaryFire);
        
        // Apply directional modifiers
        float forwardDot = Vector3.Dot(moveDirection, transform.forward);
        if (forwardDot < -0.5f && !config.canSprintBackwards)
        {
            targetSpeed *= config.backwardSpeedMultiplier;
            isSprinting = false; // Can't sprint backwards
        }
        else if (Mathf.Abs(Vector3.Dot(moveDirection, transform.right)) > 0.7f)
        {
            targetSpeed *= config.strafeSpeedMultiplier;
        }
        
        // Apply air speed limit
        if (!isGrounded)
        {
            targetSpeed = Mathf.Min(targetSpeed, config.airSpeed);
        }
        
        // Scale by input magnitude for analog control
        targetSpeed *= smoothedInput.magnitude;
    }
    
    private void ApplyAcceleration()
    {
        if (targetSpeed > currentSpeed)
        {
            // Accelerating
            float acceleration = config.GetAcceleration(isGrounded);
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        }
        else if (targetSpeed < currentSpeed)
        {
            // Decelerating
            float deceleration = config.GetDeceleration(isGrounded);
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * Time.deltaTime);
        }
        
        // Apply friction when no input
        if (smoothedInput.magnitude < 0.01f && isGrounded)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, config.stopSpeed * Time.deltaTime);
        }
    }
    
    private void ApplyMovement()
    {
        // Add external forces
        velocity += externalForces;
        externalForces = Vector3.Lerp(externalForces, Vector3.zero, 10f * Time.deltaTime);
        
        // Apply platform movement
        if (platform != null)
        {
            velocity += platformVelocity;
        }
        
        // Move the character
        collisionFlags = characterController.Move(velocity * Time.deltaTime);
        
        // Clear forces after applying
        forceQueue.Clear();
    }
    #endregion
    
    #region Jumping
    private void UpdateJump()
    {
        // Update coyote time
        if (isGrounded)
        {
            coyoteTimeCounter = config.coyoteTime;
            jumpCount = 0;
            isJumping = false;
        }
        else if (wasGrounded && !isJumping)
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
        
        // Check if we can jump
        bool canJump = (isGrounded || coyoteTimeCounter > 0f || jumpCount < config.maxJumpCount - 1) && !forcedCrouch;
        
        // Handle jump request
        if ((jumpRequested || jumpBuffered) && canJump)
        {
            PerformJump();
        }
        
        // Handle variable jump height
        if (config.variableJumpHeight && isJumping && velocity.y > 0f)
        {
            if (!currentInput.jump)
            {
                // Apply jump cut gravity multiplier
                velocity.y *= (1f - config.jumpCutGravityMultiplier * Time.deltaTime);
            }
        }
        
        // Clear jump request
        jumpRequested = false;
    }
    
    private void PerformJump()
    {
        // Calculate jump velocity
        float jumpVelocity = config.JumpVelocity;
        
        // Apply crouch jump multiplier
        if (isCrouching)
        {
            jumpVelocity *= config.crouchJumpMultiplier;
        }
        
        // Inherit platform velocity if jumping from moving platform
        if (platform != null && config.inheritPlatformVelocity)
        {
            velocity += platformVelocity * config.platformVelocityInheritance;
        }
        
        // Apply jump
        velocity.y = jumpVelocity;
        isJumping = true;
        jumpCount++;
        lastJumpTime = Time.time;
        coyoteTimeCounter = 0f;
        jumpBuffered = false;
        
        // Fire event
        OnJumped?.Invoke();
        
        if (debugMovement)
            Debug.Log($"[CharacterMotor] Jump performed! Velocity: {jumpVelocity}");
    }
    #endregion
    
    #region Gravity
    private void UpdateGravity()
    {
        if (!isGrounded)
        {
            // Apply gravity with appropriate multiplier
            float gravityMultiplier = velocity.y < 0f ? config.fallGravityMultiplier : 1f;
            velocity.y += config.Gravity * gravityMultiplier * gravityScale * Time.deltaTime;
            
            // Clamp fall speed
            velocity.y = Mathf.Max(velocity.y, config.maxFallSpeed);
        }
        else if (!isJumping)
        {
            // Reset Y velocity when grounded (with small negative to keep grounded)
            velocity.y = -2f;
        }
    }
    #endregion
    
    #region Ground Detection
    private void CheckGround()
    {
        wasGrounded = isGrounded;
        
        // Perform ground check
        Vector3 checkPosition = groundCheckPoint.position;
        float checkDistance = config.groundCheckDistance + (velocity.y < 0 ? Mathf.Abs(velocity.y) * Time.deltaTime : 0f);
        
        if (config.useSphereCast)
        {
            isGrounded = Physics.SphereCast(
                checkPosition + Vector3.up * config.groundCheckRadius,
                groundCheckRadius,
                Vector3.down,
                out groundHit,
                checkDistance + config.groundCheckRadius,
                config.groundLayers,
                QueryTriggerInteraction.Ignore
            );
        }
        else
        {
            isGrounded = Physics.Raycast(
                checkPosition,
                Vector3.down,
                out groundHit,
                checkDistance,
                config.groundLayers,
                QueryTriggerInteraction.Ignore
            );
        }
        
        if (isGrounded)
        {
            groundNormal = groundHit.normal;
            groundDistance = groundHit.distance;
            
            // Check slope angle
            slopeAngle = Vector3.Angle(Vector3.up, groundNormal);
            isOnSlope = slopeAngle > 0.1f && slopeAngle <= config.maxSlopeAngle;
            
            // Update platform
            UpdatePlatformDetection();
            
            // Landing event
            if (!wasGrounded && velocity.y < -1f)
            {
                float fallSpeed = Mathf.Abs(velocity.y);
                OnLanded?.Invoke(fallSpeed);
                
                if (debugMovement)
                    Debug.Log($"[CharacterMotor] Landed with fall speed: {fallSpeed}");
            }
            
            lastGroundedTime = Time.time;
        }
        else
        {
            groundNormal = Vector3.up;
            groundDistance = float.MaxValue;
            isOnSlope = false;
            platform = null;
        }
    }
    #endregion
    
    #region Crouching
    private void UpdateCrouch()
    {
        // Check for forced crouch (ceiling above)
        CheckForcedCrouch();
        
        // Update crouch state
        if (crouchRequested || forcedCrouch)
        {
            if (!isCrouching)
            {
                StartCrouch();
            }
        }
        else
        {
            if (isCrouching && !forcedCrouch)
            {
                StopCrouch();
            }
        }
        
        // Smoothly adjust height
        if (Mathf.Abs(currentHeight - targetHeight) > 0.01f)
        {
            currentHeight = Mathf.MoveTowards(currentHeight, targetHeight, config.crouchTransitionSpeed * Time.deltaTime);
            characterController.height = currentHeight;
            
            // Adjust center position
            Vector3 center = characterController.center;
            center.y = currentHeight / 2f;
            characterController.center = center;
        }
    }
    
    private void StartCrouch()
    {
        isCrouching = true;
        targetHeight = config.crouchHeight;
        OnCrouchStarted?.Invoke();
        
        if (debugMovement)
            Debug.Log("[CharacterMotor] Started crouching");
    }
    
    private void StopCrouch()
    {
        // Check if there's space to stand
        if (!CanStand()) return;
        
        isCrouching = false;
        targetHeight = config.standingHeight;
        
        // Exit slide recovery mode when standing up
        if (slideRecoveryMode)
        {
            slideRecoveryMode = false;
            if (debugMovement)
                Debug.Log("[CharacterMotor] Exited slide recovery mode - can move again");
        }
        
        OnCrouchEnded?.Invoke();
        
        if (debugMovement)
            Debug.Log("[CharacterMotor] Stopped crouching");
    }
    
    private void CheckForcedCrouch()
    {
        if (!config.autoStandUp) return;
        
        forcedCrouch = !CanStand() && isCrouching;
    }
    
    private bool CanStand()
    {
        float checkHeight = config.standingHeight - config.crouchHeight;
        Vector3 checkPosition = transform.position + Vector3.up * config.crouchHeight;
        
        return !Physics.CheckSphere(
            checkPosition + Vector3.up * (checkHeight / 2f),
            config.characterRadius,
            config.groundLayers,
            QueryTriggerInteraction.Ignore
        );
    }
    #endregion
    
    #region Sliding
    private void UpdateSlide()
    {
        if (!config.slidingEnabled) return;
        
        // Check slide initiation
        if (currentInput.crouchPressed && isSprinting && currentSpeed >= config.slideSpeedThreshold && slideCooldownTimer <= 0f)
        {
            StartSlide();
        }
        
        // Update slide
        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            
            // Apply slide deceleration
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, config.slideDeceleration * Time.deltaTime);
            
            // Apply slide direction with limited control
            Vector3 inputInfluence = moveDirection * config.slideControlInfluence;
            slideDirection = Vector3.Lerp(slideDirection, slideDirection + inputInfluence, Time.deltaTime * 2f).normalized;
            
            // Override move direction during slide
            moveDirection = slideDirection;
            
            // End slide conditions
            if (slideTimer <= 0f || currentSpeed < 1f || currentInput.jumpPressed)
            {
                StopSlide();
            }
        }
        
        // Update cooldown
        if (slideCooldownTimer > 0f)
        {
            slideCooldownTimer -= Time.deltaTime;
        }
    }
    
    private void StartSlide()
    {
        isSliding = true;
        slideTimer = config.maxSlideDuration;
        slideDirection = moveDirection;
        currentSpeed *= config.slideSpeedBoost;
        
        // Remember if we were crouching before the slide
        wasCrouchingBeforeSlide = isCrouching;
        
        // Force crouch during slide
        if (!isCrouching)
        {
            StartCrouch();
        }
        
        OnSlideStarted?.Invoke();
        
        if (debugMovement)
            Debug.Log("[CharacterMotor] Started sliding");
    }
    
    private void StopSlide()
    {
        isSliding = false;
        slideCooldownTimer = config.slideCooldown;
        
        // Enter slide recovery mode - character must stand up to move again
        if (!wasCrouchingBeforeSlide)
        {
            // If they weren't crouching before slide, they must stand up to move
            slideRecoveryMode = true;
        }
        // If they were already crouching before slide, they can continue crouching and moving
        
        OnSlideEnded?.Invoke();
        
        if (debugMovement)
            Debug.Log($"[CharacterMotor] Stopped sliding - recovery mode: {slideRecoveryMode}");
    }
    #endregion
    
    #region Stamina
    private void UpdateStamina()
    {
        if (!config.useStamina) return;
        
        if (isSprinting && smoothedInput.magnitude > 0.1f)
        {
            // Drain stamina
            currentStamina -= config.staminaDrainRate * Time.deltaTime;
            currentStamina = Mathf.Max(0f, currentStamina);
            staminaRegenTimer = config.staminaRecoveryDelay;
            
            // Stop sprinting if out of stamina
            if (currentStamina <= 0f)
            {
                isSprinting = false;
                OnSprintEnded?.Invoke();
            }
        }
        else
        {
            // Regenerate stamina after delay
            if (staminaRegenTimer > 0f)
            {
                staminaRegenTimer -= Time.deltaTime;
            }
            else
            {
                currentStamina += config.staminaRecoveryRate * Time.deltaTime;
                currentStamina = Mathf.Min(config.maxStamina, currentStamina);
            }
        }
    }
    
    private bool CanSprint()
    {
        if (!config.useStamina) return true;
        return currentStamina > 0f;
    }
    #endregion
    
    #region Slope Handling
    private void HandleSlopeMovement()
    {
        if (slopeAngle > config.maxSlopeAngle && config.slideOnSteepSlope)
        {
            // Slide down steep slopes
            Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
            velocity += slideDir * config.slopeSlideSpeed * Time.deltaTime;
        }
        else if (moveDirection.magnitude > 0f)
        {
            // Adjust movement for slope
            Vector3 slopeDir = Vector3.ProjectOnPlane(moveDirection, groundNormal).normalized;
            
            // Apply slope speed modifier
            float slopeDot = Vector3.Dot(moveDirection, -groundNormal);
            float speedMultiplier = slopeDot > 0f ? config.slopeUpSpeedMultiplier : config.slopeDownSpeedMultiplier;
            
            velocity.x = slopeDir.x * currentSpeed * speedMultiplier;
            velocity.z = slopeDir.z * currentSpeed * speedMultiplier;
            
            // Apply downward force to stick to slope
            if (velocity.y > 0f)
            {
                velocity.y = -config.slopeForceDown;
            }
        }
    }
    #endregion
    
    #region Platform Support
    private void UpdatePlatformDetection()
    {
        if (!config.supportMovingPlatforms) return;
        
        Transform newPlatform = groundHit.collider?.transform;
        
        // Check if platform is moving (has rigidbody or tagged)
        if (newPlatform != null)
        {
            bool isMovingPlatform = newPlatform.GetComponent<Rigidbody>() != null || 
                                   newPlatform.CompareTag("MovingPlatform");
            
            if (!isMovingPlatform)
                newPlatform = null;
        }
        
        // Platform changed
        if (newPlatform != platform)
        {
            platform = newPlatform;
            if (platform != null)
            {
                lastPlatformPosition = platform.position;
            }
        }
    }
    
    private void UpdatePlatform()
    {
        if (platform == null) return;
        
        // Calculate platform movement
        Vector3 platformDelta = platform.position - lastPlatformPosition;
        platformVelocity = platformDelta / Time.deltaTime;
        lastPlatformPosition = platform.position;
        
        // Move with platform (for rotation, use transform parenting or manual rotation tracking)
        if (isGrounded)
        {
            characterController.Move(platformDelta);
        }
    }
    #endregion
    
    #region Collision Handling
    private void HandleCollision(ControllerColliderHit hit)
    {
        collisionHits.Add(hit);
        
        // Push rigidbodies
        if (config.pushPower > 0f)
        {
            Rigidbody body = hit.collider.attachedRigidbody;
            if (body != null && !body.isKinematic)
            {
                // Check if we can push
                bool canPush = isGrounded || config.canPushInAir;
                if (!canPush) return;
                
                // Check push layer
                int layer = hit.gameObject.layer;
                if ((config.pushableLayers.value & (1 << layer)) == 0) return;
                
                // Calculate push direction
                Vector3 pushDir = hit.moveDirection;
                pushDir.y = 0f;
                pushDir.Normalize();
                
                // Apply push force
                float pushForce = config.pushPower * Mathf.Clamp01(currentSpeed / config.walkSpeed);
                body.AddForce(pushDir * pushForce, ForceMode.Impulse);
            }
        }
        
        // Wall bounce
        if (!isGrounded && config.wallBounce > 0f)
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle > 60f) // Wall-like surface
            {
                Vector3 reflection = Vector3.Reflect(velocity.normalized, hit.normal);
                velocity = reflection * velocity.magnitude * config.wallBounce;
            }
        }
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// Add an external force to the character
    /// </summary>
    public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force)
    {
        switch (mode)
        {
            case ForceMode.Force:
                externalForces += force * Time.deltaTime;
                break;
            case ForceMode.Impulse:
                externalForces += force;
                break;
            case ForceMode.VelocityChange:
                velocity += force;
                break;
            case ForceMode.Acceleration:
                externalForces += force * config.mass * Time.deltaTime;
                break;
        }
    }
    
    /// <summary>
    /// Set the character's velocity directly
    /// </summary>
    public void SetVelocity(Vector3 newVelocity)
    {
        velocity = newVelocity;
    }
    
    /// <summary>
    /// Teleport the character to a position
    /// </summary>
    public void Teleport(Vector3 position)
    {
        characterController.enabled = false;
        transform.position = position;
        characterController.enabled = true;
        velocity = Vector3.zero;
        externalForces = Vector3.zero;
        
        // Force camera to update height immediately after teleport
        if (cameraTransform != null)
        {
            CameraController cameraController = cameraTransform.GetComponent<CameraController>();
            if (cameraController != null)
            {
                cameraController.ForceUpdateHeight();
            }
        }
    }
    
    /// <summary>
    /// Force the character to jump
    /// </summary>
    public void ForceJump(float jumpForce = -1f)
    {
        if (jumpForce < 0f)
            jumpForce = config.JumpVelocity;
        
        velocity.y = jumpForce;
        isJumping = true;
        OnJumped?.Invoke();
    }
    
    /// <summary>
    /// Reset movement state
    /// </summary>
    public void ResetMovement()
    {
        velocity = Vector3.zero;
        externalForces = Vector3.zero;
        currentSpeed = 0f;
        isJumping = false;
        isSprinting = false;
        isSliding = false;
        slideRecoveryMode = false;
        jumpCount = 0;
    }
    #endregion
    
    #region Debug
    private void UpdateDebug()
    {
        if (!showDebugInfo) return;
        
        // You can implement debug UI here or use this for logging
    }
    
    private void DrawGroundCheckGizmos()
    {
        if (groundCheckPoint == null) return;
        
        Gizmos.color = isGrounded ? config.groundCheckColor : Color.red;
        
        if (config.useSphereCast)
        {
            Gizmos.DrawWireSphere(groundCheckPoint.position, config.groundCheckRadius);
            Gizmos.DrawLine(groundCheckPoint.position, groundCheckPoint.position + Vector3.down * config.groundCheckDistance);
        }
        else
        {
            Gizmos.DrawRay(groundCheckPoint.position, Vector3.down * config.groundCheckDistance);
        }
    }
    
    private void DrawMovementGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Draw velocity
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, velocity * 0.5f);
        
        // Draw move direction
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, moveDirection * 2f);
    }
    
    private void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 500));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label($"Speed: {currentSpeed:F2} / {targetSpeed:F2}");
        GUILayout.Label($"Velocity: {velocity:F2}");
        GUILayout.Label($"Grounded: {isGrounded}");
        GUILayout.Label($"Slope Angle: {slopeAngle:F1}°");
        GUILayout.Label($"Jump Count: {jumpCount}");
        GUILayout.Label($"Stamina: {StaminaPercent:P0}");
        
        GUILayout.Space(10);
        
        GUILayout.Label($"States:");
        GUILayout.Label($"  Jumping: {isJumping}");
        GUILayout.Label($"  Crouching: {isCrouching}");
        GUILayout.Label($"  Sprinting: {isSprinting}");
        GUILayout.Label($"  Sliding: {isSliding}");
        GUILayout.Label($"  Slide Recovery: {slideRecoveryMode}");
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
    #endregion
}