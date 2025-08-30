using UnityEngine;

namespace StealthSystem
{
    /// <summary>
    /// Component that makes an entity detectable (primarily for the player)
    /// Integrates with existing CharacterMotor for movement data
    /// </summary>
    public class DetectableComponent : MonoBehaviour, IDetectable
    {
        [Header("Noise Settings")]
        [SerializeField] private float baseNoiseLevel = 0.3f;
        [SerializeField] private float sprintNoiseMultiplier = 2f;
        [SerializeField] private float crouchNoiseMultiplier = 0.3f;
        [SerializeField] private float slideNoiseMultiplier = 0.8f;
        [SerializeField] private AnimationCurve speedToNoiseCurve = AnimationCurve.Linear(0f, 0f, 10f, 1f);
        
        [Header("Visibility Settings")]
        [SerializeField] private float baseVisibilityLevel = 1f;
        [SerializeField] private float crouchVisibilityMultiplier = 0.6f;
        [SerializeField] private float sprintVisibilityMultiplier = 1.3f;
        [SerializeField] private float shadowVisibilityMultiplier = 0.4f;
        
        [Header("Disguise Settings")]
        [SerializeField] private float currentDisguiseLevel = 0f;
        [SerializeField] private bool canUseDisguises = true;
        
        [Header("Behavior Tracking")]
        [SerializeField] private float aggressiveBehaviorTimeout = 30f;
        [SerializeField] private bool debugDetectable = false;
        
        // Cached components
        private CharacterMotor characterMotor;
        private Transform cachedTransform;
        
        // State tracking
        private PlayerBehavior currentBehavior = PlayerBehavior.Stealth;
        private PlayerBehavior previousBehavior = PlayerBehavior.Stealth;
        private float lastAggressiveTime = -1f;
        private int detectionCount = 0;
        private bool isInShadows = false;
        
        // Noise calculation
        private float currentNoiseLevel;
        private float noiseRadius;
        private string currentNoiseType = "footsteps";
        
        #region IDetectable Implementation
        public Vector3 Position => cachedTransform.position;
        public Vector3 Velocity => characterMotor != null ? characterMotor.Velocity : Vector3.zero;
        public float CurrentSpeed => characterMotor != null ? characterMotor.CurrentSpeed : 0f;
        
        public float NoiseLevel => currentNoiseLevel;
        public float NoiseRadius => noiseRadius;
        public string NoiseType => currentNoiseType;
        
        public float VisibilityLevel { get; private set; }
        public float SizeModifier { get; private set; } = 1f;
        public bool IsInShadows => isInShadows;
        public float DisguiseLevel => currentDisguiseLevel;
        
        public PlayerBehavior CurrentBehavior => currentBehavior;
        public bool HasBeenAggressive => lastAggressiveTime >= 0f && Time.time - lastAggressiveTime < aggressiveBehaviorTimeout;
        public int DetectionCount => detectionCount;
        #endregion
        
        #region Unity Lifecycle
        private void Awake()
        {
            cachedTransform = transform;
            characterMotor = GetComponent<CharacterMotor>();
            
            if (characterMotor == null)
            {
                Debug.LogWarning($"[DetectableComponent] No CharacterMotor found on {gameObject.name}. Some detection features may not work.");
            }
        }
        
        private void Start()
        {
            RegisterWithSystems();
        }
        
        private void Update()
        {
            UpdateNoiseLevel();
            UpdateVisibilityLevel();
            UpdateBehavior();
            
            if (debugDetectable)
                DebugDetectable();
        }
        
        private void OnDestroy()
        {
            UnregisterFromSystems();
        }
        #endregion
        
        #region Noise Calculation
        private void UpdateNoiseLevel()
        {
            if (characterMotor == null)
            {
                currentNoiseLevel = 0f;
                noiseRadius = 0f;
                return;
            }
            
            float speed = characterMotor.CurrentSpeed;
            float baseNoise = baseNoiseLevel;
            
            // Apply movement state modifiers
            if (characterMotor.IsSprinting)
            {
                baseNoise *= sprintNoiseMultiplier;
                currentNoiseType = "running";
            }
            else if (characterMotor.IsCrouching)
            {
                baseNoise *= crouchNoiseMultiplier;
                currentNoiseType = "sneaking";
            }
            else if (characterMotor.IsSliding)
            {
                baseNoise *= slideNoiseMultiplier;
                currentNoiseType = "sliding";
            }
            else if (speed > 0.1f)
            {
                currentNoiseType = "footsteps";
            }
            else
            {
                currentNoiseType = "idle";
            }
            
            // Apply speed curve
            float speedNoise = speedToNoiseCurve.Evaluate(speed);
            currentNoiseLevel = Mathf.Clamp01(baseNoise * speedNoise);
            
            // Calculate noise radius (how far the noise travels)
            noiseRadius = currentNoiseLevel * 15f; // Max 15 units
            
            // Special case: no noise if not moving
            if (speed < 0.1f)
            {
                currentNoiseLevel = 0f;
                noiseRadius = 0f;
            }
        }
        #endregion
        
        #region Visibility Calculation
        private void UpdateVisibilityLevel()
        {
            float visibility = baseVisibilityLevel;
            
            // Apply movement state modifiers
            if (characterMotor != null)
            {
                if (characterMotor.IsCrouching)
                {
                    visibility *= crouchVisibilityMultiplier;
                    SizeModifier = 0.6f; // Smaller target when crouched
                }
                else if (characterMotor.IsSprinting)
                {
                    visibility *= sprintVisibilityMultiplier;
                    SizeModifier = 1.2f; // Larger/more obvious when sprinting
                }
                else
                {
                    SizeModifier = 1f;
                }
            }
            
            // Apply lighting conditions
            if (isInShadows)
            {
                visibility *= shadowVisibilityMultiplier;
            }
            
            // Apply disguise
            visibility *= (1f - currentDisguiseLevel);
            
            VisibilityLevel = Mathf.Clamp01(visibility);
        }
        #endregion
        
        #region Behavior Tracking
        private void UpdateBehavior()
        {
            previousBehavior = currentBehavior;
            
            // Simple behavior detection - can be expanded
            if (HasBeenAggressive)
            {
                currentBehavior = PlayerBehavior.Aggressive;
            }
            else
            {
                currentBehavior = PlayerBehavior.Stealth;
            }
            
            // Notify systems if behavior changed
            if (currentBehavior != previousBehavior)
            {
                OnBehaviorChanged(previousBehavior, currentBehavior);
            }
        }
        
        private void OnBehaviorChanged(PlayerBehavior oldBehavior, PlayerBehavior newBehavior)
        {
            if (AlertSystem.Instance != null)
            {
                AlertSystem.Instance.ReportPlayerBehaviorChanged(newBehavior, oldBehavior); // ✅ Fixed method name
            }
            
            if (debugDetectable)
                Debug.Log($"[DetectableComponent] Behavior changed: {oldBehavior} -> {newBehavior}");
        }
        #endregion
        
        #region Public Methods
        /// <summary>
        /// Called when this entity is detected
        /// </summary>
        public void OnDetected(DetectionData detectionData)
        {
            detectionCount++;
            
            if (debugDetectable)
                Debug.Log($"[DetectableComponent] Detected by {detectionData.detectorId} (Count: {detectionCount})");
        }
        
        /// <summary>
        /// Mark the player as having performed an aggressive action
        /// </summary>
        public void MarkAggressive(string reason = "")
        {
            lastAggressiveTime = Time.time;
            
            if (debugDetectable)
                Debug.Log($"[DetectableComponent] Marked aggressive: {reason}");
        }
        
        /// <summary>
        /// Set the current disguise level
        /// </summary>
        public void SetDisguiseLevel(float disguiseLevel, string disguiseType = "")
        {
            if (!canUseDisguises) return;
            
            currentDisguiseLevel = Mathf.Clamp01(disguiseLevel);
            
            if (debugDetectable)
                Debug.Log($"[DetectableComponent] Disguise set to {currentDisguiseLevel:F2}: {disguiseType}");
        }
        
        /// <summary>
        /// Set whether the entity is currently in shadows
        /// </summary>
        public void SetInShadows(bool inShadows)
        {
            isInShadows = inShadows;
        }
        
        /// <summary>
        /// Reset detection count (for new level/area)
        /// </summary>
        public void ResetDetectionCount()
        {
            detectionCount = 0;
        }
        #endregion
        
        #region System Registration
        private void RegisterWithSystems()
        {
            // Register with alert system as the primary detectable target
            if (AlertSystem.Instance != null)
            {
                AlertSystem.Instance.RegisterDetectable(this);
            }
        }
        
        private void UnregisterFromSystems()
        {
            if (AlertSystem.Instance != null)
            {
                AlertSystem.Instance.UnregisterDetectable(this);
            }
        }
        #endregion
        
        #region Debug
        private void DebugDetectable()
        {
            Debug.Log($"[DetectableComponent] Noise: {currentNoiseLevel:F2} ({currentNoiseType}) " +
                     $"Visibility: {VisibilityLevel:F2} Disguise: {currentDisguiseLevel:F2} " +
                     $"Behavior: {currentBehavior}");
        }
        
        private void OnDrawGizmosSelected()
        {
            if (!debugDetectable) return;
            
            // Draw noise radius
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(Position, noiseRadius);
            
            // Draw visibility indicator
            Gizmos.color = Color.Lerp(Color.green, Color.red, VisibilityLevel);
            Gizmos.DrawWireCube(Position + Vector3.up * 2f, Vector3.one * 0.5f);
            
            // Draw behavior indicator
            Gizmos.color = currentBehavior == PlayerBehavior.Aggressive ? Color.red : Color.green;
            Gizmos.DrawWireSphere(Position + Vector3.up * 2.5f, 0.25f);
        }
        #endregion
    }
}