/// <summary>
/// Holds the data for a single participant in a match,
/// including their result (e.g. "Win" / "Loss") for that specific match.
/// </summary>
public struct MatchParticipantEntry
{
    public string UserId;
    public string Nickname;
    public int    BrawlerId;
    public int    Trophies;
    public int    Level;
    /// <summary>Per-player match result, e.g. "Win", "Loss", "Draw".</summary>
    public string Result;
}
