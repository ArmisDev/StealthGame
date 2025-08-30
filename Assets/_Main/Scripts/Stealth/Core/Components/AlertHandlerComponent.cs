using UnityEngine;

namespace StealthSystem
{
    /// <summary>
    /// Component for entities that need to respond to alerts but aren't necessarily detectors
    /// (e.g., doors that lock, lights that change, environmental elements)
    /// </summary>
    public class AlertHandlerComponent : MonoBehaviour, IAlertReceiver, IZoneEntity
    {
        [Header("Alert Response Settings")]
        [SerializeField] private string zoneId = "DefaultZone";
        [SerializeField] private bool respondToGlobalAlerts = true;
        [SerializeField] private bool respondToZoneAlerts = true;
        [SerializeField] private bool respondToBehaviorChanges = false;
        
        [Header("Alert Level Responses")]
        [SerializeField] private AlertResponse[] alertResponses = new AlertResponse[]
        {
            new AlertResponse { level = AlertLevel.Green, delay = 0f },
            new AlertResponse { level = AlertLevel.Suspicious, delay = 0f },
            new AlertResponse { level = AlertLevel.Orange, delay = 1f },
            new AlertResponse { level = AlertLevel.Red, delay = 0f }
        };
        
        [Header("Debug")]
        [SerializeField] private bool debugAlertHandler = false;
        
        // Current state
        private AlertLevel currentAlertLevel = AlertLevel.Green;
        private PlayerBehavior currentPlayerBehavior = PlayerBehavior.Stealth;
        
        #region IAlertReceiver Implementation
        public virtual void OnAlertLevelChanged(AlertLevel newLevel, AlertLevel previousLevel, string zoneId)
        {
            if (!ShouldRespondToZoneAlert(zoneId)) return;
            
            currentAlertLevel = newLevel;
            
            // Find the response for this alert level
            AlertResponse response = GetResponseForLevel(newLevel);
            if (response != null)
            {
                if (response.delay > 0f)
                {
                    Invoke(nameof(ExecuteDelayedResponse), response.delay);
                }
                else
                {
                    ExecuteAlertResponse(newLevel, previousLevel);
                }
            }
            
            if (debugAlertHandler)
                Debug.Log($"[AlertHandler] {gameObject.name} responding to alert: {previousLevel} -> {newLevel} in zone {zoneId}");
        }
        
        public virtual void OnDetectionInZone(DetectionData detectionData)
        {
            if (!respondToZoneAlerts || detectionData.zoneId != this.zoneId) return;
            
            OnDetectionResponse(detectionData);
            
            if (debugAlertHandler)
                Debug.Log($"[AlertHandler] {gameObject.name} responding to detection in zone {detectionData.zoneId}");
        }
        
        public virtual void OnPlayerBehaviorChanged(PlayerBehavior newBehavior, PlayerBehavior previousBehavior)
        {
            if (!respondToBehaviorChanges) return;
            
            currentPlayerBehavior = newBehavior;
            OnBehaviorResponse(newBehavior, previousBehavior);
            
            if (debugAlertHandler)
                Debug.Log($"[AlertHandler] {gameObject.name} responding to behavior change: {previousBehavior} -> {newBehavior}");
        }
        #endregion
        
        #region IZoneEntity Implementation
        public string ZoneId 
        { 
            get => zoneId; 
            set => zoneId = value; 
        }
        
        public virtual bool IsMultiZone => false;
        
        public virtual void OnZoneEntered(string newZoneId)
        {
            zoneId = newZoneId;
        }
        
        public virtual void OnZoneExited(string oldZoneId)
        {
            // Override if needed
        }
        
        public virtual string[] GetCurrentZones()
        {
            return new string[] { zoneId };
        }
        #endregion
        
        #region Unity Lifecycle
        protected virtual void Start()
        {
            RegisterWithSystems();
        }
        
        protected virtual void OnDestroy()
        {
            UnregisterFromSystems();
        }
        #endregion
        
        #region Alert Response Logic
        protected virtual bool ShouldRespondToZoneAlert(string alertZoneId)
        {
            if (respondToGlobalAlerts && string.IsNullOrEmpty(alertZoneId))
                return true;
                
            if (respondToZoneAlerts && alertZoneId == this.zoneId)
                return true;
                
            return false;
        }
        
        protected virtual AlertResponse GetResponseForLevel(AlertLevel level)
        {
            foreach (var response in alertResponses)
            {
                if (response.level == level)
                    return response;
            }
            return null;
        }
        
        protected virtual void ExecuteDelayedResponse()
        {
            ExecuteAlertResponse(currentAlertLevel, AlertLevel.Green);
        }
        
        /// <summary>
        /// Override this method to implement specific alert responses
        /// </summary>
        protected virtual void ExecuteAlertResponse(AlertLevel newLevel, AlertLevel previousLevel)
        {
            // Default implementation - log the response
            Debug.Log($"[AlertHandler] {gameObject.name} executing response for alert level: {newLevel}");
        }
        
        /// <summary>
        /// Override this method to respond to detections in the zone
        /// </summary>
        protected virtual void OnDetectionResponse(DetectionData detectionData)
        {
            // Default implementation - could trigger effects based on detection source
        }
        
        /// <summary>
        /// Override this method to respond to player behavior changes
        /// </summary>
        protected virtual void OnBehaviorResponse(PlayerBehavior newBehavior, PlayerBehavior previousBehavior)
        {
            // Default implementation - could change responses based on player aggression
        }
        #endregion
        
        #region System Registration
        protected virtual void RegisterWithSystems()
        {
            if (ZoneManager.Instance != null)
            {
                ZoneManager.Instance.RegisterEntity(this);
            }
            
            if (AlertSystem.Instance != null)
            {
                AlertSystem.Instance.RegisterAlertReceiver(this);
            }
        }
        
        protected virtual void UnregisterFromSystems()
        {
            if (ZoneManager.Instance != null)
            {
                ZoneManager.Instance.UnregisterEntity(this);
            }
            
            if (AlertSystem.Instance != null)
            {
                AlertSystem.Instance.UnregisterAlertReceiver(this);
            }
        }
        #endregion
        
        #region Public Utility Methods
        /// <summary>
        /// Get the current alert level this handler is responding to
        /// </summary>
        public AlertLevel GetCurrentAlertLevel()
        {
            return currentAlertLevel;
        }
        
        /// <summary>
        /// Get the current player behavior this handler is tracking
        /// </summary>
        public PlayerBehavior GetCurrentPlayerBehavior()
        {
            return currentPlayerBehavior;
        }
        
        /// <summary>
        /// Manually trigger an alert response (for testing or special cases)
        /// </summary>
        public void TriggerAlertResponse(AlertLevel level)
        {
            ExecuteAlertResponse(level, currentAlertLevel);
            currentAlertLevel = level;
        }
        #endregion
    }
    
    /// <summary>
    /// Configuration for how this handler responds to different alert levels
    /// </summary>
    [System.Serializable]
    public class AlertResponse
    {
        [Header("Response Settings")]
        public AlertLevel level = AlertLevel.Green;
        public float delay = 0f;
        public string description = "";
        
        [Header("Response Flags")]
        public bool enableResponse = true;
        public bool repeatResponse = false;
        public float repeatInterval = 1f;
    }
}