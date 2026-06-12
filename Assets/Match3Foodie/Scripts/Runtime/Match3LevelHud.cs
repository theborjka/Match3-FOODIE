using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3Foodie
{
    public sealed class Match3LevelHud : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private Match3LevelController levelController;

        [Header("Timer")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private Image timerProgressImage;

        [Header("Goals")]
        [SerializeField] private Transform goalsRoot;
        [SerializeField] private Match3GoalItemView goalItemPrefab;
        [SerializeField] private List<Match3GoalItemView> goalViews = new();
        [SerializeField] private Match3CollectionTargetProvider collectionTargetProvider;

        private void Awake()
        {
            if (levelController == null)
            {
                levelController = FindAnyObjectByType<Match3LevelController>();
            }

            if (collectionTargetProvider == null)
            {
                collectionTargetProvider = GetComponent<Match3CollectionTargetProvider>();
            }

            if (collectionTargetProvider == null)
            {
                collectionTargetProvider = gameObject.AddComponent<Match3CollectionTargetProvider>();
            }
        }

        private void OnEnable()
        {
            if (levelController == null)
            {
                return;
            }

            levelController.TimerChanged.AddListener(RefreshTimer);
            levelController.GoalsChanged.AddListener(RefreshGoals);
            RefreshAll();
        }

        private void Start()
        {
            RefreshAll();
        }

        private void OnDisable()
        {
            if (levelController == null)
            {
                return;
            }

            levelController.TimerChanged.RemoveListener(RefreshTimer);
            levelController.GoalsChanged.RemoveListener(RefreshGoals);
        }

        public void RefreshAll()
        {
            if (levelController == null)
            {
                return;
            }

            RefreshTimer(levelController.RemainingTime);
            RefreshGoals(new List<Match3GoalProgress>(levelController.Goals));
        }

        private void RefreshTimer(float remainingSeconds)
        {
            if (timerText != null)
            {
                var seconds = Mathf.CeilToInt(remainingSeconds);
                timerText.text = seconds.ToString();
            }

            if (timerProgressImage != null && levelController != null && levelController.LevelSettings != null)
            {
                var timeLimit = Mathf.Max(0.01f, levelController.LevelSettings.TimeLimitSeconds);
                timerProgressImage.fillAmount = Mathf.Clamp01(remainingSeconds / timeLimit);
            }
        }

        private void RefreshGoals(List<Match3GoalProgress> goals)
        {
            if (goalsRoot == null)
            {
                return;
            }

            while (goalViews.Count < goals.Count)
            {
                var createdView = CreateGoalView();
                if (createdView == null)
                {
                    break;
                }

                goalViews.Add(createdView);
            }

            for (var i = 0; i < goalViews.Count; i++)
            {
                var active = i < goals.Count;
                goalViews[i].gameObject.SetActive(active);

                if (active)
                {
                    goalViews[i].Bind(goals[i]);
                    collectionTargetProvider.SetTarget(goals[i].Element, goalViews[i]);
                }
            }
        }

        private Match3GoalItemView CreateGoalView()
        {
            if (goalItemPrefab != null)
            {
                return Instantiate(goalItemPrefab, goalsRoot);
            }

            return null;
        }
    }
}
