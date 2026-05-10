using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Populates a single match card in the BattleLog scroll list.
/// Attach to the root of the Match Data prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class MatchCardView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timeAgoText;
    [SerializeField] private TextMeshProUGUI matchTypeText;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private TextMeshProUGUI modalityText;
    [SerializeField] private Button          detailsButton;

    /// <summary>
    /// Rellena la tarjeta con los datos de la coincidencia y conecta la función de devolución de llamada del botón Detalles.
    /// </summary>
    public void Populate(MatchEntry entry, Action onDetailsClicked)
    {
        int daysDiff = (DateTime.Today - entry.Date.Date).Days;
        timeAgoText.text  = daysDiff == 0 ? "HOY"
                          : daysDiff == 1 ? "HACE 1 DÍA"
                          : $"HACE {daysDiff} DÍAS";

        matchTypeText.text = entry.MatchType;
        resultText.text    = entry.Result.ToUpper();
        modalityText.text  = entry.Modality;

        detailsButton.onClick.RemoveAllListeners();
        detailsButton.onClick.AddListener(() => onDetailsClicked());
    }
}
