using UnityEngine;
using System;

/// <summary>
/// Pure data structure that holds all possible inputs for a single frame.
/// Designed to be network-friendly, serializable, and decoupled from input sources.
/// </summary>
[System.Serializable]
public struct InputData
{
    #region Movement Input
    /// <summary>Horizontal movement input (WASD/Left Stick). X = strafe, Y = forward/back</summary>
    public Vector2 movementInput;
    
    /// <summary>Vertical movement input (jump/crouch as analog values)</summary>
    public float verticalMovementInput;
    #endregion

    #region Look Input
    /// <summary>Look delta input from mouse or right stick</summary>
    public Vector2 lookInput;
    
    /// <summary>Look sensitivity multiplier for this frame</summary>
    public float lookSensitivity;
    #endregion

    #region Action States (Digital Inputs)
    /// <summary>Jump button state</summary>
    public bool jump;
    
    /// <summary>Jump button pressed this frame</summary>
    public bool jumpPressed;
    
    /// <summary>Jump button released this frame</summary>
    public bool jumpReleased;
    
    /// <summary>Crouch button state</summary>
    public bool crouch;
    
    /// <summary>Crouch button pressed this frame</summary>
    public bool crouchPressed;
    
    /// <summary>Crouch button released this frame</summary>
    public bool crouchReleased;
    
    /// <summary>Sprint button state</summary>
    public bool sprint;
    
    /// <summary>Sprint button pressed this frame</summary>
    public bool sprintPressed;
    
    /// <summary>Sprint button released this frame</summary>
    public bool sprintReleased;
    
    /// <summary>Primary fire button state</summary>
    public bool fire;
    
    /// <summary>Primary fire button pressed this frame</summary>
    public bool firePressed;
    
    /// <summary>Primary fire button released this frame</summary>
    public bool fireReleased;
    
    /// <summary>Secondary fire button state (ADS/Alt fire)</summary>
    public bool secondaryFire;
    
    /// <summary>Secondary fire button pressed this frame</summary>
    public bool secondaryFirePressed;
    
    /// <summary>Secondary fire button released this frame</summary>
    public bool secondaryFireReleased;
    
    /// <summary>Reload button state</summary>
    public bool reload;
    
    /// <summary>Reload button pressed this frame</summary>
    public bool reloadPressed;
    
    /// <summary>Interact button state</summary>
    public bool interact;
    
    /// <summary>Interact button pressed this frame</summary>
    public bool interactPressed;
    
    /// <summary>Melee attack button state</summary>
    public bool melee;
    
    /// <summary>Melee attack button pressed this frame</summary>
    public bool meleePressed;
    
    /// <summary>Weapon switch up</summary>
    public bool weaponSwitchUp;
    
    /// <summary>Weapon switch down</summary>
    public bool weaponSwitchDown;
    
    /// <summary>Grenade throw button</summary>
    public bool throwGrenade;
    
    /// <summary>Grenade throw button pressed this frame</summary>
    public bool throwGrenadePressed;
    
    /// <summary>Flashlight toggle</summary>
    public bool flashlightPressed;
    
    /// <summary>Inventory/Menu toggle</summary>
    public bool menuPressed;
    #endregion

    #region Analog Values
    /// <summary>Left trigger pressure (0-1)</summary>
    public float leftTrigger;
    
    /// <summary>Right trigger pressure (0-1)</summary>
    public float rightTrigger;
    
    /// <summary>Lean amount (-1 = left, 1 = right, 0 = center)</summary>
    public float leanInput;
    
    /// <summary>Walk/Run modifier (0 = walk, 1 = run)</summary>
    public float walkRunModifier;
    
    /// <summary>Zoom level for scoped weapons (0-1)</summary>
    public float zoomLevel;
    
    /// <summary>Mouse scroll wheel delta</summary>
    public float scrollWheelDelta;
    #endregion

    #region Utility Methods
    /// <summary>
    /// Resets all input data to default values
    /// </summary>
    public void Reset()
    {
        // Movement
        movementInput = Vector2.zero;
        verticalMovementInput = 0f;
        
        // Look
        lookInput = Vector2.zero;
        lookSensitivity = 1f;
        
        // Digital inputs - current state
        jump = false;
        crouch = false;
        sprint = false;
        fire = false;
        secondaryFire = false;
        reload = false;
        interact = false;
        melee = false;
        weaponSwitchUp = false;
        weaponSwitchDown = false;
        throwGrenade = false;
        
        // Digital inputs - frame events
        jumpPressed = false;
        jumpReleased = false;
        crouchPressed = false;
        crouchReleased = false;
        sprintPressed = false;
        sprintReleased = false;
        firePressed = false;
        fireReleased = false;
        secondaryFirePressed = false;
        secondaryFireReleased = false;
        reloadPressed = false;
        interactPressed = false;
        meleePressed = false;
        throwGrenadePressed = false;
        flashlightPressed = false;
        menuPressed = false;
        
        // Analog values
        leftTrigger = 0f;
        rightTrigger = 0f;
        leanInput = 0f;
        walkRunModifier = 1f;
        zoomLevel = 0f;
        scrollWheelDelta = 0f;
    }
    
    /// <summary>
    /// Validates and clamps all input values to their expected ranges
    /// </summary>
    public void ValidateAndClamp()
    {
        // Clamp movement input to -1 to 1 range
        movementInput.x = Mathf.Clamp(movementInput.x, -1f, 1f);
        movementInput.y = Mathf.Clamp(movementInput.y, -1f, 1f);
        
        // Clamp vertical movement
        verticalMovementInput = Mathf.Clamp(verticalMovementInput, -1f, 1f);
        
        // Look sensitivity should be positive
        lookSensitivity = Mathf.Max(0f, lookSensitivity);
        
        // Clamp analog values to 0-1 range
        leftTrigger = Mathf.Clamp01(leftTrigger);
        rightTrigger = Mathf.Clamp01(rightTrigger);
        walkRunModifier = Mathf.Clamp01(walkRunModifier);
        zoomLevel = Mathf.Clamp01(zoomLevel);
        
        // Clamp lean input to -1 to 1 range
        leanInput = Mathf.Clamp(leanInput, -1f, 1f);
    }
    
    /// <summary>
    /// Clears only the frame-specific input events (pressed/released states)
    /// Useful for clearing events after they've been processed
    /// </summary>
    public void ClearFrameEvents()
    {
        jumpPressed = false;
        jumpReleased = false;
        crouchPressed = false;
        crouchReleased = false;
        sprintPressed = false;
        sprintReleased = false;
        firePressed = false;
        fireReleased = false;
        secondaryFirePressed = false;
        secondaryFireReleased = false;
        reloadPressed = false;
        interactPressed = false;
        meleePressed = false;
        throwGrenadePressed = false;
        flashlightPressed = false;
        menuPressed = false;
        
        // Clear scroll wheel delta as it's frame-specific
        scrollWheelDelta = 0f;
    }
    
    /// <summary>
    /// Creates a copy of this InputData with validated and clamped values
    /// </summary>
    public InputData GetValidatedCopy()
    {
        InputData copy = this;
        copy.ValidateAndClamp();
        return copy;
    }
    
    /// <summary>
    /// Checks if any movement input is active
    /// </summary>
    public bool HasMovementInput => movementInput.sqrMagnitude > 0.01f || Mathf.Abs(verticalMovementInput) > 0.01f;
    
    /// <summary>
    /// Checks if any look input is active
    /// </summary>
    public bool HasLookInput => lookInput.sqrMagnitude > 0.01f;
    
    /// <summary>
    /// Checks if any action button is currently pressed
    /// </summary>
    public bool HasAnyActionPressed => jump || crouch || sprint || fire || secondaryFire || 
                                      reload || interact || melee || weaponSwitchUp || 
                                      weaponSwitchDown || throwGrenade;
    
    /// <summary>
    /// Checks if any frame event occurred this frame
    /// </summary>
    public bool HasAnyFrameEvent => jumpPressed || jumpReleased || crouchPressed || crouchReleased ||
                                   sprintPressed || sprintReleased || firePressed || fireReleased ||
                                   secondaryFirePressed || secondaryFireReleased || reloadPressed ||
                                   interactPressed || meleePressed || throwGrenadePressed ||
                                   flashlightPressed || menuPressed;
    #endregion

    #region Static Factory Methods
    /// <summary>
    /// Creates a new InputData with all values reset to defaults
    /// </summary>
    public static InputData CreateEmpty()
    {
        InputData inputData = new InputData();
        inputData.Reset();
        return inputData;
    }
    
    /// <summary>
    /// Creates InputData from basic movement and look vectors
    /// </summary>
    public static InputData CreateBasic(Vector2 movement, Vector2 look)
    {
        InputData inputData = CreateEmpty();
        inputData.movementInput = movement;
        inputData.lookInput = look;
        inputData.ValidateAndClamp();
        return inputData;
    }
    #endregion
}
