using System;
using System.Collections.Generic;
using UnityEngine;

namespace Match3Foodie
{
    public sealed class Match3CollectionTargetProvider : MonoBehaviour
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private Match3ElementDefinition element;
            [SerializeField] private RectTransform target;

            public Match3ElementDefinition Element => element;
            public RectTransform Target => target;
        }

        [SerializeField] private List<Entry> targets = new();

        private readonly Dictionary<Match3ElementDefinition, Match3GoalItemView> runtimeViews = new();
        private readonly Dictionary<Match3ElementDefinition, RectTransform> runtimeTargets = new();

        public Canvas Canvas { get; private set; }

        private void Awake()
        {
            Canvas = GetComponentInParent<Canvas>();
        }

        public void SetTarget(Match3ElementDefinition element, Match3GoalItemView view)
        {
            if (element == null)
            {
                return;
            }

            if (view == null)
            {
                runtimeViews.Remove(element);
                runtimeTargets.Remove(element);
                return;
            }

            runtimeViews[element] = view;
            runtimeTargets[element] = view.CollectionTarget;
        }

        public void SetTarget(Match3ElementDefinition element, RectTransform target)
        {
            if (element == null)
            {
                return;
            }

            if (target == null)
            {
                runtimeTargets.Remove(element);
                return;
            }

            runtimeTargets[element] = target;
        }

        public bool TryGetScreenPosition(Match3ElementDefinition element, out Vector2 screenPosition)
        {
            screenPosition = default;
            var target = GetTarget(element);
            if (target == null)
            {
                return false;
            }

            screenPosition = RectTransformUtility.WorldToScreenPoint(null, target.position);
            return true;
        }

        public bool TryGetTarget(Match3ElementDefinition element, out Canvas canvas, out RectTransform target, out Match3GoalItemView view)
        {
            canvas = Canvas != null ? Canvas : GetComponentInParent<Canvas>();
            target = GetTarget(element);
            runtimeViews.TryGetValue(element, out view);
            return canvas != null && target != null;
        }

        public void PlayBump(Match3ElementDefinition element)
        {
            if (runtimeViews.TryGetValue(element, out var view) && view != null)
            {
                view.PlayBump();
            }
        }

        private RectTransform GetTarget(Match3ElementDefinition element)
        {
            if (element == null)
            {
                return null;
            }

            if (runtimeTargets.TryGetValue(element, out var runtimeTarget) && runtimeTarget != null)
            {
                return runtimeTarget;
            }

            foreach (var entry in targets)
            {
                if (entry.Element == element)
                {
                    return entry.Target;
                }
            }

            return null;
        }
    }
}
