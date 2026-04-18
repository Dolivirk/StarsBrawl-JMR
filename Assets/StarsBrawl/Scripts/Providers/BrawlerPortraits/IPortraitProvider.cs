using UnityEngine;

public interface IPortraitProvider
{
    Sprite GetPortraitByID(int id);
    bool TryGetPortraitByID(int id, out Sprite sprite);
}