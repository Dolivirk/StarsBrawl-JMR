using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Populates a single brawler portrait card with data from a BrawlerEntry.
/// Attach to the root of the Brawler Portrait Data prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class BrawlerPortraitView : MonoBehaviour
{
    [SerializeField] private Image            portraitImage;
    [SerializeField] private TextMeshProUGUI  nameText;
    [SerializeField] private TextMeshProUGUI  levelText;
    [SerializeField] private TextMeshProUGUI  trophiesText;

    /// <summary>Fills the card with brawler data and portrait sprite.</summary>
    public void Populate(BrawlerEntry entry, Sprite portrait)
    {
        portraitImage.sprite = portrait;
        nameText.text        = entry.Name.ToUpper();
        levelText.text       = entry.Level.ToString();
        trophiesText.text    = entry.Trophies.ToString();
    }
}
