using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Random = UnityEngine.Random;

namespace Match3Foodie
{
    public sealed class Match3Board : MonoBehaviour
    {
        private const int MaxBoardGenerationAttempts = 500;
        private const int MaxShuffleAttempts = 100;

        [Serializable] public sealed class PieceEvent : UnityEvent<Match3PieceView> { }
        [Serializable] public sealed class PiecesEvent : UnityEvent<List<Match3PieceView>> { }

        private enum StartMotionDirection
        {
            Left,
            Right,
            Custom,
        }

        [Header("Config")]
        [SerializeField] private Match3BoardSettings settings;
        [SerializeField] private Transform piecesRoot;
        [SerializeField] private Match3CollectionTargetProvider collectionTargetProvider;

        [Header("Start Motion")]
        [SerializeField] private bool playStartMotion = true;
        [SerializeField] private StartMotionDirection startMotionDirection = StartMotionDirection.Left;
        [SerializeField, Min(0f)] private float startMotionDistance = 8f;
        [SerializeField] private Vector3 customStartMotionOffset;
        [SerializeField, Min(0f)] private float startMotionDuration = 0.65f;
        [SerializeField, Min(0f)] private float startMotionElasticDistance = 0.22f;
        [SerializeField, Range(0.05f, 0.95f)] private float startMotionSlidePortion = 0.82f;

        [Header("Input")]
        [SerializeField] private bool inputEnabled = true;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private LayerMask pieceLayerMask = ~0;
        [SerializeField, Min(0.01f)] private float dragThreshold = 0.18f;
        [SerializeField] private bool logInputDebug;

        [Header("Events")]
        [SerializeField] private PieceEvent pieceSelected = new();
        [SerializeField] private PieceEvent pieceCollected = new();
        [SerializeField] private PieceEvent pieceCleared = new();
        [SerializeField] private PiecesEvent piecesMatched = new();
        [SerializeField] private UnityEvent boardSettled = new();

        private Match3PieceView[,] pieces;
        private Match3PieceView selectedPiece;
        private Match3PieceView pointerDownPiece;
        private Vector3 pointerDownWorld;
        private readonly HashSet<Match3PieceView> specialClearedPieces = new();
        private Vector3 boardBaseLocalPosition;
        private Coroutine startMotionRoutine;
        private bool isResolving;
        private bool hasPlayedStartMotion;
        private int totalSpawnWeight;

        public Match3BoardSettings Settings => settings;
        public bool IsResolving => isResolving;
        public PieceEvent PieceSelected => pieceSelected;
        public PieceEvent PieceCollected => pieceCollected;
        public PieceEvent PieceCleared => pieceCleared;
        public PiecesEvent PiecesMatched => piecesMatched;
        public UnityEvent BoardSettled => boardSettled;
        public int Width => settings != null ? settings.Width : 0;
        public int Height => settings != null ? settings.Height : 0;

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!inputEnabled)
            {
                pointerDownPiece = null;
                SetSelectedPiece(null);
            }
        }

        public Match3PieceView GetPiece(Match3GridPosition position)
        {
            return pieces != null && IsInside(position) ? pieces[position.X, position.Y] : null;
        }

        public bool TryGetPieceAtScreenPosition(Vector2 screenPosition, out Match3PieceView piece)
        {
            piece = null;
            if (pieces == null || settings == null)
            {
                return false;
            }

            piece = PieceAtWorld(ScreenToBoardWorld(screenPosition));
            return piece != null;
        }

        public bool TryClearPiecesByBooster(IEnumerable<Match3PieceView> piecesToClear)
        {
            if (isResolving || piecesToClear == null)
            {
                return false;
            }

            var clearSet = new HashSet<Match3PieceView>();
            foreach (var piece in piecesToClear)
            {
                if (piece != null)
                {
                    clearSet.Add(piece);
                }
            }

            if (clearSet.Count == 0)
            {
                return false;
            }

            StartCoroutine(ClearBoosterPiecesRoutine(clearSet));
            return true;
        }

        public bool TryClearPiecesByBoosterSequence(
            IEnumerable<Match3PieceView> piecesToClear,
            GameObject visualPrefab,
            float visualSpeed,
            float visualLifetimeAfterUse)
        {
            if (isResolving || piecesToClear == null)
            {
                return false;
            }

            var path = new List<Match3PieceView>();
            var seen = new HashSet<Match3PieceView>();
            foreach (var piece in piecesToClear)
            {
                if (piece != null && seen.Add(piece))
                {
                    path.Add(piece);
                }
            }

            if (path.Count == 0)
            {
                return false;
            }

            StartCoroutine(ClearBoosterPiecesSequenceRoutine(path, visualPrefab, visualSpeed, visualLifetimeAfterUse));
            return true;
        }

        public List<Match3PieceView> GetPiecesWithDefinition(Match3ElementDefinition definition)
        {
            var result = new List<Match3PieceView>();
            if (definition == null || pieces == null)
            {
                return result;
            }

            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    var piece = pieces[x, y];
                    if (piece != null && piece.Definition == definition)
                    {
                        result.Add(piece);
                    }
                }
            }

            return result;
        }

        private void Awake()
        {
            if (piecesRoot == null)
            {
                piecesRoot = transform;
            }

            boardBaseLocalPosition = transform.localPosition;

            if (collectionTargetProvider == null)
            {
                collectionTargetProvider = FindAnyObjectByType<Match3CollectionTargetProvider>();
            }

        }

        private void Start()
        {
            BuildBoard();
        }

        private void Update()
        {
            HandlePointerInput();
        }

        [ContextMenu("Rebuild Board")]
        public void BuildBoard()
        {
            ValidateSettings();

            if (startMotionRoutine != null)
            {
                StopCoroutine(startMotionRoutine);
                startMotionRoutine = null;
            }

            transform.localPosition = boardBaseLocalPosition;
            ClearExistingPieces();
            CacheSpawnWeights();

            pieces = new Match3PieceView[settings.Width, settings.Height];
            var definitions = GeneratePlayableDefinitions();

            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    var position = new Match3GridPosition(x, y);
                    var definition = definitions[x, y];
                    pieces[x, y] = SpawnPiece(definition, position, WorldPosition(position));
                }
            }

            if (Application.isPlaying && playStartMotion && !hasPlayedStartMotion)
            {
                hasPlayedStartMotion = true;
                startMotionRoutine = StartCoroutine(PlayStartMotionRoutine());
                return;
            }

            boardSettled.Invoke();
        }

        public void RebuildBoard(bool replayStartMotion)
        {
            if (replayStartMotion)
            {
                hasPlayedStartMotion = false;
            }

            BuildBoard();
        }

        private IEnumerator PlayStartMotionRoutine()
        {
            var previousInputEnabled = inputEnabled;
            inputEnabled = false;
            isResolving = true;
            pointerDownPiece = null;
            SetSelectedPiece(null);

            var target = boardBaseLocalPosition;
            var offset = GetStartMotionOffset();
            var start = target + offset;
            var slideDirection = offset.sqrMagnitude > 0.0001f
                ? -offset.normalized
                : Vector3.up;
            var overshoot = target + slideDirection * startMotionElasticDistance;

            transform.localPosition = start;

            if (startMotionDuration <= 0f)
            {
                transform.localPosition = target;
                inputEnabled = previousInputEnabled;
                isResolving = false;
                startMotionRoutine = null;
                boardSettled.Invoke();
                yield break;
            }

            var slideDuration = startMotionDuration * startMotionSlidePortion;
            var settleDuration = Mathf.Max(0f, startMotionDuration - slideDuration);
            var elapsed = 0f;

            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                var t = slideDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / slideDuration);
                transform.localPosition = Vector3.LerpUnclamped(start, overshoot, EaseOutCubic(t));
                yield return null;
            }

            if (settleDuration > 0f && startMotionElasticDistance > 0f)
            {
                elapsed = 0f;
                while (elapsed < settleDuration)
                {
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / settleDuration);
                    transform.localPosition = Vector3.LerpUnclamped(overshoot, target, EaseOutCubic(t));
                    yield return null;
                }
            }

            transform.localPosition = target;
            inputEnabled = previousInputEnabled;
            isResolving = false;
            startMotionRoutine = null;
            boardSettled.Invoke();
        }

        public void SelectPiece(Match3PieceView piece)
        {
            if (piece == null || !CanAcceptInput())
            {
                return;
            }

            if (selectedPiece == null)
            {
                SetSelectedPiece(piece);
                return;
            }

            if (selectedPiece == piece)
            {
                SetSelectedPiece(null);
                return;
            }

            if (!AreAdjacent(selectedPiece.GridPosition, piece.GridPosition))
            {
                SetSelectedPiece(piece);
                return;
            }

            StartCoroutine(TrySwapRoutine(selectedPiece, piece));
        }

        public void TryMovePiece(Match3PieceView piece, Vector2Int direction)
        {
            if (piece == null || !CanAcceptInput())
            {
                return;
            }

            var targetPosition = new Match3GridPosition(
                piece.GridPosition.X + direction.x,
                piece.GridPosition.Y + direction.y);

            if (!IsInside(targetPosition))
            {
                return;
            }

            var target = pieces[targetPosition.X, targetPosition.Y];
            if (target != null)
            {
                StartCoroutine(TrySwapRoutine(piece, target));
            }
        }

        public Vector3 WorldPosition(Match3GridPosition gridPosition)
        {
            return transform.TransformPoint(LocalPosition(gridPosition));
        }

        private Vector3 LocalPosition(Match3GridPosition gridPosition)
        {
            var step = settings.StepSize;
            var boardSize = settings.BoardSize;
            var bottomLeftCellCenter = new Vector2(
                -boardSize.x * 0.5f + settings.CellSize * 0.5f,
                -boardSize.y * 0.5f + settings.CellSize * 0.5f);

            return new Vector3(
                bottomLeftCellCenter.x + gridPosition.X * step.x,
                bottomLeftCellCenter.y + gridPosition.Y * step.y,
                0f);
        }

        private void HandlePointerInput()
        {
            if (!inputEnabled || !CanAcceptInput())
            {
                return;
            }

            if (TryGetPointerDown(out var downScreenPosition))
            {
                pointerDownWorld = ScreenToBoardWorld(downScreenPosition);
                pointerDownPiece = PieceAtWorld(pointerDownWorld);
                return;
            }

            if (!TryGetPointerUp(out var upScreenPosition))
            {
                return;
            }

            if (pointerDownPiece == null)
            {
                return;
            }

            var upWorld = ScreenToBoardWorld(upScreenPosition);
            var delta = upWorld - pointerDownWorld;
            var releasedPiece = pointerDownPiece;
            pointerDownPiece = null;

            if (delta.magnitude < dragThreshold)
            {
                SelectPiece(releasedPiece);
                return;
            }

            var direction = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? new Vector2Int(delta.x > 0f ? 1 : -1, 0)
                : new Vector2Int(0, delta.y > 0f ? 1 : -1);

            TryMovePiece(releasedPiece, direction);
        }

        private Match3PieceView PieceAtWorld(Vector3 worldPosition)
        {
            var hit = Physics2D.OverlapPoint(worldPosition, pieceLayerMask);
            if (hit != null)
            {
                var pieceFromCollider = hit.GetComponentInParent<Match3PieceView>();
                if (pieceFromCollider != null)
                {
                    return pieceFromCollider;
                }
            }

            return PieceAtGridPoint(worldPosition);
        }

        private Match3PieceView PieceAtGridPoint(Vector3 worldPosition)
        {
            if (pieces == null || settings == null)
            {
                return null;
            }

            var local = transform.InverseTransformPoint(worldPosition);
            var boardSize = settings.BoardSize;
            var step = settings.StepSize;
            var bottomLeftCellCenter = new Vector2(
                -boardSize.x * 0.5f + settings.CellSize * 0.5f,
                -boardSize.y * 0.5f + settings.CellSize * 0.5f);

            var x = Mathf.RoundToInt((local.x - bottomLeftCellCenter.x) / step.x);
            var y = Mathf.RoundToInt((local.y - bottomLeftCellCenter.y) / step.y);
            var position = new Match3GridPosition(x, y);

            if (!IsInside(position))
            {
                LogInput($"Pointer outside board at world {worldPosition}.");
                return null;
            }

            var cellCenter = new Vector2(
                bottomLeftCellCenter.x + x * step.x,
                bottomLeftCellCenter.y + y * step.y);
            var localPoint = new Vector2(local.x, local.y);
            var halfCell = settings.CellSize * 0.5f;

            if (Mathf.Abs(localPoint.x - cellCenter.x) > halfCell || Mathf.Abs(localPoint.y - cellCenter.y) > halfCell)
            {
                LogInput($"Pointer between cells at world {worldPosition}.");
                return null;
            }

            return pieces[x, y];
        }

        private Vector3 ScreenToBoardWorld(Vector2 screenPosition)
        {
            var camera = inputCamera != null ? inputCamera : Camera.main;
            if (camera == null)
            {
                return Vector3.zero;
            }

            var screenPoint = new Vector3(
                screenPosition.x,
                screenPosition.y,
                Mathf.Abs(camera.transform.position.z - transform.position.z));

            return camera.ScreenToWorldPoint(screenPoint);
        }

        private static bool TryGetPointerDown(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                screenPosition = pointer.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                screenPosition = touch.position;
                return touch.phase == TouchPhase.Began;
            }

            screenPosition = Input.mousePosition;
            return Input.GetMouseButtonDown(0);
#else
            screenPosition = default;
            return false;
#endif
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
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                screenPosition = touch.position;
                return touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            }

            screenPosition = Input.mousePosition;
            return Input.GetMouseButtonUp(0);
#else
            screenPosition = default;
            return false;
#endif
        }

        private void LogInput(string message)
        {
            if (logInputDebug)
            {
                Debug.Log($"[Match3 Input] {message}", this);
            }
        }

        private IEnumerator TrySwapRoutine(Match3PieceView first, Match3PieceView second)
        {
            isResolving = true;
            SetSelectedPiece(null);

            yield return SwapPiecesRoutine(first, second, settings.SwapDuration);

            var matches = FindMatches();
            if (matches.Count == 0)
            {
                yield return SwapPiecesRoutine(first, second, settings.SwapDuration);
                isResolving = false;
                yield break;
            }

            yield return ResolveMatchesRoutine(matches);
            if (!HasAnyValidMove())
            {
                yield return ShuffleBoardRoutine();
            }

            isResolving = false;
            boardSettled.Invoke();
        }

        private IEnumerator ResolveMatchesRoutine(HashSet<Match3PieceView> matches)
        {
            while (matches.Count > 0)
            {
                yield return ResolveSpecialEffectsRoutine(matches);

            var matchedPieces = new List<Match3PieceView>(matches);
            piecesMatched.Invoke(matchedPieces);

                yield return ClearPiecesRoutine(matchedPieces);
                yield return new WaitForSeconds(settings.ClearDelay);
                yield return CollapseColumnsRoutine();
                yield return new WaitForSeconds(settings.RefillDelay);
                yield return RefillColumnsRoutine();

                matches = FindMatches();
            }
        }

        private IEnumerator ClearBoosterPiecesRoutine(HashSet<Match3PieceView> clearSet)
        {
            isResolving = true;
            SetSelectedPiece(null);
            pointerDownPiece = null;

            yield return ResolveSpecialEffectsRoutine(clearSet);

            var clearedPieces = new List<Match3PieceView>();
            foreach (var piece in clearSet)
            {
                if (piece != null)
                {
                    clearedPieces.Add(piece);
                }
            }
            piecesMatched.Invoke(clearedPieces);

            yield return ClearPiecesRoutine(clearedPieces);
            yield return new WaitForSeconds(settings.ClearDelay);

            yield return CollapseColumnsRoutine();
            yield return new WaitForSeconds(settings.RefillDelay);
            yield return RefillColumnsRoutine();

            var matches = FindMatches();
            if (matches.Count > 0)
            {
                yield return ResolveMatchesRoutine(matches);
            }

            if (!HasAnyValidMove())
            {
                yield return ShuffleBoardRoutine();
            }

            isResolving = false;
            boardSettled.Invoke();
        }

        private IEnumerator ClearBoosterPiecesSequenceRoutine(
            List<Match3PieceView> path,
            GameObject visualPrefab,
            float visualSpeed,
            float visualLifetimeAfterUse)
        {
            isResolving = true;
            SetSelectedPiece(null);
            pointerDownPiece = null;

            var clearSet = new HashSet<Match3PieceView>(path);
            var specialRoutines = new List<Coroutine>();
            var visual = CreateSequenceVisual(visualPrefab, path[0]);

            for (var i = 0; i < path.Count; i++)
            {
                var piece = path[i];
                if (piece == null)
                {
                    continue;
                }

                var targetPosition = WorldPosition(piece.GridPosition);
                if (visual != null)
                {
                    yield return MoveSequenceVisualRoutine(visual.transform, targetPosition, visualSpeed);
                }

                ClearSequencePiece(piece, clearSet, specialRoutines);
            }

            DestroySequenceVisual(visual, visualLifetimeAfterUse);

            yield return new WaitForSeconds(settings.ClearDelay);

            foreach (var routine in specialRoutines)
            {
                yield return routine;
            }

            yield return CollapseColumnsRoutine();
            yield return new WaitForSeconds(settings.RefillDelay);
            yield return RefillColumnsRoutine();

            var matches = FindMatches();
            if (matches.Count > 0)
            {
                yield return ResolveMatchesRoutine(matches);
            }

            if (!HasAnyValidMove())
            {
                yield return ShuffleBoardRoutine();
            }

            isResolving = false;
            boardSettled.Invoke();
        }

        private GameObject CreateSequenceVisual(GameObject visualPrefab, Match3PieceView startPiece)
        {
            if (visualPrefab == null || startPiece == null)
            {
                return null;
            }

            var visual = Instantiate(visualPrefab);
            visual.SetActive(true);
            visual.transform.position = WorldPosition(startPiece.GridPosition);

            var trailRenderers = visual.GetComponentsInChildren<TrailRenderer>(true);
            foreach (var trailRenderer in trailRenderers)
            {
                trailRenderer.Clear();
            }

            return visual;
        }

        private IEnumerator MoveSequenceVisualRoutine(Transform visual, Vector3 target, float speed)
        {
            if (visual == null)
            {
                yield break;
            }

            while (visual != null && Vector3.Distance(visual.position, target) > 0.001f)
            {
                visual.position = Vector3.MoveTowards(visual.position, target, Mathf.Max(0.01f, speed) * Time.deltaTime);
                yield return null;
            }

            if (visual != null)
            {
                visual.position = target;
            }
        }

        private void ClearSequencePiece(Match3PieceView piece, HashSet<Match3PieceView> clearSet, List<Coroutine> specialRoutines)
        {
            if (piece == null)
            {
                return;
            }

            piecesMatched.Invoke(new List<Match3PieceView> { piece });

            if (piece.Definition != null && piece.Definition.SpecialEffectType == Match3SpecialEffectType.Fish)
            {
                ClearPieceFromGrid(piece);
                specialRoutines.Add(StartCoroutine(ResolveFishEffectNonBlockingRoutine(piece, clearSet)));
                return;
            }

            StartCoroutine(ClearPiecesRoutine(new List<Match3PieceView> { piece }));
        }

        private void DestroySequenceVisual(GameObject visual, float lifetime)
        {
            if (visual == null)
            {
                return;
            }

            if (lifetime > 0f)
            {
                Destroy(visual, lifetime);
            }
            else
            {
                Destroy(visual);
            }
        }

        private IEnumerator ResolveSpecialEffectsRoutine(HashSet<Match3PieceView> matches)
        {
            var matchedPieces = new List<Match3PieceView>(matches);

            ShuffleList(matchedPieces);
            var fishRoutines = new List<Coroutine>();
            foreach (var piece in matchedPieces)
            {
                if (piece == null || piece.Definition == null)
                {
                    continue;
                }

                if (piece.Definition.SpecialEffectType == Match3SpecialEffectType.Fish)
                {
                    fishRoutines.Add(StartCoroutine(ResolveFishEffectRoutine(piece, matches)));
                }
            }

            foreach (var routine in fishRoutines)
            {
                yield return routine;
            }
        }

        private IEnumerator ResolveFishEffectRoutine(Match3PieceView fishPiece, HashSet<Match3PieceView> matches)
        {
            var delayRange = settings.FishRandomDelay;
            var delay = Random.Range(Mathf.Min(delayRange.x, delayRange.y), Mathf.Max(delayRange.x, delayRange.y));
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            var target = PickRandomNonMatchedPiece(matches);
            if (target == null)
            {
                yield break;
            }

            matches.Add(target);
            var distance = Vector3.Distance(fishPiece.transform.position, target.transform.position);
            var duration = distance / Mathf.Max(0.01f, settings.FishFlightSpeed);
            fishPiece.FlyFishTo(
                target.transform.position,
                settings.FishFlightSpeed,
                settings.FishWaveAmplitude,
                settings.FishWaveFrequency,
                settings.FishFaceFlightDirection,
                settings.FishSpriteForwardAngle,
                settings.FishMaxTiltAngle,
                settings.FishFlightSortingOrderBoost);

            yield return new WaitForSeconds(duration);
            ClearPieceFromGrid(fishPiece);
            specialClearedPieces.Add(fishPiece);
            StartClearVisual(fishPiece);

            ClearPieceFromGrid(target);

            if (target.Definition != null && target.Definition.SpecialEffectType == Match3SpecialEffectType.Fish)
            {
                matches.Add(target);
                yield return ResolveFishEffectRoutine(target, matches);
                yield break;
            }

            specialClearedPieces.Add(target);
            StartClearVisual(target);
        }

        private IEnumerator ResolveFishEffectNonBlockingRoutine(Match3PieceView fishPiece, HashSet<Match3PieceView> matches)
        {
            if (fishPiece == null)
            {
                yield break;
            }

            var target = PickRandomNonMatchedPiece(matches);
            if (target == null)
            {
                StartClearVisual(fishPiece);
                yield break;
            }

            matches.Add(target);

            var delayRange = settings.FishRandomDelay;
            var delay = Random.Range(Mathf.Min(delayRange.x, delayRange.y), Mathf.Max(delayRange.x, delayRange.y));
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (fishPiece == null || target == null)
            {
                if (fishPiece != null)
                {
                    StartClearVisual(fishPiece);
                }

                yield break;
            }

            var targetPosition = target.transform.position;
            var distance = Vector3.Distance(fishPiece.transform.position, targetPosition);
            var duration = distance / Mathf.Max(0.01f, settings.FishFlightSpeed);
            fishPiece.FlyFishTo(
                targetPosition,
                settings.FishFlightSpeed,
                settings.FishWaveAmplitude,
                settings.FishWaveFrequency,
                settings.FishFaceFlightDirection,
                settings.FishSpriteForwardAngle,
                settings.FishMaxTiltAngle,
                settings.FishFlightSortingOrderBoost);

            yield return new WaitForSeconds(duration);

            ClearPieceFromGrid(fishPiece);
            StartClearVisual(fishPiece);

            if (target == null)
            {
                yield break;
            }

            ClearPieceFromGrid(target);

            if (target.Definition != null && target.Definition.SpecialEffectType == Match3SpecialEffectType.Fish)
            {
                yield return ResolveFishEffectNonBlockingRoutine(target, matches);
                yield break;
            }

            StartClearVisual(target);
        }

        private static void ShuffleList<T>(IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var randomIndex = Random.Range(0, i + 1);
                (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
            }
        }

        private Match3PieceView PickRandomNonMatchedPiece(HashSet<Match3PieceView> matches)
        {
            var candidates = new List<Match3PieceView>();
            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    var piece = pieces[x, y];
                    if (piece != null && !matches.Contains(piece))
                    {
                        candidates.Add(piece);
                    }
                }
            }

            return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
        }

        private IEnumerator ShuffleBoardRoutine()
        {
            SetSelectedPiece(null);
            pointerDownPiece = null;

            for (var attempt = 0; attempt < MaxShuffleAttempts; attempt++)
            {
                ShuffleCurrentPieces();

                if (FindMatches().Count == 0 && HasAnyValidMove())
                {
                    yield return MoveAllPiecesToGridRoutine(settings.ShuffleDuration);
                    yield break;
                }
            }

            Debug.LogWarning("Match3 board could not find a clean shuffle with valid moves. Regenerating board.", this);
            BuildBoard();
        }

        private void ShuffleCurrentPieces()
        {
            var shuffledPieces = new List<Match3PieceView>(settings.Width * settings.Height);
            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    if (pieces[x, y] != null)
                    {
                        shuffledPieces.Add(pieces[x, y]);
                    }
                }
            }

            for (var i = shuffledPieces.Count - 1; i > 0; i--)
            {
                var randomIndex = Random.Range(0, i + 1);
                (shuffledPieces[i], shuffledPieces[randomIndex]) = (shuffledPieces[randomIndex], shuffledPieces[i]);
            }

            var index = 0;
            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    var piece = shuffledPieces[index++];
                    var position = new Match3GridPosition(x, y);
                    pieces[x, y] = piece;
                    piece.SetGridPosition(position);
                }
            }
        }

        private IEnumerator MoveAllPiecesToGridRoutine(float duration)
        {
            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    var piece = pieces[x, y];
                    if (piece != null)
                    {
                        piece.MoveTo(WorldPosition(new Match3GridPosition(x, y)), duration);
                    }
                }
            }

            yield return new WaitForSeconds(duration);
        }

        private IEnumerator SwapPiecesRoutine(Match3PieceView first, Match3PieceView second, float duration)
        {
            var firstPosition = first.GridPosition;
            var secondPosition = second.GridPosition;

            pieces[firstPosition.X, firstPosition.Y] = second;
            pieces[secondPosition.X, secondPosition.Y] = first;
            first.SetGridPosition(secondPosition);
            second.SetGridPosition(firstPosition);

            first.MoveTo(WorldPosition(secondPosition), duration);
            second.MoveTo(WorldPosition(firstPosition), duration);
            yield return new WaitForSeconds(duration);
        }

        private IEnumerator ClearPiecesRoutine(List<Match3PieceView> matchedPieces)
        {
            foreach (var piece in matchedPieces)
            {
                if (piece == null)
                {
                    continue;
                }

                var position = piece.GridPosition;
                if (IsInside(position) && pieces[position.X, position.Y] == piece)
                {
                    pieces[position.X, position.Y] = null;
                }

                if (!specialClearedPieces.Contains(piece))
                {
                    StartClearVisual(piece);
                }
            }

            specialClearedPieces.Clear();
            yield break;
        }

        private float StartClearVisual(Match3PieceView piece)
        {
            if (piece == null)
            {
                return 0f;
            }

            pieceCleared.Invoke(piece);

            float duration;
            if (TryStartCollectionFlyer(piece, out var collectDuration))
            {
                piece.PlayDestroyEffect();
                ScheduleDestroyPiece(piece, collectDuration);
                return collectDuration;
            }

            StartCoroutine(piece.PlayDestroyRoutine());
            duration = 0.18f;
            ScheduleDestroyPiece(piece, duration);
            return duration;
        }

        private void ScheduleDestroyPiece(Match3PieceView piece, float delay)
        {
            if (piece != null)
            {
                StartCoroutine(DestroyPieceAfterDelayRoutine(piece, delay));
            }
        }

        private IEnumerator DestroyPieceAfterDelayRoutine(Match3PieceView piece, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (piece != null)
            {
                Destroy(piece.gameObject);
            }
        }

        private bool TryStartCollectionFlyer(Match3PieceView piece, out float duration)
        {
            duration = 0f;
            if (piece == null || piece.Definition == null)
            {
                return false;
            }

            if (collectionTargetProvider == null)
            {
                collectionTargetProvider = FindAnyObjectByType<Match3CollectionTargetProvider>();
            }

            if (collectionTargetProvider == null
                || !collectionTargetProvider.TryGetTarget(piece.Definition, out var canvas, out var target, out _))
            {
                return false;
            }

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return false;
            }

            var camera = inputCamera != null ? inputCamera : Camera.main;
            var canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var screenStart = camera != null
                ? RectTransformUtility.WorldToScreenPoint(camera, piece.transform.position)
                : Vector2.zero;
            var screenTarget = RectTransformUtility.WorldToScreenPoint(canvasCamera, target.position);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenStart, canvasCamera, out var localStart)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenTarget, canvasCamera, out var localTarget))
            {
                return false;
            }

            var flyerSize = GetPieceCanvasSize(piece, canvasRect, camera, canvasCamera);

            var flyerObject = new GameObject($"Collect {piece.Definition.ElementId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(Match3CollectionFlyer));
            flyerObject.transform.SetParent(canvasRect, false);
            flyerObject.transform.SetAsLastSibling();

            var flyer = flyerObject.GetComponent<Match3CollectionFlyer>();
            flyer.Initialize(piece.Definition.Sprite, piece.Definition.Tint, localStart, flyerSize);

            var exitDirection = localStart.sqrMagnitude > 0.001f ? localStart.normalized : Vector2.up;
            var exitDistance = GetWorldDistanceAsCanvasDistance(piece.transform.position, settings.CollectExitDistance, canvasRect, camera, canvasCamera);
            var flightSpeed = GetWorldDistanceAsCanvasDistance(piece.transform.position, settings.CollectFlightSpeed, canvasRect, camera, canvasCamera);
            var flightStart = localStart + exitDirection * exitDistance;
            var flightDuration = Vector2.Distance(flightStart, localTarget) / Mathf.Max(1f, flightSpeed);
            duration = settings.CollectExitDuration + flightDuration + settings.CollectArrivePopDuration;

            var collectedPiece = piece;
            StartCoroutine(flyer.FlyRoutine(
                localTarget,
                flightSpeed,
                exitDistance,
                settings.CollectExitDuration,
                settings.CollectArrivePopScale,
                settings.CollectArrivePopDuration,
                () =>
                {
                    pieceCollected.Invoke(collectedPiece);
                    collectionTargetProvider.PlayBump(collectedPiece.Definition);
                }));

            piece.gameObject.SetActive(false);
            return true;
        }

        private Vector2 GetPieceCanvasSize(Match3PieceView piece, RectTransform canvasRect, Camera worldCamera, Camera canvasCamera)
        {
            var visualSize = piece.VisualWorldSize;
            var center = piece.transform.position;
            var right = center + transform.right * visualSize.x;
            var up = center + transform.up * visualSize.y;

            if (!TryWorldToCanvasLocal(center, canvasRect, worldCamera, canvasCamera, out var localCenter)
                || !TryWorldToCanvasLocal(right, canvasRect, worldCamera, canvasCamera, out var localRight)
                || !TryWorldToCanvasLocal(up, canvasRect, worldCamera, canvasCamera, out var localUp))
            {
                return Vector2.one * 64f;
            }

            return new Vector2(
                Mathf.Max(1f, Vector2.Distance(localCenter, localRight)),
                Mathf.Max(1f, Vector2.Distance(localCenter, localUp)));
        }

        private float GetWorldDistanceAsCanvasDistance(Vector3 worldOrigin, float worldDistance, RectTransform canvasRect, Camera worldCamera, Camera canvasCamera)
        {
            if (!TryWorldToCanvasLocal(worldOrigin, canvasRect, worldCamera, canvasCamera, out var localOrigin)
                || !TryWorldToCanvasLocal(worldOrigin + transform.up * worldDistance, canvasRect, worldCamera, canvasCamera, out var localOffset))
            {
                return worldDistance * 96f;
            }

            return Vector2.Distance(localOrigin, localOffset);
        }

        private static bool TryWorldToCanvasLocal(Vector3 worldPosition, RectTransform canvasRect, Camera worldCamera, Camera canvasCamera, out Vector2 localPosition)
        {
            var screenPosition = worldCamera != null
                ? RectTransformUtility.WorldToScreenPoint(worldCamera, worldPosition)
                : Vector2.zero;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, canvasCamera, out localPosition);
        }

        private void ClearPieceFromGrid(Match3PieceView piece)
        {
            if (piece == null)
            {
                return;
            }

            var position = piece.GridPosition;
            if (IsInside(position) && pieces[position.X, position.Y] == piece)
            {
                pieces[position.X, position.Y] = null;
            }
        }

        private IEnumerator CollapseColumnsRoutine()
        {
            var longestDuration = 0f;

            for (var x = 0; x < settings.Width; x++)
            {
                var writeY = 0;
                for (var readY = 0; readY < settings.Height; readY++)
                {
                    var piece = pieces[x, readY];
                    if (piece == null)
                    {
                        continue;
                    }

                    if (writeY != readY)
                    {
                        pieces[x, writeY] = piece;
                        pieces[x, readY] = null;

                        var target = new Match3GridPosition(x, writeY);
                        var distance = readY - writeY;
                        var duration = Mathf.Max(settings.FallDurationPerCell * distance, settings.FallDurationPerCell);
                        piece.SetGridPosition(target);
                        piece.FallTo(WorldPosition(target), duration, settings.FallImpactBounceDistance, settings.FallImpactBounceDuration);
                        longestDuration = Mathf.Max(longestDuration, duration + settings.FallImpactBounceDuration);
                    }

                    writeY++;
                }
            }

            if (longestDuration > 0f)
            {
                yield return new WaitForSeconds(longestDuration);
            }
        }

        private IEnumerator RefillColumnsRoutine()
        {
            var longestDuration = 0f;

            for (var x = 0; x < settings.Width; x++)
            {
                var missing = 0;
                for (var y = 0; y < settings.Height; y++)
                {
                    if (pieces[x, y] != null)
                    {
                        continue;
                    }

                    var target = new Match3GridPosition(x, y);
                    var spawn = new Match3GridPosition(x, settings.Height + missing);
                    var piece = SpawnPiece(PickRandomDefinition(), target, WorldPosition(spawn));
                    pieces[x, y] = piece;

                    var distance = spawn.Y - y;
                    var duration = Mathf.Max(settings.RefillFallDurationPerCell * distance, settings.RefillFallDurationPerCell);
                    var delayRange = settings.RefillRandomDelay;
                    var delay = Random.Range(Mathf.Min(delayRange.x, delayRange.y), Mathf.Max(delayRange.x, delayRange.y));
                    piece.SpawnDropTo(
                        WorldPosition(target),
                        duration,
                        delay,
                        settings.RefillSpawnScale,
                        settings.RefillPopScale,
                        settings.FallImpactBounceDistance,
                        settings.FallImpactBounceDuration);
                    longestDuration = Mathf.Max(longestDuration, duration + delay + settings.FallImpactBounceDuration);
                    missing++;
                }
            }

            if (longestDuration > 0f)
            {
                yield return new WaitForSeconds(longestDuration);
            }
        }

        private HashSet<Match3PieceView> FindMatches()
        {
            var matches = new HashSet<Match3PieceView>();

            if (settings.MatchLines)
            {
                FindLineMatches(matches);
            }

            if (settings.MatchSquares)
            {
                FindSquareMatches(matches);
            }

            if (settings.MatchTShapes)
            {
                FindTShapeMatches(matches);
            }

            if (settings.MatchCrosses)
            {
                FindCrossMatches(matches);
            }

            if (settings.MatchCorners)
            {
                FindCornerMatches(matches);
            }

            return matches;
        }

        private void FindLineMatches(HashSet<Match3PieceView> matches)
        {
            for (var y = 0; y < settings.Height; y++)
            {
                var runStart = 0;
                for (var x = 1; x <= settings.Width; x++)
                {
                    if (x < settings.Width && SameDefinition(pieces[runStart, y], pieces[x, y]))
                    {
                        continue;
                    }

                    AddRunIfMatch(matches, runStart, y, x - runStart, true);
                    runStart = x;
                }
            }

            for (var x = 0; x < settings.Width; x++)
            {
                var runStart = 0;
                for (var y = 1; y <= settings.Height; y++)
                {
                    if (y < settings.Height && SameDefinition(pieces[x, runStart], pieces[x, y]))
                    {
                        continue;
                    }

                    AddRunIfMatch(matches, x, runStart, y - runStart, false);
                    runStart = y;
                }
            }
        }

        private void FindSquareMatches(HashSet<Match3PieceView> matches)
        {
            for (var y = 0; y < settings.Height - 1; y++)
            {
                for (var x = 0; x < settings.Width - 1; x++)
                {
                    var origin = pieces[x, y];
                    if (origin == null)
                    {
                        continue;
                    }

                    if (SameDefinition(origin, pieces[x + 1, y])
                        && SameDefinition(origin, pieces[x, y + 1])
                        && SameDefinition(origin, pieces[x + 1, y + 1])
                        && IsExactSquareMatch(x, y, origin))
                    {
                        matches.Add(origin);
                        matches.Add(pieces[x + 1, y]);
                        matches.Add(pieces[x, y + 1]);
                        matches.Add(pieces[x + 1, y + 1]);
                    }
                }
            }
        }

        private bool IsExactSquareMatch(int x, int y, Match3PieceView origin)
        {
            return !SameDefinitionAt(origin, x - 1, y)
                && !SameDefinitionAt(origin, x - 1, y + 1)
                && !SameDefinitionAt(origin, x + 2, y)
                && !SameDefinitionAt(origin, x + 2, y + 1)
                && !SameDefinitionAt(origin, x, y - 1)
                && !SameDefinitionAt(origin, x + 1, y - 1)
                && !SameDefinitionAt(origin, x, y + 2)
                && !SameDefinitionAt(origin, x + 1, y + 2);
        }

        private bool SameDefinitionAt(Match3PieceView origin, int x, int y)
        {
            return x >= 0
                && x < settings.Width
                && y >= 0
                && y < settings.Height
                && SameDefinition(origin, pieces[x, y]);
        }

        private void FindTShapeMatches(HashSet<Match3PieceView> matches)
        {
            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    var center = pieces[x, y];
                    if (center == null)
                    {
                        continue;
                    }

                    var left = CountSameDirection(x, y, -1, 0);
                    var right = CountSameDirection(x, y, 1, 0);
                    var down = CountSameDirection(x, y, 0, -1);
                    var up = CountSameDirection(x, y, 0, 1);

                    AddTShapeIfMatch(matches, x, y, -1, 0, 1, 0, 0, -1, left, right, down);
                    AddTShapeIfMatch(matches, x, y, -1, 0, 1, 0, 0, 1, left, right, up);
                    AddTShapeIfMatch(matches, x, y, 0, -1, 0, 1, -1, 0, down, up, left);
                    AddTShapeIfMatch(matches, x, y, 0, -1, 0, 1, 1, 0, down, up, right);
                }
            }
        }

        private void FindCrossMatches(HashSet<Match3PieceView> matches)
        {
            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    var center = pieces[x, y];
                    if (center == null)
                    {
                        continue;
                    }

                    var left = CountSameDirection(x, y, -1, 0);
                    var right = CountSameDirection(x, y, 1, 0);
                    var down = CountSameDirection(x, y, 0, -1);
                    var up = CountSameDirection(x, y, 0, 1);

                    AddCrossIfMatch(matches, x, y, left, right, down, up);
                }
            }
        }

        private void FindCornerMatches(HashSet<Match3PieceView> matches)
        {
            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    var center = pieces[x, y];
                    if (center == null)
                    {
                        continue;
                    }

                    var left = CountSameDirection(x, y, -1, 0);
                    var right = CountSameDirection(x, y, 1, 0);
                    var down = CountSameDirection(x, y, 0, -1);
                    var up = CountSameDirection(x, y, 0, 1);

                    AddCornerIfMatch(matches, x, y, -1, 0, left, 0, -1, down);
                    AddCornerIfMatch(matches, x, y, -1, 0, left, 0, 1, up);
                    AddCornerIfMatch(matches, x, y, 1, 0, right, 0, -1, down);
                    AddCornerIfMatch(matches, x, y, 1, 0, right, 0, 1, up);
                }
            }
        }

        private void AddCornerIfMatch(
            HashSet<Match3PieceView> matches,
            int centerX,
            int centerY,
            int firstDeltaX,
            int firstDeltaY,
            int firstLength,
            int secondDeltaX,
            int secondDeltaY,
            int secondLength)
        {
            if (firstLength < 2 || secondLength < 2)
            {
                return;
            }

            matches.Add(pieces[centerX, centerY]);
            AddDirectionalArm(matches, centerX, centerY, firstDeltaX, firstDeltaY, 2);
            AddDirectionalArm(matches, centerX, centerY, secondDeltaX, secondDeltaY, 2);
        }

        private void AddTShapeIfMatch(
            HashSet<Match3PieceView> matches,
            int centerX,
            int centerY,
            int barFirstDeltaX,
            int barFirstDeltaY,
            int barSecondDeltaX,
            int barSecondDeltaY,
            int stemDeltaX,
            int stemDeltaY,
            int barFirstLength,
            int barSecondLength,
            int stemLength)
        {
            if (barFirstLength < 1 || barSecondLength < 1 || stemLength < 2)
            {
                return;
            }

            matches.Add(pieces[centerX, centerY]);
            AddDirectionalArm(matches, centerX, centerY, barFirstDeltaX, barFirstDeltaY, 1);
            AddDirectionalArm(matches, centerX, centerY, barSecondDeltaX, barSecondDeltaY, 1);
            AddDirectionalArm(matches, centerX, centerY, stemDeltaX, stemDeltaY, 2);
        }

        private void AddCrossIfMatch(HashSet<Match3PieceView> matches, int centerX, int centerY, int left, int right, int down, int up)
        {
            if (left < 1 || right < 1 || down < 1 || up < 1)
            {
                return;
            }

            matches.Add(pieces[centerX, centerY]);
            AddDirectionalArm(matches, centerX, centerY, -1, 0, 1);
            AddDirectionalArm(matches, centerX, centerY, 1, 0, 1);
            AddDirectionalArm(matches, centerX, centerY, 0, -1, 1);
            AddDirectionalArm(matches, centerX, centerY, 0, 1, 1);
        }

        private int CountSameDirection(int startX, int startY, int deltaX, int deltaY)
        {
            var center = pieces[startX, startY];
            if (center == null)
            {
                return 0;
            }

            var count = 0;
            var x = startX + deltaX;
            var y = startY + deltaY;

            while (x >= 0 && x < settings.Width && y >= 0 && y < settings.Height && SameDefinition(center, pieces[x, y]))
            {
                count++;
                x += deltaX;
                y += deltaY;
            }

            return count;
        }

        private void AddDirectionalArm(HashSet<Match3PieceView> matches, int centerX, int centerY, int deltaX, int deltaY, int length)
        {
            for (var i = 1; i <= length; i++)
            {
                matches.Add(pieces[centerX + deltaX * i, centerY + deltaY * i]);
            }
        }

        private void AddRunIfMatch(HashSet<Match3PieceView> matches, int x, int y, int length, bool horizontal)
        {
            if (length < 3)
            {
                return;
            }

            for (var i = 0; i < length; i++)
            {
                var piece = horizontal ? pieces[x + i, y] : pieces[x, y + i];
                if (piece != null)
                {
                    matches.Add(piece);
                }
            }
        }

        private Match3PieceView SpawnPiece(Match3ElementDefinition definition, Match3GridPosition gridPosition, Vector3 worldPosition)
        {
            var prefab = definition.PiecePrefab != null ? definition.PiecePrefab : settings.DefaultPiecePrefab;
            Match3PieceView piece;

            if (prefab != null)
            {
                piece = Instantiate(prefab, worldPosition, Quaternion.identity, piecesRoot);
            }
            else
            {
                piece = CreateFallbackPiece(worldPosition);
            }

            ApplyPieceVisualSize(piece);
            piece.Initialize(this, definition, gridPosition);
            return piece;
        }

        private void ApplyPieceVisualSize(Match3PieceView piece)
        {
            var visualScale = settings.ElementWorldSize;
            piece.transform.localScale = Vector3.one * visualScale;
        }

        private Match3PieceView CreateFallbackPiece(Vector3 worldPosition)
        {
            var pieceObject = new GameObject("Piece");
            pieceObject.transform.SetParent(piecesRoot, false);
            pieceObject.transform.position = worldPosition;

            var renderer = pieceObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;

            var collider = pieceObject.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            return pieceObject.AddComponent<Match3PieceView>();
        }

        private Match3ElementDefinition[,] GeneratePlayableDefinitions()
        {
            for (var attempt = 0; attempt < MaxBoardGenerationAttempts; attempt++)
            {
                var definitions = new Match3ElementDefinition[settings.Width, settings.Height];
                for (var y = 0; y < settings.Height; y++)
                {
                    for (var x = 0; x < settings.Width; x++)
                    {
                        definitions[x, y] = PickDefinitionForInitialFill(x, y, definitions);
                    }
                }

                if (!HasAnyMatch(definitions) && HasAnyValidMove(definitions))
                {
                    return definitions;
                }
            }

            throw new InvalidOperationException("Match3 board could not generate a layout with valid moves. Check element spawn weights and board size.");
        }

        private Match3ElementDefinition PickDefinitionForInitialFill(int x, int y, Match3ElementDefinition[,] definitions)
        {
            if (!settings.AvoidInitialMatches)
            {
                return PickRandomDefinition();
            }

            for (var attempt = 0; attempt < 24; attempt++)
            {
                var definition = PickRandomDefinition();
                if (!WouldCreateInitialMatch(x, y, definition, definitions))
                {
                    return definition;
                }
            }

            return PickRandomDefinition();
        }

        private bool WouldCreateInitialMatch(int x, int y, Match3ElementDefinition definition, Match3ElementDefinition[,] definitions)
        {
            var horizontal = x >= 2
                && definitions[x - 1, y] == definition
                && definitions[x - 2, y] == definition;

            var vertical = y >= 2
                && definitions[x, y - 1] == definition
                && definitions[x, y - 2] == definition;

            return horizontal || vertical;
        }

        private bool HasAnyValidMove()
        {
            return HasAnyValidMove(CreateDefinitionGridFromPieces());
        }

        private Match3ElementDefinition[,] CreateDefinitionGridFromPieces()
        {
            var definitions = new Match3ElementDefinition[settings.Width, settings.Height];
            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    definitions[x, y] = pieces[x, y] != null ? pieces[x, y].Definition : null;
                }
            }

            return definitions;
        }

        private bool HasAnyValidMove(Match3ElementDefinition[,] definitions)
        {
            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    if (CanCreateMatchBySwapping(definitions, x, y, x + 1, y)
                        || CanCreateMatchBySwapping(definitions, x, y, x, y + 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool CanCreateMatchBySwapping(Match3ElementDefinition[,] definitions, int firstX, int firstY, int secondX, int secondY)
        {
            if (secondX >= settings.Width || secondY >= settings.Height)
            {
                return false;
            }

            var first = definitions[firstX, firstY];
            var second = definitions[secondX, secondY];
            if (first == null || second == null || first == second)
            {
                return false;
            }

            definitions[firstX, firstY] = second;
            definitions[secondX, secondY] = first;
            var createsMatch = HasAnyMatch(definitions);
            definitions[firstX, firstY] = first;
            definitions[secondX, secondY] = second;

            return createsMatch;
        }

        private bool HasAnyMatch(Match3ElementDefinition[,] definitions)
        {
            if (settings.MatchLines && HasLineMatch(definitions))
            {
                return true;
            }

            if (settings.MatchSquares && HasSquareMatch(definitions))
            {
                return true;
            }

            if (settings.MatchCorners && HasCornerMatch(definitions))
            {
                return true;
            }

            if (settings.MatchTShapes && HasTShapeMatch(definitions))
            {
                return true;
            }

            return settings.MatchCrosses && HasCrossMatch(definitions);
        }

        private bool HasLineMatch(Match3ElementDefinition[,] definitions)
        {
            for (var y = 0; y < settings.Height; y++)
            {
                var runLength = 1;
                for (var x = 1; x < settings.Width; x++)
                {
                    runLength = definitions[x, y] != null && definitions[x, y] == definitions[x - 1, y] ? runLength + 1 : 1;
                    if (runLength >= 3)
                    {
                        return true;
                    }
                }
            }

            for (var x = 0; x < settings.Width; x++)
            {
                var runLength = 1;
                for (var y = 1; y < settings.Height; y++)
                {
                    runLength = definitions[x, y] != null && definitions[x, y] == definitions[x, y - 1] ? runLength + 1 : 1;
                    if (runLength >= 3)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasSquareMatch(Match3ElementDefinition[,] definitions)
        {
            for (var y = 0; y < settings.Height - 1; y++)
            {
                for (var x = 0; x < settings.Width - 1; x++)
                {
                    var definition = definitions[x, y];
                    if (definition != null
                        && definitions[x + 1, y] == definition
                        && definitions[x, y + 1] == definition
                        && definitions[x + 1, y + 1] == definition
                        && IsExactSquareMatch(definitions, x, y, definition))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsExactSquareMatch(Match3ElementDefinition[,] definitions, int x, int y, Match3ElementDefinition definition)
        {
            return !SameDefinitionAt(definitions, definition, x - 1, y)
                && !SameDefinitionAt(definitions, definition, x - 1, y + 1)
                && !SameDefinitionAt(definitions, definition, x + 2, y)
                && !SameDefinitionAt(definitions, definition, x + 2, y + 1)
                && !SameDefinitionAt(definitions, definition, x, y - 1)
                && !SameDefinitionAt(definitions, definition, x + 1, y - 1)
                && !SameDefinitionAt(definitions, definition, x, y + 2)
                && !SameDefinitionAt(definitions, definition, x + 1, y + 2);
        }

        private bool SameDefinitionAt(Match3ElementDefinition[,] definitions, Match3ElementDefinition definition, int x, int y)
        {
            return x >= 0
                && x < settings.Width
                && y >= 0
                && y < settings.Height
                && definitions[x, y] == definition;
        }

        private bool HasTShapeMatch(Match3ElementDefinition[,] definitions)
        {
            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    if (definitions[x, y] == null)
                    {
                        continue;
                    }

                    var left = CountSameDirection(definitions, x, y, -1, 0);
                    var right = CountSameDirection(definitions, x, y, 1, 0);
                    var down = CountSameDirection(definitions, x, y, 0, -1);
                    var up = CountSameDirection(definitions, x, y, 0, 1);

                    if ((left >= 1 && right >= 1 && (down >= 2 || up >= 2))
                        || (down >= 1 && up >= 1 && (left >= 2 || right >= 2)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasCornerMatch(Match3ElementDefinition[,] definitions)
        {
            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    if (definitions[x, y] == null)
                    {
                        continue;
                    }

                    var left = CountSameDirection(definitions, x, y, -1, 0);
                    var right = CountSameDirection(definitions, x, y, 1, 0);
                    var down = CountSameDirection(definitions, x, y, 0, -1);
                    var up = CountSameDirection(definitions, x, y, 0, 1);

                    if ((left >= 2 && down >= 2)
                        || (left >= 2 && up >= 2)
                        || (right >= 2 && down >= 2)
                        || (right >= 2 && up >= 2))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasCrossMatch(Match3ElementDefinition[,] definitions)
        {
            for (var y = 0; y < settings.Height; y++)
            {
                for (var x = 0; x < settings.Width; x++)
                {
                    if (definitions[x, y] == null)
                    {
                        continue;
                    }

                    if (CountSameDirection(definitions, x, y, -1, 0) > 0
                        && CountSameDirection(definitions, x, y, 1, 0) > 0
                        && CountSameDirection(definitions, x, y, 0, -1) > 0
                        && CountSameDirection(definitions, x, y, 0, 1) > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private int CountSameDirection(Match3ElementDefinition[,] definitions, int startX, int startY, int deltaX, int deltaY)
        {
            var definition = definitions[startX, startY];
            if (definition == null)
            {
                return 0;
            }

            var count = 0;
            var x = startX + deltaX;
            var y = startY + deltaY;

            while (x >= 0 && x < settings.Width && y >= 0 && y < settings.Height && definitions[x, y] == definition)
            {
                count++;
                x += deltaX;
                y += deltaY;
            }

            return count;
        }

        private Match3ElementDefinition PickRandomDefinition()
        {
            var roll = Random.Range(0, totalSpawnWeight);
            foreach (var definition in settings.Elements)
            {
                var weight = Mathf.Max(0, definition.SpawnWeight);
                if (weight == 0)
                {
                    continue;
                }

                if (roll < weight)
                {
                    return definition;
                }

                roll -= weight;
            }

            return settings.Elements[0];
        }

        private void CacheSpawnWeights()
        {
            totalSpawnWeight = 0;
            foreach (var definition in settings.Elements)
            {
                if (definition != null)
                {
                    totalSpawnWeight += Mathf.Max(0, definition.SpawnWeight);
                }
            }
        }

        private bool CanAcceptInput() => settings.AllowInputWhileResolving || !isResolving;

        private void SetSelectedPiece(Match3PieceView piece)
        {
            if (selectedPiece != null)
            {
                selectedPiece.SetSelected(false);
            }

            selectedPiece = piece;
            if (selectedPiece != null)
            {
                selectedPiece.SetSelected(true);
                pieceSelected.Invoke(selectedPiece);
            }
        }

        private static bool SameDefinition(Match3PieceView first, Match3PieceView second)
        {
            return first != null && second != null && first.Definition == second.Definition;
        }

        private static bool AreAdjacent(Match3GridPosition first, Match3GridPosition second)
        {
            return Mathf.Abs(first.X - second.X) + Mathf.Abs(first.Y - second.Y) == 1;
        }

        private bool IsInside(Match3GridPosition position)
        {
            return position.X >= 0 && position.X < settings.Width && position.Y >= 0 && position.Y < settings.Height;
        }

        private void ClearExistingPieces()
        {
            if (piecesRoot == null)
            {
                piecesRoot = transform;
            }

            for (var i = piecesRoot.childCount - 1; i >= 0; i--)
            {
                var child = piecesRoot.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void ValidateSettings()
        {
            if (settings == null)
            {
                throw new InvalidOperationException($"{nameof(Match3Board)} requires {nameof(Match3BoardSettings)}.");
            }

            if (settings.Elements == null || settings.Elements.Count < 3)
            {
                throw new InvalidOperationException("Match3 board needs at least three element definitions.");
            }

            foreach (var definition in settings.Elements)
            {
                if (definition == null)
                {
                    throw new InvalidOperationException("Match3 board settings contain an empty element slot.");
                }
            }

            var spawnableElements = 0;
            totalSpawnWeight = 0;
            foreach (var definition in settings.Elements)
            {
                if (definition == null)
                {
                    continue;
                }

                var weight = Mathf.Max(0, definition.SpawnWeight);
                totalSpawnWeight += weight;
                if (weight > 0)
                {
                    spawnableElements++;
                }
            }

            if (totalSpawnWeight <= 0)
            {
                throw new InvalidOperationException("At least one element must have spawn weight greater than zero.");
            }

            if (spawnableElements < 3)
            {
                throw new InvalidOperationException("Match3 board needs at least three elements with spawn weight greater than zero.");
            }
        }

        private static float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        }

        private Vector3 GetStartMotionOffset()
        {
            return startMotionDirection switch
            {
                StartMotionDirection.Left => Vector3.left * startMotionDistance,
                StartMotionDirection.Right => Vector3.right * startMotionDistance,
                StartMotionDirection.Custom => customStartMotionOffset,
                _ => Vector3.left * startMotionDistance,
            };
        }

        private void OnDrawGizmosSelected()
        {
            if (settings == null)
            {
                return;
            }

            Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.45f);
            for (var x = 0; x < settings.Width; x++)
            {
                for (var y = 0; y < settings.Height; y++)
                {
                    var position = new Match3GridPosition(x, y);
                    Gizmos.DrawWireCube(WorldPosition(position), Vector3.one * settings.CellSize * 0.92f);
                }
            }
        }
    }
}
