using System;
using System.Collections;
using UnityEngine;

namespace Match3Foodie
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class Match3PieceView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField, Min(0.01f)] private float destroyPopDuration = 0.16f;
        [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve fallCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve destroyScaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        private Match3Board board;
        private Coroutine motionRoutine;
        private Vector3 baseScale;
        private Quaternion baseRotation;
        private bool baseFlipX;
        private Vector3 baseLocalScale;
        private int baseSortingOrder;

        public Match3ElementDefinition Definition { get; private set; }
        public Match3GridPosition GridPosition { get; private set; }
        public bool IsSelected { get; private set; }
        public float DestroyPopDuration => destroyPopDuration;
        public Vector2 VisualWorldSize
        {
            get
            {
                if (spriteRenderer != null)
                {
                    var size = spriteRenderer.bounds.size;
                    return new Vector2(size.x, size.y);
                }

                var scale = transform.lossyScale;
                return new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }
        }

        public void Initialize(Match3Board owner, Match3ElementDefinition definition, Match3GridPosition gridPosition)
        {
            board = owner;
            Definition = definition;
            GridPosition = gridPosition;
            baseScale = transform.localScale;
            baseLocalScale = transform.localScale;
            baseRotation = transform.rotation;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                baseFlipX = spriteRenderer.flipX;
                baseSortingOrder = spriteRenderer.sortingOrder;
                spriteRenderer.sprite = definition.Sprite;
                spriteRenderer.color = definition.Tint;
            }

            transform.rotation = baseRotation;

            gameObject.name = $"Piece {definition.ElementId} {gridPosition}";
        }

        public void SetGridPosition(Match3GridPosition gridPosition)
        {
            GridPosition = gridPosition;
            if (Definition != null)
            {
                gameObject.name = $"Piece {Definition.ElementId} {gridPosition}";
            }
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            transform.localScale = selected ? baseScale * 1.12f : baseScale;
        }

        public Coroutine MoveTo(Vector3 worldPosition, float duration)
        {
            if (motionRoutine != null)
            {
                StopCoroutine(motionRoutine);
            }

            motionRoutine = StartCoroutine(MoveRoutine(worldPosition, duration));
            return motionRoutine;
        }

        public Coroutine FallTo(Vector3 worldPosition, float duration, float impactBounceDistance, float impactBounceDuration)
        {
            if (motionRoutine != null)
            {
                StopCoroutine(motionRoutine);
            }

            motionRoutine = StartCoroutine(FallRoutine(worldPosition, duration, impactBounceDistance, impactBounceDuration));
            return motionRoutine;
        }

        public Coroutine FlyFishTo(Vector3 worldPosition, float speed, float waveAmplitude, float waveFrequency, bool faceDirection, float spriteForwardAngle, float maxTiltAngle, int sortingOrderBoost)
        {
            if (motionRoutine != null)
            {
                StopCoroutine(motionRoutine);
            }

            motionRoutine = StartCoroutine(FishFlightRoutine(worldPosition, speed, waveAmplitude, waveFrequency, faceDirection, spriteForwardAngle, maxTiltAngle, sortingOrderBoost));
            return motionRoutine;
        }

        public Coroutine SpawnDropTo(Vector3 worldPosition, float duration, float delay, float spawnScale, float popScale, float impactBounceDistance, float impactBounceDuration)
        {
            if (motionRoutine != null)
            {
                StopCoroutine(motionRoutine);
            }

            motionRoutine = StartCoroutine(SpawnDropRoutine(worldPosition, duration, delay, spawnScale, popScale, impactBounceDistance, impactBounceDuration));
            return motionRoutine;
        }

        public Coroutine CollectTo(
            Vector3 worldPosition,
            float speed,
            float exitDistance,
            float exitDuration,
            float arrivePopScale,
            float arrivePopDuration,
            Action onArrived)
        {
            if (motionRoutine != null)
            {
                StopCoroutine(motionRoutine);
            }

            motionRoutine = StartCoroutine(CollectRoutine(
                worldPosition,
                speed,
                exitDistance,
                exitDuration,
                arrivePopScale,
                arrivePopDuration,
                onArrived));
            return motionRoutine;
        }

        public IEnumerator PlayDestroyRoutine()
        {
            if (this == null)
            {
                yield break;
            }

            PlayDestroyEffect();

            var elapsed = 0f;
            var safeDuration = Mathf.Max(0.01f, destroyPopDuration);
            var startScale = transform.localScale;
            while (elapsed < safeDuration)
            {
                if (this == null)
                {
                    yield break;
                }

                elapsed += AnimationDeltaTime();
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var scale = destroyScaleCurve.Evaluate(t);
                transform.localScale = startScale * scale;
                yield return null;
            }

            if (this != null)
            {
                Destroy(gameObject);
            }
        }

        public void PlayDestroyEffect()
        {
            if (Definition == null || Definition.DestructionEffectPrefab == null)
            {
                return;
            }

            Instantiate(Definition.DestructionEffectPrefab, transform.position, Quaternion.identity, board.transform);
        }

        private IEnumerator MoveRoutine(Vector3 target, float duration)
        {
            yield return MoveRoutine(target, duration, moveCurve);
        }

        private IEnumerator MoveRoutine(Vector3 target, float duration, AnimationCurve curve)
        {
            transform.rotation = baseRotation;
            ResetSpriteFacing();
            ResetSorting();
            var start = transform.position;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += AnimationDeltaTime();
                var t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.LerpUnclamped(start, target, curve.Evaluate(t));
                yield return null;
            }

            transform.position = target;
            motionRoutine = null;
        }

        private IEnumerator FallRoutine(Vector3 target, float duration, float impactBounceDistance, float impactBounceDuration)
        {
            transform.rotation = baseRotation;
            ResetSpriteFacing();
            ResetSorting();

            var start = transform.position;
            var safeDuration = Mathf.Max(0.01f, duration);
            var elapsed = 0f;
            var fallDirection = (target - start).normalized;

            while (elapsed < safeDuration)
            {
                elapsed += AnimationDeltaTime();
                var t = Mathf.Clamp01(elapsed / safeDuration);
                transform.position = Vector3.LerpUnclamped(start, target, fallCurve.Evaluate(t));
                yield return null;
            }

            transform.position = target;
            transform.localScale = baseScale;
            yield return ImpactBounceRoutine(target, fallDirection, impactBounceDistance, impactBounceDuration);
            motionRoutine = null;
        }

        private IEnumerator CollectRoutine(
            Vector3 target,
            float speed,
            float exitDistance,
            float exitDuration,
            float arrivePopScale,
            float arrivePopDuration,
            Action onArrived)
        {
            transform.rotation = baseRotation;
            ResetSpriteFacing();

            var start = transform.position;
            transform.localScale = baseScale;

            var exitDirection = (start - board.transform.position).normalized;
            if (exitDirection.sqrMagnitude <= 0.0001f)
            {
                exitDirection = Vector3.up;
            }

            var flightStart = start + exitDirection * exitDistance;
            var elapsed = 0f;
            if (exitDuration > 0f && exitDistance > 0f)
            {
                while (elapsed < exitDuration)
                {
                    elapsed += AnimationDeltaTime();
                    var t = Mathf.Clamp01(elapsed / exitDuration);
                    transform.position = Vector3.LerpUnclamped(start, flightStart, moveCurve.Evaluate(t));
                    transform.localScale = Vector3.LerpUnclamped(baseScale, baseScale * 1.08f, Mathf.Sin(t * Mathf.PI));
                    yield return null;
                }
            }
            else
            {
                flightStart = start;
            }

            transform.position = flightStart;
            transform.localScale = baseScale;

            var distance = Vector3.Distance(flightStart, target);
            var duration = distance / Mathf.Max(0.01f, speed);
            elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += AnimationDeltaTime();
                var t = Mathf.Clamp01(elapsed / duration);
                var curvedT = moveCurve.Evaluate(t);
                transform.position = Vector3.LerpUnclamped(flightStart, target, curvedT);
                transform.localScale = baseScale;
                yield return null;
            }

            transform.position = target;
            onArrived?.Invoke();
            PlayDestroyEffect();

            elapsed = 0f;
            var popDuration = Mathf.Max(0.01f, arrivePopDuration);
            var popScale = baseScale * arrivePopScale;
            while (elapsed < popDuration)
            {
                elapsed += AnimationDeltaTime();
                var t = Mathf.Clamp01(elapsed / popDuration);
                transform.localScale = t < 0.5f
                    ? Vector3.LerpUnclamped(baseScale, popScale, t * 2f)
                    : Vector3.LerpUnclamped(popScale, Vector3.zero, (t - 0.5f) * 2f);
                yield return null;
            }

            motionRoutine = null;
        }

        private IEnumerator SpawnDropRoutine(Vector3 target, float duration, float delay, float spawnScale, float popScale, float impactBounceDistance, float impactBounceDuration)
        {
            transform.rotation = baseRotation;
            ResetSpriteFacing();

            var start = transform.position;
            var elapsed = 0f;
            var safeDuration = Mathf.Max(0.01f, duration);
            var startScale = baseScale * spawnScale;
            var overshootScale = baseScale * popScale;
            var fallDirection = (target - start).normalized;
            transform.localScale = startScale;

            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            while (elapsed < safeDuration)
            {
                elapsed += AnimationDeltaTime();
                var t = Mathf.Clamp01(elapsed / safeDuration);
                transform.position = Vector3.LerpUnclamped(start, target, fallCurve.Evaluate(t));

                var animatedScale = t < 0.65f
                    ? Vector3.LerpUnclamped(startScale, overshootScale, Mathf.InverseLerp(0f, 0.65f, t))
                    : Vector3.LerpUnclamped(overshootScale, baseScale, Mathf.InverseLerp(0.65f, 1f, t));
                transform.localScale = animatedScale;
                yield return null;
            }

            transform.position = target;
            transform.localScale = baseScale;
            yield return ImpactBounceRoutine(target, fallDirection, impactBounceDistance, impactBounceDuration);
            motionRoutine = null;
        }

        private IEnumerator ImpactBounceRoutine(Vector3 target, Vector3 fallDirection, float distance, float duration)
        {
            if (distance <= 0f || duration <= 0f || fallDirection.sqrMagnitude <= 0.0001f)
            {
                transform.position = target;
                yield break;
            }

            var offset = -fallDirection.normalized * distance;
            var safeDuration = Mathf.Max(0.01f, duration);
            var elapsed = 0f;

            while (elapsed < safeDuration)
            {
                elapsed += AnimationDeltaTime();
                var t = Mathf.Clamp01(elapsed / safeDuration);
                transform.position = target + offset * Mathf.Sin(t * Mathf.PI);
                yield return null;
            }

            transform.position = target;
        }

        private IEnumerator FishFlightRoutine(Vector3 target, float speed, float waveAmplitude, float waveFrequency, bool faceDirection, float spriteForwardAngle, float maxTiltAngle, int sortingOrderBoost)
        {
            transform.rotation = baseRotation;
            ResetSpriteFacing();
            SetSortingBoost(sortingOrderBoost);

            var start = transform.position;
            var delta = target - start;
            var distance = delta.magnitude;
            if (distance <= 0.001f)
            {
                transform.position = target;
                ResetSorting();
                motionRoutine = null;
                yield break;
            }

            var direction = delta / distance;
            var perpendicular = new Vector3(-direction.y, direction.x, 0f);
            var duration = distance / Mathf.Max(0.01f, speed);
            var elapsed = 0f;

            ApplyFacing(direction, faceDirection, spriteForwardAngle, maxTiltAngle);

            while (elapsed < duration)
            {
                elapsed += AnimationDeltaTime();
                var t = Mathf.Clamp01(elapsed / duration);
                var wave = Mathf.Sin(t * Mathf.PI * 2f * waveFrequency) * waveAmplitude * Mathf.Sin(t * Mathf.PI);
                transform.position = Vector3.LerpUnclamped(start, target, t) + perpendicular * wave;
                yield return null;
            }

            transform.position = target;
            ResetSorting();
            motionRoutine = null;
        }

        private void ApplyFacing(Vector3 direction, bool faceDirection, float spriteForwardAngle, float maxTiltAngle)
        {
            if (!faceDirection || direction.sqrMagnitude <= 0.0001f)
            {
                transform.rotation = baseRotation;
                ResetSpriteFacing();
                return;
            }

            var worldTargetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var baseWorldAngle = baseRotation.eulerAngles.z;
            var localTargetAngle = Mathf.DeltaAngle(baseWorldAngle, worldTargetAngle);

            var unflippedRotation = Mathf.DeltaAngle(spriteForwardAngle, localTargetAngle);
            var flippedForwardAngle = 180f - spriteForwardAngle;
            var flippedRotation = Mathf.DeltaAngle(flippedForwardAngle, localTargetAngle);
            var useFlip = Mathf.Abs(flippedRotation) < Mathf.Abs(unflippedRotation);
            var rotation = useFlip ? flippedRotation : unflippedRotation;

            transform.localScale = baseLocalScale;
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = useFlip ? !baseFlipX : baseFlipX;
            }

            transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, rotation);
        }

        private void ResetSpriteFacing()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = baseFlipX;
            }

            transform.localScale = baseLocalScale;
        }

        private void SetSortingBoost(int sortingOrderBoost)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = baseSortingOrder + sortingOrderBoost;
            }
        }

        private void ResetSorting()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = baseSortingOrder;
            }
        }

        private static float AnimationDeltaTime()
        {
            var delta = Time.smoothDeltaTime > 0f ? Time.smoothDeltaTime : Time.deltaTime;
            return Mathf.Clamp(delta, 0f, 1f / 30f);
        }

        private void Reset()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }
}
