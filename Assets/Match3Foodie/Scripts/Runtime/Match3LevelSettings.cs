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

        [Header("Math Bonus")]
        [Tooltip("Dorblue element. Optional: leave empty to use the first Board Settings element marked as Special Effect Type = MathBonus.")]
        [SerializeField] private Match3ElementDefinition mathBonusElement;
        [Tooltip("How many Dorblue collections fill the math counter before the popup starts.")]
        [SerializeField, Min(1)] private int mathBonusRequiredCollections = 5;
        [Tooltip("How much the Dorblue counter requirement grows after each completed math popup.")]
        [SerializeField, Min(0)] private int mathBonusRequiredIncreasePerChallenge = 0;
        [Tooltip("Delay after the board fully finishes resolving matches before the math popup opens.")]
        [SerializeField, Min(0f)] private float mathChallengeStartDelay = 0.5f;

        public float TimeLimitSeconds => timeLimitSeconds;
        public bool FailWhenTimerEnds => failWhenTimerEnds;
        public IReadOnlyList<Goal> Goals => goals;
        public Match3ElementDefinition MathBonusElement => mathBonusElement;
        public int MathBonusRequiredCollections => mathBonusRequiredCollections;
        public int MathBonusRequiredIncreasePerChallenge => mathBonusRequiredIncreasePerChallenge;
        public float MathChallengeStartDelay => mathChallengeStartDelay;
    }
}
