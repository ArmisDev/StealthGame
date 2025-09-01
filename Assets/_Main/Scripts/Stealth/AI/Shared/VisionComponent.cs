using UnityEngine;

namespace StealthSystem
{
    /// <summary>
    /// Reusable vision component that can be shared between different AI types.
    /// Handles line-of-sight checks, angle calculations, and vision strength.
    /// </summary>
    public class VisionComponent : MonoBehaviour
    {
        [Header("Vision Settings")]
        [SerializeField] private float visionRange = 10f;
        [SerializeField] private float visionAngle = 90f;
        [SerializeField] private LayerMask visionBlockingLayers = -1;
        [SerializeField] private Transform eyePosition;
        
        [Header("Peripheral Vision")]
        [SerializeField] private bool hasPeripheralVision = false;
        [SerializeField] private float peripheralAngle = 150f;
        [SerializeField] private float peripheralRange = 5f;
        [SerializeField] private float peripheralSensitivity = 0.5f;
        
        public float VisionRange => visionRange;
        public float VisionAngle => visionAngle;
        public Transform EyePosition => eyePosition;
        
        private void Awake()
        {
            if (eyePosition == null)
            {
                eyePosition = transform;
            }
        }
        
        /// <summary>
        /// Check if a target is visible and calculate detection strength
        /// </summary>
        public float CheckVision(IDetectable target, Vector3 targetPosition)
        {
            if (target == null || eyePosition == null)
                return 0f;
            
            float distance = Vector3.Distance(eyePosition.position, targetPosition);
            Vector3 directionToTarget = (targetPosition - eyePosition.position).normalized;
            float angleToTarget = Vector3.Angle(eyePosition.forward, directionToTarget);
            
            // Determine which vision type applies
            bool inMainVision = angleToTarget <= visionAngle * 0.5f && distance <= visionRange;
            bool inPeripheralVision = hasPeripheralVision && 
                                     angleToTarget <= peripheralAngle * 0.5f && 
                                     distance <= peripheralRange;
            
            if (!inMainVision && !inPeripheralVision)
                return 0f;
            
            // Line of sight check
            if (!HasLineOfSight(eyePosition.position, targetPosition))
                return 0f;
            
            // Calculate detection strength
            float maxAngle = inMainVision ? visionAngle * 0.5f : peripheralAngle * 0.5f;
            float maxRange = inMainVision ? visionRange : peripheralRange;
            float sensitivity = inMainVision ? 1f : peripheralSensitivity;
            
            return CalculateDetectionStrength(target, distance, angleToTarget, maxAngle, maxRange, sensitivity);
        }
        
        /// <summary>
        /// Check if there's a clear line of sight to the target
        /// </summary>
        public bool HasLineOfSight(Vector3 fromPosition, Vector3 toPosition)
        {
            Vector3 direction = toPosition - fromPosition;
            float distance = direction.magnitude;
            
            if (Physics.Raycast(fromPosition, direction.normalized, out RaycastHit hit, distance, visionBlockingLayers))
            {
                return hit.collider.CompareTag("Player");
            }
            
            return true;
        }
        
        private float CalculateDetectionStrength(IDetectable target, float distance, float angle, float maxAngle, float maxRange, float sensitivity)
        {
            float strength = sensitivity;
            
            // Distance factor
            strength *= (1f - distance / maxRange);
            
            // Angle factor
            strength *= (1f - angle / maxAngle);
            
            // Target factors
            strength *= target.VisibilityLevel;
            strength *= target.SizeModifier;
            Debug.Log($"Detection strength: {Mathf.Clamp01(strength)}");
            
            // Clamp could remove the size modifier factor add when sprinting
            return Mathf.Clamp01(strength);
        }
        
        private void OnDrawGizmosSelected()
        {
            if (eyePosition == null) return;
            
            // Main vision cone
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            DrawVisionCone(eyePosition.position, eyePosition.forward, visionAngle, visionRange);
            
            // Peripheral vision
            if (hasPeripheralVision)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                DrawVisionCone(eyePosition.position, eyePosition.forward, peripheralAngle, peripheralRange);
            }
        }
        
        private void DrawVisionCone(Vector3 position, Vector3 forward, float angle, float range)
        {
            Vector3 leftBoundary = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up) * forward * range;
            Vector3 rightBoundary = Quaternion.AngleAxis(angle * 0.5f, Vector3.up) * forward * range;
            
            Gizmos.DrawLine(position, position + leftBoundary);
            Gizmos.DrawLine(position, position + rightBoundary);
            Gizmos.DrawLine(position + leftBoundary, position + rightBoundary);
        }
    }
}