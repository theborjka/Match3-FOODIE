using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Match3Foodie
{
    public sealed class Match3GoalItemView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private RectTransform collectionTarget;
        [SerializeField] private GameObject completedMarker;
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color activeColorText = Color.white;

        [SerializeField] private Color completedColor = new(1f, 1f, 1f, 0.45f);
        [SerializeField, Min(1f)] private float bumpScale = 1.14f;
        [SerializeField, Min(0.01f)] private float bumpDuration = 0.16f;

        public Match3ElementDefinition Element { get; private set; }
        public RectTransform CollectionTarget => collectionTarget != null ? collectionTarget : transform as RectTransform;

        private Coroutine bumpRoutine;
        private Vector3 baseScale = Vector3.one;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        public void Configure(Image icon, TMP_Text amount, GameObject marker = null)
        {
            iconImage = icon;
            amountText = amount;
            completedMarker = marker;
        }

        public void Bind(Match3GoalProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            Element = progress.Element;

            if (iconImage != null)
            {
                iconImage.sprite = progress.Element != null ? progress.Element.Sprite : null;
                iconImage.color = progress.IsComplete ? completedColor : activeColor;
                iconImage.enabled = iconImage.sprite != null;
            }

            if (amountText != null)
            {
                amountText.text = progress.RemainingAmount.ToString();
                amountText.color = progress.IsComplete ? completedColor : activeColorText;
            }

            if (completedMarker != null)
            {
                completedMarker.SetActive(progress.IsComplete);
            }
        }

        public void PlayBump()
        {
            if (bumpRoutine != null)
            {
                StopCoroutine(bumpRoutine);
            }

            bumpRoutine = StartCoroutine(BumpRoutine());
        }

        private IEnumerator BumpRoutine()
        {
            var elapsed = 0f;
            while (elapsed < bumpDuration)
            {
                elapsed += AnimationDeltaTime();
                var t = Mathf.Clamp01(elapsed / bumpDuration);
                var scale = t < 0.5f
                    ? Mathf.LerpUnclamped(1f, bumpScale, t * 2f)
                    : Mathf.LerpUnclamped(bumpScale, 1f, (t - 0.5f) * 2f);
                transform.localScale = baseScale * scale;
                yield return null;
            }

            transform.localScale = baseScale;
            bumpRoutine = null;
        }

        private static float AnimationDeltaTime()
        {
            var delta = Time.smoothDeltaTime > 0f ? Time.smoothDeltaTime : Time.deltaTime;
            return Mathf.Clamp(delta, 0f, 1f / 30f);
        }
    }
}
