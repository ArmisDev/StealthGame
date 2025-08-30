using UnityEngine;
using System.Collections;

namespace StealthSystem
{
    /// <summary>
    /// Security Camera AI implementation using the modular detection framework.
    /// Features sweep patterns, brief suspicious phase, and instant alarm triggering.
    /// </summary>
    public class SecurityCamera : DetectorComponent
    {
        #region Serialized Fields
        [Header("Camera Vision")]
        [SerializeField] private float visionRange = 15f;
        [SerializeField] private float visionAngle = 60f;
        [SerializeField] private LayerMask visionBlockingLayers = -1;
        [SerializeField] private Transform cameraHead;
        [SerializeField] private Transform searchOrigin;
        [SerializeField] private Camera viewCamera;
        
        [Header("Sweep Pattern")]
        [SerializeField] private CameraSweepType sweepType = CameraSweepType.LeftRight;
        [SerializeField] private float sweepAngle = 90f;
        [SerializeField] private float sweepSpeed = 30f; // degrees per second
        [SerializeField] private float sweepPauseTime = 1f;
        [SerializeField] private bool randomizePauseTime = false;
        
        [Header("Detection Response")]
        [SerializeField] private float focusTime = 0.5f; // Time to "focus" on suspicious area
        [SerializeField] private float confirmationTime = 1.5f; // Suspicious phase duration
        [SerializeField] private float focusZoomFOV = 30f; // FOV when focusing
        [SerializeField] private AudioClip focusSound;
        [SerializeField] private AudioClip alarmSound;
        
        [Header("Visual Indicators")]
        [SerializeField] private Light statusLight;
        [SerializeField] private Color normalColor = Color.green;
        [SerializeField] private Color suspiciousColor = Color.yellow;
        [SerializeField] private Color alertColor = Color.red;
        [SerializeField] private float blinkSpeed = 2f;

        [Header("Vision Cone Line Renderer")]
        [SerializeField] private bool showSearchRange = true;
        [SerializeField] private Material coneLineMaterial;
        [SerializeField] private float lineWidth = 0.02f;
        [SerializeField] private int coneResolution = 16;
        [SerializeField] private bool fadeWithDistance = true;

        private LineRenderer[] coneLines;
        private GameObject coneParent;
        
        [Header("Camera States")]
        [SerializeField] private bool startDisabled = false;
        [SerializeField] private bool canBeDisabled = true;
        [SerializeField] private float disabledDuration = 30f;
        
        [Header("Debug")]
        [SerializeField] private bool showVisionGizmos = true;
        [SerializeField] private bool showSweepGizmos = true;
        #endregion
        
        #region Camera States
        public enum CameraState
        {
            Sweeping,       // Normal sweep pattern
            Focusing,       // Brief focus on suspicious area
            Suspicious,     // Confirming detection
            Alert,          // Player confirmed, alarm triggered
            Disabled        // Camera temporarily disabled
        }
        
        public enum CameraSweepType
        {
            LeftRight,      // Sweep left and right
            Continuous,     // Continuous rotation in one direction
            Fixed,          // No movement, fixed direction
            Custom          // Custom waypoint-based sweep
        }
        #endregion
        
        #region Private Fields
        // Components
        private AudioSource audioSource;
        private Coroutine sweepRoutine;
        private Coroutine focusRoutine;
        private Coroutine lightBlinkRoutine;
        
        // Sweep state
        private float currentRotationY;
        private float targetRotationY;
        private bool sweepingRight = true;
        private float sweepStartAngle;
        private float sweepEndAngle;
        private bool isPaused = false;
        private float pauseTimer = 0f;
        
        // Detection state
        private CameraState cameraState = CameraState.Sweeping;
        private Vector3 suspiciousPosition;
        private float originalFOV;
        private bool isDisabled = false;
        
        // Alert response
        private AlertLevel responseAlertLevel = AlertLevel.Green;
        #endregion
        
        #region Properties
        public CameraState CurrentCameraState => cameraState;
        public bool IsDisabled => isDisabled;
        public bool IsDetecting => cameraState == CameraState.Focusing || cameraState == CameraState.Suspicious;
        #endregion
        
        #region Unity Lifecycle
        protected override void Awake()
        {
            base.Awake();
            
            audioSource = GetComponent<AudioSource>();
            
            // Set up camera head if not assigned
            if (cameraHead == null)
            {
                cameraHead = transform;
            }
            
            // Set up view camera
            if (viewCamera == null)
            {
                viewCamera = GetComponentInChildren<Camera>();
            }
            
            if (viewCamera != null)
            {
                originalFOV = viewCamera.fieldOfView;
            }
            
            // Initialize rotation
            currentRotationY = transform.eulerAngles.y;
            CalculateSweepAngles();
        }
        
        protected override void Start()
        {
            base.Start();

            SetupVisionProjector();
            
            if (startDisabled)
            {
                DisableCamera(disabledDuration);
            }
            else
            {
                StartSweepBehavior();
            }
            
            UpdateStatusLight();
        }
        
        protected override void Update()
        {
            base.Update();
            
            if (!isDisabled)
            {
                UpdateSweepMovement();
            }

            UpdateVisionProjector();
        }
        #endregion
        
        #region DetectorComponent Implementation
        protected override float PerformDetectionCheck(IDetectable target, Vector3 targetPosition, float distance)
        {
            if (isDisabled || cameraHead == null)
                return 0f;
            
            // Only detect during sweep and focus states
            if (cameraState == CameraState.Disabled || cameraState == CameraState.Alert)
                return 0f;
            
            return CheckVisionDetection(target, targetPosition, distance);
        }
        
        protected override void OnAlertLevelChanged(AlertLevel newLevel)
        {
            responseAlertLevel = newLevel;
            
            // Cameras don't change behavior based on zone alerts like guards do
            // They maintain their sweep patterns regardless of alert level
            UpdateStatusLight();
        }
        
        protected override void OnDetectionStateChanged(DetectionState oldState, DetectionState newState)
        {
            switch (newState)
            {
                case DetectionState.Suspicious:
                    StartSuspiciousSequence();
                    break;
                
                case DetectionState.Hostile:
                    TriggerAlarm();
                    break;
                
                case DetectionState.Unaware:
                    ReturnToSweep();
                    break;
            }
        }
        
        protected override DetectionSource GetDetectionSource()
        {
            return DetectionSource.Camera;
        }
        
        protected override void OnSharedDetection(DetectionData sharedDetection)
        {
            // Cameras can't investigate shared detections, but they can become more alert
            if (cameraState == CameraState.Sweeping)
            {
                // Briefly focus toward the detection area if it's visible
                Vector3 detectionDirection = (sharedDetection.lastKnownPlayerPosition - transform.position).normalized;
                float angleToDetection = Vector3.Angle(transform.forward, detectionDirection);
                
                if (angleToDetection <= sweepAngle)
                {
                    StartCoroutine(BriefFocusToward(sharedDetection.lastKnownPlayerPosition));
                }
            }
        }
        #endregion
        
        #region Vision Detection
        private float CheckVisionDetection(IDetectable target, Vector3 targetPosition, float distance)
        {
            Vector3 cameraPos = searchOrigin.position;
            Vector3 directionToTarget = (targetPosition - cameraPos).normalized;
            
            // Check if target is in camera's field of view
            float angleToTarget = Vector3.Angle(searchOrigin.forward, directionToTarget);
            
            // Use current FOV for detection angle (normal or focused)
            float currentFOV = viewCamera != null ? viewCamera.fieldOfView : visionAngle;
            if (angleToTarget > currentFOV * 0.5f)
                return 0f;
            
            // Distance check
            if (distance > visionRange)
                return 0f;
            
            // Line of sight check
            if (!HasLineOfSight(cameraPos, targetPosition))
                return 0f;
            
            // Calculate detection strength
            return CalculateVisionDetectionStrength(target, targetPosition, distance, angleToTarget, currentFOV * 0.5f);
        }
        
        private bool HasLineOfSight(Vector3 fromPosition, Vector3 toPosition)
        {
            Vector3 direction = toPosition - fromPosition;
            float distance = direction.magnitude;
            
            if (Physics.Raycast(fromPosition, direction.normalized, out RaycastHit hit, distance, visionBlockingLayers))
            {
                // Check if we hit the target or an obstruction
                return hit.collider.CompareTag("Player");
            }
            
            return true;
        }
        
        private float CalculateVisionDetectionStrength(IDetectable target, Vector3 targetPosition, float distance, float angle, float maxAngle)
        {
            // Base detection strength
            float strength = 1f;
            
            // Distance factor
            float distanceFactor = 1f - (distance / visionRange);
            strength *= distanceFactor;
            
            // Angle factor (center of view = better detection)
            float angleFactor = 1f - (angle / maxAngle);
            strength *= angleFactor;
            
            // Target visibility
            strength *= target.VisibilityLevel;
            
            // Target size modifier
            strength *= target.SizeModifier;
            
            // Cameras have consistent detection regardless of alert level
            // But they're more sensitive when focusing
            if (cameraState == CameraState.Focusing)
            {
                strength *= 1.5f;
            }
            
            return Mathf.Clamp01(strength);
        }
        #endregion
        
        #region Camera State Management
        private void ChangeCameraState(CameraState newState)
        {
            if (cameraState == newState) return;
            
            CameraState oldState = cameraState;
            cameraState = newState;
            
            OnCameraStateChanged(oldState, newState);
            
            if (debugDetection)
                Debug.Log($"[SecurityCamera] {DetectorId} state: {oldState} -> {newState}");
        }
        
        private void OnCameraStateChanged(CameraState oldState, CameraState newState)
        {
            // Stop current coroutines
            if (focusRoutine != null)
            {
                StopCoroutine(focusRoutine);
                focusRoutine = null;
            }
            
            // Handle state transitions
            switch (newState)
            {
                case CameraState.Sweeping:
                    StartSweepBehavior();
                    ResetCameraFOV();
                    break;
                
                case CameraState.Focusing:
                    StopSweepBehavior();
                    PlayAudioClip(focusSound);
                    break;
                
                case CameraState.Suspicious:
                    // Continue focus behavior but start confirmation timer
                    break;
                
                case CameraState.Alert:
                    PlayAudioClip(alarmSound);
                    StopSweepBehavior();
                    break;
                
                case CameraState.Disabled:
                    StopSweepBehavior();
                    break;
            }
            
            UpdateStatusLight();
            UpdateVisionProjector();
        }
        
        private void StartSuspiciousSequence()
        {
            suspiciousPosition = lastKnownPlayerPosition;
            ChangeCameraState(CameraState.Focusing);
            focusRoutine = StartCoroutine(SuspiciousSequenceRoutine());
        }
        
        private IEnumerator SuspiciousSequenceRoutine()
        {
            // Focus phase - camera zooms/focuses on suspicious area
            ChangeCameraState(CameraState.Focusing);
            
            // Turn camera toward suspicious position
            Vector3 focusDirection = (suspiciousPosition - cameraHead.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(focusDirection);
            
            // Zoom in camera
            if (viewCamera != null)
            {
                float startFOV = viewCamera.fieldOfView;
                float elapsed = 0f;
                
                while (elapsed < focusTime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / focusTime;
                    
                    // Smoothly zoom and rotate
                    viewCamera.fieldOfView = Mathf.Lerp(startFOV, focusZoomFOV, t);
                    cameraHead.rotation = Quaternion.Slerp(cameraHead.rotation, targetRotation, t);
                    
                    yield return null;
                }
            }
            
            // Suspicious confirmation phase
            ChangeCameraState(CameraState.Suspicious);
            float confirmationTimer = confirmationTime;
            
            while (confirmationTimer > 0f && cameraState == CameraState.Suspicious)
            {
                // Continue checking if we can still see the target
                if (currentTarget != null)
                {
                    float currentStrength = TryDetect(currentTarget, currentTarget.Position);
                    if (currentStrength <= 0.1f)
                    {
                        // Lost target during suspicious phase
                        ReturnToSweep();
                        yield break;
                    }
                }
                
                confirmationTimer -= Time.deltaTime;
                yield return null;
            }
            
            // If we reach here, confirmation is complete -> trigger alarm
            if (cameraState == CameraState.Suspicious)
            {
                TriggerAlarm();
            }
        }
        
        private void TriggerAlarm()
        {
            ChangeCameraState(CameraState.Alert);
            
            // Keep camera focused on last known position
            // if (suspiciousPosition != Vector3.zero)
            // {
            //     Vector3 alertDirection = (suspiciousPosition - cameraHead.position).normalized;
            //     cameraHead.rotation = Quaternion.LookRotation(alertDirection);
            // }
        }
        
        private void ReturnToSweep()
        {
            ChangeCameraState(CameraState.Sweeping);
        }
        #endregion
        
        #region Sweep Behavior
        private void StartSweepBehavior()
        {
            if (sweepType == CameraSweepType.Fixed || isDisabled)
                return;
            
            sweepRoutine = StartCoroutine(SweepRoutine());
        }
        
        private void StopSweepBehavior()
        {
            if (sweepRoutine != null)
            {
                StopCoroutine(sweepRoutine);
                sweepRoutine = null;
            }
        }
        
        private IEnumerator SweepRoutine()
        {
            while (cameraState == CameraState.Sweeping && !isDisabled)
            {
                switch (sweepType)
                {
                    case CameraSweepType.LeftRight:
                        yield return StartCoroutine(LeftRightSweep());
                        break;
                    
                    case CameraSweepType.Continuous:
                        yield return StartCoroutine(ContinuousSweep());
                        break;
                }
            }
        }
        
        private IEnumerator LeftRightSweep()
        {
            while (cameraState == CameraState.Sweeping)
            {
                // Determine target angle
                targetRotationY = sweepingRight ? sweepEndAngle : sweepStartAngle;
                
                // Rotate to target
                while (Mathf.Abs(currentRotationY - targetRotationY) > 1f && cameraState == CameraState.Sweeping)
                {
                    currentRotationY = Mathf.MoveTowards(currentRotationY, targetRotationY, sweepSpeed * Time.deltaTime);
                    yield return null;
                }
                
                // Pause at end of sweep
                float pauseTime = randomizePauseTime ? 
                    sweepPauseTime + Random.Range(-0.5f, 0.5f) : 
                    sweepPauseTime;
                
                isPaused = true;
                pauseTimer = pauseTime;
                
                while (pauseTimer > 0f && cameraState == CameraState.Sweeping)
                {
                    pauseTimer -= Time.deltaTime;
                    yield return null;
                }
                
                isPaused = false;
                
                // Switch direction
                sweepingRight = !sweepingRight;
            }
        }
        
        private IEnumerator ContinuousSweep()
        {
            while (cameraState == CameraState.Sweeping)
            {
                currentRotationY += sweepSpeed * Time.deltaTime;
                if (currentRotationY >= 360f) currentRotationY -= 360f;
                yield return null;
            }
        }
        
        private void UpdateSweepMovement()
        {
            if (cameraHead != null && cameraState == CameraState.Sweeping)
            {
                Vector3 currentRotation = cameraHead.eulerAngles;
                currentRotation.y = currentRotationY;
                cameraHead.eulerAngles = currentRotation;
            }
        }
        
        private void CalculateSweepAngles()
        {
            float baseAngle = transform.eulerAngles.y;
            sweepStartAngle = baseAngle - (sweepAngle * 0.5f);
            sweepEndAngle = baseAngle + (sweepAngle * 0.5f);
            
            // Normalize angles
            if (sweepStartAngle < 0f) sweepStartAngle += 360f;
            if (sweepEndAngle >= 360f) sweepEndAngle -= 360f;
        }
        
        private IEnumerator BriefFocusToward(Vector3 focusPosition)
        {
            Vector3 originalForward = cameraHead.forward;
            Vector3 focusDirection = (focusPosition - cameraHead.position).normalized;
            
            // Briefly look toward the focus position
            float focusTimer = 1f;
            while (focusTimer > 0f && cameraState == CameraState.Sweeping)
            {
                cameraHead.forward = Vector3.Slerp(cameraHead.forward, focusDirection, 2f * Time.deltaTime);
                focusTimer -= Time.deltaTime;
                yield return null;
            }
            
            // Return to normal sweep
            yield return new WaitForSeconds(0.5f);
        }
        #endregion
        
        #region Camera Control
        /// <summary>
        /// Disable the camera for a duration (hacking, EMP, etc.)
        /// </summary>
        public void DisableCamera(float duration)
        {
            if (!canBeDisabled) return;
            
            StartCoroutine(DisableCameraRoutine(duration));
        }
        
        private IEnumerator DisableCameraRoutine(float duration)
        {
            bool wasActive = IsActive;
            
            isDisabled = true;
            IsActive = false;
            ChangeCameraState(CameraState.Disabled);
            
            yield return new WaitForSeconds(duration);
            
            isDisabled = false;
            IsActive = wasActive;
            
            if (wasActive)
            {
                ReturnToSweep();
            }
            
            if (debugDetection)
                Debug.Log($"[SecurityCamera] {DetectorId} re-enabled after {duration} seconds");
        }
        
        /// <summary>
        /// Permanently disable/enable the camera
        /// </summary>
        public void SetCameraEnabled(bool enabled)
        {
            if (enabled && isDisabled)
            {
                isDisabled = false;
                ReturnToSweep();
            }
            else if (!enabled && !isDisabled)
            {
                isDisabled = true;
                ChangeCameraState(CameraState.Disabled);
            }
            
            IsActive = enabled;
        }
        
        private void ResetCameraFOV()
        {
            if (viewCamera != null && originalFOV > 0f)
            {
                viewCamera.fieldOfView = originalFOV;
            }
        }
        #endregion
        
        #region Visual Indicators
        private void UpdateStatusLight()
        {
            if (statusLight == null) return;
            
            // Stop current blink routine
            if (lightBlinkRoutine != null)
            {
                StopCoroutine(lightBlinkRoutine);
                lightBlinkRoutine = null;
            }
            
            if (isDisabled)
            {
                statusLight.color = Color.black;
                statusLight.enabled = false;
                return;
            }
            
            statusLight.enabled = true;
            
            // Determine color based on BOTH camera state AND zone alert level
            Color targetColor = DetermineStatusLightColor();
            
            // Determine if we should blink
            bool shouldBlink = ShouldBlinkLight();
            
            if (shouldBlink)
            {
                lightBlinkRoutine = StartCoroutine(BlinkLight(targetColor));
            }
            else
            {
                statusLight.color = targetColor;
            }
        }

        private Color DetermineStatusLightColor()
        {
            // Camera's own state takes precedence when actively detecting
            switch (cameraState)
            {
                case CameraState.Alert:
                    return alertColor;
                
                case CameraState.Suspicious:
                case CameraState.Focusing:
                    return suspiciousColor;
                
                case CameraState.Sweeping:
                    // When sweeping, show zone alert level
                    return responseAlertLevel switch
                    {
                        AlertLevel.Red => alertColor,
                        AlertLevel.Orange => suspiciousColor,
                        AlertLevel.Suspicious => suspiciousColor,
                        _ => normalColor
                    };
                
                default:
                    return normalColor;
            }
        }

        private bool ShouldBlinkLight()
        {
            // Blink when camera is actively detecting OR when zone is on high alert
            return cameraState == CameraState.Suspicious || 
                cameraState == CameraState.Alert ||
                (cameraState == CameraState.Sweeping && responseAlertLevel >= AlertLevel.Orange);
        }
        
        private IEnumerator BlinkLight(Color blinkColor)
        {
            while (cameraState == CameraState.Suspicious || cameraState == CameraState.Alert)
            {
                statusLight.color = blinkColor;
                yield return new WaitForSeconds(0.5f / blinkSpeed);
                
                statusLight.color = Color.black;
                yield return new WaitForSeconds(0.5f / blinkSpeed);
            }
        }

        #region Vision Cone Line Renderer
        private void SetupVisionProjector()
        {
            if (!showSearchRange) return;
            
            // Create line renderers directly as children of the camera head
            coneLines = new LineRenderer[coneResolution + 3];
            
            // Create cone outline
            for (int i = 0; i < coneResolution; i++)
            {
                GameObject lineObj = new GameObject($"ConeLine_{i}");
                lineObj.transform.parent = searchOrigin;
                
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                SetupLineRenderer(lr);
                coneLines[i] = lr;
            }
            
            // Add center lines
            for (int i = 0; i < 3; i++)
            {
                GameObject centerLineObj = new GameObject($"CenterLine_{i}");
                centerLineObj.transform.parent = searchOrigin;
                
                LineRenderer lr = centerLineObj.AddComponent<LineRenderer>();
                SetupLineRenderer(lr);
                lr.startWidth = lineWidth * 0.5f;
                lr.endWidth = fadeWithDistance ? 0f : lineWidth * 0.5f;
                
                coneLines[coneResolution + i] = lr;
            }
            
            UpdateVisionProjector();
        }

        private void SetupLineRenderer(LineRenderer lr)
        {
            lr.material = coneLineMaterial;
            lr.startWidth = lineWidth;
            lr.endWidth = fadeWithDistance ? 0f : lineWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.sortingOrder = 1;
            
            // Disable shadows and lighting
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        private void UpdateVisionProjector()
        {
            if (coneLines == null || !showSearchRange) return;
            
            bool shouldShow = !isDisabled && cameraState != CameraState.Disabled;
            Color coneColor = DetermineProjectorColor();
            
            float currentFOV = viewCamera != null ? viewCamera.fieldOfView : visionAngle;
            
            // Update cone outline
            float angleStep = currentFOV / (coneResolution - 1);
            float startAngle = -currentFOV * 0.5f;
            
            for (int i = 0; i < coneResolution && i < coneLines.Length; i++)
            {
                if (coneLines[i] == null) continue;
                
                coneLines[i].enabled = shouldShow;
                if (!shouldShow) continue;
                
                coneLines[i].startColor = coneColor;
                coneLines[i].endColor = coneColor;
                
                float angle = startAngle + (angleStep * i);
                
                // Calculate direction in searchOrigin's local space
                Vector3 localDirection = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                Vector3 worldDirection = searchOrigin.TransformDirection(localDirection);
                
                // Use world space positions
                coneLines[i].SetPosition(0, searchOrigin.position);
                coneLines[i].SetPosition(1, searchOrigin.position + worldDirection * visionRange);
            }
            
            // Update center lines
            if (coneLines.Length > coneResolution + 2)
            {
                float[] centerAngles = { -currentFOV * 0.5f, 0f, currentFOV * 0.5f };
                
                for (int i = 0; i < 3 && coneResolution + i < coneLines.Length; i++)
                {
                    if (coneLines[coneResolution + i] == null) continue;
                    
                    coneLines[coneResolution + i].enabled = shouldShow;
                    if (!shouldShow) continue;
                    
                    // Make center lines slightly brighter
                    Color centerColor = coneColor;
                    centerColor.a = Mathf.Min(1f, centerColor.a * 1.5f);
                    coneLines[coneResolution + i].startColor = centerColor;
                    coneLines[coneResolution + i].endColor = centerColor;
                    
                    Vector3 localDirection = Quaternion.Euler(0, centerAngles[i], 0) * Vector3.forward;
                    Vector3 worldDirection = searchOrigin.TransformDirection(localDirection);
                    
                    coneLines[coneResolution + i].SetPosition(0, searchOrigin.position);
                    coneLines[coneResolution + i].SetPosition(1, searchOrigin.position + worldDirection * visionRange);
                }
            }
        }

        private Color DetermineProjectorColor()
        {
            Color baseColor = cameraState switch
            {
                CameraState.Alert => Color.red,
                CameraState.Suspicious => Color.yellow,
                CameraState.Focusing => Color.yellow,
                CameraState.Sweeping when responseAlertLevel >= AlertLevel.Orange => Color.yellow,
                CameraState.Sweeping when responseAlertLevel == AlertLevel.Suspicious => new Color(1f, 0.8f, 0f),
                _ => Color.white
            };
            
            // Set alpha based on state
            float alpha = cameraState switch
            {
                CameraState.Alert => 0.8f,
                CameraState.Suspicious => 0.7f,
                CameraState.Focusing => 0.7f,
                CameraState.Sweeping when responseAlertLevel >= AlertLevel.Orange => 0.6f,
                _ => 0.5f
            };
            
            return new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }

        public void SetVisionConeVisible(bool visible)
        {
            showSearchRange = visible;
            UpdateVisionProjector();
        }

        private void OnDestroy()
        {
            if (coneLines != null)
            {
                foreach (var line in coneLines)
                {
                    if (line != null && line.gameObject != null)
                    {
                        DestroyImmediate(line.gameObject);
                    }
                }
            }
        }
        #endregion
        #endregion
        
        #region Audio
        private void PlayAudioClip(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
        #endregion
        
        #region Debug Visualization
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            
            if (showVisionGizmos)
                DrawVisionGizmos();
            
            if (showSweepGizmos)
                DrawSweepGizmos();
        }
        
        private void DrawVisionGizmos()
        {
            if (cameraHead == null) return;
            
            Vector3 cameraPos = cameraHead.position;
            Vector3 forward = cameraHead.forward;
            
            // Vision cone
            float currentFOV = viewCamera != null ? viewCamera.fieldOfView : visionAngle;
            Gizmos.color = isDisabled ? Color.gray : new Color(1f, 0f, 0f, 0.3f);
            
            Vector3 leftBoundary = Quaternion.AngleAxis(-currentFOV * 0.5f, Vector3.up) * forward * visionRange;
            Vector3 rightBoundary = Quaternion.AngleAxis(currentFOV * 0.5f, Vector3.up) * forward * visionRange;
            
            Gizmos.DrawLine(cameraPos, cameraPos + leftBoundary);
            Gizmos.DrawLine(cameraPos, cameraPos + rightBoundary);
            Gizmos.DrawLine(cameraPos + leftBoundary, cameraPos + rightBoundary);
            
            // Detection range circle
            Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
            Gizmos.DrawWireSphere(cameraPos, visionRange);
        }
        
        private void DrawSweepGizmos()
        {
            if (sweepType == CameraSweepType.Fixed || sweepType == CameraSweepType.Continuous)
                return;
            
            Vector3 center = transform.position;
            
            // Sweep arc
            Gizmos.color = Color.cyan;
            Vector3 leftBoundary = Quaternion.AngleAxis(sweepStartAngle - transform.eulerAngles.y, Vector3.up) * transform.forward * visionRange * 0.5f;
            Vector3 rightBoundary = Quaternion.AngleAxis(sweepEndAngle - transform.eulerAngles.y, Vector3.up) * transform.forward * visionRange * 0.5f;
            
            Gizmos.DrawLine(center, center + leftBoundary);
            Gizmos.DrawLine(center, center + rightBoundary);
            
            // Current direction
            if (Application.isPlaying)
            {
                Gizmos.color = Color.yellow;
                Vector3 currentDirection = Quaternion.AngleAxis(currentRotationY - transform.eulerAngles.y, Vector3.up) * transform.forward * visionRange * 0.3f;
                Gizmos.DrawRay(center, currentDirection);
            }
        }
        #endregion
    }
}