using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Match3Foodie
{
    public sealed class Match3MathChallengePopup : MonoBehaviour
    {
        [Serializable] public sealed class FloatEvent : UnityEvent<float> { }

        private enum RewardVfxSpace
        {
            World,
            Ui,
        }

        [Header("UI")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform animatedPanel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private Button[] answerButtons = new Button[3];
        [SerializeField] private TMP_Text[] answerTexts = new TMP_Text[3];
        [SerializeField] private TMP_Text rewardAnnouncerText;

        [Header("Progress")]
        [SerializeField] private RectTransform progressFillRect;
        [SerializeField] private RectTransform progressBoundsRect;
        [SerializeField, Min(0f)] private float progressFullWidth;
        [SerializeField, Min(0f)] private float progressMotionDuration = 0.22f;
        [SerializeField, Min(1f)] private float progressBumpScale = 1.08f;

        [Header("Question")]
        [SerializeField, Min(1)] private int minOperand = 1;
        [SerializeField, Min(1)] private int maxOperand = 12;
        [SerializeField, Min(1)] private int questionsPerGame = 3;

        [Header("Feedback")]
        [SerializeField, Min(0f)] private float answerFeedbackDelay = 0.55f;
        [SerializeField] private Color correctGlowColor = new(0.25f, 1f, 0.35f, 1f);
        [SerializeField] private Color wrongGlowColor = new(1f, 0.18f, 0.12f, 1f);

        [Header("Motion")]
        [SerializeField, Min(0f)] private float showDuration = 0.24f;
        [SerializeField, Min(0f)] private float hideDuration = 0.14f;
        [SerializeField, Range(0.1f, 1f)] private float hiddenScale = 0.82f;

        [Header("Reward Announcer")]
        [SerializeField] private string rewardTextFormat = "+{0:0}s";
        [SerializeField, Min(0f)] private float rewardShowDuration = 0.24f;
        [SerializeField, Min(0f)] private float rewardHoldDuration = 0.45f;
        [SerializeField, Min(0f)] private float rewardHideDuration = 0.16f;
        [SerializeField, Min(1f)] private float rewardPopScale = 1.14f;
        [SerializeField] private RectTransform rewardFlyTarget;
        [SerializeField, Min(0f)] private float rewardFlyDuration = 0.45f;
        [SerializeField, Min(1f)] private float rewardArrivePopScale = 1.2f;
        [SerializeField, Min(0f)] private float rewardArrivePopDuration = 0.16f;
        [SerializeField] private GameObject rewardVfxPrefab;
        [SerializeField] private RewardVfxSpace rewardVfxSpace = RewardVfxSpace.World;
        [SerializeField] private RectTransform rewardVfxParent;
        [SerializeField] private Camera rewardWorldVfxCamera;
        [SerializeField, Min(0.01f)] private float rewardWorldVfxCameraDistance = 10f;
        [SerializeField] private Vector3 rewardWorldVfxOffset;
        [SerializeField] private string rewardVfxSortingLayerName;
        [SerializeField] private int rewardVfxSortingOrder = 500;
        [SerializeField, Min(0f)] private float rewardVfxLifetime = 1.5f;

        [Header("Events")]
        [SerializeField] private UnityEvent correctAnswerSelected = new();
        [SerializeField] private UnityEvent wrongAnswerSelected = new();
        [SerializeField] private FloatEvent rewardArrived = new();

        private Action<int> completed;
        private readonly List<Color> defaultButtonColors = new();
        private int correctAnswer;
        private int remainingQuestions;
        private int totalQuestions;
        private int correctAnswers;
        private float rewardSecondsPerCorrectAnswer;
        private float currentRewardSeconds;
        private float cachedProgressFullWidth;
        private Coroutine progressRoutine;
        private bool isOpen;
        private bool isWaitingForFeedback;
        private Coroutine motionRoutine;

        public bool IsOpen => isOpen;
        public int QuestionsPerGame => questionsPerGame;
        public UnityEvent CorrectAnswerSelected => correctAnswerSelected;
        public UnityEvent WrongAnswerSelected => wrongAnswerSelected;
        public FloatEvent RewardArrived => rewardArrived;

        private void Awake()
        {
            Hide();
        }

        public void Show(Action<bool> onCompleted)
        {
            Show(correctCount => onCompleted?.Invoke(correctCount > 0));
        }

        public void Show(Action<int> onCompleted)
        {
            Show(0f, onCompleted);
        }

        public void Show(float rewardSecondsPerCorrectAnswer, Action<int> onCompleted)
        {
            completed = onCompleted;
            this.rewardSecondsPerCorrectAnswer = Mathf.Max(0f, rewardSecondsPerCorrectAnswer);
            isOpen = true;
            isWaitingForFeedback = false;
            totalQuestions = Mathf.Max(1, questionsPerGame);
            remainingQuestions = totalQuestions;
            correctAnswers = 0;

            if (root != null)
            {
                root.SetActive(true);
            }

            PrepareMotionTargets();
            HideRewardAnnouncer(true);
            CacheProgressWidth();
            SetProgressImmediate(0f);
            GenerateQuestion();
            PlayShowMotion();
        }

        public void Hide()
        {
            isOpen = false;

            if (root != null)
            {
                root.SetActive(false);
            }

            HideRewardAnnouncer(true);
        }

        private void GenerateQuestion()
        {
            ResetButtonVisuals();
            SetButtonsInteractable(true);

            var a = Random.Range(minOperand, maxOperand + 1);
            var b = Random.Range(minOperand, maxOperand + 1);
            correctAnswer = a + b;

            if (questionText != null)
            {
                questionText.text = $"{a} + {b} = ?";
            }

            var correctIndex = Random.Range(0, answerButtons.Length);
            var usedAnswers = new HashSet<int> { correctAnswer };
            for (var i = 0; i < answerButtons.Length; i++)
            {
                if (answerButtons[i] == null)
                {
                    continue;
                }

                var answer = i == correctIndex ? correctAnswer : GenerateWrongAnswer(usedAnswers);
                usedAnswers.Add(answer);
                if (answerTexts != null && i < answerTexts.Length && answerTexts[i] != null)
                {
                    answerTexts[i].text = answer.ToString();
                }

                answerButtons[i].onClick.RemoveAllListeners();
                var capturedAnswer = answer;
                answerButtons[i].onClick.AddListener(() => HandleAnswer(capturedAnswer));
            }
        }

        private int GenerateWrongAnswer(HashSet<int> usedAnswers)
        {
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var offset = Random.Range(-5, 6);
                var answer = Mathf.Max(0, correctAnswer + offset);
                if (offset != 0 && !usedAnswers.Contains(answer))
                {
                    return answer;
                }
            }

            var fallback = correctAnswer + 1;
            while (usedAnswers.Contains(fallback))
            {
                fallback++;
            }

            return fallback;
        }

        private void HandleAnswer(int answer)
        {
            if (!isOpen || isWaitingForFeedback)
            {
                return;
            }

            var wasCorrect = answer == correctAnswer;
            if (wasCorrect)
            {
                correctAnswers++;
                correctAnswerSelected.Invoke();
            }
            else
            {
                wrongAnswerSelected.Invoke();
            }
            remainingQuestions--;
            AnimateProgressTo(GetAnsweredProgress());

            StartCoroutine(AnswerFeedbackRoutine(answer, wasCorrect));
        }

        private IEnumerator AnswerFeedbackRoutine(int answer, bool wasCorrect)
        {
            isWaitingForFeedback = true;

            for (var i = 0; i < answerButtons.Length; i++)
            {
                if (!TryGetButtonAnswer(i, out var buttonAnswer))
                {
                    continue;
                }

                if (buttonAnswer == correctAnswer)
                {
                    SetButtonColor(answerButtons[i], correctGlowColor);
                }
                else if (!wasCorrect && buttonAnswer == answer)
                {
                    SetButtonColor(answerButtons[i], wrongGlowColor);
                }
            }

            if (answerFeedbackDelay > 0f)
            {
                yield return new WaitForSeconds(answerFeedbackDelay);
            }

            isWaitingForFeedback = false;

            if (remainingQuestions > 0)
            {
                GenerateQuestion();
                yield break;
            }

            var callback = completed;
            completed = null;
            yield return HideWithMotionRoutine();

            if (correctAnswers > 0)
            {
                yield return PlayRewardAnnouncerRoutine(correctAnswers * rewardSecondsPerCorrectAnswer);
            }
            callback?.Invoke(correctAnswers);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            foreach (var button in answerButtons)
            {
                if (button != null)
                {
                    button.interactable = interactable;
                }
            }
        }

        private void OnValidate()
        {
            minOperand = Mathf.Max(1, minOperand);
            maxOperand = Mathf.Max(minOperand, maxOperand);
            questionsPerGame = Mathf.Max(1, questionsPerGame);
            answerFeedbackDelay = Mathf.Max(0f, answerFeedbackDelay);
            progressFullWidth = Mathf.Max(0f, progressFullWidth);
            progressMotionDuration = Mathf.Max(0f, progressMotionDuration);
            progressBumpScale = Mathf.Max(1f, progressBumpScale);
            showDuration = Mathf.Max(0f, showDuration);
            hideDuration = Mathf.Max(0f, hideDuration);
            rewardShowDuration = Mathf.Max(0f, rewardShowDuration);
            rewardHoldDuration = Mathf.Max(0f, rewardHoldDuration);
            rewardHideDuration = Mathf.Max(0f, rewardHideDuration);
            rewardPopScale = Mathf.Max(1f, rewardPopScale);
            rewardFlyDuration = Mathf.Max(0f, rewardFlyDuration);
            rewardArrivePopScale = Mathf.Max(1f, rewardArrivePopScale);
            rewardArrivePopDuration = Mathf.Max(0f, rewardArrivePopDuration);
            rewardWorldVfxCameraDistance = Mathf.Max(0.01f, rewardWorldVfxCameraDistance);
            rewardVfxLifetime = Mathf.Max(0f, rewardVfxLifetime);
        }

        private void ResetButtonVisuals()
        {
            CacheDefaultButtonColors();
            for (var i = 0; i < answerButtons.Length; i++)
            {
                if (answerButtons[i] != null && i < defaultButtonColors.Count)
                {
                    SetButtonColor(answerButtons[i], defaultButtonColors[i]);
                }
            }
        }

        private float GetAnsweredProgress()
        {
            if (totalQuestions <= 0)
            {
                return 0f;
            }

            var answered = Mathf.Clamp(totalQuestions - remainingQuestions, 0, totalQuestions);
            return answered / (float)totalQuestions;
        }

        private void CacheProgressWidth()
        {
            if (progressFillRect == null)
            {
                cachedProgressFullWidth = 0f;
                return;
            }

            if (progressFullWidth > 0f)
            {
                cachedProgressFullWidth = progressFullWidth;
                return;
            }

            if (progressBoundsRect != null && progressBoundsRect.rect.width > 0f)
            {
                cachedProgressFullWidth = progressBoundsRect.rect.width;
                return;
            }

            if (progressFillRect.rect.width > 0f)
            {
                cachedProgressFullWidth = progressFillRect.rect.width;
                return;
            }

            cachedProgressFullWidth = Mathf.Max(0f, progressFillRect.sizeDelta.x);
        }

        private void SetProgressImmediate(float progress)
        {
            if (progressRoutine != null)
            {
                StopCoroutine(progressRoutine);
                progressRoutine = null;
            }

            SetProgressWidth(Mathf.Clamp01(progress));
            if (progressFillRect != null)
            {
                progressFillRect.localScale = Vector3.one;
            }
        }

        private void AnimateProgressTo(float progress)
        {
            if (progressFillRect == null)
            {
                return;
            }

            if (progressRoutine != null)
            {
                StopCoroutine(progressRoutine);
            }

            progressRoutine = StartCoroutine(ProgressRoutine(Mathf.Clamp01(progress)));
        }

        private IEnumerator ProgressRoutine(float targetProgress)
        {
            var startWidth = progressFillRect.rect.width;
            var targetWidth = cachedProgressFullWidth * targetProgress;
            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, progressMotionDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutCubic(t);
                var width = Mathf.LerpUnclamped(startWidth, targetWidth, eased);
                progressFillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, width));

                var bump = Mathf.Sin(t * Mathf.PI) * (progressBumpScale - 1f);
                progressFillRect.localScale = new Vector3(1f, 1f + bump, 1f);
                yield return null;
            }

            progressFillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, targetWidth));
            progressFillRect.localScale = Vector3.one;
            progressRoutine = null;
        }

        private void SetProgressWidth(float progress)
        {
            if (progressFillRect == null)
            {
                return;
            }

            progressFillRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Max(0f, cachedProgressFullWidth * progress));
        }

        private void CacheDefaultButtonColors()
        {
            while (defaultButtonColors.Count < answerButtons.Length)
            {
                var index = defaultButtonColors.Count;
                var graphic = answerButtons[index] != null ? answerButtons[index].targetGraphic : null;
                defaultButtonColors.Add(graphic != null ? graphic.color : Color.white);
            }
        }

        private static void SetButtonColor(Button button, Color color)
        {
            if (button != null && button.targetGraphic != null)
            {
                button.targetGraphic.color = color;
            }
        }

        private bool TryGetButtonAnswer(int index, out int answer)
        {
            answer = 0;
            return answerButtons != null
                && index >= 0
                && index < answerButtons.Length
                && answerButtons[index] != null
                && answerTexts != null
                && index < answerTexts.Length
                && answerTexts[index] != null
                && int.TryParse(answerTexts[index].text, out answer);
        }

        private void PrepareMotionTargets()
        {
            if (animatedPanel == null && root != null)
            {
                animatedPanel = root.transform as RectTransform;
            }

            if (canvasGroup == null && root != null)
            {
                canvasGroup = root.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = root.AddComponent<CanvasGroup>();
                }
            }
        }

        private IEnumerator PlayRewardAnnouncerRoutine(float rewardSeconds)
        {
            if (rewardAnnouncerText == null)
            {
                yield break;
            }

            HideRewardAnnouncer(true);
            currentRewardSeconds = rewardSeconds;
            rewardAnnouncerText.text = rewardSeconds > 0f
                ? string.Format(rewardTextFormat, rewardSeconds)
                : "Correct!";
            rewardAnnouncerText.gameObject.SetActive(true);
            SpawnRewardVfx();
            yield return RewardAnnouncerRoutine();
        }

        private IEnumerator RewardAnnouncerRoutine()
        {
            var transformToAnimate = rewardAnnouncerText.transform;
            var startPosition = transformToAnimate.position;
            var elapsed = 0f;

            while (elapsed < rewardShowDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = rewardShowDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / rewardShowDuration);
                var scale = Mathf.LerpUnclamped(0f, rewardPopScale, EaseOutBack(t));
                transformToAnimate.localScale = Vector3.one * scale;
                yield return null;
            }

            transformToAnimate.localScale = Vector3.one;

            if (rewardHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(rewardHoldDuration);
            }

            if (rewardFlyTarget != null)
            {
                elapsed = 0f;
                var flyDuration = Mathf.Max(0.01f, rewardFlyDuration);
                while (elapsed < flyDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(elapsed / flyDuration);
                    var eased = EaseInOut(t);
                    transformToAnimate.position = Vector3.LerpUnclamped(startPosition, rewardFlyTarget.position, eased);
                    transformToAnimate.localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, t);
                    yield return null;
                }

                transformToAnimate.position = rewardFlyTarget.position;
                rewardArrived.Invoke(currentRewardSeconds);

                elapsed = 0f;
                var popDuration = Mathf.Max(0.01f, rewardArrivePopDuration);
                while (elapsed < popDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(elapsed / popDuration);
                    var scale = t < 0.5f
                        ? Mathf.LerpUnclamped(0.72f, rewardArrivePopScale, t * 2f)
                        : Mathf.LerpUnclamped(rewardArrivePopScale, 0f, (t - 0.5f) * 2f);
                    transformToAnimate.localScale = Vector3.one * scale;
                    yield return null;
                }
            }
            else
            {
                elapsed = 0f;
                while (elapsed < rewardHideDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var t = rewardHideDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / rewardHideDuration);
                    transformToAnimate.localScale = Vector3.one * Mathf.Lerp(1f, 0f, t);
                    yield return null;
                }
            }

            transformToAnimate.position = startPosition;
            HideRewardAnnouncer(false);
        }

        private void HideRewardAnnouncer(bool stopRoutines)
        {
            if (rewardAnnouncerText == null)
            {
                return;
            }

            rewardAnnouncerText.transform.localScale = Vector3.zero;
            rewardAnnouncerText.gameObject.SetActive(false);
        }

        private void SpawnRewardVfx()
        {
            if (rewardVfxPrefab == null || rewardAnnouncerText == null)
            {
                return;
            }

            var instance = rewardVfxSpace == RewardVfxSpace.Ui
                ? SpawnRewardUiVfx()
                : SpawnRewardWorldVfx();

            if (instance == null)
            {
                return;
            }

            instance.SetActive(true);
            PlayRewardVfx(instance);

            if (rewardVfxLifetime > 0f)
            {
                Destroy(instance, rewardVfxLifetime);
            }
        }

        private GameObject SpawnRewardUiVfx()
        {
            var parent = ResolveRewardVfxParent();
            var instance = parent != null
                ? Instantiate(rewardVfxPrefab, parent, false)
                : Instantiate(rewardVfxPrefab);

            instance.transform.SetAsLastSibling();

            if (instance.transform is RectTransform rectTransform && rewardAnnouncerText.transform is RectTransform announcerRect)
            {
                PlaceRewardVfxRect(rectTransform, announcerRect, parent);
                rectTransform.localRotation = Quaternion.identity;
                rectTransform.localScale = Vector3.one;
            }

            return instance;
        }

        private GameObject SpawnRewardWorldVfx()
        {
            var camera = ResolveRewardWorldVfxCamera();
            if (camera == null)
            {
                Debug.LogWarning("Reward world VFX needs a camera. Assign Reward World Vfx Camera or tag your main camera as MainCamera.", this);
                return null;
            }

            var instance = Instantiate(rewardVfxPrefab);
            var screenPosition = RectTransformUtility.WorldToScreenPoint(GetRewardTextCanvasCamera(), rewardAnnouncerText.transform.position);
            var worldPosition = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, rewardWorldVfxCameraDistance));
            instance.transform.position = worldPosition + rewardWorldVfxOffset;
            instance.transform.rotation = camera.transform.rotation;
            ApplyRewardVfxSorting(instance);
            return instance;
        }

        private RectTransform ResolveRewardVfxParent()
        {
            if (rewardVfxParent != null)
            {
                return rewardVfxParent;
            }

            if (rewardAnnouncerText.transform.parent is RectTransform textParent)
            {
                return textParent;
            }

            return root != null ? root.transform as RectTransform : null;
        }

        private static void PlaceRewardVfxRect(RectTransform vfxRect, RectTransform announcerRect, RectTransform parent)
        {
            if (parent == null || parent == announcerRect.parent)
            {
                vfxRect.anchoredPosition = announcerRect.anchoredPosition;
                return;
            }

            var canvas = parent.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var screenPosition = RectTransformUtility.WorldToScreenPoint(camera, announcerRect.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, camera, out var localPosition))
            {
                vfxRect.anchoredPosition = localPosition;
            }
        }

        private static void PlayRewardVfx(GameObject instance)
        {
            var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particleSystem in particleSystems)
            {
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }
        }

        private Camera ResolveRewardWorldVfxCamera()
        {
            if (rewardWorldVfxCamera != null)
            {
                return rewardWorldVfxCamera;
            }

            return Camera.main;
        }

        private Camera GetRewardTextCanvasCamera()
        {
            var canvas = rewardAnnouncerText != null ? rewardAnnouncerText.GetComponentInParent<Canvas>() : null;
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        private void ApplyRewardVfxSorting(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (var renderer in renderers)
            {
                if (!string.IsNullOrWhiteSpace(rewardVfxSortingLayerName))
                {
                    renderer.sortingLayerName = rewardVfxSortingLayerName;
                }

                renderer.sortingOrder = rewardVfxSortingOrder;
            }
        }

        private void PlayShowMotion()
        {
            if (motionRoutine != null)
            {
                StopCoroutine(motionRoutine);
            }

            motionRoutine = StartCoroutine(ShowMotionRoutine());
        }

        private IEnumerator ShowMotionRoutine()
        {
            PrepareMotionTargets();

            if (showDuration <= 0f)
            {
                ApplyMotionState(1f, 1f);
                motionRoutine = null;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < showDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / showDuration);
                ApplyMotionState(EaseOutBack(t), t);
                yield return null;
            }

            ApplyMotionState(1f, 1f);
            motionRoutine = null;
        }

        private IEnumerator HideWithMotionRoutine()
        {
            if (motionRoutine != null)
            {
                StopCoroutine(motionRoutine);
                motionRoutine = null;
            }

            if (hideDuration > 0f)
            {
                var elapsed = 0f;
                while (elapsed < hideDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(elapsed / hideDuration);
                    ApplyMotionState(1f - t, 1f - t);
                    yield return null;
                }
            }

            Hide();
        }

        private void ApplyMotionState(float scaleAmount, float alpha)
        {
            if (animatedPanel != null)
            {
                var scale = Mathf.LerpUnclamped(hiddenScale, 1f, scaleAmount);
                animatedPanel.localScale = Vector3.one * scale;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(alpha);
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static float EaseInOut(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
        }

        private static float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        }
    }
}
