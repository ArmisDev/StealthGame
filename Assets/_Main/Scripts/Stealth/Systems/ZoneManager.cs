using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace StealthSystem
{
    /// <summary>
    /// Central manager for all security zones. Handles zone registration, entity tracking,
    /// and spatial queries for the alert system.
    /// </summary>
    public class ZoneManager : MonoBehaviour
    {
        #region Singleton
        private static ZoneManager instance;
        public static ZoneManager Instance 
        { 
            get 
            {
                if (instance == null)
                    instance = FindAnyObjectByType<ZoneManager>();
                return instance;
            }
        }
        
        public static bool HasInstance => instance != null;
        #endregion
        
        #region Serialized Fields
        [Header("Zone Settings")]
        [SerializeField] private bool autoFindZones = true;
        [SerializeField] private bool enableZoneDebugging = true;
        [SerializeField] private Color defaultZoneColor = Color.yellow;
        
        [Header("Player Tracking")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private float playerCheckInterval = 0.1f;
        
        [Header("Debug")]
        [SerializeField] private bool showZoneBounds = true;
        [SerializeField] private bool logZoneTransitions = false;
        #endregion
        
        #region Private Fields
        // Zone storage
        private Dictionary<string, SecurityZone> registeredZones = new Dictionary<string, SecurityZone>();
        private Dictionary<string, List<IZoneEntity>> zoneEntities = new Dictionary<string, List<IZoneEntity>>();
        
        // Player tracking
        private string currentPlayerZone = "";
        private string previousPlayerZone = "";
        private float lastPlayerCheck = 0f;
        
        // Entity tracking
        private List<IZoneEntity> trackedEntities = new List<IZoneEntity>();
        
        // Zone adjacency (for alert propagation)
        private Dictionary<string, List<string>> adjacentZones = new Dictionary<string, List<string>>();
        #endregion
        
        #region Events
        /// <summary>Called when player enters a new zone</summary>
        public static event Action<string, string> OnPlayerZoneChanged; // newZone, oldZone
        
        /// <summary>Called when any entity enters a zone</summary>
        public static event Action<IZoneEntity, string> OnEntityEnteredZone;
        
        /// <summary>Called when any entity exits a zone</summary>
        public static event Action<IZoneEntity, string> OnEntityExitedZone;
        
        /// <summary>Called when a new zone is registered</summary>
        public static event Action<SecurityZone> OnZoneRegistered;
        #endregion
        
        #region Properties
        /// <summary>Current zone the player is in</summary>
        public string CurrentPlayerZone => currentPlayerZone;
        
        /// <summary>Previous zone the player was in</summary>
        public string PreviousPlayerZone => previousPlayerZone;
        
        /// <summary>Number of registered zones</summary>
        public int ZoneCount => registeredZones.Count;
        
        /// <summary>All registered zone IDs</summary>
        public IEnumerable<string> ZoneIds => registeredZones.Keys;
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
            InitializeZoneManager();
        }
        
        private void Update()
        {
            UpdatePlayerZoneTracking();
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
        private void InitializeZoneManager()
        {
            // Find player if not assigned
            if (autoFindPlayer && playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
            }
            
            // Auto-find zones in scene
            if (autoFindZones)
            {
                FindAndRegisterSceneZones();
            }
            
            Debug.Log($"[ZoneManager] Initialized with {registeredZones.Count} zones");
        }
        
        private void FindAndRegisterSceneZones()
        {
            SecurityZone[] sceneZones = FindObjectsByType<SecurityZone>(FindObjectsSortMode.None);
            
            foreach (var zone in sceneZones)
            {
                RegisterZone(zone);
            }
            
            Debug.Log($"[ZoneManager] Auto-registered {sceneZones.Length} zones from scene");
        }
        #endregion
        
        #region Zone Registration
        /// <summary>
        /// Register a security zone with the manager
        /// </summary>
        public bool RegisterZone(SecurityZone zone)
        {
            if (zone == null)
            {
                Debug.LogError("[ZoneManager] Cannot register null zone");
                return false;
            }
            
            string zoneId = zone.ZoneId;
            if (string.IsNullOrEmpty(zoneId))
            {
                Debug.LogError($"[ZoneManager] Zone {zone.name} has empty ZoneId");
                return false;
            }
            
            if (registeredZones.ContainsKey(zoneId))
            {
                Debug.LogWarning($"[ZoneManager] Zone '{zoneId}' already registered. Replacing...");
            }
            
            registeredZones[zoneId] = zone;
            zoneEntities[zoneId] = new List<IZoneEntity>();
            
            // Set up zone adjacency
            UpdateZoneAdjacency(zone);
            
            OnZoneRegistered?.Invoke(zone);
            
            if (enableZoneDebugging)
                Debug.Log($"[ZoneManager] Registered zone: {zoneId}");
            
            return true;
        }
        
        /// <summary>
        /// Unregister a zone
        /// </summary>
        public bool UnregisterZone(string zoneId)
        {
            if (!registeredZones.ContainsKey(zoneId))
                return false;
            
            // Remove all entities from this zone
            if (zoneEntities.ContainsKey(zoneId))
            {
                var entities = zoneEntities[zoneId].ToList();
                foreach (var entity in entities)
                {
                    UnregisterEntity(entity);
                }
                zoneEntities.Remove(zoneId);
            }
            
            // Remove adjacency data
            adjacentZones.Remove(zoneId);
            foreach (var adjacent in adjacentZones.Values)
            {
                adjacent.Remove(zoneId);
            }
            
            registeredZones.Remove(zoneId);
            
            if (enableZoneDebugging)
                Debug.Log($"[ZoneManager] Unregistered zone: {zoneId}");
            
            return true;
        }
        
        /// <summary>
        /// Get a registered zone by ID
        /// </summary>
        public SecurityZone GetZone(string zoneId)
        {
            registeredZones.TryGetValue(zoneId, out SecurityZone zone);
            return zone;
        }
        
        /// <summary>
        /// Check if a zone is registered
        /// </summary>
        public bool HasZone(string zoneId)
        {
            return registeredZones.ContainsKey(zoneId);
        }
        #endregion
        
        #region Entity Management
        /// <summary>
        /// Register an entity for zone tracking
        /// </summary>
        public void RegisterEntity(IZoneEntity entity)
        {
            if (entity == null || trackedEntities.Contains(entity))
                return;
            
            trackedEntities.Add(entity);
            
            // Determine initial zone
            string initialZone = DetermineEntityZone(entity);
            if (!string.IsNullOrEmpty(initialZone))
            {
                AddEntityToZone(entity, initialZone);
            }
            
            if (enableZoneDebugging)
                Debug.Log($"[ZoneManager] Registered entity in zone: {initialZone}");
        }
        
        /// <summary>
        /// Unregister an entity from zone tracking
        /// </summary>
        public void UnregisterEntity(IZoneEntity entity)
        {
            if (entity == null)
                return;
            
            // Remove from all zones
            string[] currentZones = entity.GetCurrentZones();
            foreach (string zoneId in currentZones)
            {
                RemoveEntityFromZone(entity, zoneId);
            }
            
            trackedEntities.Remove(entity);
            
            if (enableZoneDebugging)
                Debug.Log($"[ZoneManager] Unregistered entity");
        }
        
        /// <summary>
        /// Add entity to a specific zone
        /// </summary>
        private void AddEntityToZone(IZoneEntity entity, string zoneId)
        {
            if (!zoneEntities.ContainsKey(zoneId))
            {
                zoneEntities[zoneId] = new List<IZoneEntity>();
            }
            
            if (!zoneEntities[zoneId].Contains(entity))
            {
                zoneEntities[zoneId].Add(entity);
                entity.OnZoneEntered(zoneId);
                OnEntityEnteredZone?.Invoke(entity, zoneId);
            }
        }
        
        /// <summary>
        /// Remove entity from a specific zone
        /// </summary>
        private void RemoveEntityFromZone(IZoneEntity entity, string zoneId)
        {
            if (zoneEntities.ContainsKey(zoneId))
            {
                if (zoneEntities[zoneId].Remove(entity))
                {
                    entity.OnZoneExited(zoneId);
                    OnEntityExitedZone?.Invoke(entity, zoneId);
                }
            }
        }
        
        /// <summary>
        /// Get all entities in a specific zone
        /// </summary>
        public List<IZoneEntity> GetEntitiesInZone(string zoneId)
        {
            if (zoneEntities.ContainsKey(zoneId))
                return new List<IZoneEntity>(zoneEntities[zoneId]);
            
            return new List<IZoneEntity>();
        }
        
        /// <summary>
        /// Get entities of a specific type in a zone
        /// </summary>
        public List<T> GetEntitiesInZone<T>(string zoneId) where T : class, IZoneEntity
        {
            var entities = GetEntitiesInZone(zoneId);
            return entities.OfType<T>().ToList();
        }
        #endregion
        
        #region Spatial Queries
        /// <summary>
        /// Find which zone contains the given position
        /// </summary>
        public string FindZoneAtPosition(Vector3 position)
        {
            foreach (var kvp in registeredZones)
            {
                if (kvp.Value.ContainsPoint(position))
                {
                    return kvp.Key;
                }
            }
            return "";
        }
        
        /// <summary>
        /// Get all zones within a radius of a position
        /// </summary>
        public List<string> FindZonesInRadius(Vector3 center, float radius)
        {
            List<string> foundZones = new List<string>();
            
            foreach (var kvp in registeredZones)
            {
                if (kvp.Value.IsWithinDistance(center, radius))
                {
                    foundZones.Add(kvp.Key);
                }
            }
            
            return foundZones;
        }
        
        /// <summary>
        /// Get the distance to the nearest zone boundary
        /// </summary>
        public float GetDistanceToZoneBoundary(Vector3 position, string zoneId)
        {
            if (registeredZones.TryGetValue(zoneId, out SecurityZone zone))
            {
                return zone.GetDistanceToBoundary(position);
            }
            return float.MaxValue;
        }
        
        /// <summary>
        /// Determine which zone an entity should belong to based on its position
        /// </summary>
        private string DetermineEntityZone(IZoneEntity entity)
        {
            if (entity is MonoBehaviour mono)
            {
                return FindZoneAtPosition(mono.transform.position);
            }
            return "";
        }
        #endregion
        
        #region Player Zone Tracking
        private void UpdatePlayerZoneTracking()
        {
            if (playerTransform == null || Time.time - lastPlayerCheck < playerCheckInterval)
                return;
            
            lastPlayerCheck = Time.time;
            
            string newZone = FindZoneAtPosition(playerTransform.position);
            
            if (newZone != currentPlayerZone)
            {
                previousPlayerZone = currentPlayerZone;
                currentPlayerZone = newZone;
                
                OnPlayerZoneChanged?.Invoke(currentPlayerZone, previousPlayerZone);
                
                if (logZoneTransitions)
                    Debug.Log($"[ZoneManager] Player moved: {previousPlayerZone} -> {currentPlayerZone}");
            }
        }
        
        /// <summary>
        /// Manually set the player's current zone (for teleportation, etc.)
        /// </summary>
        public void SetPlayerZone(string zoneId)
        {
            if (zoneId != currentPlayerZone)
            {
                previousPlayerZone = currentPlayerZone;
                currentPlayerZone = zoneId;
                OnPlayerZoneChanged?.Invoke(currentPlayerZone, previousPlayerZone);
            }
        }
        #endregion
        
        #region Zone Adjacency
        /// <summary>
        /// Set up adjacency relationships between zones
        /// </summary>
        private void UpdateZoneAdjacency(SecurityZone zone)
        {
            string zoneId = zone.ZoneId;
            if (!adjacentZones.ContainsKey(zoneId))
            {
                adjacentZones[zoneId] = new List<string>();
            }
            
            // Find adjacent zones based on distance or manual configuration
            foreach (var otherZone in registeredZones.Values)
            {
                if (otherZone != zone && zone.IsAdjacentTo(otherZone))
                {
                    AddZoneAdjacency(zoneId, otherZone.ZoneId);
                }
            }
        }
        
        /// <summary>
        /// Manually add adjacency between two zones
        /// </summary>
        public void AddZoneAdjacency(string zoneId1, string zoneId2)
        {
            if (!adjacentZones.ContainsKey(zoneId1))
                adjacentZones[zoneId1] = new List<string>();
            
            if (!adjacentZones.ContainsKey(zoneId2))
                adjacentZones[zoneId2] = new List<string>();
            
            if (!adjacentZones[zoneId1].Contains(zoneId2))
                adjacentZones[zoneId1].Add(zoneId2);
            
            if (!adjacentZones[zoneId2].Contains(zoneId1))
                adjacentZones[zoneId2].Add(zoneId1);
        }
        
        /// <summary>
        /// Get zones adjacent to the given zone
        /// </summary>
        public List<string> GetAdjacentZones(string zoneId)
        {
            if (adjacentZones.ContainsKey(zoneId))
                return new List<string>(adjacentZones[zoneId]);
            
            return new List<string>();
        }
        #endregion
        
        #region Debug and Utilities
        /// <summary>
        /// Get debug information about all zones and entities
        /// </summary>
        public string GetDebugInfo()
        {
            var info = $"ZoneManager Debug Info:\n";
            info += $"Registered Zones: {registeredZones.Count}\n";
            info += $"Tracked Entities: {trackedEntities.Count}\n";
            info += $"Player Zone: {currentPlayerZone} (was: {previousPlayerZone})\n\n";
            
            foreach (var kvp in zoneEntities)
            {
                info += $"Zone '{kvp.Key}': {kvp.Value.Count} entities\n";
            }
            
            return info;
        }
        
        /// <summary>
        /// Force update of all entity zones (useful after teleportation)
        /// </summary>
        public void RefreshAllEntityZones()
        {
            foreach (var entity in trackedEntities.ToList())
            {
                string newZone = DetermineEntityZone(entity);
                string[] currentZones = entity.GetCurrentZones();
                
                // Remove from old zones
                foreach (string oldZone in currentZones)
                {
                    if (oldZone != newZone)
                        RemoveEntityFromZone(entity, oldZone);
                }
                
                // Add to new zone
                if (!string.IsNullOrEmpty(newZone) && !currentZones.Contains(newZone))
                {
                    AddEntityToZone(entity, newZone);
                }
            }
            
            Debug.Log("[ZoneManager] Refreshed all entity zones");
        }
        #endregion
        
        #region Gizmo Drawing
        private void OnDrawGizmos()
        {
            if (!showZoneBounds) return;
            
            foreach (var zone in registeredZones.Values)
            {
                if (zone != null)
                {
                    zone.DrawZoneGizmos();
                }
            }
        }
        #endregion
    }
}