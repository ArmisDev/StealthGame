using UnityEngine;
using System.Collections;

namespace StealthSystem
{
    /// <summary>
    /// Base component that provides common detection functionality for all detector types
    /// </summary>
    public abstract class DetectorComponent : MonoBehaviour, IDetector, IZoneEntity
    {
        [Header("Detector Settings")]
        [SerializeField] protected string detectorId;
        [SerializeField] protected string zoneId = "DefaultZone";
        [SerializeField] protected float detectionRange = 10f;
        [SerializeField] protected float detectionSpeed = 2f;
        [SerializeField] protected bool canCommunicate = true;
        [SerializeField] protected bool startActive = true;
        
        [Header("Detection Timing")]
        [SerializeField] protected float suspiciousTime = 3f;
        [SerializeField] protected float searchTime = 10f;
        [SerializeField] protected float alertCooldown = 60f;
        
        [Header("Debug")]
        [SerializeField] protected bool debugDetection = false;
        
        // State tracking
        protected DetectionState currentState = DetectionState.Unaware;
        protected IDetectable currentTarget;
        protected float detectionTimer;
        protected float alertTimer;
        protected Coroutine detectionRoutine;
        protected bool isActive;
        
        // Detection data
        protected DetectionData lastDetection;
        protected Vector3 lastKnownPlayerPosition;
        
        // Cached player references for performance
        private GameObject cachedPlayerObject;
        private IDetectable cachedPlayerDetectable;
        
        #region IDetector Implementation
        public virtual string DetectorId 
        { 
            get 
            {
                if (string.IsNullOrEmpty(detectorId))
                    detectorId = $"{GetType().Name}_{GetInstanceID()}";
                return detectorId;
            }
        }
        
        public virtual string ZoneId 
        { 
            get => zoneId; 
            set => zoneId = value; 
        }
        
        public DetectionState CurrentState => currentState;
        public virtual bool IsActive 
        { 
            get => isActive; 
            set => isActive = value; 
        }
        
        public virtual float DetectionRange => detectionRange;
        public virtual float DetectionSpeed => detectionSpeed;
        public virtual bool CanCommunicate => canCommunicate;
        
        public virtual float TryDetect(IDetectable target, Vector3 targetPosition)
        {
            if (!IsActive || target == null)
                return 0f;
            
            // Distance check
            float distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > detectionRange)
                return 0f;
            
            // Override in derived classes for specific detection logic
            return PerformDetectionCheck(target, targetPosition, distance);
        }
        
        public virtual void OnDetectionMade(DetectionData detectionData)
        {
            lastDetection = detectionData;
            lastKnownPlayerPosition = detectionData.lastKnownPlayerPosition;
            
            // Start or continue detection process
            if (detectionRoutine == null)
            {
                detectionRoutine = StartCoroutine(DetectionProcess(detectionData));
            }
            
            // Notify alert system
            if (AlertSystem.Instance != null)
            {
                AlertSystem.Instance.ReportDetection(detectionData);
            }
            
            if (debugDetection)
                Debug.Log($"[{DetectorId}] Detection made: {detectionData.detectionStrength:F2} strength");
        }
        
        public virtual void OnDetectionLost()
        {
            if (detectionRoutine != null)
            {
                StopCoroutine(detectionRoutine);
                detectionRoutine = null;
            }
            
            if (debugDetection)
                Debug.Log($"[{DetectorId}] Detection lost");
        }
        
        public virtual void OnZoneAlertChanged(AlertLevel newLevel, AlertData alertData)
        {
            UpdateAlertBehavior(newLevel);
            
            if (debugDetection)
                Debug.Log($"[{DetectorId}] Zone alert changed to: {newLevel}");
        }
        
        public virtual void OnZoneDetectionReported(DetectionData detectionData)
        {
            // If another detector spotted the player, update our last known position
            if (detectionData.detectorId != DetectorId)
            {
                lastKnownPlayerPosition = detectionData.lastKnownPlayerPosition;
                OnSharedDetection(detectionData);
            }
        }
        
        public virtual void UpdateAlertBehavior(AlertLevel alertLevel)
        {
            // Override in derived classes for specific alert responses
            OnAlertLevelChanged(alertLevel);
        }
        #endregion
        
        #region IZoneEntity Implementation
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
        protected virtual void Awake()
        {
            isActive = startActive;
            
            if (string.IsNullOrEmpty(detectorId))
                detectorId = $"{GetType().Name}_{GetInstanceID()}";
        }
        
        protected virtual void Start()
        {
            RegisterWithSystems();
            CachePlayerReference();
        }
        
        protected virtual void Update()
        {
            if (!IsActive) return;
            
            UpdateDetection();
            UpdateTimers();
        }
        
        protected virtual void OnDestroy()
        {
            UnregisterFromSystems();
            
            if (detectionRoutine != null)
            {
                StopCoroutine(detectionRoutine);
            }
        }
        #endregion
        
        #region Abstract Methods (Override in derived classes)
        /// <summary>
        /// Perform the actual detection logic (vision, hearing, etc.)
        /// </summary>
        protected abstract float PerformDetectionCheck(IDetectable target, Vector3 targetPosition, float distance);
        
        /// <summary>
        /// Called when alert level changes - implement specific behavior
        /// </summary>
        protected abstract void OnAlertLevelChanged(AlertLevel newLevel);
        
        /// <summary>
        /// Called during detection process - implement state transitions
        /// </summary>
        protected abstract void OnDetectionStateChanged(DetectionState oldState, DetectionState newState);
        #endregion
        
        #region Virtual Methods (Can override if needed)
        /// <summary>
        /// Called when another detector in the zone reports a detection
        /// </summary>
        protected virtual void OnSharedDetection(DetectionData sharedDetection)
        {
            // Default: become suspicious if we weren't already alert
            if (currentState == DetectionState.Unaware)
            {
                ChangeState(DetectionState.Suspicious);
                detectionTimer = suspiciousTime * 0.5f; // Shorter suspicious time from shared info
            }
        }
        
        /// <summary>
        /// Main detection update loop
        /// </summary>
        protected virtual void UpdateDetection()
        {
            // Check if our cached player reference is still valid
            if (cachedPlayerObject == null && cachedPlayerDetectable != null)
            {
                // Player object was destroyed, clear the cache
                cachedPlayerDetectable = null;
                currentTarget = null;
            }
            
            if (currentTarget == null)
            {
                // Try to find player target
                currentTarget = FindPlayerTarget();
            }
            
            if (currentTarget != null)
            {
                float detectionStrength = TryDetect(currentTarget, currentTarget.Position);
                
                if (detectionStrength > 0f)
                {
                    var detectionData = DetectionData.Create(
                        GetDetectionSource(),
                        transform.position,
                        currentTarget.Position,
                        detectionStrength,
                        zoneId,
                        DetectorId
                    );
                    
                    OnDetectionMade(detectionData);
                }
                else if (currentState != DetectionState.Unaware)
                {
                    // Lost detection
                    OnDetectionLost();
                }
            }
        }
        
        /// <summary>
        /// Update internal timers
        /// </summary>
        protected virtual void UpdateTimers()
        {
            if (alertTimer > 0f)
            {
                alertTimer -= Time.deltaTime;
                
                if (alertTimer <= 0f && currentState != DetectionState.Unaware)
                {
                    ChangeState(DetectionState.Unaware);
                }
            }
        }
        
        /// <summary>
        /// Find the player target (using cached reference for performance)
        /// </summary>
        protected virtual IDetectable FindPlayerTarget()
        {
            // Use cached reference if valid
            if (cachedPlayerObject != null && cachedPlayerDetectable != null)
            {
                return cachedPlayerDetectable;
            }
            
            // Cache is invalid, try to refresh it
            CachePlayerReference();
            return cachedPlayerDetectable;
        }
        
        /// <summary>
        /// Cache the player GameObject and IDetectable component for performance
        /// </summary>
        protected virtual void CachePlayerReference()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                cachedPlayerObject = player;
                cachedPlayerDetectable = player.GetComponent<IDetectable>();
                
                if (debugDetection && cachedPlayerDetectable != null)
                    Debug.Log($"[{DetectorId}] Player reference cached successfully");
            }
            else
            {
                cachedPlayerObject = null;
                cachedPlayerDetectable = null;
                
                if (debugDetection)
                    Debug.Log($"[{DetectorId}] Player not found for caching");
            }
        }
        
        /// <summary>
        /// Force refresh the cached player reference (useful if player respawns)
        /// </summary>
        public virtual void RefreshPlayerCache()
        {
            CachePlayerReference();
        }
        
        /// <summary>
        /// Get the type of detection this detector provides
        /// </summary>
        protected virtual DetectionSource GetDetectionSource()
        {
            return DetectionSource.Other;
        }
        
        /// <summary>
        /// Change the current detection state
        /// </summary>
        protected virtual void ChangeState(DetectionState newState)
        {
            if (currentState != newState)
            {
                DetectionState oldState = currentState;
                currentState = newState;
                OnDetectionStateChanged(oldState, newState);
                
                if (debugDetection)
                    Debug.Log($"[{DetectorId}] State: {oldState} -> {newState}");
            }
        }
        
        /// <summary>
        /// Register with alert system and zone manager
        /// </summary>
        protected virtual void RegisterWithSystems()
        {
            if (ZoneManager.Instance != null)
            {
                ZoneManager.Instance.RegisterEntity(this);
            }
            
            if (AlertSystem.Instance != null)
            {
                AlertSystem.Instance.RegisterDetector(this);
            }
        }
        
        /// <summary>
        /// Unregister from systems on destruction
        /// </summary>
        protected virtual void UnregisterFromSystems()
        {
            if (ZoneManager.Instance != null)
            {
                ZoneManager.Instance.UnregisterEntity(this);
            }
            
            if (AlertSystem.Instance != null)
            {
                AlertSystem.Instance.UnregisterDetector(this);
            }
        }
        #endregion
        
        #region Detection Process Coroutine
        protected virtual IEnumerator DetectionProcess(DetectionData initialDetection)
        {
            ChangeState(DetectionState.Suspicious);
            detectionTimer = suspiciousTime;
            
            // Suspicious phase
            while (detectionTimer > 0f && currentState == DetectionState.Suspicious)
            {
                detectionTimer -= Time.deltaTime;
                
                // Continue checking if we can still detect the target
                if (currentTarget != null)
                {
                    float currentStrength = TryDetect(currentTarget, currentTarget.Position);
                    if (currentStrength <= 0f)
                    {
                        // Lost target during suspicious phase
                        ChangeState(DetectionState.Searching);
                        detectionTimer = searchTime;
                        break;
                    }
                }
                
                yield return null;
            }
            
            // If we completed suspicious phase without losing target, go hostile
            if (currentState == DetectionState.Suspicious && detectionTimer <= 0f)
            {
                ChangeState(DetectionState.Hostile);
                alertTimer = alertCooldown;
            }
            
            // Search phase (if we lost target during suspicious)
            while (detectionTimer > 0f && currentState == DetectionState.Searching)
            {
                detectionTimer -= Time.deltaTime;
                
                // Keep checking if we reacquire target
                if (currentTarget != null)
                {
                    float currentStrength = TryDetect(currentTarget, currentTarget.Position);
                    if (currentStrength > 0f)
                    {
                        // Reacquired target, back to suspicious
                        ChangeState(DetectionState.Suspicious);
                        detectionTimer = suspiciousTime * 0.5f; // Shorter time since we recently saw them
                        continue;
                    }
                }
                
                yield return null;
            }
            
            // Return to normal state after search timeout
            if (currentState == DetectionState.Searching && detectionTimer <= 0f)
            {
                ChangeState(DetectionState.Unaware);
            }
            
            detectionRoutine = null;
        }
        #endregion
        
        #region Debug
        protected virtual void OnDrawGizmosSelected()
        {
            if (!debugDetection) return;
            
            // Draw detection range
            Gizmos.color = IsActive ? Color.yellow : Color.gray;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            
            // Draw last known player position
            if (lastKnownPlayerPosition != Vector3.zero)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(lastKnownPlayerPosition, 0.5f);
                Gizmos.DrawLine(transform.position, lastKnownPlayerPosition);
            }
        }
        #endregion
    }
}