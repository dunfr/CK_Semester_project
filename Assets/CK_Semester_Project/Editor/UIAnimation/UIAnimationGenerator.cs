using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation.EditorTools
{
    // 이름.controller와 이름_Open/Stay/Close.anim을 만들고 Image의 Animator에 연결한다.
    public static class UIAnimationGenerator
    {
        public const string PresetFolder = "Assets/UI/Animation/Presets";

        private const float BlendSeconds = 0.1f;
        private const string DefaultWord = "Default";
        private const string FallbackName = "UI";
        private const string EventSystemMenu = "GameObject/UI (Canvas)/Event System";

        private static readonly UIAnimationState[] States =
        {
            UIAnimationState.Open,
            UIAnimationState.Stay,
            UIAnimationState.Close
        };

        private static readonly string[] HighlightWords = { "Selected", "Highlight", "Highlighted", "Hover" };

        public static string ControllerPath(string folder, string name)
        {
            return $"{folder}/{name}.controller";
        }

        public static string ClipPath(string folder, string name, UIAnimationState state)
        {
            return $"{folder}/{name}_{state}.anim";
        }

        public static IEnumerable<string> AssetPaths(string folder, string name)
        {
            yield return ControllerPath(folder, name);
            foreach (UIAnimationState state in States)
            {
                yield return ClipPath(folder, name, state);
            }
        }

        public static string SuggestName(Image image)
        {
            string name = CleanName(image.gameObject.name);
            return name.Length > 0 ? name : FallbackName;
        }

        // 대상 이름의 폴더에 모은다. 부모에 애니메이션이 있으면 가장 위 부모의 폴더에 함께 넣는다.
        public static string FolderFor(Image image, string root)
        {
            return ParentFolder(image) ?? $"{root}/{SuggestName(image)}";
        }

        // 오브젝트 이름이 같아도 애니메이션이 섞이지 않게 번호를 붙인다. 부모 폴더 안에서는 파일만, 그 밖에서는 폴더까지 나눈다.
        public static (string Folder, string Name) FilesFor(Image image, string root)
        {
            string name = SuggestName(image);
            string parent = ParentFolder(image);
            if (parent != null)
            {
                return (parent, UniqueName(parent, name));
            }

            string unique = name;
            for (int number = 2; !IsNameFree($"{root}/{unique}", unique); number++)
            {
                unique = $"{name}_{number}";
            }
            return ($"{root}/{unique}", unique);
        }

        // 버튼처럼 조작하는 Image만 마우스 반응이 기본으로 켜진다.
        public static UIPointerEffectMask SuggestEffects(Image image)
        {
            return image.GetComponent<Selectable>() != null ? UIPointerEffectMask.All : UIPointerEffectMask.None;
        }

        // 버튼에 넣어 둔 하이라이트 이미지를 먼저 쓰고, 없으면 같은 폴더에서 이름이 짝인 이미지를 찾는다.
        public static Sprite SuggestHighlight(Image image)
        {
            Selectable selectable = image.GetComponent<Selectable>();
            Sprite assigned = selectable != null ? selectable.spriteState.highlightedSprite : null;
            return assigned != null ? assigned : FindPairedSprite(image.sprite);
        }

        public static string CleanName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string((name ?? string.Empty).Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        }

        public static bool IsNameFree(string folder, string name)
        {
            return !AssetPaths(folder, name).Any(File.Exists);
        }

        public static string UniqueName(string folder, string name)
        {
            string candidate = name;
            for (int number = 2; !IsNameFree(folder, candidate); number++)
            {
                candidate = $"{name}_{number}";
            }
            return candidate;
        }

        public static UIAnimationPreset FindPreset(string folder, string name)
        {
            return PresetAt(ControllerPath(folder, name));
        }

        // Project 창에 안 보이게 숨긴 하위 에셋이라 LoadAssetAtPath와 FindAssets로는 찾을 수 없다.
        public static UIAnimationPreset PresetAt(string controllerPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(controllerPath).OfType<UIAnimationPreset>().FirstOrDefault();
        }

        public static AnimatorController ControllerOf(UIAnimationPreset preset)
        {
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(preset));
        }

        public static AnimationClip ClipOf(UIAnimationPreset preset, UIAnimationState state)
        {
            AnimatorState found = FindState(ControllerOf(preset), state);
            return found != null ? found.motion as AnimationClip : null;
        }

        public static UIAnimationPreset Create(
            Image image, string folder, string name, UIPointerEffectMask pointerEffects, System.Action<UIAnimationPreset> setup = null)
        {
            EnsureFolder(folder);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath(folder, name));
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = UIAnimationPreset.IsOpenParameter,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = true
            });

            UIAnimationPreset preset = ScriptableObject.CreateInstance<UIAnimationPreset>();
            preset.name = name;
            preset.hideFlags = HideFlags.HideInHierarchy;
            preset.Capture(image, HasChildGraphics(image), pointerEffects);
            setup?.Invoke(preset);
            FillHighlightSprites(preset, image);
            AssetDatabase.AddObjectToAsset(preset, controller);

            BuildGraph(controller, folder, name);
            Rebuild(preset);
            Save(preset);
            Connect(image, preset);
            return preset;
        }

        // 파일 이름과 폴더는 대상에서 정하고, 먼저 만든 자식 파일도 이 폴더로 모은다.
        public static UIAnimationPreset CreateFor(
            Image image, string root, UIPointerEffectMask pointerEffects, System.Action<UIAnimationPreset> setup = null)
        {
            (string folder, string name) = FilesFor(image, root);
            UIAnimationPreset preset = Create(image, folder, name, pointerEffects, setup);
            GatherChildren(image, folder);
            return preset;
        }

        // 함께 쓰던 값을 그대로 복사해 이 Image만의 파일을 만든다.
        public static UIAnimationPreset Separate(Image image, UIAnimationPreset shared, string root)
        {
            return CreateFor(image, root, UIPointerEffectMask.None, copy => copy.CopyValuesFrom(shared));
        }

        // 컴포넌트가 지워졌거나 Controller가 바뀌었으면 설정대로 되돌린다.
        public static void Repair(Image image, UIAnimationPreset preset)
        {
            Animator animator = image.GetComponent<Animator>();
            UIAnimationPointerEffects effects = image.GetComponent<UIAnimationPointerEffects>();
            bool connected = animator != null
                && animator.runtimeAnimatorController == ControllerOf(preset)
                && (effects != null ? preset.HasPointerEffects || effects.HasTriggers : !preset.HasPointerEffects);
            if (!connected && !Application.isPlaying)
            {
                Connect(image, preset);
            }
        }

        // 메뉴로 만들어야 프로젝트의 입력 방식에 맞는 모듈이 붙는다. 메뉴가 고른 오브젝트는 원래대로 되돌린다.
        public static void EnsureEventSystem(UIAnimationPreset preset)
        {
            if (!preset.HasPointerEffects || Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            {
                return;
            }
            Object selected = Selection.activeObject;
            Selection.activeObject = null;
            EditorApplication.ExecuteMenuItem(EventSystemMenu);
            Selection.activeObject = selected;
        }

        public static void Connect(Image image, UIAnimationPreset preset)
        {
            GameObject target = image.gameObject;
            UIAnimationPlayer player = GetOrAdd<UIAnimationPlayer>(target);
            Animator animator = GetOrAdd<Animator>(target);
            Undo.RecordObject(animator, "UI 애니메이션 연결");
            animator.runtimeAnimatorController = ControllerOf(preset);
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.applyRootMotion = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);

            var serializedPlayer = new SerializedObject(player);
            serializedPlayer.FindProperty("_preset").objectReferenceValue = preset;
            serializedPlayer.ApplyModifiedProperties();
            ApplyOptions(target, preset);
        }

        // 저장한 프리셋은 Controller와 떨어진 독립 에셋이라 다른 Image에 불러올 수 있다.
        public static void SaveAsPreset(UIAnimationPreset preset, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            UIAnimationPreset copy = Object.Instantiate(preset);
            copy.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssetIfDirty(copy);
        }

        public static List<UIAnimationPreset> SavedPresets()
        {
            return AssetDatabase.FindAssets("t:UIAnimationPreset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<UIAnimationPreset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(preset => preset != null)
                .ToList();
        }

        public static List<UIAnimationPlayer> Users(UIAnimationPreset preset)
        {
            return Object.FindObjectsByType<UIAnimationPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(player => player.Preset == preset)
                .ToList();
        }

        public static List<UIAnimationPlayer> ChildPlayers(UIAnimationPlayer parent)
        {
            var players = new List<UIAnimationPlayer>();
            foreach (Transform child in parent.transform)
            {
                if (child.TryGetComponent(out UIAnimationPlayer player))
                {
                    players.Add(player);
                }
            }
            return players;
        }

        // 자식이 차례로 나오게 한다. 닫을 때는 반대 순서로 들어가고, 부모는 자식이 다 들어간 뒤 닫힌다.
        public static void ApplyStagger(UIAnimationPlayer parent, float step, bool reverse)
        {
            List<UIAnimationPlayer> children = ChildPlayers(parent);
            if (reverse)
            {
                children.Reverse();
            }

            float closeEnd = 0f;
            for (int index = 0; index < children.Count; index++)
            {
                float closeDelay = step * (children.Count - 1 - index);
                SetDelays(children[index], step * index, closeDelay);
                if (children[index].Preset != null)
                {
                    closeEnd = Mathf.Max(closeEnd, closeDelay + children[index].Preset.Get(UIAnimationState.Close).Duration);
                }
            }
            SetDelays(parent, parent.OpenDelay, closeEnd);
            SetFollowers(parent, children);
        }

        public static void ClearStagger(UIAnimationPlayer parent)
        {
            foreach (UIAnimationPlayer child in parent.Followers.Where(child => child != null))
            {
                SetDelays(child, 0f, 0f);
            }
            SetDelays(parent, parent.OpenDelay, 0f);
            SetFollowers(parent, new List<UIAnimationPlayer>());
        }

        public static void ShareWithChildren(UIAnimationPlayer parent, UIAnimationPreset preset)
        {
            foreach (Transform child in parent.transform)
            {
                if (child.TryGetComponent(out Image image) && !child.TryGetComponent(out UIAnimationPlayer _))
                {
                    Connect(image, preset);
                }
            }
        }

        public static List<Button> ButtonsCalling(UIAnimationPlayer player, string method)
        {
            return Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(button => FindCall(button, player, method) >= 0)
                .ToList();
        }

        public static void ConnectButton(Button button, UIAnimationPlayer player, string method)
        {
            if (FindCall(button, player, method) >= 0)
            {
                return;
            }
            Undo.RecordObject(button, "UI 애니메이션 버튼 연결");
            UnityAction action = method == nameof(UIAnimationPlayer.Close) ? player.Close : (UnityAction)player.Open;
            UnityEventTools.AddVoidPersistentListener(button.onClick, action);
            EditorUtility.SetDirty(button);
        }

        public static void DisconnectButton(Button button, UIAnimationPlayer player, string method)
        {
            int index = FindCall(button, player, method);
            if (index >= 0)
            {
                Undo.RecordObject(button, "UI 애니메이션 버튼 연결 해제");
                UnityEventTools.RemovePersistentListener(button.onClick, index);
                EditorUtility.SetDirty(button);
            }
        }

        // 화면에 보이는 자리는 그대로 두고 기준점만 옮긴다.
        public static void SetPivot(RectTransform rect, Vector2 pivot)
        {
            Undo.RecordObject(rect, "기준점 변경");
            Vector2 change = pivot - rect.pivot;
            Vector2 size = Vector2.Scale(rect.rect.size, rect.localScale);
            rect.pivot = pivot;
            rect.anchoredPosition += Vector2.Scale(change, size);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }

        // 프리셋을 함께 쓰는 Image까지 옵션에 맞는 컴포넌트를 붙이거나 뗀다.
        public static void SyncComponents(UIAnimationPreset preset)
        {
            foreach (UIAnimationPlayer player in Users(preset))
            {
                ApplyOptions(player.gameObject, preset);
            }
        }

        // 프리셋과 구운 결과가 Undo 한 번에 같이 돌아가도록, Rebuild 전에 함께 기록한다.
        public static void RecordUndo(UIAnimationPreset preset)
        {
            AnimatorController controller = ControllerOf(preset);
            var recorded = new List<Object>();
            foreach (UIAnimationState state in States)
            {
                AnimatorState animatorState = FindState(controller, state);
                if (animatorState == null)
                {
                    continue;
                }
                recorded.Add(animatorState);
                recorded.AddRange(animatorState.transitions);
                if (animatorState.motion != null)
                {
                    recorded.Add(animatorState.motion);
                }
            }
            Undo.RegisterCompleteObjectUndo(recorded.ToArray(), "UI 애니메이션 수정");
        }

        public static void Rebuild(UIAnimationPreset preset)
        {
            AnimatorController controller = ControllerOf(preset);
            if (controller == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(controller);
            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(path);
            foreach (UIAnimationState state in States)
            {
                AnimatorState animatorState = FindState(controller, state);
                if (animatorState == null)
                {
                    Debug.LogWarning($"[UI 애니메이션] {path}에 {state} 상태가 없어 건너뜁니다.", controller);
                    continue;
                }
                if (!(animatorState.motion is AnimationClip clip))
                {
                    clip = CreateClip(folder, name, state);
                    animatorState.motion = clip;
                }
                UIAnimationClipBaker.Bake(preset, state, clip);
                animatorState.speed = preset.Get(state).Speed;
                EditorUtility.SetDirty(clip);
                EditorUtility.SetDirty(animatorState);
            }

            AnimatorState stay = FindState(controller, UIAnimationState.Stay);
            if (stay != null)
            {
                foreach (AnimatorStateTransition transition in stay.transitions)
                {
                    transition.duration = preset.IsStayStatic ? 0f : BlendSeconds;
                    EditorUtility.SetDirty(transition);
                }
            }
            EditorUtility.SetDirty(controller);
        }

        public static void Save(UIAnimationPreset preset)
        {
            AnimatorController controller = ControllerOf(preset);
            if (controller == null)
            {
                return;
            }
            AssetDatabase.SaveAssetIfDirty(controller);
            foreach (UIAnimationState state in States)
            {
                AnimationClip clip = ClipOf(preset, state);
                if (clip != null)
                {
                    AssetDatabase.SaveAssetIfDirty(clip);
                }
            }
        }

        // 저장 버튼 하나로 파일과 씬을 함께 저장한다. 프리팹 파일과 프리팹 모드는 씬으로 저장할 수 없어 파일만 저장한다.
        public static bool SaveAll(UIAnimationPreset preset, Scene scene)
        {
            Save(preset);
            return !scene.IsValid() || EditorSceneManager.IsPreviewScene(scene) || EditorSceneManager.SaveScene(scene);
        }

        private static void BuildGraph(AnimatorController controller, string folder, string name)
        {
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState open = AddState(machine, UIAnimationState.Open, new Vector3(300f, 120f), folder, name);
            AnimatorState stay = AddState(machine, UIAnimationState.Stay, new Vector3(560f, 120f), folder, name);
            AnimatorState close = AddState(machine, UIAnimationState.Close, new Vector3(560f, 260f), folder, name);
            machine.defaultState = open;

            AnimatorStateTransition finished = open.AddTransition(stay);
            finished.hasExitTime = true;
            finished.exitTime = 1f;
            finished.hasFixedDuration = true;
            finished.duration = 0f;

            AddSwitch(open, close, AnimatorConditionMode.IfNot);
            AddSwitch(stay, close, AnimatorConditionMode.IfNot);
            AddSwitch(close, open, AnimatorConditionMode.If);
        }

        private static AnimatorState AddState(
            AnimatorStateMachine machine, UIAnimationState state, Vector3 position, string folder, string name)
        {
            AnimatorState added = machine.AddState(state.ToString(), position);
            added.writeDefaultValues = false;
            added.motion = CreateClip(folder, name, state);
            return added;
        }

        private static AnimationClip CreateClip(string folder, string name, UIAnimationState state)
        {
            var clip = new AnimationClip { name = $"{name}_{state}" };
            AssetDatabase.CreateAsset(clip, ClipPath(folder, name, state));
            return clip;
        }

        private static void AddSwitch(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = BlendSeconds;
            transition.AddCondition(mode, 0f, UIAnimationPreset.IsOpenParameter);
        }

        private static AnimatorState FindState(AnimatorController controller, UIAnimationState state)
        {
            if (controller == null)
            {
                return null;
            }
            string stateName = state.ToString();
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                if (child.state.name == stateName)
                {
                    return child.state;
                }
            }
            return null;
        }

        private static void ApplyOptions(GameObject target, UIAnimationPreset preset)
        {
            if (preset.FadeChildren)
            {
                GetOrAdd<CanvasGroup>(target);
            }
            if (preset.HasPointerEffects)
            {
                GetOrAdd<UIAnimationPointerEffects>(target);
                PrepareInput(target);
            }
            else if (target.TryGetComponent(out UIAnimationPointerEffects effects) && !effects.HasTriggers)
            {
                // 연결한 트리거는 다시 켜도 되살릴 수 없으니 남아 있으면 지우지 않는다.
                Undo.DestroyObjectImmediate(effects);
            }
            if (target.TryGetComponent(out Image image))
            {
                ApplyWipe(image, preset);
            }
        }

        // 마우스 반응이 동작하도록 맞춘다. Button의 Transition은 같은 Image를 바꿔 반응과 겹치므로 끈다.
        private static void PrepareInput(GameObject target)
        {
            if (!target.TryGetComponent(out Image image))
            {
                return;
            }
            if (!image.raycastTarget)
            {
                Undo.RecordObject(image, "Raycast Target 켜기");
                image.raycastTarget = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            }
            Canvas canvas = image.canvas;
            if (canvas != null && !canvas.rootCanvas.TryGetComponent(out GraphicRaycaster _))
            {
                Undo.AddComponent<GraphicRaycaster>(canvas.rootCanvas.gameObject);
            }
            if (target.TryGetComponent(out Selectable selectable) && selectable.targetGraphic == image
                && (selectable.transition == Selectable.Transition.ColorTint || selectable.transition == Selectable.Transition.SpriteSwap))
            {
                Undo.RecordObject(selectable, "Transition 끄기");
                selectable.transition = Selectable.Transition.None;
                PrefabUtility.RecordPrefabInstancePropertyModifications(selectable);
            }
        }

        // 와이프는 Fill 방식 Image에서만 보이므로 쓸 때만 바꾸고, 끄면 원래 방식으로 되돌린다.
        private static void ApplyWipe(Image image, UIAnimationPreset preset)
        {
            UIWipeDirection direction = preset.WipeDirection;
            bool horizontal = direction == UIWipeDirection.LeftToRight || direction == UIWipeDirection.RightToLeft;
            Image.Type type = preset.AnimatesWipe ? Image.Type.Filled : preset.ImageType;
            Image.FillMethod method = horizontal ? Image.FillMethod.Horizontal : Image.FillMethod.Vertical;
            int origin = direction == UIWipeDirection.RightToLeft || direction == UIWipeDirection.TopToBottom ? 1 : 0;
            if (image.type == type && (!preset.AnimatesWipe || (image.fillMethod == method && image.fillOrigin == origin)))
            {
                return;
            }

            Undo.RecordObject(image, "와이프 설정");
            image.type = type;
            if (preset.AnimatesWipe)
            {
                image.fillMethod = method;
                image.fillOrigin = origin;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        }

        private static void SetDelays(UIAnimationPlayer player, float openDelay, float closeDelay)
        {
            var serialized = new SerializedObject(player);
            serialized.FindProperty("_openDelay").floatValue = openDelay;
            serialized.FindProperty("_closeDelay").floatValue = closeDelay;
            serialized.ApplyModifiedProperties();
        }

        private static void SetFollowers(UIAnimationPlayer parent, List<UIAnimationPlayer> followers)
        {
            var serialized = new SerializedObject(parent);
            SerializedProperty list = serialized.FindProperty("_followers");
            list.arraySize = followers.Count;
            for (int index = 0; index < followers.Count; index++)
            {
                list.GetArrayElementAtIndex(index).objectReferenceValue = followers[index];
            }
            serialized.ApplyModifiedProperties();
        }

        private static int FindCall(Button button, UIAnimationPlayer player, string method)
        {
            for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
            {
                if (button.onClick.GetPersistentTarget(index) == player && button.onClick.GetPersistentMethodName(index) == method)
                {
                    return index;
                }
            }
            return -1;
        }

        private static string ParentFolder(Image image)
        {
            string folder = null;
            for (Transform parent = image.transform.parent; parent != null; parent = parent.parent)
            {
                string path = parent.TryGetComponent(out UIAnimationPlayer player) && player.Preset != null
                    ? AssetDatabase.GetAssetPath(player.Preset)
                    : string.Empty;
                if (path.Length > 0)
                {
                    folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
                }
            }
            return folder;
        }

        // 옮겨도 GUID가 그대로라 연결은 끊기지 않는다. 다른 곳에서 함께 쓰는 파일은 그 자리에 둔다.
        private static void GatherChildren(Image image, string folder)
        {
            List<UIAnimationPreset> presets = image.GetComponentsInChildren<UIAnimationPlayer>(true)
                .Select(player => player.Preset)
                .Where(preset => preset != null)
                .Distinct()
                .ToList();
            foreach (UIAnimationPreset preset in presets)
            {
                string path = AssetDatabase.GetAssetPath(preset);
                string from = Path.GetDirectoryName(path)?.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(path);
                if (from == folder || !IsNameFree(folder, name)
                    || !Users(preset).All(player => player.transform.IsChildOf(image.transform)))
                {
                    continue;
                }

                foreach (string asset in AssetPaths(from, name))
                {
                    AssetDatabase.MoveAsset(asset, $"{folder}/{Path.GetFileName(asset)}");
                }
                if (!Directory.EnumerateFileSystemEntries(from).Any())
                {
                    AssetDatabase.DeleteAsset(from);
                }
            }
        }

        private static bool HasChildGraphics(Image image)
        {
            return image.GetComponentsInChildren<Graphic>(true).Length > 1;
        }

        private static void FillHighlightSprites(UIAnimationPreset preset, Image image)
        {
            UIPointerEffectSettings hover = preset.Get(UIPointerEffect.Hover);
            if (hover.Enabled && hover.Sprite == null)
            {
                hover.Sprite = SuggestHighlight(image);
            }
            UIPointerEffectSettings pressed = preset.Get(UIPointerEffect.Pressed);
            Selectable selectable = image.GetComponent<Selectable>();
            if (pressed.Enabled && pressed.Sprite == null && selectable != null)
            {
                pressed.Sprite = selectable.spriteState.pressedSprite;
            }
        }

        private static Sprite FindPairedSprite(Sprite sprite)
        {
            string path = sprite != null ? AssetDatabase.GetAssetPath(sprite) : string.Empty;
            if (path.Length == 0)
            {
                return null;
            }

            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string extension = Path.GetExtension(path);
            string name = Path.GetFileNameWithoutExtension(path);
            foreach (string word in HighlightWords)
            {
                foreach (string candidate in PairedNames(name, word))
                {
                    Sprite found = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{candidate}{extension}");
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return null;
        }

        // Slot_Default → Slot_Selected, Slot_DefaultFrame → Slot_SelectedFrame 또는 Slot_Selected, Slot → Slot_Selected
        private static IEnumerable<string> PairedNames(string name, string word)
        {
            int index = name.IndexOf(DefaultWord, System.StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                string start = name.Substring(0, index);
                yield return start + word + name.Substring(index + DefaultWord.Length);
                yield return start + word;
            }
            yield return $"{name}_{word}";
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            return target.TryGetComponent(out T component) ? component : Undo.AddComponent<T>(target);
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }
                current = next;
            }
        }
    }
}
