using System;
using System.Collections.Generic;
using RunGun.Levels;
using UnityEngine;

namespace RunGun.UI
{
    [CreateAssetMenu(fileName = "LevelSelectCatalog", menuName = "RunGun/UI/Level Select Catalog")]
    public sealed class LevelSelectCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class LevelDefinition
        {
            [SerializeField] private string number;
            [SerializeField] private string displayName;
            [TextArea(2, 4)] [SerializeField] private string description;
            [SerializeField] private string sceneName;
            [Tooltip("Must match LevelRunTimer's Level Id for timed levels.")]
            [SerializeField] private string bestTimeId;
            [Tooltip("Shared source for the save ID and rank requirements. Overrides the legacy fields below when assigned.")]
            [SerializeField] private LevelTimingDefinition timingDefinition;
            [SerializeField] private bool tracksTime;
            [SerializeField] private bool usesTimedRanks;
            [SerializeField] private List<LevelTimingDefinition.RankDefinition> ranks = new();

            public string Number => number;
            public string DisplayName => displayName;
            public string Description => description;
            public string SceneName => sceneName;
            public string BestTimeId => timingDefinition != null ? timingDefinition.LevelId : bestTimeId;
            public bool TracksTime => timingDefinition != null || tracksTime;
            public bool UsesTimedRanks => timingDefinition != null ? timingDefinition.HasTimedRanks : usesTimedRanks;
            public IReadOnlyList<LevelTimingDefinition.RankDefinition> Ranks =>
                timingDefinition != null ? timingDefinition.Ranks : ranks;
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
