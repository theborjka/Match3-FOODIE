using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Match3Foodie
{
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public sealed class Match3CollectionFlyer : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Image image;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            image = GetComponent<Image>();
            image.raycastTarget = false;
        }

        public void Initialize(Sprite sprite, Color color, Vector2 startPosition, Vector2 size)
        {
            rectTransform = (RectTransform)transform;
            image = GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            rectTransform.anchoredPosition = startPosition;
            rectTransform.sizeDelta = size;
            rectTransform.localScale = Vector3.one;
        }

        public IEnumerator FlyRoutine(
            Vector2 targetPosition,
            float speed,
            float exitDistance,
            float exitDuration,
            float arrivePopScale,
            float arrivePopDuration,
            Action onArrived)
        {
            var start = rectTransform.anchoredPosition;
            var exitDirection = start.sqrMagnitude > 0.001f ? start.normalized : Vector2.up;
            var flightStart = start + exitDirection * exitDistance;

            var elapsed = 0f;
            if (exitDuration > 0f && exitDistance > 0f)
            {
                while (elapsed < exitDuration)
                {
                    elapsed += AnimationDeltaTime();
                    var t = Mathf.Clamp01(elapsed / exitDuration);
                    rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, flightStart, Smooth(t));
                    rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(1f, 1.08f, Mathf.Sin(t * Mathf.PI));
                    yield return null;
                }
            }
            else
            {
                flightStart = start;
            }

            rectTransform.anchoredPosition = flightStart;
            rectTransform.localScale = Vector3.one;

            var distance = Vector2.Distance(flightStart, targetPosition);
            var duration = distance / Mathf.Max(0.01f, speed);
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += AnimationDeltaTime();
                var t = Mathf.Clamp01(elapsed / duration);
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(flightStart, targetPosition, Smooth(t));
                yield return null;
            }

            rectTransform.anchoredPosition = targetPosition;
            onArrived?.Invoke();

            elapsed = 0f;
            var popDuration = Mathf.Max(0.01f, arrivePopDuration);
            while (elapsed < popDuration)
            {
                elapsed += AnimationDeltaTime();
                var t = Mathf.Clamp01(elapsed / popDuration);
                rectTransform.localScale = t < 0.5f
                    ? Vector3.one * Mathf.LerpUnclamped(1f, arrivePopScale, t * 2f)
                    : Vector3.one * Mathf.LerpUnclamped(arrivePopScale, 0f, (t - 0.5f) * 2f);
                yield return null;
            }

            Destroy(gameObject);
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float AnimationDeltaTime()
        {
            var delta = Time.smoothDeltaTime > 0f ? Time.smoothDeltaTime : Time.deltaTime;
            return Mathf.Clamp(delta, 0f, 1f / 30f);
        }
    }
}
