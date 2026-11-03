using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CK.SemesterProject.Editor
{
    // 그래픽 파트의 모션 FBX를 게임에 맞게 고친 .anim 사본으로 굽는다(Bip001의 X·Y는 캐릭터 기준 옆·앞 방향, Z는 높이).
    // - 모든 클립: 전투 모션(Attack_idle, Ult)은 몸 루트(Bip001)가 시작부터 옆으로 41cm, 앞으로 7cm 밀려 있다.
    //   그대로 쓰면 캐릭터가 충돌체·전투 위치에서 벗어나 보이므로, 시작 프레임의 가로 위치만큼 X·Y 곡선을 뺀다.
    //   모션 안의 이동은 시작점 기준으로 남고, 루트 바로 아래의 다른 노드(연출 카메라·이펙트)도 같은 만큼 옮겨 구도를 유지한다.
    // - 점프(Jump): 모션 안에 몸이 52cm 떠오르는 높이가 들어 있다. 실제 높이는 PlayerMovement가 CharacterController를 띄워 내므로,
    //   Z가 시작 높이 위로 올라가지 않게 잘라 제자리 점프로 만든다. 도약 전 웅크림과 착지 충격은 그대로 남는다.
    // - 연출 카메라(Ult의 Ult_cam, Ult_cam.Target): 숏이 바뀌는 컷이 한 프레임짜리 보간으로 들어 있어, 위치를 계단 키로 바꿔 한 번에 넘어가게 한다.
    // - 멈춘 곡선: 원본은 압축 없이 모든 곡선에 매 프레임 키가 있어, 거의 움직이지 않는 뼈의 위치·크기 곡선까지 그대로 구우면 .anim이 커진다
    //   (Ult는 122MB로 GitHub 파일 한도 100MB를 넘었다). 값의 폭이 허용 오차 안인 곡선은 처음 값의 키 두 개로 줄인다. 연출 카메라 노드는 그대로 둔다.
    // 그래픽 파트가 FBX를 다시 보내면 메뉴로 다시 만든다. 이미 있는 클립은 GUID를 유지한 채 내용만 바꾼다.
    public static class FuchsiaMotionClipBaker
    {
        private const string BodyParent = "Move";
        private const string RootBone = BodyParent + "/Bip001";
        private const string OutputFolder = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Animations";
        // 값의 폭이 이 안이면 멈춘 곡선으로 본다(위치는 m 단위라 0.1mm, 회전은 쿼터니언 성분, 크기는 배율).
        private const float StillPositionRange = 1e-4f;
        private const float StillOtherRange = 1e-5f;

        private static readonly (string Source, string Output, bool Loop, bool InPlaceJump, string[] CameraNodes)[] Clips =
        {
            ("Assets/FuchsiaAnimation/Attack_idle.fbx", OutputFolder + "/FuchsiaAttackIdle.anim", true, false, System.Array.Empty<string>()),
            ("Assets/FuchsiaAnimation/Ult.fbx", OutputFolder + "/FuchsiaUlt.anim", false, false, new[] { "Ult_cam", "Ult_cam.Target" }),
            ("Assets/FuchsiaAnimation/Jump.fbx", OutputFolder + "/FuchsiaJump.anim", false, true, System.Array.Empty<string>()),
        };

        [MenuItem("Tools/Fuchsia/모션 클립 다시 만들기")]
        public static void BakeAll()
        {
            foreach ((string source, string output, bool loop, bool inPlaceJump, string[] cameraNodes) in Clips)
            {
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>()
                    .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__"));
                if (clip == null)
                {
                    Debug.LogWarning("모션 클립 굽기: 클립을 찾지 못했습니다. " + source);
                    continue;
                }
                Bake(clip, output, loop, inPlaceJump, cameraNodes);
            }
            AssetDatabase.SaveAssets();
        }

        private static void Bake(AnimationClip source, string outputPath, bool loop, bool inPlaceJump, string[] cameraNodes)
        {
            var baked = new AnimationClip { frameRate = source.frameRate };
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(source);
            AnimationCurve[] curves = bindings.Select(binding => AnimationUtility.GetEditorCurve(source, binding)).ToArray();
            Vector3 bodyOffset = GetBodyOffset(bindings, curves);
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
                    else if (inPlaceJump && property == "m_LocalPosition.z")
                    {
                        KeepBelowStart(curves[i]);
                    }
                    continue;
                }
                // 루트 바로 아래 노드는 루트 공간에 있으므로, 몸을 옮긴 만큼 같이 옮긴다.
                if (path.Length > 0 && path.IndexOf('/') < 0 && path != BodyParent)
                {
                    if (property == "m_LocalPosition.x")
                    {
                        Offset(curves[i], -bodyOffset.x);
                    }
                    else if (property == "m_LocalPosition.z")
                    {
                        Offset(curves[i], -bodyOffset.z);
                    }
                }
            }
            foreach (string cameraNode in cameraNodes)
            {
                StepCuts(bindings, curves, cameraNode, source.frameRate);
            }
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].type == typeof(Transform) && !cameraNodes.Contains(bindings[i].path))
                {
                    FlattenStill(curves[i], bindings[i].propertyName.StartsWith("m_LocalPosition.") ? StillPositionRange : StillOtherRange);
                }
            }
            AnimationUtility.SetEditorCurves(baked, bindings, curves);
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
            {
                AnimationUtility.SetObjectReferenceCurve(baked, binding, AnimationUtility.GetObjectReferenceCurve(source, binding));
            }
            AnimationUtility.SetAnimationEvents(baked, AnimationUtility.GetAnimationEvents(source));
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(baked, settings);
            baked.EnsureQuaternionContinuity();

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            if (existing != null)
            {
                string name = existing.name;
                EditorUtility.CopySerialized(baked, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(baked);
            }
            else
            {
                AssetDatabase.CreateAsset(baked, outputPath);
            }
        }

        // 시작 프레임의 몸 루트(Bip001) 가로 위치를 캐릭터 루트 공간으로 바꾼 값. Bip001은 Move(-90° X 회전) 아래에 있다.
        private static Vector3 GetBodyOffset(EditorCurveBinding[] bindings, AnimationCurve[] curves)
        {
            float Start(string path, string property, float fallback)
            {
                int index = System.Array.FindIndex(bindings, binding => binding.path == path
                    && binding.type == typeof(Transform) && binding.propertyName == property);
                return index >= 0 ? curves[index].Evaluate(0f) : fallback;
            }

            var parentRotation = new Quaternion(Start(BodyParent, "m_LocalRotation.x", 0f), Start(BodyParent, "m_LocalRotation.y", 0f),
                Start(BodyParent, "m_LocalRotation.z", 0f), Start(BodyParent, "m_LocalRotation.w", 1f));
            return parentRotation.normalized
                * new Vector3(Start(RootBone, "m_LocalPosition.x", 0f), Start(RootBone, "m_LocalPosition.y", 0f), 0f);
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
        private static void StepCuts(EditorCurveBinding[] bindings, AnimationCurve[] curves, string node, float frameRate)
        {
            int[] nodeCurves = Enumerable.Range(0, bindings.Length)
                .Where(i => bindings[i].path == node && bindings[i].type == typeof(Transform)
                    && bindings[i].propertyName.StartsWith("m_LocalPosition.")).ToArray();
            AnimationCurve Position(string axis) => nodeCurves.Select(i => bindings[i].propertyName == "m_LocalPosition." + axis ? curves[i] : null)
                .FirstOrDefault(curve => curve != null);
            AnimationCurve x = Position("x"), y = Position("y"), z = Position("z");
            if (x == null || y == null || z == null)
            {
                return;
            }

            float frame = 1f / frameRate;
            Vector3 At(float time) => new Vector3(x.Evaluate(time), y.Evaluate(time), z.Evaluate(time));
            float[] cuts = x.keys.Concat(y.keys).Concat(z.keys).Select(key => key.time).Distinct().Where(time =>
            {
                float jump = (At(time + frame) - At(time)).magnitude;
                float around = Mathf.Max((At(time) - At(time - frame)).magnitude, (At(time + 2f * frame) - At(time + frame)).magnitude);
                return jump > .2f && jump > 10f * around;
            }).ToArray();

            foreach (int i in nodeCurves)
            {
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
        }

        private static float Slope(Keyframe from, Keyframe to) => (to.value - from.value) / (to.time - from.time);
    }
}
