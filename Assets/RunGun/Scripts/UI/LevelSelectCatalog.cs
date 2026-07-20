using System;
using System.Collections.Generic;
using UnityEngine;

namespace RunGun.UI
{
    [CreateAssetMenu(fileName = "LevelSelectCatalog", menuName = "RunGun/UI/Level Select Catalog")]
    public sealed class LevelSelectCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class RankRequirement
        {
            [SerializeField] private string displayName;
            [SerializeField] private float maximumTime;
            [SerializeField] private Sprite icon;

            public string DisplayName => displayName;
            public float MaximumTime => maximumTime;
            public Sprite Icon => icon;
        }

        [Serializable]
        public sealed class LevelDefinition
        {
            [SerializeField] private string number;
            [SerializeField] private string displayName;
            [TextArea(2, 4)] [SerializeField] private string description;
            [SerializeField] private string sceneName;
            [Tooltip("Must match LevelRunTimer's Level Id for timed levels.")]
            [SerializeField] private string bestTimeId;
            [SerializeField] private bool tracksTime;
            [SerializeField] private bool usesTimedRanks;
            [SerializeField] private List<RankRequirement> ranks = new();

            public string Number => number;
            public string DisplayName => displayName;
            public string Description => description;
            public string SceneName => sceneName;
            public string BestTimeId => bestTimeId;
            public bool TracksTime => tracksTime;
            public bool UsesTimedRanks => usesTimedRanks;
            public IReadOnlyList<RankRequirement> Ranks => ranks;
        }

        [Serializable]
        public sealed class CategoryDefinition
        {
            [SerializeField] private string id;
            [SerializeField] private string displayName;
            [SerializeField] private List<LevelDefinition> levels = new();

            public string Id => id;
            public string DisplayName => displayName;
            public IReadOnlyList<LevelDefinition> Levels => levels;
        }

        [SerializeField] private List<CategoryDefinition> categories = new();

        public IReadOnlyList<CategoryDefinition> Categories => categories;
    }
}
