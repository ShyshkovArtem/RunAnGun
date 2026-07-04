using System;
using System.Collections.Generic;
using ElmanGameDevTools.PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
            public bool reloadOneByOne;
            public float fireInterval = 0.25f;
            public int impactPower = 1;
            public GameObject viewModel;
            public Animator viewModelAnimator;
            public RuntimeAnimatorController reloadAnimatorController;
            public string reloadAnimationState;
            public Transform rocketVisual;
            public Transform muzzleTransform;
            public GameObject muzzleFlashPrefab;
            public GameObject impactEffectPrefab;
            public AudioClip shotClip;
            [Range(0f, 1f)] public float shotVolume = 1f;
            [Range(0.1f, 3f)] public float shotPitch = 1f;
            public AudioClip reloadClip;
            [Range(0f, 1f)] public float reloadVolume = 1f;
            [Range(0.1f, 3f)] public float reloadPitch = 1f;
        }

        [Header("Weapons")]
        [SerializeField] private List<WeaponDefinition> weapons = new();
        [SerializeField] private int startWeaponIndex;

        [Header("References")]
        [SerializeField] private Transform gunPlace;
        [SerializeField] private Transform aimCamera;
        [SerializeField] private PlayerController movementController;
        [SerializeField] private AudioSource shotAudioSource;
        [SerializeField] private AudioSource reloadAudioSource;

        [Header("Auto Binding")]
        [SerializeField] private bool autoBindAudioClips = true;
        [SerializeField] private bool autoBindWarFxPrefabs = true;
        [SerializeField] private bool useMuzzlePointChildEffects = true;

        [Header("Effects")]
        [SerializeField] private float muzzleFlashLifetime = 1f;
        [SerializeField] private float impactEffectLifetime = 4f;
        [SerializeField] private float pistolImpactEffectScale = 0.45f;
        [SerializeField] private float rifleImpactEffectScale = 0.35f;
        [SerializeField] private float shotgunImpactEffectScale = 0.32f;
        [SerializeField] private int shotgunImpactEffectCount = 8;
        [SerializeField] private float shotgunImpactEffectSpread = 0.24f;

        [Header("View Alignment")]
        [SerializeField] private bool alignGunPlaceToCamera = true;
        [SerializeField] private bool followCameraPosition = true;
        [SerializeField] private Vector3 gunPlaceCameraOffset = new(0.25f, -0.25f, 0.45f);
        [SerializeField] private LayerMask impactMask = ~0;
        [SerializeField] private float rayOriginForwardOffset = 0.25f;

        [Header("Movement Impulses")]
        [SerializeField] private float pistolImpulse = 3.75f;
        [SerializeField] private float rifleImpulse = 1.2f;
        [SerializeField] private float shotgunImpulse = 18f;
        [SerializeField] private float shotgunMissImpulse = 3.5f;
        [SerializeField] private float shotgunImpactDistance = 8f;
        [SerializeField] private float bazookaImpulse = 30f;
        [SerializeField] private float bazookaMissImpulse = 8f;
        [SerializeField] private float bazookaImpactDistance = 80f;
        [SerializeField] private float bazookaRadius = 6f;
        [SerializeField] private float bazookaRocketSpeed = 45f;
        [SerializeField] private float bazookaRocketMaxFlightTime = 2f;

        [Header("Debug")]
        [SerializeField] private bool logWeaponFire;
        [SerializeField] private bool logWeaponImpulses;

        [Header("Impact Marks")]
        [SerializeField] private bool spawnBulletHoles = true;
        [SerializeField] private int maxTemporaryImpactMarks = 80;
        [SerializeField] private float bulletHoleLifetime = 6f;
        [SerializeField] private float bulletHoleSurfaceOffset = 0.01f;
        [SerializeField] private Color bulletHoleColor = new(0.03f, 0.03f, 0.03f, 0.9f);
        [SerializeField] private float pistolBulletHoleSize = 0.08f;
        [SerializeField] private float rifleBulletHoleSize = 0.07f;
        [SerializeField] private float shotgunPelletHoleSize = 0.055f;
        [SerializeField] private int shotgunPelletMarks = 9;
        [SerializeField] private float shotgunPelletSpread = 0.22f;
        [SerializeField] private float bazookaBulletHoleSize = 0.85f;
        [SerializeField] private Sprite bazookaExplosionDecal;
        [SerializeField] private int bazookaScorchMarks = 8;
        [SerializeField] private float bazookaScorchSpread = 0.55f;

        [Header("View Model Animation")]
        [SerializeField] private Vector3 switchPositionOffset = new(0f, -0.25f, 0.08f);
        [SerializeField] private float switchDuration = 0.18f;
        [SerializeField] private Vector3 shotPositionOffset = new(0f, -0.02f, -0.12f);
        [SerializeField] private Vector3 shotRotationOffset = new(-7f, 0f, 0f);
        [SerializeField] private float shotKick = 1f;
        [SerializeField] private float shotReturnSpeed = 18f;
        [SerializeField] private float reloadAnimationTransitionDuration = 0.05f;
        [SerializeField] private bool reloadAnimationControlsViewPose = true;

        public event Action<WeaponDefinition> WeaponChanged;
        public event Action<WeaponDefinition> AmmoChanged;

        public WeaponDefinition CurrentWeapon => IsValidIndex(_selectedIndex) ? weapons[_selectedIndex] : null;
        public IReadOnlyList<WeaponDefinition> Weapons => weapons;
        public int SelectedIndex => _selectedIndex;
        public bool IsReloading => _isReloading;
        public float ReloadProgress
        {
            get
            {
                if (!_isReloading)
                {
                    return 0f;
                }

                var weapon = CurrentWeapon;
                if (weapon == null)
                {
                    return 0f;
                }

                float reloadDuration = Mathf.Max(0.01f, weapon.reloadTime);
                return Mathf.Clamp01(1f - ((_reloadCompleteTime - Time.time) / reloadDuration));
            }
        }

        private int _selectedIndex = -1;
        private bool _isReloading;
        private float _reloadCompleteTime;
        private float _nextFireTime;
        private readonly Queue<GameObject> _impactMarks = new();
        private readonly Dictionary<GameObject, ViewModelPose> _viewModelPoses = new();
        private float _switchTimer;
        private float _shotBlend;
        private Quaternion _gunPlaceCameraRotationOffset = Quaternion.identity;
        private bool _hasCachedGunPlaceOffset;
        private Transform _cachedFallbackCamera;
        private Coroutine _bazookaRocketFlight;
        private readonly Dictionary<Transform, RocketVisualPose> _rocketVisualPoses = new();
        private static Mesh _bulletHoleMesh;
        private static Material _bulletHoleMaterial;

        private sealed class ViewModelPose
        {
            public Transform transform;
            public Vector3 basePosition;
            public Quaternion baseRotation;
        }

        private sealed class RocketVisualPose
        {
            public Transform parent;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
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
                if (weapon.type == WeaponType.Shotgun)
                {
                    weapon.reloadOneByOne = true;
                }

                if (weapon.ammoInMagazine < 0)
                {
                    weapon.ammoInMagazine = weapon.magazineSize;
                }

                weapon.ammoInMagazine = Mathf.Clamp(weapon.ammoInMagazine, 0, weapon.magazineSize);
                weapon.shotVolume = Mathf.Clamp01(weapon.shotVolume);
                weapon.reloadVolume = Mathf.Clamp01(weapon.reloadVolume);
                weapon.shotPitch = Mathf.Clamp(weapon.shotPitch, 0.1f, 3f);
                weapon.reloadPitch = Mathf.Clamp(weapon.reloadPitch, 0.1f, 3f);
            }

            if (movementController == null)
            {
                movementController = GetComponent<PlayerController>();
            }

            EnsureAudioSources();
            AutoBindAudioClips();
            AutoBindWarFxPrefabs();
            AutoBindReloadAnimationControllers();
            AutoBindDecals();
            AutoBindViewModels();
            SetMuzzlePointTemplatesActive(false);
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

        private void OnDisable()
        {
            StopReloadAnimation(CurrentWeapon);
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

            var previousWeapon = CurrentWeapon;
            if (_isReloading)
            {
                StopReloadAnimation(previousWeapon);
            }

            _selectedIndex = index;
            CancelReload();
            _switchTimer = Mathf.Max(0.01f, switchDuration);
            _shotBlend = 0f;
            SetViewModelsActive();

            WeaponChanged?.Invoke(nextWeapon);
            AmmoChanged?.Invoke(nextWeapon);
        }

        public void TryFire()
        {
            var weapon = CurrentWeapon;
            if (weapon == null || Time.time < _nextFireTime)
            {
                return;
            }

            if (_isReloading)
            {
                if (!weapon.reloadOneByOne || weapon.ammoInMagazine <= 0)
                {
                    return;
                }

                CancelReload();
            }

            if (weapon.ammoInMagazine <= 0)
            {
                TryReload();
                return;
            }

            weapon.ammoInMagazine--;
            _nextFireTime = Time.time + Mathf.Max(0.01f, weapon.fireInterval);
            _shotBlend = Mathf.Clamp01(_shotBlend + shotKick);
            PlayShotSound(weapon);

            GetAimRay(out Vector3 rayOrigin, out Vector3 aimDirection);
            bool hasHit = TryRaycastWeapon(weapon.type, rayOrigin, aimDirection, out RaycastHit hit);
            SpawnMuzzleFlash(weapon, aimDirection);

            if (weapon.type == WeaponType.Bazooka)
            {
                LaunchBazookaRocket(weapon, rayOrigin, aimDirection, hasHit, hit);
                CompleteShot(weapon, false);
                return;
            }

            SpawnImpactEffect(weapon, hasHit, hit);
            SpawnBulletHole(weapon, hasHit, hit);
            ApplyMovementImpact(weapon, rayOrigin, aimDirection, hasHit, hit);

            CompleteShot(weapon, true);
        }

        private void CompleteShot(WeaponDefinition weapon, bool autoReloadWhenEmpty)
        {
            AmmoChanged?.Invoke(weapon);
            if (logWeaponFire)
            {
                Debug.Log($"Fired {weapon.displayName}. Ammo: {weapon.ammoInMagazine}/{weapon.magazineSize}");
            }

            if (autoReloadWhenEmpty && weapon.ammoInMagazine <= 0)
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
            StartReloadStep(weapon);
        }

        private void UpdateReload()
        {
            if (!_isReloading || Time.time < _reloadCompleteTime)
            {
                return;
            }

            var weapon = CurrentWeapon;
            if (weapon == null)
            {
                CancelReload();
                return;
            }

            if (weapon.reloadOneByOne)
            {
                weapon.ammoInMagazine = Mathf.Min(weapon.ammoInMagazine + 1, weapon.magazineSize);
                AmmoChanged?.Invoke(weapon);

                if (weapon.ammoInMagazine >= weapon.magazineSize)
                {
                    CancelReload();
                    SetRocketLoadedVisible(weapon);
                    return;
                }

                StartReloadStep(weapon);
                return;
            }

            weapon.ammoInMagazine = weapon.magazineSize;
            CancelReload();
            SetRocketLoadedVisible(weapon);
            AmmoChanged?.Invoke(weapon);
        }

        private void StartReloadStep(WeaponDefinition weapon)
        {
            _reloadCompleteTime = Time.time + Mathf.Max(0.01f, weapon.reloadTime);
            PlayReloadSound(weapon);
            PlayReloadAnimation(weapon);
        }

        private void CancelReload()
        {
            StopReloadAnimation(CurrentWeapon);
            _isReloading = false;
            _reloadCompleteTime = 0f;

            if (reloadAudioSource != null)
            {
                reloadAudioSource.Stop();
            }
        }

        private void EnsureAudioSources()
        {
            if (shotAudioSource == null)
            {
                shotAudioSource = gameObject.AddComponent<AudioSource>();
            }

            if (reloadAudioSource == null)
            {
                reloadAudioSource = gameObject.AddComponent<AudioSource>();
            }

            ConfigureAudioSource(shotAudioSource);
            ConfigureAudioSource(reloadAudioSource);
        }

        private static void ConfigureAudioSource(AudioSource audioSource)
        {
            if (audioSource == null)
            {
                return;
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        private void PlayShotSound(WeaponDefinition weapon)
        {
            if (shotAudioSource == null || weapon?.shotClip == null)
            {
                return;
            }

            shotAudioSource.pitch = weapon.shotPitch;
            shotAudioSource.PlayOneShot(weapon.shotClip, weapon.shotVolume);
        }

        private void PlayReloadSound(WeaponDefinition weapon)
        {
            if (reloadAudioSource == null || weapon?.reloadClip == null)
            {
                return;
            }

            reloadAudioSource.Stop();
            reloadAudioSource.clip = weapon.reloadClip;
            reloadAudioSource.volume = weapon.reloadVolume;
            reloadAudioSource.pitch = weapon.reloadPitch;
            reloadAudioSource.Play();
        }

        private void PlayReloadAnimation(WeaponDefinition weapon)
        {
            if (weapon == null || weapon.reloadAnimatorController == null || string.IsNullOrWhiteSpace(weapon.reloadAnimationState))
            {
                return;
            }

            var animator = EnsureWeaponAnimator(weapon);
            if (animator == null)
            {
                return;
            }

            if (animator.runtimeAnimatorController != weapon.reloadAnimatorController)
            {
                animator.runtimeAnimatorController = weapon.reloadAnimatorController;
            }

            animator.enabled = true;
            animator.speed = GetReloadAnimationSpeed(weapon);
            float transitionDuration = Mathf.Max(0f, reloadAnimationTransitionDuration);
            if (transitionDuration > 0f)
            {
                animator.CrossFadeInFixedTime(weapon.reloadAnimationState, transitionDuration, 0, 0f);
            }
            else
            {
                animator.Play(weapon.reloadAnimationState, 0, 0f);
            }
        }

        private void StopReloadAnimation(WeaponDefinition weapon)
        {
            var animator = weapon?.viewModelAnimator;
            if (animator == null)
            {
                return;
            }

            animator.speed = 1f;
            if (animator.gameObject.activeInHierarchy)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            animator.enabled = false;

            if (weapon.viewModel != null)
            {
                ResetViewModelPose(weapon.viewModel);
            }
        }

        private float GetReloadAnimationSpeed(WeaponDefinition weapon)
        {
            float reloadDuration = Mathf.Max(0.01f, weapon.reloadTime);
            var clips = weapon.reloadAnimatorController != null ? weapon.reloadAnimatorController.animationClips : null;
            if (clips == null || clips.Length == 0)
            {
                return 1f;
            }

            AnimationClip clip = null;
            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].name.Equals(weapon.reloadAnimationState, StringComparison.OrdinalIgnoreCase))
                {
                    clip = clips[i];
                    break;
                }
            }

            clip ??= clips[0];
            return clip != null ? Mathf.Max(0.01f, clip.length / reloadDuration) : 1f;
        }

        private void AutoBindAudioClips()
        {
            if (!autoBindAudioClips)
            {
                return;
            }

            for (var i = 0; i < weapons.Count; i++)
            {
                var weapon = weapons[i];
                if (weapon == null)
                {
                    continue;
                }

                if (weapon.shotClip == null)
                {
                    weapon.shotClip = LoadWeaponAudioClip(weapon.type, "Shot");
                }

                if (weapon.reloadClip == null)
                {
                    weapon.reloadClip = LoadWeaponAudioClip(weapon.type, "Reload");
                }
            }
        }

        private static AudioClip LoadWeaponAudioClip(WeaponType weaponType, string action)
        {
#if UNITY_EDITOR
            string typeName = weaponType.ToString().ToLowerInvariant();
            string path = $"Assets/RunGun/Audio/Weapon{action}/{typeName}{action}.mp3";
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
#else
            return null;
#endif
        }

        private void AutoBindWarFxPrefabs()
        {
            if (!autoBindWarFxPrefabs)
            {
                return;
            }

#if UNITY_EDITOR
            var defaultMuzzle = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/JMO Assets/WarFX/_Effects/MuzzleFlashes/FPS/WFX_MF FPS RIFLE1.prefab");
            var defaultImpact = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/JMO Assets/WarFX/_Effects/Bullet Impacts/+ Bullet Hole/Unlit/WFX_BImpact Concrete + Hole Unlit.prefab");

            for (var i = 0; i < weapons.Count; i++)
            {
                var weapon = weapons[i];
                if (weapon == null)
                {
                    continue;
                }

                if (weapon.muzzleFlashPrefab == null)
                {
                    weapon.muzzleFlashPrefab = defaultMuzzle;
                }

                if (weapon.impactEffectPrefab == null)
                {
                    weapon.impactEffectPrefab = defaultImpact;
                }
            }
#endif
        }

        private void AutoBindReloadAnimationControllers()
        {
#if UNITY_EDITOR
            var pistolReloadController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/RunGun/Animation/M1911.controller");
#else
            RuntimeAnimatorController pistolReloadController = null;
#endif

            for (var i = 0; i < weapons.Count; i++)
            {
                var weapon = weapons[i];
                if (weapon == null)
                {
                    continue;
                }

                if (weapon.type != WeaponType.Pistol)
                {
                    continue;
                }

                if (weapon.reloadAnimatorController == null)
                {
                    weapon.reloadAnimatorController = pistolReloadController;
                }

                if (string.IsNullOrWhiteSpace(weapon.reloadAnimationState))
                {
                    weapon.reloadAnimationState = "PistolReload";
                }
            }
        }

        private void ReadWeaponSelectionInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (WasPressed(keyboard.digit1Key, keyboard.numpad1Key)) SelectWeapon(0, false);
                if (WasPressed(keyboard.digit2Key, keyboard.numpad2Key)) SelectWeapon(1, false);
                if (WasPressed(keyboard.digit3Key, keyboard.numpad3Key)) SelectWeapon(2, false);
                if (WasPressed(keyboard.digit4Key, keyboard.numpad4Key)) SelectWeapon(3, false);
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float scrollY = mouse.scroll.ReadValue().y;
            if (scrollY > 0.01f)
            {
                CycleWeapon(-1);
            }
            else if (scrollY < -0.01f)
            {
                CycleWeapon(1);
            }
        }

        private void CycleWeapon(int direction)
        {
            if (weapons == null || weapons.Count <= 1)
            {
                return;
            }

            int step = direction >= 0 ? 1 : -1;
            int startIndex = IsValidIndex(_selectedIndex) ? _selectedIndex : 0;

            for (var i = 1; i <= weapons.Count; i++)
            {
                int nextIndex = (startIndex + (i * step)) % weapons.Count;
                if (nextIndex < 0)
                {
                    nextIndex += weapons.Count;
                }

                var weapon = weapons[nextIndex];
                if (weapon != null && weapon.unlocked)
                {
                    SelectWeapon(nextIndex, false);
                    return;
                }
            }
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

        private void ApplyMovementImpact(WeaponDefinition weapon, Vector3 rayOrigin, Vector3 aimDirection, bool hasHit, RaycastHit hit)
        {
            if (movementController == null)
            {
                return;
            }

            Vector3 impulse = weapon.type switch
            {
                WeaponType.Pistol => -aimDirection * pistolImpulse,
                WeaponType.Rifle => -aimDirection * rifleImpulse,
                WeaponType.Shotgun => CalculateShotgunImpulse(aimDirection, hasHit, hit),
                WeaponType.Bazooka => CalculateBazookaImpulse(aimDirection, hasHit, hit),
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

        private void SpawnBulletHole(WeaponDefinition weapon, bool hasHit, RaycastHit hit)
        {
            if (!spawnBulletHoles || weapon == null)
            {
                return;
            }

            if (!hasHit)
            {
                return;
            }

            if (weapon.type != WeaponType.Bazooka && weapon.impactEffectPrefab != null)
            {
                return;
            }

            if (weapon.type == WeaponType.Shotgun)
            {
                SpawnShotgunPelletHoles(hit);
                return;
            }

            if (weapon.type == WeaponType.Bazooka)
            {
                SpawnBazookaExplosionPrint(hit);
                return;
            }

            CreateBulletHole(hit, GetBulletHoleSize(weapon.type), $"BulletHole_{weapon.type}");
        }

        private void SpawnMuzzleFlash(WeaponDefinition weapon, Vector3 aimDirection)
        {
            if (weapon == null)
            {
                return;
            }

            Transform muzzleTransform = weapon.muzzleTransform;
            if (useMuzzlePointChildEffects && TrySpawnMuzzlePointChildEffects(muzzleTransform))
            {
                return;
            }

            if (weapon.muzzleFlashPrefab == null)
            {
                return;
            }

            Vector3 position;
            Quaternion rotation;
            Transform parent = null;

            if (muzzleTransform != null)
            {
                position = muzzleTransform.position;
                rotation = muzzleTransform.rotation;
                parent = muzzleTransform;
            }
            else
            {
                position = weapon.viewModel != null
                    ? weapon.viewModel.transform.position + aimDirection * 0.45f
                    : rayOriginFallback(aimDirection);
                rotation = Quaternion.LookRotation(aimDirection);
            }

            var muzzleFlash = Instantiate(weapon.muzzleFlashPrefab, position, rotation, parent);
            Destroy(muzzleFlash, Mathf.Max(0.05f, muzzleFlashLifetime));

            Vector3 rayOriginFallback(Vector3 direction)
            {
                GetAimRay(out Vector3 origin, out _);
                return origin + direction * 0.65f;
            }
        }

        private void SpawnImpactEffect(WeaponDefinition weapon, bool hasHit, RaycastHit hit)
        {
            if (weapon?.impactEffectPrefab == null || !hasHit)
            {
                return;
            }

            if (weapon.type == WeaponType.Bazooka)
            {
                return;
            }

            if (weapon.type == WeaponType.Shotgun)
            {
                SpawnShotgunImpactEffects(weapon.impactEffectPrefab, hit);
                return;
            }

            SpawnImpactEffectInstance(
                weapon.impactEffectPrefab,
                hit,
                GetImpactEffectScale(weapon.type),
                $"ImpactEffect_{weapon.type}");
        }

        private void SpawnShotgunImpactEffects(GameObject impactPrefab, RaycastHit hit)
        {
            var surfaceRotation = Quaternion.LookRotation(hit.normal);
            int effectCount = Mathf.Max(1, shotgunImpactEffectCount);

            for (var i = 0; i < effectCount; i++)
            {
                Vector2 pelletOffset = UnityEngine.Random.insideUnitCircle * shotgunImpactEffectSpread;
                Vector3 worldOffset = surfaceRotation * new Vector3(pelletOffset.x, pelletOffset.y, 0f);
                float scale = shotgunImpactEffectScale * UnityEngine.Random.Range(0.8f, 1.2f);
                SpawnImpactEffectInstance(impactPrefab, hit, scale, "ImpactEffect_ShotgunPellet", worldOffset);
            }
        }

        private void SpawnImpactEffectInstance(GameObject impactPrefab, RaycastHit hit, float scale, string objectName, Vector3 worldOffset = default)
        {
            Quaternion rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            Vector3 position = hit.point + worldOffset + hit.normal * bulletHoleSurfaceOffset;
            var impactEffect = Instantiate(impactPrefab, position, rotation);
            impactEffect.name = objectName;
            impactEffect.transform.localScale *= Mathf.Max(0.01f, scale);
            impactEffect.transform.SetParent(hit.collider.transform, true);
            Destroy(impactEffect, Mathf.Max(0.1f, impactEffectLifetime));
            RegisterImpactMark(impactEffect);
        }

        private float GetImpactEffectScale(WeaponType weaponType)
        {
            return weaponType switch
            {
                WeaponType.Pistol => pistolImpactEffectScale,
                WeaponType.Rifle => rifleImpactEffectScale,
                WeaponType.Shotgun => shotgunImpactEffectScale,
                _ => 1f
            };
        }

        private void LaunchBazookaRocket(WeaponDefinition weapon, Vector3 rayOrigin, Vector3 aimDirection, bool hasHit, RaycastHit hit)
        {
            var rocketVisual = EnsureRocketVisual(weapon);
            if (rocketVisual == null)
            {
                SpawnImpactEffect(weapon, hasHit, hit);
                SpawnBulletHole(weapon, hasHit, hit);
                ApplyMovementImpact(weapon, rayOrigin, aimDirection, hasHit, hit);
                if (CurrentWeapon == weapon && weapon.ammoInMagazine <= 0)
                {
                    TryReload();
                }

                return;
            }

            if (_bazookaRocketFlight != null)
            {
                StopCoroutine(_bazookaRocketFlight);
                _bazookaRocketFlight = null;
                RestoreRocketVisual(rocketVisual, false);
            }

            Vector3 targetPosition = hasHit
                ? hit.point
                : rayOrigin + aimDirection * GetBulletHoleRange(WeaponType.Bazooka);

            _bazookaRocketFlight = StartCoroutine(FlyBazookaRocket(weapon, rocketVisual, targetPosition, rayOrigin, aimDirection, hasHit, hit));
        }

        private System.Collections.IEnumerator FlyBazookaRocket(
            WeaponDefinition weapon,
            Transform rocketVisual,
            Vector3 targetPosition,
            Vector3 rayOrigin,
            Vector3 aimDirection,
            bool hasHit,
            RaycastHit hit)
        {
            CacheRocketVisualPose(rocketVisual);

            Vector3 startPosition = rocketVisual.position;
            rocketVisual.SetParent(null, true);
            rocketVisual.position = startPosition;
            rocketVisual.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
            rocketVisual.gameObject.SetActive(true);

            float distance = Vector3.Distance(startPosition, targetPosition);
            float duration = distance / Mathf.Max(0.01f, bazookaRocketSpeed);
            duration = Mathf.Min(duration, Mathf.Max(0.01f, bazookaRocketMaxFlightTime));
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (rocketVisual == null)
                {
                    yield break;
                }

                float t = duration <= 0f ? 1f : elapsed / duration;
                rocketVisual.position = Vector3.Lerp(startPosition, targetPosition, t);
                Vector3 flightDirection = targetPosition - rocketVisual.position;
                if (flightDirection.sqrMagnitude > 0.0001f)
                {
                    rocketVisual.rotation = Quaternion.LookRotation(flightDirection.normalized, Vector3.up);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (rocketVisual != null)
            {
                rocketVisual.position = targetPosition;
                RestoreRocketVisual(rocketVisual, false);
            }

            SpawnImpactEffect(weapon, hasHit, hit);
            SpawnBulletHole(weapon, hasHit, hit);
            ApplyMovementImpact(weapon, rayOrigin, aimDirection, hasHit, hit);
            _bazookaRocketFlight = null;

            if (CurrentWeapon == weapon && weapon.ammoInMagazine <= 0)
            {
                TryReload();
            }
        }

        private Transform EnsureRocketVisual(WeaponDefinition weapon)
        {
            if (weapon == null || weapon.type != WeaponType.Bazooka)
            {
                return null;
            }

            if (weapon.rocketVisual == null && weapon.viewModel != null)
            {
                weapon.rocketVisual = FindChildRecursive(weapon.viewModel.transform, "Rocket");
            }

            if (weapon.rocketVisual != null)
            {
                CacheRocketVisualPose(weapon.rocketVisual);
            }

            return weapon.rocketVisual;
        }

        private void CacheRocketVisualPose(Transform rocketVisual)
        {
            if (rocketVisual == null || _rocketVisualPoses.ContainsKey(rocketVisual))
            {
                return;
            }

            _rocketVisualPoses.Add(rocketVisual, new RocketVisualPose
            {
                parent = rocketVisual.parent,
                localPosition = rocketVisual.localPosition,
                localRotation = rocketVisual.localRotation,
                localScale = rocketVisual.localScale
            });
        }

        private void RestoreRocketVisual(Transform rocketVisual, bool active)
        {
            if (rocketVisual == null)
            {
                return;
            }

            if (_rocketVisualPoses.TryGetValue(rocketVisual, out var pose))
            {
                rocketVisual.SetParent(pose.parent, false);
                rocketVisual.localPosition = pose.localPosition;
                rocketVisual.localRotation = pose.localRotation;
                rocketVisual.localScale = pose.localScale;
            }

            rocketVisual.gameObject.SetActive(active);
        }

        private void SetRocketLoadedVisible(WeaponDefinition weapon)
        {
            var rocketVisual = EnsureRocketVisual(weapon);
            if (rocketVisual != null)
            {
                RestoreRocketVisual(rocketVisual, true);
            }
        }

        private float GetBulletHoleRange(WeaponType weaponType)
        {
            return weaponType switch
            {
                WeaponType.Shotgun => shotgunImpactDistance,
                WeaponType.Bazooka => bazookaImpactDistance,
                _ => bazookaImpactDistance
            };
        }

        private float GetBulletHoleSize(WeaponType weaponType)
        {
            return weaponType switch
            {
                WeaponType.Pistol => pistolBulletHoleSize,
                WeaponType.Rifle => rifleBulletHoleSize,
                WeaponType.Shotgun => shotgunPelletHoleSize,
                WeaponType.Bazooka => bazookaBulletHoleSize,
                _ => pistolBulletHoleSize
            };
        }

        private void SpawnShotgunPelletHoles(RaycastHit hit)
        {
            var surfaceRotation = Quaternion.LookRotation(hit.normal);
            int markCount = Mathf.Max(1, shotgunPelletMarks);

            for (var i = 0; i < markCount; i++)
            {
                Vector2 pelletOffset = UnityEngine.Random.insideUnitCircle * shotgunPelletSpread;
                Vector3 worldOffset = surfaceRotation * new Vector3(pelletOffset.x, pelletOffset.y, 0f);
                float size = shotgunPelletHoleSize * UnityEngine.Random.Range(0.75f, 1.25f);
                CreateBulletHole(hit, size, "ShotgunPelletHole", worldOffset);
            }
        }

        private void SpawnBazookaExplosionPrint(RaycastHit hit)
        {
            var surfaceRotation = Quaternion.LookRotation(hit.normal);
            EnsureBazookaExplosionDecal();
            CreateSpriteDecal(hit, bazookaBulletHoleSize, "BazookaExplosionPrint", bazookaExplosionDecal, UnityEngine.Random.Range(0f, 360f));

            int scorchCount = Mathf.Max(0, bazookaScorchMarks);
            for (var i = 0; i < scorchCount; i++)
            {
                Vector2 scorchOffset = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(bazookaBulletHoleSize * 0.25f, bazookaScorchSpread);
                Vector3 worldOffset = surfaceRotation * new Vector3(scorchOffset.x, scorchOffset.y, 0f);
                float size = bazookaBulletHoleSize * UnityEngine.Random.Range(0.12f, 0.28f);
                CreateBulletHole(hit, size, "BazookaScorchMark", worldOffset);
            }
        }

        private void CreateBulletHole(RaycastHit hit, float size, string objectName, Vector3 worldOffset = default)
        {
            var bulletHole = new GameObject(objectName);
            bulletHole.transform.position = hit.point + worldOffset + hit.normal * bulletHoleSurfaceOffset;
            bulletHole.transform.rotation = Quaternion.LookRotation(hit.normal);
            bulletHole.transform.localScale = Vector3.one * Mathf.Max(0.001f, size);
            bulletHole.transform.SetParent(hit.collider.transform, true);

            var meshFilter = bulletHole.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GetBulletHoleMesh();

            var meshRenderer = bulletHole.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetBulletHoleMaterial();

            Destroy(bulletHole, Mathf.Max(0.01f, bulletHoleLifetime));
            RegisterImpactMark(bulletHole);
        }

        private void CreateSpriteDecal(RaycastHit hit, float size, string objectName, Sprite sprite, float zRotation, Vector3 worldOffset = default)
        {
            if (sprite == null)
            {
                CreateBulletHole(hit, size, objectName, worldOffset);
                return;
            }

            var decal = new GameObject(objectName);
            decal.transform.position = hit.point + worldOffset + hit.normal * (bulletHoleSurfaceOffset * 2f);
            decal.transform.rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0f, 0f, zRotation);
            decal.transform.SetParent(hit.collider.transform, true);

            var spriteRenderer = decal.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 20;

            float spriteSize = Mathf.Max(Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y), 0.001f);
            decal.transform.localScale = Vector3.one * (Mathf.Max(0.001f, size) / spriteSize);

            Destroy(decal, Mathf.Max(0.01f, bulletHoleLifetime));
            RegisterImpactMark(decal);
        }

        private void RegisterImpactMark(GameObject mark)
        {
            if (mark == null)
            {
                return;
            }

            _impactMarks.Enqueue(mark);
            while (_impactMarks.Count > maxTemporaryImpactMarks)
            {
                var oldMark = _impactMarks.Dequeue();
                if (oldMark != null)
                {
                    Destroy(oldMark);
                }
            }
        }

        private void GetAimRay(out Vector3 rayOrigin, out Vector3 aimDirection)
        {
            if (_cachedFallbackCamera == null && Camera.main != null)
            {
                _cachedFallbackCamera = Camera.main.transform;
            }

            var aimTransform = aimCamera != null ? aimCamera : _cachedFallbackCamera != null ? _cachedFallbackCamera : transform;
            aimDirection = aimTransform.forward.normalized;
            rayOrigin = aimTransform.position + aimDirection * rayOriginForwardOffset;
        }

        private bool TryRaycastWeapon(WeaponType weaponType, Vector3 rayOrigin, Vector3 aimDirection, out RaycastHit hit)
        {
            return Physics.Raycast(rayOrigin, aimDirection, out hit, GetBulletHoleRange(weaponType), impactMask, QueryTriggerInteraction.Ignore);
        }

        private Mesh GetBulletHoleMesh()
        {
            if (_bulletHoleMesh != null)
            {
                return _bulletHoleMesh;
            }

            const int segments = 18;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            for (var i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f);
            }

            for (var i = 0; i < segments; i++)
            {
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i == segments - 1 ? 1 : i + 2;
            }

            _bulletHoleMesh = new Mesh
            {
                name = "Runtime Bullet Hole"
            };
            _bulletHoleMesh.vertices = vertices;
            _bulletHoleMesh.triangles = triangles;
            _bulletHoleMesh.RecalculateNormals();
            _bulletHoleMesh.RecalculateBounds();
            return _bulletHoleMesh;
        }

        private Material GetBulletHoleMaterial()
        {
            if (_bulletHoleMaterial != null)
            {
                return _bulletHoleMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");

            _bulletHoleMaterial = new Material(shader)
            {
                name = "Runtime Bullet Hole",
                color = bulletHoleColor
            };

            return _bulletHoleMaterial;
        }

        private void AutoBindDecals()
        {
            EnsureBazookaExplosionDecal();
        }

        private void EnsureBazookaExplosionDecal()
        {
#if UNITY_EDITOR
            if (IsUnityObjectMissing(bazookaExplosionDecal))
            {
                bazookaExplosionDecal = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/RunGun/Art/explosion_decal.png");
            }
#endif
        }

        private static bool IsUnityObjectMissing(UnityEngine.Object unityObject)
        {
            return unityObject == null;
        }

        private Vector3 CalculateShotgunImpulse(Vector3 aimDirection, bool hasHit, RaycastHit hit)
        {
            if (hasHit && hit.distance <= shotgunImpactDistance)
            {
                float distance01 = Mathf.Clamp01(hit.distance / Mathf.Max(0.01f, shotgunImpactDistance));
                float falloff = Mathf.Lerp(1f, 0.35f, distance01);
                Vector3 blastDirection = (hit.normal - aimDirection * 0.35f).normalized;
                return blastDirection * (shotgunImpulse * falloff);
            }

            return -aimDirection * shotgunMissImpulse;
        }

        private Vector3 CalculateBazookaImpulse(Vector3 aimDirection, bool hasHit, RaycastHit hit)
        {
            if (!hasHit)
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

            if (reloadAnimationControlsViewPose && _isReloading && HasReloadAnimation(weapon))
            {
                _shotBlend = Mathf.MoveTowards(_shotBlend, 0f, shotReturnSpeed * Time.deltaTime);
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

            if (_cachedFallbackCamera == null && Camera.main != null)
            {
                _cachedFallbackCamera = Camera.main.transform;
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

                if (weapon.muzzleTransform == null && weapon.viewModel != null)
                {
                    weapon.muzzleTransform = FindChildRecursive(weapon.viewModel.transform, "MuzzlePoint");
                }

                if (weapon.type == WeaponType.Bazooka && weapon.rocketVisual == null && weapon.viewModel != null)
                {
                    weapon.rocketVisual = FindChildRecursive(weapon.viewModel.transform, "Rocket");
                }

                EnsureRocketVisual(weapon);
                CacheViewModelPose(weapon.viewModel);
            }

            SetViewModelsActive();
        }

        private Animator EnsureWeaponAnimator(WeaponDefinition weapon)
        {
            if (weapon == null || weapon.viewModel == null)
            {
                return null;
            }

            if (weapon.viewModelAnimator == null)
            {
                weapon.viewModelAnimator = weapon.viewModel.GetComponent<Animator>();
            }

            if (weapon.viewModelAnimator == null)
            {
                weapon.viewModelAnimator = weapon.viewModel.AddComponent<Animator>();
            }

            if (weapon.reloadAnimatorController != null && weapon.viewModelAnimator.runtimeAnimatorController != weapon.reloadAnimatorController)
            {
                weapon.viewModelAnimator.runtimeAnimatorController = weapon.reloadAnimatorController;
            }

            weapon.viewModelAnimator.enabled = false;
            return weapon.viewModelAnimator;
        }

        private static bool HasReloadAnimation(WeaponDefinition weapon)
        {
            return weapon?.reloadAnimatorController != null && !string.IsNullOrWhiteSpace(weapon.reloadAnimationState);
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

        private bool TrySpawnMuzzlePointChildEffects(Transform muzzleTransform)
        {
            if (muzzleTransform == null || muzzleTransform.childCount == 0)
            {
                return false;
            }

            int templateCount = muzzleTransform.childCount;
            for (var i = 0; i < templateCount; i++)
            {
                var template = muzzleTransform.GetChild(i);
                if (template == null || IsRuntimeMuzzleEffect(template))
                {
                    continue;
                }

                var effect = Instantiate(template.gameObject, muzzleTransform);
                effect.name = $"{template.name}_Shot";
                effect.transform.localPosition = template.localPosition;
                effect.transform.localRotation = template.localRotation;
                effect.transform.localScale = template.localScale;
                effect.SetActive(true);

                PlayParticleSystems(effect);
                Destroy(effect, Mathf.Max(0.05f, muzzleFlashLifetime));
                return true;
            }

            return false;
        }

        private void SetMuzzlePointTemplatesActive(bool active)
        {
            if (weapons == null)
            {
                return;
            }

            for (var i = 0; i < weapons.Count; i++)
            {
                var muzzleTransform = weapons[i]?.muzzleTransform;
                if (muzzleTransform == null)
                {
                    continue;
                }

                for (var childIndex = 0; childIndex < muzzleTransform.childCount; childIndex++)
                {
                    var child = muzzleTransform.GetChild(childIndex);
                    if (child != null && !IsRuntimeMuzzleEffect(child))
                    {
                        child.gameObject.SetActive(active);
                    }
                }
            }
        }

        private static bool IsRuntimeMuzzleEffect(Transform effect)
        {
            return effect != null && effect.name.EndsWith("_Shot", StringComparison.Ordinal);
        }

        private static void PlayParticleSystems(GameObject effect)
        {
            var particleSystems = effect.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var particleSystem = particleSystems[i];
                particleSystem.gameObject.SetActive(true);
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }
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
                    reloadOneByOne = true,
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
