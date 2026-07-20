using System;
using System.Collections.Generic;
using UnityEngine;

namespace RunGun.Levels
{
    [CreateAssetMenu(fileName = "LevelTimingDefinition", menuName = "RunGun/Levels/Timing Definition")]
    public sealed class LevelTimingDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class RankDefinition
        {
            [SerializeField] private string displayName;
            [SerializeField] private Sprite icon;
            [SerializeField] private Color textColor = Color.white;
            [Min(0f)] [SerializeField] private float maximumTime;

            public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Rank" : displayName;
            public Sprite Icon => icon;
            public Color TextColor => textColor;
            public float MaximumTime => maximumTime;

            public RankDefinition(string name, float time, Color color)
            {
                displayName = name;
                maximumTime = time;
                textColor = color;
            }

            public void SetMinimumTime(float minimum) => maximumTime = Mathf.Max(minimum, maximumTime);
            public void SetIconIfMissing(Sprite fallback)
            {
                if (icon == null)
                    icon = fallback;
            }

            public void SetColorIfMissing(Color fallback)
            {
                if (textColor.a <= 0f)
                    textColor = fallback;
            }
        }

        [Tooltip("Unique and permanent ID used for the PlayerPrefs best-time key.")]
        [SerializeField] private string levelId;
        [SerializeField] private List<RankDefinition> ranks = new();

        public string LevelId => levelId?.Trim();
        public IReadOnlyList<RankDefinition> Ranks => ranks;
        public bool HasTimedRanks => ranks != null && ranks.Count > 0;

        private void OnValidate()
        {
            if (ranks == null)
                return;

            float previousMaximum = 0f;
            for (int i = 0; i < ranks.Count; i++)
            {
                RankDefinition rank = ranks[i];
                if (rank == null)
                    continue;

                if (i < ranks.Count - 1)
                {
                    rank.SetMinimumTime(previousMaximum);
                    previousMaximum = rank.MaximumTime;
                }
            }
        }
    }
}
