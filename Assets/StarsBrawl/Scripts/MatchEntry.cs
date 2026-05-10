using System;

/// <summary>Holds the data for a single match row displayed in the BattleLog screen.</summary>
public struct MatchEntry
{
    public int      MatchId;
    public DateTime Date;
    public string   MatchType;
    public string   Result;
    public string   Modality;
}
