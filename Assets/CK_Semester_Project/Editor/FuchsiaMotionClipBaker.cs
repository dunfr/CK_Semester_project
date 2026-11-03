using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.Editor
{
    // FBX의 몸 중심을 맞춘 사본을 굽는다. Bip001의 X·Y는 옆·앞, Z는 높이다.
    // 기존 Attack_idle·Ult·Jump는 같은 보정/컷/정지 곡선 축소를 유지한다. 없는 원본은 경고만 한다.
    // Normal은 다른 이름(w)의 리그 루트와 장면 전체의 가로 오프셋을 보정하고 끝 정지 구간을 자른다.
    // Normal 타깃에는 위치 곡선이 없어 모델 기본값으로 보충한다. Main의 Move 이동은 안무이므로 남긴다.
    // PhysCamera001 두 노드도 몸과 함께 옮겨 구도를 유지한다. 기존 에셋은 GUID를 유지한다.
    public static class FuchsiaMotionClipBaker
    {
        private const string BodyParent = "Move";
        private const string RootBone = BodyParent + "/Bip001";
        private const string OutputFolder = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Animations";
        private const float StillPositionRange = 1e-4f;
        private const float StillOtherRange = 1e-5f;

        private struct ClipSpec
        {
            public string Source, Output;
            public bool Loop, InPlaceJump, PrepareSkill;
            public string[] CameraNodes;

            public ClipSpec(string source, string name, bool loop, bool inPlaceJump, bool prepareSkill, params string[] cameraNodes)
            {
                Source = source;
                Output = OutputFolder + "/" + name + ".anim";
                Loop = loop;
                InPlaceJump = inPlaceJump;
                PrepareSkill = prepareSkill;
                CameraNodes = cameraNodes;
            }
        }

        // 배치 보고서가 실제로 이번에 구운 결과를 읽는다. 이전 원본이 없으면 기존 .anim은 건드리지 않는다.
        public sealed class BakeReport
        {
            public string OutputPath, RenamedRoot;
            public float SourceLength, Length, TrimmedSeconds;
            public int CutCount;
        }

        public static IReadOnlyList<BakeReport> LastBakeReports => _lastBakeReports;
        private static readonly List<BakeReport> _lastBakeReports = new List<BakeReport>();
        private static readonly ClipSpec[] Clips =
        {
            new ClipSpec("Assets/FuchsiaAnimation/Attack_idle.fbx", "FuchsiaAttackIdle", true, false, false),
            new ClipSpec("Assets/FuchsiaAnimation/Ult.fbx", "FuchsiaUlt", false, false, false, "Ult_cam", "Ult_cam.Target"),
            new ClipSpec("Assets/FuchsiaAnimation/Jump.fbx", "FuchsiaJump", false, true, false),
            new ClipSpec("Assets/Normal Skill.fbx", "FuchsiaNormalSkill", false, false, true, "PhysCamera001", "PhysCamera001.Target"),
            new ClipSpec("Assets/Main Skills.fbx", "FuchsiaMainSkill", false, false, true, "PhysCamera001", "PhysCamera001.Target"),
        };

        [MenuItem("Tools/Fuchsia/모션 클립 다시 만들기")]
        public static void BakeAll()
        {
            _lastBakeReports.Clear();
            foreach (ClipSpec spec in Clips)
            {
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(spec.Source).OfType<AnimationClip>()
                    .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__"));
                if (clip == null)
                {
                    Debug.LogWarning("모션 클립 굽기: 클립을 찾지 못했습니다. " + spec.Source);
                    continue;
                }
                Bake(clip, spec);
            }
        }

        private static void Bake(AnimationClip source, ClipSpec spec)
        {
            var baked = new AnimationClip { frameRate = source.frameRate };
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(source);
            AnimationCurve[] curves = bindings.Select(binding => AnimationUtility.GetEditorCurve(source, binding)).ToArray();
            GameObject model = spec.PrepareSkill ? AssetDatabase.LoadAssetAtPath<GameObject>(spec.Source) : null;
            string renamed = spec.PrepareSkill ? NormalizeRigRoot(bindings) : null;
            string modelRoot = renamed ?? BodyParent;
            Transform move = model != null ? model.transform.Find(modelRoot) : null;
            if (spec.PrepareSkill) FillRigPositionCurves(ref bindings, ref curves, move, source.length);
            Vector3 bodyOffset = GetBodyOffset(bindings, curves, move);
            float end = spec.PrepareSkill ? FindMotionEnd(bindings, curves, source.length) : source.length;
            if (spec.PrepareSkill)
            {
                Debug.Log($"모션 클립 굽기: {spec.Source} 끝 정지 {source.length - end:F4}초 제거 ({source.length:F4} → {end:F4}초)");
                foreach (AnimationCurve curve in curves)
                {
                    TrimCurve(curve, end);
                }
            }
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].type != typeof(Transform))
                {
                    continue;
                }
                string path = bindings[i].path;
                string property = bindings[i].propertyName;
                if (path == RootBone)
                {
                    if (property == "m_LocalPosition.x" || property == "m_LocalPosition.y")
                    {
                        Offset(curves[i], -curves[i].Evaluate(0f));
                    }
                    else if (spec.InPlaceJump && property == "m_LocalPosition.z")
                    {
                        KeepBelowStart(curves[i]);
                    }
                    continue;
                }
                if (path == BodyParent && (property == "m_LocalPosition.x" || property == "m_LocalPosition.z"))
                {
                    // 시작 위치만 빼므로 Main의 순간 이동과 높이·회전은 남는다.
                    Offset(curves[i], -curves[i].Evaluate(0f));
                }
                if (path.Length > 0 && path.IndexOf('/') < 0 && path != BodyParent)
                {
                    if (property == "m_LocalPosition.x") Offset(curves[i], -bodyOffset.x);
                    else if (property == "m_LocalPosition.z") Offset(curves[i], -bodyOffset.z);
                }
            }
            if (spec.PrepareSkill)
            {
                FillCameraCurves(ref bindings, ref curves, spec.CameraNodes, model, bodyOffset, end, source.frameRate);
            }
            int cuts = 0;
            foreach (string node in spec.CameraNodes)
            {
                cuts += StepCuts(bindings, curves, node, source.frameRate, spec.PrepareSkill);
            }
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].type == typeof(Transform) && !spec.CameraNodes.Contains(bindings[i].path))
                {
                    FlattenStill(curves[i], bindings[i].propertyName.StartsWith("m_LocalPosition.") ? StillPositionRange : StillOtherRange);
                }
            }
            AnimationUtility.SetEditorCurves(baked, bindings, curves);
            foreach (EditorCurveBinding original in AnimationUtility.GetObjectReferenceCurveBindings(source))
            {
                EditorCurveBinding binding = original;
                binding.path = RemapPath(binding.path, renamed);
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(source, original);
                if (spec.PrepareSkill) keys = keys.Where(key => key.time <= end).ToArray();
                AnimationUtility.SetObjectReferenceCurve(baked, binding, keys);
            }
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(source);
            AnimationUtility.SetAnimationEvents(baked, spec.PrepareSkill ? events.Where(e => e.time <= end).ToArray() : events);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = spec.Loop;
            if (spec.PrepareSkill)
            {
                settings.startTime = 0f;
                settings.stopTime = end;
            }
            AnimationUtility.SetAnimationClipSettings(baked, settings);
            baked.EnsureQuaternionContinuity();
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(spec.Output);
            AnimationClip asset;
            if (existing != null)
            {
                string name = existing.name;
                EditorUtility.CopySerialized(baked, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(baked);
                asset = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(baked, spec.Output);
                asset = baked;
            }
            // SaveAssets는 관련 없는 URP 에셋도 다시 저장할 수 있어 바꾼 클립만 저장한다.
            AssetDatabase.SaveAssetIfDirty(asset);
            _lastBakeReports.Add(new BakeReport { OutputPath = spec.Output, RenamedRoot = renamed,
                SourceLength = source.length, Length = asset.length, TrimmedSeconds = source.length - end, CutCount = cuts });
        }

        private static string NormalizeRigRoot(EditorCurveBinding[] bindings)
        {
            if (bindings.Any(b => b.path == BodyParent || b.path.StartsWith(BodyParent + "/", StringComparison.Ordinal))) return null;
            string[] candidates = bindings.Where(b => b.type == typeof(Transform)).Select(b => b.path)
                .Where(path => path.EndsWith("/Bip001", StringComparison.Ordinal) && path.Count(c => c == '/') == 1)
                .Select(path => path.Substring(0, path.IndexOf('/'))).Distinct().ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException("리그 루트를 하나로 정할 수 없습니다.");
            string root = candidates[0];
            for (int i = 0; i < bindings.Length; i++) bindings[i].path = RemapPath(bindings[i].path, root);
            Debug.Log($"모션 클립 굽기: 리그 루트 {root} → {BodyParent}");
            return root;
        }

        private static string RemapPath(string path, string root)
        {
            if (root == null) return path;
            return path == root ? BodyParent : path.StartsWith(root + "/", StringComparison.Ordinal) ? BodyParent + path.Substring(root.Length) : path;
        }

        private static void FillRigPositionCurves(ref EditorCurveBinding[] bindings, ref AnimationCurve[] curves, Transform move, float end)
        {
            if (move == null || move.Find("Bip001") == null) throw new InvalidOperationException("FBX 모델의 리그를 찾지 못했습니다.");
            var bs = bindings.ToList();
            var cs = curves.ToList();
            // 압축으로 상수 위치 곡선까지 빠져도 프리팹 기본값에 의존하지 않고 중심을 맞춘다.
            Add(BodyParent, "m_LocalPosition.x", move.localPosition.x);
            Add(BodyParent, "m_LocalPosition.z", move.localPosition.z);
            Add(RootBone, "m_LocalPosition.x", move.Find("Bip001").localPosition.x);
            Add(RootBone, "m_LocalPosition.y", move.Find("Bip001").localPosition.y);
            bindings = bs.ToArray();
            curves = cs.ToArray();

            void Add(string path, string property, float value)
            {
                if (bs.Any(b => b.path == path && b.type == typeof(Transform) && b.propertyName == property)) return;
                bs.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), property));
                cs.Add(AnimationCurve.Constant(0f, end, value));
            }
        }

        private static float Start(EditorCurveBinding[] bindings, AnimationCurve[] curves, string path, string property, float fallback)
        {
            int index = Array.FindIndex(bindings, b => b.path == path && b.type == typeof(Transform) && b.propertyName == property);
            return index >= 0 ? curves[index].Evaluate(0f) : fallback;
        }

        private static Vector3 GetBodyOffset(EditorCurveBinding[] bindings, AnimationCurve[] curves, Transform modelMove)
        {
            Quaternion fallback = modelMove != null ? modelMove.localRotation : Quaternion.identity;
            var rotation = new Quaternion(Start(bindings, curves, BodyParent, "m_LocalRotation.x", fallback.x),
                Start(bindings, curves, BodyParent, "m_LocalRotation.y", fallback.y),
                Start(bindings, curves, BodyParent, "m_LocalRotation.z", fallback.z),
                Start(bindings, curves, BodyParent, "m_LocalRotation.w", fallback.w));
            if (modelMove != null && bindings.Any(b => b.path == BodyParent && IsEuler(b.propertyName)))
            {
                string prefix = EulerPrefix(bindings.First(b => b.path == BodyParent && IsEuler(b.propertyName)).propertyName);
                Vector3 e = modelMove.localEulerAngles;
                rotation = Quaternion.Euler(Start(bindings, curves, BodyParent, prefix + "x", e.x),
                    Start(bindings, curves, BodyParent, prefix + "y", e.y), Start(bindings, curves, BodyParent, prefix + "z", e.z));
            }
            Vector3 move = new Vector3(Start(bindings, curves, BodyParent, "m_LocalPosition.x", modelMove != null ? modelMove.localPosition.x : 0f),
                0f, Start(bindings, curves, BodyParent, "m_LocalPosition.z", modelMove != null ? modelMove.localPosition.z : 0f));
            return move + rotation.normalized * new Vector3(Start(bindings, curves, RootBone, "m_LocalPosition.x", 0f),
                Start(bindings, curves, RootBone, "m_LocalPosition.y", 0f), 0f);
        }

        // 마지막 값과 다른 마지막 구간의 오른쪽 끝을 찾는다. 구간 내부도 검사해 탄젠트의 흔들림을 놓치지 않는다.
        private static float FindMotionEnd(EditorCurveBinding[] bindings, AnimationCurve[] curves, float sourceEnd)
        {
            float end = 0f;
            for (int i = 0; i < curves.Length; i++)
            {
                if (bindings[i].type != typeof(Transform)) continue;
                AnimationCurve curve = curves[i];
                Keyframe[] keys = curve.keys;
                float tolerance = bindings[i].propertyName.StartsWith("m_LocalPosition.") ? StillPositionRange : StillOtherRange;
                float final = curve.Evaluate(sourceEnd);
                for (int k = keys.Length - 2; k >= 0; k--)
                {
                    bool changed = false;
                    for (int s = 0; s < 4; s++)
                    {
                        float t = Mathf.Lerp(keys[k].time, keys[k + 1].time, s / 4f);
                        if (Mathf.Abs(curve.Evaluate(t) - final) > tolerance) { changed = true; break; }
                    }
                    if (changed) { end = Mathf.Max(end, keys[k + 1].time); break; }
                }
            }
            return end > 0f ? Mathf.Min(end, sourceEnd) : sourceEnd;
        }

        private static void TrimCurve(AnimationCurve curve, float end)
        {
            Keyframe[] old = curve.keys;
            if (old.Length == 0) return;
            float value = curve.Evaluate(end);
            var keys = old.Where(k => k.time <= end).ToList();
            if (keys.Count == 0 || keys[keys.Count - 1].time < end - 1e-6f)
            {
                float slope = (curve.Evaluate(end) - curve.Evaluate(Mathf.Max(0f, end - .0001f))) / .0001f;
                keys.Add(new Keyframe(end, value, slope, 0f));
            }
            curve.keys = keys.ToArray();
        }

        private static bool IsEuler(string property) => property.StartsWith("localEulerAngles", StringComparison.Ordinal)
            || property.StartsWith("m_LocalEulerAngles", StringComparison.Ordinal);
        private static string EulerPrefix(string property) => property.Substring(0, property.LastIndexOf('.') + 1);

        private static void FillCameraCurves(ref EditorCurveBinding[] bindings, ref AnimationCurve[] curves,
            string[] nodes, GameObject model, Vector3 offset, float end, float frameRate)
        {
            if (model == null) throw new InvalidOperationException("카메라 기본값을 읽을 FBX 모델이 없습니다.");
            var bs = bindings.ToList();
            var cs = curves.ToList();
            foreach (string path in nodes)
            {
                Transform node = model.transform.Find(path);
                if (node == null) throw new InvalidOperationException("FBX 카메라 노드가 없습니다: " + path);
                Vector3 position = node.localPosition - new Vector3(offset.x, 0f, offset.z);
                for (int axis = 0; axis < 3; axis++) AddMissing(path, "m_LocalPosition." + "xyz"[axis], position[axis]);
                int[] rotationIndices = Enumerable.Range(0, bs.Count).Where(i => bs[i].path == path
                    && bs[i].type == typeof(Transform) && (bs[i].propertyName.StartsWith("m_LocalRotation.") || IsEuler(bs[i].propertyName))).ToArray();
                int[] eulers = rotationIndices.Where(i => IsEuler(bs[i].propertyName)).ToArray();
                if (eulers.Length > 0)
                {
                    // 오일러와 쿼터니언을 동시에 바인딩하면 서로 덮는다. 카메라만 같은 시간의 쿼터니언으로 통일한다.
                    string prefix = EulerPrefix(bs[eulers[0]].propertyName);
                    var times = new SortedSet<float>(eulers.SelectMany(i => cs[i].keys.Select(k => k.time)));
                    for (int f = 0; f <= Mathf.CeilToInt(end * frameRate); f++) times.Add(Mathf.Min(end, f / frameRate));
                    var quaternionCurves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                    Quaternion previous = node.localRotation;
                    foreach (float t in times)
                    {
                        Vector3 e = node.localEulerAngles;
                        for (int a = 0; a < 3; a++)
                        {
                            int index = Array.FindIndex(eulers, i => bs[i].propertyName == prefix + "xyz"[a]);
                            if (index >= 0) e[a] = cs[eulers[index]].Evaluate(t);
                        }
                        Quaternion q = Quaternion.Euler(e);
                        if (Quaternion.Dot(previous, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        for (int a = 0; a < 4; a++) quaternionCurves[a].AddKey(new Keyframe(t, q[a]));
                        previous = q;
                    }
                    foreach (int i in rotationIndices.OrderByDescending(i => i)) { bs.RemoveAt(i); cs.RemoveAt(i); }
                    for (int a = 0; a < 4; a++) { bs.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[a])); cs.Add(quaternionCurves[a]); }
                    Debug.Log("모션 클립 굽기: 카메라 오일러 → 쿼터니언 " + path);
                }
                else
                {
                    for (int axis = 0; axis < 4; axis++) AddMissing(path, "m_LocalRotation." + "xyzw"[axis], node.localRotation[axis]);
                }
            }
            bindings = bs.ToArray();
            curves = cs.ToArray();

            void AddMissing(string path, string property, float value)
            {
                if (bs.Any(b => b.path == path && b.type == typeof(Transform) && b.propertyName == property)) return;
                bs.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), property));
                cs.Add(AnimationCurve.Constant(0f, end, value));
            }
        }
        private static void Offset(AnimationCurve curve, float amount)
        {
            Keyframe[] keys = curve.keys;
            for (int k = 0; k < keys.Length; k++)
            {
                keys[k].value += amount;
            }
            curve.keys = keys;
        }

        // 시작 높이보다 높은 키를 시작 높이로 낮추고 그 키의 탄젠트를 평평하게 한다(원래 탄젠트가 남으면 낮춘 키 사이가 다시 솟는다).
        // 나머지 키(웅크림·착지 충격)는 원래 곡선 그대로 둔다.
        private static void KeepBelowStart(AnimationCurve curve)
        {
            float start = curve.Evaluate(0f);
            Keyframe[] keys = curve.keys;
            var lowered = new bool[keys.Length];
            for (int k = 0; k < keys.Length; k++)
            {
                if (keys[k].value > start)
                {
                    keys[k].value = start;
                    keys[k].inTangent = 0f;
                    keys[k].outTangent = 0f;
                    lowered[k] = true;
                }
            }
            curve.keys = keys;
            for (int k = 0; k < keys.Length; k++)
            {
                if (lowered[k])
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Free);
                    AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Free);
                }
            }
        }

        // 멈춘 곡선(값의 폭이 range 이하)을 처음 값의 키 두 개(처음·끝 시간)로 바꾼다. 움직이는 곡선은 그대로 둔다.
        private static void FlattenStill(AnimationCurve curve, float range)
        {
            Keyframe[] keys = curve.keys;
            if (keys.Length <= 2 || keys.Max(key => key.value) - keys.Min(key => key.value) > range)
            {
                return;
            }
            curve.keys = new[] { new Keyframe(keys[0].time, keys[0].value), new Keyframe(keys[keys.Length - 1].time, keys[0].value) };
        }

        // 컷(숏이 바뀌는 한 프레임): 원본은 그 한 프레임을 보간해 카메라가 두 숏 사이를 훑고 지나가고, 컷 키의 가파른 탄젠트가
        // 앞뒤 숏으로 번져 컷 직전·직후 프레임도 흔들린다. 노드 위치가 한 프레임에 20cm 넘게, 앞뒤 프레임 이동의 10배 넘게 바뀌면 컷으로 보고,
        // 위치 곡선의 컷 구간은 앞 키 값을 유지하는 계단으로, 컷 키의 바깥쪽 탄젠트는 앞뒤 구간 자신의 기울기로 바꾼다.
        // 계속 움직이는 빠른 카메라 이동(돌리 등)은 앞뒤 프레임도 크게 움직이므로 컷으로 보지 않는다.
        // 회전(쿼터니언) 곡선은 실행 중에 키 탄젠트를 쓰지 않아 계단이 듣지 않는다. 그래서 시선은 PlayerBattleAnimation이
        // 계단이 듣는 두 노드(카메라·타깃)의 위치로 정한다.
        private static int StepCuts(EditorCurveBinding[] bindings, AnimationCurve[] curves, string node, float frameRate, bool sparseKeys)
        {
            int[] nodeCurves = Enumerable.Range(0, bindings.Length)
                .Where(i => bindings[i].path == node && bindings[i].type == typeof(Transform)
                    && bindings[i].propertyName.StartsWith("m_LocalPosition.")).ToArray();
            AnimationCurve Position(string axis) => nodeCurves.Select(i => bindings[i].propertyName == "m_LocalPosition." + axis ? curves[i] : null)
                .FirstOrDefault(curve => curve != null);
            AnimationCurve x = Position("x"), y = Position("y"), z = Position("z");
            if (x == null || y == null || z == null)
            {
                return 0;
            }

            float frame = 1f / frameRate;
            Vector3 At(float time) => new Vector3(x.Evaluate(time), y.Evaluate(time), z.Evaluate(time));
            // 새 FBX는 Keyframe Reduction으로 축마다 키 시간이 다를 수 있다. 기존 클립의 처리 순서는 유지한다.
            float last = Mathf.Max(x.keys.Last().time, Mathf.Max(y.keys.Last().time, z.keys.Last().time));
            var candidates = sparseKeys
                ? Enumerable.Range(0, Mathf.FloorToInt(last * frameRate + .001f)).Select(f => f * frame)
                : x.keys.Concat(y.keys).Concat(z.keys).Select(key => key.time).Distinct();
            float[] cuts = candidates.Where(time =>
            {
                float jump = (At(time + frame) - At(time)).magnitude;
                float around = Mathf.Max((At(time) - At(time - frame)).magnitude, (At(time + 2f * frame) - At(time + frame)).magnitude);
                return jump > .2f && jump > 10f * around;
            }).ToArray();

            foreach (int i in nodeCurves)
            {
                if (sparseKeys)
                {
                    // 계단의 양 끝 키가 없는 축은 원래 곡선을 평가해 보충한다.
                    // 모든 키를 재샘플링하지 않아 부드러운 돌리 구간의 보간은 그대로 남는다.
                    foreach (float cut in cuts)
                    {
                        float before = curves[i].Evaluate(cut), after = curves[i].Evaluate(cut + frame);
                        AddSample(curves[i], cut, before);
                        AddSample(curves[i], cut + frame, after);
                    }
                }
                Keyframe[] keys = curves[i].keys;
                foreach (float cut in cuts)
                {
                    int k = System.Array.FindIndex(keys, key => Mathf.Abs(key.time - cut) < .25f * frame);
                    if (k < 0 || k + 1 >= keys.Length || Mathf.Abs(keys[k + 1].time - cut - frame) >= .25f * frame)
                    {
                        continue;
                    }
                    keys[k].outTangent = float.PositiveInfinity;
                    keys[k + 1].inTangent = float.PositiveInfinity;
                    keys[k].inTangent = k > 0 ? Slope(keys[k - 1], keys[k]) : 0f;
                    keys[k + 1].outTangent = k + 2 < keys.Length ? Slope(keys[k + 1], keys[k + 2]) : 0f;
                }
                curves[i].keys = keys;
            }
            return cuts.Length;
        }

        private static void AddSample(AnimationCurve curve, float time, float value)
        {
            if (curve.keys.Any(k => Mathf.Abs(k.time - time) < 1e-5f)) return;
            const float delta = .0001f;
            float slope = (curve.Evaluate(time + delta) - curve.Evaluate(time - delta)) / (2f * delta);
            curve.AddKey(new Keyframe(time, value, slope, slope));
        }

        private static float Slope(Keyframe from, Keyframe to) => (to.value - from.value) / (to.time - from.time);
    }
}

