using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Match3Foodie
{
    public sealed class Match3MathChallengePopup : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private Button[] answerButtons = new Button[3];
        [SerializeField] private TMP_Text[] answerTexts = new TMP_Text[3];

        [Header("Question")]
        [SerializeField, Min(1)] private int minOperand = 1;
        [SerializeField, Min(1)] private int maxOperand = 12;
        [SerializeField, Min(1)] private int questionsPerGame = 3;

        [Header("Feedback")]
        [SerializeField, Min(0f)] private float answerFeedbackDelay = 0.55f;
        [SerializeField] private Color correctGlowColor = new(0.25f, 1f, 0.35f, 1f);
        [SerializeField] private Color wrongGlowColor = new(1f, 0.18f, 0.12f, 1f);

        private Action<int> completed;
        private readonly List<Color> defaultButtonColors = new();
        private int correctAnswer;
        private int questionIndex;
        private int correctAnswers;
        private bool isOpen;
        private bool isWaitingForFeedback;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            Hide();
        }

        public void Show(Action<bool> onCompleted)
        {
            Show(correctCount => onCompleted?.Invoke(correctCount > 0));
        }

        public void Show(Action<int> onCompleted)
        {
            completed = onCompleted;
            isOpen = true;
            isWaitingForFeedback = false;
            questionIndex = 0;
            correctAnswers = 0;

            GenerateQuestion();

            if (root != null)
            {
                root.SetActive(true);
            }
        }

        public void Hide()
        {
            isOpen = false;

            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void GenerateQuestion()
        {
            questionIndex++;
            ResetButtonVisuals();
            SetButtonsInteractable(true);

            var a = Random.Range(minOperand, maxOperand + 1);
            var b = Random.Range(minOperand, maxOperand + 1);
            correctAnswer = a + b;

            if (questionText != null)
            {
                questionText.text = $"{a} + {b} = ?";
            }

            var correctIndex = Random.Range(0, answerButtons.Length);
            var usedAnswers = new HashSet<int> { correctAnswer };
            for (var i = 0; i < answerButtons.Length; i++)
            {
                if (answerButtons[i] == null)
                {
                    continue;
                }

                var answer = i == correctIndex ? correctAnswer : GenerateWrongAnswer(usedAnswers);
                usedAnswers.Add(answer);
                if (answerTexts != null && i < answerTexts.Length && answerTexts[i] != null)
                {
                    answerTexts[i].text = answer.ToString();
                }

                answerButtons[i].onClick.RemoveAllListeners();
                var capturedAnswer = answer;
                answerButtons[i].onClick.AddListener(() => HandleAnswer(capturedAnswer));
            }
        }

        private int GenerateWrongAnswer(HashSet<int> usedAnswers)
        {
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var offset = Random.Range(-5, 6);
                var answer = Mathf.Max(0, correctAnswer + offset);
                if (offset != 0 && !usedAnswers.Contains(answer))
                {
                    return answer;
                }
            }

            var fallback = correctAnswer + 1;
            while (usedAnswers.Contains(fallback))
            {
                fallback++;
            }

            return fallback;
        }

        private void HandleAnswer(int answer)
        {
            if (!isOpen || isWaitingForFeedback)
            {
                return;
            }

            var wasCorrect = answer == correctAnswer;
            if (wasCorrect)
            {
                correctAnswers++;
            }

            StartCoroutine(AnswerFeedbackRoutine(answer, wasCorrect));
        }

        private IEnumerator AnswerFeedbackRoutine(int answer, bool wasCorrect)
        {
            isWaitingForFeedback = true;

            for (var i = 0; i < answerButtons.Length; i++)
            {
                if (!TryGetButtonAnswer(i, out var buttonAnswer))
                {
                    continue;
                }

                if (buttonAnswer == correctAnswer)
                {
                    SetButtonColor(answerButtons[i], correctGlowColor);
                }
                else if (!wasCorrect && buttonAnswer == answer)
                {
                    SetButtonColor(answerButtons[i], wrongGlowColor);
                }
            }

            if (answerFeedbackDelay > 0f)
            {
                yield return new WaitForSeconds(answerFeedbackDelay);
            }

            isWaitingForFeedback = false;

            if (questionIndex < questionsPerGame)
            {
                GenerateQuestion();
                yield break;
            }

            var callback = completed;
            completed = null;
            Hide();
            callback?.Invoke(correctAnswers);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            foreach (var button in answerButtons)
            {
                if (button != null)
                {
                    button.interactable = interactable;
                }
            }
        }

        private void ResetButtonVisuals()
        {
            CacheDefaultButtonColors();
            for (var i = 0; i < answerButtons.Length; i++)
            {
                if (answerButtons[i] != null && i < defaultButtonColors.Count)
                {
                    SetButtonColor(answerButtons[i], defaultButtonColors[i]);
                }
            }
        }

        private void CacheDefaultButtonColors()
        {
            while (defaultButtonColors.Count < answerButtons.Length)
            {
                var index = defaultButtonColors.Count;
                var graphic = answerButtons[index] != null ? answerButtons[index].targetGraphic : null;
                defaultButtonColors.Add(graphic != null ? graphic.color : Color.white);
            }
        }

        private static void SetButtonColor(Button button, Color color)
        {
            if (button != null && button.targetGraphic != null)
            {
                button.targetGraphic.color = color;
            }
        }

        private bool TryGetButtonAnswer(int index, out int answer)
        {
            answer = 0;
            return answerButtons != null
                && index >= 0
                && index < answerButtons.Length
                && answerButtons[index] != null
                && answerTexts != null
                && index < answerTexts.Length
                && answerTexts[index] != null
                && int.TryParse(answerTexts[index].text, out answer);
        }
    }
}
