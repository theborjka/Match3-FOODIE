using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Random = UnityEngine.Random;

namespace Match3Foodie
{
    public sealed class Match3BoosterController : MonoBehaviour
    {
        [Serializable] public sealed class IntEvent : UnityEvent<int> { }
        [Serializable] public sealed class BoosterEvent : UnityEvent<Match3BoosterType> { }

        [Header("Source")]
        [SerializeField] private Match3Board board;

        [Header("Uses")]
        [SerializeField, Min(0)] private int popAnyUses = 3;
        [SerializeField, Min(0)] private int popColorUses = 2;
        [SerializeField, Min(0)] private int randomWalkUses = 1;

        [Header("Random Walk")]
        [SerializeField, Min(1)] private int randomWalkPieceCount = 8;
        [SerializeField] private bool randomWalkAvoidRepeats = true;

        [Header("Optional UI")]
        [SerializeField] private TMP_Text popAnyUsesText;
        [SerializeField] private TMP_Text popColorUsesText;
        [SerializeField] private TMP_Text randomWalkUsesText;

        [Header("Events")]
        [SerializeField] private BoosterEvent boosterActivated = new();
        [SerializeField] private BoosterEvent boosterUsed = new();
        [SerializeField] private BoosterEvent boosterCanceled = new();
        [SerializeField] private IntEvent popAnyUsesChanged = new();
        [SerializeField] private IntEvent popColorUsesChanged = new();
        [SerializeField] private IntEvent randomWalkUsesChanged = new();

        private Match3BoosterType activeBooster;

        public Match3BoosterType ActiveBooster => activeBooster;
        public int PopAnyUses => popAnyUses;
        public int PopColorUses => popColorUses;
        public int RandomWalkUses => randomWalkUses;
        public BoosterEvent BoosterActivated => boosterActivated;
        public BoosterEvent BoosterUsed => boosterUsed;
        public BoosterEvent BoosterCanceled => boosterCanceled;
        public IntEvent PopAnyUsesChanged => popAnyUsesChanged;
        public IntEvent PopColorUsesChanged => popColorUsesChanged;
        public IntEvent RandomWalkUsesChanged => randomWalkUsesChanged;

        private void Awake()
        {
            if (board == null)
            {
                board = FindAnyObjectByType<Match3Board>();
            }

            RefreshUsesUI();
        }

        private void OnValidate()
        {
            popAnyUses = Mathf.Max(0, popAnyUses);
            popColorUses = Mathf.Max(0, popColorUses);
            randomWalkUses = Mathf.Max(0, randomWalkUses);
            randomWalkPieceCount = Mathf.Max(1, randomWalkPieceCount);
            RefreshUsesUI();
        }

        private void Update()
        {
            if (activeBooster == Match3BoosterType.None)
            {
                return;
            }

            if (TryGetCancelInput())
            {
                CancelActiveBooster();
                return;
            }

            if (!TryGetPointerUp(out var screenPosition))
            {
                return;
            }

            if (board == null || !board.TryGetPieceAtScreenPosition(screenPosition, out var piece))
            {
                return;
            }

            TryUseActiveBooster(piece);
        }

        public void ActivatePopAnyPiece()
        {
            ActivateBooster(Match3BoosterType.PopAnyPiece);
        }

        public void ActivatePopAllOfColor()
        {
            ActivateBooster(Match3BoosterType.PopAllOfColor);
        }

        public void ActivateRandomWalk()
        {
            ActivateBooster(Match3BoosterType.RandomWalk);
        }

        public void CancelActiveBooster()
        {
            if (activeBooster == Match3BoosterType.None)
            {
                return;
            }

            var canceled = activeBooster;
            activeBooster = Match3BoosterType.None;
            board?.SetInputEnabled(true);
            boosterCanceled.Invoke(canceled);
        }

        public void SetUses(Match3BoosterType boosterType, int uses)
        {
            switch (boosterType)
            {
                case Match3BoosterType.PopAnyPiece:
                    popAnyUses = Mathf.Max(0, uses);
                    popAnyUsesChanged.Invoke(popAnyUses);
                    break;
                case Match3BoosterType.PopAllOfColor:
                    popColorUses = Mathf.Max(0, uses);
                    popColorUsesChanged.Invoke(popColorUses);
                    break;
                case Match3BoosterType.RandomWalk:
                    randomWalkUses = Mathf.Max(0, uses);
                    randomWalkUsesChanged.Invoke(randomWalkUses);
                    break;
            }

            RefreshUsesUI();
        }

        private void ActivateBooster(Match3BoosterType boosterType)
        {
            if (board == null || board.IsResolving || GetUses(boosterType) <= 0)
            {
                return;
            }

            activeBooster = boosterType;
            board.SetInputEnabled(false);
            boosterActivated.Invoke(activeBooster);
        }

        private void TryUseActiveBooster(Match3PieceView selectedPiece)
        {
            if (selectedPiece == null || GetUses(activeBooster) <= 0)
            {
                return;
            }

            var piecesToClear = GetPiecesForBooster(activeBooster, selectedPiece);
            if (piecesToClear.Count == 0 || !board.TryClearPiecesByBooster(piecesToClear))
            {
                return;
            }

            DecrementUses(activeBooster);
            var used = activeBooster;
            activeBooster = Match3BoosterType.None;
            board.SetInputEnabled(true);
            boosterUsed.Invoke(used);
        }

        private List<Match3PieceView> GetPiecesForBooster(Match3BoosterType boosterType, Match3PieceView selectedPiece)
        {
            return boosterType switch
            {
                Match3BoosterType.PopAnyPiece => new List<Match3PieceView> { selectedPiece },
                Match3BoosterType.PopAllOfColor => board.GetPiecesWithDefinition(selectedPiece.Definition),
                Match3BoosterType.RandomWalk => BuildRandomWalk(selectedPiece),
                _ => new List<Match3PieceView>(),
            };
        }

        private List<Match3PieceView> BuildRandomWalk(Match3PieceView startPiece)
        {
            var path = new List<Match3PieceView>();
            var visited = new HashSet<Match3PieceView>();
            var current = startPiece;

            while (current != null && path.Count < randomWalkPieceCount)
            {
                path.Add(current);
                visited.Add(current);

                var neighbors = GetNeighbors(current.GridPosition, visited);
                if (neighbors.Count == 0 && randomWalkAvoidRepeats)
                {
                    neighbors = GetNeighbors(current.GridPosition, null);
                }

                current = neighbors.Count > 0 ? neighbors[Random.Range(0, neighbors.Count)] : null;
            }

            return path;
        }

        private List<Match3PieceView> GetNeighbors(Match3GridPosition position, HashSet<Match3PieceView> excluded)
        {
            var neighbors = new List<Match3PieceView>(4);
            AddNeighbor(neighbors, excluded, new Match3GridPosition(position.X + 1, position.Y));
            AddNeighbor(neighbors, excluded, new Match3GridPosition(position.X - 1, position.Y));
            AddNeighbor(neighbors, excluded, new Match3GridPosition(position.X, position.Y + 1));
            AddNeighbor(neighbors, excluded, new Match3GridPosition(position.X, position.Y - 1));
            return neighbors;
        }

        private void AddNeighbor(List<Match3PieceView> neighbors, HashSet<Match3PieceView> excluded, Match3GridPosition position)
        {
            var piece = board.GetPiece(position);
            if (piece != null && (excluded == null || !excluded.Contains(piece)))
            {
                neighbors.Add(piece);
            }
        }

        private int GetUses(Match3BoosterType boosterType)
        {
            return boosterType switch
            {
                Match3BoosterType.PopAnyPiece => popAnyUses,
                Match3BoosterType.PopAllOfColor => popColorUses,
                Match3BoosterType.RandomWalk => randomWalkUses,
                _ => 0,
            };
        }

        private void DecrementUses(Match3BoosterType boosterType)
        {
            SetUses(boosterType, GetUses(boosterType) - 1);
        }

        private void RefreshUsesUI()
        {
            if (popAnyUsesText != null)
            {
                popAnyUsesText.text = popAnyUses.ToString();
            }

            if (popColorUsesText != null)
            {
                popColorUsesText.text = popColorUses.ToString();
            }

            if (randomWalkUsesText != null)
            {
                randomWalkUsesText.text = randomWalkUses.ToString();
            }
        }

        private static bool TryGetPointerUp(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasReleasedThisFrame)
            {
                screenPosition = pointer.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            screenPosition = Input.mousePosition;
            return Input.GetMouseButtonUp(0);
#else
            screenPosition = default;
            return false;
#endif
        }

        private static bool TryGetCancelInput()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
