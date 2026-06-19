using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Match3Foodie
{
    public sealed class Match3ComboAnnouncer : MonoBehaviour
    {
        [Serializable] public sealed class IntEvent : UnityEvent<int> { }

        private enum MilestoneTrigger
        {
            SingleMatchAtLeast,
            SequenceTotalAtLeast,
        }

        [Serializable]
        private sealed class MilestoneAnnouncement
        {
            [SerializeField] private MilestoneTrigger trigger = MilestoneTrigger.SequenceTotalAtLeast;
            [SerializeField, Min(1)] private int threshold = 10;
            [SerializeField] private string title = "WOW!";
            [SerializeField] private Color titleColor = Color.white;
            [SerializeField] private string countFormat = "{0} TOTAL!";
            [SerializeField] private Color countColor = Color.white;
            [SerializeField] private GameObject vfxPrefab;
            [SerializeField, Min(1f)] private float popScale = 1.45f;
            [SerializeField, Min(0f)] private float holdDuration = 0.65f;
            [SerializeField, Min(0f)] private float shakeStrength = 12f;

            public MilestoneAnnouncement() { }

            public MilestoneAnnouncement(
                MilestoneTrigger trigger,
                int threshold,
                string title,
                Color titleColor,
                string countFormat,
                Color countColor,
                float popScale,
                float holdDuration,
                float shakeStrength)
            {
                this.trigger = trigger;
                this.threshold = Mathf.Max(1, threshold);
                this.title = title;
                this.titleColor = titleColor;
                this.countFormat = countFormat;
                this.countColor = countColor;
                this.popScale = Mathf.Max(1f, popScale);
                this.holdDuration = Mathf.Max(0f, holdDuration);
                this.shakeStrength = Mathf.Max(0f, shakeStrength);
            }

            public MilestoneTrigger Trigger => trigger;
            public int Threshold => threshold;
            public string Title => title;
            public Color TitleColor => titleColor;
            public string CountFormat => countFormat;
            public Color CountColor => countColor.a > 0f ? countColor : Color.white;
            public GameObject VfxPrefab => vfxPrefab;
            public float PopScale => popScale;
            public float HoldDuration => holdDuration;
            public float ShakeStrength => shakeStrength;
        }

        [Header("Source")]
        [SerializeField] private Match3Board board;
        [SerializeField] private Camera worldCamera;

        [Header("UI Templates")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text countText;

        [Header("Placement")]
        [SerializeField] private RectTransform popupParent;
        [SerializeField] private Vector2 popupOffset = new(0f, 82f);
        [SerializeField, Min(0f)] private float randomJitter = 28f;
        [SerializeField, Min(0f)] private float screenPadding = 80f;

        [Header("Text Layout")]
        [SerializeField] private Vector2 titleTextOffset = new(0f, 30f);
        [SerializeField] private Vector2 countTextOffset = new(0f, -28f);
        [SerializeField, Min(1f)] private float managedTextWidth = 560f;
        [SerializeField, Min(1f)] private float managedTitleHeight = 90f;
        [SerializeField, Min(1f)] private float managedCountHeight = 88f;

        [Header("Milestones")]
        [SerializeField] private List<MilestoneAnnouncement> milestones = new()
        {
            new MilestoneAnnouncement(
                MilestoneTrigger.SingleMatchAtLeast,
                5,
                "HUGE!",
                new Color(1f, 0.86f, 0.18f, 1f),
                "+{0} IN ONE!",
                Color.white,
                1.42f,
                0.58f,
                10f),
            new MilestoneAnnouncement(
                MilestoneTrigger.SequenceTotalAtLeast,
                10,
                "WOW!",
                new Color(0.3f, 1f, 1f, 1f),
                "{0} TOTAL!",
                Color.white,
                1.48f,
                0.62f,
                12f),
            new MilestoneAnnouncement(
                MilestoneTrigger.SequenceTotalAtLeast,
                20,
                "INSANE!",
                new Color(1f, 0.36f, 0.95f, 1f),
                "{0} TOTAL!",
                Color.white,
                1.58f,
                0.72f,
                15f),
            new MilestoneAnnouncement(
                MilestoneTrigger.SequenceTotalAtLeast,
                40,
                "LEGENDARY!",
                new Color(1f, 0.2f, 0.12f, 1f),
                "{0} TOTAL!",
                Color.white,
                1.7f,
                0.82f,
                20f),
        };

        [Header("Motion")]
        [SerializeField, Min(0f)] private float showDuration = 0.18f;
        [SerializeField, Min(0f)] private float hideDuration = 0.18f;
        [SerializeField, Min(0f)] private float punchRotation = 7f;
        [SerializeField] private Vector2 enterOffset = new(0f, -36f);
        [SerializeField] private Vector2 exitOffset = new(0f, 34f);

        [Header("VFX")]
        [SerializeField] private RectTransform uiVfxParent;
        [SerializeField] private Transform worldVfxParent;
        [SerializeField] private Vector3 worldVfxOffset;
        [SerializeField, Min(0f)] private float vfxLifetime = 5f;

        [Header("Shake")]
        [SerializeField] private RectTransform shakeRoot;
        [SerializeField, Min(0f)] private float shakeDuration = 0.16f;

        [Header("Events")]
        [SerializeField] private IntEvent announced = new();

        private readonly HashSet<int> announcedMilestones = new();
        private Coroutine shakeRoutine;
        private Vector2 shakeRootBasePosition;
        private int sequenceTotal;

        public IntEvent Announced => announced;

        private void Awake()
        {
            ResolveReferences();
            HideTemplates();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (board != null)
            {
                board.PiecesMatched.AddListener(ShowForMatchedPieces);
                board.BoardSettled.AddListener(ResetSequence);
            }
        }

        private void OnDisable()
        {
            if (board != null)
            {
                board.PiecesMatched.RemoveListener(ShowForMatchedPieces);
                board.BoardSettled.RemoveListener(ResetSequence);
            }
        }

        private void ShowForMatchedPieces(List<Match3PieceView> matchedPieces)
        {
            var currentCount = matchedPieces != null ? matchedPieces.Count : 0;
            if (currentCount <= 0)
            {
                return;
            }

            var previousSequenceTotal = sequenceTotal;
            sequenceTotal += currentCount;

            var milestone = ResolveTriggeredMilestone(currentCount, previousSequenceTotal, sequenceTotal);
            if (milestone == null)
            {
                return;
            }

            var count = milestone.Trigger == MilestoneTrigger.SingleMatchAtLeast ? currentCount : sequenceTotal;
            var uiPosition = GetPopupPosition(matchedPieces);
            var worldPosition = GetMatchedWorldCenter(matchedPieces);
            StartCoroutine(AnnounceRoutine(milestone, count, uiPosition, worldPosition));
        }

        private IEnumerator AnnounceRoutine(
            MilestoneAnnouncement milestone,
            int count,
            Vector2 shownPosition,
            Vector3 worldPosition)
        {
            var popup = CreatePopup(milestone, count, shownPosition);
            if (popup == null)
            {
                yield break;
            }

            PlayVfx(shownPosition, worldPosition, milestone.VfxPrefab);
            PlayShake(milestone.ShakeStrength);
            announced.Invoke(count);

            yield return PopInRoutine(popup.Root, popup.Group, shownPosition, milestone.PopScale);

            if (milestone.HoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(milestone.HoldDuration);
            }

            yield return HideRoutine(popup.Root, popup.Group, shownPosition);

            if (popup.Root != null)
            {
                Destroy(popup.Root.gameObject);
            }
        }

        private PopupInstance CreatePopup(MilestoneAnnouncement milestone, int count, Vector2 shownPosition)
        {
            ResolveReferences();
            if (popupParent == null || milestone == null)
            {
                Debug.LogWarning("Match3ComboAnnouncer needs Popup Parent and Milestone settings to show milestone text.", this);
                return null;
            }

            if (titleText == null && countText == null)
            {
                Debug.LogWarning("Match3ComboAnnouncer needs Title Text or Count Text template to show milestone text.", this);
                return null;
            }

            var popupObject = new GameObject($"Milestone Announcement {milestone.Threshold}", typeof(RectTransform), typeof(CanvasGroup));
            var popupRoot = popupObject.GetComponent<RectTransform>();
            popupRoot.SetParent(popupParent, false);
            popupRoot.anchorMin = new Vector2(0.5f, 0.5f);
            popupRoot.anchorMax = new Vector2(0.5f, 0.5f);
            popupRoot.pivot = new Vector2(0.5f, 0.5f);
            popupRoot.anchoredPosition = shownPosition;
            popupRoot.localScale = Vector3.zero;
            popupRoot.SetAsLastSibling();

            var group = popupObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var title = CreateText(titleText, popupRoot, "Title", milestone.Title, milestone.TitleColor);
            if (title != null)
            {
                ApplyTextRectLayout(title, titleTextOffset, managedTextWidth, managedTitleHeight);
            }

            var countTextValue = string.Format(milestone.CountFormat, count);
            var countInstance = CreateText(countText, popupRoot, "Count", countTextValue, milestone.CountColor);
            if (countInstance != null)
            {
                ApplyTextRectLayout(countInstance, countTextOffset, managedTextWidth, managedCountHeight);
            }

            return new PopupInstance(popupRoot, group);
        }

        private TMP_Text CreateText(TMP_Text template, RectTransform parent, string name, string value, Color color)
        {
            if (template == null || parent == null)
            {
                return null;
            }

            var created = Instantiate(template, parent);
            created.name = name;
            created.gameObject.SetActive(true);
            created.text = value;
            if (created.transform is RectTransform rectTransform)
            {
                rectTransform.localScale = Vector3.one;
                rectTransform.localEulerAngles = Vector3.zero;
            }

            ConfigureText(created, color);
            return created;
        }

        private IEnumerator PopInRoutine(RectTransform popup, CanvasGroup group, Vector2 shownPosition, float targetPopScale)
        {
            if (popup == null)
            {
                yield break;
            }

            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, showDuration);
            var startPosition = shownPosition + enterOffset;
            var popScale = Mathf.Max(1f, targetPopScale);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutBack(t);
                popup.anchoredPosition = Vector2.LerpUnclamped(startPosition, shownPosition, EaseOutCubic(t));
                popup.localScale = Vector3.one * Mathf.LerpUnclamped(0.08f, popScale, eased);
                popup.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * Mathf.PI) * punchRotation);
                SetAlpha(group, t);
                yield return null;
            }

            elapsed = 0f;
            var settleDuration = Mathf.Max(0.01f, showDuration * 0.45f);
            while (elapsed < settleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / settleDuration);
                popup.localScale = Vector3.one * Mathf.LerpUnclamped(popScale, 1f, EaseOutCubic(t));
                popup.localEulerAngles = Vector3.zero;
                yield return null;
            }

            popup.anchoredPosition = shownPosition;
            popup.localScale = Vector3.one;
            popup.localEulerAngles = Vector3.zero;
            SetAlpha(group, 1f);
        }

        private IEnumerator HideRoutine(RectTransform popup, CanvasGroup group, Vector2 shownPosition)
        {
            if (popup == null)
            {
                yield break;
            }

            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, hideDuration);
            var startScale = popup.localScale;
            var startPosition = popup.anchoredPosition;
            var targetPosition = shownPosition + exitOffset;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseInCubic(t);
                popup.localScale = Vector3.LerpUnclamped(startScale, Vector3.one * 0.85f, eased);
                popup.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
                SetAlpha(group, 1f - t);
                yield return null;
            }
        }

        private MilestoneAnnouncement ResolveTriggeredMilestone(
            int currentCount,
            int previousSequenceTotal,
            int currentSequenceTotal)
        {
            if (milestones == null || milestones.Count == 0)
            {
                return null;
            }

            var bestIndex = -1;
            var bestScore = -1;
            for (var i = 0; i < milestones.Count; i++)
            {
                var milestone = milestones[i];
                if (milestone == null || announcedMilestones.Contains(i))
                {
                    continue;
                }

                if (!ShouldTriggerMilestone(milestone, currentCount, previousSequenceTotal, currentSequenceTotal))
                {
                    continue;
                }

                announcedMilestones.Add(i);
                var score = GetMilestonePriority(milestone);
                if (score > bestScore)
                {
                    bestIndex = i;
                    bestScore = score;
                }
            }

            return bestIndex >= 0 ? milestones[bestIndex] : null;
        }

        private Vector2 GetPopupPosition(List<Match3PieceView> matchedPieces)
        {
            if (popupParent == null || matchedPieces == null || matchedPieces.Count == 0)
            {
                return GetFallbackPopupPosition();
            }

            return WorldToPopupPosition(GetMatchedWorldCenter(matchedPieces));
        }

        private Vector3 GetMatchedWorldCenter(List<Match3PieceView> matchedPieces)
        {
            var worldCenter = Vector3.zero;
            var count = 0;
            foreach (var piece in matchedPieces)
            {
                if (piece == null)
                {
                    continue;
                }

                worldCenter += piece.transform.position;
                count++;
            }

            return count > 0 ? worldCenter / count : Vector3.zero;
        }

        private Vector2 WorldToPopupPosition(Vector3 worldPosition)
        {
            ResolveReferences();
            var canvas = popupParent != null ? popupParent.GetComponentInParent<Canvas>() : null;
            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var screenPosition = RectTransformUtility.WorldToScreenPoint(worldCamera, worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popupParent, screenPosition, uiCamera, out var localPosition);

            localPosition += popupOffset + UnityEngine.Random.insideUnitCircle * randomJitter;
            return ClampToParent(localPosition);
        }

        private Vector2 GetFallbackPopupPosition()
        {
            if (popupParent == null)
            {
                return Vector2.zero;
            }

            return ClampToParent(popupOffset + UnityEngine.Random.insideUnitCircle * randomJitter);
        }

        private Vector2 ClampToParent(Vector2 position)
        {
            if (popupParent == null)
            {
                return position;
            }

            var rect = popupParent.rect;
            var halfWidth = rect.width * 0.5f;
            var halfHeight = rect.height * 0.5f;
            return new Vector2(
                Mathf.Clamp(position.x, -halfWidth + screenPadding, halfWidth - screenPadding),
                Mathf.Clamp(position.y, -halfHeight + screenPadding, halfHeight - screenPadding));
        }

        private void PlayVfx(Vector2 anchoredPosition, Vector3 worldPosition, GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            if (!PrefabUsesRectTransform(prefab))
            {
                PlayWorldVfx(prefab, worldPosition);
                return;
            }

            if (popupParent == null)
            {
                return;
            }

            var parent = uiVfxParent != null ? uiVfxParent : popupParent;
            var instance = Instantiate(prefab, parent);
            instance.SetActive(true);

            if (instance.transform is RectTransform vfxRect)
            {
                if (parent == popupParent)
                {
                    vfxRect.anchoredPosition = anchoredPosition;
                }
                else
                {
                    var canvas = popupParent.GetComponentInParent<Canvas>();
                    var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                    var worldPoint = popupParent.TransformPoint(anchoredPosition);
                    var screenPosition = RectTransformUtility.WorldToScreenPoint(camera, worldPoint);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, camera, out var localPosition);
                    vfxRect.anchoredPosition = localPosition;
                }
            }

            PlayParticles(instance);
            DestroyAfterLifetime(instance);
        }

        private void PlayWorldVfx(GameObject prefab, Vector3 worldPosition)
        {
            var instance = worldVfxParent != null ? Instantiate(prefab, worldVfxParent) : Instantiate(prefab);
            instance.transform.position = worldPosition + worldVfxOffset;
            instance.SetActive(true);

            PlayParticles(instance);
            DestroyAfterLifetime(instance);
        }

        private void PlayParticles(GameObject instance)
        {
            foreach (var particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particleSystem.Play(true);
            }
        }

        private void DestroyAfterLifetime(GameObject instance)
        {
            if (instance != null && vfxLifetime > 0f)
            {
                Destroy(instance, vfxLifetime);
            }
        }

        private void PlayShake(float strength)
        {
            if (shakeRoot == null || strength <= 0f || shakeDuration <= 0f)
            {
                return;
            }

            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                shakeRoot.anchoredPosition = shakeRootBasePosition;
            }

            shakeRootBasePosition = shakeRoot.anchoredPosition;
            shakeRoutine = StartCoroutine(ShakeRoutine(strength));
        }

        private IEnumerator ShakeRoutine(float maxStrength)
        {
            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, shakeDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var strength = maxStrength * (1f - t);
                shakeRoot.anchoredPosition = shakeRootBasePosition + UnityEngine.Random.insideUnitCircle * strength;
                yield return null;
            }

            shakeRoot.anchoredPosition = shakeRootBasePosition;
            shakeRoutine = null;
        }

        private void ResolveReferences()
        {
            if (board == null)
            {
                board = FindAnyObjectByType<Match3Board>();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (popupParent == null)
            {
                popupParent = transform.parent as RectTransform;
            }

            if (popupParent != null)
            {
                popupParent.gameObject.SetActive(true);
            }
        }

        private void HideTemplates()
        {
            if (titleText != null)
            {
                titleText.gameObject.SetActive(false);
            }

            if (countText != null)
            {
                countText.gameObject.SetActive(false);
            }
        }

        private void ResetSequence()
        {
            sequenceTotal = 0;
            announcedMilestones.Clear();
        }

        private void ConfigureText(TMP_Text text, Color color)
        {
            if (text == null)
            {
                return;
            }

            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.enableVertexGradient = false;
            text.color = color;
            text.faceColor = color;
            text.ForceMeshUpdate();
        }

        private void ApplyTextRectLayout(TMP_Text text, Vector2 offset, float width, float height)
        {
            if (text == null || text.transform is not RectTransform rectTransform)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = offset;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private static bool ShouldTriggerMilestone(
            MilestoneAnnouncement milestone,
            int currentCount,
            int previousSequenceTotal,
            int currentSequenceTotal)
        {
            return milestone.Trigger switch
            {
                MilestoneTrigger.SingleMatchAtLeast => currentCount >= milestone.Threshold,
                MilestoneTrigger.SequenceTotalAtLeast =>
                    previousSequenceTotal < milestone.Threshold && currentSequenceTotal >= milestone.Threshold,
                _ => false,
            };
        }

        private static int GetMilestonePriority(MilestoneAnnouncement milestone)
        {
            var triggerBonus = milestone.Trigger == MilestoneTrigger.SequenceTotalAtLeast ? 1 : 0;
            return milestone.Threshold * 10 + triggerBonus;
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null)
            {
                group.alpha = Mathf.Clamp01(alpha);
            }
        }

        private static bool PrefabUsesRectTransform(GameObject prefab)
        {
            return prefab != null && prefab.transform is RectTransform;
        }

        private static float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        }

        private static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        private static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private sealed class PopupInstance
        {
            public PopupInstance(RectTransform root, CanvasGroup group)
            {
                Root = root;
                Group = group;
            }

            public RectTransform Root { get; }
            public CanvasGroup Group { get; }
        }
    }
}
