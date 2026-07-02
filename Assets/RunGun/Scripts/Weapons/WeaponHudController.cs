using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace RunGun.Weapons
{
    public sealed class WeaponHudController : MonoBehaviour
    {
        [Serializable]
        public sealed class WeaponSlotView
        {
            public WeaponType type;
            public GameObject root;
            public Image background;
            public Image icon;

            [NonSerialized] public Color defaultBackgroundColor;
            [NonSerialized] public Color defaultIconColor;
            [NonSerialized] public bool cachedDefaults;

            public void CacheDefaults()
            {
                if (cachedDefaults)
                {
                    return;
                }

                if (background != null)
                {
                    defaultBackgroundColor = background.color;
                }

                if (icon != null)
                {
                    defaultIconColor = icon.color;
                }

                cachedDefaults = true;
            }
        }

        [SerializeField] private PlayerWeaponController weaponController;
        [SerializeField] private Transform gunsPanel;
        [SerializeField] private List<WeaponSlotView> slots = new();
        [SerializeField] private Component ammoTextComponent;
        [SerializeField] private Image ammoIcon;
        [SerializeField] private GameObject reloadCircleRoot;
        [SerializeField] private Image reloadCircle;
        [SerializeField] private bool hideReloadCircleWhenIdle = true;
        [SerializeField, Range(0, 255)] private int selectedBackgroundAlpha = 150;
        [SerializeField, Range(0, 255)] private int selectedIconAlpha = 255;

        private PropertyInfo _ammoTextProperty;
        private PlayerWeaponController.WeaponDefinition _lastAmmoWeapon;
        private int _lastAmmo = -1;
        private int _lastMagazineSize = -1;
        private string _lastAmmoText;
        private bool _lastReloadVisible;
        private float _lastReloadFill = -1f;

        private void Awake()
        {
            if (weaponController == null)
            {
                weaponController = GetComponent<PlayerWeaponController>();
            }

            AutoBindMissingReferences();
            CacheSlots();
            ConfigureReloadCircle();
        }

        private void Update()
        {
            UpdateAmmoText(weaponController != null ? weaponController.CurrentWeapon : null);
            UpdateReloadCircle();
        }

        private void OnEnable()
        {
            if (weaponController == null)
            {
                weaponController = GetComponent<PlayerWeaponController>();
            }

            AutoBindMissingReferences();
            CacheSlots();
            ConfigureReloadCircle();

            if (weaponController == null)
            {
                return;
            }

            weaponController.WeaponChanged += HandleWeaponChanged;
            weaponController.AmmoChanged += HandleAmmoChanged;

            if (weaponController.CurrentWeapon != null)
            {
                Refresh(weaponController.CurrentWeapon);
            }
        }

        private void OnDisable()
        {
            if (weaponController == null)
            {
                return;
            }

            weaponController.WeaponChanged -= HandleWeaponChanged;
            weaponController.AmmoChanged -= HandleAmmoChanged;
        }

        public void Bind(PlayerWeaponController controller)
        {
            if (weaponController == controller && controller != null)
            {
                Refresh(controller.CurrentWeapon);
                return;
            }

            if (isActiveAndEnabled && weaponController != null)
            {
                weaponController.WeaponChanged -= HandleWeaponChanged;
                weaponController.AmmoChanged -= HandleAmmoChanged;
            }

            weaponController = controller;

            if (isActiveAndEnabled && weaponController != null)
            {
                weaponController.WeaponChanged += HandleWeaponChanged;
                weaponController.AmmoChanged += HandleAmmoChanged;
                if (weaponController.CurrentWeapon != null)
                {
                    Refresh(weaponController.CurrentWeapon);
                }
            }
        }

        private void HandleWeaponChanged(PlayerWeaponController.WeaponDefinition weapon)
        {
            Refresh(weapon);
        }

        private void HandleAmmoChanged(PlayerWeaponController.WeaponDefinition weapon)
        {
            UpdateAmmoText(weapon);
        }

        private void Refresh(PlayerWeaponController.WeaponDefinition selectedWeapon)
        {
            CacheSlots();

            foreach (var slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                var selected = selectedWeapon != null && slot.type == selectedWeapon.type;
                ApplySlotState(slot, selected);
            }

            UpdateAmmoText(selectedWeapon);
            UpdateReloadCircle();
        }

        private void ApplySlotState(WeaponSlotView slot, bool selected)
        {
            slot.CacheDefaults();

            if (slot.background != null)
            {
                var color = selected ? slot.background.color : slot.defaultBackgroundColor;
                if (selected)
                {
                    color.a = selectedBackgroundAlpha / 255f;
                }

                slot.background.color = color;
            }

            if (slot.icon != null)
            {
                var color = selected ? slot.icon.color : slot.defaultIconColor;
                if (selected)
                {
                    color.a = selectedIconAlpha / 255f;
                }

                slot.icon.color = color;
            }
        }

        private void UpdateAmmoText(PlayerWeaponController.WeaponDefinition weapon)
        {
            if (weapon == null)
            {
                _lastAmmoWeapon = null;
                _lastAmmo = -1;
                _lastMagazineSize = -1;
                SetText(string.Empty);
                return;
            }

            if (_lastAmmoWeapon == weapon
                && _lastAmmo == weapon.ammoInMagazine
                && _lastMagazineSize == weapon.magazineSize)
            {
                return;
            }

            _lastAmmoWeapon = weapon;
            _lastAmmo = weapon.ammoInMagazine;
            _lastMagazineSize = weapon.magazineSize;
            SetText($"{weapon.ammoInMagazine}/{weapon.magazineSize}");
        }

        private void SetText(string value)
        {
            if (ammoTextComponent == null)
            {
                return;
            }

            if (_lastAmmoText == value)
            {
                return;
            }

            if (ammoTextComponent is Text uiText)
            {
                uiText.text = value;
                _lastAmmoText = value;
                return;
            }

            _ammoTextProperty ??= ammoTextComponent.GetType().GetProperty(
                    "text",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (_ammoTextProperty != null && _ammoTextProperty.PropertyType == typeof(string) && _ammoTextProperty.CanWrite)
            {
                _ammoTextProperty.SetValue(ammoTextComponent, value);
                _lastAmmoText = value;
            }
        }

        private void AutoBindMissingReferences()
        {
            if (gunsPanel == null)
            {
                gunsPanel = FindChildRecursive(transform, "GunsPanel") ?? GameObject.Find("GunsPanel")?.transform;
            }

            if (slots == null || slots.Count == 0)
            {
                slots = new List<WeaponSlotView>
                {
                    CreateSlot(WeaponType.Pistol),
                    CreateSlot(WeaponType.Shotgun),
                    CreateSlot(WeaponType.Rifle),
                    CreateSlot(WeaponType.Bazooka)
                };
            }
            else
            {
                for (var i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null)
                    {
                        FillSlotReferences(slots[i]);
                    }
                }
            }

            if (ammoTextComponent == null)
            {
                var ammoTextObject = FindChildRecursive(transform, "AmmoText")?.gameObject ?? GameObject.Find("AmmoText");
                if (ammoTextObject != null)
                {
                    ammoTextComponent = GetTextComponent(ammoTextObject);
                    _ammoTextProperty = null;
                    _lastAmmoText = null;
                }
            }
            else if (!CanWriteText(ammoTextComponent))
            {
                var textComponent = GetTextComponent(ammoTextComponent.gameObject);
                if (textComponent != null)
                {
                    ammoTextComponent = textComponent;
                    _ammoTextProperty = null;
                    _lastAmmoText = null;
                }
            }

            if (ammoIcon == null)
            {
                var ammoIconObject = FindChildRecursive(transform, "AmmoIcon")?.gameObject ?? GameObject.Find("AmmoIcon");
                if (ammoIconObject != null)
                {
                    ammoIcon = ammoIconObject.GetComponent<Image>();
                }
            }

            if (reloadCircle == null)
            {
                BindReloadCircle();
            }
        }

        private void BindReloadCircle()
        {
            Transform reloadRootTransform = reloadCircleRoot != null
                ? reloadCircleRoot.transform
                : FindChildRecursive(transform, "ReloadCircle");

            if (reloadRootTransform == null)
            {
                reloadCircle = FindSceneImageByName("ReloadCircle");
                reloadRootTransform = reloadCircle != null ? reloadCircle.transform : null;
            }

            if (reloadRootTransform == null)
            {
                return;
            }

            reloadCircleRoot = reloadRootTransform.gameObject;
            if (reloadCircle == null)
            {
                reloadCircle = GetReloadFillImage(reloadCircleRoot);
            }
        }

        private void ConfigureReloadCircle()
        {
            if (reloadCircle == null)
            {
                return;
            }

            if (reloadCircleRoot == null)
            {
                reloadCircleRoot = reloadCircle.gameObject;
            }

            reloadCircle.type = Image.Type.Filled;
            reloadCircle.fillMethod = Image.FillMethod.Radial360;
            reloadCircle.fillAmount = 0f;

            if (hideReloadCircleWhenIdle)
            {
                reloadCircleRoot.SetActive(false);
            }
        }

        private void UpdateReloadCircle()
        {
            if (reloadCircle == null || reloadCircleRoot == null)
            {
                BindReloadCircle();
                ConfigureReloadCircle();
            }

            if (reloadCircle == null || weaponController == null)
            {
                return;
            }

            bool reloading = weaponController.IsReloading;
            if (hideReloadCircleWhenIdle && reloadCircleRoot != null && _lastReloadVisible != reloading)
            {
                reloadCircleRoot.SetActive(reloading);
                _lastReloadVisible = reloading;
            }

            float fillAmount = reloading ? weaponController.ReloadProgress : 0f;
            if (!Mathf.Approximately(_lastReloadFill, fillAmount))
            {
                reloadCircle.fillAmount = fillAmount;
                _lastReloadFill = fillAmount;
            }

            if (!hideReloadCircleWhenIdle)
            {
                reloadCircle.enabled = reloading;
            }
        }

        private WeaponSlotView CreateSlot(WeaponType type)
        {
            var slot = new WeaponSlotView { type = type };
            FillSlotReferences(slot);
            return slot;
        }

        private void FillSlotReferences(WeaponSlotView slot)
        {
            if (slot.root == null)
            {
                var root = gunsPanel != null
                    ? FindChildRecursive(gunsPanel, slot.type.ToString())
                    : GameObject.Find(slot.type.ToString())?.transform;

                if (root != null)
                {
                    slot.root = root.gameObject;
                }
            }

            if (slot.root == null)
            {
                return;
            }

            if (slot.background == null)
            {
                var background = FindChildRecursive(slot.root.transform, "Background");
                if (background == null)
                {
                    background = FindChildRecursive(slot.root.transform, "Backgroud");
                }

                slot.background = background != null
                    ? background.GetComponent<Image>()
                    : slot.root.GetComponent<Image>();
            }

            if (slot.icon == null)
            {
                var icon = FindChildRecursive(slot.root.transform, "Icon");
                slot.icon = icon != null ? icon.GetComponent<Image>() : null;
            }
        }

        private void CacheSlots()
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                slot.CacheDefaults();
            }
        }

        private static Component GetTextComponent(GameObject target)
        {
            var uiText = target.GetComponent<Text>();
            if (uiText != null)
            {
                return uiText;
            }

            var components = target.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                var textProperty = component.GetType().GetProperty(
                    "text",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (textProperty != null && textProperty.PropertyType == typeof(string) && textProperty.CanWrite)
                {
                    return component;
                }
            }

            return null;
        }

        private static bool CanWriteText(Component component)
        {
            if (component == null)
            {
                return false;
            }

            if (component is Text)
            {
                return true;
            }

            var textProperty = component.GetType().GetProperty(
                "text",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return textProperty != null && textProperty.PropertyType == typeof(string) && textProperty.CanWrite;
        }

        private static Image FindSceneImageByName(string objectName)
        {
            var images = Resources.FindObjectsOfTypeAll<Image>();
            for (var i = 0; i < images.Length; i++)
            {
                var image = images[i];
                if (image == null || !image.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (image.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                {
                    return image;
                }
            }

            return null;
        }

        private static Image GetReloadFillImage(GameObject reloadRoot)
        {
            if (reloadRoot == null)
            {
                return null;
            }

            var rootImage = reloadRoot.GetComponent<Image>();
            if (rootImage != null)
            {
                return rootImage;
            }

            var childImages = reloadRoot.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < childImages.Length; i++)
            {
                var imageName = childImages[i].name;
                if (imageName.Equals("ReloadCircle", StringComparison.OrdinalIgnoreCase)
                    || imageName.Equals("ReloadFill", StringComparison.OrdinalIgnoreCase)
                    || imageName.Equals("Fill", StringComparison.OrdinalIgnoreCase)
                    || imageName.Equals("Progress", StringComparison.OrdinalIgnoreCase))
                {
                    return childImages[i];
                }
            }

            for (var i = 0; i < childImages.Length; i++)
            {
                if (!childImages[i].name.Contains("Background", StringComparison.OrdinalIgnoreCase))
                {
                    return childImages[i];
                }
            }

            return childImages.Length > 0 ? childImages[0] : null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                var nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
