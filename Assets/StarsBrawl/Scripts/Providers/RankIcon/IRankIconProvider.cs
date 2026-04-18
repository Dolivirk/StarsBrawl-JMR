using UnityEngine;

public interface IRankIconProvider
{
    Sprite GetRankIconByID(int id);
    bool TryGetRankIconByID(int id, out Sprite sprite);
}