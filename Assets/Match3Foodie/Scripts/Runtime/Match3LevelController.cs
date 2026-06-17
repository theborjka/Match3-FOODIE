using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Match3Foodie
{
    public sealed class Match3LevelController : MonoBehaviour
    {
        [Serializable] public sealed class FloatEvent : UnityEvent<float> { }
        [Serializable] public sealed class IntPairEvent : UnityEvent<int, int> { }
        [Serializable] public sealed class GoalProgressEvent : UnityEvent<Match3GoalProgress> { }
        [Serializable] public sealed class GoalsProgressEvent : UnityEvent<List<Match3GoalProgress>> { }

        [Header("Config")]
        [SerializeField] private Match3LevelSettings levelSettings;
        [SerializeField] private Match3Board board;
        [SerializeField] private Match3MathChallengePopup mathChallengePopup;
        [SerializeField] private Match3BoosterController boosterController;
        [SerializeField] private bool startTimerOnEnable = true;
        [SerializeField] private bool disableBoardWhenLevelEnds = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugMathBonusKey = true;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key debugMathBonusKey = Key.M;
        [SerializeField] private bool enableDebugFailKey = true;
        [SerializeField] private Key debugFailKey = Key.L;
#elif ENABLE_LEGACY_INPUT_MANAGER
        [SerializeField] private KeyCode debugMathBonusKey = KeyCode.M;
        [SerializeField] private bool enableDebugFailKey = true;
        [SerializeField] private KeyCode debugFailKey = KeyCode.L;
#endif

        [Header("Events")]
        [SerializeField] private FloatEvent timerChanged = new();
        [SerializeField] private GoalProgressEvent goalChanged = new();
        [SerializeField] private GoalsProgressEvent goalsChanged = new();
        [SerializeField] private IntPairEvent mathBonusCounterChanged = new();
        [SerializeField] private UnityEvent mathChallengeStarted = new();
        [SerializeField] private UnityEvent levelCompleted = new();
        [SerializeField] private UnityEvent levelFailed = new();

        private readonly List<Match3GoalProgress> goalProgress = new();
        private float remainingTime;
        private int mathBonusCollectedAmount;
        private int currentMathBonusRequiredCollections;
        private bool timerRunning;
        private bool levelEnded;
        private bool mathChallengePending;
        private bool mathChallengeRunning;
        private bool levelCompletePending;
        private Coroutine mathChallengeRoutine;

        public Match3LevelSettings LevelSettings => levelSettings;
        public float RemainingTime => remainingTime;
        public int MathBonusCollectedAmount => mathBonusCollectedAmount;
        public int MathBonusRequiredCollections => currentMathBonusRequiredCollections;
        public Match3ElementDefinition MathBonusElement => ResolveMathBonusElement();
        public bool IsTimerRunning => timerRunning;
        public bool IsLevelEnded => levelEnded;
        public IReadOnlyList<Match3GoalProgress> Goals => goalProgress;
        public FloatEvent TimerChanged => timerChanged;
        public GoalProgressEvent GoalChanged => goalChanged;
        public GoalsProgressEvent GoalsChanged => goalsChanged;
        public IntPairEvent MathBonusCounterChanged => mathBonusCounterChanged;
        public UnityEvent MathChallengeStarted => mathChallengeStarted;
        public UnityEvent LevelCompleted => levelCompleted;
        public UnityEvent LevelFailed => levelFailed;

        private void Awake()
        {
            if (board == null)
            {
                board = FindAnyObjectByType<Match3Board>();
            }

            if (mathChallengePopup == null)
            {
                mathChallengePopup = FindAnyObjectByType<Match3MathChallengePopup>();
            }

            if (boosterController == null)
            {
                boosterController = FindAnyObjectByType<Match3BoosterController>();
            }

            ResetLevelState();
        }

        private void OnEnable()
        {
            if (board != null)
            {
                board.PieceCollected.AddListener(HandlePieceCollected);
                board.PieceCleared.AddListener(HandlePieceClearedForMathBonus);
                board.BoardSettled.AddListener(HandleBoardSettled);
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
                board.PieceCleared.RemoveListener(HandlePieceClearedForMathBonus);
                board.BoardSettled.RemoveListener(HandleBoardSettled);
            }
        }

        private void Update()
        {
            if (enableDebugMathBonusKey && TryGetDebugMathBonusInput())
            {
                DebugFillMathBonusCounter();
            }

            if (enableDebugFailKey && TryGetDebugFailInput())
            {
                DebugFailLevel();
            }

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
            mathBonusCollectedAmount = 0;
            currentMathBonusRequiredCollections = GetBaseMathBonusRequiredCollections();
            mathChallengePending = false;
            mathChallengeRunning = false;
            levelCompletePending = false;
            timerRunning = false;
            levelEnded = false;

            if (mathChallengeRoutine != null)
            {
                StopCoroutine(mathChallengeRoutine);
                mathChallengeRoutine = null;
            }

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
            mathBonusCounterChanged.Invoke(mathBonusCollectedAmount, MathBonusRequiredCollections);
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

        public void RestartLevel()
        {
            if (mathChallengeRoutine != null)
            {
                StopCoroutine(mathChallengeRoutine);
                mathChallengeRoutine = null;
            }

            boosterController?.SetControlsLocked(false);
            boosterController?.ResetUsesToInitial();
            ResetLevelState();
            board?.RebuildBoard(true);
            StartTimer();
        }

        [ContextMenu("Debug Fail Level")]
        public void DebugFailLevel()
        {
            if (levelEnded)
            {
                return;
            }

            remainingTime = 0f;
            timerChanged.Invoke(remainingTime);
            FailLevel();
        }

        [ContextMenu("Debug Fill Math Bonus Counter")]
        public void DebugFillMathBonusCounter()
        {
            if (levelEnded
                || levelSettings == null
                || MathBonusElement == null
                || mathChallengePending
                || mathChallengeRunning
                || mathChallengeRoutine != null)
            {
                return;
            }

            var required = Mathf.Max(1, MathBonusRequiredCollections);
            mathBonusCollectedAmount = required;
            mathChallengePending = true;
            mathBonusCounterChanged.Invoke(mathBonusCollectedAmount, required);

            if (board == null || !board.IsResolving)
            {
                HandleBoardSettled();
            }
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
                RequestCompleteLevel();
            }
        }

        private void HandlePieceClearedForMathBonus(Match3PieceView clearedPiece)
        {
            if (levelSettings == null
                || MathBonusElement == null
                || clearedPiece == null
                || clearedPiece.Definition != MathBonusElement
                || mathChallengeRunning)
            {
                return;
            }

            var required = Mathf.Max(1, MathBonusRequiredCollections);
            mathBonusCollectedAmount++;
            mathBonusCounterChanged.Invoke(mathBonusCollectedAmount, required);

            if (mathBonusCollectedAmount >= required)
            {
                mathChallengePending = true;
            }
        }

        private void HandleBoardSettled()
        {
            if (levelCompletePending)
            {
                CompleteLevel();
                return;
            }

            if (levelEnded || !mathChallengePending || mathChallengeRunning || mathChallengeRoutine != null)
            {
                return;
            }

            mathChallengeRoutine = StartCoroutine(StartPendingMathChallengeRoutine());
        }

        private IEnumerator StartPendingMathChallengeRoutine()
        {
            mathChallengeRunning = true;
            var consumedRequiredAmount = Mathf.Max(1, MathBonusRequiredCollections);
            var boardInputShouldResume = !levelEnded;
            board?.SetInputEnabled(false);

            var delay = levelSettings != null ? levelSettings.MathChallengeStartDelay : 0.5f;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (mathChallengePopup == null)
            {
                mathChallengePopup = FindAnyObjectByType<Match3MathChallengePopup>();
            }

            if (boosterController == null)
            {
                boosterController = FindAnyObjectByType<Match3BoosterController>();
            }

            var mathBonusElement = MathBonusElement;
            if (mathChallengePopup != null && levelSettings != null && mathBonusElement != null)
            {
                var timerWasRunning = timerRunning;
                PauseTimer();
                boosterController?.SetControlsLocked(true);

                var answered = false;
                var correctAnswers = 0;
                mathChallengeStarted.Invoke();
                mathChallengePopup.Show(mathBonusElement.MathBonusSeconds, count =>
                {
                    answered = true;
                    correctAnswers = count;
                });

                yield return new WaitUntil(() => answered);

                if (correctAnswers > 0)
                {
                    AddTime(mathBonusElement.MathBonusSeconds * correctAnswers);
                }

                if (timerWasRunning)
                {
                    ResumeTimer();
                }

            }

            if (boardInputShouldResume && !levelEnded)
            {
                board?.SetInputEnabled(true);
            }

            boosterController?.SetControlsLocked(false);

            mathBonusCollectedAmount = Mathf.Max(0, mathBonusCollectedAmount - consumedRequiredAmount);
            IncreaseMathBonusRequirementAfterChallenge();
            mathChallengePending = mathBonusCollectedAmount >= Mathf.Max(1, MathBonusRequiredCollections);
            mathChallengeRunning = false;
            mathChallengeRoutine = null;
            mathBonusCounterChanged.Invoke(mathBonusCollectedAmount, MathBonusRequiredCollections);

            if (mathChallengePending)
            {
                HandleBoardSettled();
            }
        }

        private int GetBaseMathBonusRequiredCollections()
        {
            return levelSettings != null ? Mathf.Max(1, levelSettings.MathBonusRequiredCollections) : 0;
        }

        private void IncreaseMathBonusRequirementAfterChallenge()
        {
            if (levelSettings == null)
            {
                currentMathBonusRequiredCollections = 0;
                return;
            }

            currentMathBonusRequiredCollections = Mathf.Max(
                1,
                currentMathBonusRequiredCollections + levelSettings.MathBonusRequiredIncreasePerChallenge);
        }

        private Match3ElementDefinition ResolveMathBonusElement()
        {
            if (levelSettings != null && levelSettings.MathBonusElement != null)
            {
                return levelSettings.MathBonusElement;
            }

            if (board == null || board.Settings == null)
            {
                return null;
            }

            foreach (var element in board.Settings.Elements)
            {
                if (element != null && element.SpecialEffectType == Match3SpecialEffectType.MathBonus)
                {
                    return element;
                }
            }

            return null;
        }

        private bool TryGetDebugMathBonusInput()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current[debugMathBonusKey].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(debugMathBonusKey);
#else
            return false;
#endif
        }

        private bool TryGetDebugFailInput()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current[debugFailKey].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(debugFailKey);
#else
            return false;
#endif
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

        private void RequestCompleteLevel()
        {
            if (levelEnded || levelCompletePending)
            {
                return;
            }

            if (board != null && board.IsResolving)
            {
                levelCompletePending = true;
                return;
            }

            CompleteLevel();
        }

        private void CompleteLevel()
        {
            if (levelEnded)
            {
                return;
            }

            levelEnded = true;
            levelCompletePending = false;
            timerRunning = false;

            if (disableBoardWhenLevelEnds && board != null)
            {
                board.SetInputEnabled(false);
            }

            boosterController?.SetControlsLocked(true);
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

            board?.SetInputEnabled(false);
            boosterController?.SetControlsLocked(true);

            levelFailed.Invoke();
        }
    }
}
