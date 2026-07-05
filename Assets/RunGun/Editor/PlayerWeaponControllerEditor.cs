using RunGun.Weapons;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerWeaponController), true)]
[CanEditMultipleObjects]
public sealed class PlayerWeaponControllerEditor : Editor
{
    private static readonly string[] Weapons =
    {
        "weapons",
        "startWeaponIndex"
    };

    private static readonly string[] References =
    {
        "gunPlace",
        "aimCamera",
        "movementController",
        "shotAudioSource",
        "reloadAudioSource"
    };

    private static readonly string[] ViewAlignment =
    {
        "alignGunPlaceToCamera",
        "followCameraPosition",
        "gunPlaceCameraOffset",
        "rayOriginForwardOffset",
        "impactMask"
    };

    private static readonly string[] AudioAndEffects =
    {
        "autoBindAudioClips",
        "autoBindWarFxPrefabs",
        "useMuzzlePointChildEffects",
        "muzzleFlashLifetime",
        "impactEffectLifetime",
        "pistolImpactEffectScale",
        "rifleImpactEffectScale",
        "shotgunImpactEffectScale",
        "shotgunImpactEffectCount",
        "shotgunImpactEffectSpread"
    };

    private static readonly string[] MovementImpulses =
    {
        "pistolImpulse",
        "rifleImpulse",
        "shotgunImpulse",
        "shotgunMissImpulse",
        "shotgunImpactDistance",
        "bazookaImpulse",
        "bazookaMissImpulse",
        "bazookaImpactDistance",
        "bazookaRadius",
        "bazookaRocketSpeed",
        "bazookaRocketMaxFlightTime",
        "pistolAirRedirectStrength",
        "rifleAirRedirectStrength",
        "shotgunAirRedirectStrength",
        "bazookaAirRedirectStrength"
    };

    private static readonly string[] ImpactMarks =
    {
        "spawnBulletHoles",
        "maxTemporaryImpactMarks",
        "bulletHoleLifetime",
        "bulletHoleSurfaceOffset",
        "bulletHoleColor",
        "pistolBulletHoleSize",
        "rifleBulletHoleSize",
        "shotgunPelletHoleSize",
        "shotgunPelletMarks",
        "shotgunPelletSpread",
        "bazookaBulletHoleSize",
        "bazookaExplosionDecal",
        "bazookaScorchMarks",
        "bazookaScorchSpread"
    };

    private static readonly string[] ViewModelAnimation =
    {
        "switchPositionOffset",
        "switchDuration",
        "shotPositionOffset",
        "shotRotationOffset",
        "shotKick",
        "shotReturnSpeed",
        "reloadAnimationTransitionDuration",
        "reloadAnimationControlsViewPose"
    };

    private static readonly string[] DebugOptions =
    {
        "logWeaponFire",
        "logWeaponImpulses"
    };

    private bool _showWeapons = true;
    private bool _showReferences = true;
    private bool _showView = true;
    private bool _showEffects = true;
    private bool _showMovement = true;
    private bool _showImpactMarks;
    private bool _showAnimation;
    private bool _showDebug;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptField();
        DrawSection("Weapons", ref _showWeapons, Weapons);
        DrawSection("References", ref _showReferences, References);
        DrawSection("View Alignment", ref _showView, ViewAlignment);
        DrawSection("Audio & Effects", ref _showEffects, AudioAndEffects);
        DrawSection("Movement Impulses", ref _showMovement, MovementImpulses);
        DrawSection("Impact Marks", ref _showImpactMarks, ImpactMarks);
        DrawSection("View Model Animation", ref _showAnimation, ViewModelAnimation);
        DrawSection("Debug", ref _showDebug, DebugOptions);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawScriptField()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }
    }

    private void DrawSection(string title, ref bool foldout, string[] propertyNames)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, title);
        EditorGUILayout.EndFoldoutHeaderGroup();

        if (!foldout)
        {
            EditorGUILayout.Space(2f);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.indentLevel++;
            DrawProperties(propertyNames);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4f);
    }

    private void DrawProperties(string[] propertyNames)
    {
        for (var i = 0; i < propertyNames.Length; i++)
        {
            var property = serializedObject.FindProperty(propertyNames[i]);
            if (property == null)
            {
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }
    }
}

[CustomPropertyDrawer(typeof(PlayerWeaponController.WeaponDefinition))]
public sealed class WeaponDefinitionDrawer : PropertyDrawer
{
    private const float LineHeight = 18f;
    private const float LineGap = 2f;
    private const float SectionGap = 6f;

    private static readonly string[] Core =
    {
        "type",
        "displayName",
        "unlocked",
        "magazineSize",
        "ammoInMagazine",
        "reloadTime",
        "reloadOneByOne",
        "fireInterval",
        "impactPower"
    };

    private static readonly string[] ViewAndEffects =
    {
        "viewModel",
        "viewModelAnimator",
        "reloadAnimatorController",
        "reloadAnimationState",
        "rocketVisual",
        "muzzleTransform",
        "muzzleFlashPrefab",
        "impactEffectPrefab"
    };

    private static readonly string[] Audio =
    {
        "shotClip",
        "shotVolume",
        "shotPitch",
        "reloadClip",
        "reloadVolume",
        "reloadPitch"
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var rect = new Rect(position.x, position.y, position.width, LineHeight);
        property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, GetTitle(property), true);
        if (!property.isExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;
        rect.y += LineHeight + LineGap;
        DrawProperties(ref rect, property, Core);
        DrawLabel(ref rect, "View Model & Effects");
        DrawProperties(ref rect, property, ViewAndEffects);
        DrawLabel(ref rect, "Audio");
        DrawProperties(ref rect, property, Audio);
        EditorGUI.indentLevel--;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
        {
            return LineHeight + LineGap;
        }

        int fieldCount = Core.Length + ViewAndEffects.Length + Audio.Length;
        const int labelCount = 2;
        return LineHeight + LineGap + ((fieldCount + labelCount) * (LineHeight + LineGap)) + (SectionGap * 2f);
    }

    private static GUIContent GetTitle(SerializedProperty property)
    {
        var displayName = property.FindPropertyRelative("displayName");
        var type = property.FindPropertyRelative("type");
        string typeName = type.enumDisplayNames[type.enumValueIndex];
        string title = string.IsNullOrWhiteSpace(displayName.stringValue)
            ? typeName
            : $"{displayName.stringValue} ({typeName})";

        return new GUIContent(title);
    }

    private static void DrawLabel(ref Rect rect, string text)
    {
        rect.y += SectionGap;
        EditorGUI.LabelField(rect, text, EditorStyles.boldLabel);
        rect.y += LineHeight + LineGap;
    }

    private static void DrawProperties(ref Rect rect, SerializedProperty property, string[] propertyNames)
    {
        for (var i = 0; i < propertyNames.Length; i++)
        {
            DrawProperty(ref rect, property, propertyNames[i]);
        }
    }

    private static void DrawProperty(ref Rect rect, SerializedProperty property, string relativeName)
    {
        var child = property.FindPropertyRelative(relativeName);
        if (child == null)
        {
            return;
        }

        EditorGUI.PropertyField(rect, child);
        rect.y += EditorGUI.GetPropertyHeight(child) + LineGap;
    }
}
