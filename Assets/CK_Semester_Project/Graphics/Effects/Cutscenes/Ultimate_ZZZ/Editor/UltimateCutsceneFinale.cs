using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;

namespace CK.CutsceneTools
{
    /// <summary>
    /// 난격 마무리 시퀀스 (지글거림 → 완성 → 라이팅 → 공간 깨짐 + 벚꽃잎)를 만든다. 에디터 전용.
    /// 그물은 원본 난격(PF_FoxPreSlash)의 참격 리본을 그대로 복제해 쓰고, 깨짐은 난격 중심의 월드 공간에서 일어난다.
    /// 호출할 때마다 관련 오브젝트·트랙·클립을 지우고 새로 만든다.
    /// </summary>
    public static class UltimateCutsceneFinale
    {
        private const string Root = "Assets/CK_Semester_Project/Graphics/Effects/Cutscenes/Ultimate_ZZZ/";
        private const string FoxTex = "Assets/CK_Semester_Project/Graphics/Effects/FoxPreSlash/Textures/";
        private const string FoxPrefab = "Assets/CK_Semester_Project/Graphics/Effects/FoxPreSlash/Prefabs/PF_FoxPreSlash.prefab";

        private static readonly List<AnimationClip> _made = new List<AnimationClip>();

        private static GameObject Find(string name) => UltimateCutsceneBuilder.Find(name);

        internal static void Kill(params string[] names)
        {
            foreach (string n in names)
            {
                GameObject g;
                while ((g = Find(n)) != null) Object.DestroyImmediate(g);
            }
        }

        internal static float Sm(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static AnimationCurve Curve(List<Keyframe> ks, bool stepped)
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

        internal static void DeleteTracks(TimelineAsset tl, params string[] names)
        {
            foreach (TrackAsset t in tl.GetRootTracks().Where(t => names.Contains(t.name)).ToList())
            {
                tl.DeleteTrack(t);
            }
        }

        internal static void WriteTexture(string path, int w, int h, System.Func<float, float, Color> fn, bool readable = false)
        {
            var tx = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = fn((x + 0.5f) / w, (y + 0.5f) / h);
            tx.SetPixels(px);
            tx.Apply();
            File.WriteAllBytes(path, tx.EncodeToPNG());
            Object.DestroyImmediate(tx);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var im = (TextureImporter)AssetImporter.GetAtPath(path);
            if (im != null)
            {
                im.mipmapEnabled = false;
                im.alphaIsTransparency = true;
                im.wrapMode = TextureWrapMode.Clamp;
                if (readable) { im.isReadable = true; im.textureCompression = TextureImporterCompression.Uncompressed; }
                im.SaveAndReimport();
            }
        }

        internal static Material ParticleMat(string name, Texture2D tex, int blend, int queue)
        {
            string p = Root + "Prefabs/" + name + ".mat";
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
            m.SetColor("_BaseColor", Color.white);
            m.renderQueue = queue;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static ParticleSystem.MinMaxGradient RandomOf(params Color[] cols)
        {
            var g = new Gradient();
            var keys = new GradientColorKey[cols.Length];
            for (int i = 0; i < cols.Length; i++) keys[i] = new GradientColorKey(cols[i], cols.Length == 1 ? 0f : (float)i / (cols.Length - 1));
            g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
        }

        internal static Gradient AlphaGradient(params (float t, float a)[] keys)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                keys.Select(k => new GradientAlphaKey(k.a, k.t)).ToArray());
            return g;
        }

        internal static void ControlClip(PlayableDirector dir, TrackAsset track, GameObject go, double start, double dur, uint seed)
        {
            var c = ((ControlTrack)track).CreateClip<ControlPlayableAsset>();
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

        private static AnimationClip NewClip(string name)
        {
            string p = Root + "Animation/" + name + ".anim";
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

        internal static ParticleSystem NewPS(string name, Transform parent, Vector3 localPos, Material mat, int order)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            var ps = g.AddComponent<ParticleSystem>();
            var m = ps.main; m.playOnAwake = false; m.loop = false;
            var r = g.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.sortingOrder = order;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 20f;
            return ps;
        }

        /// <summary>
        /// 1차 공격의 가로 슬래시가 화면 중앙을 가른 뒤, 화면 중앙을 기점으로 어두운 배경이 원형으로 퍼져 나가게 한다.
        /// 카메라의 자식으로 먼 곳(기본 40m)에 둔 원형 그라데이션 하나가 커진다. 월드 깊이 테스트를 그대로 받으므로 캐릭터·바닥·이펙트 뒤(= 하늘/배경)만 어두워진다.
        /// </summary>
        public static void ApplyDarkSpread(PlayableDirector dir, TimelineAsset tl, UltimateCutsceneStyle s, float end, StringBuilder sb)
        {
            Kill("FX_DarkSpread");
            DeleteTracks(tl, "FX_DarkSpread");
            Transform camT = Find("CM_Camera_02")?.transform;
            if (!s.enableDarkSpread || camT == null)
            {
                return;
            }

            WriteTexture(Root + "UI/T_DarkRadial.png", 512, 512, (u, v) =>
            {
                float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                return new Color(1f, 1f, 1f, 1f - Sm(0.38f, 0.98f, r));
            });
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "UI/T_DarkRadial.png");
            Material mat = ParticleMat("M_UltDarkSpread", tex, 0, 2999);

            float tIn = s.darkSpreadTime.x;
            float spread = Mathf.Max(0.05f, s.darkSpreadTime.y - s.darkSpreadTime.x);
            float outEnd = s.fogOut.y;
            float life = Mathf.Max(spread + 0.5f, outEnd - tIn + 0.1f);
            float D = s.darkSpreadDistance;

            var go = new GameObject("FX_DarkSpread");
            go.transform.SetParent(camT, false);
            go.transform.localPosition = new Vector3(0f, 0f, D);
            go.transform.localRotation = Quaternion.identity;
            var ps = NewPS("DarkSpread", go.transform, Vector3.zero, mat, -40);
            ps.GetComponent<ParticleSystemRenderer>().maxParticleSize = 500f;
            var m = ps.main;
            m.duration = life; m.startLifetime = life; m.startSpeed = 0f; m.startSize = D * 4.5f;
            Color c = s.darkSpreadColor; c.a = 1f; m.startColor = c;
            m.simulationSpace = ParticleSystemSimulationSpace.Local; m.maxParticles = 1;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var sh = ps.shape; sh.enabled = false;
            var szl = ps.sizeOverLifetime; szl.enabled = true;
            var sk = new List<Keyframe>();
            for (int i = 0; i <= 16; i++)
            {
                float u = i / 16f;
                sk.Add(new Keyframe(u * spread / life, 0.03f + 0.97f * (u * u * (3f - 2f * u))));
            }
            sk.Add(new Keyframe(1f, 1f));
            szl.size = new ParticleSystem.MinMaxCurve(1f, Curve(sk, false));
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = AlphaGradient((0f, 0f), (0.25f / life, s.darkSpreadAlpha), ((s.fogOut.x - tIn) / life, s.darkSpreadAlpha), ((outEnd - tIn) / life, 0f), (1f, 0f));
            go.SetActive(false);

            var tr = tl.CreateTrack(typeof(ControlTrack), null, "FX_DarkSpread");
            ControlClip(dir, tr, go, tIn, Mathf.Min(life, end - tIn), 77u);
            sb.AppendLine("어두운 배경 퍼짐: 화면 중앙 기점 " + tIn.ToString("F2") + "~" + (tIn + spread).ToString("F2") + "s, " + s.fogOut.x.ToString("F2") + "s 부터 걷힘");
        }

        public static void Apply(PlayableDirector dir, TimelineAsset tl, UltimateCutsceneStyle s, float land, float end, StringBuilder sb)
        {
            Transform fx = Find("FX")?.transform;
            if (fx == null) return;
            Kill("FX_Web", "FX_GlowRig", "FX_GlowCore", "Volume_Glow", "UI_BreakCanvas", "FX_BreakShards", "FX_BreakPetals", "FX_SpaceBreak", "FX_SlashBurst");
            DeleteTracks(tl, "Finale_Web_MuteToDisable", "Finale_Glow", "Finale_ScreenBreak_MuteToDisable", "Finale_Burst_MuteToDisable", "Volume_Glow", "FX_Glow_Anim", "FX_Glow");

            float tWeb0 = land + s.webFirstLine;
            float tSizzle = land + s.sizzleStart;
            float tComplete = land + s.completeTime;
            float tGlow = land + s.glowStart;
            float tBreak = land + s.breakTime;
            Vector3 center = s.pourCameraLook + new Vector3(0f, 0.2f, 0f);
            Vector3 camPos = s.pourCameraPos;
            Quaternion facing = Quaternion.LookRotation(center - camPos, Vector3.up);
            Quaternion toCam = Quaternion.LookRotation(camPos - center, Vector3.up);
            var rnd = new System.Random(s.webSeed);
            float R(float a, float b) => (float)(a + (b - a) * rnd.NextDouble());

            // ---------------------------------------------------------- textures / materials
            var texGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(FoxTex + "T_SoftGlow.png");
            Material mGlow = ParticleMat("M_UltGlowCore", texGlow, 2, 3031);

            // ---------------------------------------------------------- web: original 난격 ribbons, held and sizzling
            var web = new GameObject("FX_Web");
            web.transform.SetParent(fx, false);
            web.transform.position = center;
            web.transform.rotation = facing;
            web.transform.localScale = Vector3.one * s.webScale;
            web.AddComponent<Animator>();
            var ribbons = new GameObject("Ribbons");
            ribbons.transform.SetParent(web.transform, false);
            var layout = new GameObject("Layout");
            layout.transform.SetParent(ribbons.transform, false);

            var srcAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FoxPrefab);
            var temp = Object.Instantiate(srcAsset);
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            temp.transform.localScale = Vector3.one;
            var list = new List<(ParticleSystem ps, float delay, Vector3 pos, Quaternion rot, Vector3 scl)>();
            var motions = new Dictionary<ParticleSystem, Motion>();
            foreach (ParticleSystem ps in temp.GetComponentsInChildren<ParticleSystem>(true))
            {
                var pr = ps.GetComponent<ParticleSystemRenderer>();
                if (pr == null || pr.renderMode != ParticleSystemRenderMode.Mesh || ps.main.startDelay.constantMax > 1.0f)
                {
                    continue;
                }
                motions[ps] = CaptureMotion(ps);
                list.Add((ps, ps.main.startDelay.constantMax, ps.transform.position, ps.transform.rotation, ps.transform.lossyScale));
            }
            list = list.OrderBy(x => x.delay).ThenBy(x => x.ps.name).ToList();
            // 리본 메시의 실제 꼭짓점(XY 투영)으로 배치의 시각적 중심과 각 리본의 중심선을 구한다.
            // 이 중심선이 그대로 공간의 금이 되므로, 금이 참격이 지나간 자리를 따라간다.
            var pts = new List<List<Vector2>>();
            Vector2 bmin = new Vector2(1e9f, 1e9f), bmax = new Vector2(-1e9f, -1e9f);
            foreach (var x in list)
            {
                var mesh = x.ps.GetComponent<ParticleSystemRenderer>().mesh;
                var one = new List<Vector2>();
                Pose(x.ps, motions[x.ps], out Vector3 pPos, out float pRot);   // 원본의 이동·회전이 끝난 뒤의 최종 자세
                Vector3[] vs = mesh != null && mesh.isReadable ? mesh.vertices : null;
                if (vs == null || vs.Length == 0)
                {
                    Bounds mb = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                    vs = new[] { mb.min, mb.max, new Vector3(mb.min.x, mb.max.y, 0f), new Vector3(mb.max.x, mb.min.y, 0f) };
                }
                foreach (Vector3 mv in vs)
                {
                    Vector3 wp = x.pos + x.rot * Vector3.Scale(Quaternion.Euler(0f, 0f, pRot) * mv + pPos, x.scl);
                    one.Add(new Vector2(wp.x, wp.y));
                    bmin = Vector2.Min(bmin, new Vector2(wp.x, wp.y));
                    bmax = Vector2.Max(bmax, new Vector2(wp.x, wp.y));
                }
                pts.Add(one);
            }
            Vector3 avg = list.Count > 0 ? new Vector3((bmin.x + bmax.x) * 0.5f, (bmin.y + bmax.y) * 0.5f, 0f) : Vector3.zero;
            var centerlines = new List<Vector2[]>();
            foreach (List<Vector2> one in pts)
            {
                centerlines.Add(Centerline(one, new Vector2(avg.x, avg.y)));
            }
            Vector2 extent = bmax - bmin;

            // 원본의 나머지 레이어(교차 섬광·헤이즈·스파크 등)가 복제한 리본과 같은 좌표계에서 맞물리도록 FX_Combo 를 그물과 같은 위치·방향·크기로 맞춘다.
            GameObject comboPivot = Find("FX_Combo_Pivot"), comboObj = Find("FX_Combo");
            if (comboPivot != null && comboObj != null)
            {
                comboPivot.transform.rotation = facing;
                comboPivot.transform.position = center - facing * (new Vector3(avg.x, avg.y, 0f) * s.webScale);
                comboObj.transform.localPosition = Vector3.zero;
                comboObj.transform.localRotation = Quaternion.identity;
                comboObj.transform.localScale = Vector3.one * s.webScale;
            }

            var clip = NewClip("ULT_Web");
            float fps = s.sizzleFps;
            float step = 1f / fps;
            float last = tBreak + s.burstFadeTime + 0.03f;
            float maxDelay = list.Count > 0 ? list.Max(x => x.delay) : 0f;
            for (int i = 0; i < list.Count; i++)
            {
                var it = list[i];
                GameObject rg = it.ps.gameObject;
                rg.name = "R" + i.ToString("00") + "_" + rg.name;
                rg.transform.SetParent(layout.transform, false);
                Vector3 basePos = it.pos - avg;
                float baseRot = it.rot.eulerAngles.z;
                rg.transform.localPosition = basePos;
                rg.transform.localRotation = it.rot;
                rg.transform.localScale = it.scl;
                string path = "Ribbons/Layout/" + rg.name;

                float appear = list[i].delay * (maxDelay > 0.001f ? s.webBuildTime / maxDelay : 1f);   // 원본 시작 타이밍 그대로
                float absAppear = tWeb0 + appear;
                float origLife = motions[it.ps].L0;
                // 기본은 원본 난격 그대로(리본마다 원래 수명만 보였다 사라진다). 쌓아서 터뜨리는 방식은 webHoldUntilBurst 를 켠다.
                bool holdLong = s.webHoldUntilBurst;
                float life = holdLong ? Mathf.Max(0.2f, last - absAppear) : origLife + Mathf.Max(0f, s.webExtraLife);
                ParticleSystem ps = it.ps;
                ConfigureMotion(ps, motions[ps], life);
                var m = ps.main;
                m.playOnAwake = false; m.loop = false;
                m.startDelay = appear;
                m.startLifetime = life;
                m.duration = appear + life + 0.05f;
                // 원본의 크기 변화(Size over Lifetime)는 그대로 둔다: 수명이 늘어난 만큼 천천히 점점 커진다.
                if (holdLong)
                {
                    var col = ps.colorOverLifetime; col.enabled = true;
                    float fade = Mathf.Min(0.5f, s.burstFadeTime / life);
                    var rm = ps.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                    if (rm != null && rm.name.StartsWith("M_Core"))
                    {
                        // 흰 코어 리본(Glow 84)은 쌓아 두면 화면이 하얗게 날아간다: 낮게 깔아 두었다가 라이팅 시작부터 터지는 순간까지 점점 밝힌다.
                        float h = Mathf.Clamp01(s.webCoreHoldAlpha); float pk = Mathf.Clamp01(Mathf.Max(h, s.webCorePeakAlpha));
                        float kG = Mathf.Clamp01((tGlow - absAppear) / life);
                        float kB = Mathf.Clamp(Mathf.InverseLerp(absAppear, absAppear + life, tBreak), kG + 0.02f, 1f - fade);
                        float kM = Mathf.Lerp(kG, kB, 0.6f);
                        col.color = AlphaGradient((0f, h), (kG, h), (kM, Mathf.Lerp(h, pk, 0.3f)), (kB, pk), (1f - fade, pk), (1f, 0f));
                    }
                    else col.color = AlphaGradient((0f, 1f), (1f - fade, 1f), (1f, 0f));
                }
                var rr = ps.GetComponent<ParticleSystemRenderer>();
                rr.sortingOrder = 30;
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // sizzle: stepped jitter of position / rotation / scale
                var px = new List<Keyframe>(); var py = new List<Keyframe>(); var rz = new List<Keyframe>();
                var sxk = new List<Keyframe>(); var syk = new List<Keyframe>();
                px.Add(new Keyframe(0f, basePos.x)); py.Add(new Keyframe(0f, basePos.y)); rz.Add(new Keyframe(0f, baseRot));
                sxk.Add(new Keyframe(0f, it.scl.x)); syk.Add(new Keyframe(0f, it.scl.y));
                float tStart = Mathf.Max(absAppear - 0.02f, 0.01f);
                for (float t = tStart; t <= last; t += step)
                {
                    float amp;
                    if (t < tSizzle) amp = 0.12f;
                    else if (t < tComplete) amp = Mathf.Lerp(0.55f, 1f, Mathf.Sin(Mathf.PI * Mathf.InverseLerp(tSizzle, tComplete, t)));
                    else if (t < tBreak - 0.3f) amp = 0.12f;
                    else if (t >= tBreak - 0.06f) amp = 0f;
                    else amp = Mathf.Lerp(0.12f, 0.7f, Mathf.InverseLerp(tBreak - 0.3f, tBreak - 0.06f, t));
                    float a = amp * s.sizzleAmplitude;
                    float pulse = 1f + R(-1f, 1f) * s.sizzleScalePulse * amp;
                    if (t >= tComplete && t < tComplete + 0.1f) pulse *= s.completePop;
                    px.Add(new Keyframe(t, basePos.x + R(-a, a)));
                    py.Add(new Keyframe(t, basePos.y + R(-a, a)));
                    rz.Add(new Keyframe(t, baseRot + R(-1f, 1f) * s.sizzleRotation * amp));
                    sxk.Add(new Keyframe(t, it.scl.x * pulse));
                    syk.Add(new Keyframe(t, it.scl.y * pulse * (holdLong && t > tBreak ? Mathf.Lerp(1f, s.webBurstThin, Mathf.InverseLerp(tBreak, last, t)) : 1f)));
                }
                px.Add(new Keyframe(last + step, basePos.x)); py.Add(new Keyframe(last + step, basePos.y)); rz.Add(new Keyframe(last + step, baseRot));
                sxk.Add(new Keyframe(last + step, it.scl.x)); syk.Add(new Keyframe(last + step, it.scl.y));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.x"), Curve(px, true));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.y"), Curve(py, true));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw.z"), Curve(rz, true));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale.x"), Curve(sxk, true));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale.y"), Curve(syk, true));

                // 발광: 원본 머티리얼의 모양·색은 그대로 두고 _GlowIntensity 만 올려, 쌓일수록 밝아지다 터지는 순간 가장 강하게 빛난다.
                Material gm = rr.sharedMaterial;
                if (holdLong && gm != null && gm.HasProperty("_GlowIntensity"))
                {
                    float g0 = gm.GetFloat("_GlowIntensity");
                    var gk = new List<Keyframe>
                    {
                        new Keyframe(0f, g0), new Keyframe(tGlow, g0),
                        new Keyframe(tBreak, g0 * (1f + s.burstGlowBoost)),
                        new Keyframe(last, g0 * (1f + s.burstGlowBoost * 1.8f)),
                        new Keyframe(last + step, g0),
                    };
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(ParticleSystemRenderer), "material._GlowIntensity"), Curve(gk, false));
                }
            }
            Object.DestroyImmediate(temp);
            ribbons.SetActive(false);
            var webGroup = (GroupTrack)tl.CreateTrack(typeof(GroupTrack), null, "Finale_Web_MuteToDisable");
            var webAnim = (AnimationTrack)tl.CreateTrack(typeof(AnimationTrack), webGroup, "FX_Web_Anim");
            var wc = webAnim.CreateClip(clip);
            wc.start = 0; wc.duration = end; wc.displayName = "ULT_Web";
            dir.SetGenericBinding(webAnim, web.GetComponent<Animator>());
            var webCtl = tl.CreateTrack(typeof(ControlTrack), webGroup, "FX_Web");
            ControlClip(dir, webCtl, ribbons, tWeb0 - 0.02f, last + 0.1f - (tWeb0 - 0.02f), 5u);
            webGroup.muted = !s.enableWeb;

            // ---------------------------------------------------------- lighting glow
            const string vpPath = Root + "Volumes/VP_Ult_Glow.asset";
            var vp = AssetDatabase.LoadAssetAtPath<VolumeProfile>(vpPath);
            if (vp == null) { vp = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(vp, vpPath); }
            if (!vp.TryGet(out Bloom bl)) bl = vp.Add<Bloom>(true);
            bl.threshold.Override(0.7f); bl.intensity.Override(s.glowBloomIntensity); bl.scatter.Override(0.75f); bl.tint.Override(s.glowColor);
            if (!vp.TryGet(out ColorAdjustments ca)) ca = vp.Add<ColorAdjustments>(true);
            ca.postExposure.Override(s.glowExposure); ca.colorFilter.Override(Color.Lerp(Color.white, s.glowColor, 0.6f));
            EditorUtility.SetDirty(vp);
            var vol = new GameObject("Volume_Glow");
            vol.transform.SetParent(dir.transform, false);
            var v = vol.AddComponent<Volume>(); v.isGlobal = true; v.priority = 8f; v.weight = 0f; v.sharedProfile = vp;
            vol.AddComponent<Animator>();
            var glowGroup = (GroupTrack)tl.CreateTrack(typeof(GroupTrack), null, "Finale_Glow");
            var vt = (AnimationTrack)tl.CreateTrack(typeof(AnimationTrack), glowGroup, "Volume_Glow");
            vt.CreateInfiniteClip("Volume_Glow_Clip");
            var wk = new List<Keyframe>();
            for (float t = 0f; t <= end + 0.0001f; t += 1f / 60f)
            {
                float w = 0.75f * (t < tGlow ? 0f : t < tBreak ? Mathf.Pow(Mathf.InverseLerp(tGlow, tBreak, t), 1.6f) : Mathf.Clamp01(1f - (t - tBreak) / 0.12f));
                wk.Add(new Keyframe(t, w));
            }
            AnimationUtility.SetEditorCurve(vt.infiniteClip, EditorCurveBinding.FloatCurve("", typeof(Volume), "weight"), Curve(wk, false));
            dir.SetGenericBinding(vt, vol.GetComponent<Animator>());

            var rig = new GameObject("FX_GlowRig");
            rig.transform.SetParent(fx, false);
            rig.AddComponent<Animator>();
            var lg = new GameObject("Glow_Light");
            lg.transform.SetParent(rig.transform, false);
            lg.transform.position = Vector3.Lerp(center, camPos, 0.25f);
            var li = lg.AddComponent<Light>();
            li.type = LightType.Point; li.color = s.glowColor; li.range = 12f; li.intensity = 0f; li.shadows = LightShadows.None;
            var gclip = NewClip("ULT_Glow");
            var lk = new List<Keyframe>();
            for (float t = 0f; t <= end + 0.0001f; t += 1f / 60f)
            {
                float k = t < tGlow ? 0f : t < tBreak ? Mathf.Pow(Mathf.InverseLerp(tGlow, tBreak, t), 1.4f) : Mathf.Clamp01(1f - (t - tBreak) / 0.1f);
                lk.Add(new Keyframe(t, s.glowLightIntensity * k));
            }
            AnimationUtility.SetEditorCurve(gclip, EditorCurveBinding.FloatCurve("Glow_Light", typeof(Light), "m_Intensity"), Curve(lk, false));
            var ga = (AnimationTrack)tl.CreateTrack(typeof(AnimationTrack), glowGroup, "FX_Glow_Anim");
            var gc = ga.CreateClip(gclip); gc.start = 0; gc.duration = end; gc.displayName = "ULT_Glow";
            dir.SetGenericBinding(ga, rig.GetComponent<Animator>());
            var core = new GameObject("FX_GlowCore");
            core.transform.SetParent(fx, false);
            core.transform.position = center;
            core.transform.rotation = toCam;
            var cps = NewPS("GlowCore", core.transform, Vector3.zero, mGlow, 35);
            var cm = cps.main; cm.duration = 1f; cm.startLifetime = Mathf.Max(0.2f, tBreak - tGlow); cm.startSpeed = 0f; cm.startSize = s.glowCoreSize; cm.startColor = s.glowColor; cm.simulationSpace = ParticleSystemSimulationSpace.World; cm.maxParticles = 4;
            var ce = cps.emission; ce.rateOverTime = 0f; ce.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var csh = cps.shape; csh.enabled = false;
            var csz = cps.sizeOverLifetime; csz.enabled = true; csz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(0.6f, 0.6f), new Keyframe(1f, 1f)));
            var ccol = cps.colorOverLifetime; ccol.enabled = true; ccol.color = AlphaGradient((0f, 0f), (0.5f, 0.5f), (1f, 0.95f));
            core.SetActive(false);
            var glowCtl = tl.CreateTrack(typeof(ControlTrack), glowGroup, "FX_Glow");
            ControlClip(dir, glowCtl, core, tGlow, tBreak + 0.05f - tGlow, 9u);

            // ---------------------------------------------------------- burst: 쌓인 참격이 발광하며 터지고, 사쿠라 슬래시의 벚꽃잎이 참격 선 위에서 흩날린다
            var brk = new GameObject("FX_SlashBurst");
            brk.transform.SetParent(fx, false);
            brk.transform.position = center;
            brk.transform.rotation = toCam;
            var bcore = new GameObject("Core");
            bcore.transform.SetParent(brk.transform, false);

            // 폭발 섬광: 큰 빛 + 짧고 강한 코어
            var flare = NewPS("BurstFlare", bcore.transform, new Vector3(0f, 0f, 0.2f), mGlow, 44);
            var fm = flare.main; fm.duration = 0.7f; fm.startLifetime = 0.55f; fm.startSpeed = 0f; fm.startSize = s.breakFlareSize; fm.startColor = s.glowColor; fm.simulationSpace = ParticleSystemSimulationSpace.World; fm.maxParticles = 2;
            var fe = flare.emission; fe.rateOverTime = 0f; fe.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var fsh = flare.shape; fsh.enabled = false;
            var fsz = flare.sizeOverLifetime; fsz.enabled = true; fsz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.2f, 1f), new Keyframe(1f, 1.2f)));
            var fcol = flare.colorOverLifetime; fcol.enabled = true; fcol.color = AlphaGradient((0f, 1f), (0.2f, 0.9f), (1f, 0f));
            var core2 = NewPS("BurstCore", bcore.transform, new Vector3(0f, 0f, 0.3f), mGlow, 46);
            var cm2 = core2.main; cm2.duration = 0.4f; cm2.startLifetime = 0.3f; cm2.startSpeed = 0f; cm2.startSize = s.breakFlareSize * 0.45f; cm2.startColor = Color.white; cm2.simulationSpace = ParticleSystemSimulationSpace.World; cm2.maxParticles = 2;
            var ce2 = core2.emission; ce2.rateOverTime = 0f; ce2.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var csh2 = core2.shape; csh2.enabled = false;
            var csz2 = core2.sizeOverLifetime; csz2.enabled = true; csz2.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.3f)));
            var ccol2 = core2.colorOverLifetime; ccol2.enabled = true; ccol2.color = AlphaGradient((0f, 1f), (1f, 0f));
            bcore.SetActive(false);

            // 벚꽃잎: 타임라인의 다른 벚꽃잎(원본 난격 PF_FoxPreSlash 의 꽃잎 파티클)과 같은 것을 그대로 복제해 참격 선 위 여러 지점에서 터뜨린다.
            // 머티리얼·크기·색·회전·움직임은 원본 그대로(크기만 petalScale 배율), 떨어지는 느낌만 중력으로 더한다.
            var petalGo = new GameObject("Petals");
            petalGo.transform.SetParent(brk.transform, false);
            int petalSets = 0;
            var foxTmp = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FoxPrefab));
            string[] petalNames = { "F00_Existing_Petals", "F08_Cut_Petals", "F24_Drifting_Petals", "F36_Last_Petals" };
            var petalSrc = new List<ParticleSystem>();
            foreach (ParticleSystem pp in foxTmp.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (System.Array.IndexOf(petalNames, pp.name) >= 0) petalSrc.Add(pp);
            }
            var spots = new List<Vector2>();
            foreach (Vector2[] cl in centerlines) foreach (Vector2 q in cl) spots.Add(q);
            for (int i = spots.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (spots[i], spots[j]) = (spots[j], spots[i]); }
            int groups = Mathf.Min(s.petalGroups, spots.Count);
            for (int gi = 0; gi < groups; gi++)
            {
                var grp = new GameObject("Petals_" + gi.ToString("00"));
                grp.transform.SetParent(petalGo.transform, false);
                grp.transform.position = web.transform.TransformPoint(new Vector3(spots[gi].x, spots[gi].y, 0f));
                grp.transform.rotation = facing;
                foreach (ParticleSystem src in petalSrc)
                {
                    GameObject c = Object.Instantiate(src.gameObject, grp.transform);
                    c.name = src.name;
                    c.SetActive(true);
                    c.transform.localPosition = new Vector3(R(-0.5f, 0.5f), R(-0.5f, 0.5f), 0f);
                    c.transform.localRotation = Quaternion.identity;
                    var cps2 = c.GetComponent<ParticleSystem>();
                    var pmn = cps2.main;
                    pmn.playOnAwake = false;
                    pmn.startDelay = R(0f, 0.25f);
                    pmn.startSizeMultiplier *= s.petalScale;
                    pmn.gravityModifier = s.petalGravity;
                    pmn.startLifetimeMultiplier *= s.petalLifeMul;
                    var prr = c.GetComponent<ParticleSystemRenderer>();
                    if (prr != null) prr.sortingOrder = 48;
                }
                petalSets++;
            }
            Object.DestroyImmediate(foxTmp);
            petalGo.SetActive(false);

            var breakGroup = (GroupTrack)tl.CreateTrack(typeof(GroupTrack), null, "Finale_Burst_MuteToDisable");
            var coreTrack = tl.CreateTrack(typeof(ControlTrack), breakGroup, "FX_BurstFlare");
            ControlClip(dir, coreTrack, bcore, tBreak, Mathf.Min(0.8f, end - tBreak), 21u);
            var petalTrack = tl.CreateTrack(typeof(ControlTrack), breakGroup, "FX_BurstPetals");
            ControlClip(dir, petalTrack, petalGo, tBreak, Mathf.Min(1.4f, end - tBreak), 33u);
            breakGroup.muted = !s.enableBurst;
            petalTrack.muted = !s.enableBreakPetals;
            sb.AppendLine("벚꽃잎: 원본 난격(Fox) 꽃잎 파티클 " + petalSrc.Count + "종 x " + petalSets + "곳을 참격 선 위에 배치");

            foreach (AnimationClip mc in _made) { EditorUtility.SetDirty(mc); AssetDatabase.SaveAssetIfDirty(mc); }
            _made.Clear();
            sb.AppendLine("마무리: 원본 난격 리본 " + list.Count + "개 복제 " + tWeb0.ToString("F2") + "~" + (tWeb0 + s.webBuildTime).ToString("F2") + "s, 지글 " + tSizzle.ToString("F2") + "s, 완성 " + tComplete.ToString("F2") + "s, 라이팅 " + tGlow.ToString("F2") + "s, 폭발 " + tBreak.ToString("F2") + "s (폭발 " + (s.enableBurst ? "ON" : "OFF") + ", 벚꽃잎 " + (s.enableBreakPetals ? "ON" : "OFF") + ")");
        }

        // 원본 리본은 수명(0.1~0.4초) 동안 Velocity/Rotation over Lifetime 으로 휩쓸리듯 이동·회전한다.
        // 수명을 늘려 붙잡아 두면 그 움직임까지 같이 늘어나 슬래시가 계속 흘러가 버리므로, 원본 수명 안에서 일어나던 움직임을 앞쪽으로 압축하고 이후에는 멈춘다.
        private struct Motion
        {
            public bool hasV, hasR;
            public ParticleSystem.MinMaxCurve vx, vy, vz, rx, ry, rz;
            public float L0;
        }

        private static Motion CaptureMotion(ParticleSystem ps)
        {
            var mo = new Motion { L0 = Mathf.Max(0.05f, ps.main.startLifetime.constantMax) };
            var v = ps.velocityOverLifetime; mo.hasV = v.enabled; mo.vx = v.x; mo.vy = v.y; mo.vz = v.z;
            var r = ps.rotationOverLifetime; mo.hasR = r.enabled; mo.rx = r.x; mo.ry = r.y; mo.rz = r.z;
            return mo;
        }

        private static ParticleSystem.MinMaxCurve Squash(ParticleSystem.MinMaxCurve c, float k)
        {
            float tail = Mathf.Min(1f, k + 0.002f);
            var keys = new List<Keyframe>();
            if (c.mode == ParticleSystemCurveMode.Curve || c.mode == ParticleSystemCurveMode.TwoCurves)
            {
                AnimationCurve src = c.mode == ParticleSystemCurveMode.Curve ? c.curve : c.curveMax;
                for (int i = 0; i <= 24; i++)
                {
                    float u = i / 24f;
                    keys.Add(new Keyframe(u * k, src.Evaluate(u)));
                }
                keys.Add(new Keyframe(tail, 0f));
                return new ParticleSystem.MinMaxCurve(c.curveMultiplier, Curve(keys, false));
            }
            float val = c.mode == ParticleSystemCurveMode.TwoConstants ? (c.constantMin + c.constantMax) * 0.5f : c.constant;
            keys.Add(new Keyframe(0f, 1f)); keys.Add(new Keyframe(k, 1f)); keys.Add(new Keyframe(tail, 0f));
            return new ParticleSystem.MinMaxCurve(val, Curve(keys, false));
        }

        private static void ConfigureMotion(ParticleSystem ps, Motion mo, float life)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.startDelay = 0f;
            m.startLifetime = life;
            m.duration = life + 0.1f;
            float k = life <= mo.L0 * 1.02f ? 1f : Mathf.Min(0.95f, mo.L0 / life);
            if (mo.hasV)
            {
                var v = ps.velocityOverLifetime;
                v.x = Squash(mo.vx, k); v.y = Squash(mo.vy, k); v.z = Squash(mo.vz, k);
            }
            if (mo.hasR)
            {
                var r = ps.rotationOverLifetime;
                r.x = Squash(mo.rx, k); r.y = Squash(mo.ry, k); r.z = Squash(mo.rz, k);
            }
        }

        /// <summary>원본 이동·회전이 끝난 뒤 리본(입자)의 위치와 Z 회전. 미리 시뮬레이션해서 읽는다.</summary>
        private static void Pose(ParticleSystem ps, Motion mo, out Vector3 pos, out float rotZ)
        {
            ConfigureMotion(ps, mo, 1.5f);
            ps.Simulate(mo.L0 + 0.01f, true, true, true);
            var arr = new ParticleSystem.Particle[4];
            int n = ps.GetParticles(arr);
            pos = n > 0 ? arr[0].position : Vector3.zero;
            rotZ = n > 0 ? arr[0].rotation3D.z : 0f;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>리본 꼭짓점 → 중심선. 주축(PCA)을 따라 8개 구간의 평균점을 이어 만든다(휘어진 리본도 따라간다).</summary>
        private static Vector2[] Centerline(List<Vector2> p, Vector2 avg)
        {
            if (p.Count < 2) return new[] { p[0] - avg, p[0] - avg + Vector2.right };
            Vector2 m = Vector2.zero;
            foreach (Vector2 v in p) m += v;
            m /= p.Count;
            float cxx = 0f, cyy = 0f, cxy = 0f;
            foreach (Vector2 v in p) { Vector2 d = v - m; cxx += d.x * d.x; cyy += d.y * d.y; cxy += d.x * d.y; }
            float th = 0.5f * Mathf.Atan2(2f * cxy, cxx - cyy);
            Vector2 ax = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
            float tmin = 1e9f, tmax = -1e9f;
            Vector2 pmin = m, pmax = m;
            foreach (Vector2 v in p)
            {
                float t = Vector2.Dot(v - m, ax);
                if (t < tmin) { tmin = t; pmin = v; }
                if (t > tmax) { tmax = t; pmax = v; }
            }
            const int nb = 8;
            var sum = new Vector2[nb]; var cnt = new int[nb];
            foreach (Vector2 v in p)
            {
                float t = Vector2.Dot(v - m, ax);
                int bi = Mathf.Clamp((int)((t - tmin) / (tmax - tmin + 1e-5f) * nb), 0, nb - 1);
                sum[bi] += v; cnt[bi]++;
            }
            var poly = new List<Vector2> { pmin - avg };
            for (int i = 0; i < nb; i++) if (cnt[i] > 0) poly.Add(sum[i] / cnt[i] - avg);
            poly.Add(pmax - avg);
            return poly.ToArray();
        }
    }
}
