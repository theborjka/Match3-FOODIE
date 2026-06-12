using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Match3Foodie
{
    public sealed class Match3LevelController : MonoBehaviour
    {
        [Serializable] public sealed class FloatEvent : UnityEvent<float> { }
        [Serializable] public sealed class GoalProgressEvent : UnityEvent<Match3GoalProgress> { }
        [Serializable] public sealed class GoalsProgressEvent : UnityEvent<List<Match3GoalProgress>> { }

        [Header("Config")]
        [SerializeField] private Match3LevelSettings levelSettings;
        [SerializeField] private Match3Board board;
        [SerializeField] private bool startTimerOnEnable = true;
        [SerializeField] private bool disableBoardWhenLevelEnds = true;

        [Header("Events")]
        [SerializeField] private FloatEvent timerChanged = new();
        [SerializeField] private GoalProgressEvent goalChanged = new();
        [SerializeField] private GoalsProgressEvent goalsChanged = new();
        [SerializeField] private UnityEvent levelCompleted = new();
        [SerializeField] private UnityEvent levelFailed = new();

        private readonly List<Match3GoalProgress> goalProgress = new();
        private float remainingTime;
        private bool timerRunning;
        private bool levelEnded;

        public Match3LevelSettings LevelSettings => levelSettings;
        public float RemainingTime => remainingTime;
        public bool IsTimerRunning => timerRunning;
        public bool IsLevelEnded => levelEnded;
        public IReadOnlyList<Match3GoalProgress> Goals => goalProgress;
        public FloatEvent TimerChanged => timerChanged;
        public GoalProgressEvent GoalChanged => goalChanged;
        public GoalsProgressEvent GoalsChanged => goalsChanged;
        public UnityEvent LevelCompleted => levelCompleted;
        public UnityEvent LevelFailed => levelFailed;

        private void Awake()
        {
            if (board == null)
            {
                board = FindAnyObjectByType<Match3Board>();
            }

            ResetLevelState();
        }

        private void OnEnable()
        {
            if (board != null)
            {
                board.PieceCollected.AddListener(HandlePieceCollected);
            }

            if (startTimerOnEnable)
            {
                StartTimer();
            }
        }

        private void OnDisable()
        {
            if (board != null)
            {
                board.PieceCollected.RemoveListener(HandlePieceCollected);
            }
        }

        private void Update()
        {
            if (!timerRunning || levelEnded)
            {
                return;
            }

            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            timerChanged.Invoke(remainingTime);

            if (remainingTime <= 0f && levelSettings != null && levelSettings.FailWhenTimerEnds)
            {
                FailLevel();
            }
        }

        [ContextMenu("Reset Level State")]
        public void ResetLevelState()
        {
            goalProgress.Clear();
            remainingTime = levelSettings != null ? levelSettings.TimeLimitSeconds : 0f;
            timerRunning = false;
            levelEnded = false;

            if (board != null)
            {
                board.SetInputEnabled(true);
            }

            if (levelSettings != null)
            {
                foreach (var goal in levelSettings.Goals)
                {
                    if (goal.Element != null)
                    {
                        goalProgress.Add(new Match3GoalProgress(goal.Element, goal.RequiredAmount));
                    }
                }
            }

            timerChanged.Invoke(remainingTime);
            goalsChanged.Invoke(new List<Match3GoalProgress>(goalProgress));
        }

        public void StartTimer()
        {
            if (levelSettings == null || levelEnded)
            {
                return;
            }

            timerRunning = true;
            timerChanged.Invoke(remainingTime);
        }

        public void PauseTimer()
        {
            timerRunning = false;
        }

        public void ResumeTimer()
        {
            if (levelSettings == null || levelEnded || remainingTime <= 0f)
            {
                return;
            }

            timerRunning = true;
            timerChanged.Invoke(remainingTime);
        }

        public void AddTime(float seconds)
        {
            if (levelSettings == null || levelEnded || seconds <= 0f)
            {
                return;
            }

            remainingTime += seconds;
            timerChanged.Invoke(remainingTime);
        }

        public string GetFormattedRemainingTime()
        {
            var seconds = Mathf.CeilToInt(remainingTime);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        private void HandlePieceCollected(Match3PieceView collectedPiece)
        {
            if (levelEnded || collectedPiece == null)
            {
                return;
            }

            for (var i = 0; i < goalProgress.Count; i++)
            {
                var progress = goalProgress[i];
                if (progress.IsComplete)
                {
                    continue;
                }

                if (collectedPiece.Definition != progress.Element)
                {
                    continue;
                }

                progress.AddCollected(1);
                goalChanged.Invoke(progress);
                break;
            }

            goalsChanged.Invoke(new List<Match3GoalProgress>(goalProgress));

            if (AreAllGoalsComplete())
            {
                CompleteLevel();
            }
        }

        private bool AreAllGoalsComplete()
        {
            if (goalProgress.Count == 0)
            {
                return false;
            }

            foreach (var progress in goalProgress)
            {
                if (!progress.IsComplete)
                {
                    return false;
                }
            }

            return true;
        }

        private void CompleteLevel()
        {
            if (levelEnded)
            {
                return;
            }

            levelEnded = true;
            timerRunning = false;

            if (disableBoardWhenLevelEnds && board != null)
            {
                board.SetInputEnabled(false);
            }

            levelCompleted.Invoke();
        }

        private void FailLevel()
        {
            if (levelEnded)
            {
                return;
            }

            levelEnded = true;
            timerRunning = false;

            if (disableBoardWhenLevelEnds && board != null)
            {
                board.SetInputEnabled(false);
            }

            levelFailed.Invoke();
        }
    }
}
