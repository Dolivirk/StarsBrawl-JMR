using UnityEngine;

[CreateAssetMenu(menuName = "StarsBrawl/Rank/Rank Data", fileName = "RankData")]
public sealed class RankIconData : ScriptableObject
{
    [SerializeField] [Min(1)] private int id;
    [SerializeField] private Sprite icon;

    public int Id => id;
    public Sprite Icon => icon;
}