using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace StealthSystem
{
    /// <summary>
    /// Standalone Guard AI with direct detection logic, decoupled from DetectorComponent.
    /// Features clean state management and direct AlertSystem integration.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class GuardAI_Standalone : MonoBehaviour, IAlertReceiver, IZoneEntity
    {
        #region Serialized Fields
        [Header("Guard Configuration")]
        [SerializeField] private GuardType guardType = GuardType.Patrol;
        [SerializeField] private GuardSettings settings = GuardSettings.CreateDefault();
        [SerializeField] private string guardId;
        [SerializeField] private string zoneId;
        
        [Header("Detection Settings")]
        [SerializeField] private float detectionRange = 10f;
        [SerializeField] private float chaseThreshold = 0.3f;
        [SerializeField] private float searchThreshold = 0.1f;
        [SerializeField] private bool canCommunicate = true;
        
        [Header("Patrol Setup")]
        [SerializeField] private Transform[] patrolWaypoints;
        [SerializeField] private bool reversePatrolOnEnd = true;
        [SerializeField] private bool randomizeStartWaypoint = false;
        
        [Header("Vision Setup")]
        [SerializeField] private VisionComponent visionComponent;
        
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] alertSounds;
        [SerializeField] private AudioClip[] searchSounds;
        [SerializeField] private float audioOffsetTime = 0.5f;
        [SerializeField] private int maxQueuedClips = 5;
        
        [Header("Debug")]
        [SerializeField] private bool debugGuardAI = false;
        [SerializeField] private bool debugDetection = false;
        [SerializeField] private bool drawGizmos = true;
        #endregion
        
        #region Private Fields
        // Components
        private CharacterController characterController;
        
        // State Management
        private GuardState currentGuardState = GuardState.Patrolling;
        private GuardState previousGuardState;
        private MovementMode currentMovementMode = MovementMode.Walk;
        
        // Detection
        private IDetectable currentTarget;
        private Vector3 lastKnownPlayerPos;
        private float lastDetectionStrength = 0f;
        private float detectionLossTimer = 0f;
        private const float DETECTION_LOSS_DELAY = 0.5f; // Prevent flickering
        
        // Patrol System
        private int currentWaypointIndex = 0;
        private bool patrolReversed = false;
        private bool isPaused = false;
        
        // Search
        private List<Vector3> searchPoints = new List<Vector3>();
        private int currentSearchPointIndex = 0;
        private Coroutine behaviorCoroutine;
        
        // Movement
        private Vector3 currentDestination;
        private Vector3 moveDirection;
        private bool isMoving = false;
        
        // Timers
        private float stateTimer = 0f;
        private float returnTimer = 0f;
        
        // Audio Queue System
        private Queue<AudioClip> audioQueue = new Queue<AudioClip>();
        private Coroutine audioPlaybackCoroutine;
        private bool isPlayingAudio = false;
        #endregion
        
        #region Properties
        public string GuardId => guardId;
        public string ZoneId { get => zoneId; set => zoneId = value; }
        public GuardType Type => guardType;
        public GuardState CurrentState => currentGuardState;
        public MovementMode MovementMode => currentMovementMode;
        public bool IsMoving => isMoving && moveDirection.magnitude > 0.1f;
        public Vector3 CurrentDestination => currentDestination;
        public float DetectionRange => detectionRange;
        public bool CanCommunicate => canCommunicate;
        public bool IsAudioActive => isPlayingAudio || audioQueue.Count > 0;
        #endregion
        
        #region Unity Lifecycle
        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            
            // Set up guard ID
            if (string.IsNullOrEmpty(guardId))
                guardId = $"Guard_{GetInstanceID()}";
            
            // Get or create VisionComponent
            if (visionComponent == null)
                visionComponent = GetComponent<VisionComponent>();
                
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }
        
        private void Start()
        {
            InitializeGuard();
            RegisterWithSystems();
        }
        
        private void Update()
        {
            UpdateDetection();
            UpdateGuardBehavior();
            UpdateMovement();
            UpdateTimers();
            
            if (debugGuardAI)
                DebugGuardState();
        }
        
        private void OnDestroy()
        {
            UnregisterFromSystems();
            ClearAudioQueue();
            StopAllCoroutines();
        }
        #endregion
        
        #region Initialization
        private void InitializeGuard()
        {
            // Set up detection
            if (visionComponent == null)
            {
                Debug.LogWarning($"[GuardAI] {name} has no VisionComponent. Detection will be limited.");
            }
            
            // Initialize patrol if we have waypoints
            if (guardType == GuardType.Patrol && patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                InitializePatrol();
            }
            
            // Start initial behavior
            ChangeGuardState(GuardState.Patrolling);
            
            if (debugGuardAI)
                Debug.Log($"[GuardAI] {name} initialized as {guardType} guard");
        }
        
        private void InitializePatrol()
        {
            if (randomizeStartWaypoint && patrolWaypoints.Length > 0)
            {
                currentWaypointIndex = Random.Range(0, patrolWaypoints.Length);
            }
            
            if (patrolWaypoints.Length > 0)
            {
                currentDestination = patrolWaypoints[currentWaypointIndex].position;
            }
        }
        
        private void RegisterWithSystems()
        {
            // Register with AlertSystem for zone alerts
            if (AlertSystem.Instance != null && !string.IsNullOrEmpty(zoneId))
            {
                AlertSystem.Instance.RegisterAlertReceiver(this);
            }
            
            // Register with ZoneManager if available
            if (ZoneManager.Instance != null && !string.IsNullOrEmpty(zoneId))
            {
                ZoneManager.Instance.RegisterEntity(this);
            }
        }
        
        private void UnregisterFromSystems()
        {
            // Unregister from AlertSystem
            if (AlertSystem.Instance != null)
            {
                AlertSystem.Instance.UnregisterAlertReceiver(this);
            }
            
            // Unregister from ZoneManager
            if (ZoneManager.Instance != null)
            {
                ZoneManager.Instance.UnregisterEntity(this);
            }
        }
        #endregion
        
        #region Detection System
        private void UpdateDetection()
        {
            // Find player target if we don't have one
            if (currentTarget == null)
            {
                currentTarget = FindPlayerTarget();
            }
            
            if (currentTarget != null)
            {
                float detectionStrength = PerformDetectionCheck(currentTarget, currentTarget.Position);
                
                if (detectionStrength > 0f)
                {
                    // Reset loss timer on successful detection
                    detectionLossTimer = 0f;
                    lastDetectionStrength = detectionStrength;
                    
                    // Handle detection
                    HandleDetection(detectionStrength);
                    
                    // Report to AlertSystem
                    ReportDetection(detectionStrength);
                }
                else
                {
                    // Handle detection loss with delay to prevent flickering
                    detectionLossTimer += Time.deltaTime;
                    if (detectionLossTimer >= DETECTION_LOSS_DELAY)
                    {
                        HandleDetectionLoss();
                        detectionLossTimer = 0f;
                    }
                }
            }
        }
        
        private float PerformDetectionCheck(IDetectable target, Vector3 targetPosition)
        {
            // Distance check first
            float distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > detectionRange)
                return 0f;
            
            // Use VisionComponent for detailed detection
            if (visionComponent != null)
            {
                return visionComponent.CheckVision(target, targetPosition);
            }
            
            // Fallback basic detection
            Vector3 directionToTarget = (targetPosition - transform.position).normalized;
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);
            
            if (angleToTarget <= 45f) // Simple fallback angle
            {
                // Basic line of sight check
                if (Physics.Raycast(transform.position, directionToTarget, distance))
                    return 0f;
                
                return Mathf.Clamp01(1f - (distance / detectionRange));
            }
            
            return 0f;
        }
        
        private void HandleDetection(float detectionStrength)
        {
            // Store last known position
            lastKnownPlayerPos = currentTarget.Position;
            
            // React based on detection strength - always update state regardless of current state
            if (detectionStrength >= chaseThreshold)
            {
                // High confidence detection - start chasing
                ChangeGuardState(GuardState.Chasing);
            }
            else if (detectionStrength >= searchThreshold)
            {
                // Medium confidence - search if not already chasing
                if (currentGuardState != GuardState.Chasing)
                {
                    ChangeGuardState(GuardState.Searching);
                }
            }
            
            if (debugDetection)
                Debug.Log($"[GuardAI] {name} detection: {detectionStrength:F2} strength, state: {currentGuardState}");
        }
        
        private void HandleDetectionLoss()
        {
            // Switch to search mode when we lose the target
            if (currentGuardState == GuardState.Chasing)
            {
                ChangeGuardState(GuardState.Searching);
            }
            
            if (debugDetection)
                Debug.Log($"[GuardAI] {name} lost detection, switching to search");
        }
        
        private void ReportDetection(float detectionStrength)
        {
            if (AlertSystem.Instance != null && canCommunicate && detectionStrength >= searchThreshold)
            {
                var detectionData = DetectionData.Create(
                    DetectionSource.Guard,
                    transform.position,
                    currentTarget.Position,
                    detectionStrength,
                    zoneId,
                    guardId
                );
                
                AlertSystem.Instance.ReportDetection(detectionData);
                
                if (debugDetection)
                    Debug.Log($"[GuardAI] {name} reported detection to AlertSystem: {detectionStrength:F2}");
            }
        }
        
        private IDetectable FindPlayerTarget()
        {
            // Simple player finding - you might want to cache this
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                return player.GetComponent<IDetectable>();
            }
            return null;
        }
        #endregion
        
        #region State Management
        private void ChangeGuardState(GuardState newState)
        {
            if (currentGuardState == newState) return;
            
            previousGuardState = currentGuardState;
            currentGuardState = newState;
            stateTimer = 0f;
            
            OnStateChanged(previousGuardState, currentGuardState);
            
            if (debugGuardAI)
                Debug.Log($"[GuardAI] {name} changed state: {previousGuardState} -> {currentGuardState}");
        }
        
        private void OnStateChanged(GuardState fromState, GuardState toState)
        {
            // Stop any ongoing behavior coroutines
            if (behaviorCoroutine != null)
            {
                StopCoroutine(behaviorCoroutine);
                behaviorCoroutine = null;
            }
            
            // Handle state-specific initialization
            switch (toState)
            {
                case GuardState.Patrolling:
                    StartPatrolling();
                    break;
                    
                case GuardState.Searching:
                    StartSearching();
                    break;
                    
                case GuardState.Chasing:
                    StartChasing();
                    break;
                    
                case GuardState.Returning:
                    StartReturning();
                    break;
                    
                case GuardState.Alerted:
                    SetMovementMode(MovementMode.Walk);
                    break;
            }
        }
        #endregion
        
        #region Behavior Updates
        private void UpdateGuardBehavior()
        {
            switch (currentGuardState)
            {
                case GuardState.Patrolling:
                    UpdatePatrol();
                    break;
                    
                case GuardState.Searching:
                    UpdateSearch();
                    break;
                    
                case GuardState.Chasing:
                    UpdateChase();
                    break;
                    
                case GuardState.Returning:
                    UpdateReturn();
                    break;
                    
                case GuardState.Alerted:
                    UpdateAlerted();
                    break;
            }
        }
        
        private void UpdatePatrol()
        {
            if (guardType != GuardType.Patrol || patrolWaypoints == null || patrolWaypoints.Length == 0)
                return;
                
            if (isPaused) return;
            
            if (HasReachedDestination())
            {
                StartCoroutine(PatrolPause());
            }
        }
        
        private void UpdateSearch()
        {
            if (searchPoints.Count == 0)
            {
                GenerateSearchPoints();
                return;
            }
            
            if (HasReachedDestination())
            {
                currentSearchPointIndex++;
                if (currentSearchPointIndex >= searchPoints.Count || stateTimer > settings.searchTime)
                {
                    ChangeGuardState(GuardState.Returning);
                }
                else
                {
                    currentDestination = searchPoints[currentSearchPointIndex];
                }
            }
        }
        
        private void UpdateChase()
        {
            // Update destination to current player position if we can see them
            if (currentTarget != null && lastDetectionStrength >= chaseThreshold)
            {
                currentDestination = currentTarget.Position;
                lastKnownPlayerPos = currentTarget.Position;
            }
            else
            {
                // Lost sight, switch to search mode (handled by detection loss)
                // This is now handled in HandleDetectionLoss()
            }
        }
        
        private void UpdateReturn()
        {
            returnTimer += Time.deltaTime;
            
            if (HasReachedDestination() || returnTimer > settings.returnTimeout)
            {
                returnTimer = 0f;
                ChangeGuardState(GuardState.Patrolling);
            }
        }
        
        private void UpdateAlerted()
        {
            // Stand ready, rotate to scan area
            RotateTowardDirection(transform.forward);
        }
        #endregion
        
        #region State Implementations
        private void StartPatrolling()
        {
            SetMovementMode(MovementMode.Walk);
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                currentDestination = patrolWaypoints[currentWaypointIndex].position;
            }
        }
        
        private void StartSearching()
        {
            SetMovementMode(MovementMode.Run);
            searchPoints.Clear();
            currentSearchPointIndex = 0;
            PlayAudioClip(searchSounds);
        }
        
        private void StartChasing()
        {
            SetMovementMode(MovementMode.Run);
            if (currentTarget != null)
            {
                currentDestination = currentTarget.Position;
            }
            PlayAudioClip(alertSounds);
        }
        
        private void StartReturning()
        {
            SetMovementMode(MovementMode.Walk);
            returnTimer = 0f;
            
            // Return to nearest patrol waypoint
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                currentDestination = FindNearestWaypoint();
            }
        }
        #endregion
        
        #region Movement System
        private void UpdateMovement()
        {
            if (characterController == null) return;
            
            Vector3 targetPosition = currentDestination;
            Vector3 direction = (targetPosition - transform.position);
            direction.y = 0f; // Keep movement horizontal
            
            if (direction.magnitude > settings.stoppingDistance)
            {
                moveDirection = direction.normalized;
                isMoving = true;
                
                // Rotate toward movement direction
                RotateTowardDirection(moveDirection);
                
                // Apply movement
                float speed = GetCurrentSpeed();
                Vector3 movement = moveDirection * speed * Time.deltaTime;
                
                // Apply gravity
                movement.y = -9.81f * Time.deltaTime;
                
                characterController.Move(movement);
            }
            else
            {
                isMoving = false;
                moveDirection = Vector3.zero;
            }
        }
        
        private void RotateTowardDirection(Vector3 direction)
        {
            if (direction.magnitude < 0.1f) return;
            
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, 
                targetRotation, 
                settings.rotationSpeed * Time.deltaTime
            );
        }
        
        private float GetCurrentSpeed()
        {
            switch (currentMovementMode)
            {
                case MovementMode.Walk: return settings.walkSpeed;
                case MovementMode.Run: return settings.runSpeed;
                case MovementMode.Crouch: return settings.crouchSpeed;
                default: return settings.walkSpeed;
            }
        }
        
        private void SetMovementMode(MovementMode mode)
        {
            currentMovementMode = mode;
        }
        
        private bool HasReachedDestination()
        {
            return Vector3.Distance(transform.position, currentDestination) <= settings.stoppingDistance;
        }
        #endregion
        
        #region Patrol System
        private IEnumerator PatrolPause()
        {
            isPaused = true;
            
            float pauseTime = settings.patrolPauseTime;
            if (settings.randomizePatrolPause)
            {
                pauseTime += Random.Range(0f, settings.maxRandomPauseVariation);
            }
            
            yield return new WaitForSeconds(pauseTime);
            
            MoveToNextWaypoint();
            isPaused = false;
        }
        
        private void MoveToNextWaypoint()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length <= 1) return;
            
            if (reversePatrolOnEnd)
            {
                if (patrolReversed)
                {
                    currentWaypointIndex--;
                    if (currentWaypointIndex <= 0)
                    {
                        currentWaypointIndex = 0;
                        patrolReversed = false;
                    }
                }
                else
                {
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= patrolWaypoints.Length - 1)
                    {
                        currentWaypointIndex = patrolWaypoints.Length - 1;
                        patrolReversed = true;
                    }
                }
            }
            else
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
            }
            
            currentDestination = patrolWaypoints[currentWaypointIndex].position;
        }
        
        private Vector3 FindNearestWaypoint()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
                return transform.position;
            
            Vector3 nearest = patrolWaypoints[0].position;
            float nearestDistance = Vector3.Distance(transform.position, nearest);
            int nearestIndex = 0;
            
            for (int i = 1; i < patrolWaypoints.Length; i++)
            {
                float distance = Vector3.Distance(transform.position, patrolWaypoints[i].position);
                if (distance < nearestDistance)
                {
                    nearest = patrolWaypoints[i].position;
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }
            
            currentWaypointIndex = nearestIndex;
            return nearest;
        }
        #endregion
        
        #region Search System
        private void GenerateSearchPoints()
        {
            searchPoints.Clear();
            Vector3 searchCenter = lastKnownPlayerPos;
            
            for (int i = 0; i < settings.maxSearchPoints; i++)
            {
                float angle = (360f / settings.maxSearchPoints) * i;
                float distance = Random.Range(settings.searchRadius * 0.5f, settings.searchRadius);
                
                Vector3 searchPoint = searchCenter + 
                    Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
                
                // Simple ground check - you might want to add NavMesh sampling here
                searchPoint.y = transform.position.y;
                searchPoints.Add(searchPoint);
            }
            
            if (searchPoints.Count > 0)
            {
                currentDestination = searchPoints[0];
                currentSearchPointIndex = 0;
            }
        }
        #endregion
        
        #region Helper Methods
        private void UpdateTimers()
        {
            stateTimer += Time.deltaTime;
        }
        
        private void DebugGuardState()
        {
            Debug.Log($"[GuardAI] {name} - State: {currentGuardState}, Movement: {currentMovementMode}, " +
                     $"Moving: {isMoving}, Timer: {stateTimer:F1}, Detection: {lastDetectionStrength:F2}");
        }
        #endregion
        
        #region Audio System
        private void PlayAudioClip(AudioClip[] clips)
        {
            if (clips != null && clips.Length > 0)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                    QueueAudioClip(clip);
            }
        }
        
        private void QueueAudioClip(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;
            
            if (audioQueue.Count >= maxQueuedClips)
            {
                if (debugGuardAI)
                    Debug.LogWarning($"[GuardAI] {name} audio queue is full, skipping clip: {clip.name}");
                return;
            }
            
            audioQueue.Enqueue(clip);
            
            if (debugGuardAI)
                Debug.Log($"[GuardAI] {name} queued audio clip: {clip.name} (Queue size: {audioQueue.Count})");
            
            if (!isPlayingAudio)
            {
                StartAudioPlayback();
            }
        }
        
        private void StartAudioPlayback()
        {
            if (audioPlaybackCoroutine != null)
            {
                StopCoroutine(audioPlaybackCoroutine);
            }
            
            audioPlaybackCoroutine = StartCoroutine(AudioPlaybackCoroutine());
        }
        
        private IEnumerator AudioPlaybackCoroutine()
        {
            isPlayingAudio = true;
            
            while (audioQueue.Count > 0)
            {
                AudioClip currentClip = audioQueue.Dequeue();
                
                if (currentClip != null && audioSource != null)
                {
                    if (debugGuardAI)
                        Debug.Log($"[GuardAI] {name} playing audio clip: {currentClip.name}");
                    
                    audioSource.PlayOneShot(currentClip);
                    
                    float waitTime = currentClip.length + audioOffsetTime;
                    yield return new WaitForSeconds(waitTime);
                }
            }
            
            isPlayingAudio = false;
            audioPlaybackCoroutine = null;
            
            if (debugGuardAI)
                Debug.Log($"[GuardAI] {name} finished audio playback queue");
        }
        
        private void ClearAudioQueue()
        {
            audioQueue.Clear();
            
            if (audioPlaybackCoroutine != null)
            {
                StopCoroutine(audioPlaybackCoroutine);
                audioPlaybackCoroutine = null;
            }
            
            isPlayingAudio = false;
            
            if (debugGuardAI)
                Debug.Log($"[GuardAI] {name} cleared audio queue");
        }
        
        private void PlayAudioClipImmediate(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
                
                if (debugGuardAI)
                    Debug.Log($"[GuardAI] {name} played immediate audio clip: {clip.name}");
            }
        }
        
        private void PlayAudioClipImmediate(AudioClip[] clips)
        {
            if (clips != null && clips.Length > 0)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                    PlayAudioClipImmediate(clip);
            }
        }
        #endregion
        
        #region IAlertReceiver Implementation
        public virtual void OnAlertLevelChanged(AlertLevel newLevel, AlertLevel previousLevel, string zoneId)
        {
            // Only respond to alerts in our zone
            if (!string.IsNullOrEmpty(this.zoneId) && zoneId != this.zoneId) return;
            
            switch (newLevel)
            {
                case AlertLevel.Green:
                    if (currentGuardState != GuardState.Patrolling)
                        ChangeGuardState(GuardState.Returning);
                    break;
                    
                case AlertLevel.Suspicious:
                    if (currentGuardState == GuardState.Patrolling)
                        ChangeGuardState(GuardState.Alerted);
                    break;
                    
                case AlertLevel.Orange:
                    if (currentGuardState == GuardState.Patrolling || currentGuardState == GuardState.Alerted)
                        ChangeGuardState(GuardState.Searching);
                    break;
                    
                case AlertLevel.Red:
                    ChangeGuardState(GuardState.Chasing);
                    break;
            }
            
            if (debugGuardAI)
                Debug.Log($"[GuardAI] {name} alert level changed: {previousLevel} -> {newLevel}");
        }
        
        public virtual void OnDetectionInZone(DetectionData detectionData)
        {
            // Another entity in our zone detected something
            if (detectionData.detectorId != guardId)
            {
                lastKnownPlayerPos = detectionData.lastKnownPlayerPosition;
                
                // If we're not busy, help with the search
                if (currentGuardState == GuardState.Patrolling)
                {
                    ChangeGuardState(GuardState.Searching);
                }
                
                if (debugGuardAI)
                    Debug.Log($"[GuardAI] {name} received zone detection from {detectionData.detectorId}");
            }
        }
        
        public virtual void OnPlayerBehaviorChanged(PlayerBehavior newBehavior, PlayerBehavior previousBehavior)
        {
            // Adjust guard behavior based on player aggression
            if (newBehavior == PlayerBehavior.Aggressive)
            {
                // Player is being aggressive - be more alert
                if (currentGuardState == GuardState.Patrolling)
                {
                    ChangeGuardState(GuardState.Alerted);
                }
            }
            
            if (debugGuardAI)
                Debug.Log($"[GuardAI] {name} player behavior changed: {previousBehavior} -> {newBehavior}");
        }
        #endregion
        
        #region IZoneEntity Implementation
        public bool IsMultiZone => false;
        
        public void OnZoneEntered(string newZoneId)
        {
            zoneId = newZoneId;
            
            if (debugGuardAI)
                Debug.Log($"[GuardAI] {name} entered zone: {newZoneId}");
        }
        
        public void OnZoneExited(string oldZoneId)
        {
            if (debugGuardAI)
                Debug.Log($"[GuardAI] {name} exited zone: {oldZoneId}");
        }
        
        public string[] GetCurrentZones()
        {
            return new string[] { zoneId };
        }
        #endregion
        
        #region Gizmos
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            
            // Draw patrol waypoints
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                Gizmos.color = Color.blue;
                for (int i = 0; i < patrolWaypoints.Length; i++)
                {
                    if (patrolWaypoints[i] != null)
                    {
                        Gizmos.DrawWireSphere(patrolWaypoints[i].position, 0.5f);
                        
                        // Draw lines between waypoints
                        if (i < patrolWaypoints.Length - 1 && patrolWaypoints[i + 1] != null)
                        {
                            Gizmos.DrawLine(patrolWaypoints[i].position, patrolWaypoints[i + 1].position);
                        }
                    }
                }
                
                // Draw current destination
                if (Application.isPlaying)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(currentDestination, 0.3f);
                    Gizmos.DrawLine(transform.position, currentDestination);
                }
            }
            
            // Draw detection range
            if (Application.isPlaying)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
                Gizmos.DrawWireSphere(transform.position, detectionRange);
            }
            
            // Draw vision cone using VisionComponent if available
            if (Application.isPlaying && visionComponent != null)
            {
                Gizmos.color = currentGuardState == GuardState.Chasing ? Color.red : Color.yellow;
                Vector3 eyePos = visionComponent.EyePosition != null ? visionComponent.EyePosition.position : transform.position;
                Vector3 forward = visionComponent.EyePosition != null ? visionComponent.EyePosition.forward : transform.forward;
                Vector3 leftBoundary = Quaternion.Euler(0, -visionComponent.VisionAngle / 2, 0) * forward;
                Vector3 rightBoundary = Quaternion.Euler(0, visionComponent.VisionAngle / 2, 0) * forward;
                
                Gizmos.DrawRay(eyePos, leftBoundary * visionComponent.VisionRange);
                Gizmos.DrawRay(eyePos, rightBoundary * visionComponent.VisionRange);
                Gizmos.DrawRay(eyePos, forward * visionComponent.VisionRange);
            }
            
            // Draw search points
            if (Application.isPlaying && searchPoints.Count > 0)
            {
                Gizmos.color = Color.green;
                foreach (Vector3 point in searchPoints)
                {
                    Gizmos.DrawWireSphere(point, 0.2f);
                }
            }
            
            // Draw last known player position
            if (Application.isPlaying && lastKnownPlayerPos != Vector3.zero)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(lastKnownPlayerPos, 0.4f);
            }
        }
        #endregion
    }
}
