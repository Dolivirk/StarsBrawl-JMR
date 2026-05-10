using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    private const string UserDetailsScene   = "UserDetails";
    private const string BrawlerDetailsScene = "BrawlerDetails";
    private const string BrawlersScene       = "Brawlers";
    private const string BattleLogScene      = "BattleLog";

    [Header("Dependencies")]
    [SerializeField] private SQL sql;

    [Header("Player Data")]
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private TextMeshProUGUI playerTrophiesText;

    [Header("Resources")]
    [SerializeField] private TextMeshProUGUI blingText;
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI gemsText;

    [Header("Last Played Brawler")]
    [SerializeField] private TextMeshProUGUI brawlerTrophiesText;
    [SerializeField] private TextMeshProUGUI brawlerLevelText;

    private void Start()
    {
        string userId = UserSession.Instance?.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[MainMenuController] No user session found.");
            return;
        }

        PopulateUserProfile(userId);
        PopulateLastBrawler(userId);
    }

    private void PopulateUserProfile(string userId)
    {
        if (!sql.TryGetUserProfile(userId, out UserProfile profile))
        {
            Debug.LogError($"[MainMenuController] Could not load profile for user: {userId}");
            return;
        }

        nicknameText.text       = profile.Nickname;
        playerTrophiesText.text = profile.Trophies.ToString();
        blingText.text          = profile.Bling.ToString();
        coinsText.text          = profile.Coins.ToString();
        gemsText.text           = profile.Gems.ToString();
    }

    private void PopulateLastBrawler(string userId)
    {
        if (!sql.TryGetLastBrawlerStats(userId, out BrawlerStats stats))
        {
            Debug.LogWarning($"[MainMenuController] No brawler data found for user: {userId}");
            return;
        }

        brawlerTrophiesText.text = stats.Trophies.ToString();
        brawlerLevelText.text    = stats.Level.ToString();
    }

    public void OnNicknameClicked()
    {
        // Antes de abrir UserDetails, establezca ViewedUserId con el ID del usuario que ha iniciado sesión.
        UserSession.Instance?.SetViewedUser(UserSession.Instance.UserId);
        SceneManager.LoadScene(UserDetailsScene);
    }
    public void OnBrawlerPlaceholderClicked() => SceneManager.LoadScene(BrawlerDetailsScene);
    public void OnBrawlersClicked()        => SceneManager.LoadScene(BrawlersScene);
    public void OnBattleLogClicked()       => SceneManager.LoadScene(BattleLogScene);
}
