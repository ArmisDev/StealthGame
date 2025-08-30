//==============================================================================
// FILE: Assets/Scripts/Stealth/Systems/SecurityZone.cs
using UnityEngine;

namespace StealthSystem
{
    /// <summary>
    /// Defines a security zone area with boundaries and properties
    /// Can be box, sphere, or custom shaped zones
    /// </summary>
    public class SecurityZone : MonoBehaviour
    {
        #region Zone Types
        public enum ZoneShape
        {
            Box,        // Rectangular zone
            Sphere,     // Circular zone  
            Custom      // Custom collider shape
        }
        #endregion
        
        #region Serialized Fields
        [Header("Zone Identity")]
        [SerializeField] private string zoneId = "Zone_001";
        [SerializeField] private string zoneName = "Security Zone";
        [SerializeField] private string description = "";
        
        [Header("Zone Shape")]
        [SerializeField] private ZoneShape shape = ZoneShape.Box;
        [SerializeField] private Vector3 boxSize = new Vector3(10f, 4f, 10f);
        [SerializeField] private float sphereRadius = 5f;
        [SerializeField] private Collider customCollider;
        
        [Header("Zone Properties")]
        [SerializeField] private int securityLevel = 1; // Higher = more secure
        [SerializeField] private bool isRestrictedArea = false;
        [SerializeField] private bool allowsDisguises = true;
        [SerializeField] private Color zoneColor = Color.yellow;
        
        [Header("Adjacent Zones")]
        [SerializeField] private string[] manualAdjacentZones = new string[0];
        [SerializeField] private float adjacencyDistance = 2f;
        
        [Header("Debug")]
        [SerializeField] private bool showZoneBounds = true;
        [SerializeField] private bool showZoneInfo = false;
        #endregion
        
        #region Properties
        public string ZoneId => zoneId;
        public string ZoneName => zoneName;
        public string Description => description;
        public int SecurityLevel => securityLevel;
        public bool IsRestrictedArea => isRestrictedArea;
        public bool AllowsDisguises => allowsDisguises;
        public Color ZoneColor => zoneColor;
        public Vector3 Center => transform.position;
        #endregion
        
        #region Unity Lifecycle
        private void Start()
        {
            RegisterWithZoneManager();
            ValidateZoneSetup();
        }
        
        private void OnDestroy()
        {
            UnregisterFromZoneManager();
        }
        
        private void OnValidate()
        {
            // Ensure zone ID is valid
            if (string.IsNullOrEmpty(zoneId))
                zoneId = $"Zone_{GetInstanceID()}";
            
            // Ensure sizes are positive
            boxSize = new Vector3(
                Mathf.Max(0.1f, boxSize.x),
                Mathf.Max(0.1f, boxSize.y),
                Mathf.Max(0.1f, boxSize.z)
            );
            sphereRadius = Mathf.Max(0.1f, sphereRadius);
        }
        #endregion
        
        #region Zone Management
        private void RegisterWithZoneManager()
        {
            if (ZoneManager.HasInstance)
            {
                ZoneManager.Instance.RegisterZone(this);
            }
        }
        
        private void UnregisterFromZoneManager()
        {
            if (ZoneManager.HasInstance)
            {
                ZoneManager.Instance.UnregisterZone(zoneId);
            }
        }
        
        private void ValidateZoneSetup()
        {
            if (shape == ZoneShape.Custom && customCollider == null)
            {
                Debug.LogWarning($"[SecurityZone] {zoneId} is set to Custom shape but has no collider assigned");
            }
        }
        #endregion
        
        #region Spatial Queries
        /// <summary>
        /// Check if a point is inside this zone
        /// </summary>
        public bool ContainsPoint(Vector3 point)
        {
            switch (shape)
            {
                case ZoneShape.Box:
                    return ContainsPointBox(point);
                
                case ZoneShape.Sphere:
                    return ContainsPointSphere(point);
                
                case ZoneShape.Custom:
                    return ContainsPointCustom(point);
                
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Check if this zone is within a certain distance of a point
        /// </summary>
        public bool IsWithinDistance(Vector3 point, float distance)
        {
            return GetDistanceToBoundary(point) <= distance;
        }
        
        /// <summary>
        /// Get the distance from a point to the zone boundary
        /// </summary>
        public float GetDistanceToBoundary(Vector3 point)
        {
            switch (shape)
            {
                case ZoneShape.Box:
                    return GetDistanceToBoxBoundary(point);
                
                case ZoneShape.Sphere:
                    return GetDistanceToSphereBoundary(point);
                
                case ZoneShape.Custom:
                    return GetDistanceToCustomBoundary(point);
                
                default:
                    return float.MaxValue;
            }
        }
        
        /// <summary>
        /// Check if this zone is adjacent to another zone
        /// </summary>
        public bool IsAdjacentTo(SecurityZone otherZone)
        {
            // Check manual adjacency list first
            if (System.Array.Exists(manualAdjacentZones, id => id == otherZone.zoneId))
                return true;
            
            // Check distance-based adjacency
            float distance = GetDistanceToBoundary(otherZone.Center);
            return distance <= adjacencyDistance;
        }
        #endregion
        
        #region Shape-Specific Methods
        private bool ContainsPointBox(Vector3 point)
        {
            Vector3 localPoint = transform.InverseTransformPoint(point);
            Vector3 halfSize = boxSize * 0.5f;
            
            return Mathf.Abs(localPoint.x) <= halfSize.x &&
                   Mathf.Abs(localPoint.y) <= halfSize.y &&
                   Mathf.Abs(localPoint.z) <= halfSize.z;
        }
        
        private bool ContainsPointSphere(Vector3 point)
        {
            return Vector3.Distance(transform.position, point) <= sphereRadius;
        }
        
        private bool ContainsPointCustom(Vector3 point)
        {
            if (customCollider == null) return false;
            
            return customCollider.bounds.Contains(point);
        }
        
        private float GetDistanceToBoxBoundary(Vector3 point)
        {
            Vector3 localPoint = transform.InverseTransformPoint(point);
            Vector3 halfSize = boxSize * 0.5f;
            
            // If inside, return negative distance to edge
            if (ContainsPointBox(point))
            {
                float xDist = Mathf.Min(halfSize.x - Mathf.Abs(localPoint.x), halfSize.x + Mathf.Abs(localPoint.x));
                float yDist = Mathf.Min(halfSize.y - Mathf.Abs(localPoint.y), halfSize.y + Mathf.Abs(localPoint.y));
                float zDist = Mathf.Min(halfSize.z - Mathf.Abs(localPoint.z), halfSize.z + Mathf.Abs(localPoint.z));
                
                return -Mathf.Min(xDist, Mathf.Min(yDist, zDist));
            }
            
            // If outside, return distance to nearest face
            Vector3 clampedPoint = new Vector3(
                Mathf.Clamp(localPoint.x, -halfSize.x, halfSize.x),
                Mathf.Clamp(localPoint.y, -halfSize.y, halfSize.y),
                Mathf.Clamp(localPoint.z, -halfSize.z, halfSize.z)
            );
            
            Vector3 worldClampedPoint = transform.TransformPoint(clampedPoint);
            return Vector3.Distance(point, worldClampedPoint);
        }
        
        private float GetDistanceToSphereBoundary(Vector3 point)
        {
            float distanceToCenter = Vector3.Distance(transform.position, point);
            return distanceToCenter - sphereRadius;
        }
        
        private float GetDistanceToCustomBoundary(Vector3 point)
        {
            if (customCollider == null) return float.MaxValue;
            
            Vector3 closestPoint = customCollider.ClosestPoint(point);
            return Vector3.Distance(point, closestPoint);
        }
        #endregion
        
        #region Debug Visualization
        public void DrawZoneGizmos()
        {
            if (!showZoneBounds) return;
            
            Gizmos.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            
            switch (shape)
            {
                case ZoneShape.Box:
                    Gizmos.DrawCube(Vector3.zero, boxSize);
                    Gizmos.color = zoneColor;
                    Gizmos.DrawWireCube(Vector3.zero, boxSize);
                    break;
                
                case ZoneShape.Sphere:
                    Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                    Gizmos.DrawSphere(Vector3.zero, sphereRadius);
                    Gizmos.color = zoneColor;
                    Gizmos.DrawWireSphere(Vector3.zero, sphereRadius);
                    break;
                
                case ZoneShape.Custom:
                    if (customCollider != null)
                    {
                        Gizmos.color = zoneColor;
                        Gizmos.DrawWireCube(customCollider.bounds.center, customCollider.bounds.size);
                    }
                    break;
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            DrawZoneGizmos();
            
            // Draw zone info
            if (showZoneInfo)
            {
                #if UNITY_EDITOR
                Vector3 labelPos = transform.position + Vector3.up * 2f;
                UnityEditor.Handles.Label(labelPos, $"{zoneName}\n{zoneId}\nSecurity: {securityLevel}");
                #endif
            }
        }
        #endregion
        
        #region Public Utility Methods
        /// <summary>
        /// Get the closest point on the zone boundary to a given point
        /// </summary>
        public Vector3 GetClosestPointOnBoundary(Vector3 point)
        {
            switch (shape)
            {
                case ZoneShape.Sphere:
                    Vector3 direction = (point - transform.position).normalized;
                    return transform.position + direction * sphereRadius;
                
                case ZoneShape.Custom:
                    if (customCollider != null)
                        return customCollider.ClosestPoint(point);
                    break;
                
                case ZoneShape.Box:
                    Vector3 localPoint = transform.InverseTransformPoint(point);
                    Vector3 halfSize = boxSize * 0.5f;
                    Vector3 clampedLocal = new Vector3(
                        Mathf.Clamp(localPoint.x, -halfSize.x, halfSize.x),
                        Mathf.Clamp(localPoint.y, -halfSize.y, halfSize.y),
                        Mathf.Clamp(localPoint.z, -halfSize.z, halfSize.z)
                    );
                    return transform.TransformPoint(clampedLocal);
            }
            
            return transform.position;
        }
        
        /// <summary>
        /// Get a random point inside this zone
        /// </summary>
        public Vector3 GetRandomPointInZone()
        {
            switch (shape)
            {
                case ZoneShape.Box:
                    Vector3 randomLocal = new Vector3(
                        Random.Range(-boxSize.x * 0.5f, boxSize.x * 0.5f),
                        Random.Range(-boxSize.y * 0.5f, boxSize.y * 0.5f),
                        Random.Range(-boxSize.z * 0.5f, boxSize.z * 0.5f)
                    );
                    return transform.TransformPoint(randomLocal);
                
                case ZoneShape.Sphere:
                    Vector3 randomDirection = Random.insideUnitSphere * sphereRadius;
                    return transform.position + randomDirection;
                
                case ZoneShape.Custom:
                    if (customCollider != null)
                    {
                        Bounds bounds = customCollider.bounds;
                        Vector3 randomPoint;
                        int attempts = 0;
                        
                        // Try up to 100 times to find a point inside the custom collider
                        do
                        {
                            randomPoint = new Vector3(
                                Random.Range(bounds.min.x, bounds.max.x),
                                Random.Range(bounds.min.y, bounds.max.y),
                                Random.Range(bounds.min.z, bounds.max.z)
                            );
                            attempts++;
                        }
                        while (!ContainsPoint(randomPoint) && attempts < 100);
                        
                        return randomPoint;
                    }
                    break;
            }
            
            return transform.position;
        }
        #endregion
    }
}