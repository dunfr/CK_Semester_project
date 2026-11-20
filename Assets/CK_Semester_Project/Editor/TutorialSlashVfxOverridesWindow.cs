using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace CK.SemesterProject.Editor
{
    internal sealed class TutorialSlashVfxOverridesWindow : EditorWindow
    {
        private static readonly PropertyName SlashReference = new PropertyName("SlashVfxObject");
        private static readonly PropertyName ImpactReference = new PropertyName("SlashImpactObject");

        [MenuItem("CK/Slash Follow VFX Overrides")]
        internal static void Open()
        {
            GetWindow<TutorialSlashVfxOverridesWindow>("Slash Follow VFX");
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            var director = TutorialSlashTimelineEditor.PreviewDirector;
            if (director != null)
            {
                RefreshPreview(director);
            }
            Repaint();
        }

        private void OnGUI()
        {
            var director = TutorialSlashTimelineEditor.PreviewDirector;
            Transform slash = GetEffect(director, SlashReference);
            Transform impact = GetEffect(director, ImpactReference);
            if (slash == null || impact == null)
            {
                EditorGUILayout.HelpBox("CK > Slash Follow Timeline을 열고 두 이펙트의 바인딩을 확인하세요.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "이펙트의 로컬 위치, 회전, 크기를 편집합니다. 슬래시 값은 Slash VFX Transform 트랙의 키 위에 적용됩니다.",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(director.state == PlayState.Playing))
            {
                bool changed = DrawTransform(slash);
                changed |= DrawTransform(impact);
                if (changed)
                {
                    RefreshPreview(director);
                }
                if (GUILayout.Button("Apply VFX to Timeline Prefab"))
                {
                    if (ApplyToPrefab(slash, impact, TutorialSlashTimelineEditor.PrefabPath))
                    {
                        ShowNotification(new GUIContent("PF_Battle_SlashFollow에 저장했습니다."));
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Slash Follow VFX", "프리팹을 저장하지 못했습니다. 프리팹과 이펙트 바인딩을 확인하세요.", "확인");
                    }
                }
            }
        }

        private static Transform GetEffect(PlayableDirector director, PropertyName reference)
        {
            if (director == null)
            {
                return null;
            }
            var effect = director.GetReferenceValue(reference, out bool valid) as GameObject;
            return valid && effect != null && effect.transform.IsChildOf(director.transform)
                ? effect.transform : null;
        }

        private static bool DrawTransform(Transform effect)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(effect.name, EditorStyles.boldLabel);
                if (GUILayout.Button("Select", GUILayout.ExpandWidth(false)))
                {
                    Selection.activeGameObject = effect.gameObject;
                }
            }
            EditorGUI.BeginChangeCheck();
            Vector3 position = EditorGUILayout.Vector3Field("Local Position", effect.localPosition);
            Vector3 rotation = EditorGUILayout.Vector3Field("Local Rotation", effect.localEulerAngles);
            Vector3 scale = EditorGUILayout.Vector3Field("Local Scale", effect.localScale);
            if (!EditorGUI.EndChangeCheck())
            {
                return false;
            }

            Undo.RecordObject(effect, "Edit Slash Follow VFX");
            effect.SetLocalPositionAndRotation(position,
                rotation == effect.localEulerAngles ? effect.localRotation : Quaternion.Euler(rotation));
            effect.localScale = scale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
            return true;
        }

        private static void RefreshPreview(PlayableDirector director)
        {
            if (director.state == PlayState.Playing)
            {
                return;
            }
            // World-space particles need to be emitted again at the edited transform.
            foreach (var particle in director.GetComponentsInChildren<ParticleSystem>())
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            director.Evaluate();
            SceneView.RepaintAll();
        }

        internal static bool ApplyToPrefab(Transform slash, Transform impact, string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                return false;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var director = contents.GetComponent<PlayableDirector>();
                Transform prefabSlash = GetEffect(director, SlashReference);
                Transform prefabImpact = GetEffect(director, ImpactReference);
                if (prefabSlash == null || prefabImpact == null)
                {
                    return false;
                }

                CopyTransform(slash, prefabSlash);
                CopyTransform(impact, prefabImpact);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath, out bool saved);
                return saved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void CopyTransform(Transform source, Transform target)
        {
            target.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            target.localScale = source.localScale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
