using System;
using System.Collections;
using UnityEngine;

namespace Match3Foodie
{
    [DefaultExecutionOrder(-1000)]
    public sealed class Match3SplashScreen : MonoBehaviour
    {
        private enum ExitDirection
        {
            Left,
            Right,
            Up,
            Down,
            Custom,
        }

        [Header("Source")]
        [SerializeField] private Match3Board board;
        [SerializeField] private Match3LevelController levelController;

        [Header("UI")]
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform movingRoot;

        [Header("Timing")]
        [SerializeField] private bool playOnStart = true;
        [SerializeField, Min(0f)] private float holdSeconds = 1.5f;
        [SerializeField, Min(0f)] private float exitDuration = 0.45f;

        [Header("Exit Motion")]
        [SerializeField] private ExitDirection exitDirection = ExitDirection.Left;
        [SerializeField] private Vector2 customExitOffset;
        [SerializeField, Min(0f)] private float exitDistance = 1400f;
        [SerializeField, Min(0f)] private float anticipationDistance = 45f;
        [SerializeField, Range(0.05f, 0.95f)] private float anticipationPortion = 0.18f;

        private Vector2 shownPosition;
        private Coroutine playRoutine;

        private void Awake()
        {
            ResolveReferences();

            if (movingRoot != null)
            {
                shownPosition = movingRoot.anchoredPosition;
            }

            if (board != null)
            {
                board.SetBuildOnStart(false);
                board.SetBoardVisible(false);
            }

            ShowImmediate();
        }

        private void Start()
        {
            if (!playOnStart)
            {
                return;
            }

            Play(() => levelController?.StartLevelAfterSplash());
        }

        public void Play(Action completed = null)
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
            }

            playRoutine = StartCoroutine(PlayRoutine(completed));
        }

        public void ShowImmediate()
        {
            if (root == null)
            {
                root = gameObject;
            }

            board?.SetBoardVisible(false);
            root.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (movingRoot != null)
            {
                movingRoot.anchoredPosition = shownPosition;
            }
        }

        public void HideImmediate()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private IEnumerator PlayRoutine(Action completed)
        {
            ShowImmediate();

            if (holdSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(holdSeconds);
            }

            var targetOffset = GetExitOffset();
            var exitPosition = shownPosition + targetOffset;
            var anticipationPosition = shownPosition - targetOffset.normalized * anticipationDistance;
            var anticipationDuration = exitDuration * anticipationPortion;
            var slideDuration = Mathf.Max(0f, exitDuration - anticipationDuration);

            if (movingRoot != null && anticipationDuration > 0f && anticipationDistance > 0f)
            {
                var elapsed = 0f;
                while (elapsed < anticipationDuration)
                {
                    elapsed += AnimationDeltaTime();
                    var t = Mathf.Clamp01(elapsed / anticipationDuration);
                    movingRoot.anchoredPosition = Vector2.LerpUnclamped(shownPosition, anticipationPosition, EaseInOutCubic(t));
                    yield return null;
                }
            }

            if (movingRoot != null && slideDuration > 0f)
            {
                var start = anticipationDuration > 0f && anticipationDistance > 0f
                    ? anticipationPosition
                    : shownPosition;
                var elapsed = 0f;
                while (elapsed < slideDuration)
                {
                    elapsed += AnimationDeltaTime();
                    var t = Mathf.Clamp01(elapsed / slideDuration);
                    movingRoot.anchoredPosition = Vector2.LerpUnclamped(start, exitPosition, EaseInBack(t));
                    yield return null;
                }
            }

            if (movingRoot != null)
            {
                movingRoot.anchoredPosition = exitPosition;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (root != null)
            {
                root.SetActive(false);
            }

            playRoutine = null;
            completed?.Invoke();
            board?.SetBoardVisible(true);
        }

        private Vector2 GetExitOffset()
        {
            return exitDirection switch
            {
                ExitDirection.Left => Vector2.left * exitDistance,
                ExitDirection.Right => Vector2.right * exitDistance,
                ExitDirection.Up => Vector2.up * exitDistance,
                ExitDirection.Down => Vector2.down * exitDistance,
                ExitDirection.Custom => customExitOffset,
                _ => Vector2.left * exitDistance,
            };
        }

        private void ResolveReferences()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (movingRoot == null)
            {
                movingRoot = transform as RectTransform;
            }

            if (board == null)
            {
                board = FindAnyObjectByType<Match3Board>();
            }

            if (levelController == null)
            {
                levelController = FindAnyObjectByType<Match3LevelController>();
            }
        }

        private static float AnimationDeltaTime()
        {
            var delta = Time.smoothDeltaTime > 0f ? Time.smoothDeltaTime : Time.unscaledDeltaTime;
            return Mathf.Clamp(delta, 0f, 1f / 30f);
        }

        private static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        private static float EaseInBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            t = Mathf.Clamp01(t);
            return c3 * t * t * t - c1 * t * t;
        }
    }
}
