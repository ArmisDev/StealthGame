using UnityEngine;
using System;

/// <summary>
/// ScriptableObject that holds all movement parameters and settings for a character.
/// Provides a designer-friendly way to tweak movement without touching code.
/// </summary>
[CreateAssetMenu(fileName = "CharacterConfig", menuName = "FPS/Character Configuration", order = 0)]
public class CharacterConfiguration : ScriptableObject
{
    #region Movement Speeds
    [Header("Movement Speeds")]
    [Space(5)]
    
    [Tooltip("Base walking speed in units per second")]
    [Range(1f, 10f)]
    public float walkSpeed = 4f;
    
    [Tooltip("Sprint speed in units per second")]
    [Range(5f, 20f)]
    public float sprintSpeed = 7f;
    
    [Tooltip("Movement speed while crouched")]
    [Range(0.5f, 5f)]
    public float crouchSpeed = 2f;
    
    [Tooltip("Movement speed while aiming down sights")]
    [Range(1f, 5f)]
    public float adsSpeed = 2.5f;
    
    [Tooltip("Air control speed - how fast you can move while airborne")]
    [Range(0f, 5f)]
    public float airSpeed = 2f;
    
    [Tooltip("Backwards movement speed multiplier")]
    [Range(0.5f, 1f)]
    public float backwardSpeedMultiplier = 0.85f;
    
    [Tooltip("Strafe movement speed multiplier")]
    [Range(0.8f, 1f)]
    public float strafeSpeedMultiplier = 0.95f;
    #endregion
    
    #region Acceleration & Friction
    [Header("Acceleration & Friction")]
    [Space(5)]
    
    [Tooltip("How quickly the character accelerates on the ground")]
    [Range(5f, 20f)]
    public float groundAcceleration = 10f;
    
    [Tooltip("How quickly the character decelerates on the ground")]
    [Range(5f, 20f)]
    public float groundDeceleration = 10f;
    
    [Tooltip("How quickly the character accelerates in the air")]
    [Range(1f, 10f)]
    public float airAcceleration = 2f;
    
    [Tooltip("How quickly the character decelerates in the air")]
    [Range(1f, 10f)]
    public float airDeceleration = 2f;
    
    [Tooltip("Friction applied when on the ground")]
    [Range(0f, 20f)]
    public float groundFriction = 6f;
    
    [Tooltip("Friction applied when in the air")]
    [Range(0f, 5f)]
    public float airFriction = 0.3f;
    
    [Tooltip("Optional acceleration curve for smooth speed transitions")]
    public AnimationCurve accelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Tooltip("How quickly the character stops when no input is given")]
    [Range(1f, 50f)]
    public float stopSpeed = 10f;
    #endregion
    
    #region Jump Settings
    [Header("Jump Settings")]
    [Space(5)]
    
    [Tooltip("Maximum jump height in units")]
    [Range(0.5f, 5f)]
    public float jumpHeight = 2f;
    
    [Tooltip("Time to reach peak of jump in seconds")]
    [Range(0.1f, 1f)]
    public float jumpDuration = 0.4f;
    
    [Tooltip("Gravity multiplier when falling (higher = faster fall)")]
    [Range(1f, 3f)]
    public float fallGravityMultiplier = 1.5f;
    
    [Tooltip("Gravity multiplier when jump button is released early")]
    [Range(1f, 5f)]
    public float jumpCutGravityMultiplier = 2f;
    
    [Tooltip("Maximum downward velocity")]
    [Range(-50f, -10f)]
    public float maxFallSpeed = -20f;
    
    [Tooltip("Time after leaving ground where jump is still allowed")]
    [Range(0f, 0.3f)]
    public float coyoteTime = 0.1f;
    
    [Tooltip("Time before landing where jump input is buffered")]
    [Range(0f, 0.3f)]
    public float jumpBufferTime = 0.1f;
    
    [Tooltip("Number of jumps allowed (1 = normal, 2 = double jump)")]
    [Range(1, 3)]
    public int maxJumpCount = 1;
    
    [Tooltip("Can hold jump button for variable height jump?")]
    public bool variableJumpHeight = true;
    
    [Tooltip("Minimum jump height when tapping jump (percentage of max)")]
    [Range(0.2f, 0.8f)]
    public float minJumpHeightPercent = 0.4f;
    #endregion
    
    #region Crouch Settings
    [Header("Crouch Settings")]
    [Space(5)]
    
    [Tooltip("Character height when standing")]
    [Range(1f, 3f)]
    public float standingHeight = 2f;
    
    [Tooltip("Character height when crouched")]
    [Range(0.5f, 1.5f)]
    public float crouchHeight = 1f;
    
    [Tooltip("Character controller radius")]
    [Range(0.1f, 1f)]
    public float characterRadius = 0.3f;
    
    [Tooltip("Speed of crouch/stand transition")]
    [Range(5f, 20f)]
    public float crouchTransitionSpeed = 10f;
    
    [Tooltip("Can crouch while in the air?")]
    public bool canCrouchInAir = true;
    
    [Tooltip("Automatically stand when space becomes available?")]
    public bool autoStandUp = true;
    
    [Tooltip("Jump height multiplier when jumping from crouch")]
    [Range(0.5f, 1.5f)]
    public float crouchJumpMultiplier = 0.8f;
    #endregion
    
    #region Slide Settings
    [Header("Slide Settings")]
    [Space(5)]
    
    [Tooltip("Enable sliding mechanic?")]
    public bool slidingEnabled = true;
    
    [Tooltip("Minimum speed required to initiate slide")]
    [Range(3f, 10f)]
    public float slideSpeedThreshold = 5f;
    
    [Tooltip("Initial slide speed boost")]
    [Range(1f, 2f)]
    public float slideSpeedBoost = 1.3f;
    
    [Tooltip("Maximum slide duration in seconds")]
    [Range(0.5f, 3f)]
    public float maxSlideDuration = 1.5f;
    
    [Tooltip("Slide deceleration rate")]
    [Range(1f, 10f)]
    public float slideDeceleration = 5f;
    
    [Tooltip("Slide control influence (0 = no control, 1 = full control)")]
    [Range(0f, 1f)]
    public float slideControlInfluence = 0.3f;
    
    [Tooltip("Cooldown between slides in seconds")]
    [Range(0f, 2f)]
    public float slideCooldown = 0.5f;
    
    [Tooltip("Friction curve over slide duration")]
    public AnimationCurve slideFrictionCurve = AnimationCurve.Linear(0f, 0.1f, 1f, 1f);
    #endregion
    
    #region Ground Detection
    [Header("Ground Detection")]
    [Space(5)]
    
    [Tooltip("Distance to check below character for ground")]
    [Range(0.01f, 0.5f)]
    public float groundCheckDistance = 0.1f;
    
    [Tooltip("Radius of ground check sphere")]
    [Range(0.1f, 1f)]
    public float groundCheckRadius = 0.3f;
    
    [Tooltip("Additional skin width for ground detection")]
    [Range(0.01f, 0.1f)]
    public float skinWidth = 0.02f;
    
    [Tooltip("Layers considered as ground")]
    public LayerMask groundLayers = -1;
    
    [Tooltip("Tolerance for considering character grounded")]
    [Range(0f, 0.2f)]
    public float groundedTolerance = 0.05f;
    
    [Tooltip("Use spherecast for ground detection (more accurate but expensive)")]
    public bool useSphereCast = true;
    #endregion
    
    #region Slope Handling
    [Header("Slope Handling")]
    [Space(5)]
    
    [Tooltip("Maximum angle in degrees that character can walk up")]
    [Range(30f, 60f)]
    public float maxSlopeAngle = 45f;
    
    [Tooltip("Speed multiplier when walking up slopes")]
    [Range(0.5f, 1f)]
    public float slopeUpSpeedMultiplier = 0.8f;
    
    [Tooltip("Speed multiplier when walking down slopes")]
    [Range(1f, 2f)]
    public float slopeDownSpeedMultiplier = 1.2f;
    
    [Tooltip("Should character slide down steep slopes?")]
    public bool slideOnSteepSlope = true;
    
    [Tooltip("Speed when sliding down steep slopes")]
    [Range(1f, 15f)]
    public float slopeSlideSpeed = 8f;
    
    [Tooltip("Force to keep character grounded on slopes")]
    [Range(0f, 10f)]
    public float slopeForceDown = 5f;
    
    [Tooltip("Ray distance for slope detection")]
    [Range(0.5f, 2f)]
    public float slopeRayLength = 1f;
    #endregion
    
    #region Step Handling
    [Header("Step Handling")]
    [Space(5)]
    
    [Tooltip("Maximum height of steps character can walk up")]
    [Range(0f, 1f)]
    public float maxStepHeight = 0.3f;
    
    [Tooltip("Automatically step up small obstacles?")]
    public bool autoStepUp = true;
    
    [Tooltip("Speed of step up animation")]
    [Range(5f, 20f)]
    public float stepUpSpeed = 10f;
    
    [Tooltip("Forward distance to check for steps")]
    [Range(0.1f, 1f)]
    public float stepCheckDistance = 0.4f;
    
    [Tooltip("Smooth step climbing (vs instant)")]
    public bool smoothStepClimbing = true;
    #endregion
    
    #region Physics & Collision
    [Header("Physics & Collision")]
    [Space(5)]
    
    [Tooltip("Character's mass for physics interactions")]
    [Range(50f, 150f)]
    public float mass = 80f;
    
    [Tooltip("Force applied when pushing rigidbodies")]
    [Range(0f, 10f)]
    public float pushPower = 2f;
    
    [Tooltip("Can push objects while in air?")]
    public bool canPushInAir = false;
    
    [Tooltip("Layers that can be pushed")]
    public LayerMask pushableLayers = -1;
    
    [Tooltip("Bounce reduction when hitting walls (0 = no bounce, 1 = full bounce)")]
    [Range(0f, 1f)]
    public float wallBounce = 0.1f;
    
    [Tooltip("Minimum collision normal Y to be considered ground")]
    [Range(0.5f, 1f)]
    public float minGroundNormalY = 0.7f;
    #endregion
    
    #region Advanced Movement
    [Header("Advanced Movement")]
    [Space(5)]
    
    [Tooltip("Enable wall running?")]
    public bool wallRunEnabled = false;
    
    [Tooltip("Enable ledge grabbing?")]
    public bool ledgeGrabEnabled = false;
    
    [Tooltip("Enable mantling/vaulting?")]
    public bool mantlingEnabled = false;
    
    [Tooltip("Moving platform support")]
    public bool supportMovingPlatforms = true;
    
    [Tooltip("Inherit velocity from moving platforms when jumping")]
    public bool inheritPlatformVelocity = true;
    
    [Tooltip("Platform velocity inheritance factor")]
    [Range(0f, 1f)]
    public float platformVelocityInheritance = 0.5f;
    #endregion
    
    #region Sprint & Stamina
    [Header("Sprint & Stamina")]
    [Space(5)]
    
    [Tooltip("Use stamina system for sprinting?")]
    public bool useStamina = false;
    
    [Tooltip("Maximum stamina")]
    [Range(50f, 200f)]
    public float maxStamina = 100f;
    
    [Tooltip("Stamina drain rate while sprinting")]
    [Range(5f, 30f)]
    public float staminaDrainRate = 15f;
    
    [Tooltip("Stamina recovery rate")]
    [Range(5f, 30f)]
    public float staminaRecoveryRate = 10f;
    
    [Tooltip("Delay before stamina starts recovering")]
    [Range(0f, 3f)]
    public float staminaRecoveryDelay = 1f;
    
    [Tooltip("Can sprint backwards?")]
    public bool canSprintBackwards = false;
    
    [Tooltip("Field of view change when sprinting")]
    [Range(0f, 20f)]
    public float sprintFOVIncrease = 10f;
    #endregion
    
    #region Debug Settings
    [Header("Debug Settings")]
    [Space(5)]
    
    [Tooltip("Show debug gizmos in scene view")]
    public bool showDebugGizmos = true;
    
    [Tooltip("Debug gizmo colors")]
    public Color groundCheckColor = Color.green;
    public Color slopeCheckColor = Color.yellow;
    public Color stepCheckColor = Color.cyan;
    #endregion
    
    #region Computed Properties
    /// <summary>
    /// Calculated gravity value based on jump height and duration
    /// </summary>
    public float Gravity => -(2f * jumpHeight) / (jumpDuration * jumpDuration);
    
    /// <summary>
    /// Initial jump velocity to reach desired jump height
    /// </summary>
    public float JumpVelocity => Mathf.Abs(Gravity) * jumpDuration;
    
    /// <summary>
    /// Minimum jump velocity based on minimum height percentage
    /// </summary>
    public float MinJumpVelocity => JumpVelocity * Mathf.Sqrt(minJumpHeightPercent);
    
    /// <summary>
    /// Get the appropriate movement speed based on current state
    /// </summary>
    public float GetMovementSpeed(bool isSprinting, bool isCrouching, bool isAiming)
    {
        if (isCrouching) return crouchSpeed;
        if (isAiming) return adsSpeed;
        if (isSprinting) return sprintSpeed;
        return walkSpeed;
    }
    
    /// <summary>
    /// Get acceleration based on grounded state
    /// </summary>
    public float GetAcceleration(bool isGrounded)
    {
        return isGrounded ? groundAcceleration : airAcceleration;
    }
    
    /// <summary>
    /// Get deceleration based on grounded state
    /// </summary>
    public float GetDeceleration(bool isGrounded)
    {
        return isGrounded ? groundDeceleration : airDeceleration;
    }
    
    /// <summary>
    /// Get friction based on grounded state
    /// </summary>
    public float GetFriction(bool isGrounded)
    {
        return isGrounded ? groundFriction : airFriction;
    }
    #endregion
    
    #region Validation
    private void OnValidate()
    {
        // Ensure crouch height is less than standing height
        crouchHeight = Mathf.Min(crouchHeight, standingHeight - 0.1f);
        
        // Ensure min jump height is less than 100%
        minJumpHeightPercent = Mathf.Clamp(minJumpHeightPercent, 0.1f, 0.95f);
        
        // Ensure step height is reasonable
        maxStepHeight = Mathf.Min(maxStepHeight, standingHeight * 0.4f);
        
        // Ensure ground check distance is positive
        groundCheckDistance = Mathf.Max(groundCheckDistance, 0.01f);
        
        // Ensure physics values are reasonable
        mass = Mathf.Max(mass, 1f);
        maxFallSpeed = Mathf.Min(maxFallSpeed, -1f);
        
        // Validate animation curves
        if (accelerationCurve == null || accelerationCurve.length == 0)
        {
            accelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
        
        if (slideFrictionCurve == null || slideFrictionCurve.length == 0)
        {
            slideFrictionCurve = AnimationCurve.Linear(0f, 0.1f, 1f, 1f);
        }
    }
    #endregion
}