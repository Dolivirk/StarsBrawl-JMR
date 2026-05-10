using UnityEngine;

/// <summary>
/// Persistent singleton that survives scene loads and holds session state.
/// Separates the logged-in owner's id (UserId) from a temporarily viewed
/// player's id (ViewedUserId) so navigation and data fetching stay unambiguous.
/// Created automatically before any scene loads via RuntimeInitializeOnLoadMethod,
/// so it is always available regardless of which scene is entered first.
/// </summary>
public class UserSession : MonoBehaviour
{
    public static UserSession Instance { get; private set; }

    /// <summary>The user_id of the player who logged in. Never changes after login.</summary>
    public string UserId    { get; private set; }

    /// <summary>
    /// The user_id whose profile is currently being viewed.
    /// Equals UserId when viewing the logged-in player's own profile,
    /// or another player's id when navigated from MatchDetails.
    /// </summary>
    public string ViewedUserId { get; private set; }

    public int BrawlerId { get; private set; }
    public int MatchId   { get; private set; }

    /// <summary>
    /// Ensures UserSession exists before any scene object runs Awake or Start.
    /// This makes the singleton available even when Play Mode is started from
    /// a scene other than Login (e.g. Brawlers, BrawlerDetails).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject("UserSession");
        go.AddComponent<UserSession>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Stores the identifier of the logged-in user and sets ViewedUserId to the same value.
    /// Call this only at login time.
    /// </summary>
    public void SetUser(string userId)
    {
        UserId       = userId;
        ViewedUserId = userId;
    }

    /// <summary>
    /// Sets the profile to view without changing the logged-in UserId.
    /// Call this when navigating to another player's UserDetails from MatchDetails.
    /// </summary>
    public void SetViewedUser(string userId)
    {
        ViewedUserId = userId;
    }

    /// <summary>Stores the brawler_id selected in the Brawlers screen.</summary>
    public void SetBrawler(int brawlerId)
    {
        BrawlerId = brawlerId;
    }

    /// <summary>Stores the match_id selected in the BattleLog screen.</summary>
    public void SetMatch(int matchId)
    {
        MatchId = matchId;
    }
}
