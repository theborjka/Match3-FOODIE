using UnityEngine;

namespace Match3Foodie
{
    public enum Match3SpecialEffectType
    {
        None = 0,
        Fish = 1,
        MathBonus = 2,
        Bomb = 3,
        Line = 4,
        CrossLine = 5,
    }

    public enum Match3LineClearDirection
    {
        Horizontal = 0,
        Vertical = 1,
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
        [SerializeField, Min(0)] private int bombRadius = 1;
        [SerializeField] private Match3LineClearDirection lineClearDirection = Match3LineClearDirection.Horizontal;

        [Header("Created Special Variations")]
        [SerializeField] private Sprite bombSprite;
        [SerializeField] private Sprite horizontalLineSprite;
        [SerializeField] private Sprite verticalLineSprite;
        [SerializeField] private Sprite crossLineSprite;

        public string ElementId => elementId;
        public Sprite Sprite => sprite;
        public Color Tint => tint;
        public int SpawnWeight => spawnWeight;
        public Match3PieceView PiecePrefab => piecePrefab;
        public GameObject DestructionEffectPrefab => destructionEffectPrefab;
        public Match3SpecialEffectType SpecialEffectType => specialEffectType;
        public float MathBonusSeconds => mathBonusSeconds;
        public int BombRadius => Mathf.Max(0, bombRadius);
        public Match3LineClearDirection LineClearDirection => lineClearDirection;

        public Sprite GetSpriteForSpecial(Match3SpecialEffectType effectType, Match3LineClearDirection direction)
        {
            return effectType switch
            {
                Match3SpecialEffectType.Bomb => bombSprite != null ? bombSprite : sprite,
                Match3SpecialEffectType.Line when direction == Match3LineClearDirection.Vertical =>
                    verticalLineSprite != null ? verticalLineSprite : sprite,
                Match3SpecialEffectType.Line =>
                    horizontalLineSprite != null ? horizontalLineSprite : sprite,
                Match3SpecialEffectType.CrossLine => crossLineSprite != null ? crossLineSprite : sprite,
                _ => sprite,
            };
        }
    }
}
