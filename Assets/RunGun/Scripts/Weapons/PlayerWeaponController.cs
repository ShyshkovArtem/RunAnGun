using System;
using System.Collections.Generic;
using ElmanGameDevTools.PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace RunGun.Weapons
{
    public sealed class PlayerWeaponController : MonoBehaviour
    {
        [Serializable]
        public sealed class WeaponDefinition
        {
            public WeaponType type;
            public string displayName;
            public bool unlocked = true;
            public int magazineSize = 6;
            public int ammoInMagazine = -1;
            public float reloadTime = 1f;
            public float fireInterval = 0.25f;
            public int impactPower = 1;
            public GameObject viewModel;
        }

        [SerializeField] private List<WeaponDefinition> weapons = new();
        [SerializeField] private int startWeaponIndex;
        [SerializeField] private Transform gunPlace;
        [SerializeField] private Transform aimCamera;
        [SerializeField] private PlayerController movementController;
        [SerializeField] private bool alignGunPlaceToCamera = true;
        [SerializeField] private bool followCameraPosition = true;
        [SerializeField] private Vector3 gunPlaceCameraOffset = new(0.25f, -0.25f, 0.45f);
        [SerializeField] private LayerMask impactMask = ~0;
        [SerializeField] private float rayOriginForwardOffset = 0.25f;
        [SerializeField] private float pistolImpulse = 3.75f;
        [SerializeField] private float rifleImpulse = 1.2f;
        [SerializeField] private float shotgunImpulse = 18f;
        [SerializeField] private float shotgunMissImpulse = 3.5f;
        [SerializeField] private float shotgunImpactDistance = 8f;
        [SerializeField] private float bazookaImpulse = 30f;
        [SerializeField] private float bazookaMissImpulse = 8f;
        [SerializeField] private float bazookaImpactDistance = 80f;
        [SerializeField] private float bazookaRadius = 6f;
        [SerializeField] private bool logWeaponFire;
        [SerializeField] private bool logWeaponImpulses;
        [SerializeField] private Vector3 switchPositionOffset = new(0f, -0.25f, 0.08f);
        [SerializeField] private float switchDuration = 0.18f;
        [SerializeField] private Vector3 shotPositionOffset = new(0f, -0.02f, -0.12f);
        [SerializeField] private Vector3 shotRotationOffset = new(-7f, 0f, 0f);
        [SerializeField] private float shotKick = 1f;
        [SerializeField] private float shotReturnSpeed = 18f;

        public event Action<WeaponDefinition> WeaponChanged;
        public event Action<WeaponDefinition> AmmoChanged;

        public WeaponDefinition CurrentWeapon => IsValidIndex(_selectedIndex) ? weapons[_selectedIndex] : null;
        public IReadOnlyList<WeaponDefinition> Weapons => weapons;
        public int SelectedIndex => _selectedIndex;
        public bool IsReloading => _isReloading;

        private int _selectedIndex = -1;
        private bool _isReloading;
        private float _reloadCompleteTime;
        private float _nextFireTime;
        private readonly Dictionary<GameObject, ViewModelPose> _viewModelPoses = new();
        private float _switchTimer;
        private float _shotBlend;
        private Quaternion _gunPlaceCameraRotationOffset = Quaternion.identity;
        private bool _hasCachedGunPlaceOffset;

        private sealed class ViewModelPose
        {
            public Transform transform;
            public Vector3 basePosition;
            public Quaternion baseRotation;
        }

        private void Reset()
        {
            CreateDefaultWeapons();
        }

        private void Awake()
        {
            if (weapons == null || weapons.Count == 0)
            {
                CreateDefaultWeapons();
            }

            for (var i = 0; i < weapons.Count; i++)
            {
                var weapon = weapons[i];
                if (weapon == null)
                {
                    continue;
                }

                weapon.magazineSize = Mathf.Max(1, weapon.magazineSize);
                if (weapon.ammoInMagazine < 0)
                {
                    weapon.ammoInMagazine = weapon.magazineSize;
                }

                weapon.ammoInMagazine = Mathf.Clamp(weapon.ammoInMagazine, 0, weapon.magazineSize);
            }

            if (movementController == null)
            {
                movementController = GetComponent<PlayerController>();
            }

            AutoBindViewModels();
            CacheGunPlaceCameraTransformOffset();
        }

        private void Start()
        {
            SelectWeapon(Mathf.Clamp(startWeaponIndex, 0, weapons.Count - 1), true);
        }

        private void Update()
        {
            UpdateReload();
            ReadWeaponSelectionInput();
            ReadFireInput();
            ReadReloadInput();
        }

        private void LateUpdate()
        {
            AlignGunPlaceToCamera();
            AnimateCurrentViewModel();
        }

        public void SelectWeapon(WeaponType type)
        {
            for (var i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] != null && weapons[i].type == type)
                {
                    SelectWeapon(i, false);
                    return;
                }
            }
        }

        public void SelectWeapon(int index, bool force)
        {
            if (!IsValidIndex(index))
            {
                return;
            }

            var nextWeapon = weapons[index];
            if (nextWeapon == null || !nextWeapon.unlocked)
            {
                return;
            }

            if (!force && _selectedIndex == index)
            {
                return;
            }

            _selectedIndex = index;
            _isReloading = false;
            _switchTimer = Mathf.Max(0.01f, switchDuration);
            _shotBlend = 0f;
            SetViewModelsActive();

            WeaponChanged?.Invoke(nextWeapon);
            AmmoChanged?.Invoke(nextWeapon);
        }

        public void TryFire()
        {
            var weapon = CurrentWeapon;
            if (weapon == null || _isReloading || Time.time < _nextFireTime)
            {
                return;
            }

            if (weapon.ammoInMagazine <= 0)
            {
                TryReload();
                return;
            }

            weapon.ammoInMagazine--;
            _nextFireTime = Time.time + Mathf.Max(0.01f, weapon.fireInterval);
            _shotBlend = Mathf.Clamp01(_shotBlend + shotKick);
            ApplyMovementImpact(weapon);

            AmmoChanged?.Invoke(weapon);
            if (logWeaponFire)
            {
                Debug.Log($"Fired {weapon.displayName}. Ammo: {weapon.ammoInMagazine}/{weapon.magazineSize}");
            }

            if (weapon.ammoInMagazine <= 0)
            {
                TryReload();
            }
        }

        public void TryReload()
        {
            var weapon = CurrentWeapon;
            if (weapon == null || _isReloading || weapon.ammoInMagazine >= weapon.magazineSize)
            {
                return;
            }

            _isReloading = true;
            _reloadCompleteTime = Time.time + Mathf.Max(0.01f, weapon.reloadTime);
        }

        private void UpdateReload()
        {
            if (!_isReloading || Time.time < _reloadCompleteTime)
            {
                return;
            }

            _isReloading = false;
            var weapon = CurrentWeapon;
            if (weapon == null)
            {
                return;
            }

            weapon.ammoInMagazine = weapon.magazineSize;
            AmmoChanged?.Invoke(weapon);
        }

        private void ReadWeaponSelectionInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (WasPressed(keyboard.digit1Key, keyboard.numpad1Key)) SelectWeapon(0, false);
            if (WasPressed(keyboard.digit2Key, keyboard.numpad2Key)) SelectWeapon(1, false);
            if (WasPressed(keyboard.digit3Key, keyboard.numpad3Key)) SelectWeapon(2, false);
            if (WasPressed(keyboard.digit4Key, keyboard.numpad4Key)) SelectWeapon(3, false);
        }

        private void ReadFireInput()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                TryFire();
            }
        }

        private void ReadReloadInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                TryReload();
            }
        }

        private void ApplyMovementImpact(WeaponDefinition weapon)
        {
            if (movementController == null)
            {
                return;
            }

            var aimTransform = aimCamera != null ? aimCamera : Camera.main != null ? Camera.main.transform : transform;
            Vector3 aimDirection = aimTransform.forward.normalized;
            Vector3 rayOrigin = aimTransform.position + aimDirection * rayOriginForwardOffset;
            Vector3 impulse = weapon.type switch
            {
                WeaponType.Pistol => -aimDirection * pistolImpulse,
                WeaponType.Rifle => -aimDirection * rifleImpulse,
                WeaponType.Shotgun => CalculateShotgunImpulse(rayOrigin, aimDirection),
                WeaponType.Bazooka => CalculateBazookaImpulse(rayOrigin, aimDirection),
                _ => Vector3.zero
            };

            if (impulse.sqrMagnitude <= 0.001f)
            {
                return;
            }

            movementController.AddExternalImpulse(impulse);

            if (logWeaponImpulses)
            {
                Debug.Log($"{weapon.displayName} movement impulse: {impulse}");
            }
        }

        private Vector3 CalculateShotgunImpulse(Vector3 rayOrigin, Vector3 aimDirection)
        {
            if (Physics.Raycast(rayOrigin, aimDirection, out var hit, shotgunImpactDistance, impactMask, QueryTriggerInteraction.Ignore))
            {
                float distance01 = Mathf.Clamp01(hit.distance / Mathf.Max(0.01f, shotgunImpactDistance));
                float falloff = Mathf.Lerp(1f, 0.35f, distance01);
                Vector3 blastDirection = (hit.normal - aimDirection * 0.35f).normalized;
                return blastDirection * (shotgunImpulse * falloff);
            }

            return -aimDirection * shotgunMissImpulse;
        }

        private Vector3 CalculateBazookaImpulse(Vector3 rayOrigin, Vector3 aimDirection)
        {
            if (!Physics.Raycast(rayOrigin, aimDirection, out var hit, bazookaImpactDistance, impactMask, QueryTriggerInteraction.Ignore))
            {
                return -aimDirection * bazookaMissImpulse;
            }

            Vector3 playerCenter = movementController.controller != null
                ? movementController.controller.bounds.center
                : movementController.transform.position;

            Vector3 blastDirection = playerCenter - hit.point;
            if (blastDirection.sqrMagnitude < 0.001f)
            {
                blastDirection = -aimDirection;
            }

            float distance = blastDirection.magnitude;
            float falloff = Mathf.Clamp01(1f - distance / Mathf.Max(0.01f, bazookaRadius));
            falloff = Mathf.Lerp(0.35f, 1f, falloff);
            return blastDirection.normalized * (bazookaImpulse * falloff);
        }

        private void SetViewModelsActive()
        {
            for (var i = 0; i < weapons.Count; i++)
            {
                var weapon = weapons[i];
                if (weapon?.viewModel != null)
                {
                    ResetViewModelPose(weapon.viewModel);
                    weapon.viewModel.SetActive(i == _selectedIndex);
                }
            }
        }

        private void AnimateCurrentViewModel()
        {
            var weapon = CurrentWeapon;
            if (weapon?.viewModel == null || !_viewModelPoses.TryGetValue(weapon.viewModel, out var pose))
            {
                return;
            }

            if (_switchTimer > 0f)
            {
                _switchTimer = Mathf.Max(0f, _switchTimer - Time.deltaTime);
            }

            _shotBlend = Mathf.MoveTowards(_shotBlend, 0f, shotReturnSpeed * Time.deltaTime);

            var switchBlend = switchDuration <= 0f ? 0f : _switchTimer / switchDuration;
            switchBlend = Mathf.SmoothStep(0f, 1f, switchBlend);

            var targetPosition = pose.basePosition;
            targetPosition += switchPositionOffset * switchBlend;
            targetPosition += shotPositionOffset * _shotBlend;

            var recoilRotation = Quaternion.Euler(shotRotationOffset * _shotBlend);

            pose.transform.localPosition = targetPosition;
            pose.transform.localRotation = pose.baseRotation * recoilRotation;
        }

        private void ResetViewModelPose(GameObject viewModel)
        {
            if (viewModel == null || !_viewModelPoses.TryGetValue(viewModel, out var pose))
            {
                return;
            }

            pose.transform.localPosition = pose.basePosition;
            pose.transform.localRotation = pose.baseRotation;
        }

        private void AutoBindViewModels()
        {
            if (gunPlace == null)
            {
                var gunPlaceTransform = FindChildRecursive(transform, "GunPlace");
                if (gunPlaceTransform != null)
                {
                    gunPlace = gunPlaceTransform;
                }
            }

            if (aimCamera == null)
            {
                var childCamera = GetComponentInChildren<Camera>(true);
                if (childCamera != null)
                {
                    aimCamera = childCamera.transform;
                }
                else if (Camera.main != null)
                {
                    aimCamera = Camera.main.transform;
                }
            }

            for (var i = 0; i < weapons.Count; i++)
            {
                var weapon = weapons[i];
                if (weapon == null)
                {
                    continue;
                }

                if (weapon.viewModel == null && gunPlace != null)
                {
                    var viewModel = FindDirectChild(gunPlace, weapon.type.ToString());
                    if (viewModel == null)
                    {
                        viewModel = FindChildRecursive(gunPlace, weapon.type.ToString());
                    }

                    if (viewModel != null)
                    {
                        weapon.viewModel = viewModel.gameObject;
                    }
                }

                CacheViewModelPose(weapon.viewModel);
            }

            SetViewModelsActive();
        }

        private void CacheGunPlaceCameraTransformOffset()
        {
            if (gunPlace == null || aimCamera == null)
            {
                _gunPlaceCameraRotationOffset = Quaternion.identity;
                _hasCachedGunPlaceOffset = false;
                return;
            }

            _gunPlaceCameraRotationOffset = Quaternion.Inverse(aimCamera.rotation) * gunPlace.rotation;

            if (!_hasCachedGunPlaceOffset)
            {
                gunPlaceCameraOffset = aimCamera.InverseTransformPoint(gunPlace.position);
                _hasCachedGunPlaceOffset = true;
            }
        }

        private void AlignGunPlaceToCamera()
        {
            if (gunPlace == null || aimCamera == null)
            {
                return;
            }

            if (followCameraPosition)
            {
                gunPlace.position = aimCamera.TransformPoint(gunPlaceCameraOffset);
            }

            if (alignGunPlaceToCamera)
            {
                gunPlace.rotation = aimCamera.rotation * _gunPlaceCameraRotationOffset;
            }
        }

        private void CacheViewModelPose(GameObject viewModel)
        {
            if (viewModel == null || _viewModelPoses.ContainsKey(viewModel))
            {
                return;
            }

            var viewModelTransform = viewModel.transform;
            _viewModelPoses.Add(viewModel, new ViewModelPose
            {
                transform = viewModelTransform,
                basePosition = viewModelTransform.localPosition,
                baseRotation = viewModelTransform.localRotation
            });
        }

        private bool IsValidIndex(int index)
        {
            return weapons != null && index >= 0 && index < weapons.Count;
        }

        private static bool WasPressed(KeyControl key, KeyControl fallbackKey)
        {
            return key.wasPressedThisFrame || fallbackKey.wasPressedThisFrame;
        }

        private static Transform FindDirectChild(Transform root, string childName)
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
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            var directChild = FindDirectChild(root, childName);
            if (directChild != null)
            {
                return directChild;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var nested = FindChildRecursive(root.GetChild(i), childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void CreateDefaultWeapons()
        {
            weapons = new List<WeaponDefinition>
            {
                new()
                {
                    type = WeaponType.Pistol,
                    displayName = "Pistol",
                    magazineSize = 6,
                    ammoInMagazine = 6,
                    reloadTime = 0.9f,
                    fireInterval = 0.25f,
                    impactPower = 1
                },
                new()
                {
                    type = WeaponType.Shotgun,
                    displayName = "Shotgun",
                    magazineSize = 5,
                    ammoInMagazine = 5,
                    reloadTime = 1.2f,
                    fireInterval = 0.75f,
                    impactPower = 2
                },
                new()
                {
                    type = WeaponType.Rifle,
                    displayName = "Rifle",
                    magazineSize = 15,
                    ammoInMagazine = 15,
                    reloadTime = 1.4f,
                    fireInterval = 0.1f,
                    impactPower = 2
                },
                new()
                {
                    type = WeaponType.Bazooka,
                    displayName = "Bazooka",
                    magazineSize = 1,
                    ammoInMagazine = 1,
                    reloadTime = 1.8f,
                    fireInterval = 1.2f,
                    impactPower = 3
                }
            };
        }
    }
}
