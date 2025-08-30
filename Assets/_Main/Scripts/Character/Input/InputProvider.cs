using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;

/// <summary>
/// Human input handler that reads actual player input from Unity's Input System.
/// Implements IInputProvider to provide standardized input data to character controllers.
/// </summary>
public class InputProvider : MonoBehaviour, IInputProvider
{
    #region Serialized Fields
    [Header("Input Action Asset")]
    [SerializeField] private InputActionAsset inputActionAsset;
    
    [Header("Action Map Names")]
    [SerializeField] private string gameplayActionMapName = "Gameplay";
    [SerializeField] private string menuActionMapName = "Menu";
    
    [Header("Input Settings")]
    [SerializeField] private InputSettings inputSettings = new InputSettings();
    
    [Header("Provider Settings")]
    [SerializeField] private string providerName = "Player Input";
    [SerializeField] private int priority = 100;
    [SerializeField] private bool enableInputBuffering = true;
    [SerializeField] private float inputBufferTime = 0.1f;
    
    [Header("Debug")]
    [SerializeField] private bool debugInput = false;
    #endregion
    
    #region Private Fields
    private InputActionMap currentActionMap;
    private InputActionMap gameplayActionMap;
    private InputActionMap menuActionMap;
    
    private InputData currentInputData;
    private InputData previousInputData;
    
    // Input Actions - Movement
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction crouchAction;
    private InputAction sprintAction;
    
    // Input Actions - Combat
    private InputAction fireAction;
    private InputAction secondaryFireAction;
    private InputAction reloadAction;
    private InputAction meleeAction;
    private InputAction throwGrenadeAction;
    
    // Input Actions - Interaction
    private InputAction interactAction;
    private InputAction flashlightAction;
    private InputAction menuAction;
    
    // Input Actions - Weapon
    private InputAction weaponSwitchAction;
    private InputAction scrollWheelAction;
    
    // Input Actions - Analog
    private InputAction leftTriggerAction;
    private InputAction rightTriggerAction;
    private InputAction leanAction;
    
    // Input buffering
    private Dictionary<string, InputBuffer> inputBuffers = new Dictionary<string, InputBuffer>();
    
    // State tracking
    private bool isEnabled = true;
    private bool isInitialized = false;
    private Gamepad currentGamepad;
    
    // Toggle states (moved from InputSettings)
    private bool crouchToggleState = false;
    private bool sprintToggleState = false;
    private bool adsToggleState = false;
    
    // Haptic feedback
    private Coroutine hapticCoroutine;
    #endregion
    
    #region IInputProvider Implementation
    public bool IsEnabled 
    { 
        get => isEnabled; 
        set 
        {
            if (isEnabled != value)
            {
                isEnabled = value;
                if (value)
                    OnProviderEnabled();
                else
                    OnProviderDisabled();
            }
        }
    }
    
    public bool IsAvailable => isInitialized && inputActionAsset != null;
    public int Priority => priority;
    public string ProviderName => providerName;
    public bool HasActiveInput => currentInputData.HasMovementInput || currentInputData.HasLookInput || currentInputData.HasAnyActionPressed;
    public bool SupportsContinuousInput => true;
    public bool SupportsHapticFeedback => currentGamepad != null;
    
    public InputData GetInputData()
    {
        if (!IsEnabled || !IsAvailable)
            return InputData.CreateEmpty();
            
        return currentInputData.GetValidatedCopy();
    }
    
    public void UpdateInput()
    {
        if (!IsEnabled || !IsAvailable)
            return;
            
        // Store previous frame data
        previousInputData = currentInputData;
        
        // Reset current frame data
        currentInputData = InputData.CreateEmpty();
        
        // Update input data from actions
        UpdateMovementInput();
        UpdateLookInput();
        UpdateActionInput();
        UpdateAnalogInput();
        
        // Process input buffering
        if (enableInputBuffering)
            ProcessInputBuffering();
        
        // Apply input settings
        ApplyInputSettings();
        
        // Validate and clamp values
        currentInputData.ValidateAndClamp();
        
        // Debug output
        if (debugInput)
            DebugInputData();
    }
    
    public void TriggerHapticFeedback(float intensity, float duration)
    {
        if (!SupportsHapticFeedback)
            return;
            
        StopHapticFeedback();
        hapticCoroutine = StartCoroutine(HapticFeedbackCoroutine(intensity, duration));
    }
    
    public void StopHapticFeedback()
    {
        if (hapticCoroutine != null)
        {
            StopCoroutine(hapticCoroutine);
            hapticCoroutine = null;
        }
        
        if (currentGamepad != null)
        {
            currentGamepad.SetMotorSpeeds(0f, 0f);
        }
    }
    
    public void OnProviderEnabled()
    {
        if (currentActionMap != null)
            currentActionMap.Enable();
    }
    
    public void OnProviderDisabled()
    {
        if (currentActionMap != null)
            currentActionMap.Disable();
            
        StopHapticFeedback();
    }
    
    public void ResetInputState()
    {
        currentInputData.Reset();
        previousInputData.Reset();
        inputBuffers.Clear();
        
        // Reset toggle states
        crouchToggleState = false;
        sprintToggleState = false;
        adsToggleState = false;
    }
    
    public void ApplyConfiguration(object config)
    {
        if (config is InputSettings settings)
        {
            inputSettings = settings;
        }
    }
    
    public object GetConfiguration()
    {
        return inputSettings;
    }
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        InitializeInputSystem();
    }
    
    private void Start()
    {
        SetActionMap(gameplayActionMapName);
    }
    
    private void OnEnable()
    {
        if (isInitialized)
            OnProviderEnabled();
    }
    
    private void OnDisable()
    {
        OnProviderDisabled();
    }
    
    private void OnDestroy()
    {
        CleanupInputSystem();
    }
    #endregion
    
    #region Input System Initialization
    private void InitializeInputSystem()
    {
        if (inputActionAsset == null)
        {
            Debug.LogError($"[{ProviderName}] Input Action Asset is not assigned!");
            return;
        }
        
        // Get action maps
        gameplayActionMap = inputActionAsset.FindActionMap(gameplayActionMapName);
        menuActionMap = inputActionAsset.FindActionMap(menuActionMapName);
        
        if (gameplayActionMap == null)
        {
            Debug.LogError($"[{ProviderName}] Gameplay action map '{gameplayActionMapName}' not found!");
            return;
        }
        
        // Cache input actions
        CacheInputActions();
        
        // Setup input device callbacks
        SetupDeviceCallbacks();
        
        // Initialize input data
        currentInputData = InputData.CreateEmpty();
        previousInputData = InputData.CreateEmpty();
        
        isInitialized = true;
        Debug.Log($"[{ProviderName}] Input system initialized successfully");
    }
    
    private void CacheInputActions()
    {
        // Movement actions
        moveAction = gameplayActionMap.FindAction("Move");
        lookAction = gameplayActionMap.FindAction("Look");
        jumpAction = gameplayActionMap.FindAction("Jump");
        crouchAction = gameplayActionMap.FindAction("Crouch");
        sprintAction = gameplayActionMap.FindAction("Sprint");
        
        // Combat actions
        fireAction = gameplayActionMap.FindAction("Fire");
        secondaryFireAction = gameplayActionMap.FindAction("SecondaryFire");
        reloadAction = gameplayActionMap.FindAction("Reload");
        meleeAction = gameplayActionMap.FindAction("Melee");
        throwGrenadeAction = gameplayActionMap.FindAction("ThrowGrenade");
        
        // Interaction actions
        interactAction = gameplayActionMap.FindAction("Interact");
        flashlightAction = gameplayActionMap.FindAction("Flashlight");
        menuAction = gameplayActionMap.FindAction("Menu");
        
        // Weapon actions
        weaponSwitchAction = gameplayActionMap.FindAction("WeaponSwitch");
        scrollWheelAction = gameplayActionMap.FindAction("ScrollWheel");
        
        // Analog actions
        leftTriggerAction = gameplayActionMap.FindAction("LeftTrigger");
        rightTriggerAction = gameplayActionMap.FindAction("RightTrigger");
        leanAction = gameplayActionMap.FindAction("Lean");
    }
    
    private void SetupDeviceCallbacks()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }
    
    private void CleanupInputSystem()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        StopHapticFeedback();
    }
    
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is Gamepad gamepad)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    currentGamepad = gamepad;
                    Debug.Log($"[{ProviderName}] Gamepad connected: {gamepad.displayName}");
                    break;
                    
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    if (currentGamepad == gamepad)
                    {
                        currentGamepad = null;
                        StopHapticFeedback();
                        Debug.Log($"[{ProviderName}] Gamepad disconnected: {gamepad.displayName}");
                    }
                    break;
            }
        }
    }
    #endregion
    
    #region Action Map Management
    public void SetActionMap(string actionMapName)
    {
        InputActionMap newActionMap = null;
        
        if (actionMapName == gameplayActionMapName)
            newActionMap = gameplayActionMap;
        else if (actionMapName == menuActionMapName)
            newActionMap = menuActionMap;
        
        if (newActionMap == null)
        {
            Debug.LogWarning($"[{ProviderName}] Action map '{actionMapName}' not found!");
            return;
        }
        
        // Disable current action map
        if (currentActionMap != null && currentActionMap.enabled)
            currentActionMap.Disable();
        
        // Enable new action map
        currentActionMap = newActionMap;
        if (IsEnabled)
            currentActionMap.Enable();
        
        Debug.Log($"[{ProviderName}] Switched to action map: {actionMapName}");
    }
    
    public void SwitchToGameplay()
    {
        SetActionMap(gameplayActionMapName);
    }
    
    public void SwitchToMenu()
    {
        SetActionMap(menuActionMapName);
    }
    #endregion
    
    #region Input Processing
    private void UpdateMovementInput()
    {
        // Movement input
        if (moveAction != null)
            currentInputData.movementInput = moveAction.ReadValue<Vector2>();
        
        // Jump input
        if (jumpAction != null)
        {
            currentInputData.jump = jumpAction.IsPressed();
            currentInputData.jumpPressed = jumpAction.WasPressedThisFrame();
            currentInputData.jumpReleased = jumpAction.WasReleasedThisFrame();
        }
        
        // Crouch input (handle hold vs toggle)
        if (crouchAction != null)
        {
            bool crouchInput = crouchAction.IsPressed();
            bool crouchPressed = crouchAction.WasPressedThisFrame();
            
            if (inputSettings.crouchToggle)
            {
                if (crouchPressed)
                    crouchToggleState = !crouchToggleState;
                currentInputData.crouch = crouchToggleState;
            }
            else
            {
                currentInputData.crouch = crouchInput;
            }
            
            currentInputData.crouchPressed = crouchPressed;
            currentInputData.crouchReleased = crouchAction.WasReleasedThisFrame();
        }
        
        // Sprint input (handle hold vs toggle)
        if (sprintAction != null)
        {
            bool sprintInput = sprintAction.IsPressed();
            bool sprintPressed = sprintAction.WasPressedThisFrame();
            
            if (inputSettings.sprintToggle)
            {
                if (sprintPressed)
                    sprintToggleState = !sprintToggleState;
                currentInputData.sprint = sprintToggleState;
            }
            else
            {
                currentInputData.sprint = sprintInput;
            }
            
            currentInputData.sprintPressed = sprintPressed;
            currentInputData.sprintReleased = sprintAction.WasReleasedThisFrame();
        }
    }
    
    private void UpdateLookInput()
    {
        if (lookAction != null)
        {
            Vector2 lookDelta = lookAction.ReadValue<Vector2>();
            
            // Apply sensitivity
            lookDelta.x *= inputSettings.mouseSensitivity;
            lookDelta.y *= inputSettings.mouseSensitivity;
            
            // Apply invert Y
            if (inputSettings.invertY)
                lookDelta.y = -lookDelta.y;
            
            currentInputData.lookInput = lookDelta;
            currentInputData.lookSensitivity = inputSettings.mouseSensitivity;
        }
    }
    
    private void UpdateActionInput()
    {
        // Fire input (handle hold vs toggle for ADS)
        if (fireAction != null)
        {
            currentInputData.fire = fireAction.IsPressed();
            currentInputData.firePressed = fireAction.WasPressedThisFrame();
            currentInputData.fireReleased = fireAction.WasReleasedThisFrame();
        }
        
        // Secondary fire input (ADS - handle hold vs toggle)
        if (secondaryFireAction != null)
        {
            bool adsInput = secondaryFireAction.IsPressed();
            bool adsPressed = secondaryFireAction.WasPressedThisFrame();
            
            if (inputSettings.adsToggle)
            {
                if (adsPressed)
                    adsToggleState = !adsToggleState;
                currentInputData.secondaryFire = adsToggleState;
            }
            else
            {
                currentInputData.secondaryFire = adsInput;
            }
            
            currentInputData.secondaryFirePressed = adsPressed;
            currentInputData.secondaryFireReleased = secondaryFireAction.WasReleasedThisFrame();
        }
        
        // Reload input
        if (reloadAction != null)
        {
            currentInputData.reload = reloadAction.IsPressed();
            currentInputData.reloadPressed = reloadAction.WasPressedThisFrame();
        }
        
        // Interact input
        if (interactAction != null)
        {
            currentInputData.interact = interactAction.IsPressed();
            currentInputData.interactPressed = interactAction.WasPressedThisFrame();
        }
        
        // Melee input
        if (meleeAction != null)
        {
            currentInputData.melee = meleeAction.IsPressed();
            currentInputData.meleePressed = meleeAction.WasPressedThisFrame();
        }
        
        // Weapon switch input
        if (weaponSwitchAction != null)
        {
            float weaponSwitch = weaponSwitchAction.ReadValue<float>();
            currentInputData.weaponSwitchUp = weaponSwitch > 0.5f;
            currentInputData.weaponSwitchDown = weaponSwitch < -0.5f;
        }
        
        // Grenade input
        if (throwGrenadeAction != null)
        {
            currentInputData.throwGrenade = throwGrenadeAction.IsPressed();
            currentInputData.throwGrenadePressed = throwGrenadeAction.WasPressedThisFrame();
        }
        
        // Flashlight input
        if (flashlightAction != null)
        {
            currentInputData.flashlightPressed = flashlightAction.WasPressedThisFrame();
        }
        
        // Menu input
        if (menuAction != null)
        {
            currentInputData.menuPressed = menuAction.WasPressedThisFrame();
        }
        
        // Scroll wheel input
        if (scrollWheelAction != null)
        {
            currentInputData.scrollWheelDelta = scrollWheelAction.ReadValue<float>();
        }
    }
    
    private void UpdateAnalogInput()
    {
        // Trigger inputs
        if (leftTriggerAction != null)
            currentInputData.leftTrigger = leftTriggerAction.ReadValue<float>();
        
        if (rightTriggerAction != null)
            currentInputData.rightTrigger = rightTriggerAction.ReadValue<float>();
        
        // Lean input
        if (leanAction != null)
            currentInputData.leanInput = leanAction.ReadValue<float>();
        
        // Walk/Run modifier (based on sprint state)
        currentInputData.walkRunModifier = currentInputData.sprint ? 1f : inputSettings.walkSpeed;
    }
    
    private void ApplyInputSettings()
    {
        // Apply deadzone to movement input
        if (currentInputData.movementInput.magnitude < inputSettings.movementDeadzone)
            currentInputData.movementInput = Vector2.zero;
        
        // Apply deadzone to look input
        if (currentInputData.lookInput.magnitude < inputSettings.lookDeadzone)
            currentInputData.lookInput = Vector2.zero;
        
        // Apply deadzone to lean input
        if (Mathf.Abs(currentInputData.leanInput) < inputSettings.leanDeadzone)
            currentInputData.leanInput = 0f;
        
        // Apply trigger deadzones
        if (currentInputData.leftTrigger < inputSettings.triggerDeadzone)
            currentInputData.leftTrigger = 0f;
        
        if (currentInputData.rightTrigger < inputSettings.triggerDeadzone)
            currentInputData.rightTrigger = 0f;
    }
    #endregion
    
    #region Input Buffering
    private void ProcessInputBuffering()
    {
        // Update existing buffers
        var keysToRemove = new List<string>();
        foreach (var kvp in inputBuffers)
        {
            kvp.Value.timeRemaining -= Time.unscaledDeltaTime;
            if (kvp.Value.timeRemaining <= 0f)
                keysToRemove.Add(kvp.Key);
        }
        
        // Remove expired buffers
        foreach (var key in keysToRemove)
            inputBuffers.Remove(key);
        
        // Add new input events to buffer
        if (currentInputData.jumpPressed)
            AddInputToBuffer("jump", currentInputData.jumpPressed);
        
        if (currentInputData.firePressed)
            AddInputToBuffer("fire", currentInputData.firePressed);
        
        if (currentInputData.reloadPressed)
            AddInputToBuffer("reload", currentInputData.reloadPressed);
        
        if (currentInputData.interactPressed)
            AddInputToBuffer("interact", currentInputData.interactPressed);
        
        if (currentInputData.meleePressed)
            AddInputToBuffer("melee", currentInputData.meleePressed);
        
        // Apply buffered inputs to current frame
        ApplyBufferedInputs();
    }
    
    private void AddInputToBuffer(string inputName, bool inputValue)
    {
        if (inputValue)
        {
            inputBuffers[inputName] = new InputBuffer
            {
                inputValue = inputValue,
                timeRemaining = inputBufferTime
            };
        }
    }
    
    private void ApplyBufferedInputs()
    {
        // Apply buffered inputs if they haven't been consumed
        if (inputBuffers.ContainsKey("jump"))
            currentInputData.jumpPressed = true;
        
        if (inputBuffers.ContainsKey("fire"))
            currentInputData.firePressed = true;
        
        if (inputBuffers.ContainsKey("reload"))
            currentInputData.reloadPressed = true;
        
        if (inputBuffers.ContainsKey("interact"))
            currentInputData.interactPressed = true;
        
        if (inputBuffers.ContainsKey("melee"))
            currentInputData.meleePressed = true;
    }
    
    public void ConsumeBufferedInput(string inputName)
    {
        if (inputBuffers.ContainsKey(inputName))
            inputBuffers.Remove(inputName);
    }
    #endregion
    
    #region Haptic Feedback
    private System.Collections.IEnumerator HapticFeedbackCoroutine(float intensity, float duration)
    {
        if (currentGamepad == null)
            yield break;
        
        float clampedIntensity = Mathf.Clamp01(intensity);
        currentGamepad.SetMotorSpeeds(clampedIntensity, clampedIntensity);
        
        yield return new WaitForSecondsRealtime(duration);
        
        currentGamepad.SetMotorSpeeds(0f, 0f);
        hapticCoroutine = null;
    }
    #endregion
    
    #region Debug
    private void DebugInputData()
    {
        if (!debugInput)
            return;
        
        var debugText = $"[{ProviderName}] Input Debug:\n";
        debugText += $"Movement: {currentInputData.movementInput}\n";
        debugText += $"Look: {currentInputData.lookInput}\n";
        debugText += $"Jump: {currentInputData.jump} (Pressed: {currentInputData.jumpPressed})\n";
        debugText += $"Sprint: {currentInputData.sprint} (Pressed: {currentInputData.sprintPressed})\n";
        debugText += $"Fire: {currentInputData.fire} (Pressed: {currentInputData.firePressed})\n";
        debugText += $"Active Input: {HasActiveInput}";
        
        Debug.Log(debugText);
    }
    #endregion
}

#region Supporting Classes
/// <summary>
/// Configuration settings for input handling
/// </summary>
[System.Serializable]
public class InputSettings
{
    [Header("Sensitivity")]
    public float mouseSensitivity = 1f;
    public bool invertY = false;
    
    [Header("Deadzones")]
    [Range(0f, 0.5f)] public float movementDeadzone = 0.1f;
    [Range(0f, 0.5f)] public float lookDeadzone = 0.05f;
    [Range(0f, 0.5f)] public float leanDeadzone = 0.1f;
    [Range(0f, 0.5f)] public float triggerDeadzone = 0.1f;
    
    [Header("Toggle Settings")]
    public bool crouchToggle = false;
    public bool sprintToggle = false;
    public bool adsToggle = false;
    
    [Header("Movement")]
    [Range(0.1f, 1f)] public float walkSpeed = 0.5f;
}

/// <summary>
/// Represents a buffered input event
/// </summary>
[System.Serializable]
public class InputBuffer
{
    public bool inputValue;
    public float timeRemaining;
}
#endregion

