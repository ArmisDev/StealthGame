using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace StealthSystem
{
    /// <summary>
    /// Base Guard AI that integrates with the existing stealth detection system.
    /// Extends DetectorComponent for automatic detection and alert integration.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class GuardAI : DetectorComponent, IAlertReceiver
    {
        #region Serialized Fields
        [Header("Guard Configuration")]
        [SerializeField] private GuardType guardType = GuardType.Patrol;
        [SerializeField] private GuardSettings settings = GuardSettings.CreateDefault();
        
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
        [SerializeField] private float audioOffsetTime = 0.5f; // Time to wait between audio clips
        [SerializeField] private int maxQueuedClips = 5; // Maximum clips that can be queued
        
        [Header("Debug")]
        [SerializeField] private bool debugGuardAI = false;
        [SerializeField] private bool drawGizmos = true;
        #endregion
        
        #region Private Fields
        // Components
        private CharacterController characterController;
        
        // State Management
        private GuardState currentGuardState = GuardState.Patrolling;
        private GuardState previousGuardState;
        private MovementMode currentMovementMode = MovementMode.Walk;
        
        // Patrol System
        private int currentWaypointIndex = 0;
        private bool patrolReversed = false;
        private Coroutine patrolCoroutine;
        private bool isPaused = false;
        
        // Search
        private Vector3 lastKnownPlayerPos;
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
        public GuardType Type => guardType;
        public GuardState CurrentState => currentGuardState;
        public MovementMode MovementMode => currentMovementMode;
        public bool IsMoving => isMoving && moveDirection.magnitude > 0.1f;
        public Vector3 CurrentDestination => currentDestination;
        #endregion
        
        #region Unity Lifecycle
        protected override void Awake()
        {
            base.Awake();
            characterController = GetComponent<CharacterController>();
            
            // Get or create VisionComponent
            if (visionComponent == null)
                visionComponent = GetComponent<VisionComponent>();
                
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }
        
        protected override void Start()
        {
            base.Start();
            InitializeGuard();
        }
        
        protected override void Update()
        {
            base.Update(); // Handles detection logic
            
            UpdateGuardBehavior();
            UpdateMovement();
            UpdateTimers();
            
            if (debugGuardAI)
                DebugGuardState();
        }
        
        private void OnDestroy()
        {
            ClearAudioQueue();
            StopAllCoroutines();
        }
        #endregion
        
        #region Initialization
        private void InitializeGuard()
        {
            // Set up detection parameters
            SetupDetection();
            
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
        
        private void SetupDetection()
        {
            // Get or add VisionComponent
            if (visionComponent == null)
                visionComponent = GetComponent<VisionComponent>();
                
            if (visionComponent == null)
            {
                Debug.LogWarning($"[GuardAI] {name} has no VisionComponent. Detection will be limited.");
            }
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
        
        protected virtual void OnStateChanged(GuardState fromState, GuardState toState)
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
            if (currentTarget != null)
            {
                currentDestination = currentTarget.Position;
                lastKnownPlayerPos = currentTarget.Position;
            }
            else
            {
                // Lost sight, switch to search mode
                ChangeGuardState(GuardState.Searching);
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
        
        private void LookAround()
        {
            // Simple look around behavior - rotate slowly
            float rotationAmount = 45f * Mathf.Sin(stateTimer * 2f);
            Vector3 lookDirection = Quaternion.Euler(0, rotationAmount, 0) * Vector3.forward;
            RotateTowardDirection(lookDirection);
        }
        
        private void PlayAudioClip(AudioClip[] clips)
        {
            if (clips != null && clips.Length > 0)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                    QueueAudioClip(clip);
            }
        }
        
        /// <summary>
        /// Queue an audio clip to be played in sequence
        /// </summary>
        private void QueueAudioClip(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;
            
            // Check if queue is full
            if (audioQueue.Count >= maxQueuedClips)
            {
                if (debugGuardAI)
                    Debug.LogWarning($"[GuardAI] {name} audio queue is full, skipping clip: {clip.name}");
                return;
            }
            
            // Add to queue
            audioQueue.Enqueue(clip);
            
            if (debugGuardAI)
                Debug.Log($"[GuardAI] {name} queued audio clip: {clip.name} (Queue size: {audioQueue.Count})");
            
            // Start playback if not already playing
            if (!isPlayingAudio)
            {
                StartAudioPlayback();
            }
        }
        
        /// <summary>
        /// Start the audio playback coroutine
        /// </summary>
        private void StartAudioPlayback()
        {
            if (audioPlaybackCoroutine != null)
            {
                StopCoroutine(audioPlaybackCoroutine);
            }
            
            audioPlaybackCoroutine = StartCoroutine(AudioPlaybackCoroutine());
        }
        
        /// <summary>
        /// Coroutine that handles sequential audio playback with offset timing
        /// </summary>
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
                    
                    // Play the clip
                    audioSource.PlayOneShot(currentClip);
                    
                    // Wait for the clip to finish plus offset time
                    float waitTime = currentClip.length + audioOffsetTime;
                    yield return new WaitForSeconds(waitTime);
                }
            }
            
            isPlayingAudio = false;
            audioPlaybackCoroutine = null;
            
            if (debugGuardAI)
                Debug.Log($"[GuardAI] {name} finished audio playback queue");
        }
        
        /// <summary>
        /// Clear the audio queue and stop current playback
        /// </summary>
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
        
        /// <summary>
        /// Check if audio is currently playing or queued
        /// </summary>
        public bool IsAudioActive => isPlayingAudio || audioQueue.Count > 0;
        
        /// <summary>
        /// Play an audio clip immediately, bypassing the queue (for urgent sounds)
        /// </summary>
        private void PlayAudioClipImmediate(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
                
                if (debugGuardAI)
                    Debug.Log($"[GuardAI] {name} played immediate audio clip: {clip.name}");
            }
        }
        
        /// <summary>
        /// Play an audio clip immediately from an array, bypassing the queue
        /// </summary>
        private void PlayAudioClipImmediate(AudioClip[] clips)
        {
            if (clips != null && clips.Length > 0)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                    PlayAudioClipImmediate(clip);
            }
        }
        
        private void DebugGuardState()
        {
            Debug.Log($"[GuardAI] {name} - State: {currentGuardState}, Movement: {currentMovementMode}, " +
                     $"Moving: {isMoving}, Timer: {stateTimer:F1}");
        }
        #endregion
        
        #region DetectorComponent Overrides
        protected override float PerformDetectionCheck(IDetectable target, Vector3 targetPosition, float distance)
        {
            // Use the VisionComponent for detection if available
            if (visionComponent != null)
            {
                return visionComponent.CheckVision(target, targetPosition);
            }
            
            // Fallback basic detection if no VisionComponent
            if (distance <= detectionRange)
            {
                Vector3 directionToTarget = (targetPosition - transform.position).normalized;
                float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);
                
                if (angleToTarget <= 45f) // Simple fallback angle
                {
                    // Basic line of sight check
                    if (Physics.Raycast(transform.position, directionToTarget, distance, ~0))
                        return 0f;
                    
                    return Mathf.Clamp01(1f - (distance / detectionRange));
                }
            }
            
            return 0f;
        }
        
        protected override void OnAlertLevelChanged(AlertLevel newLevel)
        {
            // This is handled by IAlertReceiver.OnAlertLevelChanged
            // but DetectorComponent also requires this abstract method
            if (debugGuardAI)
                Debug.Log($"[GuardAI] {name} received alert level change via DetectorComponent: {newLevel}");
        }
        
        protected override void OnDetectionStateChanged(DetectionState oldState, DetectionState newState)
        {
            // Sync DetectorComponent state changes with GuardAI state
            switch (newState)
            {
                case DetectionState.Suspicious:
                    if (currentGuardState == GuardState.Patrolling)
                        ChangeGuardState(GuardState.Searching);
                    break;
                    
                case DetectionState.Searching:
                    if (currentGuardState != GuardState.Chasing)
                        ChangeGuardState(GuardState.Searching);
                    break;
                    
                case DetectionState.Hostile:
                    ChangeGuardState(GuardState.Chasing);
                    break;
                    
                case DetectionState.Unaware:
                    if (currentGuardState != GuardState.Patrolling)
                        ChangeGuardState(GuardState.Returning);
                    break;
            }
            
            if (debugGuardAI)
                Debug.Log($"[GuardAI] {name} detection state changed: {oldState} -> {newState}");
        }
        
        public override void OnDetectionMade(DetectionData detectionData)
        {
            base.OnDetectionMade(detectionData);
            
            // Store last known player position
            lastKnownPlayerPos = detectionData.lastKnownPlayerPosition;
            
            // React based on detection strength
            if (detectionData.detectionStrength > 0.5f)
            {
                // High confidence detection - start chasing
                ChangeGuardState(GuardState.Chasing);
            }
            else
            {
                // Lower confidence - start searching
                if (currentGuardState == GuardState.Patrolling)
                {
                    ChangeGuardState(GuardState.Searching);
                }
            }
        }
        
        public override void OnDetectionLost()
        {
            base.OnDetectionLost();
            
            // Switch to search mode when we lose the target
            if (currentGuardState == GuardState.Chasing)
            {
                ChangeGuardState(GuardState.Searching);
            }
        }
        #endregion
        
        #region IAlertReceiver Implementation
        public virtual void OnAlertLevelChanged(AlertLevel newLevel, AlertLevel previousLevel, string zoneId)
        {
            // Only respond to alerts in our zone
            if (!string.IsNullOrEmpty(ZoneId) && zoneId != ZoneId) return;
            
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
        }
        
        public virtual void OnDetectionInZone(DetectionData detectionData)
        {
            // Another entity in our zone detected something
            if (detectionData.detectorId != DetectorId)
            {
                lastKnownPlayerPos = detectionData.lastKnownPlayerPosition;
                
                // If we're not busy, help with the search
                if (currentGuardState == GuardState.Patrolling)
                {
                    ChangeGuardState(GuardState.Searching);
                }
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
        }
        #endregion
    }
}