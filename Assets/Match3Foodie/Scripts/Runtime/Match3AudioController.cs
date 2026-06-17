using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Match3Foodie
{
    public sealed class Match3AudioController : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private Match3Board board;
        [SerializeField] private Match3LevelController levelController;
        [SerializeField] private Match3MathChallengePopup mathChallengePopup;
        [SerializeField] private AudioSource audioSource;

        [Header("Clips")]
        [SerializeField] private AudioClip matchClip;
        [SerializeField] private AudioClip fishMatchClip;
        [SerializeField] private AudioClip mathMiniGameStartClip;
        [SerializeField] private AudioClip winClip;
        [SerializeField] private AudioClip loseClip;
        [SerializeField] private AudioClip uiButtonClickClip;
        [SerializeField] private AudioClip timerTickClip;
        [SerializeField] private AudioClip mathCorrectAnswerClip;
        [SerializeField] private AudioClip mathWrongAnswerClip;
        [SerializeField] private AudioClip mathRewardArrivedClip;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float matchVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float fishMatchVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float mathMiniGameStartVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float winVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float loseVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float uiButtonClickVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float timerTickVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float mathCorrectAnswerVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float mathWrongAnswerVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float mathRewardArrivedVolume = 1f;

        [Header("Timer Tick")]
        [SerializeField, Min(0f)] private float timerTickThreshold = 10f;

        [Header("UI Buttons")]
        [SerializeField] private bool autoBindButtonsOnEnable = true;
        [SerializeField] private bool includeInactiveButtons = true;

        private readonly HashSet<Button> boundButtons = new();
        private int lastTickSecond = -1;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (board != null)
            {
                board.PiecesMatched.AddListener(HandlePiecesMatched);
                board.PieceCleared.AddListener(HandlePieceCleared);
            }

            if (levelController != null)
            {
                levelController.MathChallengeStarted.AddListener(PlayMathMiniGameStart);
                levelController.LevelCompleted.AddListener(PlayWin);
                levelController.LevelFailed.AddListener(PlayLose);
                levelController.TimerChanged.AddListener(HandleTimerChanged);
            }

            if (mathChallengePopup != null)
            {
                mathChallengePopup.CorrectAnswerSelected.AddListener(PlayMathCorrectAnswer);
                mathChallengePopup.WrongAnswerSelected.AddListener(PlayMathWrongAnswer);
                mathChallengePopup.RewardArrived.AddListener(PlayMathRewardArrived);
            }

            if (autoBindButtonsOnEnable)
            {
                RefreshButtonBindings();
            }
        }

        private void Start()
        {
            if (autoBindButtonsOnEnable)
            {
                RefreshButtonBindings();
            }
        }

        private void OnDisable()
        {
            if (board != null)
            {
                board.PiecesMatched.RemoveListener(HandlePiecesMatched);
                board.PieceCleared.RemoveListener(HandlePieceCleared);
            }

            if (levelController != null)
            {
                levelController.MathChallengeStarted.RemoveListener(PlayMathMiniGameStart);
                levelController.LevelCompleted.RemoveListener(PlayWin);
                levelController.LevelFailed.RemoveListener(PlayLose);
                levelController.TimerChanged.RemoveListener(HandleTimerChanged);
            }

            if (mathChallengePopup != null)
            {
                mathChallengePopup.CorrectAnswerSelected.RemoveListener(PlayMathCorrectAnswer);
                mathChallengePopup.WrongAnswerSelected.RemoveListener(PlayMathWrongAnswer);
                mathChallengePopup.RewardArrived.RemoveListener(PlayMathRewardArrived);
            }

            UnbindButtons();
        }

        [ContextMenu("Refresh Button Bindings")]
        public void RefreshButtonBindings()
        {
            UnbindButtons();

            var buttons = includeInactiveButtons
                ? FindObjectsByType<Button>(FindObjectsInactive.Include)
                : FindObjectsByType<Button>(FindObjectsInactive.Exclude);

            foreach (var button in buttons)
            {
                if (button == null || !boundButtons.Add(button))
                {
                    continue;
                }

                button.onClick.AddListener(PlayUiButtonClick);
            }
        }

        private void UnbindButtons()
        {
            foreach (var button in boundButtons)
            {
                if (button != null)
                {
                    button.onClick.RemoveListener(PlayUiButtonClick);
                }
            }

            boundButtons.Clear();
        }

        private void HandlePiecesMatched(List<Match3PieceView> matchedPieces)
        {
            if (matchedPieces == null || matchedPieces.Count == 0)
            {
                return;
            }

            Play(matchClip, matchVolume);
        }

        private void HandlePieceCleared(Match3PieceView piece)
        {
            if (piece == null
                || piece.Definition == null
                || piece.Definition.SpecialEffectType != Match3SpecialEffectType.Fish)
            {
                return;
            }

            Play(fishMatchClip, fishMatchVolume);
        }

        private void HandleTimerChanged(float remainingSeconds)
        {
            if (remainingSeconds <= 0f || remainingSeconds > timerTickThreshold)
            {
                lastTickSecond = -1;
                return;
            }

            var wholeSecond = Mathf.CeilToInt(remainingSeconds);
            if (wholeSecond == lastTickSecond)
            {
                return;
            }

            lastTickSecond = wholeSecond;
            Play(timerTickClip, timerTickVolume);
        }

        private void PlayMathMiniGameStart()
        {
            Play(mathMiniGameStartClip, mathMiniGameStartVolume);
        }

        private void PlayWin()
        {
            Play(winClip, winVolume);
        }

        private void PlayLose()
        {
            Play(loseClip, loseVolume);
        }

        private void PlayUiButtonClick()
        {
            Play(uiButtonClickClip, uiButtonClickVolume);
        }

        private void PlayMathCorrectAnswer()
        {
            Play(mathCorrectAnswerClip, mathCorrectAnswerVolume);
        }

        private void PlayMathWrongAnswer()
        {
            Play(mathWrongAnswerClip, mathWrongAnswerVolume);
        }

        private void PlayMathRewardArrived(float secondsAdded)
        {
            Play(mathRewardArrivedClip, mathRewardArrivedVolume);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip == null || audioSource == null || volume <= 0f)
            {
                return;
            }

            audioSource.PlayOneShot(clip, volume);
        }

        private void ResolveReferences()
        {
            if (board == null)
            {
                board = FindAnyObjectByType<Match3Board>();
            }

            if (levelController == null)
            {
                levelController = FindAnyObjectByType<Match3LevelController>();
            }

            if (mathChallengePopup == null)
            {
                mathChallengePopup = FindAnyObjectByType<Match3MathChallengePopup>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
        }
    }
}
