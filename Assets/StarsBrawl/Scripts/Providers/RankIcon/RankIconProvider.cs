using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RankIconProvider : MonoBehaviour, IRankIconProvider
{
    [SerializeField] private RankIconData[] rankIcons;

    private readonly Dictionary<int, Sprite>    rankIconsById   = new();
    private readonly Dictionary<string, Sprite> rankIconsByName = new(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        BuildMap();
    }

    private void BuildMap()
    {
        rankIconsById.Clear();
        rankIconsByName.Clear();

        if (rankIcons == null || rankIcons.Length == 0)
        {
            Debug.LogWarning("[RankIconProvider] No RankIconData entries assigned.");
            return;
        }

        foreach (RankIconData data in rankIcons)
        {
            if (data == null)
                continue;

            if (data.Id <= 0)
            {
                Debug.LogWarning($"[RankIconProvider] '{data.name}' has no valid ID.");
                continue;
            }

            if (rankIconsById.ContainsKey(data.Id))
            {
                Debug.LogWarning($"[RankIconProvider] Duplicate rank ID: {data.Id}");
            }
            else
            {
                rankIconsById.Add(data.Id, data.Icon);
            }

            if (!string.IsNullOrWhiteSpace(data.RankName))
            {
                if (rankIconsByName.ContainsKey(data.RankName))
                    Debug.LogWarning($"[RankIconProvider] Duplicate rank name: '{data.RankName}'");
                else
                    rankIconsByName.Add(data.RankName, data.Icon);
            }
        }
    }

    /// <inheritdoc/>
    public Sprite GetRankIconByID(int id)
    {
        if (id <= 0)
            throw new ArgumentException("Invalid rank id.", nameof(id));

        if (!rankIconsById.TryGetValue(id, out Sprite sprite))
            throw new KeyNotFoundException($"Rank icon with ID '{id}' not found.");

        return sprite;
    }

    /// <inheritdoc/>
    public bool TryGetRankIconByID(int id, out Sprite sprite)
    {
        if (id <= 0)
        {
            sprite = null;
            return false;
        }

        return rankIconsById.TryGetValue(id, out sprite);
    }

    /// <inheritdoc/>
    public bool TryGetRankIconByName(string rankName, out Sprite sprite)
    {
        if (string.IsNullOrWhiteSpace(rankName))
        {
            sprite = null;
            return false;
        }

        return rankIconsByName.TryGetValue(rankName, out sprite);
    }
}
