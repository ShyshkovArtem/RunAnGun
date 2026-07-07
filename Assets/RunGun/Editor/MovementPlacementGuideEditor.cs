using ElmanGameDevTools.PlayerSystem;
using RunGun.Levels;
using UnityEditor;
using UnityEngine;

namespace RunGun.Editor
{
    [CustomEditor(typeof(MovementPlacementGuide))]
    public sealed class MovementPlacementGuideEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var guide = (MovementPlacementGuide)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Calculated Reach", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Preset", guide.Preset.ToString());
            EditorGUILayout.LabelField("Speed", $"{guide.HorizontalSpeed:0.00} m/s");
            EditorGUILayout.LabelField("Flight Time", $"{guide.FlightTime:0.00} s");
            EditorGUILayout.LabelField("Horizontal Distance", $"{guide.GetHorizontalDistance():0.00} m");

            if (guide.HasTarget)
            {
                string targetState = guide.TargetIsReachable ? "Reachable" : "Missed";
                EditorGUILayout.LabelField("Target", targetState);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Load Values From Player In Scene"))
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    Undo.RecordObject(guide, "Load Movement Guide Values");
                    guide.LoadFromPlayer(player);
                    EditorUtility.SetDirty(guide);
                }
                else
                {
                    Debug.LogWarning("No PlayerController found in the scene.");
                }
            }

            if (GUILayout.Button("Create Landing Marker"))
            {
                CreateLandingMarker(guide);
            }
        }

        private void OnSceneGUI()
        {
            var guide = (MovementPlacementGuide)target;
            Vector3 landing = guide.LandingPoint;

            Handles.color = guide.HasTarget && !guide.TargetIsReachable ? Color.red : Color.green;
            Handles.Label(
                landing + Vector3.up * 0.35f,
                $"{guide.Preset}\n{guide.GetHorizontalDistance():0.0}m / {guide.FlightTime:0.00}s");
        }

        [MenuItem("GameObject/RunGun/Movement Placement Guide", false, 20)]
        private static void CreateGuide(MenuCommand command)
        {
            var gameObject = new GameObject("Movement Placement Guide");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Movement Placement Guide");

            if (command.context is GameObject parent)
                GameObjectUtility.SetParentAndAlign(gameObject, parent);

            gameObject.AddComponent<MovementPlacementGuide>();
            Selection.activeGameObject = gameObject;
        }

        private static void CreateLandingMarker(MovementPlacementGuide guide)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"{guide.Preset} Landing Marker";
            marker.transform.position = guide.LandingPoint;
            marker.transform.localScale = Vector3.one * 0.35f;

            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
                Object.DestroyImmediate(markerCollider);

            Undo.RegisterCreatedObjectUndo(marker, "Create Landing Marker");
            Selection.activeGameObject = marker;
        }
    }
}
