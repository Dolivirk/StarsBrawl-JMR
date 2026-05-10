/// <summary>Holds all data displayed in the BrawlerDetails screen.</summary>
public struct BrawlerDetailsData
{
    public string Name;
    public string ClassName;
    public string RarityName;
    public string Description;
    public string AttributeDescription;
    public int    BaseHealth;
    public int    BaseDamage;
    /// <summary>Number of projectiles fired per attack. Display only when greater than 1.</summary>
    public int    ProjectilesPerAttack;
    /// <summary>Number of attacks per use (i.e. shots per activation).</summary>
    public int    Attacks;
    public string SuperName;
    public int    Trophies;
    public int    Level;
}
