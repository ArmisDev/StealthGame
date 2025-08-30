using UnityEngine;

namespace StealthSystem
{
    /// <summary>
    /// Interface for any entity that can detect the player (guards, cameras, sensors, etc.)
    /// </summary>
    public interface IDetector
    {
        #region Core Detection
        /// <summary>Unique identifier for this detector</summary>
        string DetectorId { get; }
        
        /// <summary>What zone this detector operates in</summary>
        string ZoneId { get; set; }
        
        /// <summary>Current detection state of this detector</summary>
        DetectionState CurrentState { get; }
        
        /// <summary>Can this detector currently detect anything?</summary>
        bool IsActive { get; set; }
        
        /// <summary>Attempts to detect the target at given position</summary>
        /// <returns>Detection strength (0 = no detection, 1 = full detection)</returns>
        float TryDetect(IDetectable target, Vector3 targetPosition);
        
        /// <summary>Called when this detector has detected something</summary>
        void OnDetectionMade(DetectionData detectionData);
        
        /// <summary>Called when detection is lost</summary>
        void OnDetectionLost();
        #endregion
        
        #region Alert Response
        /// <summary>Called when the zone alert level changes</summary>
        void OnZoneAlertChanged(AlertLevel newLevel, AlertData alertData);
        
        /// <summary>Called when another detector in the zone reports a detection</summary>
        void OnZoneDetectionReported(DetectionData detectionData);
        
        /// <summary>Updates the detector's behavior based on current alert level</summary>
        void UpdateAlertBehavior(AlertLevel alertLevel);
        #endregion
        
        #region Configuration
        /// <summary>Detection range of this detector</summary>
        float DetectionRange { get; }
        
        /// <summary>How quickly this detector can confirm a detection (suspicious -> hostile)</summary>
        float DetectionSpeed { get; }
        
        /// <summary>Can this detector communicate with others?</summary>
        bool CanCommunicate { get; }
        #endregion
    }
}