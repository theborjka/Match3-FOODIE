using System;
using System.Collections.Generic;
using UnityEngine;

namespace Match3Foodie
{
    [CreateAssetMenu(menuName = "Match3 Foodie/Level Settings", fileName = "Level Settings")]
    public sealed class Match3LevelSettings : ScriptableObject
    {
        [Serializable]
        public sealed class Goal
        {
            [SerializeField] private Match3ElementDefinition element;
            [SerializeField, Min(1)] private int requiredAmount = 10;

            public Match3ElementDefinition Element => element;
            public int RequiredAmount => requiredAmount;
        }

        [Header("Timer")]
        [SerializeField, Min(1f)] private float timeLimitSeconds = 120f;
        [SerializeField] private bool failWhenTimerEnds = true;

        [Header("Shopping List")]
        [SerializeField] private List<Goal> goals = new();

        public float TimeLimitSeconds => timeLimitSeconds;
        public bool FailWhenTimerEnds => failWhenTimerEnds;
        public IReadOnlyList<Goal> Goals => goals;
    }
}
