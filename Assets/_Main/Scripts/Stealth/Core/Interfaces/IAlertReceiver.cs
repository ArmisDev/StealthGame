using UnityEngine;

namespace StealthSystem
{
    /// <summary>
    /// Interface for entities that need to respond to alert level changes
    /// </summary>
    public interface IAlertReceiver
    {
        /// <summary>Called when the global or zone alert level changes</summary>
        void OnAlertLevelChanged(AlertLevel newLevel, AlertLevel previousLevel, string zoneId);
        
        /// <summary>Called when a detection is made in this entity's zone</summary>
        void OnDetectionInZone(DetectionData detectionData);
        
        /// <summary>Called when player behavior changes (stealth -> aggressive)</summary>
        void OnPlayerBehaviorChanged(PlayerBehavior newBehavior, PlayerBehavior previousBehavior);
    }
}