using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
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

        [Serializable]
        private sealed class BoosterButtonShaderVisual
        {
            [SerializeField] private Image buttonImage;

            private Material originalMaterial;
            private Material materialInstance;

            public void Initialize()
            {
                if (buttonImage == null || materialInstance != null)
                {
                    return;
                }

                originalMaterial = buttonImage.material;
                var sourceMaterial = buttonImage.material != null ? buttonImage.material : buttonImage.materialForRendering;
                if (sourceMaterial == null)
                {
                    return;
                }

                materialInstance = UnityEngine.Object.Instantiate(sourceMaterial);
                materialInstance.name = sourceMaterial.name + " (Booster Instance)";
                buttonImage.material = materialInstance;
            }

            public void Apply(
                bool selected,
                string outlineFadeProperty,
                float selectedOutlineFade,
                float idleOutlineFade,
                string enchantedFadeProperty,
                float selectedEnchantedFade,
                float idleEnchantedFade)
            {
                var material = GetMaterial();
                if (material == null)
                {
                    return;
                }

                SetFloatIfPresent(material, outlineFadeProperty, selected ? selectedOutlineFade : idleOutlineFade);
                SetFloatIfPresent(material, enchantedFadeProperty, selected ? selectedEnchantedFade : idleEnchantedFade);
            }

            public void Restore()
            {
                if (buttonImage != null)
                {
                    buttonImage.material = originalMaterial;
                }

                if (materialInstance == null)
                {
                    return;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(materialInstance);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(materialInstance);
                }

                materialInstance = null;
                originalMaterial = null;
            }

            private Material GetMaterial()
            {
                return materialInstance != null ? materialInstance : buttonImage != null ? buttonImage.material : null;
            }

            private static void SetFloatIfPresent(Material material, string propertyName, float value)
            {
                if (!string.IsNullOrWhiteSpace(propertyName) && material.HasProperty(propertyName))
                {
                    material.SetFloat(propertyName, value);
                }
            }
        }

        [Header("Source")]
        [SerializeField] private Match3Board board;

        [Header("Uses")]
        [SerializeField, Min(0)] private int popAnyUses = 3;
        [SerializeField, Min(0)] private int popColorUses = 2;
        [SerializeField, Min(0)] private int randomWalkUses = 1;

        [Header("Random Walk")]
        [SerializeField, Min(1)] private int randomWalkPieceCount = 8;
        [SerializeField] private bool randomWalkAvoidRepeats = true;
        [SerializeField] private GameObject randomWalkVisualPrefab;
        [SerializeField, Min(0.01f)] private float randomWalkVisualSpeed = 8f;
        [SerializeField, Min(0f)] private float randomWalkVisualLifetimeAfterUse = 0.35f;

        [Header("Optional UI")]
        [SerializeField] private TMP_Text popAnyUsesText;
        [SerializeField] private TMP_Text popColorUsesText;
        [SerializeField] private TMP_Text randomWalkUsesText;

        [Header("Input Lock")]
        [SerializeField] private CanvasGroup controlsCanvasGroup;
        [SerializeField, Min(0f)] private float lockFadeDuration = 0.18f;

        [Header("Selection Shader")]
        [SerializeField] private BoosterButtonShaderVisual popAnyButtonVisual;
        [SerializeField] private BoosterButtonShaderVisual popColorButtonVisual;
        [SerializeField] private BoosterButtonShaderVisual randomWalkButtonVisual;
        [SerializeField] private string outerOutlineFadeProperty = "_OuterOutlineFade";
        [SerializeField] private float selectedOuterOutlineFade = 1f;
        [SerializeField] private float idleOuterOutlineFade;
        [SerializeField] private string enchantedFadeProperty = "_EnchantedFade";
        [SerializeField] private float selectedEnchantedFade = 1f;
        [SerializeField] private float idleEnchantedFade;

        [Header("Events")]
        [SerializeField] private BoosterEvent boosterActivated = new();
        [SerializeField] private BoosterEvent boosterUsed = new();
        [SerializeField] private BoosterEvent boosterCanceled = new();
        [SerializeField] private IntEvent popAnyUsesChanged = new();
        [SerializeField] private IntEvent popColorUsesChanged = new();
        [SerializeField] private IntEvent randomWalkUsesChanged = new();

        private Match3BoosterType activeBooster;
        private int initialPopAnyUses;
        private int initialPopColorUses;
        private int initialRandomWalkUses;
        private bool controlsLocked;
        private bool boosterUseInProgress;
        private Coroutine controlsFadeRoutine;

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

            initialPopAnyUses = popAnyUses;
            initialPopColorUses = popColorUses;
            initialRandomWalkUses = randomWalkUses;
            RefreshUsesUI();
            InitializeButtonVisuals();
            RefreshButtonVisuals();
            ApplyControlsLockVisual(false, true);
        }

        private void OnValidate()
        {
            popAnyUses = Mathf.Max(0, popAnyUses);
            popColorUses = Mathf.Max(0, popColorUses);
            randomWalkUses = Mathf.Max(0, randomWalkUses);
            randomWalkPieceCount = Mathf.Max(1, randomWalkPieceCount);
            RefreshUsesUI();

            if (Application.isPlaying)
            {
                RefreshButtonVisuals();
            }
        }

        private void Update()
        {
            if (activeBooster == Match3BoosterType.None || boosterUseInProgress)
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
            boosterUseInProgress = false;
            board?.SetInputEnabled(true);
            RefreshButtonVisuals();
            boosterCanceled.Invoke(canceled);
        }

        public void SetControlsLocked(bool locked)
        {
            if (controlsLocked == locked)
            {
                return;
            }

            controlsLocked = locked;
            if (controlsLocked)
            {
                CancelActiveBooster();
            }

            ApplyControlsLockVisual(controlsLocked, false);
        }

        public void ResetUsesToInitial()
        {
            activeBooster = Match3BoosterType.None;
            boosterUseInProgress = false;
            popAnyUses = Mathf.Max(0, initialPopAnyUses);
            popColorUses = Mathf.Max(0, initialPopColorUses);
            randomWalkUses = Mathf.Max(0, initialRandomWalkUses);
            popAnyUsesChanged.Invoke(popAnyUses);
            popColorUsesChanged.Invoke(popColorUses);
            randomWalkUsesChanged.Invoke(randomWalkUses);
            RefreshUsesUI();
            RefreshButtonVisuals();
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
            if (controlsLocked || board == null || board.IsResolving || GetUses(boosterType) <= 0)
            {
                return;
            }

            activeBooster = boosterType;
            board.SetInputEnabled(false);
            RefreshButtonVisuals();
            boosterActivated.Invoke(activeBooster);
        }

        private void TryUseActiveBooster(Match3PieceView selectedPiece)
        {
            if (selectedPiece == null || GetUses(activeBooster) <= 0 || boosterUseInProgress)
            {
                return;
            }

            if (activeBooster == Match3BoosterType.RandomWalk)
            {
                StartCoroutine(UseRandomWalkBoosterRoutine(selectedPiece));
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
            RefreshButtonVisuals();
            boosterUsed.Invoke(used);
        }

        private IEnumerator UseRandomWalkBoosterRoutine(Match3PieceView selectedPiece)
        {
            boosterUseInProgress = true;

            var path = BuildRandomWalk(selectedPiece);
            if (path.Count == 0)
            {
                boosterUseInProgress = false;
                yield break;
            }

            if (!board.TryClearPiecesByBoosterSequence(
                    path,
                    randomWalkVisualPrefab,
                    randomWalkVisualSpeed,
                    randomWalkVisualLifetimeAfterUse))
            {
                boosterUseInProgress = false;
                yield break;
            }

            DecrementUses(Match3BoosterType.RandomWalk);
            activeBooster = Match3BoosterType.None;
            boosterUseInProgress = false;
            board.SetInputEnabled(true);
            RefreshButtonVisuals();
            boosterUsed.Invoke(Match3BoosterType.RandomWalk);
        }

        private List<Match3PieceView> GetPiecesForBooster(Match3BoosterType boosterType, Match3PieceView selectedPiece)
        {
            return boosterType switch
            {
                Match3BoosterType.PopAnyPiece => new List<Match3PieceView> { selectedPiece },
                Match3BoosterType.PopAllOfColor => board.GetPiecesWithDefinition(selectedPiece.Definition),
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
                popAnyUsesText.text = "x" + popAnyUses.ToString();
            }

            if (popColorUsesText != null)
            {
                popColorUsesText.text = "x" + popColorUses.ToString();
            }

            if (randomWalkUsesText != null)
            {
                randomWalkUsesText.text = "x" + randomWalkUses.ToString();
            }
        }

        private void InitializeButtonVisuals()
        {
            popAnyButtonVisual?.Initialize();
            popColorButtonVisual?.Initialize();
            randomWalkButtonVisual?.Initialize();
        }

        private void RefreshButtonVisuals()
        {
            ApplyButtonVisual(popAnyButtonVisual, activeBooster == Match3BoosterType.PopAnyPiece);
            ApplyButtonVisual(popColorButtonVisual, activeBooster == Match3BoosterType.PopAllOfColor);
            ApplyButtonVisual(randomWalkButtonVisual, activeBooster == Match3BoosterType.RandomWalk);
        }

        private void ApplyButtonVisual(BoosterButtonShaderVisual visual, bool selected)
        {
            visual?.Apply(
                selected,
                outerOutlineFadeProperty,
                selectedOuterOutlineFade,
                idleOuterOutlineFade,
                enchantedFadeProperty,
                selectedEnchantedFade,
                idleEnchantedFade);
        }

        private void OnDestroy()
        {
            popAnyButtonVisual?.Restore();
            popColorButtonVisual?.Restore();
            randomWalkButtonVisual?.Restore();
        }

        private void ApplyControlsLockVisual(bool locked, bool instant)
        {
            if (controlsCanvasGroup == null)
            {
                controlsCanvasGroup = GetComponent<CanvasGroup>();
            }

            if (controlsCanvasGroup == null)
            {
                return;
            }

            controlsCanvasGroup.interactable = !locked;
            controlsCanvasGroup.blocksRaycasts = !locked;

            var targetAlpha = locked ? 0f : 1f;
            if (controlsFadeRoutine != null)
            {
                StopCoroutine(controlsFadeRoutine);
                controlsFadeRoutine = null;
            }

            if (instant || lockFadeDuration <= 0f)
            {
                controlsCanvasGroup.alpha = targetAlpha;
                return;
            }

            controlsFadeRoutine = StartCoroutine(FadeControlsRoutine(targetAlpha));
        }

        private IEnumerator FadeControlsRoutine(float targetAlpha)
        {
            var startAlpha = controlsCanvasGroup.alpha;
            var elapsed = 0f;

            while (elapsed < lockFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / lockFadeDuration);
                controlsCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            controlsCanvasGroup.alpha = targetAlpha;
            controlsFadeRoutine = null;
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
