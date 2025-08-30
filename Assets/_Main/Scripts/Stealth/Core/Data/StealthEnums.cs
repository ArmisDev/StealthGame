namespace StealthSystem
{
    public enum AlertLevel
    {
        Green,      // Normal operations - no alerts
        Suspicious, // Individual entity investigating something  
        Orange,     // Zone alert - entities actively searching for player
        Red         // Site lockdown - full hostile response
    }
    
    public enum PlayerBehavior
    {
        Stealth,    // Hiding, sneaking, non-violent approach
        Aggressive  // Combat, loud movement, taking out guards
    }
    
    public enum DetectionSource
    {
        Guard,      // Spotted by patrol guard
        Camera,     // Caught on security camera
        Noise,      // Made too much noise
        Body,       // Dead/unconscious guard found
        Environmental, // Motion sensor, laser tripwire, etc.
        Other       // Custom detection source
    }
    
    public enum DetectionState
    {
        Unaware,    // No knowledge of player presence
        Suspicious, // Something seems off, investigating
        Searching,  // Actively looking for the player
        Hostile,    // Player confirmed, taking action
        Combat      // In direct confrontation
    }
}