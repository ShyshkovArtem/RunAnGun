using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RunGun.Levels
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/Levels/Menu VFX Snapshot Controller")]
    public sealed class MenuVfxSnapshotController : MonoBehaviour
    {
        [Serializable]
        private sealed class Snapshot
        {
            [SerializeField] private string effectName;
            [Min(0f)] [SerializeField] private float snapshotTime = 0.35f;
            [SerializeField] private uint randomSeed = 1;

            public string EffectName => effectName;
            public float SnapshotTime => snapshotTime;
            public uint RandomSeed => randomSeed;
        }

        [SerializeField] private bool previewInEditMode = true;
        [SerializeField] private List<Snapshot> effects = new();

        private bool _previewQueued;

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                ApplySnapshots();
                return;
            }

            QueueEditorPreview();
        }

        private void OnValidate()
        {
            QueueEditorPreview();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall -= ApplyQueuedEditorPreview;
            _previewQueued = false;
#endif
        }

        [ContextMenu("Apply VFX Snapshots")]
        public void ApplySnapshots()
        {
            Scene scene = gameObject.scene;
            if (!scene.IsValid())
                return;

            for (int i = 0; i < effects.Count; i++)
                ApplySnapshot(scene, effects[i]);
        }

        private static void ApplySnapshot(Scene scene, Snapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.EffectName))
                return;

            Transform effectRoot = FindSceneTransform(scene, snapshot.EffectName);
            if (effectRoot == null)
                return;

            ParticleSystem[] particleSystems = effectRoot.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particles = particleSystems[i];
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.useAutoRandomSeed = false;
                particles.randomSeed = snapshot.RandomSeed + (uint)i;
                particles.Simulate(snapshot.SnapshotTime, false, true, true);
                particles.Pause(false);
            }
        }

        private static Transform FindSceneTransform(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindChildRecursive(roots[i].transform, objectName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string objectName)
        {
            if (root.name == objectName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), objectName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void QueueEditorPreview()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || !previewInEditMode || _previewQueued)
                return;

            _previewQueued = true;
            EditorApplication.delayCall += ApplyQueuedEditorPreview;
#endif
        }

#if UNITY_EDITOR
        private void ApplyQueuedEditorPreview()
        {
            EditorApplication.delayCall -= ApplyQueuedEditorPreview;
            _previewQueued = false;

            if (this != null && isActiveAndEnabled && previewInEditMode)
                ApplySnapshots();
        }
#endif
    }
}
