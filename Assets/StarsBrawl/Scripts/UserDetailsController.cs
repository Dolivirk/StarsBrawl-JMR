using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controla la pantalla Detalles del usuario.
/// Obtiene y muestra el perfil del usuario con ID especificado como ViewedUserId en UserSession: el perfil del jugador que ha iniciado sesión (desde el menú principal)
/// o el perfil de otro jugador (desde Detalles del partido).
/// </summary>
[DisallowMultipleComponent]
public sealed class UserDetailsController : MonoBehaviour
{
    private const string MainMenuScene    = "MainMenu";
    private const string MatchDetailsScene = "MatchDetails";

    [Header("Dependencies")]
    [SerializeField] private SQL              sql;
    [SerializeField] private RankIconProvider rankIconProvider;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private TextMeshProUGUI tagText;

    [Header("Rank")]
    [SerializeField] private Image           rankIcon;
    [SerializeField] private TextMeshProUGUI rankNameText;

    [Header("Trophies")]
    [SerializeField] private TextMeshProUGUI trophiesText;

    [Header("Wins")]
    [SerializeField] private TextMeshProUGUI showdownWinsText;
    [SerializeField] private TextMeshProUGUI tripleWinsText;
    [SerializeField] private TextMeshProUGUI winStreakText;

    [Header("Brawler Stats")]
    [SerializeField] private TextMeshProUGUI mostPlayedBrawlerText;
    [SerializeField] private TextMeshProUGUI bestBrawlerText;
    [SerializeField] private TextMeshProUGUI mostPlayedModeText;

    // Cierto cuando llegamos aquí haciendo clic en la tarjeta de un jugador en MatchDetails.
    private bool _cameFromMatchDetails;

    private void Start()
    {
        if (UserSession.Instance == null)
        {
            Debug.LogError("[UserDetailsController] No user session found.");
            return;
        }

        // ViewedUserId es el perfil de destino (propio o de otro jugador).
        string viewedId  = UserSession.Instance.ViewedUserId;
        string ownId     = UserSession.Instance.UserId;

        _cameFromMatchDetails = !string.IsNullOrEmpty(viewedId)
                             && !string.IsNullOrEmpty(ownId)
                             && viewedId != ownId;

        if (string.IsNullOrEmpty(viewedId))
        {
            Debug.LogError("[UserDetailsController] ViewedUserId is empty.");
            return;
        }

        // Muestre el esquema completo en la consola para depuración.
        sql.LogMatchesSchema();

        Debug.Log($"[UserDetailsController] Fetching details for user_id: {viewedId} (own: {ownId})");

        if (!sql.TryGetUserDetails(viewedId, out UserDetails details))
        {
            Debug.LogWarning($"[UserDetailsController] Could not retrieve details for user_id: {viewedId}");
            return;
        }

        PopulateUI(details);
    }

    /// <summary>Rellena todos los elementos de la interfaz de usuario con los detalles del usuario obtenidos.</summary>
    private void PopulateUI(UserDetails details)
    {
        nicknameText.text = details.Nickname;
        tagText.text      = $"#{details.UserId}";
        trophiesText.text = details.Trophies.ToString();

       // Se prioriza la búsqueda por nombre (coincide con el nombre del rango en la base de datos, como "Bronce", "Plata", etc.).
       // Si el nombre no está asociado, se recurre a la búsqueda por ID.
        if (!rankIconProvider.TryGetRankIconByName(details.RankName, out Sprite sprite))
            rankIconProvider.TryGetRankIconByID(details.Rank, out sprite);

        if (sprite != null)
            rankIcon.sprite = sprite;

        rankNameText.text = details.RankName;

        showdownWinsText.text      = details.ShowdownWins.ToString();
        tripleWinsText.text        = details.TripleWins.ToString();
        winStreakText.text          = details.WinStreak.ToString();

        mostPlayedBrawlerText.text = details.MostPlayedBrawler;
        bestBrawlerText.text       = details.BestBrawler;
        mostPlayedModeText.text    = details.MostPlayedMode;
    }

    /// <summary>
    /// Regresa a MatchDetails si accedimos desde una tarjeta de jugador,
    /// o al menú principal si accedimos desde el botón de apodo del menú principal.
    /// </summary>
    public void OnGoBackClicked()
    {
        if (_cameFromMatchDetails)
            SceneManager.LoadScene(MatchDetailsScene);
        else
            SceneManager.LoadScene(MainMenuScene);
    }
}
