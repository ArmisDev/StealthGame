using UnityEngine;

namespace StealthSystem
{
    [System.Serializable]
    public struct GuardSettings
    {
        [Header("Movement")]
        public float walkSpeed;
        public float runSpeed;
        public float crouchSpeed;
        public float rotationSpeed;
        public float stoppingDistance;
        
        [Header("Patrol Behavior")]
        public float patrolPauseTime;
        public bool randomizePatrolPause;
        public float maxRandomPauseVariation;
        
        [Header("Search Behavior")]
        public float searchSpeed;
        public float searchTime;
        public float searchRadius;
        public int maxSearchPoints;
        
        [Header("Return Behavior")]
        public float returnSpeed;
        public float returnTimeout;
        
        public static GuardSettings CreateDefault()
        {
            return new GuardSettings
            {
                walkSpeed = 2f,
                runSpeed = 4f,
                crouchSpeed = 1f,
                rotationSpeed = 90f,
                stoppingDistance = 1.5f,
                patrolPauseTime = 2f,
                randomizePatrolPause = true,
                maxRandomPauseVariation = 1f,
                searchSpeed = 3f,
                searchTime = 15f,
                searchRadius = 10f,
                maxSearchPoints = 4,
                returnSpeed = 2.5f,
                returnTimeout = 30f
            };
        }
    }
}