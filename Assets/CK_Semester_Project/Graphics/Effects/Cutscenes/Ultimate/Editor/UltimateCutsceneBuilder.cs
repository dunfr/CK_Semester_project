using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace CK.CutsceneTools
{
    /// <summary>
    /// 궁극기(FuchsiaUlt) 컷신을 TL_Ultimate_Base 에 만든다. 에디터 전용이며 호출할 때마다 생성물을 지우고 새로 만든다.
    /// 컷 구성: 원본 Ult_cam 을 그대로 따라가다가, 원본의 적 확대(분홍 구체) 구간은 나무 전경 샷으로 바꾼다.
    /// 참격은 새로 만들지 않고 특수스킬 컷신이 쓰는 원본 난격 PF_FoxPreSlash 의 참격 리본·꽃잎을 복제해 쓴다.
    /// </summary>
    public static class UltimateCutsceneBuilder
    {
        private const string Root = "Assets/CK_Semester_Project/Graphics/Effects/Cutscenes/Ultimate/";
        private const string ScenePath = Root + "Scenes/SC_Ultimate_Workspace.unity";
        private const string TimelinePath = Root + "Timeline/TL_Ultimate_Base.playable";
        private const string FoxPrefab = "Assets/CK_Semester_Project/Graphics/Effects/FoxPreSlash/Prefabs/PF_FoxPreSlash.prefab";
        private const string FoxTex = "Assets/CK_Semester_Project/Graphics/Effects/FoxPreSlash/Textures/";
        private const string GlowProfilePath = "Assets/CK_Semester_Project/Graphics/Effects/Cutscenes/SpecialSkill_ZZZ/Volumes/VP_SpecialSkill_Glow.asset";
        private const string RigName = "UltimateRig";

        // ----------------------------------------------------------------- 타이밍 (타임라인 초). FuchsiaUlt 모션 분석값.
        private const float SwingHit = 4.12f;      // 검 휘두름이 멈추는 순간(오른손 속도 피크 4.0~4.1s)
        private const float TreeStart = 4.40f;     // 가지가 뻗기 시작
        private const float TreeEnd = 5.62f;       // 나무 완성
        private const float TurnStart = 5.70f;     // 몸을 돌려 뒤를 돌아봄(5.7~6.1s), 오른손이 안경에 닿음(5.8s)
        private const float GlowStart = 5.55f;     // 나무가 발광하기 시작
        private const float BreakTime = 6.15f;     // 안경을 올린 직후 폭발
        private const float ShotTreeA = 4.55f;     // 원본 적 확대 구간 대체 (4.55~5.70)
        private const float ShotTreeB = 6.08f;     // 폭발 전경 (6.08~6.90)
        private const float ShotTreeBEnd = 6.90f;
        private const float KanjiSplit = 6.62f;
        private static readonly Vector3 Enemy = new Vector3(1.2f, 0f, 6.7f);   // 원본 적 확대 컷이 바라보는 지점
        private const string KanjiSource = Root + "UI/T_UltimateKanji_Brush.png";   // 붓글씨 `花` 원본(흰 바탕, 검은 먹)
        private const float FaceCutStart = 202f / 60f;   // 원본 얼굴 확대 샷(프레임 202~227)은 쓰지 않는다
        private const float FaceCutEnd = 228f / 60f;

        // 라이팅은 특수스킬 컷신과 같은 값을 쓴다(SpecialSkillCutsceneStyle 에셋).
        private static SpecialSkillCutsceneStyle _style;
        private static SpecialSkillCutsceneStyle Style
        {
            get
            {
                if (_style == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:SpecialSkillCutsceneStyle");
                    _style = guids.Length > 0
                        ? AssetDatabase.LoadAssetAtPath<SpecialSkillCutsceneStyle>(AssetDatabase.GUIDToAssetPath(guids[0]))
                        : ScriptableObject.CreateInstance<SpecialSkillCutsceneStyle>();
                }
                return _style;
            }
        }

        private static readonly List<AnimationClip> _made = new List<AnimationClip>();

        [MenuItem("CK/Ultimate Cutscene/Build Ultimate Timeline")]
        public static string Build()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            var sb = new StringBuilder();
            var director = GameObject.Find("Ultimate_Director")?.GetComponent<PlayableDirector>();
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (director == null || timeline == null)
            {
                return "Ultimate_Director 또는 TL_Ultimate_Base 가 없습니다. CK/Ultimate Cutscene/Create Workspace Scene 을 먼저 실행하세요.";
            }
            director.playableAsset = timeline;
            Transform player = GameObject.Find("FuchsiaUpdatedAnimated")?.transform;
            Transform ultCam = player != null ? FindDeep(player, "Ult_cam") : null;
            Transform ultTarget = player != null ? FindDeep(player, "Ult_cam.Target") : null;
            CinemachineBrain brain = Object.FindFirstObjectByType<CinemachineBrain>();
            if (ultCam == null || ultTarget == null || brain == null)
            {
                return "플레이어의 Ult_cam / Ult_cam.Target 또는 CinemachineBrain 을 찾지 못했습니다.";
            }

            // 이전 생성물 정리: Fuchsia 트랙만 남긴다.
            foreach (GameObject old in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(g => g.name == RigName && g.transform.parent == null).ToArray())
            {
                Object.DestroyImmediate(old);
            }
            foreach (TrackAsset t in timeline.GetRootTracks().Where(t => t.name != "Fuchsia").ToList())
            {
                timeline.DeleteTrack(t);
            }
            float end = (float)timeline.duration;
            var rig = new GameObject(RigName);
            _made.Clear();

            BuildCameras(director, timeline, brain, rig.transform, ultCam, ultTarget, end, sb);
            Bounds treeBounds = BuildTree(director, timeline, rig.transform, end, sb);
            BuildBurst(director, timeline, rig.transform, treeBounds, sb);
            BuildKanji(director, timeline, rig.transform, treeBounds, sb);
            BuildScreen(director, timeline, rig.transform, end, sb);

            foreach (AnimationClip c in _made)
            {
                EditorUtility.SetDirty(c);
                AssetDatabase.SaveAssetIfDirty(c);
            }
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            director.RebuildGraph();   // 트랙을 새로 만들었으므로 그래프를 다시 짠다(안 하면 이전 그래프가 평가된다)
            director.time = 0d;
            director.Evaluate();
            sb.Insert(0, "궁극기 타임라인 구성 완료\n");
            Debug.Log(sb.ToString());
            return sb.ToString();
        }

        // ================================================================= helpers
        private static Transform FindDeep(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        private static AnimationCurve Curve(List<Keyframe> ks, bool stepped = false)
        {
            var c = new AnimationCurve(ks.ToArray());
            for (int i = 0; i < c.length; i++)
            {
                var m = stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear;
                AnimationUtility.SetKeyLeftTangentMode(c, i, m);
                AnimationUtility.SetKeyRightTangentMode(c, i, m);
            }
            return c;
        }

        private static AnimationCurve Keys(params (float t, float v)[] k)
        {
            return Curve(k.Select(x => new Keyframe(x.t, x.v)).ToList());
        }

        private static Gradient AlphaGradient(params (float t, float a)[] keys)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                keys.Select(k => new GradientAlphaKey(Mathf.Clamp01(k.a), Mathf.Clamp01(k.t))).ToArray());
            return g;
        }

        // 색 속성의 .a 만 애니메이션하면 나머지 채널이 0 으로 덮이므로 RGB 를 상수 곡선으로 함께 넣는다.
        private static void ColorRgb(AnimationClip clip, string path, System.Type type, string prop, Color c, float end)
        {
            string[] ch = { "r", "g", "b" };
            float[] v = { c.r, c.g, c.b };
            for (int i = 0; i < 3; i++)
            {
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, prop + "." + ch[i]), Keys((0f, v[i]), (end, v[i])));
            }
        }


        private static AnimationClip NewClip(string name)
        {
            string p = Root + "Animation/" + name + ".anim";
            EnsureFolder(Root + "Animation");
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            if (c != null)
            {
                foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(c)) AnimationUtility.SetEditorCurve(c, b, null);
            }
            else
            {
                c = new AnimationClip { frameRate = 60f, name = name };
                AssetDatabase.CreateAsset(c, p);
            }
            _made.Add(c);
            return c;
        }

        private static void EnsureFolder(string path)
        {
            path = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void ControlClip(PlayableDirector dir, TrackAsset track, GameObject go, double start, double dur, uint seed)
        {
            TimelineClip c = ((ControlTrack)track).CreateClip<ControlPlayableAsset>();
            c.start = start;
            c.duration = dur;
            c.displayName = go.name;
            var a = (ControlPlayableAsset)c.asset;
            a.sourceGameObject.exposedName = GUID.Generate().ToString();
            dir.SetReferenceValue(a.sourceGameObject.exposedName, go);
            a.postPlayback = ActivationControlPlayable.PostPlaybackState.Inactive;
            a.updateParticle = true;
            a.updateDirector = false;
            a.updateITimeControl = false;
            a.searchHierarchy = true;
            a.particleRandomSeed = seed;
        }

        private static Material ParticleMat(string name, Texture tex, int blend, int queue, Color color)
        {
            EnsureFolder(Root + "Materials");
            string p = Root + "Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, p);
            }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", blend);
            m.SetFloat("_SrcBlend", 5f);
            m.SetFloat("_DstBlend", blend == 2 ? 1f : 10f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            // URP 는 큐를 3000 + _QueueOffset 으로 되돌리므로 오프셋으로 지정해야 유지된다(-50~50).
            float queueOffset = Mathf.Clamp(queue - 3000, -50, 50);
            m.SetFloat("_QueueOffset", queueOffset);
            m.renderQueue = 3000 + (int)queueOffset;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static ParticleSystem NewPS(string name, Transform parent, Vector3 localPos, Material mat, int order)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            var ps = g.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule m = ps.main;
            m.playOnAwake = false;
            m.loop = false;
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 40f;
            return ps;
        }

        private static AnimationTrack AnimTrack(PlayableDirector dir, TimelineAsset tl, TrackAsset parent, string name, Animator target)
        {
            var t = (AnimationTrack)tl.CreateTrack(typeof(AnimationTrack), parent, name);
            dir.SetGenericBinding(t, target);
            return t;
        }

        // ================================================================= cameras
        private static void BuildCameras(PlayableDirector dir, TimelineAsset tl, CinemachineBrain brain, Transform rig,
            Transform ultCam, Transform ultTarget, float end, StringBuilder sb)
        {
            var camRig = new GameObject("CamRig");
            camRig.transform.SetParent(rig, false);
            camRig.AddComponent<Animator>();

            // 원본 연출 카메라: 모션이 움직이는 Ult_cam 노드의 위치에서 Ult_cam.Target 쪽을 본다(게임의 PlayerBattleAnimation 과 같은 방식). 컷도 노드 그대로 따른다.
            var orig = new GameObject("CM_Ult_Original");
            orig.transform.SetParent(camRig.transform, false);
            CinemachineCamera cmOrig = orig.AddComponent<CinemachineCamera>();
            cmOrig.Follow = ultCam;
            cmOrig.LookAt = ultTarget;
            orig.AddComponent<CinemachineHardLockToTarget>();
            orig.AddComponent<CinemachineHardLookAt>();
            LensSettings lens = cmOrig.Lens;
            lens.FieldOfView = 45f;
            cmOrig.Lens = lens;

            // 원본의 얼굴 확대 샷(프레임 202~227)을 빼고, 직전 샷의 마지막 구도를 이어받아 천천히 밀고 들어간 뒤 다음 샷으로 컷한다.
            dir.time = FaceCutStart - 1f / 60f;
            dir.Evaluate();
            Vector3 holdPos = ultCam.position, holdAim = ultTarget.position;
            var holdLook = new GameObject("HoldLookAt");
            holdLook.transform.SetParent(camRig.transform, false);
            holdLook.transform.position = holdAim;
            var hold = new GameObject("CM_Ult_Hold");
            hold.transform.SetParent(camRig.transform, false);
            hold.transform.position = holdPos;
            CinemachineCamera cmHold = hold.AddComponent<CinemachineCamera>();
            cmHold.LookAt = holdLook.transform;
            hold.AddComponent<CinemachineHardLookAt>();
            LensSettings holdLens = cmHold.Lens;
            holdLens.FieldOfView = 45f;
            cmHold.Lens = holdLens;
            Vector3 holdEnd = holdPos + (holdAim - holdPos).normalized * 0.4f;

            var look = new GameObject("TreeLookAt");
            look.transform.SetParent(camRig.transform, false);
            look.transform.position = Enemy + new Vector3(0f, 5.0f, 0f);

            var tree = new GameObject("CM_Tree");
            tree.transform.SetParent(camRig.transform, false);
            tree.transform.position = new Vector3(-2.4f, 2.6f, -9.0f);
            CinemachineCamera cmTree = tree.AddComponent<CinemachineCamera>();
            cmTree.LookAt = look.transform;
            tree.AddComponent<CinemachineHardLookAt>();
            LensSettings treeLens = cmTree.Lens;
            treeLens.FieldOfView = 58f;
            cmTree.Lens = treeLens;

            // 나무 전경 카메라 이동: 가지가 뻗는 동안 천천히 다가가고, 폭발 때는 한 번 끊어 크게 잡았다가 물러난다.
            AnimationClip clip = NewClip("Ultimate_CamRig");
            void Pos(string axis, params (float t, float v)[] k) =>
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("CM_Tree", typeof(Transform), "m_LocalPosition." + axis), Keys(k));
            // CamRig 는 원점 기준이라 로컬 = 월드.
            Pos("x", (0f, -2.4f), (ShotTreeA, -2.4f), (TurnStart, -1.2f), (ShotTreeB - 0.01f, -1.2f), (ShotTreeB, 0.3f), (ShotTreeBEnd, 1.2f), (end, 1.2f));
            Pos("y", (0f, 2.6f), (ShotTreeA, 2.6f), (TurnStart, 3.2f), (ShotTreeB - 0.01f, 3.2f), (ShotTreeB, 3.0f), (ShotTreeBEnd, 3.6f), (end, 3.6f));
            Pos("z", (0f, -9.0f), (ShotTreeA, -9.0f), (TurnStart, -6.5f), (ShotTreeB - 0.01f, -6.5f), (ShotTreeB, -9.5f), (ShotTreeBEnd, -7.2f), (end, -7.2f));
            void HoldPos(string axis, float a, float b) =>
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("CM_Ult_Hold", typeof(Transform), "m_LocalPosition." + axis),
                    Keys((0f, a), (FaceCutStart, a), (FaceCutEnd, b), (end, b)));
            HoldPos("x", holdPos.x, holdEnd.x);
            HoldPos("y", holdPos.y, holdEnd.y);
            HoldPos("z", holdPos.z, holdEnd.z);
            TrackAsset camAnim = AnimTrack(dir, tl, null, "Cam_Move", camRig.GetComponent<Animator>());
            TimelineClip cc = ((AnimationTrack)camAnim).CreateClip(clip);
            cc.start = 0d;
            cc.duration = end;
            cc.displayName = "Ultimate_CamRig";

            var track = tl.CreateTrack<CinemachineTrack>(null, "Cam");
            dir.SetGenericBinding(track, brain);
            void Shot(CinemachineCamera cam, float start, float stop, string label)
            {
                TimelineClip c = track.CreateClip<CinemachineShot>();
                c.start = start;
                c.duration = stop - start;
                c.displayName = label;
                var shot = (CinemachineShot)c.asset;
                shot.VirtualCamera.exposedName = GUID.Generate().ToString();
                dir.SetReferenceValue(shot.VirtualCamera.exposedName, cam);
            }
            Shot(cmOrig, 0f, FaceCutStart, "1 플레이어 강조 (원본 카메라)");
            Shot(cmHold, FaceCutStart, FaceCutEnd, "1 얼굴 확대 대신 이어가는 샷");
            Shot(cmOrig, FaceCutEnd, ShotTreeA, "1 플레이어 강조·휘두름 (원본 카메라)");
            Shot(cmTree, ShotTreeA, TurnStart, "2~3 세로 참격·가지 (나무 전경)");
            Shot(cmOrig, TurnStart, ShotTreeB, "3 뒤돌아봄·안경 (원본 카메라)");
            Shot(cmTree, ShotTreeB, ShotTreeBEnd, "4 폭발·한자 (나무 전경)");
            Shot(cmOrig, ShotTreeBEnd, end, "여운 (원본 카메라)");
            // 카메라는 컷신 시간 밖에서 게임 카메라와 다투지 않게 둔다.
            sb.AppendLine("카메라: 원본 Ult_cam 추종(FOV 45) 3구간 + 나무 전경 2구간 — 원본 적 확대(분홍 구체) 구간은 제외");
        }

        // ================================================================= tree
        private struct Ribbon
        {
            public string Pink, Core, Violet;
        }

        private static readonly Ribbon[] Pool =
        {
            new Ribbon { Pink = "F10_Falling_Diagonal", Core = "F10_Falling_Diagonal_WhiteCore", Violet = "F10_Cyan_Diagonal" },
            new Ribbon { Pink = "F14_Central_Rising", Core = "F14_Central_Rising_WhiteCore" },
            new Ribbon { Pink = "F20_Lower_Rising", Core = "F20_Lower_Rising_WhiteCore" },
            new Ribbon { Pink = "F24_Falling_Fork", Core = "F24_Falling_Fork_WhiteCore", Violet = "F27_Violet_Afterimage" },
            new Ribbon { Pink = "F12_Low_Sweep", Core = "F12_Low_Sweep_WhiteCore", Violet = "F12_Blue_Lower_Edge" },
            new Ribbon { Pink = "F26_Central_Thrust", Core = "F26_Central_Thrust_WhiteCore" },
            new Ribbon { Pink = "F22_Long_Rising", Core = "F22_Long_Rising_WhiteCore" },
            new Ribbon { Pink = "F18_Upper_Cross", Core = "F18_Upper_Cross_WhiteCore" },
            new Ribbon { Pink = "F11_High_Rising", Core = "F11_High_Rising_WhiteCore" },
            new Ribbon { Pink = "F25_Fine_Rising" },
            new Ribbon { Pink = "F21_Far_Left_Ray" },
        };

        private struct Branch
        {
            public Ribbon Def;
            public Vector2 From;
            public float AngleDeg, Length, Thick, Appear, Grow;
            public string Name;
        }

        private static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        private static Bounds BuildTree(PlayableDirector dir, TimelineAsset tl, Transform rig, float end, StringBuilder sb)
        {
            var treeRoot = new GameObject("FX_Tree");
            treeRoot.transform.SetParent(rig, false);
            treeRoot.transform.position = Enemy;
            treeRoot.AddComponent<Animator>();
            var ribbons = new GameObject("Ribbons");
            ribbons.transform.SetParent(treeRoot.transform, false);

            // 가지 배치. 줄기(세로 참격)는 지면 아래에서 하늘까지, 가지는 아래가 크고 위로 갈수록 짧고 가늘다. 2차 가지는 1차 가지의 끝쪽에서 갈라진다.
            var rnd = new System.Random(20261107);
            float R(float a, float b) => (float)(a + (b - a) * rnd.NextDouble());
            var branches = new List<Branch>();
            Ribbon trunkDef = Pool[0];
            branches.Add(new Branch { Name = "Trunk", Def = trunkDef, From = new Vector2(0f, -1.2f), AngleDeg = 90f, Length = 13.5f, Thick = TrunkThick, Appear = SwingHit, Grow = 0.10f });
            const int firstGen = 9;
            int pick = 1;
            float span = TreeEnd - TreeStart;
            var firstGenList = new List<Branch>();
            for (int i = 0; i < firstGen; i++)
            {
                float f = i / (float)(firstGen - 1);
                float side = i % 2 == 0 ? -1f : 1f;
                float h = Mathf.Lerp(1.6f, 8.2f, f) + R(-0.3f, 0.3f);
                float off = Mathf.Lerp(62f, 30f, f) + R(-6f, 6f);
                float len = Mathf.Lerp(9.5f, 4.2f, f) + R(-0.5f, 0.5f);
                var b = new Branch
                {
                    Name = "B" + i.ToString("00"),
                    Def = Pool[1 + (pick++ % (Pool.Length - 1))],
                    From = new Vector2(R(-0.1f, 0.1f), h),
                    AngleDeg = 90f + side * off,
                    Length = len,
                    Thick = Mathf.Lerp(BranchThick, BranchThick * 0.55f, f),
                    Appear = TreeStart + span * f * 0.82f,
                    Grow = Mathf.Lerp(0.30f, 0.18f, f),
                };
                firstGenList.Add(b);
                branches.Add(b);
            }
            int sub = 0;
            foreach (Branch p in firstGenList)
            {
                if (p.Length < 5f) continue;
                for (int k = 0; k < 2; k++)
                {
                    float along = k == 0 ? 0.52f : 0.80f;
                    float turn = (k == 0 ? 1f : -1f) * R(32f, 46f);
                    branches.Add(new Branch
                    {
                        Name = "S" + (sub++).ToString("00"),
                        Def = Pool[1 + (pick++ % (Pool.Length - 1))],
                        From = p.From + Dir(p.AngleDeg) * (p.Length * along),
                        AngleDeg = p.AngleDeg + turn,
                        Length = p.Length * R(0.42f, 0.55f),
                        Thick = p.Thick * 0.7f,
                        Appear = p.Appear + p.Grow + R(0.04f, 0.16f),
                        Grow = 0.16f,
                    });
                }
            }

            var src = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FoxPrefab));
            var byName = new Dictionary<string, ParticleSystem>();
            foreach (ParticleSystem ps in src.GetComponentsInChildren<ParticleSystem>(true))
            {
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r != null && r.renderMode == ParticleSystemRenderMode.Mesh && !byName.ContainsKey(ps.name)) byName[ps.name] = ps;
            }

            AnimationClip clip = NewClip("Ultimate_Tree");
            float last = BreakTime + 0.55f;
            float tc0 = SwingHit - 0.03f;               // 컨트롤 클립 시작. 리본의 startDelay 는 이 시각 기준이다.
            var bounds = new Bounds(Enemy, Vector3.zero);
            int made = 0;
            foreach (Branch b in branches)
            {
                bool trunk = b.Name == "Trunk";
                foreach (string psName in new[] { b.Def.Pink, b.Def.Core })
                {
                    if (string.IsNullOrEmpty(psName) || !byName.TryGetValue(psName, out ParticleSystem srcPs)) continue;
                    var r0 = srcPs.GetComponent<ParticleSystemRenderer>();
                    Mesh mesh = r0.mesh;
                    if (mesh == null || !mesh.isReadable) continue;
                    AxisOf(mesh, out float alpha, out Vector2 pmin, out float length);
                    if (length < 0.1f) continue;

                    GameObject go = Object.Instantiate(srcPs.gameObject, ribbons.transform);
                    go.name = b.Name + "_" + psName;
                    go.SetActive(true);
                    var ps = go.GetComponent<ParticleSystem>();
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    float sx = b.Length / length;
                    float sy = sx * b.Thick * (psName == b.Def.Core ? 0.28f : 1f);   // 흰 코어는 분홍 몸통 안쪽에 가늘게
                    Quaternion rot = Quaternion.Euler(0f, 0f, b.AngleDeg - alpha);
                    // 메시 시작점(pmin)이 가지의 시작점 From 에 닿도록 놓는다.
                    Vector3 full = new Vector3(b.From.x, b.From.y, 0f) - rot * new Vector3(pmin.x * sx, pmin.y * sy, 0f);
                    go.transform.localPosition = full;
                    go.transform.localRotation = rot;
                    go.transform.localScale = new Vector3(sx, sy, 1f);

                    float life = Mathf.Max(0.3f, last - b.Appear);
                    ParticleSystem.MainModule m = ps.main;
                    m.playOnAwake = false;
                    m.loop = false;
                    m.startDelay = b.Appear - tc0;
                    m.startLifetime = life;
                    m.duration = (b.Appear - tc0) + life + 0.05f;
                    m.simulationSpace = ParticleSystemSimulationSpace.Local;
                    m.startRotation = 0f;
                    m.startSize = 1f;
                    m.maxParticles = 4;
                    ParticleSystem.EmissionModule em = ps.emission;
                    em.rateOverTime = 0f;
                    em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                    ParticleSystem.ShapeModule shape = ps.shape; shape.enabled = false;
                    ParticleSystem.VelocityOverLifetimeModule vo = ps.velocityOverLifetime; vo.enabled = false;
                    ParticleSystem.RotationOverLifetimeModule ro = ps.rotationOverLifetime; ro.enabled = false;
                    ParticleSystem.NoiseModule nz = ps.noise; nz.enabled = false;
                    ParticleSystem.ForceOverLifetimeModule fo = ps.forceOverLifetime; fo.enabled = false;
                    ParticleSystem.SizeOverLifetimeModule szl = ps.sizeOverLifetime;
                    szl.enabled = false;
                    float burstAt = Mathf.Clamp01((BreakTime - b.Appear) / life);
                    ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
                    // 원본의 색 그라데이션(분홍·흰색 등)은 그대로 두고 알파만 바꾼다.
                    GradientColorKey[] origColors = col.enabled && col.color.mode == ParticleSystemGradientMode.Gradient
                        ? col.color.gradient.colorKeys
                        : new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
                    col.enabled = true;
                    var holdGradient = new Gradient();
                    holdGradient.SetKeys(origColors, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, burstAt), new GradientAlphaKey(0f, Mathf.Min(1f, burstAt + 0.45f / life)), new GradientAlphaKey(0f, 1f) });
                    col.color = holdGradient;
                    var rr = go.GetComponent<ParticleSystemRenderer>();
                    rr.renderMode = ParticleSystemRenderMode.Mesh;
                    rr.alignment = ParticleSystemRenderSpace.Local;
                    rr.pivot = Vector3.zero;

                    // 자라나기: 시작점이 고정된 채 길이가 늘어난다(위치와 크기를 함께 움직임). 폭발하면 굵어지며 사라진다.
                    string tpath = "Ribbons/" + go.name;
                    float tEnd = b.Appear + b.Grow;
                    Vector3 a0 = new Vector3(b.From.x, b.From.y, 0f);
                    void Grow(string prop, float from, float to, float pop)
                    {
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(tpath, typeof(Transform), prop),
                            Keys((0f, from), (b.Appear, from), (tEnd, to), (BreakTime, to), (BreakTime + 0.35f, pop)));
                    }
                    Grow("m_LocalPosition.x", a0.x, full.x, full.x);
                    Grow("m_LocalPosition.y", a0.y, full.y, full.y);
                    Grow("m_LocalScale.x", 0.001f, sx, sx * 1.04f);
                    Grow("m_LocalScale.y", 0.001f, sy, sy * (trunk ? 1.5f : 1.3f));
                    rr.sortingOrder = 30 + (trunk ? 4 : 0) + (psName == b.Def.Core ? 2 : 0);
                    rr.shadowCastingMode = ShadowCastingMode.Off;
                    rr.receiveShadows = false;

                    // 발광: 머티리얼 모양·색은 그대로 두고 _GlowIntensity 만 올려 쌓일수록 밝아지다 폭발 순간 최대가 된다.
                    Material gm = rr.sharedMaterial;
                    if (gm != null && gm.HasProperty("_GlowIntensity"))
                    {
                        float g0 = gm.GetFloat("_GlowIntensity");
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(tpath, typeof(ParticleSystemRenderer), "material._GlowIntensity"),
                            Keys((0f, g0), (GlowStart, g0), (BreakTime, g0 * (1f + Style.burstGlowBoost)),
                                (last, g0 * (1f + Style.burstGlowBoost * 1.8f)), (last + 1f / 60f, g0)));   // 특수스킬 컷신과 같은 발광 곡선
                    }
                    Vector2 tip = b.From + Dir(b.AngleDeg) * b.Length;
                    bounds.Encapsulate(Enemy + new Vector3(b.From.x, b.From.y, 0f));
                    bounds.Encapsulate(Enemy + new Vector3(tip.x, tip.y, 0f));
                    made++;
                }
            }
            Object.DestroyImmediate(src);

            var anim = AnimTrack(dir, tl, null, "FX_Tree_Anim", treeRoot.GetComponent<Animator>());
            TimelineClip ac = ((AnimationTrack)anim).CreateClip(clip);
            ac.start = 0d;
            ac.duration = end;
            ac.displayName = "Ultimate_Tree";
            var ctl = tl.CreateTrack(typeof(ControlTrack), null, "FX_Tree");
            ControlClip(dir, ctl, ribbons, tc0, last + 0.1f - tc0, 5u);
            sb.AppendLine("나무: 줄기 1 + 1차 가지 " + firstGen + " + 2차 가지 " + sub + " → 리본 파티클 " + made + "개 (원본 PF_FoxPreSlash 복제), 줄기 " + SwingHit.ToString("F2") + "s, 가지 " + TreeStart.ToString("F2") + "~" + TreeEnd.ToString("F2") + "s");
            return bounds;
        }

        /// <summary>리본 메시의 주축(PCA) 각도, 시작점(주축 최소쪽 끝), 길이.</summary>
        private static void AxisOf(Mesh mesh, out float alphaDeg, out Vector2 pmin, out float length)
        {
            Vector3[] vs = mesh.vertices;
            var p = vs.Select(v => new Vector2(v.x, v.y)).ToArray();
            Vector2 m = Vector2.zero;
            foreach (Vector2 v in p) m += v;
            m /= Mathf.Max(1, p.Length);
            float cxx = 0f, cyy = 0f, cxy = 0f;
            foreach (Vector2 v in p) { Vector2 d = v - m; cxx += d.x * d.x; cyy += d.y * d.y; cxy += d.x * d.y; }
            float th = 0.5f * Mathf.Atan2(2f * cxy, cxx - cyy);
            Vector2 ax = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
            float tmin = float.MaxValue, tmax = float.MinValue;
            Vector2 a = m, b = m;
            foreach (Vector2 v in p)
            {
                float t = Vector2.Dot(v - m, ax);
                if (t < tmin) { tmin = t; a = v; }
                if (t > tmax) { tmax = t; b = v; }
            }
            // 주축이 반대로 잡혀도 항상 같은 쪽(오른쪽)이 끝이 되게 한다.
            if (ax.x < 0f) { ax = -ax; th += Mathf.PI; (a, b) = (b, a); (tmin, tmax) = (-tmax, -tmin); }
            // 끝점은 리본 중심선 위의 점으로 잡는다(꼭짓점이 폭 방향으로 치우치지 않게 중심 + 축 * t).
            pmin = m + ax * tmin;
            length = tmax - tmin;
            alphaDeg = th * Mathf.Rad2Deg;
        }

        // ================================================================= burst
        private static void BuildBurst(PlayableDirector dir, TimelineAsset tl, Transform rig, Bounds tree, StringBuilder sb)
        {
            var burst = new GameObject("FX_Burst");
            burst.transform.SetParent(rig, false);
            Vector3 center = Enemy + new Vector3(0f, 4.6f, -0.3f);
            burst.transform.position = center;
            burst.transform.rotation = Quaternion.identity;
            var glowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FoxTex + "T_SoftGlow.png");
            Material mGlow = ParticleMat("M_UltimateGlow", glowTex, 2, 3031, Color.white);
            Color pink = Style.glowColor;   // 특수스킬 컷신과 같은 발광 색

            // 참격 적중 섬광(세로 참격이 생기는 순간)
            var hit = new GameObject("HitFlash");
            hit.transform.SetParent(rig, false);
            hit.transform.position = Enemy + new Vector3(0f, 2.4f, -0.4f);
            ParticleSystem hf = NewPS("HitFlare", hit.transform, Vector3.zero, mGlow, 44);
            ParticleSystem.MainModule hm = hf.main;
            hm.duration = 0.5f; hm.startLifetime = 0.4f; hm.startSpeed = 0f; hm.startSize = Style.breakFlareSize * 0.5f; hm.startColor = pink;
            hm.simulationSpace = ParticleSystemSimulationSpace.World; hm.maxParticles = 2;
            ParticleSystem.EmissionModule he = hf.emission; he.rateOverTime = 0f; he.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            ParticleSystem.ShapeModule hs = hf.shape; hs.enabled = false;
            ParticleSystem.SizeOverLifetimeModule hz = hf.sizeOverLifetime; hz.enabled = true;
            hz.size = new ParticleSystem.MinMaxCurve(1f, Keys((0f, 0.25f), (0.25f, 1f), (1f, 1.2f)));
            ParticleSystem.ColorOverLifetimeModule hc = hf.colorOverLifetime; hc.enabled = true;
            hc.color = AlphaGradient((0f, 1f), (0.3f, 0.8f), (1f, 0f));
            hit.SetActive(false);
            var hitTrack = tl.CreateTrack(typeof(ControlTrack), null, "FX_HitFlash");
            ControlClip(dir, hitTrack, hit, SwingHit - 0.02f, 0.6, 71u);

            // 폭발: 큰 빛 + 짧고 강한 코어
            var bcore = new GameObject("Core");
            bcore.transform.SetParent(burst.transform, false);
            ParticleSystem flare = NewPS("BurstFlare", bcore.transform, Vector3.zero, mGlow, 44);
            ParticleSystem.MainModule fm = flare.main;
            fm.duration = 0.7f; fm.startLifetime = 0.55f; fm.startSpeed = 0f; fm.startSize = Style.breakFlareSize; fm.startColor = pink;
            fm.simulationSpace = ParticleSystemSimulationSpace.World; fm.maxParticles = 2;
            ParticleSystem.EmissionModule fe = flare.emission; fe.rateOverTime = 0f; fe.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            ParticleSystem.ShapeModule fs = flare.shape; fs.enabled = false;
            ParticleSystem.SizeOverLifetimeModule fz = flare.sizeOverLifetime; fz.enabled = true;
            fz.size = new ParticleSystem.MinMaxCurve(1f, Keys((0f, 0.3f), (0.2f, 1f), (1f, 1.2f)));
            ParticleSystem.ColorOverLifetimeModule fc = flare.colorOverLifetime; fc.enabled = true;
            fc.color = AlphaGradient((0f, 1f), (0.2f, 0.9f), (1f, 0f));
            ParticleSystem core = NewPS("BurstCore", bcore.transform, new Vector3(0f, 0f, -0.2f), mGlow, 46);
            ParticleSystem.MainModule cm = core.main;
            cm.duration = 0.4f; cm.startLifetime = 0.3f; cm.startSpeed = 0f; cm.startSize = Style.breakFlareSize * 0.45f; cm.startColor = Color.white;
            cm.simulationSpace = ParticleSystemSimulationSpace.World; cm.maxParticles = 2;
            ParticleSystem.EmissionModule ce = core.emission; ce.rateOverTime = 0f; ce.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            ParticleSystem.ShapeModule cs = core.shape; cs.enabled = false;
            ParticleSystem.SizeOverLifetimeModule cz = core.sizeOverLifetime; cz.enabled = true;
            cz.size = new ParticleSystem.MinMaxCurve(1f, Keys((0f, 0.4f), (0.3f, 1f), (1f, 1.3f)));
            ParticleSystem.ColorOverLifetimeModule cc = core.colorOverLifetime; cc.enabled = true;
            cc.color = AlphaGradient((0f, 1f), (1f, 0f));
            bcore.SetActive(false);
            var flareTrack = tl.CreateTrack(typeof(ControlTrack), null, "FX_BurstFlare");
            ControlClip(dir, flareTrack, bcore, BreakTime, 0.8, 21u);

            // 벚꽃잎: 특수스킬 컷신과 같은 원본 난격(PF_FoxPreSlash)의 꽃잎 파티클을 나무 위 여러 지점에서 터뜨린다.
            var petals = new GameObject("Petals");
            petals.transform.SetParent(rig, false);
            var foxTmp = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FoxPrefab));
            string[] petalNames = { "F00_Existing_Petals", "F08_Cut_Petals", "F24_Drifting_Petals", "F36_Last_Petals" };
            var petalSrc = foxTmp.GetComponentsInChildren<ParticleSystem>(true).Where(p => petalNames.Contains(p.name)).ToList();
            var rnd = new System.Random(77);
            float R(float a, float b) => (float)(a + (b - a) * rnd.NextDouble());
            int groups = 14;
            for (int gi = 0; gi < groups; gi++)
            {
                var grp = new GameObject("Petals_" + gi.ToString("00"));
                grp.transform.SetParent(petals.transform, false);
                grp.transform.position = new Vector3(Enemy.x + R(-5.5f, 5.5f), Mathf.Max(1.0f, R(1.5f, 9f)), Enemy.z - 0.4f);
                foreach (ParticleSystem s in petalSrc)
                {
                    GameObject c = Object.Instantiate(s.gameObject, grp.transform);
                    c.name = s.name;
                    c.SetActive(true);
                    c.transform.localPosition = new Vector3(R(-0.5f, 0.5f), R(-0.5f, 0.5f), 0f);
                    c.transform.localRotation = Quaternion.identity;
                    var cps = c.GetComponent<ParticleSystem>();
                    ParticleSystem.MainModule pm = cps.main;
                    pm.playOnAwake = false;
                    pm.startDelay = R(0f, 0.3f);
                    pm.startSizeMultiplier *= 2.4f;
                    pm.gravityModifier = 0.25f;
                    pm.startLifetimeMultiplier *= 1.6f;
                    var pr = c.GetComponent<ParticleSystemRenderer>();
                    if (pr != null) pr.sortingOrder = 48;
                }
            }
            Object.DestroyImmediate(foxTmp);
            petals.SetActive(false);
            var petalTrack = tl.CreateTrack(typeof(ControlTrack), null, "FX_BurstPetals");
            ControlClip(dir, petalTrack, petals, BreakTime, 2.2, 33u);
            sb.AppendLine("섬광·폭발: 적중 " + SwingHit.ToString("F2") + "s, 폭발 " + BreakTime.ToString("F2") + "s, 꽃잎 " + groups + "곳 (원본 난격 꽃잎 복제)");
        }

        // ================================================================= kanji
        // 레이어 순서(아래 → 위): 참격(나뭇가지, 30~36) → 꽃잎(48) → 한자(60) → 베는 빛(70)
        private const int KanjiOrder = 60;
        // 리본 굵기 배율(원본 리본 폭 기준). 원본 SG_Slash 셰이더는 폭을 크게 키우면 색 계산이 범위를 벗어나 보색(초록)이 번지므로 낮게 유지한다.
        private const float TrunkThick = 3.0f;
        private const float BranchThick = 1.6f;

        private static void BuildKanji(PlayableDirector dir, TimelineAsset tl, Transform rig, Bounds tree, StringBuilder sb)
        {
            string pathA = Root + "UI/T_UltimateKanji_A.png";
            string pathB = Root + "UI/T_UltimateKanji_B.png";
            float aspect = MakeKanjiTextures(KanjiSource, pathA, pathB);
            if (aspect <= 0f)
            {
                sb.AppendLine("한자: 글리프를 만들지 못해 건너뜀");
                return;
            }
            var texA = AssetDatabase.LoadAssetAtPath<Texture2D>(pathA);
            var texB = AssetDatabase.LoadAssetAtPath<Texture2D>(pathB);
            var glowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FoxTex + "T_SoftGlow.png");
            Color ink = new Color(0.95f, 0.1f, 0.5f, 1f);
            Material mA = ParticleMat("M_UltimateKanjiA", texA, 0, 3045, ink);
            Material mB = ParticleMat("M_UltimateKanjiB", texB, 0, 3045, ink);
            Material mCut = ParticleMat("M_UltimateCut", glowTex, 2, 3050, new Color(1f, 0.7f, 0.92f, 1f));

            var kanji = new GameObject("FX_Kanji");
            kanji.transform.SetParent(rig, false);
            kanji.transform.position = new Vector3(Enemy.x, 4.0f, Enemy.z - 1.7f);   // 줄기·꽃잎보다 카메라 쪽(꽃잎 셰이더가 깊이를 쓰므로 뒤에 두면 가려진다), 줄기와 같은 x
            kanji.AddComponent<Animator>();
            float height = 6.6f;
            float width = height * aspect;
            GameObject Quad(string name, Material mat, Vector3 scale, int order)
            {
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = name;
                q.transform.SetParent(kanji.transform, false);
                q.transform.localScale = scale;
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.sortingOrder = order;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                return q;
            }
            Quad("Kanji_A", mA, new Vector3(width, height, 1f), KanjiOrder);   // 왼쪽 절반
            Quad("Kanji_B", mB, new Vector3(width, height, 1f), KanjiOrder);   // 오른쪽 절반
            // 한자의 중앙을 세로로 가르는 참격 빛(줄기와 같은 선). 갈라지는 순간 번쩍인다.
            GameObject cut = Quad("CutLine", mCut, new Vector3(height * 1.4f, 0.4f, 1f), KanjiOrder + 10);
            cut.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            cut.transform.localPosition = new Vector3(0f, 0f, -0.2f);

            AnimationClip clip = NewClip("Ultimate_Kanji");
            float t0 = BreakTime, t1 = KanjiSplit, t2 = KanjiSplit + 0.75f;
            void Move(string obj, float sign)
            {
                // 중앙에서 좌우로 갈라져 벌어진다(A 는 왼쪽, B 는 오른쪽).
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(obj, typeof(Transform), "m_LocalPosition.x"),
                    Keys((0f, 0f), (t1, 0f), (t1 + 0.12f, sign * 0.7f), (t2, sign * 3.4f)));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(obj, typeof(Transform), "m_LocalPosition.y"),
                    Keys((0f, 0f), (t1, 0f), (t2, -sign * 0.4f)));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(obj, typeof(Transform), "localEulerAnglesRaw.z"),
                    Keys((0f, 0f), (t1, 0f), (t2, sign * 5f)));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(obj, typeof(MeshRenderer), "material._BaseColor.a"),
                    Keys((0f, 0f), (t0 - 0.001f, 0f), (t0 + 0.08f, 0.9f), (t1 + 0.15f, 0.9f), (t2, 0f)));
                ColorRgb(clip, obj, typeof(MeshRenderer), "material._BaseColor", ink, t2);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(obj, typeof(Transform), "m_LocalScale.x"),
                    Keys((0f, width * 1.3f), (t0 - 0.001f, width * 1.3f), (t0 + 0.30f, width), (t2, width)));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(obj, typeof(Transform), "m_LocalScale.y"),
                    Keys((0f, height * 1.3f), (t0 - 0.001f, height * 1.3f), (t0 + 0.30f, height), (t2, height)));
            }
            Move("Kanji_A", -1f);
            Move("Kanji_B", 1f);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("CutLine", typeof(MeshRenderer), "material._BaseColor.a"),
                Keys((0f, 0f), (t1 - 0.001f, 0f), (t1, 1f), (t1 + 0.35f, 0f), (t2, 0f)));
            ColorRgb(clip, "CutLine", typeof(MeshRenderer), "material._BaseColor", new Color(1f, 0.7f, 0.92f), t2);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("CutLine", typeof(Transform), "m_LocalScale.x"),
                Keys((0f, 0.1f), (t1 - 0.001f, 0.1f), (t1, height * 0.6f), (t1 + 0.35f, height * 1.5f), (t2, height * 1.5f)));
            var track = AnimTrack(dir, tl, null, "FX_Kanji_Anim", kanji.GetComponent<Animator>());
            TimelineClip kc = ((AnimationTrack)track).CreateClip(clip);
            kc.start = 0d;
            kc.duration = (float)tl.duration;
            kc.displayName = "Ultimate_Kanji";
            sb.AppendLine("한자 (붓글씨 花): " + BreakTime.ToString("F2") + "s 나타나 " + KanjiSplit.ToString("F2") + "s 중앙이 세로로 베여 좌우로 갈라짐 (레이어: 참격 → 꽃잎 → 한자)");
        }

        /// <summary>
        /// 붓글씨 한자 원본(흰 바탕에 검은 먹)의 먹색을 알파로 바꾸고, 글자 영역만 잘라 가운데 세로선으로 둘로 가른 투명 PNG 두 장(왼쪽/오른쪽)을 만든다.
        /// 두 장은 같은 크기·같은 위치라서 겹쳐 놓으면 원래 글자가 된다. 가로세로 비를 돌려준다(실패 시 0).
        /// </summary>
        private static float MakeKanjiTextures(string sourcePath, string pathA, string pathB)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", sourcePath));
            if (!File.Exists(full))
            {
                return 0f;
            }
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(File.ReadAllBytes(full)))
            {
                Object.DestroyImmediate(src);
                return 0f;
            }
            int sw = src.width, sh = src.height;
            Color32[] px = src.GetPixels32();
            Object.DestroyImmediate(src);

            // 원본이 투명 배경이면 그 알파를 그대로 쓰고, 불투명 흰 바탕이면 먹색(어두울수록 불투명)을 알파로 바꾼다.
            bool hasAlpha = false;
            foreach (Color32 pc in px) { if (pc.a < 250) { hasAlpha = true; break; } }
            var alpha = new float[sw * sh];
            int minX = sw, minY = sh, maxX = -1, maxY = -1;
            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    Color32 c = px[y * sw + x];
                    float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                    float a = hasAlpha ? c.a / 255f : Mathf.Clamp01((0.92f - lum) / 0.8f);
                    alpha[y * sw + x] = a;
                    if (a > 0.08f)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (maxX < minX || maxY < minY)
            {
                return 0f;
            }
            int pad = 6;
            minX = Mathf.Max(0, minX - pad); minY = Mathf.Max(0, minY - pad);
            maxX = Mathf.Min(sw - 1, maxX + pad); maxY = Mathf.Min(sh - 1, maxY + pad);
            int w = maxX - minX + 1, h = maxY - minY + 1;
            int half = w / 2;                       // 글자의 가운데 세로선

            var colA = new Color[w * h];
            var colB = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float a = alpha[(minY + y) * sw + (minX + x)];
                    if (a <= 0.003f) continue;
                    var c = new Color(1f, 1f, 1f, a);
                    if (x < half) colA[y * w + x] = c; else colB[y * w + x] = c;
                }
            }
            void Save(string path, Color[] cols)
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.SetPixels(cols);
                t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var im = (TextureImporter)AssetImporter.GetAtPath(path);
                if (im != null)
                {
                    im.mipmapEnabled = false;
                    im.alphaIsTransparency = true;
                    im.wrapMode = TextureWrapMode.Clamp;
                    im.maxTextureSize = 2048;
                    im.textureCompression = TextureImporterCompression.Uncompressed;
                    im.SaveAndReimport();
                }
            }
            Save(pathA, colA);
            Save(pathB, colB);
            return (float)w / h;
        }

        // ================================================================= screen: backdrop, glow (특수스킬과 동일), flash
        private static void BuildScreen(PlayableDirector dir, TimelineAsset tl, Transform rig, float end, StringBuilder sb)
        {
            // 어두운 배경: 하늘·바닥만 덮는 반투명 돔과 바닥 판. 월드 깊이 테스트를 받으므로 캐릭터와 이펙트는 그대로 밝다.
            // (특수스킬 컷신의 FX_DarkSpread 와 같은 발상. 카메라가 여러 방향을 보므로 카메라 자식이 아니라 월드에 둔다.)
            var backdrop = new GameObject("FX_Backdrop");
            backdrop.transform.SetParent(rig, false);
            backdrop.AddComponent<Animator>();
            Material mDark = ParticleMat("M_UltimateDark", null, 0, 2600, new Color(0.035f, 0.0f, 0.075f, 1f));
            GameObject Backdrop(string name, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale)
            {
                GameObject g = GameObject.CreatePrimitive(type);
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.name = name;
                g.transform.SetParent(backdrop.transform, false);
                g.transform.position = pos;
                g.transform.eulerAngles = euler;
                g.transform.localScale = scale;
                var r = g.GetComponent<MeshRenderer>();
                r.sharedMaterial = mDark;
                r.sortingOrder = -100;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                return g;
            }
            Backdrop("Dome", PrimitiveType.Sphere, Vector3.zero, Vector3.zero, Vector3.one * 180f);
            Backdrop("Ground", PrimitiveType.Quad, new Vector3(0f, 0.03f, 0f), new Vector3(90f, 0f, 0f), new Vector3(160f, 160f, 1f));
            AnimationClip bclip = NewClip("Ultimate_Backdrop");
            foreach (string part in new[] { "Dome", "Ground" })
            {
                float peak = part == "Dome" ? 0.94f : 0.8f;
                AnimationUtility.SetEditorCurve(bclip, EditorCurveBinding.FloatCurve(part, typeof(MeshRenderer), "material._BaseColor.a"),
                    Keys((0f, 0f), (SwingHit - 0.03f, 0f), (SwingHit + 0.10f, peak), (BreakTime + 0.02f, peak), (BreakTime + 0.7f, peak * 0.5f), (KanjiSplit + 0.9f, 0f), (end, 0f)));
                ColorRgb(bclip, part, typeof(MeshRenderer), "material._BaseColor", new Color(0.035f, 0f, 0.075f), end);
            }
            var bt = AnimTrack(dir, tl, null, "FX_Backdrop_Anim", backdrop.GetComponent<Animator>());
            TimelineClip bc = ((AnimationTrack)bt).CreateClip(bclip);
            bc.start = 0d;
            bc.duration = end;
            bc.displayName = "Ultimate_Backdrop";

            // ---- 라이팅: 특수스킬 컷신(SpecialSkillCutsceneFinale)과 같은 구성·곡선·값. 값은 SpecialSkillCutsceneStyle 에셋에서 읽는다.
            SpecialSkillCutsceneStyle s = Style;
            Vector3 center = Enemy + new Vector3(0f, 4.6f, 0f);
            Vector3 camPos = new Vector3(-1.2f, 3.2f, -6.5f);
            var glowProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GlowProfilePath);
            if (glowProfile != null)
            {
                // 발광 볼륨: 폭발 직전까지 pow(1.6)로 올라 최대 0.75, 폭발 뒤 0.12초에 걷힌다.
                var wk = new List<Keyframe>();
                for (float t = 0f; t <= end + 0.0001f; t += 1f / 60f)
                {
                    float w = 0.75f * (t < GlowStart ? 0f : t < BreakTime ? Mathf.Pow(Mathf.InverseLerp(GlowStart, BreakTime, t), 1.6f) : Mathf.Clamp01(1f - (t - BreakTime) / 0.12f));
                    wk.Add(new Keyframe(t, w));
                }
                VolumeAnim("Volume_Glow", glowProfile, 8f, tl, dir, rig, Curve(wk));
            }

            // 발광 점광원
            var lightRig = new GameObject("FX_GlowRig");
            lightRig.transform.SetParent(rig, false);
            lightRig.AddComponent<Animator>();
            var lg = new GameObject("Glow_Light");
            lg.transform.SetParent(lightRig.transform, false);
            lg.transform.position = Vector3.Lerp(center, camPos, 0.25f);
            var li = lg.AddComponent<Light>();
            li.type = LightType.Point;
            li.color = s.glowColor;
            li.range = 12f;
            li.intensity = 0f;
            li.shadows = LightShadows.None;
            AnimationClip gclip = NewClip("Ultimate_Glow");
            var lk = new List<Keyframe>();
            for (float t = 0f; t <= end + 0.0001f; t += 1f / 60f)
            {
                float k = t < GlowStart ? 0f : t < BreakTime ? Mathf.Pow(Mathf.InverseLerp(GlowStart, BreakTime, t), 1.4f) : Mathf.Clamp01(1f - (t - BreakTime) / 0.1f);
                lk.Add(new Keyframe(t, s.glowLightIntensity * k));
            }
            AnimationUtility.SetEditorCurve(gclip, EditorCurveBinding.FloatCurve("Glow_Light", typeof(Light), "m_Intensity"), Curve(lk));
            var ga = AnimTrack(dir, tl, null, "FX_Glow_Anim", lightRig.GetComponent<Animator>());
            TimelineClip gc = ((AnimationTrack)ga).CreateClip(gclip);
            gc.start = 0d;
            gc.duration = end;
            gc.displayName = "Ultimate_Glow";

            // 발광 코어(나무 중심의 큰 빛이 폭발까지 차오름)
            var glowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FoxTex + "T_SoftGlow.png");
            Material mGlow = ParticleMat("M_UltimateGlow", glowTex, 2, 3031, Color.white);
            var coreGo = new GameObject("FX_GlowCore");
            coreGo.transform.SetParent(rig, false);
            coreGo.transform.position = center;
            ParticleSystem cps = NewPS("GlowCore", coreGo.transform, Vector3.zero, mGlow, 35);
            ParticleSystem.MainModule cm = cps.main;
            cm.duration = 1f; cm.startLifetime = Mathf.Max(0.2f, BreakTime - GlowStart); cm.startSpeed = 0f; cm.startSize = s.glowCoreSize; cm.startColor = s.glowColor;
            cm.simulationSpace = ParticleSystemSimulationSpace.World; cm.maxParticles = 4;
            ParticleSystem.EmissionModule ce = cps.emission; ce.rateOverTime = 0f; ce.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            ParticleSystem.ShapeModule csh = cps.shape; csh.enabled = false;
            ParticleSystem.SizeOverLifetimeModule csz = cps.sizeOverLifetime; csz.enabled = true;
            csz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(0.6f, 0.6f), new Keyframe(1f, 1f)));
            ParticleSystem.ColorOverLifetimeModule ccol = cps.colorOverLifetime; ccol.enabled = true;
            ccol.color = AlphaGradient((0f, 0f), (0.5f, 0.5f), (1f, 0.95f));
            coreGo.SetActive(false);
            var coreTrack = tl.CreateTrack(typeof(ControlTrack), null, "FX_Glow");
            ControlClip(dir, coreTrack, coreGo, GlowStart, BreakTime + 0.05f - GlowStart, 9u);

            // 화면 번쩍임: 특수스킬 컷신의 세기(휘두름 flashSwing, 폭발 breakFlash, 감쇠 flashHoldDecay)
            var canvasGo = new GameObject("UI_Canvas");
            canvasGo.transform.SetParent(rig, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasGo.AddComponent<Animator>();
            var flash = new GameObject("Flash");
            flash.transform.SetParent(canvasGo.transform, false);
            var img = flash.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = false;
            var rtf = flash.GetComponent<RectTransform>();
            rtf.anchorMin = Vector2.zero; rtf.anchorMax = Vector2.one; rtf.offsetMin = Vector2.zero; rtf.offsetMax = Vector2.zero;
            AnimationClip clip = NewClip("Ultimate_Screen");
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Flash", typeof(Image), "m_Color.a"),
                Keys((0f, 0f), (SwingHit - 0.02f, 0f), (SwingHit, s.flashSwing), (SwingHit + s.flashHoldDecay, 0f),
                    (BreakTime - 0.02f, 0f), (BreakTime, s.breakFlash), (BreakTime + s.flashHoldDecay + 0.1f, 0f), (end, 0f)));
            ColorRgb(clip, "Flash", typeof(Image), "m_Color", Color.white, end);
            var track = AnimTrack(dir, tl, null, "UI_Flash", canvasGo.GetComponent<Animator>());
            TimelineClip fc = ((AnimationTrack)track).CreateClip(clip);
            fc.start = 0d;
            fc.duration = end;
            fc.displayName = "Ultimate_Screen";
            sb.AppendLine("화면: 어두운 배경, 특수스킬과 같은 발광(볼륨 " + (glowProfile != null ? "VP_SpecialSkill_Glow" : "없음") + ", 점광원 " + s.glowLightIntensity + ", 코어 " + s.glowCoreSize + ", 플래시 " + s.breakFlareSize + ", 리본 발광 x" + (1f + s.burstGlowBoost) + ")");
        }

        private static void VolumeAnim(string name, VolumeProfile profile, float priority, TimelineAsset tl, PlayableDirector dir, Transform rig, AnimationCurve weight)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = priority;
            v.weight = 0f;
            v.sharedProfile = profile;
            go.AddComponent<Animator>();
            var t = (AnimationTrack)tl.CreateTrack(typeof(AnimationTrack), null, name);
            t.CreateInfiniteClip(name + "_Clip");
            AnimationUtility.SetEditorCurve(t.infiniteClip, EditorCurveBinding.FloatCurve("", typeof(Volume), "weight"), weight);
            dir.SetGenericBinding(t, go.GetComponent<Animator>());
        }
    }
}
