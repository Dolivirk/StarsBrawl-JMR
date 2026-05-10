using UnityEngine;

[CreateAssetMenu(menuName = "StarsBrawl/Rank/Rank Data", fileName = "RankData")]
public sealed class RankIconData : ScriptableObject
{
    [SerializeField] [Min(1)] private int id;

    /// <summary>
    /// Display name of the rank as stored in the DB (e.g. "Bronce", "Plata", "Oro").
    /// Used by RankIconProvider.TryGetRankIconByName.
    /// </summary>
    [SerializeField] private string rankName;

    [SerializeField] private Sprite icon;

    public int    Id       => id;
    public string RankName => rankName;
    public Sprite Icon     => icon;
}
