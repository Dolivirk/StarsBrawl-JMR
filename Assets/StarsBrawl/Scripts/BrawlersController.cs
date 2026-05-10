using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controla la pantalla de Brawlers: muestra la cantidad desbloqueada/total,
/// rellena la cuadr?cula de Brawlers y gestiona la navegaci?n.
/// </summary>
[DisallowMultipleComponent]
public sealed class BrawlersController : MonoBehaviour
{
    private const string MainMenuScene      = "MainMenu";
    private const string BrawlerDetailsScene = "BrawlerDetails";

    [Header("Dependencies")]
    [SerializeField] private SQL              sql;
    [SerializeField] private PortraitProvider portraitProvider;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI brawlersAmountText;

    [Header("Grid")]
    [SerializeField] private Transform             brawlersContainer;
    [SerializeField] private BrawlerPortraitView   cardPrefab;

    private void Start()
    {
        string userId = UserSession.Instance?.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[BrawlersController] No user session found.");
            return;
        }

        List<BrawlerEntry> unlocked = sql.GetUnlockedBrawlers(userId);
        int total = sql.GetTotalBrawlerCount();

        brawlersAmountText.text = $"({unlocked.Count}/{total})";

        PopulateGrid(unlocked);
    }

    private void PopulateGrid(List<BrawlerEntry> entries)
    {
        foreach (var entry in entries)
        {
            BrawlerPortraitView card = Instantiate(cardPrefab, brawlersContainer);

            if (!portraitProvider.TryGetPortraitByID(entry.BrawlerId, out Sprite portrait))
                Debug.LogWarning($"[BrawlersController] Portrait not found for brawler_id {entry.BrawlerId}.");

            card.Populate(entry, portrait);

            // Captura el brawler_id para el cierre lambda y luego navega.
            int brawlerId = entry.BrawlerId;
            Button btn = card.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => OnBrawlerClicked(brawlerId));
        }
    }

    public static void OnGoBackClicked()
    {
        SceneManager.LoadScene(MainMenuScene);
    }

    private static void OnBrawlerClicked(int brawlerId)
    {
        UserSession.Instance?.SetBrawler(brawlerId);
        SceneManager.LoadScene(BrawlerDetailsScene);
    }
}
