// FILE: Assets/Scripts/Stealth/Systems/AlertSystem.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System;

namespace StealthSystem
{
    /// <summary>
    /// Central alert coordination system. Manages detection states, scoring,
    /// and AI coordination across all zones.
    /// </summary>
    public class AlertSystem : MonoBehaviour
    {
        #region Singleton
        private static AlertSystem instance;
        public static AlertSystem Instance 
        { 
            get 
            {
                if (instance == null)
                    instance = FindAnyObjectByType<AlertSystem>();
                return instance;
            }
        }
        
        public static bool HasInstance => instance != null;
        #endregion
        
        #region Serialized Fields
        [Header("Alert Settings")]
        [SerializeField] private float hideTimeToReduceAlert = 60f; // Time to hide for alert reduction
        [SerializeField] private bool enableAlertSpread = true;
        [SerializeField] private float alertSpreadDelay = 2f;
        [SerializeField] private int maxReinforcementsPerZone = 5;
        
        [Header("Behavior Tracking")]
        [SerializeField] private float aggressiveBehaviorThreshold = 3f; // Detections before considered aggressive
        [SerializeField] private float behaviorChangeDelay = 1f;
        
        [Header("Scoring")]
        [SerializeField] private bool enableScoring = true;
        [SerializeField] private float singleGuardPenalty = 25f;    // -25% stealth score
        [SerializeField] private float siteWidePenalty = 75f;       // -75% stealth score  
        [SerializeField] private float recoveryBonus = 50f;         // +50% penalty recovery
        
        [Header("Reinforcements")]
        [SerializeField] private bool enableReinforcements = true;
        [SerializeField] private bool unlimitedReinforcements = false;
        [SerializeField] private float reinforcementDelay = 5f;
        
        [Header("Debug")]
        [SerializeField] private bool debugAlerts = false;
        [SerializeField] private bool showAlertUI = true;
        #endregion
        
        #region Private Fields
        // Zone alert tracking
        private Dictionary<string, AlertData> zoneAlerts = new Dictionary<string, AlertData>();
        private Dictionary<string, Coroutine> alertTimers = new Dictionary<string, Coroutine>();
        
        // Entity tracking
        private List<IDetector> registeredDetectors = new List<IDetector>();
        private List<IAlertReceiver> alertReceivers = new List<IAlertReceiver>();
        private IDetectable primaryTarget; // Usually the player
        
        // Detection tracking
        private List<DetectionData> recentDetections = new List<DetectionData>();
        private Dictionary<string, int> zoneReinforcementCounts = new Dictionary<string, int>();
        
        // Behavior tracking
        private PlayerBehavior currentPlayerBehavior = PlayerBehavior.Stealth;
        private float totalDetections = 0f;
        private bool hasBeenDetected = false;
        
        // Scoring
        private float baseStealthScore = 100f;
        private float currentStealthScore = 100f;
        private List<ScoringPenalty> activePenalties = new List<ScoringPenalty>();
        #endregion
        
        #region Events
        /// <summary>Called when any zone's alert level changes</summary>
        public static event Action<string, AlertLevel, AlertLevel> OnZoneAlertChanged; // zone, new, old
        
        /// <summary>Called when global alert state changes</summary>
        public static event Action<AlertLevel> OnGlobalAlertChanged;
        
        /// <summary>Called when player behavior changes</summary>
        public static event Action<PlayerBehavior, PlayerBehavior> OnPlayerBehaviorChanged; // new, old
        
        /// <summary>Called when stealth score changes</summary>
        public static event Action<float, float> OnStealthScoreChanged; // new, old
        
        /// <summary>Called when reinforcements are requested</summary>
        public static event Action<string, DetectionData> OnReinforcementsRequested; // zone, detection
        #endregion
        
        #region Properties
        /// <summary>Current global alert level (highest of all zones)</summary>
        public AlertLevel GlobalAlertLevel 
        { 
            get 
            {
                if (zoneAlerts.Count == 0) return AlertLevel.Green;
                return zoneAlerts.Values.Max(data => data.currentLevel);
            }
        }
        
        /// <summary>Current player behavior</summary>
        public PlayerBehavior CurrentPlayerBehavior => currentPlayerBehavior;
        
        /// <summary>Current stealth score (0-100)</summary>
        public float CurrentStealthScore => currentStealthScore;
        
        /// <summary>Has the player been detected at all?</summary>
        public bool HasBeenDetected => hasBeenDetected;
        
        /// <summary>Total number of detections</summary>
        public float TotalDetections => totalDetections;
        
        /// <summary>Is any zone currently on high alert?</summary>
        public bool IsHighAlert => zoneAlerts.Values.Any(data => data.currentLevel >= AlertLevel.Red);
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
        }
        
        private void Start()
        {
            InitializeAlertSystem();
        }
        
        private void Update()
        {
            UpdateBehaviorTracking();
            if (Input.GetKeyDown(KeyCode.T)) 
            {
                string testZone = "";
                
                // Try to use current player zone first
                if (ZoneManager.HasInstance)
                {
                    testZone = ZoneManager.Instance.CurrentPlayerZone;
                    
                    // If player isn't in a zone, use the first registered zone
                    if (string.IsNullOrEmpty(testZone))
                    {
                        var zones = ZoneManager.Instance.ZoneIds.ToArray();
                        if (zones.Length > 0)
                            testZone = zones[0];
                    }
                }
                
                // Fallback to create a default zone if none exist
                if (string.IsNullOrEmpty(testZone))
                    testZone = "DefaultZone";
                
                AlertSystem.Instance?.TriggerAlert(testZone, AlertLevel.Orange, transform.position);
                Debug.Log($"[AlertSystem] Debug alert triggered in zone: {testZone}");
            }
        }
        
        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
        #endregion
        
        #region Initialization
        private void InitializeAlertSystem()
        {
            currentStealthScore = baseStealthScore;
            
            // Subscribe to zone manager events if available
            if (ZoneManager.HasInstance)
            {
                ZoneManager.OnPlayerZoneChanged += OnPlayerZoneChanged;
            }
            
            Debug.Log("[AlertSystem] Alert System initialized");
        }
        
        private void OnPlayerZoneChanged(string newZone, string oldZone)
        {
            // Ensure we have alert data for the new zone
            if (!string.IsNullOrEmpty(newZone) && !zoneAlerts.ContainsKey(newZone))
            {
                InitializeZoneAlert(newZone);
            }
        }
        
        private void InitializeZoneAlert(string zoneId)
        {
            if (!zoneAlerts.ContainsKey(zoneId))
            {
                zoneAlerts[zoneId] = new AlertData
                {
                    currentLevel = AlertLevel.Green,
                    previousLevel = AlertLevel.Green,
                    alertStartTime = Time.time,
                    lastDetectionTime = -1f,
                    totalDetections = 0,
                    lastKnownPlayerPosition = Vector3.zero,
                    zoneId = zoneId
                };
                
                zoneReinforcementCounts[zoneId] = 0;
            }
        }
        #endregion
        
        #region Entity Registration
        /// <summary>
        /// Register a detector with the alert system
        /// </summary>
        public void RegisterDetector(IDetector detector)
        {
            if (detector == null || registeredDetectors.Contains(detector))
                return;
            
            registeredDetectors.Add(detector);
            
            // Ensure the detector's zone exists
            InitializeZoneAlert(detector.ZoneId);
            
            if (debugAlerts)
                Debug.Log($"[AlertSystem] Registered detector: {detector.DetectorId} in zone {detector.ZoneId}");
        }
        
        /// <summary>
        /// Unregister a detector
        /// </summary>
        public void UnregisterDetector(IDetector detector)
        {
            if (detector != null)
            {
                registeredDetectors.Remove(detector);
                
                if (debugAlerts)
                    Debug.Log($"[AlertSystem] Unregistered detector: {detector.DetectorId}");
            }
        }
        
        /// <summary>
        /// Register an alert receiver
        /// </summary>
        public void RegisterAlertReceiver(IAlertReceiver receiver)
        {
            if (receiver == null || alertReceivers.Contains(receiver))
                return;
            
            alertReceivers.Add(receiver);
        }
        
        /// <summary>
        /// Unregister an alert receiver
        /// </summary>
        public void UnregisterAlertReceiver(IAlertReceiver receiver)
        {
            if (receiver != null)
            {
                alertReceivers.Remove(receiver);
            }
        }
        
        /// <summary>
        /// Register the primary detectable target (player)
        /// </summary>
        public void RegisterDetectable(IDetectable detectable)
        {
            primaryTarget = detectable;
            
            if (debugAlerts)
                Debug.Log("[AlertSystem] Registered primary detectable target");
        }
        
        /// <summary>
        /// Unregister the primary detectable target
        /// </summary>
        public void UnregisterDetectable(IDetectable detectable)
        {
            if (primaryTarget == detectable)
            {
                primaryTarget = null;
            }
        }
        #endregion
        
        #region Detection Handling
        /// <summary>
        /// Central method for reporting detections
        /// </summary>
        public void ReportDetection(DetectionData detectionData)
        {
            if (string.IsNullOrEmpty(detectionData.zoneId))
            {
                Debug.LogWarning("[AlertSystem] Detection reported with no zone ID");
                return;
            }
            
            // Initialize zone if needed
            InitializeZoneAlert(detectionData.zoneId);
            
            // Update detection data
            recentDetections.Add(detectionData);
            totalDetections++;
            hasBeenDetected = true;
            
            // Update zone alert data
            var alertData = zoneAlerts[detectionData.zoneId];
            alertData.lastDetectionTime = detectionData.timestamp;
            alertData.totalDetections++;
            alertData.lastKnownPlayerPosition = detectionData.lastKnownPlayerPosition;
            zoneAlerts[detectionData.zoneId] = alertData;
            
            // Escalate alert level
            EscalateZoneAlert(detectionData.zoneId, detectionData);
            
            // Apply scoring penalty
            ApplyScoringPenalty(detectionData);
            
            // Notify all detectors in the zone
            NotifyZoneDetectors(detectionData);
            
            // Notify alert receivers
            NotifyAlertReceivers(detectionData);
            
            // Request reinforcements if appropriate
            ConsiderReinforcements(detectionData);
            
            // Start hide timer for alert reduction
            StartHideTimer(detectionData.zoneId);
            
            if (debugAlerts)
                Debug.Log($"[AlertSystem] Detection reported: {detectionData.source} in {detectionData.zoneId} (Strength: {detectionData.detectionStrength:F2})");
        }
        
        /// <summary>
        /// Escalate alert level in a zone based on detection
        /// </summary>
        private void EscalateZoneAlert(string zoneId, DetectionData detectionData)
        {
            var alertData = zoneAlerts[zoneId];
            AlertLevel newLevel = DetermineNewAlertLevel(alertData, detectionData);
            
            if (newLevel != alertData.currentLevel)
            {
                SetZoneAlertLevel(zoneId, newLevel);
                
                // Spread alert to adjacent zones if enabled
                if (enableAlertSpread && newLevel >= AlertLevel.Orange)
                {
                    StartCoroutine(SpreadAlertToAdjacentZones(zoneId, newLevel));
                }
            }
        }
        
        /// <summary>
        /// Determine what the new alert level should be
        /// </summary>
        private AlertLevel DetermineNewAlertLevel(AlertData alertData, DetectionData detectionData)
        {
            // High strength detection goes straight to Orange/Red
            if (detectionData.detectionStrength >= 0.9f)
            {
                return AlertLevel.Red;
            }
            else if (detectionData.detectionStrength >= 0.7f)
            {
                return AlertLevel.Orange;
            }
            
            // Multiple detections escalate alert level
            if (alertData.totalDetections >= 3)
            {
                return AlertLevel.Red;
            }
            else if (alertData.totalDetections >= 2)
            {
                return AlertLevel.Orange;
            }
            
            // First detection is usually suspicious
            return AlertLevel.Suspicious;
        }
        
        /// <summary>
        /// Set a zone's alert level
        /// </summary>
        public void SetZoneAlertLevel(string zoneId, AlertLevel newLevel)
        {
            if (!zoneAlerts.ContainsKey(zoneId))
            {
                InitializeZoneAlert(zoneId);
            }
            
            var alertData = zoneAlerts[zoneId];
            AlertLevel previousLevel = alertData.currentLevel;
            
            if (newLevel == previousLevel) return;
            
            // Update alert data
            alertData.previousLevel = previousLevel;
            alertData.currentLevel = newLevel;
            
            if (newLevel > AlertLevel.Green && previousLevel == AlertLevel.Green)
            {
                alertData.alertStartTime = Time.time;
            }
            
            zoneAlerts[zoneId] = alertData;
            
            // Cancel existing timer if escalating
            if (newLevel > previousLevel && alertTimers.ContainsKey(zoneId))
            {
                StopCoroutine(alertTimers[zoneId]);
                alertTimers.Remove(zoneId);
            }
            
            // Notify systems
            OnZoneAlertChanged?.Invoke(zoneId, newLevel, previousLevel);
            OnGlobalAlertChanged?.Invoke(GlobalAlertLevel);
            
            // Notify all detectors in the zone
            NotifyZoneDetectors(zoneId, newLevel);
            
            // Notify alert receivers
            NotifyAlertReceiversOfLevelChange(zoneId, newLevel, previousLevel);
            
            if (debugAlerts)
                Debug.Log($"[AlertSystem] Zone {zoneId} alert: {previousLevel} -> {newLevel}");
        }
        #endregion
        
        #region Alert Timers and Reduction
        /// <summary>
        /// Start the hide timer for a zone - if player stays hidden, alert reduces
        /// </summary>
        private void StartHideTimer(string zoneId)
        {
            // Cancel existing timer
            if (alertTimers.ContainsKey(zoneId))
            {
                StopCoroutine(alertTimers[zoneId]);
            }
            
            // Start new timer
            alertTimers[zoneId] = StartCoroutine(HideTimer(zoneId));
        }
        
        /// <summary>
        /// Hide timer coroutine - reduces alert if player stays hidden
        /// </summary>
        private IEnumerator HideTimer(string zoneId)
        {
            yield return new WaitForSeconds(hideTimeToReduceAlert);
            
            // Check if player has been detected recently in this zone
            if (zoneAlerts.ContainsKey(zoneId))
            {
                var alertData = zoneAlerts[zoneId];
                float timeSinceDetection = Time.time - alertData.lastDetectionTime;
                
                if (timeSinceDetection >= hideTimeToReduceAlert)
                {
                    // Successfully hidden - reduce alert level
                    ReduceZoneAlert(zoneId);
                    
                    // Apply recovery bonus to scoring
                    ApplyRecoveryBonus(zoneId);
                }
            }
            
            alertTimers.Remove(zoneId);
        }
        
        /// <summary>
        /// Reduce alert level in a zone (successful hiding)
        /// </summary>
        private void ReduceZoneAlert(string zoneId)
        {
            if (!zoneAlerts.ContainsKey(zoneId)) return;
            
            var alertData = zoneAlerts[zoneId];
            AlertLevel reducedLevel = GetReducedAlertLevel(alertData.currentLevel);
            
            SetZoneAlertLevel(zoneId, reducedLevel);
            
            if (debugAlerts)
                Debug.Log($"[AlertSystem] Zone {zoneId} alert reduced through successful hiding");
        }
        
        /// <summary>
        /// Get the reduced alert level
        /// </summary>
        private AlertLevel GetReducedAlertLevel(AlertLevel currentLevel)
        {
            return currentLevel switch
            {
                AlertLevel.Red => AlertLevel.Orange,
                AlertLevel.Orange => AlertLevel.Suspicious,
                AlertLevel.Suspicious => AlertLevel.Green,
                _ => AlertLevel.Green
            };
        }
        
        /// <summary>
        /// Spread alert to adjacent zones with delay
        /// </summary>
        private IEnumerator SpreadAlertToAdjacentZones(string sourceZoneId, AlertLevel alertLevel)
        {
            yield return new WaitForSeconds(alertSpreadDelay);
            
            if (ZoneManager.HasInstance)
            {
                var adjacentZones = ZoneManager.Instance.GetAdjacentZones(sourceZoneId);
                
                foreach (string adjacentZone in adjacentZones)
                {
                    if (zoneAlerts.ContainsKey(adjacentZone))
                    {
                        var adjacentAlert = zoneAlerts[adjacentZone];
                        
                        // Only escalate if adjacent zone is at lower level
                        AlertLevel reducedSpreadLevel = GetReducedAlertLevel(alertLevel);
                        if (adjacentAlert.currentLevel < reducedSpreadLevel)
                        {
                            SetZoneAlertLevel(adjacentZone, reducedSpreadLevel);
                            
                            if (debugAlerts)
                                Debug.Log($"[AlertSystem] Alert spread from {sourceZoneId} to {adjacentZone}");
                        }
                    }
                }
            }
        }
        #endregion
        
        #region Behavior Tracking
        /// <summary>
        /// Update player behavior tracking
        /// </summary>
        private void UpdateBehaviorTracking()
        {
            if (primaryTarget == null) return;
            
            PlayerBehavior newBehavior = DeterminePlayerBehavior();
            
            if (newBehavior != currentPlayerBehavior)
            {
                StartCoroutine(DelayedBehaviorChange(newBehavior));
            }
        }
        
        /// <summary>
        /// Determine current player behavior based on actions and detections
        /// </summary>
        private PlayerBehavior DeterminePlayerBehavior()
        {
            if (primaryTarget.HasBeenAggressive || totalDetections >= aggressiveBehaviorThreshold)
            {
                return PlayerBehavior.Aggressive;
            }
            
            return PlayerBehavior.Stealth;
        }
        
        /// <summary>
        /// Delayed behavior change to avoid rapid switching
        /// </summary>
        private IEnumerator DelayedBehaviorChange(PlayerBehavior newBehavior)
        {
            yield return new WaitForSeconds(behaviorChangeDelay);
            
            // Check if behavior is still the same after delay
            if (DeterminePlayerBehavior() == newBehavior && newBehavior != currentPlayerBehavior)
            {
                PlayerBehavior oldBehavior = currentPlayerBehavior;
                currentPlayerBehavior = newBehavior;
                
                OnPlayerBehaviorChanged?.Invoke(newBehavior, oldBehavior);
                
                // Notify alert receivers
                foreach (var receiver in alertReceivers)
                {
                    receiver.OnPlayerBehaviorChanged(newBehavior, oldBehavior);
                }
                
                if (debugAlerts)
                    Debug.Log($"[AlertSystem] Player behavior changed: {oldBehavior} -> {newBehavior}");
            }
        }
        
        /// <summary>
        /// Externally report player aggressive behavior
        /// </summary>
        public void ReportPlayerBehaviorChanged(PlayerBehavior newBehavior, PlayerBehavior oldBehavior)
        {
            if (newBehavior != currentPlayerBehavior)
            {
                currentPlayerBehavior = newBehavior;
                OnPlayerBehaviorChanged?.Invoke(newBehavior, oldBehavior);
                
                // Notify alert receivers
                foreach (var receiver in alertReceivers)
                {
                    receiver.OnPlayerBehaviorChanged(newBehavior, oldBehavior);
                }
            }
        }
        #endregion
        
        #region Scoring System
        /// <summary>
        /// Apply scoring penalty for detection
        /// </summary>
        private void ApplyScoringPenalty(DetectionData detectionData)
        {
            if (!enableScoring) return;
            
            float penalty = CalculatePenalty(detectionData);
            float oldScore = currentStealthScore;
            
            // Create penalty record
            var penaltyRecord = new ScoringPenalty
            {
                penaltyAmount = penalty,
                reason = GetPenaltyReason(detectionData),
                timestamp = Time.time,
                zoneId = detectionData.zoneId,
                canRecover = true
            };
            
            activePenalties.Add(penaltyRecord);
            
            // Apply penalty
            currentStealthScore = Mathf.Max(0f, currentStealthScore - penalty);
            
            OnStealthScoreChanged?.Invoke(currentStealthScore, oldScore);
            
            if (debugAlerts)
                Debug.Log($"[AlertSystem] Scoring penalty applied: -{penalty:F1} points ({penaltyRecord.reason})");
        }
        
        /// <summary>
        /// Calculate penalty amount based on detection context
        /// </summary>
        private float CalculatePenalty(DetectionData detectionData)
        {
            // Check if this is site-wide alert (multiple zones or red alert)
            bool isSiteWide = IsHighAlert || zoneAlerts.Values.Count(z => z.currentLevel >= AlertLevel.Orange) > 1;
            
            float basePenalty = isSiteWide ? siteWidePenalty : singleGuardPenalty;
            
            // Scale by detection strength
            return basePenalty * detectionData.detectionStrength;
        }
        
        /// <summary>
        /// Get penalty reason string
        /// </summary>
        private string GetPenaltyReason(DetectionData detectionData)
        {
            bool isSiteWide = IsHighAlert || zoneAlerts.Values.Count(z => z.currentLevel >= AlertLevel.Orange) > 1;
            return isSiteWide ? "Site-wide Alert" : $"Single Detection ({detectionData.source})";
        }
        
        /// <summary>
        /// Apply recovery bonus for successful hiding
        /// </summary>
        private void ApplyRecoveryBonus(string zoneId)
        {
            if (!enableScoring) return;
            
            // Find recent penalties in this zone that can be recovered
            var recoverablePenalties = activePenalties
                .Where(p => p.zoneId == zoneId && p.canRecover && Time.time - p.timestamp < 300f) // 5 minute recovery window
                .ToList();
            
            float totalRecovery = 0f;
            
            foreach (var penalty in recoverablePenalties)
            {
                float recovery = penalty.penaltyAmount * (recoveryBonus / 100f);
                totalRecovery += recovery;
                penalty.canRecover = false; // Can only recover once
            }
            
            if (totalRecovery > 0f)
            {
                float oldScore = currentStealthScore;
                currentStealthScore = Mathf.Min(baseStealthScore, currentStealthScore + totalRecovery);
                
                OnStealthScoreChanged?.Invoke(currentStealthScore, oldScore);
                
                if (debugAlerts)
                    Debug.Log($"[AlertSystem] Recovery bonus applied: +{totalRecovery:F1} points for successful hiding");
            }
        }
        #endregion
        
        #region Reinforcements
        /// <summary>
        /// Consider calling reinforcements based on detection
        /// </summary>
        private void ConsiderReinforcements(DetectionData detectionData)
        {
            if (!enableReinforcements) return;
            
            string zoneId = detectionData.zoneId;
            
            // Check if reinforcements are needed
            bool shouldCallReinforcements = ShouldCallReinforcements(detectionData);
            
            if (shouldCallReinforcements)
            {
                // Check reinforcement limits
                if (!unlimitedReinforcements)
                {
                    int currentCount = zoneReinforcementCounts.GetValueOrDefault(zoneId, 0);
                    if (currentCount >= maxReinforcementsPerZone)
                    {
                        return;
                    }
                    zoneReinforcementCounts[zoneId] = currentCount + 1;
                }
                
                StartCoroutine(CallReinforcements(zoneId, detectionData));
            }
        }
        
        /// <summary>
        /// Determine if reinforcements should be called
        /// </summary>
        private bool ShouldCallReinforcements(DetectionData detectionData)
        {
            // Call reinforcements if:
            // 1. Player is being aggressive
            // 2. High alert level
            // 3. Multiple recent detections
            
            if (currentPlayerBehavior == PlayerBehavior.Aggressive)
                return true;
            
            var alertData = zoneAlerts[detectionData.zoneId];
            if (alertData.currentLevel >= AlertLevel.Red)
                return true;
            
            if (alertData.totalDetections >= 2)
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Call reinforcements with delay
        /// </summary>
        private IEnumerator CallReinforcements(string zoneId, DetectionData detectionData)
        {
            yield return new WaitForSeconds(reinforcementDelay);
            
            OnReinforcementsRequested?.Invoke(zoneId, detectionData);
            
            if (debugAlerts)
                Debug.Log($"[AlertSystem] Reinforcements called for zone: {zoneId}");
        }
        #endregion
        
        #region Notifications
        /// <summary>
        /// Notify all detectors in a zone of a detection
        /// </summary>
        private void NotifyZoneDetectors(DetectionData detectionData)
        {
            var zoneDetectors = registeredDetectors
                .Where(d => d.ZoneId == detectionData.zoneId)
                .ToList();
            
            foreach (var detector in zoneDetectors)
            {
                detector.OnZoneDetectionReported(detectionData);
            }
        }
        
        /// <summary>
        /// Notify detectors of alert level change
        /// </summary>
        private void NotifyZoneDetectors(string zoneId, AlertLevel newLevel)
        {
            var alertData = zoneAlerts[zoneId];
            var zoneDetectors = registeredDetectors
                .Where(d => d.ZoneId == zoneId)
                .ToList();
            
            foreach (var detector in zoneDetectors)
            {
                detector.OnZoneAlertChanged(newLevel, alertData);
            }
        }
        
        /// <summary>
        /// Notify alert receivers of detection
        /// </summary>
        private void NotifyAlertReceivers(DetectionData detectionData)
        {
            foreach (var receiver in alertReceivers)
            {
                receiver.OnDetectionInZone(detectionData);
            }
        }
        
        /// <summary>
        /// Notify alert receivers of level change
        /// </summary>
        private void NotifyAlertReceiversOfLevelChange(string zoneId, AlertLevel newLevel, AlertLevel previousLevel)
        {
            foreach (var receiver in alertReceivers)
            {
                receiver.OnAlertLevelChanged(newLevel, previousLevel, zoneId);
            }
        }
        #endregion
        
        #region Query Methods
        /// <summary>
        /// Get alert level for a specific zone
        /// </summary>
        public AlertLevel GetZoneAlertLevel(string zoneId)
        {
            return zoneAlerts.GetValueOrDefault(zoneId, new AlertData()).currentLevel;
        }
        
        /// <summary>
        /// Get alert data for a zone
        /// </summary>
        public AlertData GetZoneAlertData(string zoneId)
        {
            return zoneAlerts.GetValueOrDefault(zoneId, new AlertData());
        }
        
        /// <summary>
        /// Check if any zone is at a specific alert level or higher
        /// </summary>
        public bool IsAnyZoneAtLevel(AlertLevel minimumLevel)
        {
            return zoneAlerts.Values.Any(data => data.currentLevel >= minimumLevel);
        }
        
        /// <summary>
        /// Get all zones at a specific alert level
        /// </summary>
        public List<string> GetZonesAtLevel(AlertLevel level)
        {
            return zoneAlerts
                .Where(kvp => kvp.Value.currentLevel == level)
                .Select(kvp => kvp.Key)
                .ToList();
        }
        
        /// <summary>
        /// Get recent detections in a time window
        /// </summary>
        public List<DetectionData> GetRecentDetections(float timeWindow = 60f)
        {
            float cutoffTime = Time.time - timeWindow;
            return recentDetections
                .Where(d => d.timestamp >= cutoffTime)
                .ToList();
        }
        #endregion
        
        #region Public Utility Methods
        /// <summary>
        /// Reset all alerts (for new level/area)
        /// </summary>
        public void ResetAllAlerts()
        {
            // Stop all timers
            foreach (var timer in alertTimers.Values)
            {
                if (timer != null)
                    StopCoroutine(timer);
            }
            
            alertTimers.Clear();
            zoneAlerts.Clear();
            zoneReinforcementCounts.Clear();
            recentDetections.Clear();
            activePenalties.Clear();
            
            // Reset scoring
            currentStealthScore = baseStealthScore;
            totalDetections = 0f;
            hasBeenDetected = false;
            currentPlayerBehavior = PlayerBehavior.Stealth;
            
            OnStealthScoreChanged?.Invoke(currentStealthScore, 0f);
            OnGlobalAlertChanged?.Invoke(AlertLevel.Green);
            
            Debug.Log("[AlertSystem] All alerts reset");
        }
        
        /// <summary>
        /// Manually trigger an alert (for testing or scripted events)
        /// </summary>
        public void TriggerAlert(string zoneId, AlertLevel level, Vector3 position)
        {
            var fakeDetection = DetectionData.Create(
                DetectionSource.Other,
                position,
                position,
                1f,
                zoneId,
                "Manual_Trigger"
            );
            
            ReportDetection(fakeDetection);
            SetZoneAlertLevel(zoneId, level);
        }
        
        /// <summary>
        /// Get comprehensive debug information
        /// </summary>
        public string GetDebugInfo()
        {
            var info = $"AlertSystem Debug Info:\n";
            info += $"Global Alert Level: {GlobalAlertLevel}\n";
            info += $"Player Behavior: {currentPlayerBehavior}\n";
            info += $"Stealth Score: {currentStealthScore:F1}/100\n";
            info += $"Total Detections: {totalDetections}\n";
            info += $"Active Zones: {zoneAlerts.Count}\n\n";
            
            foreach (var kvp in zoneAlerts)
            {
                var data = kvp.Value;
                info += $"Zone '{kvp.Key}': {data.currentLevel} ({data.totalDetections} detections)\n";
            }
            
            return info;
        }
        #endregion
        
        #region GUI Debug
        private void OnGUI()
        {
            if (!showAlertUI || !debugAlerts) return;
            
            GUILayout.BeginArea(new Rect(10, Screen.height - 200, 400, 190));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label($"Global Alert: {GlobalAlertLevel}");
            GUILayout.Label($"Player Behavior: {currentPlayerBehavior}");
            GUILayout.Label($"Stealth Score: {currentStealthScore:F1}/100");
            GUILayout.Label($"Total Detections: {totalDetections}");
            
            GUILayout.Space(10);
            
            foreach (var kvp in zoneAlerts.Take(5)) // Show first 5 zones
            {
                var data = kvp.Value;
                GUILayout.Label($"{kvp.Key}: {data.currentLevel} ({data.totalDetections})");
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        #endregion
    }
    
    /// <summary>
    /// Record of a scoring penalty that can potentially be recovered
    /// </summary>
    [System.Serializable]
    public class ScoringPenalty
    {
        public float penaltyAmount;
        public string reason;
        public float timestamp;
        public string zoneId;
        public bool canRecover;
    }
}