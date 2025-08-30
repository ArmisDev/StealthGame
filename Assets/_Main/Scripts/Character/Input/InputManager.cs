using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Central manager for all input providers. Handles provider switching, input aggregation,
/// and global input state management. Acts as the single point of control for input routing.
/// </summary>
public class InputManager : MonoBehaviour
{
    #region Singleton
    private static InputManager instance;
    
    /// <summary>
    /// Gets the InputManager instance. Returns null if no instance exists.
    /// Use CreateInstance() to create one if needed.
    /// </summary>
    public static InputManager Instance => instance;
    
    /// <summary>
    /// Creates an InputManager instance if one doesn't exist
    /// </summary>
    public static InputManager CreateInstance()
    {
        if (instance == null)
        {
            instance = FindAnyObjectByType<InputManager>();
            if (instance == null)
            {
                GameObject go = new GameObject("InputManager");
                instance = go.AddComponent<InputManager>();
                DontDestroyOnLoad(go);
            }
        }
        return instance;
    }
    
    /// <summary>
    /// Checks if an InputManager instance exists
    /// </summary>
    public static bool HasInstance => instance != null;
    #endregion
    
    #region Serialized Fields
    [Header("Input Providers")]
    [SerializeField] private List<InputProviderEntry> inputProviders = new List<InputProviderEntry>();
    
    [Header("Settings")]
    [SerializeField] private bool enableInputAggregation = false;
    [SerializeField] private bool pauseInputOnGamePause = true;
    [SerializeField] private bool debugInputManager = false;
    
    [Header("Input States")]
    [SerializeField] private bool globalInputEnabled = true;
    [SerializeField] private bool gameplayInputEnabled = true;
    [SerializeField] private bool menuInputEnabled = true;
    #endregion
    
    #region Private Fields
    private IInputProvider activeProvider;
    private List<IInputProvider> enabledProviders = new List<IInputProvider>();
    private InputData currentInputData;
    private InputData previousInputData;
    
    // Input state tracking
    private Dictionary<string, bool> inputStates = new Dictionary<string, bool>();
    private bool isPaused = false;
    
    // Provider management
    private Dictionary<string, IInputProvider> providerRegistry = new Dictionary<string, IInputProvider>();
    #endregion
    
    #region Events
    /// <summary>Event fired when input provider changes</summary>
    public static event Action<IInputProvider, IInputProvider> OnProviderChanged;
    
    /// <summary>Event fired when input is enabled/disabled globally</summary>
    public static event Action<bool> OnGlobalInputStateChanged;
    
    /// <summary>Event fired when gameplay input is enabled/disabled</summary>
    public static event Action<bool> OnGameplayInputStateChanged;
    
    /// <summary>Event fired when menu input is enabled/disabled</summary>
    public static event Action<bool> OnMenuInputStateChanged;
    
    /// <summary>Event fired when pause state changes</summary>
    public static event Action<bool> OnPauseStateChanged;
    
    /// <summary>Event fired every frame with current input data</summary>
    public static event Action<InputData> OnInputUpdated;
    
    /// <summary>Event fired when specific input events occur</summary>
    public static event Action<string, bool> OnInputEvent;
    #endregion
    
    #region Properties
    /// <summary>Current active input provider</summary>
    public IInputProvider ActiveProvider => activeProvider;
    
    /// <summary>Current aggregated input data</summary>
    public InputData CurrentInput => currentInputData;
    
    /// <summary>Previous frame's input data</summary>
    public InputData PreviousInput => previousInputData;
    
    /// <summary>Whether global input is enabled</summary>
    public bool GlobalInputEnabled 
    { 
        get => globalInputEnabled; 
        set => SetGlobalInputEnabled(value);
    }
    
    /// <summary>Whether gameplay input is enabled</summary>
    public bool GameplayInputEnabled 
    { 
        get => gameplayInputEnabled; 
        set => SetGameplayInputEnabled(value);
    }
    
    /// <summary>Whether menu input is enabled</summary>
    public bool MenuInputEnabled 
    { 
        get => menuInputEnabled; 
        set => SetMenuInputEnabled(value);
    }
    
    /// <summary>Whether the game is currently paused</summary>
    public bool IsPaused => isPaused;
    
    /// <summary>Number of registered input providers</summary>
    public int ProviderCount => providerRegistry.Count;
    
    /// <summary>List of all registered provider names</summary>
    public IEnumerable<string> RegisteredProviders => providerRegistry.Keys;
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton setup
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeInputManager();
    }
    
    private void Start()
    {
        RegisterSerializedProviders();
        SetupDefaultProvider();
    }
    
    private void Update()
    {
        UpdateInputProviders();
        ProcessInput();
        BroadcastInputEvents();
        
        if (debugInputManager)
            DebugInputState();
    }
    
    private void OnDestroy()
    {
        if (instance == this)
        {
            CleanupInputManager();
            CleanupEvents();
            instance = null;
        }
    }
    #endregion
    
    #region Initialization
    private void InitializeInputManager()
    {
        currentInputData = InputData.CreateEmpty();
        previousInputData = InputData.CreateEmpty();
        
        // Subscribe to game pause events if available
        // Note: You might want to connect this to your game's pause system
        
        Debug.Log("[InputManager] Input Manager initialized");
    }
    
    private void RegisterSerializedProviders()
    {
        foreach (var entry in inputProviders)
        {
            if (entry.IsValid)
            {
                RegisterProvider(entry.name, entry.Provider);
                
                if (entry.isDefault && activeProvider == null)
                {
                    SetActiveProvider(entry.name);
                }
            }
            else
            {
                Debug.LogWarning($"[InputManager] Invalid provider entry: {entry.name}");
            }
        }
    }
    
    private void SetupDefaultProvider()
    {
        if (activeProvider == null && providerRegistry.Count > 0)
        {
            // Find the highest priority provider
            var defaultProvider = providerRegistry.Values
                .OrderByDescending(p => p.Priority)
                .FirstOrDefault();
            
            if (defaultProvider != null)
            {
                SetActiveProvider(defaultProvider);
            }
        }
    }
    
    private void CleanupInputManager()
    {
        // Stop any running coroutines
        StopAllCoroutines();
        
        // Disable all providers
        foreach (var provider in providerRegistry.Values)
        {
            if (provider != null)
            {
                provider.IsEnabled = false;
                provider.OnProviderDisabled();
            }
        }
        
        providerRegistry.Clear();
        enabledProviders.Clear();
        activeProvider = null;
    }
    
    private void CleanupEvents()
    {
        // Clear all event subscriptions to prevent memory leaks
        OnProviderChanged = null;
        OnGlobalInputStateChanged = null;
        OnGameplayInputStateChanged = null;
        OnMenuInputStateChanged = null;
        OnPauseStateChanged = null;
        OnInputUpdated = null;
        OnInputEvent = null;
    }
    #endregion
    
    #region Provider Management
    /// <summary>
    /// Registers an input provider with the manager
    /// </summary>
    public void RegisterProvider(string name, IInputProvider provider)
    {
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogError("[InputManager] Cannot register provider with null or empty name");
            return;
        }
        
        if (provider == null)
        {
            Debug.LogError($"[InputManager] Cannot register null provider '{name}'");
            return;
        }
        
        if (providerRegistry.ContainsKey(name))
        {
            Debug.LogWarning($"[InputManager] Provider '{name}' already registered. Replacing...");
        }
        
        providerRegistry[name] = provider;
        Debug.Log($"[InputManager] Registered input provider: {name} (Priority: {provider.Priority})");
    }
    
    /// <summary>
    /// Unregisters an input provider
    /// </summary>
    public void UnregisterProvider(string name)
    {
        if (providerRegistry.ContainsKey(name))
        {
            var provider = providerRegistry[name];
            
            // If this was the active provider, switch to another
            if (activeProvider == provider)
            {
                SetActiveProvider((IInputProvider)null);
                FindAndSetBestProvider();
            }
            
            // Remove from enabled providers
            enabledProviders.Remove(provider);
            
            // Disable the provider
            provider.IsEnabled = false;
            
            providerRegistry.Remove(name);
            Debug.Log($"[InputManager] Unregistered input provider: {name}");
        }
    }
    
    /// <summary>
    /// Sets the active input provider by name
    /// </summary>
    public bool SetActiveProvider(string providerName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            SetActiveProvider((IInputProvider)null);
            return true;
        }
        
        if (providerRegistry.TryGetValue(providerName, out IInputProvider provider))
        {
            SetActiveProvider(provider);
            return true;
        }
        
        Debug.LogWarning($"[InputManager] Provider '{providerName}' not found");
        return false;
    }
    
    /// <summary>
    /// Sets the active input provider directly
    /// </summary>
    public void SetActiveProvider(IInputProvider provider)
    {
        var previousProvider = activeProvider;
        
        // Disable previous provider
        if (previousProvider != null)
        {
            previousProvider.IsEnabled = false;
            previousProvider.OnProviderDisabled();
        }
        
        // Set new provider
        activeProvider = provider;
        
        // Enable new provider
        if (activeProvider != null && globalInputEnabled)
        {
            activeProvider.IsEnabled = true;
            activeProvider.OnProviderEnabled();
        }
        
        // Fire event
        OnProviderChanged?.Invoke(previousProvider, activeProvider);
        
        string providerName = activeProvider?.ProviderName ?? "None";
        Debug.Log($"[InputManager] Active provider changed to: {providerName}");
    }
    
    /// <summary>
    /// Gets a registered provider by name
    /// </summary>
    public IInputProvider GetProvider(string name)
    {
        providerRegistry.TryGetValue(name, out IInputProvider provider);
        return provider;
    }
    
    /// <summary>
    /// Enables/disables a specific provider
    /// </summary>
    public void SetProviderEnabled(string name, bool enabled)
    {
        if (providerRegistry.TryGetValue(name, out IInputProvider provider))
        {
            provider.IsEnabled = enabled && globalInputEnabled;
            
            if (enabled && !enabledProviders.Contains(provider))
                enabledProviders.Add(provider);
            else if (!enabled)
                enabledProviders.Remove(provider);
        }
    }
    
    /// <summary>
    /// Finds and sets the best available provider based on priority
    /// </summary>
    private void FindAndSetBestProvider()
    {
        var bestProvider = providerRegistry.Values
            .Where(p => p.IsAvailable)
            .OrderByDescending(p => p.Priority)
            .FirstOrDefault();
        
        if (bestProvider != null)
        {
            SetActiveProvider(bestProvider);
        }
    }
    
    /// <summary>
    /// Switches to a provider temporarily, storing the previous one
    /// </summary>
    public void PushProvider(string providerName)
    {
        // Implementation for provider stack if needed
        // This would allow temporary provider switches (e.g., for cutscenes)
        SetActiveProvider(providerName);
    }
    
    /// <summary>
    /// Returns to the previous provider
    /// </summary>
    public void PopProvider()
    {
        // Implementation for provider stack if needed
        FindAndSetBestProvider();
    }
    #endregion
    
    #region Input Processing
    private void UpdateInputProviders()
    {
        if (!globalInputEnabled || isPaused)
            return;
        
        // Update active provider
        if (activeProvider != null && activeProvider.IsEnabled && activeProvider.IsAvailable)
        {
            activeProvider.UpdateInput();
        }
        
        // Update additional enabled providers if aggregation is enabled
        if (enableInputAggregation)
        {
            foreach (var provider in enabledProviders)
            {
                if (provider != activeProvider && provider.IsEnabled && provider.IsAvailable)
                {
                    provider.UpdateInput();
                }
            }
        }
    }
    
    private void ProcessInput()
    {
        // Store previous frame data
        previousInputData = currentInputData;
        
        if (!globalInputEnabled || isPaused)
        {
            currentInputData = InputData.CreateEmpty();
            return;
        }
        
        if (enableInputAggregation)
        {
            currentInputData = AggregateInputFromProviders();
        }
        else
        {
            currentInputData = GetInputFromActiveProvider();
        }
        
        // Apply global input filters
        ApplyInputFilters();
    }
    
    private InputData GetInputFromActiveProvider()
    {
        if (activeProvider != null && activeProvider.IsEnabled && activeProvider.IsAvailable)
        {
            return activeProvider.GetInputData();
        }
        
        return InputData.CreateEmpty();
    }
    
    private InputData AggregateInputFromProviders()
    {
        var aggregatedInput = InputData.CreateEmpty();
        var providersToProcess = new List<IInputProvider>();
        
        // Add active provider first (highest priority)
        if (activeProvider != null && activeProvider.IsEnabled && activeProvider.IsAvailable)
        {
            providersToProcess.Add(activeProvider);
        }
        
        // Add other enabled providers sorted by priority
        var otherProviders = enabledProviders
            .Where(p => p != activeProvider && p.IsEnabled && p.IsAvailable)
            .OrderByDescending(p => p.Priority);
        
        providersToProcess.AddRange(otherProviders);
        
        // Aggregate input data
        foreach (var provider in providersToProcess)
        {
            var providerInput = provider.GetInputData();
            aggregatedInput = MergeInputData(aggregatedInput, providerInput);
        }
        
        return aggregatedInput;
    }
    
    private InputData MergeInputData(InputData baseInput, InputData newInput)
    {
        var merged = baseInput;
        
        // Movement input - use the one with higher magnitude
        if (newInput.movementInput.sqrMagnitude > merged.movementInput.sqrMagnitude)
            merged.movementInput = newInput.movementInput;
        
        // Look input - additive
        merged.lookInput += newInput.lookInput;
        
        // Boolean inputs - OR operation (any provider can trigger)
        merged.jump |= newInput.jump;
        merged.jumpPressed |= newInput.jumpPressed;
        merged.jumpReleased |= newInput.jumpReleased;
        
        merged.crouch |= newInput.crouch;
        merged.crouchPressed |= newInput.crouchPressed;
        merged.crouchReleased |= newInput.crouchReleased;
        
        merged.sprint |= newInput.sprint;
        merged.sprintPressed |= newInput.sprintPressed;
        merged.sprintReleased |= newInput.sprintReleased;
        
        merged.fire |= newInput.fire;
        merged.firePressed |= newInput.firePressed;
        merged.fireReleased |= newInput.fireReleased;
        
        merged.secondaryFire |= newInput.secondaryFire;
        merged.secondaryFirePressed |= newInput.secondaryFirePressed;
        merged.secondaryFireReleased |= newInput.secondaryFireReleased;
        
        merged.reload |= newInput.reload;
        merged.reloadPressed |= newInput.reloadPressed;
        
        merged.interact |= newInput.interact;
        merged.interactPressed |= newInput.interactPressed;
        
        merged.melee |= newInput.melee;
        merged.meleePressed |= newInput.meleePressed;
        
        merged.weaponSwitchUp |= newInput.weaponSwitchUp;
        merged.weaponSwitchDown |= newInput.weaponSwitchDown;
        
        merged.throwGrenade |= newInput.throwGrenade;
        merged.throwGrenadePressed |= newInput.throwGrenadePressed;
        
        merged.flashlightPressed |= newInput.flashlightPressed;
        merged.menuPressed |= newInput.menuPressed;
        
        // Analog inputs - use maximum values
        merged.leftTrigger = Mathf.Max(merged.leftTrigger, newInput.leftTrigger);
        merged.rightTrigger = Mathf.Max(merged.rightTrigger, newInput.rightTrigger);
        merged.leanInput = Mathf.Abs(newInput.leanInput) > Mathf.Abs(merged.leanInput) ? newInput.leanInput : merged.leanInput;
        merged.walkRunModifier = Mathf.Max(merged.walkRunModifier, newInput.walkRunModifier);
        merged.zoomLevel = Mathf.Max(merged.zoomLevel, newInput.zoomLevel);
        
        // Scroll wheel - additive
        merged.scrollWheelDelta += newInput.scrollWheelDelta;
        
        return merged;
    }
    
    private void ApplyInputFilters()
    {
        // Apply gameplay input filter
        if (!gameplayInputEnabled)
        {
            FilterGameplayInput();
        }
        
        // Apply menu input filter
        if (!menuInputEnabled)
        {
            FilterMenuInput();
        }
    }
    
    private void FilterGameplayInput()
    {
        // Disable gameplay-specific inputs
        currentInputData.movementInput = Vector2.zero;
        currentInputData.lookInput = Vector2.zero;
        currentInputData.jump = false;
        currentInputData.jumpPressed = false;
        currentInputData.jumpReleased = false;
        currentInputData.crouch = false;
        currentInputData.crouchPressed = false;
        currentInputData.crouchReleased = false;
        currentInputData.sprint = false;
        currentInputData.sprintPressed = false;
        currentInputData.sprintReleased = false;
        currentInputData.fire = false;
        currentInputData.firePressed = false;
        currentInputData.fireReleased = false;
        currentInputData.secondaryFire = false;
        currentInputData.secondaryFirePressed = false;
        currentInputData.secondaryFireReleased = false;
        currentInputData.reload = false;
        currentInputData.reloadPressed = false;
        currentInputData.interact = false;
        currentInputData.interactPressed = false;
        currentInputData.melee = false;
        currentInputData.meleePressed = false;
        currentInputData.weaponSwitchUp = false;
        currentInputData.weaponSwitchDown = false;
        currentInputData.throwGrenade = false;
        currentInputData.throwGrenadePressed = false;
        currentInputData.flashlightPressed = false;
        currentInputData.leftTrigger = 0f;
        currentInputData.rightTrigger = 0f;
        currentInputData.leanInput = 0f;
    }
    
    private void FilterMenuInput()
    {
        // Keep only menu-related inputs
        currentInputData.menuPressed = false;
        currentInputData.scrollWheelDelta = 0f;
    }
    #endregion
    
    #region Input State Management
    /// <summary>
    /// Sets global input enabled state
    /// </summary>
    public void SetGlobalInputEnabled(bool enabled)
    {
        if (globalInputEnabled != enabled)
        {
            globalInputEnabled = enabled;
            
            // Update all providers
            foreach (var provider in providerRegistry.Values)
            {
                provider.IsEnabled = enabled && provider.IsEnabled;
            }
            
            OnGlobalInputStateChanged?.Invoke(enabled);
            Debug.Log($"[InputManager] Global input {(enabled ? "enabled" : "disabled")}");
        }
    }
    
    /// <summary>
    /// Sets gameplay input enabled state
    /// </summary>
    public void SetGameplayInputEnabled(bool enabled)
    {
        if (gameplayInputEnabled != enabled)
        {
            gameplayInputEnabled = enabled;
            OnGameplayInputStateChanged?.Invoke(enabled);
            Debug.Log($"[InputManager] Gameplay input {(enabled ? "enabled" : "disabled")}");
        }
    }
    
    /// <summary>
    /// Sets menu input enabled state
    /// </summary>
    public void SetMenuInputEnabled(bool enabled)
    {
        if (menuInputEnabled != enabled)
        {
            menuInputEnabled = enabled;
            OnMenuInputStateChanged?.Invoke(enabled);
            Debug.Log($"[InputManager] Menu input {(enabled ? "enabled" : "disabled")}");
        }
    }
    
    /// <summary>
    /// Pauses/unpauses input processing
    /// </summary>
    public void SetPaused(bool paused)
    {
        if (isPaused != paused)
        {
            isPaused = paused;
            
            // Fire event to let other systems handle time scale management
            OnPauseStateChanged?.Invoke(paused);
            
            Debug.Log($"[InputManager] Input {(paused ? "paused" : "unpaused")}");
        }
    }
    
    /// <summary>
    /// Temporarily disables all input for a duration
    /// </summary>
    public void DisableInputTemporarily(float duration)
    {
        StartCoroutine(DisableInputCoroutine(duration));
    }
    
    private System.Collections.IEnumerator DisableInputCoroutine(float duration)
    {
        bool wasEnabled = globalInputEnabled;
        SetGlobalInputEnabled(false);
        
        yield return new WaitForSecondsRealtime(duration);
        
        SetGlobalInputEnabled(wasEnabled);
    }
    #endregion
    
    #region Input Queries
    /// <summary>
    /// Checks if any input is currently active
    /// </summary>
    public bool HasAnyInput()
    {
        return currentInputData.HasMovementInput || 
               currentInputData.HasLookInput || 
               currentInputData.HasAnyActionPressed ||
               currentInputData.HasAnyFrameEvent;
    }
    
    /// <summary>
    /// Checks if a specific input was pressed this frame
    /// </summary>
    public bool WasPressed(string inputName)
    {
        return inputName.ToLower() switch
        {
            "jump" => currentInputData.jumpPressed,
            "crouch" => currentInputData.crouchPressed,
            "sprint" => currentInputData.sprintPressed,
            "fire" => currentInputData.firePressed,
            "secondaryfire" or "ads" => currentInputData.secondaryFirePressed,
            "reload" => currentInputData.reloadPressed,
            "interact" => currentInputData.interactPressed,
            "melee" => currentInputData.meleePressed,
            "grenade" => currentInputData.throwGrenadePressed,
            "flashlight" => currentInputData.flashlightPressed,
            "menu" => currentInputData.menuPressed,
            _ => false
        };
    }
    
    /// <summary>
    /// Checks if a specific input is currently held
    /// </summary>
    public bool IsHeld(string inputName)
    {
        return inputName.ToLower() switch
        {
            "jump" => currentInputData.jump,
            "crouch" => currentInputData.crouch,
            "sprint" => currentInputData.sprint,
            "fire" => currentInputData.fire,
            "secondaryfire" or "ads" => currentInputData.secondaryFire,
            "reload" => currentInputData.reload,
            "interact" => currentInputData.interact,
            "melee" => currentInputData.melee,
            "grenade" => currentInputData.throwGrenade,
            _ => false
        };
    }
    
    /// <summary>
    /// Checks if a specific input was released this frame
    /// </summary>
    public bool WasReleased(string inputName)
    {
        return inputName.ToLower() switch
        {
            "jump" => currentInputData.jumpReleased,
            "crouch" => currentInputData.crouchReleased,
            "sprint" => currentInputData.sprintReleased,
            "fire" => currentInputData.fireReleased,
            "secondaryfire" or "ads" => currentInputData.secondaryFireReleased,
            _ => false
        };
    }
    
    /// <summary>
    /// Gets the current movement input
    /// </summary>
    public Vector2 GetMovementInput()
    {
        return currentInputData.movementInput;
    }
    
    /// <summary>
    /// Gets the current look input
    /// </summary>
    public Vector2 GetLookInput()
    {
        return currentInputData.lookInput;
    }
    
    /// <summary>
    /// Gets an analog input value
    /// </summary>
    public float GetAnalogInput(string inputName)
    {
        return inputName.ToLower() switch
        {
            "lefttrigger" => currentInputData.leftTrigger,
            "righttrigger" => currentInputData.rightTrigger,
            "lean" => currentInputData.leanInput,
            "walkrun" => currentInputData.walkRunModifier,
            "zoom" => currentInputData.zoomLevel,
            "scroll" => currentInputData.scrollWheelDelta,
            _ => 0f
        };
    }
    #endregion
    
    #region Event Broadcasting
    private void BroadcastInputEvents()
    {
        // Broadcast current input data
        OnInputUpdated?.Invoke(currentInputData);
        
        // Broadcast specific input events
        BroadcastInputEvent("jump", currentInputData.jumpPressed);
        BroadcastInputEvent("crouch", currentInputData.crouchPressed);
        BroadcastInputEvent("sprint", currentInputData.sprintPressed);
        BroadcastInputEvent("fire", currentInputData.firePressed);
        BroadcastInputEvent("secondaryfire", currentInputData.secondaryFirePressed);
        BroadcastInputEvent("reload", currentInputData.reloadPressed);
        BroadcastInputEvent("interact", currentInputData.interactPressed);
        BroadcastInputEvent("melee", currentInputData.meleePressed);
        BroadcastInputEvent("grenade", currentInputData.throwGrenadePressed);
        BroadcastInputEvent("flashlight", currentInputData.flashlightPressed);
        BroadcastInputEvent("menu", currentInputData.menuPressed);
    }
    
    private void BroadcastInputEvent(string inputName, bool inputValue)
    {
        if (inputValue)
        {
            OnInputEvent?.Invoke(inputName, inputValue);
        }
    }
    
    /// <summary>
    /// Triggers haptic feedback on the active provider
    /// </summary>
    public void TriggerHapticFeedback(float intensity, float duration)
    {
        if (activeProvider != null && activeProvider.SupportsHapticFeedback)
        {
            activeProvider.TriggerHapticFeedback(intensity, duration);
        }
    }
    
    /// <summary>
    /// Stops haptic feedback on the active provider
    /// </summary>
    public void StopHapticFeedback()
    {
        if (activeProvider != null && activeProvider.SupportsHapticFeedback)
        {
            activeProvider.StopHapticFeedback();
        }
    }
    #endregion
    
    #region Debug and Utilities
    private void DebugInputState()
    {
        if (!debugInputManager)
            return;
        
        var debugText = "[InputManager] Debug Info:\n";
        debugText += $"Active Provider: {activeProvider?.ProviderName ?? "None"}\n";
        debugText += $"Registered Providers: {providerRegistry.Count}\n";
        debugText += $"Global Input: {globalInputEnabled}\n";
        debugText += $"Gameplay Input: {gameplayInputEnabled}\n";
        debugText += $"Menu Input: {menuInputEnabled}\n";
        debugText += $"Is Paused: {isPaused}\n";
        debugText += $"Has Any Input: {HasAnyInput()}\n";
        debugText += $"Movement: {currentInputData.movementInput}\n";
        debugText += $"Look: {currentInputData.lookInput}\n";
        
        Debug.Log(debugText);
    }
    
    /// <summary>
    /// Gets debug information about the input manager state
    /// </summary>
    public string GetDebugInfo()
    {
        var info = $"InputManager Debug Info:\n";
        info += $"Active Provider: {activeProvider?.ProviderName ?? "None"}\n";
        info += $"Provider Count: {providerRegistry.Count}\n";
        info += $"Enabled Providers: {enabledProviders.Count}\n";
        info += $"Global Input Enabled: {globalInputEnabled}\n";
        info += $"Gameplay Input Enabled: {gameplayInputEnabled}\n";
        info += $"Menu Input Enabled: {menuInputEnabled}\n";
        info += $"Is Paused: {isPaused}\n";
        info += $"Input Aggregation: {enableInputAggregation}\n";
        info += $"Has Active Input: {HasAnyInput()}\n";
        
        if (activeProvider != null)
        {
            info += $"Active Provider Details:\n";
            info += $"  - Name: {activeProvider.ProviderName}\n";
            info += $"  - Priority: {activeProvider.Priority}\n";
            info += $"  - Available: {activeProvider.IsAvailable}\n";
            info += $"  - Enabled: {activeProvider.IsEnabled}\n";
            info += $"  - Has Active Input: {activeProvider.HasActiveInput}\n";
            info += $"  - Supports Haptic: {activeProvider.SupportsHapticFeedback}\n";
        }
        
        return info;
    }
    
    /// <summary>
    /// Logs current input state to console
    /// </summary>
    public void LogInputState()
    {
        Debug.Log(GetDebugInfo());
    }
    
    /// <summary>
    /// Resets all input providers to their default state
    /// </summary>
    public void ResetAllProviders()
    {
        foreach (var provider in providerRegistry.Values)
        {
            provider.ResetInputState();
        }
        
        currentInputData.Reset();
        previousInputData.Reset();
        
        Debug.Log("[InputManager] All providers reset");
    }
    
    /// <summary>
    /// Gets a copy of the current input data
    /// </summary>
    public InputData GetCurrentInputCopy()
    {
        return currentInputData.GetValidatedCopy();
    }
    
    /// <summary>
    /// Gets a copy of the previous frame's input data
    /// </summary>
    public InputData GetPreviousInputCopy()
    {
        return previousInputData.GetValidatedCopy();
    }
    #endregion
    
    #region Public API Convenience Methods
    /// <summary>
    /// Quick method to switch to player input
    /// </summary>
    public void SwitchToPlayerInput()
    {
        SetActiveProvider("Player");
    }
    
    /// <summary>
    /// Quick method to switch to AI input
    /// </summary>
    public void SwitchToAIInput()
    {
        SetActiveProvider("AI");
    }
    
    /// <summary>
    /// Quick method to disable all input (for cutscenes, etc.)
    /// </summary>
    public void DisableAllInput()
    {
        SetGlobalInputEnabled(false);
    }
    
    /// <summary>
    /// Quick method to enable all input
    /// </summary>
    public void EnableAllInput()
    {
        SetGlobalInputEnabled(true);
    }
    
    /// <summary>
    /// Quick method to disable gameplay input (for menus)
    /// </summary>
    public void DisableGameplayInput()
    {
        SetGameplayInputEnabled(false);
    }
    
    /// <summary>
    /// Quick method to enable gameplay input
    /// </summary>
    public void EnableGameplayInput()
    {
        SetGameplayInputEnabled(true);
    }
    #endregion
}

#region Supporting Classes
/// <summary>
/// Serializable entry for input providers in the inspector
/// </summary>
[System.Serializable]
public class InputProviderEntry
{
    [Header("Provider Info")]
    public string name = "Provider";
    [SerializeField] private MonoBehaviour providerComponent;
    public bool isDefault = false;
    
    [Header("Settings")]
    public bool enableOnStart = true;
    public int priority = 0;
    
    /// <summary>
    /// Gets the IInputProvider from the MonoBehaviour component
    /// </summary>
    public IInputProvider Provider 
    { 
        get 
        {
            if (providerComponent == null)
                return null;
                
            if (providerComponent is IInputProvider inputProvider)
                return inputProvider;
                
            Debug.LogError($"[InputProviderEntry] Component '{providerComponent.name}' does not implement IInputProvider!");
            return null;
        }
        set
        {
            if (value is MonoBehaviour mono)
                providerComponent = mono;
            else
                Debug.LogError("[InputProviderEntry] Provider must be a MonoBehaviour that implements IInputProvider!");
        }
    }
    
    /// <summary>
    /// Validates that the provider component implements IInputProvider
    /// </summary>
    public bool IsValid => providerComponent != null && providerComponent is IInputProvider;
}
#endregion