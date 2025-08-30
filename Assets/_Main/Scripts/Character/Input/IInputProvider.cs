using UnityEngine;

/// <summary>
/// Interface that defines how any input source must behave.
/// Allows multiple input sources (player, AI, network, replay) to be used interchangeably.
/// Character controller doesn't care WHERE input comes from.
/// </summary>
public interface IInputProvider
{
    #region Core Input Methods
    /// <summary>
    /// Gets the current input data for this frame.
    /// This is the primary method that consumers will call.
    /// </summary>
    /// <returns>Complete input data structure for the current frame</returns>
    InputData GetInputData();
    
    /// <summary>
    /// Updates the input provider's internal state.
    /// Should be called once per frame before GetInputData().
    /// </summary>
    void UpdateInput();
    #endregion
    
    #region State Properties
    /// <summary>
    /// Whether this input provider is currently enabled and should be processed.
    /// </summary>
    bool IsEnabled { get; set; }
    
    /// <summary>
    /// Whether this input provider is currently available for use.
    /// For example, a network input provider might not be available if disconnected.
    /// </summary>
    bool IsAvailable { get; }
    
    /// <summary>
    /// Priority of this input provider when multiple providers are active.
    /// Higher values take precedence.
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Human-readable name for this input provider (useful for debugging).
    /// </summary>
    string ProviderName { get; }
    #endregion
    
    #region Input State Queries
    /// <summary>
    /// Whether this provider currently has any active input.
    /// Useful for determining if the provider should be considered "active".
    /// </summary>
    bool HasActiveInput { get; }
    
    /// <summary>
    /// Whether this provider supports continuous input updates.
    /// Some providers (like replay) might only provide discrete input events.
    /// </summary>
    bool SupportsContinuousInput { get; }
    #endregion
    
    #region Haptic Feedback (Optional)
    /// <summary>
    /// Whether this input provider supports haptic/rumble feedback.
    /// </summary>
    bool SupportsHapticFeedback { get; }
    
    /// <summary>
    /// Triggers haptic feedback if supported.
    /// </summary>
    /// <param name="intensity">Intensity of the feedback (0-1)</param>
    /// <param name="duration">Duration of the feedback in seconds</param>
    void TriggerHapticFeedback(float intensity, float duration);
    
    /// <summary>
    /// Stops any currently playing haptic feedback.
    /// </summary>
    void StopHapticFeedback();
    #endregion
    
    #region Lifecycle Events
    /// <summary>
    /// Called when this input provider is activated/enabled.
    /// Use this for initialization that should happen when the provider becomes active.
    /// </summary>
    void OnProviderEnabled();
    
    /// <summary>
    /// Called when this input provider is deactivated/disabled.
    /// Use this for cleanup when the provider is no longer active.
    /// </summary>
    void OnProviderDisabled();
    
    /// <summary>
    /// Called when the input provider should reset its state.
    /// Useful for clearing accumulated input or resetting to defaults.
    /// </summary>
    void ResetInputState();
    #endregion
    
    #region Configuration (Optional)
    /// <summary>
    /// Applies configuration settings to this input provider.
    /// The specific type of config depends on the implementation.
    /// </summary>
    /// <param name="config">Configuration object (implementation-specific)</param>
    void ApplyConfiguration(object config);
    
    /// <summary>
    /// Gets the current configuration of this input provider.
    /// Returns null if no configuration is available.
    /// </summary>
    /// <returns>Current configuration object or null</returns>
    object GetConfiguration();
    #endregion
}
