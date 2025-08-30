using UnityEngine;

namespace StealthSystem
{
    /// <summary>
    /// Interface for entities that belong to specific zones
    /// </summary>
    public interface IZoneEntity
    {
        /// <summary>Which zone this entity belongs to</summary>
        string ZoneId { get; set; }
        
        /// <summary>Can this entity operate across multiple zones?</summary>
        bool IsMultiZone { get; }
        
        /// <summary>Called when this entity enters a new zone</summary>
        void OnZoneEntered(string zoneId);
        
        /// <summary>Called when this entity leaves a zone</summary>
        void OnZoneExited(string zoneId);
        
        /// <summary>Gets all zones this entity currently occupies</summary>
        string[] GetCurrentZones();
    }
}