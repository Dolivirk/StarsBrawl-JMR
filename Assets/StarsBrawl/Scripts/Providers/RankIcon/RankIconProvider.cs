using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RankIconProvider : MonoBehaviour, IRankIconProvider
{
    [SerializeField] private RankIconData[] rankIcons;

    private readonly Dictionary<int, Sprite> rankIconsMap = new();

    private void Awake()
    {
        BuildMap();
    }

    private void BuildMap()
    {
        rankIconsMap.Clear();

        if (rankIcons == null || rankIcons.Length == 0)
        {
            Debug.LogWarning("PortraitProvider: No PortraitData assigned.");
            return;
        }

        foreach (var data in rankIcons)
        {
            if (data == null)
                continue;

            if (data.Id <= 0)
            {
                Debug.LogWarning($"PortraitProvider: PortraitData '{data.name}' has no ID.");
                continue;
            }

            if (rankIconsMap.ContainsKey(data.Id))
            {
                Debug.LogWarning($"Duplicate portrait ID detected: {data.Id}");
                continue;
            }

            rankIconsMap.Add(data.Id, data.Icon);
        }
    }


    public Sprite GetRankIconByID(int id)
    {
        if (id <= 0)
            throw new ArgumentException("Invalid portrait id.");

        if (!rankIconsMap.TryGetValue(id, out var sprite))
            throw new KeyNotFoundException($"Portrait with ID '{id}' not found.");

        return sprite;
    }

    public bool TryGetRankIconByID(int id, out Sprite sprite)
    {
        if (id <= 0)
        {
            sprite = null;
            return false;
        }

        return rankIconsMap.TryGetValue(id, out sprite);
    }
}