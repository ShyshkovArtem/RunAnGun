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
        [SerializeField, Range(0, 255)] private int selectedBackgroundAlpha = 150;
        [SerializeField, Range(0, 255)] private int selectedIconAlpha = 255;

        private readonly Dictionary<WeaponType, WeaponSlotView> _slotByType = new();

        private void Awake()
        {
            if (weaponController == null)
            {
                weaponController = GetComponent<PlayerWeaponController>();
            }

            AutoBindMissingReferences();
            CacheSlots();
        }

        private void OnEnable()
        {
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
            if (weaponController == controller)
            {
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
                SetText(string.Empty);
                return;
            }

            SetText($"{weapon.ammoInMagazine}/{weapon.magazineSize}");
        }

        private void SetText(string value)
        {
            if (ammoTextComponent == null)
            {
                return;
            }

            if (ammoTextComponent is Text uiText)
            {
                uiText.text = value;
                return;
            }

            var textProperty = ammoTextComponent.GetType().GetProperty(
                "text",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (textProperty != null && textProperty.PropertyType == typeof(string) && textProperty.CanWrite)
            {
                textProperty.SetValue(ammoTextComponent, value);
            }
        }

        private void AutoBindMissingReferences()
        {
            if (gunsPanel == null)
            {
                var panelObject = GameObject.Find("GunsPanel");
                if (panelObject != null)
                {
                    gunsPanel = panelObject.transform;
                }
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
                var ammoTextObject = GameObject.Find("AmmoText");
                if (ammoTextObject != null)
                {
                    ammoTextComponent = GetTextComponent(ammoTextObject);
                }
            }

            if (ammoIcon == null)
            {
                var ammoIconObject = GameObject.Find("AmmoIcon");
                if (ammoIconObject != null)
                {
                    ammoIcon = ammoIconObject.GetComponent<Image>();
                }
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
            _slotByType.Clear();

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                slot.CacheDefaults();
                _slotByType[slot.type] = slot;
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
