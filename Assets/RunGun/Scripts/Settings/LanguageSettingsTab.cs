using UnityEngine;
using UnityEngine.UI;

namespace RunGun.Settings
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/UI/Language Settings Tab")]
    public sealed class LanguageSettingsTab : MonoBehaviour
    {
        private Toggle _englishToggle;
        private Toggle _russianToggle;
        private bool _initialized;

        private void OnEnable()
        {
            if (!_initialized)
                Initialize();

            RefreshWithoutNotify();
            AddListeners();
        }

        private void OnDisable()
        {
            RemoveListeners();
        }

        private void Initialize()
        {
            _englishToggle = FindComponent<Toggle>("EnglishToggle");
            _russianToggle = FindComponent<Toggle>("RussianToggle");

            if (_englishToggle == null || _russianToggle == null)
            {
                Debug.LogError(
                    "LanguagePanel requires EnglishToggle and RussianToggle.", this);
                return;
            }

            ToggleGroup group = GetComponent<ToggleGroup>();
            if (group == null)
                group = gameObject.AddComponent<ToggleGroup>();

            group.allowSwitchOff = false;
            _englishToggle.group = group;
            _russianToggle.group = group;
            _initialized = true;
        }

        private void AddListeners()
        {
            if (!_initialized)
                return;

            _englishToggle.onValueChanged.AddListener(SetEnglish);
            _russianToggle.onValueChanged.AddListener(SetRussian);
        }

        private void RemoveListeners()
        {
            if (!_initialized)
                return;

            _englishToggle.onValueChanged.RemoveListener(SetEnglish);
            _russianToggle.onValueChanged.RemoveListener(SetRussian);
        }

        private void SetEnglish(bool selected)
        {
            if (selected)
                GameLocalization.CurrentLanguage = GameLanguage.English;
        }

        private void SetRussian(bool selected)
        {
            if (selected)
                GameLocalization.CurrentLanguage = GameLanguage.Russian;
        }

        private void RefreshWithoutNotify()
        {
            if (!_initialized)
                return;

            bool english = GameLocalization.CurrentLanguage == GameLanguage.English;
            _englishToggle.SetIsOnWithoutNotify(english);
            _russianToggle.SetIsOnWithoutNotify(!english);
        }

        private T FindComponent<T>(string objectName) where T : Component
        {
            Transform child = FindChildRecursive(transform, objectName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static Transform FindChildRecursive(Transform root, string objectName)
        {
            if (root.name == objectName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindChildRecursive(root.GetChild(i), objectName);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
