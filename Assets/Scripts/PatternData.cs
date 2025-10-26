using UnityEngine;

[CreateAssetMenu(fileName = "Pattern", menuName = "GameOfLife/Pattern", order = 1)]
public class PatternData : ScriptableObject
{
    public string patternName;
    public Vector2Int[] cells;
}
