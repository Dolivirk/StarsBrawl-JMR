using UnityEngine;

public interface IRankIconProvider
{
    Sprite GetRankIconByID(int id);
    bool TryGetRankIconByID(int id, out Sprite sprite);

    /// <summary>
    /// Looks up a rank icon by the rank's display name (e.g. "Bronce", "Plata").
    /// Comparison is case-insensitive.
    /// </summary>
    bool TryGetRankIconByName(string rankName, out Sprite sprite);
}
