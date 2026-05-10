using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controla la pantalla de detalles del partido: obtiene todos los participantes del partido seleccionado,
/// los divide en ganadores y perdedores según su resultado individual,
/// rellena las seis fichas de jugador (tres ganadores y tres perdedores),
/// y gestiona la navegación hacia atrás y entre las fichas de cada jugador.
/// </summary>
[DisallowMultipleComponent]
public sealed class MatchDetailsController : MonoBehaviour
{
    private const string BattleLogScene   = "BattleLog";
    private const string UserDetailsScene = "UserDetails";

    // Canonical "win" values accepted from the DB (case-insensitive).
    private static readonly HashSet<string> WinValues = new HashSet<string>(
        System.StringComparer.OrdinalIgnoreCase) { "Win", "Winner", "Victoria", "W" };

    [Header("Dependencies")]
    [SerializeField] private SQL              sql;
    [SerializeField] private PortraitProvider portraitProvider;

    [Header("Result")]
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("Winner Cards")]
    [SerializeField] private PlayerCardView[] winnerCards = new PlayerCardView[3];

    [Header("Loser Cards")]
    [SerializeField] private PlayerCardView[] loserCards = new PlayerCardView[3];

    private void Start()
    {
        // Muestra el esquema completo para que todos los nombres de tablas y columnas sean visibles en la consola.
        sql.LogMatchesSchema();

        int matchId = UserSession.Instance?.MatchId ?? 0;

        if (matchId <= 0)
        {
            Debug.LogError("[MatchDetailsController] No match selected in UserSession.");
            return;
        }

        Debug.Log($"[MatchDetailsController] Fetching participants for match_id: {matchId}");

        List<MatchParticipantEntry> participants = sql.GetMatchParticipants(matchId);

        Debug.Log($"[MatchDetailsController] Participants retrieved: {participants.Count}");

        PopulateResult(participants);
        PopulatePlayerCards(participants);
    }

    /// <summary>
    /// Establece el texto del encabezado del resultado según el resultado del usuario que ha iniciado sesión en esta coincidencia.
    /// </summary>
    private void PopulateResult(List<MatchParticipantEntry> participants)
    {
        string userId = UserSession.Instance?.UserId;
        if (string.IsNullOrEmpty(userId))
            return;

        foreach (MatchParticipantEntry p in participants)
        {
            if (p.UserId != userId)
                continue;

            resultText.text = string.IsNullOrEmpty(p.Result)
                ? "RESULTADO"
                : p.Result.ToUpper();
            return;
        }

        // Logged-in user not found in this match's participants — keep a neutral label.
        resultText.text = "RESULTADO";
    }

    /// <summary>
    /// Divide a los participantes en ganadores y perdedores según su campo de Resultado
    /// luego rellena las casillas correspondientes
    /// </summary>
    private void PopulatePlayerCards(List<MatchParticipantEntry> participants)
    {
        var winners = new List<MatchParticipantEntry>();
        var losers  = new List<MatchParticipantEntry>();

        foreach (MatchParticipantEntry p in participants)
        {
            if (WinValues.Contains(p.Result))
                winners.Add(p);
            else
                losers.Add(p);
        }

        // Opción alternativa: si no hay datos de resultados disponibles, dividir por índice (primera mitad = ganadores).
        if (winners.Count == 0 && losers.Count == participants.Count && participants.Count > 0)
        {
            Debug.LogWarning("[MatchDetailsController] No result data found — splitting participants by index.");
            int half = participants.Count / 2;
            for (int i = 0; i < participants.Count; i++)
            {
                if (i < half) winners.Add(participants[i]);
                else          losers.Add(participants[i]);
            }
        }

        PopulateGroup(winnerCards, winners);
        PopulateGroup(loserCards,  losers);
    }

    private void PopulateGroup(PlayerCardView[] cards, List<MatchParticipantEntry> group)
    {
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;

            if (i >= group.Count)
            {
                cards[i].gameObject.SetActive(false);
                continue;
            }

            MatchParticipantEntry entry = group[i];

            portraitProvider.TryGetPortraitByID(entry.BrawlerId, out Sprite portrait);
            cards[i].gameObject.SetActive(true);
            cards[i].Populate(entry, portrait);

            string capturedUserId = entry.UserId;
            cards[i].SetOnClick(() => OnPlayerClicked(capturedUserId));
        }
    }

    /// <summary>Regresa a la pantalla de registro de batalla.</summary>
    public void OnGoBackClicked()
    {
        SceneManager.LoadScene(BattleLogScene);
    }

    private static void OnPlayerClicked(string userId)
    {
        // Utilice SetViewedUser para que se conserve el ID de usuario que ha iniciado sesión para la navegación hacia atrás.
        UserSession.Instance?.SetViewedUser(userId);
        SceneManager.LoadScene(UserDetailsScene);
    }
}
