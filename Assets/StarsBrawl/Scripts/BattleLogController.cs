using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla la pantalla de registro de batalla: obtiene las últimas veinte partidas del usuario que ha iniciado sesión,
/// rellena la lista de desplazamiento y gestiona la navegación hacia atrás/detalles.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleLogController : MonoBehaviour
{
    private const string MainMenuScene    = "MainMenu";
    private const string MatchDetailsScene = "MatchDetails";

    [Header("Dependencies")]
    [SerializeField] private SQL sql;

    [Header("List")]
    [SerializeField] private Transform  matchesContainer;
    [SerializeField] private GameObject cardPrefab;

    private void Start()
    {
        // Exporta el esquema completo de la base de datos para que los nombres de las columnas siempre sean visibles en la consola.
        sql.LogMatchesSchema();

        string userId = UserSession.Instance?.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[BattleLogController] No user session found. Make sure the user logged in before opening this scene.");
            return;
        }

        Debug.Log($"[BattleLogController] Fetching matches for user_id: {userId}");

        List<MatchEntry> matches = sql.GetRecentMatches(userId);

        Debug.Log($"[BattleLogController] Matches retrieved: {matches.Count}");

        PopulateList(matches);
    }

    private void PopulateList(List<MatchEntry> matches)
    {
        foreach (MatchEntry entry in matches)
        {
            // Instancie la raíz del prefab y, a continuación, busque MatchCardView en cualquier lugar de su jerarquía.
            GameObject instance = Instantiate(cardPrefab, matchesContainer);
            MatchCardView card = instance.GetComponentInChildren<MatchCardView>(includeInactive: true);

            if (card == null)
            {
                Debug.LogError("[BattleLogController] MatchCardView not found in card prefab.");
                continue;
            }

            int matchId = entry.MatchId;
            card.Populate(entry, () => OnDetailsClicked(matchId));
        }
    }

    public void OnGoBackClicked()
    {
        SceneManager.LoadScene(MainMenuScene);
    }

    private static void OnDetailsClicked(int matchId)
    {
        UserSession.Instance?.SetMatch(matchId);
        SceneManager.LoadScene(MatchDetailsScene);
    }
}
