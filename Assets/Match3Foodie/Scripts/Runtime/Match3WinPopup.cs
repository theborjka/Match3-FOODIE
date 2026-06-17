using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Match3Foodie
{
    public sealed class Match3WinPopup : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private Match3LevelController levelController;

        [Header("UI")]
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Button restartButton;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float fadeDuration = 0.2f;
        [SerializeField, Min(0f)] private float panelFallDuration = 0.36f;
        [SerializeField] private Vector2 panelFallOffset = new(0f, 520f);
        [SerializeField, Min(1f)] private float panelOvershootScale = 1.04f;

        private Vector2 panelShownPosition;
        private Coroutine showRoutine;

        private void Awake()
        {
            if (levelController == null)
            {
                levelController = FindAnyObjectByType<Match3LevelController>();
            }

            if (root == null)
            {
                root = gameObject;
            }

            if (panel != null)
            {
                panelShownPosition = panel.anchoredPosition;
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(RestartLevel);
            }

            HideImmediate();
        }

        private void OnEnable()
        {
            if (levelController != null)
            {
                levelController.LevelCompleted.AddListener(Show);
            }
        }

        private void OnDisable()
        {
            if (levelController != null)
            {
                levelController.LevelCompleted.RemoveListener(Show);
            }
        }

        public void Show()
        {
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
            }

            showRoutine = StartCoroutine(ShowRoutine());
        }

        public void HideImmediate()
        {
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }

            if (fadeGroup != null)
            {
                fadeGroup.alpha = 0f;
                fadeGroup.interactable = false;
                fadeGroup.blocksRaycasts = false;
            }

            if (panel != null)
            {
                panel.anchoredPosition = panelShownPosition + panelFallOffset;
                panel.localScale = Vector3.one;
            }

            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private IEnumerator ShowRoutine()
        {
            if (root != null)
            {
                root.SetActive(true);
            }

            if (fadeGroup != null)
            {
                fadeGroup.alpha = 0f;
                fadeGroup.interactable = true;
                fadeGroup.blocksRaycasts = true;
            }

            if (panel != null)
            {
                panel.anchoredPosition = panelShownPosition + panelFallOffset;
                panel.localScale = Vector3.one * 0.96f;
            }

            var duration = Mathf.Max(fadeDuration, panelFallDuration, 0.01f);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                if (fadeGroup != null)
                {
                    var fadeT = fadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeDuration);
                    fadeGroup.alpha = Mathf.SmoothStep(0f, 1f, fadeT);
                }

                if (panel != null)
                {
                    var panelT = panelFallDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / panelFallDuration);
                    var eased = EaseOutBack(panelT);
                    panel.anchoredPosition = Vector2.LerpUnclamped(panelShownPosition + panelFallOffset, panelShownPosition, eased);
                    var scale = panelT < 0.65f
                        ? Mathf.LerpUnclamped(0.96f, panelOvershootScale, panelT / 0.65f)
                        : Mathf.LerpUnclamped(panelOvershootScale, 1f, Mathf.InverseLerp(0.65f, 1f, panelT));
                    panel.localScale = Vector3.one * scale;
                }

                yield return null;
            }

            if (fadeGroup != null)
            {
                fadeGroup.alpha = 1f;
            }

            if (panel != null)
            {
                panel.anchoredPosition = panelShownPosition;
                panel.localScale = Vector3.one;
            }

            showRoutine = null;
        }

        private void RestartLevel()
        {
            HideImmediate();
            levelController?.RestartLevel();
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            t = Mathf.Clamp01(t);
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
