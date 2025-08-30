using UnityEngine;

namespace StealthSystem
{
    /// <summary>
    /// Interface for anything that can be detected (primarily the player)
    /// </summary>
    public interface IDetectable
    {
        #region Position & Movement
        /// <summary>Current world position</summary>
        Vector3 Position { get; }
        
        /// <summary>Current velocity for movement-based detection</summary>
        Vector3 Velocity { get; }
        
        /// <summary>Current movement speed</summary>
        float CurrentSpeed { get; }
        #endregion
        
        #region Noise Detection
        /// <summary>Current noise level (0 = silent, 1 = maximum noise)</summary>
        float NoiseLevel { get; }
        
        /// <summary>Noise detection radius based on current activity</summary>
        float NoiseRadius { get; }
        
        /// <summary>Type of noise being made (footsteps, equipment, etc.)</summary>
        string NoiseType { get; }
        #endregion
        
        #region Visual Detection
        /// <summary>How visible this target is (0 = invisible, 1 = fully visible)</summary>
        float VisibilityLevel { get; }
        
        /// <summary>Size modifier for detection (crouching = smaller target)</summary>
        float SizeModifier { get; }
        
        /// <summary>Is this target currently in shadows/dark areas?</summary>
        bool IsInShadows { get; }
        
        /// <summary>Current disguise level (0 = no disguise, 1 = perfect disguise)</summary>
        float DisguiseLevel { get; }
        #endregion
        
        #region Behavior Tracking
        /// <summary>Current behavior pattern for AI response scaling</summary>
        PlayerBehavior CurrentBehavior { get; }
        
        /// <summary>Has this target been aggressive recently?</summary>
        bool HasBeenAggressive { get; }
        
        /// <summary>Number of times this target has been detected</summary>
        int DetectionCount { get; }
        #endregion
    }
}