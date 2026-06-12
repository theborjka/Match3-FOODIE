using System;

namespace Match3Foodie
{
    [Serializable]
    public sealed class Match3GoalProgress
    {
        public Match3GoalProgress(Match3ElementDefinition element, int requiredAmount)
        {
            Element = element;
            RequiredAmount = requiredAmount;
        }

        public Match3ElementDefinition Element { get; }
        public int RequiredAmount { get; }
        public int CollectedAmount { get; private set; }
        public int RemainingAmount => Math.Max(0, RequiredAmount - CollectedAmount);
        public bool IsComplete => CollectedAmount >= RequiredAmount;

        public void AddCollected(int amount)
        {
            CollectedAmount = Math.Min(RequiredAmount, CollectedAmount + Math.Max(0, amount));
        }
    }
}
