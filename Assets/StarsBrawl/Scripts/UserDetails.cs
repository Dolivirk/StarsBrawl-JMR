/// <summary>Holds all data displayed in the UserDetails screen.</summary>
public struct UserDetails
{
    public string UserId;
    public string Nickname;
    public int    Rank;
    /// <summary>Display name of the rank, resolved from the ranks table at query time.</summary>
    public string RankName;
    public int    Trophies;
    public int    ShowdownWins;
    public int    TripleWins;
    public int    WinStreak;
    public string MostPlayedBrawler;
    public string BestBrawler;
    public string MostPlayedMode;
}
