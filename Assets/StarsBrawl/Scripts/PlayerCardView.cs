using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Muestra una ficha de jugador fija en la pantalla de detalles del partido.
/// Gestiona el apodo, el retrato, los trofeos, el nivel y la navegación mediante clics.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCardView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private Image           portraitImage;
    [SerializeField] private TextMeshProUGUI trophiesText;
    [SerializeField] private TextMeshProUGUI levelText;

    private Action _onClickCallback;

    /// <summary>Rellena todos los campos visuales con los datos de los participantes.</summary>
    public void Populate(MatchParticipantEntry entry, Sprite portrait)
    {
        nicknameText.text = entry.Nickname;
        trophiesText.text = entry.Trophies.ToString();
        levelText.text    = entry.Level.ToString();

        if (portrait != null)
            portraitImage.sprite = portrait;
    }

    /// <summary>Registra la función de devolución de llamada que se invocará cuando se haga clic en esta tarjeta.</summary>
    public void SetOnClick(Action callback)
    {
        _onClickCallback = callback;
    }

    public void OnCardClicked()
    {
        _onClickCallback?.Invoke();
    }
}
