using UnityEngine;

namespace StealthSystem
{
    [System.Serializable]
    public struct DetectionData
    {
        [Header("Detection Info")]
        public DetectionSource source;
        public Vector3 detectionPosition;
        public Vector3 lastKnownPlayerPosition;
        public float detectionStrength;    // 0-1, how certain the detection is
        public float timestamp;
        
        [Header("Context")]
        public string zoneId;
        public string detectorId;          // ID of the detecting entity
        public bool wasPlayerSpotted;      // Direct visual contact vs indirect
        public PlayerBehavior playerBehavior;
        
        public static DetectionData Create(DetectionSource source, Vector3 detectionPos, 
                                         Vector3 playerPos, float strength, string zone, string detectorId)
        {
            return new DetectionData
            {
                source = source,
                detectionPosition = detectionPos,
                lastKnownPlayerPosition = playerPos,
                detectionStrength = Mathf.Clamp01(strength),
                timestamp = Time.time,
                zoneId = zone,
                detectorId = detectorId,
                wasPlayerSpotted = strength > 0.8f,
                playerBehavior = PlayerBehavior.Stealth // Will be set by behavior tracker
            };
        }
    }
    
    [System.Serializable]
    public struct AlertData
    {
        public AlertLevel currentLevel;
        public AlertLevel previousLevel;
        public float alertStartTime;
        public float lastDetectionTime;
        public int totalDetections;
        public Vector3 lastKnownPlayerPosition;
        public string zoneId;
        
        public float TimeSinceLastDetection => Time.time - lastDetectionTime;
        public float TotalAlertTime => Time.time - alertStartTime;
    }
}