using UnityEngine;

namespace StealthSystem
{
    public enum GuardType
    {
        Patrol,      // Standard patrol guard
        Stationary,  // Guard that stays in one location
        Investigator,// More thorough investigation behavior
        Heavy        // Combat-focused, harder to evade
    }
    
    public enum GuardState
    {
        Patrolling,    // Following patrol route
        Searching,     // Actively searching for the player
        Chasing,       // In direct pursuit of player
        Returning,     // Returning to patrol after losing player
        Alerted        // Standing ready, heightened awareness
    }
    
    public enum MovementMode
    {
        Walk,
        Run,
        Crouch
    }
}