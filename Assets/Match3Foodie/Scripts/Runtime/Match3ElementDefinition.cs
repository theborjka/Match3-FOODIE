using UnityEngine;

namespace Match3Foodie
{
    public enum Match3SpecialEffectType
    {
        None = 0,
        Fish = 1,
        MathBonus = 2,
    }

    [CreateAssetMenu(menuName = "Match3 Foodie/Element Definition", fileName = "Element Definition")]
    public sealed class Match3ElementDefinition : ScriptableObject
    {
        [SerializeField] private string elementId = "element";
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color tint = Color.white;
        [SerializeField, Min(0)] private int spawnWeight = 1;
        [SerializeField] private Match3PieceView piecePrefab;
        [SerializeField] private GameObject destructionEffectPrefab;

        [Header("Special Effect")]
        [SerializeField] private Match3SpecialEffectType specialEffectType;
        [SerializeField, Min(0f)] private float mathBonusSeconds = 10f;

        public string ElementId => elementId;
        public Sprite Sprite => sprite;
        public Color Tint => tint;
        public int SpawnWeight => spawnWeight;
        public Match3PieceView PiecePrefab => piecePrefab;
        public GameObject DestructionEffectPrefab => destructionEffectPrefab;
        public Match3SpecialEffectType SpecialEffectType => specialEffectType;
        public float MathBonusSeconds => mathBonusSeconds;
    }
}
