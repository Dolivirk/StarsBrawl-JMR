using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PortraitProvider : MonoBehaviour, IPortraitProvider
{
    [SerializeField] private PortraitData[] portraits;

    private readonly Dictionary<int, Sprite> portraitMap = new();

    private void Awake()
    {
        BuildMap();
    }

    private void BuildMap()
    {
        portraitMap.Clear();

        if (portraits == null || portraits.Length == 0)
        {
            Debug.LogWarning("PortraitProvider: No PortraitData assigned.");
            return;
        }

        foreach (var data in portraits)
        {
            if (data == null)
                continue;

            if (data.Id <= 0)
            {
                Debug.LogWarning($"PortraitProvider: PortraitData '{data.name}' has no ID.");
                continue;
            }

            if (portraitMap.ContainsKey(data.Id))
            {
                Debug.LogWarning($"Duplicate portrait ID detected: {data.Id}");
                continue;
            }

            portraitMap.Add(data.Id, data.Portrait);
        }
    }


    public Sprite GetPortraitByID(int id)
    {
        if (id <= 0)
            throw new ArgumentException("Invalid portrait id.");

        if (!portraitMap.TryGetValue(id, out var sprite))
            throw new KeyNotFoundException($"Portrait with ID '{id}' not found.");

        return sprite;
    }

    public bool TryGetPortraitByID(int id, out Sprite sprite)
    {
        if (id <= 0)
        {
            sprite = null;
            return false;
        }

        return portraitMap.TryGetValue(id, out sprite);
    }
}