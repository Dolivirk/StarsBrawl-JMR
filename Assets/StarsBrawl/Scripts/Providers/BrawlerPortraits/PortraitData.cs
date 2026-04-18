using UnityEngine;

[CreateAssetMenu(menuName = "StarsBrawl/Portraits/Portrait Data", fileName = "PortraitData")]
public sealed class PortraitData : ScriptableObject
{
    [SerializeField] [Min(1)] private int id;
    [SerializeField] private Sprite portrait;

    public int Id => id;
    public Sprite Portrait => portrait;
}