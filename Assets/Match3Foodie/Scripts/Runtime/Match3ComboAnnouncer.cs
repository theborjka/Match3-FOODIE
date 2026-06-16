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

        [Serializable]
        private sealed class AnnouncementTier
        {
            [SerializeField, Min(1)] private int minPieces = 5;
            [SerializeField] private string title = "Great!";
            [SerializeField] private Color titleColor = Color.white;
            [SerializeField, Min(1f)] private float popScale = 1.18f;

            public AnnouncementTier() { }

            public AnnouncementTier(int minPieces, string title, Color titleColor, float popScale)
            {
                this.minPieces = Mathf.Max(1, minPieces);
                this.title = title;
                this.titleColor = titleColor;
                this.popScale = Mathf.Max(1f, popScale);
            }

            public int MinPieces => minPieces;
            public string Title => title;
            public Color TitleColor => titleColor;
            public float PopScale => popScale;
        }

        [Header("Source")]
        [SerializeField] private Match3Board board;
        [SerializeField] private Camera worldCamera;

        [Header("UI Templates")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform animatedRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text countText;

        [Header("Floating Placement")]
        [SerializeField] private RectTransform popupParent;
        [SerializeField] private Vector2 popupOffset = new(0f, 82f);
        [SerializeField, Min(0f)] private float randomJitter = 28f;
        [SerializeField, Min(0f)] private float screenPadding = 80f;
        [SerializeField] private bool hideTemplatesOnAwake = true;

        [Header("Text Layout")]
        [SerializeField] private bool manageTextLayout = true;
        [SerializeField] private Vector2 titleTextOffset = new(0f, 30f);
        [SerializeField] private Vector2 countTextOffset = new(0f, -28f);
        [SerializeField, Min(1f)] private float managedTextWidth = 560f;
        [SerializeField, Min(1f)] private float managedTitleHeight = 90f;
        [SerializeField, Min(1f)] private float managedCountHeight = 88f;
        [SerializeField] private string currentCountFormat = "+{0} Matches";
        [SerializeField] private string totalCountFormat = "{0} Total";

        [Header("Rules")]
        [SerializeField, Min(1)] private int minimumPiecesToShow = 5;
        [SerializeField] private bool showWhenSequenceTotalReachesMinimum = true;
        [SerializeField] private List<AnnouncementTier> tiers = new()
        {
            new AnnouncementTier(5, "Great!", new Color(1f, 0.72f, 0.18f, 1f), 1.14f),
            new AnnouncementTier(8, "Wow!", new Color(0.28f, 0.95f, 1f, 1f), 1.2f),
            new AnnouncementTier(11, "Insane!", new Color(1f, 0.32f, 0.95f, 1f), 1.26f),
            new AnnouncementTier(15, "Legendary!", new Color(1f, 0.25f, 0.16f, 1f), 1.32f),
            new AnnouncementTier(22, "Unreal!", new Color(1f, 0.95f, 0.2f, 1f), 1.38f),
        };

        [Header("Motion")]
        [SerializeField, Min(0f)] private float showDuration = 0.18f;
        [SerializeField, Min(0f)] private float holdDuration = 0.5f;
        [SerializeField, Min(0f)] private float hideDuration = 0.18f;
        [SerializeField, Min(0f)] private float punchRotation = 7f;
        [SerializeField] private Vector2 enterOffset = new(0f, -36f);
        [SerializeField] private Vector2 exitOffset = new(0f, 34f);

        [Header("Juice")]
        [SerializeField] private GameObject vfxPrefab;
        [SerializeField] private RectTransform vfxParent;
        [SerializeField, Min(0f)] private float vfxLifetime = 1.2f;
        [SerializeField] private RectTransform shakeRoot;
        [SerializeField, Min(0f)] private float shakeDuration = 0.16f;
        [SerializeField, Min(0f)] private float shakeStrength = 7f;

        [Header("Events")]
        [SerializeField] private IntEvent announced = new();

        private Coroutine shakeRoutine;
        private Vector2 shakeRootBasePosition;
        private int sequenceTotal;

        public IntEvent Announced => announced;

        private void Awake()
        {
            ResolveReferences();
            if (hideTemplatesOnAwake)
            {
                HideTemplates();
            }
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (board != null)
            {
                board.PiecesMatched.AddListener(ShowForMatchedPieces);
                board.BoardSettled.AddListener(ResetSequenceTotal);
            }
        }

        private void OnDisable()
        {
            if (board != null)
            {
                board.PiecesMatched.RemoveListener(ShowForMatchedPieces);
                board.BoardSettled.RemoveListener(ResetSequenceTotal);
            }
        }

        private void ShowForMatchedPieces(List<Match3PieceView> matchedPieces)
        {
            var currentCount = matchedPieces != null ? matchedPieces.Count : 0;
            if (currentCount <= 0)
            {
                return;
            }

            sequenceTotal += currentCount;
            var shouldShow = currentCount >= minimumPiecesToShow
                || (showWhenSequenceTotalReachesMinimum && sequenceTotal >= minimumPiecesToShow);
            if (!shouldShow)
            {
                return;
            }

            var scoreForTier = Mathf.Max(currentCount, sequenceTotal);
            var tier = ResolveTier(scoreForTier);
            if (tier == null)
            {
                return;
            }

            var position = GetPopupPosition(matchedPieces);
            StartCoroutine(AnnounceRoutine(currentCount, sequenceTotal, tier, position));
        }

        public void ShowForClearedPieces(int clearedPieces)
        {
            if (clearedPieces <= 0)
            {
                return;
            }

            sequenceTotal += clearedPieces;
            var shouldShow = clearedPieces >= minimumPiecesToShow
                || (showWhenSequenceTotalReachesMinimum && sequenceTotal >= minimumPiecesToShow);
            if (!shouldShow)
            {
                return;
            }

            var tier = ResolveTier(Mathf.Max(clearedPieces, sequenceTotal));
            if (tier != null)
            {
                StartCoroutine(AnnounceRoutine(clearedPieces, sequenceTotal, tier, GetFallbackPopupPosition()));
            }
        }

        private IEnumerator AnnounceRoutine(int currentCount, int totalCount, AnnouncementTier tier, Vector2 shownPosition)
        {
            var popup = CreatePopup(currentCount, totalCount, tier, shownPosition);
            if (popup == null)
            {
                yield break;
            }

            PlayVfx(shownPosition);
            PlayShake();
            announced.Invoke(currentCount);

            yield return PopInRoutine(popup.Root, popup.Group, shownPosition, tier.PopScale);

            if (holdDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(holdDuration);
            }

            yield return HideRoutine(popup.Root, popup.Group, shownPosition);

            if (popup.Root != null)
            {
                Destroy(popup.Root.gameObject);
            }
        }

        private PopupInstance CreatePopup(int currentCount, int totalCount, AnnouncementTier tier, Vector2 shownPosition)
        {
            ResolveReferences();
            if (popupParent == null)
            {
                return null;
            }

            SetAlpha(canvasGroup, 1f);

            var popupObject = new GameObject("Combo Announcement", typeof(RectTransform), typeof(CanvasGroup));
            var popupRoot = popupObject.GetComponent<RectTransform>();
            popupRoot.SetParent(popupParent, false);
            popupRoot.anchorMin = new Vector2(0.5f, 0.5f);
            popupRoot.anchorMax = new Vector2(0.5f, 0.5f);
            popupRoot.pivot = new Vector2(0.5f, 0.5f);
            popupRoot.anchoredPosition = shownPosition;
            popupRoot.localScale = Vector3.zero;

            var group = popupObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var title = CreateText(titleText, popupRoot, "Title", tier.Title);
            if (title != null)
            {
                title.color = tier.TitleColor;
                ConfigureText(title);
                ApplyTextRectLayout(title, titleTextOffset, managedTextWidth, managedTitleHeight);
            }

            var count = CreateText(countText, popupRoot, "Count", BuildCountText(currentCount, totalCount));
            if (count != null)
            {
                ConfigureText(count);
                ApplyTextRectLayout(count, countTextOffset, managedTextWidth, managedCountHeight);
            }

            return new PopupInstance(popupRoot, group);
        }

        private TMP_Text CreateText(TMP_Text template, RectTransform parent, string name, string value)
        {
            if (template == null || parent == null)
            {
                return null;
            }

            var created = Instantiate(template, parent);
            created.name = name;
            created.gameObject.SetActive(true);
            created.text = value;
            return created;
        }

        private string BuildCountText(int currentCount, int totalCount)
        {
            var current = string.Format(currentCountFormat, currentCount);
            if (totalCount <= currentCount)
            {
                return current;
            }

            return $"{current}\n{string.Format(totalCountFormat, totalCount)}";
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

        private Vector2 GetPopupPosition(List<Match3PieceView> matchedPieces)
        {
            if (popupParent == null || matchedPieces == null || matchedPieces.Count == 0)
            {
                return GetFallbackPopupPosition();
            }

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

            if (count <= 0)
            {
                return GetFallbackPopupPosition();
            }

            worldCenter /= count;
            return WorldToPopupPosition(worldCenter);
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

        private void PlayVfx(Vector2 anchoredPosition)
        {
            if (vfxPrefab == null || popupParent == null)
            {
                return;
            }

            var parent = vfxParent != null ? vfxParent : popupParent;
            var instance = Instantiate(vfxPrefab, parent);
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

            foreach (var particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particleSystem.Play(true);
            }

            if (vfxLifetime > 0f)
            {
                Destroy(instance, vfxLifetime);
            }
        }

        private void PlayShake()
        {
            if (shakeRoot == null || shakeStrength <= 0f || shakeDuration <= 0f)
            {
                return;
            }

            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                shakeRoot.anchoredPosition = shakeRootBasePosition;
            }

            shakeRootBasePosition = shakeRoot.anchoredPosition;
            shakeRoutine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, shakeDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var strength = shakeStrength * (1f - t);
                shakeRoot.anchoredPosition = shakeRootBasePosition + UnityEngine.Random.insideUnitCircle * strength;
                yield return null;
            }

            shakeRoot.anchoredPosition = shakeRootBasePosition;
            shakeRoutine = null;
        }

        private AnnouncementTier ResolveTier(int clearedPieces)
        {
            AnnouncementTier best = null;
            foreach (var tier in tiers)
            {
                if (tier != null && clearedPieces >= tier.MinPieces && (best == null || tier.MinPieces > best.MinPieces))
                {
                    best = tier;
                }
            }

            return best;
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

            if (root == null)
            {
                root = gameObject;
            }

            if (animatedRoot == null)
            {
                animatedRoot = root.transform as RectTransform;
            }

            if (canvasGroup == null && root != null)
            {
                canvasGroup = root.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = root.AddComponent<CanvasGroup>();
                }
            }

            if (popupParent == null)
            {
                popupParent = animatedRoot != null ? animatedRoot.parent as RectTransform : transform.parent as RectTransform;
            }
        }

        private void HideTemplates()
        {
            if (animatedRoot != null)
            {
                animatedRoot.localScale = Vector3.zero;
                animatedRoot.localEulerAngles = Vector3.zero;
            }

            if (titleText != null)
            {
                titleText.gameObject.SetActive(false);
            }

            if (countText != null)
            {
                countText.gameObject.SetActive(false);
            }
        }

        private void ResetSequenceTotal()
        {
            sequenceTotal = 0;
        }

        private void ConfigureText(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }

        private void ApplyTextRectLayout(TMP_Text text, Vector2 offset, float width, float height)
        {
            if (!manageTextLayout || text == null || text.transform is not RectTransform rectTransform)
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

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null)
            {
                group.alpha = Mathf.Clamp01(alpha);
            }
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
