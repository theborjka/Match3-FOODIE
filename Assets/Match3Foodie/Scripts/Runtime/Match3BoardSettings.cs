using System.Collections.Generic;
using UnityEngine;

namespace Match3Foodie
{
    [CreateAssetMenu(menuName = "Match3 Foodie/Board Settings", fileName = "Board Settings")]
    public sealed class Match3BoardSettings : ScriptableObject
    {
        [Header("Grid")]
        [SerializeField, Min(3)] private int width = 8;
        [SerializeField, Min(3)] private int height = 8;
        [SerializeField, Min(0.1f)] private float cellSize = 1f;
        [SerializeField, Min(0f)] private Vector2 gapSize = new(0.08f, 0.08f);
        [SerializeField, Min(0.01f)] private float elementSize = 0.86f;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float swapDuration = 0.16f;
        [SerializeField, Min(0.01f)] private float shuffleDuration = 0.28f;
        [SerializeField, Min(0.01f)] private float fallDurationPerCell = 0.06f;
        [SerializeField, Min(0.01f)] private float refillFallDurationPerCell = 0.055f;
        [SerializeField, Min(0f)] private float clearDelay = 0.08f;
        [SerializeField, Min(0f)] private float refillDelay = 0.04f;
        [SerializeField, Min(0f)] private Vector2 refillRandomDelay = new(0f, 0.12f);
        [SerializeField, Min(0.01f)] private float refillSpawnScale = 0.72f;
        [SerializeField, Min(1f)] private float refillPopScale = 1.08f;
        [SerializeField, Min(0f)] private float fallImpactBounceDistance = 0.045f;
        [SerializeField, Min(0f)] private float fallImpactBounceDuration = 0.07f;

        [Header("Collection")]
        [SerializeField, Min(0.01f)] private float collectFlightSpeed = 12f;
        [SerializeField, Min(0f)] private float collectExitDistance = 0.35f;
        [SerializeField, Min(0f)] private float collectExitDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float collectArrivePopScale = 1.25f;
        [SerializeField, Min(0f)] private float collectArrivePopDuration = 0.14f;

        [Header("Gameplay")]
        [SerializeField] private bool avoidInitialMatches = true;
        [SerializeField] private bool allowInputWhileResolving;

        [Header("Match Patterns")]
        [SerializeField] private bool matchLines = true;
        [SerializeField] private bool matchTShapes = true;
        [SerializeField] private bool matchSquares = true;
        [SerializeField] private bool matchCrosses = true;
        [SerializeField] private bool matchCorners = true;

        [Header("Fish Special")]
        [SerializeField, Min(0.01f)] private float fishFlightSpeed = 9f;
        [SerializeField, Min(0f)] private Vector2 fishRandomDelay = new(0f, 0.12f);
        [SerializeField, Min(0f)] private float fishWaveAmplitude = 0.18f;
        [SerializeField, Min(0f)] private float fishWaveFrequency = 7f;
        [SerializeField] private bool fishKeepBoardOrientation;
        [SerializeField] private bool fishFaceFlightDirection = true;
        [SerializeField] private float fishSpriteForwardAngle = 180f;
        [SerializeField, Range(0f, 89f)] private float fishMaxTiltAngle = 35f;
        [SerializeField, Min(0)] private int fishFlightSortingOrderBoost = 100;

        [SerializeField] private Match3PieceView defaultPiecePrefab;
        [SerializeField] private List<Match3ElementDefinition> elements = new();

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Vector2 GapSize => gapSize;
        public float ElementSize => elementSize;
        public float ElementWorldSize => cellSize * elementSize;
        public Vector2 StepSize => new(cellSize + gapSize.x, cellSize + gapSize.y);
        public Vector2 BoardSize => new(width * cellSize + Mathf.Max(0, width - 1) * gapSize.x, height * cellSize + Mathf.Max(0, height - 1) * gapSize.y);
        public float SwapDuration => swapDuration;
        public float ShuffleDuration => shuffleDuration;
        public float FallDurationPerCell => fallDurationPerCell;
        public float RefillFallDurationPerCell => refillFallDurationPerCell;
        public float ClearDelay => clearDelay;
        public float RefillDelay => refillDelay;
        public Vector2 RefillRandomDelay => refillRandomDelay;
        public float RefillSpawnScale => refillSpawnScale;
        public float RefillPopScale => refillPopScale;
        public float FallImpactBounceDistance => fallImpactBounceDistance;
        public float FallImpactBounceDuration => fallImpactBounceDuration;
        public float CollectFlightSpeed => collectFlightSpeed;
        public float CollectExitDistance => collectExitDistance;
        public float CollectExitDuration => collectExitDuration;
        public float CollectArrivePopScale => collectArrivePopScale;
        public float CollectArrivePopDuration => collectArrivePopDuration;
        public bool AvoidInitialMatches => avoidInitialMatches;
        public bool AllowInputWhileResolving => allowInputWhileResolving;
        public bool MatchLines => matchLines;
        public bool MatchTShapes => matchTShapes;
        public bool MatchSquares => matchSquares;
        public bool MatchCrosses => matchCrosses;
        public bool MatchCorners => matchCorners;
        public float FishFlightSpeed => fishFlightSpeed;
        public Vector2 FishRandomDelay => fishRandomDelay;
        public float FishWaveAmplitude => fishWaveAmplitude;
        public float FishWaveFrequency => fishWaveFrequency;
        public bool FishKeepBoardOrientation => fishKeepBoardOrientation;
        public bool FishFaceFlightDirection => fishFaceFlightDirection && !fishKeepBoardOrientation;
        public float FishSpriteForwardAngle => fishSpriteForwardAngle;
        public float FishMaxTiltAngle => fishMaxTiltAngle;
        public int FishFlightSortingOrderBoost => fishFlightSortingOrderBoost;
        public Match3PieceView DefaultPiecePrefab => defaultPiecePrefab;
        public IReadOnlyList<Match3ElementDefinition> Elements => elements;
    }
}
