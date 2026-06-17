using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3Foodie
{
    public sealed class Match3LevelHud : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private Match3LevelController levelController;

        [Header("Timer")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private Image timerProgressImage;
        [SerializeField, Min(0f)] private float timerWarningThreshold = 10f;
        [SerializeField] private Color timerNormalColor = Color.white;
        [SerializeField] private Color timerWarningColor = Color.red;
        [SerializeField, Min(1f)] private float timerWarningPulseScale = 1.14f;
        [SerializeField, Min(0f)] private float timerWarningPulseDuration = 0.16f;

        [Header("Goals")]
        [SerializeField] private Transform goalsRoot;
        [SerializeField] private Match3GoalItemView goalItemPrefab;
        [SerializeField] private List<Match3GoalItemView> goalViews = new();
        [SerializeField] private Match3CollectionTargetProvider collectionTargetProvider;

        [Header("Math Bonus")]
        [SerializeField] private TMP_Text mathBonusCounterText;
        [SerializeField] private RectTransform mathBonusProgressFillRect;
        [SerializeField] private RectTransform mathBonusProgressBoundsRect;
        [SerializeField, Min(0f)] private float mathBonusProgressFullWidth;
        [SerializeField, Min(0f)] private float mathBonusProgressMotionDuration = 0.22f;
        [SerializeField, Min(1f)] private float mathBonusProgressBumpScale = 1.06f;

        [Header("Math Bonus Progress Shader")]
        [SerializeField] private Image mathBonusProgressEffectImage;
        [SerializeField] private string mathBonusProgressRainbowFadeProperty = "_RainbowFade";
        [SerializeField] private bool enableMathBonusProgressRainbow = true;
        [SerializeField, Range(0f, 1f)] private float mathBonusProgressRainbowActiveFade = 1f;
        [SerializeField, Range(0f, 1f)] private float mathBonusProgressRainbowInactiveFade;

        private float cachedMathBonusProgressFullWidth;
        private bool hasCachedMathBonusProgressLayout;
        private bool mathBonusProgressUsesStretchAnchors;
        private Vector2 mathBonusProgressAnchorMin;
        private Vector2 mathBonusProgressAnchorMax;
        private Vector2 mathBonusProgressOffsetMin;
        private Vector2 mathBonusProgressOffsetMax;
        private Coroutine mathBonusProgressRoutine;
        private Coroutine timerWarningPulseRoutine;
        private Material mathBonusProgressOriginalMaterial;
        private Material mathBonusProgressMaterialInstance;
        private int lastWarningTimerSecond = -1;

        private void Awake()
        {
            if (levelController == null)
            {
                levelController = FindAnyObjectByType<Match3LevelController>();
            }

            if (collectionTargetProvider == null)
            {
                collectionTargetProvider = GetComponent<Match3CollectionTargetProvider>();
            }

            if (collectionTargetProvider == null)
            {
                collectionTargetProvider = gameObject.AddComponent<Match3CollectionTargetProvider>();
            }

            InitializeMathBonusProgressEffectMaterial();
        }

        private void OnEnable()
        {
            if (levelController == null)
            {
                return;
            }

            levelController.TimerChanged.AddListener(RefreshTimer);
            levelController.GoalsChanged.AddListener(RefreshGoals);
            levelController.MathBonusCounterChanged.AddListener(RefreshMathBonusCounter);
            RefreshAll();
        }

        private void Start()
        {
            RefreshAll();
        }

        private void OnDisable()
        {
            if (levelController == null)
            {
                return;
            }

            levelController.TimerChanged.RemoveListener(RefreshTimer);
            levelController.GoalsChanged.RemoveListener(RefreshGoals);
            levelController.MathBonusCounterChanged.RemoveListener(RefreshMathBonusCounter);
        }

        private void OnDestroy()
        {
            RestoreMathBonusProgressEffectMaterial();
        }

        public void RefreshAll()
        {
            if (levelController == null)
            {
                return;
            }

            RefreshTimer(levelController.RemainingTime);
            RefreshGoals(new List<Match3GoalProgress>(levelController.Goals));
            RefreshMathBonusCounter(levelController.MathBonusCollectedAmount, levelController.MathBonusRequiredCollections);
        }

        private void RefreshTimer(float remainingSeconds)
        {
            if (timerText != null)
            {
                var seconds = Mathf.CeilToInt(remainingSeconds);
                timerText.text = seconds.ToString();
                RefreshTimerWarningVisual(remainingSeconds, seconds);
            }

            if (timerProgressImage != null && levelController != null && levelController.LevelSettings != null)
            {
                var timeLimit = Mathf.Max(0.01f, levelController.LevelSettings.TimeLimitSeconds);
                timerProgressImage.fillAmount = Mathf.Clamp01(remainingSeconds / timeLimit);
            }
        }

        private void RefreshTimerWarningVisual(float remainingSeconds, int wholeSeconds)
        {
            if (timerText == null)
            {
                return;
            }

            var warningActive = remainingSeconds > 0f && remainingSeconds <= timerWarningThreshold;
            timerText.color = warningActive ? timerWarningColor : timerNormalColor;

            if (!warningActive)
            {
                lastWarningTimerSecond = -1;
                if (timerWarningPulseRoutine != null)
                {
                    StopCoroutine(timerWarningPulseRoutine);
                    timerWarningPulseRoutine = null;
                }

                timerText.transform.localScale = Vector3.one;
                return;
            }

            if (wholeSeconds != lastWarningTimerSecond)
            {
                lastWarningTimerSecond = wholeSeconds;
                if (timerWarningPulseRoutine != null)
                {
                    StopCoroutine(timerWarningPulseRoutine);
                }

                timerWarningPulseRoutine = StartCoroutine(TimerWarningPulseRoutine());
            }
        }

        private IEnumerator TimerWarningPulseRoutine()
        {
            if (timerText == null)
            {
                yield break;
            }

            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, timerWarningPulseDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var pulse = Mathf.Sin(t * Mathf.PI) * (timerWarningPulseScale - 1f);
                timerText.transform.localScale = Vector3.one * (1f + pulse);
                yield return null;
            }

            timerText.transform.localScale = Vector3.one;
            timerWarningPulseRoutine = null;
        }

        private void RefreshGoals(List<Match3GoalProgress> goals)
        {
            if (goalsRoot == null)
            {
                return;
            }

            while (goalViews.Count < goals.Count)
            {
                var createdView = CreateGoalView();
                if (createdView == null)
                {
                    break;
                }

                goalViews.Add(createdView);
            }

            for (var i = 0; i < goalViews.Count; i++)
            {
                var active = i < goals.Count;
                goalViews[i].gameObject.SetActive(active);

                if (active)
                {
                    goalViews[i].Bind(goals[i]);
                    collectionTargetProvider.SetTarget(goals[i].Element, goalViews[i]);
                }
            }
        }

        private void RefreshMathBonusCounter(int collectedAmount, int requiredAmount)
        {
            var active = levelController != null && levelController.MathBonusElement != null && requiredAmount > 0;
            if (mathBonusCounterText != null)
            {
                mathBonusCounterText.gameObject.SetActive(active);
            }

            if (mathBonusProgressFillRect != null)
            {
                mathBonusProgressFillRect.gameObject.SetActive(active);
            }

            if (!active)
            {
                ApplyMathBonusProgressShaderEffect(false);
                return;
            }

            var visibleCollected = Mathf.Max(0, collectedAmount);
            var safeRequired = Mathf.Max(1, requiredAmount);
            if (mathBonusCounterText != null)
            {
                mathBonusCounterText.text = $"{visibleCollected}/{safeRequired}";
            }

            ApplyMathBonusProgressShaderEffect(visibleCollected >= safeRequired);
            AnimateMathBonusProgressTo(Mathf.Clamp01(visibleCollected / (float)safeRequired));
        }

        private Match3GoalItemView CreateGoalView()
        {
            if (goalItemPrefab != null)
            {
                return Instantiate(goalItemPrefab, goalsRoot);
            }

            return null;
        }

        private void CacheMathBonusProgressWidth()
        {
            if (mathBonusProgressFillRect == null)
            {
                cachedMathBonusProgressFullWidth = 0f;
                return;
            }

            if (hasCachedMathBonusProgressLayout)
            {
                return;
            }

            if (mathBonusProgressFullWidth > 0f)
            {
                cachedMathBonusProgressFullWidth = mathBonusProgressFullWidth;
            }
            else if (mathBonusProgressBoundsRect != null && mathBonusProgressBoundsRect.rect.width > 0f)
            {
                cachedMathBonusProgressFullWidth = mathBonusProgressBoundsRect.rect.width;
            }
            else
            {
                cachedMathBonusProgressFullWidth = Mathf.Max(mathBonusProgressFillRect.rect.width, mathBonusProgressFillRect.sizeDelta.x);
            }

            mathBonusProgressAnchorMin = mathBonusProgressFillRect.anchorMin;
            mathBonusProgressAnchorMax = mathBonusProgressFillRect.anchorMax;
            mathBonusProgressOffsetMin = mathBonusProgressFillRect.offsetMin;
            mathBonusProgressOffsetMax = mathBonusProgressFillRect.offsetMax;
            mathBonusProgressUsesStretchAnchors = Mathf.Abs(mathBonusProgressAnchorMax.x - mathBonusProgressAnchorMin.x) > 0.001f;
            hasCachedMathBonusProgressLayout = true;
        }

        private void AnimateMathBonusProgressTo(float progress)
        {
            if (mathBonusProgressFillRect == null)
            {
                return;
            }

            CacheMathBonusProgressWidth();
            progress = Mathf.Clamp01(progress);

            if (!Application.isPlaying || mathBonusProgressMotionDuration <= 0f)
            {
                SetMathBonusProgressWidth(progress);
                return;
            }

            if (mathBonusProgressRoutine != null)
            {
                StopCoroutine(mathBonusProgressRoutine);
            }

            mathBonusProgressRoutine = StartCoroutine(MathBonusProgressRoutine(progress));
        }

        private IEnumerator MathBonusProgressRoutine(float targetProgress)
        {
            var startProgress = GetCurrentMathBonusProgress();
            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, mathBonusProgressMotionDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutCubic(t);
                ApplyMathBonusProgress(Mathf.LerpUnclamped(startProgress, targetProgress, eased));

                var bump = Mathf.Sin(t * Mathf.PI) * (mathBonusProgressBumpScale - 1f);
                mathBonusProgressFillRect.localScale = new Vector3(1f, 1f + bump, 1f);
                yield return null;
            }

            ApplyMathBonusProgress(targetProgress);
            mathBonusProgressFillRect.localScale = Vector3.one;
            mathBonusProgressRoutine = null;
        }

        private void SetMathBonusProgressWidth(float progress)
        {
            if (mathBonusProgressFillRect == null)
            {
                return;
            }

            ApplyMathBonusProgress(progress);
            mathBonusProgressFillRect.localScale = Vector3.one;
        }

        private float GetCurrentMathBonusProgress()
        {
            if (mathBonusProgressFillRect == null)
            {
                return 0f;
            }

            if (mathBonusProgressUsesStretchAnchors)
            {
                var span = Mathf.Max(0.0001f, mathBonusProgressAnchorMax.x - mathBonusProgressAnchorMin.x);
                return Mathf.Clamp01((mathBonusProgressFillRect.anchorMax.x - mathBonusProgressAnchorMin.x) / span);
            }

            return cachedMathBonusProgressFullWidth <= 0f
                ? 0f
                : Mathf.Clamp01(mathBonusProgressFillRect.rect.width / cachedMathBonusProgressFullWidth);
        }

        private void ApplyMathBonusProgress(float progress)
        {
            if (mathBonusProgressFillRect == null)
            {
                return;
            }

            progress = Mathf.Clamp01(progress);

            if (mathBonusProgressUsesStretchAnchors)
            {
                var anchorMax = mathBonusProgressAnchorMax;
                anchorMax.x = Mathf.Lerp(mathBonusProgressAnchorMin.x, mathBonusProgressAnchorMax.x, progress);
                mathBonusProgressFillRect.anchorMin = mathBonusProgressAnchorMin;
                mathBonusProgressFillRect.anchorMax = anchorMax;
                mathBonusProgressFillRect.offsetMin = mathBonusProgressOffsetMin;
                mathBonusProgressFillRect.offsetMax = new Vector2(
                    Mathf.Lerp(0f, mathBonusProgressOffsetMax.x, progress),
                    mathBonusProgressOffsetMax.y);
                return;
            }

            mathBonusProgressFillRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Max(0f, cachedMathBonusProgressFullWidth * progress));
        }

        private static float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        }

        private void InitializeMathBonusProgressEffectMaterial()
        {
            if (mathBonusProgressEffectImage == null || mathBonusProgressMaterialInstance != null)
            {
                return;
            }

            mathBonusProgressOriginalMaterial = mathBonusProgressEffectImage.material;
            var sourceMaterial = mathBonusProgressEffectImage.material != null
                ? mathBonusProgressEffectImage.material
                : mathBonusProgressEffectImage.materialForRendering;
            if (sourceMaterial == null)
            {
                return;
            }

            mathBonusProgressMaterialInstance = Instantiate(sourceMaterial);
            mathBonusProgressMaterialInstance.name = sourceMaterial.name + " (Math Bonus Progress Instance)";
            mathBonusProgressEffectImage.material = mathBonusProgressMaterialInstance;
            ApplyMathBonusProgressShaderEffect(false);
        }

        private void ApplyMathBonusProgressShaderEffect(bool active)
        {
            InitializeMathBonusProgressEffectMaterial();

            if (mathBonusProgressMaterialInstance == null
                || string.IsNullOrWhiteSpace(mathBonusProgressRainbowFadeProperty)
                || !mathBonusProgressMaterialInstance.HasProperty(mathBonusProgressRainbowFadeProperty))
            {
                return;
            }

            var fade = active && enableMathBonusProgressRainbow
                ? mathBonusProgressRainbowActiveFade
                : mathBonusProgressRainbowInactiveFade;
            mathBonusProgressMaterialInstance.SetFloat(mathBonusProgressRainbowFadeProperty, fade);
        }

        private void RestoreMathBonusProgressEffectMaterial()
        {
            if (mathBonusProgressEffectImage != null)
            {
                mathBonusProgressEffectImage.material = mathBonusProgressOriginalMaterial;
            }

            if (mathBonusProgressMaterialInstance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(mathBonusProgressMaterialInstance);
            }
            else
            {
                DestroyImmediate(mathBonusProgressMaterialInstance);
            }

            mathBonusProgressMaterialInstance = null;
            mathBonusProgressOriginalMaterial = null;
        }
    }
}
