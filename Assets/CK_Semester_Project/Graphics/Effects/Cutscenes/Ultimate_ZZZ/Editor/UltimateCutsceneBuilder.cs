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
    /// UltimateCutsceneStyle 의 값을 워크스페이스 씬의 컷신 인스턴스(PF_Ultimate_CutsceneRoot)에 적용하고 프리팹에 Apply 한다.
    /// 에디터 전용. 휘두르는 시각(SW)·착지 시각은 Caster 트랙의 Take_B* 클립에서 읽어 오므로 모션 타이밍을 바꿔도 따라간다.
    /// </summary>
    public static class UltimateCutsceneBuilder
    {
        private const string Root = "Assets/CK_Semester_Project/Graphics/Effects/Cutscenes/Ultimate_ZZZ/";
        private const string FoxTex = "Assets/CK_Semester_Project/Graphics/Effects/FoxPreSlash/Textures/";

        [MenuItem("CK/Ultimate Cutscene/Apply Default Style")]
        private static void ApplyDefaultMenu()
        {
            string[] guids = AssetDatabase.FindAssets("t:UltimateCutsceneStyle");
            if (guids.Length == 0)
            {
                Debug.LogWarning("UltimateCutsceneStyle 에셋이 없습니다. Project 창에서 CK/Ultimate Cutscene Style 로 만드세요.");
                return;
            }
            ApplyAll(AssetDatabase.LoadAssetAtPath<UltimateCutsceneStyle>(AssetDatabase.GUIDToAssetPath(guids[0])));
        }

        public static string ApplyAll(UltimateCutsceneStyle s, bool applyToPrefab = true)
        {
            var sb = new StringBuilder();
            PlayableDirector dir = null;
            foreach (PlayableDirector d in Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (d.playableAsset != null && d.playableAsset.name.StartsWith("TL_Ultimate") && !d.playableAsset.name.EndsWith("Calm"))
                {
                    dir = d;
                }
            }
            if (dir != null)
            {
                _scene = dir.gameObject.scene;
            }
            if (dir == null || !(dir.playableAsset is TimelineAsset tl))
            {
                const string msg = "컷신 인스턴스(PlayableDirector)를 열린 씬에서 찾지 못했습니다. SC_Ultimate_Workspace 씬을 여세요.";
                Debug.LogWarning(msg);
                return msg;
            }

            if (!Derive(tl, out float[] sw, out float land))
            {
                const string msg = "Caster 트랙의 Take_B* 클립을 찾지 못했습니다.";
                Debug.LogWarning(msg);
                return msg;
            }
            float oldEnd = (float)tl.GetRootTracks().Where(t => t.name == "Cam").SelectMany(t => t.GetClips()).Select(c => c.end).DefaultIfEmpty(7.4).Max();
            float end = land + s.breakTime + s.returnStartAfterBreak + s.returnDuration + s.endHold;
            // 포그·룩 볼륨이 걷히는 시각은 폭발 시각에 맞춰 자동으로 정한다(착지 시각이 바뀌어도 따라간다).
            float tBreakAbs = land + s.breakTime;
            s.fogOut = new Vector2(tBreakAbs, tBreakAbs + 0.48f);
            s.lookVolumeTimes = new Vector4(s.lookVolumeTimes.x, s.lookVolumeTimes.y, tBreakAbs, tBreakAbs + 0.48f);
            EditorUtility.SetDirty(s);
            ExtendClips(tl, oldEnd, end);

            ApplyScales(s, sb);
            ApplyPour(dir, tl, s, land, sb);
            ApplyFog(dir, tl, s, end, sb);
            UltimateCutsceneFinale.ApplyDarkSpread(dir, tl, s, end, sb);
            ApplyGhosts(dir, tl, s, end, sb);
            ApplyHitColors(s, sb);
            ApplyLook(dir, tl, s, end, sb);
            ApplyUI(tl, s, land, end, sb);
            ApplyHitPulses(tl, s, sw, land, end, sb);
            ApplyCamera(s, sw, land, end, sb);
            ApplyShake(s, sw, land, end, sb);
            ApplyFlash(s, sw, land, end, sb);
            UltimateCutsceneFinale.Apply(dir, tl, s, land, end, sb);
            TrimInfinite(tl, end);
            ApplyMarkers(tl, s, sw, land, end, sb);

            EditorUtility.SetDirty(tl);
            AssetDatabase.SaveAssets();
            if (applyToPrefab)
            {
                PrefabUtility.ApplyPrefabInstance(dir.gameObject, InteractionMode.AutomatedAction);
                AssetDatabase.SaveAssets();
            }
            EditorSceneManager.SaveOpenScenes();
            sb.Insert(0, "적용 완료 (착지 " + land.ToString("F2") + "s, 휘두름 " + string.Join(", ", sw.Select(x => x.ToString("F2"))) + ")\n");
            Debug.Log(sb.ToString());
            return sb.ToString();
        }

        // ------------------------------------------------------------------ helpers
        private static UnityEngine.SceneManagement.Scene _scene;

        internal static GameObject Find(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (_scene.IsValid() && t.gameObject.scene != _scene)
                {
                    continue;
                }
                if (t.name == name)
                {
                    return t.gameObject;
                }
            }
            return null;
        }

        private static TrackAsset FindTrack(TimelineAsset tl, string name)
        {
            foreach (TrackAsset t in tl.GetRootTracks())
            {
                TrackAsset r = FindIn(t, name);
                if (r != null)
                {
                    return r;
                }
            }
            return null;
        }

        private static TrackAsset FindIn(TrackAsset t, string name)
        {
            if (t.name == name)
            {
                return t;
            }
            foreach (TrackAsset c in t.GetChildTracks())
            {
                TrackAsset r = FindIn(c, name);
                if (r != null)
                {
                    return r;
                }
            }
            return null;
        }

        private static bool Derive(TimelineAsset tl, out float[] sw, out float land)
        {
            sw = new float[5];
            land = 0f;
            var caster = FindTrack(tl, "Caster") as AnimationTrack;
            if (caster == null)
            {
                return false;
            }
            var clips = caster.GetClips().ToDictionary(c => c.displayName, c => c);
            for (int i = 0; i < 5; i++)
            {
                if (!clips.TryGetValue("Take_B" + (i + 1) + "_Swing", out TimelineClip c))
                {
                    return false;
                }
                sw[i] = (float)c.end;
            }
            if (!clips.TryGetValue("Take_B6_Fall", out TimelineClip f))
            {
                return false;
            }
            land = (float)f.end;
            return true;
        }

        private static float Sm(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static AnimationCurve Linear(List<Keyframe> ks)
        {
            var c = new AnimationCurve(ks.ToArray());
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Linear);
            }
            return c;
        }

        private static AnimationCurve Sample(System.Func<float, float> f, float end)
        {
            var ks = new List<Keyframe>();
            for (float t = 0f; t <= end + 0.0001f; t += 1f / 60f)
            {
                ks.Add(new Keyframe(t, f(t)));
            }
            return Linear(ks);
        }

        private static float Pulse(float t, float t0, float peak, float att, float hold, float dec)
        {
            if (t < t0 - att) return 0f;
            if (t < t0) return peak * (t - (t0 - att)) / att;
            if (t < t0 + hold) return peak;
            if (t < t0 + hold + dec) return peak * (1f - (t - t0 - hold) / dec);
            return 0f;
        }

        private static ParticleSystem.MinMaxGradient RandomOf(params Color[] cols)
        {
            var g = new Gradient();
            var keys = new GradientColorKey[cols.Length];
            for (int i = 0; i < cols.Length; i++)
            {
                keys[i] = new GradientColorKey(cols[i], cols.Length == 1 ? 0f : (float)i / (cols.Length - 1));
            }
            g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
        }

        private static Gradient AlphaFade(float a, float hold)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(a, 0.25f), new GradientAlphaKey(a, hold), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // ------------------------------------------------------------------ scales / pour
        private static void ApplyScales(UltimateCutsceneStyle s, StringBuilder sb)
        {
            void SetUniform(string n, float v)
            {
                GameObject g = Find(n);
                if (g != null)
                {
                    Vector3 cur = g.transform.localScale;
                    g.transform.localScale = new Vector3(Mathf.Sign(cur.x == 0f ? 1f : cur.x) * v, v, v);
                }
            }
            SetUniform("FX_FlurryA_Pivot", s.flurryScale);
            SetUniform("FX_FlurryB_Pivot", s.flurryScale);
            SetUniform("FX_Combo_Pivot", s.comboScale);
            SetUniform("FX_StrikePivot", s.strikeScale);
            SetUniform("FX_Slash1_Pivot", s.slash1Scale);
            GameObject slashRoot = Find("FX_Slash1");
            if (slashRoot != null)
            {
                int frozen = 0;
                foreach (ParticleSystem sps in slashRoot.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var spr = sps.GetComponent<ParticleSystemRenderer>();
                    if (spr != null && spr.renderMode == ParticleSystemRenderMode.Mesh)
                    {
                        var svo = sps.velocityOverLifetime;
                        if (svo.enabled == s.slash1NoRise) { svo.enabled = !s.slash1NoRise; frozen++; }
                    }
                }
                if (frozen > 0) sb.AppendLine("1차 슬래시 리본 " + frozen + "개: 위로 떠오르는 속도 " + (s.slash1NoRise ? "제거" : "복원"));
            }
            GameObject slashPivot = Find("FX_Slash1_Pivot");
            if (slashPivot != null && s.slash1UsePose)
            {
                slashPivot.transform.localPosition = s.slash1Position;
                slashPivot.transform.localEulerAngles = s.slash1Euler;
            }
            sb.AppendLine("크기: flurry " + s.flurryScale + " / combo " + s.comboScale + " / strike " + s.strikeScale + " / slash1 " + s.slash1Scale);
        }

        private static void ApplyPour(PlayableDirector dir, TimelineAsset tl, UltimateCutsceneStyle s, float land, StringBuilder sb)
        {
            void Place(string n, Vector3 pos, float roll)
            {
                GameObject g = Find(n);
                if (g == null) return;
                Transform t = g.transform;
                t.position = pos;
                t.rotation = Quaternion.LookRotation(pos - s.pourCameraPos, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
            }
            Place("FX_Combo_Pivot", s.comboCenter, 0f);
            Place("FX_FlurryA_Pivot", s.flurryACenter, 8f);
            Place("FX_FlurryB_Pivot", s.flurryBCenter, -18f);
            float comboStart = land - s.comboLead;
            void Start(string track, float start)
            {
                TrackAsset t = FindTrack(tl, track);
                if (t == null) return;
                foreach (TimelineClip c in t.GetClips()) c.start = start;
            }
            Start("FX_Combo", comboStart);
            Start("FX_FlurryA", comboStart + s.flurryAOffset);
            Start("FX_FlurryB", comboStart + s.flurryBOffset);
            Start("FX_Landing", land - 0.03f);

            // 겹 수: 1 = 원본 한 벌. 사용하지 않는 겹의 트랙은 뮤트한다.
            TrackAsset ta = FindTrack(tl, "FX_FlurryA");
            TrackAsset tb = FindTrack(tl, "FX_FlurryB");
            if (ta != null) ta.muted = s.pourLayers < 2;
            if (tb != null) tb.muted = s.pourLayers < 3;

            // 원본 PF 의 파티클 시스템을 전부 쓸지(67개) 초반 것을 뺄지(54개)
            GameObject combo = Find("FX_Combo");
            int systems = 0, enabled = 0;
            if (combo != null)
            {
                foreach (ParticleSystem ps in combo.GetComponentsInChildren<ParticleSystem>(true))
                {
                    bool early = ps.main.startDelay.constantMax <= 0.12f;
                    bool on = s.comboIncludesEarlyParticles || !early;
                    // 참격 리본은 FX_Web 이 같은 타이밍으로 한 벌만 그려 유지한다. 여기서는 꽃잎·스파크·헤이즈·섬광만 쓴다.
                    var pr = ps.GetComponent<ParticleSystemRenderer>();
                    if (s.enableWeb && pr != null && pr.renderMode == ParticleSystemRenderMode.Mesh)
                    {
                        on = false;
                    }
                    ps.gameObject.SetActive(on);
                    systems++;
                    if (on) enabled++;
                }
            }
            sb.AppendLine("쏟아짐: " + s.pourLayers + "겹, Combo 파티클 시스템 " + enabled + "/" + systems + " (원본 67) / 시작 " + comboStart.ToString("F2") + "s");
        }

        // ------------------------------------------------------------------ fog
        private static void WriteTexture(string path, int w, int h, System.Func<float, float, Color> fn)
        {
            var tx = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    px[y * w + x] = fn((x + 0.5f) / w, (y + 0.5f) / h);
                }
            }
            tx.SetPixels(px);
            tx.Apply();
            File.WriteAllBytes(path, tx.EncodeToPNG());
            Object.DestroyImmediate(tx);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var im = (TextureImporter)AssetImporter.GetAtPath(path);
            if (im != null && (im.mipmapEnabled || !im.alphaIsTransparency || im.wrapMode != TextureWrapMode.Repeat))
            {
                im.mipmapEnabled = false;
                im.alphaIsTransparency = true;
                im.wrapMode = TextureWrapMode.Repeat;
                im.SaveAndReimport();
            }
        }

        private static void ApplyFog(PlayableDirector dir, TimelineAsset tl, UltimateCutsceneStyle s, float end, StringBuilder sb)
        {
            WriteTexture(Root + "UI/T_FogDome.png", 512, 256, (u, v) =>
            {
                Color c = v < 0.5f ? Color.Lerp(s.domeBottom, s.domeLow, Sm(0f, 0.42f, v)) : Color.Lerp(s.domeLow, s.domeTop, Sm(0.58f, 1f, v));
                float hor = Sm(0.34f, 0.5f, v) * (1f - Sm(0.5f, 0.66f, v));
                c = Color.Lerp(c, s.domeHorizon, hor * 0.85f);
                float wob = Mathf.Sin(u * 6.2831f * 3f) * 0.015f + Mathf.Sin(u * 6.2831f * 7f + 1.3f) * 0.008f;
                float st = Mathf.Pow(Mathf.Max(0f, Mathf.Sin((v + wob) * 6.2831f * 26f)), 10f) * (0.35f + 0.65f * Mathf.Abs(Mathf.Sin(u * 6.2831f * 5f + v * 9f)));
                c = Color.Lerp(c, s.domeStreak, st * s.domeStreakStrength * (0.3f + hor));
                c.a = 1f;
                return c;
            });
            WriteTexture(Root + "UI/T_FogFloor.png", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                Color c = Color.Lerp(s.floorCenter, s.floorEdge, Sm(0.05f, 0.85f, r));
                c.a = 1f;
                return c;
            });
            WriteTexture(Root + "UI/T_FogSwirl.png", 512, 512, (u, v) =>
            {
                float x = u * 2f - 1f, y = v * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Atan2(y, x);
                float sp = a * 5f + Mathf.Log(r + 0.04f) * 7f;
                float arm = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(sp), 3f);
                float fine = 0.5f + 0.5f * Mathf.Sin(sp * 3.2f + r * 20f);
                float val = Mathf.Clamp01(arm * 0.8f + fine * arm * 0.4f);
                float mask = Sm(0.07f, 0.28f, r) * (1f - Sm(0.62f, 1.0f, r));
                Color col = Color.Lerp(s.swirlLow, s.swirlHigh, val);
                col.a = mask * (0.15f + 0.85f * val);
                return col;
            });

            GameObject fog = Find("FX_Fog");
            if (fog != null)
            {
                foreach (ParticleSystem ps in fog.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule m = ps.main;
                    ParticleSystem.ColorOverLifetimeModule co = ps.colorOverLifetime;
                    switch (ps.name)
                    {
                        case "FogPuffs":
                            m.startColor = RandomOf(s.puffColor1, s.puffColor2, s.puffColor3);
                            co.color = AlphaFade(s.puffAlpha, 0.7f);
                            break;
                        case "FogFront":
                            m.startColor = RandomOf(s.puffColor1, s.puffColor2);
                            co.color = AlphaFade(s.puffAlpha * 0.45f, 0.6f);
                            break;
                        case "FogStreaks":
                            m.startColor = RandomOf(s.streakColor1, s.streakColor2, s.streakColor3);
                            break;
                        case "FogShards":
                            m.startColor = RandomOf(s.shardColor1, s.shardColor2, s.shardColor3);
                            break;
                    }
                }
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animation/ULT_Fog.anim");
            if (clip != null)
            {
                // 포그·배경이 한꺼번에 덮이지 않고, 돔 → 바닥 → 소용돌이 순서로 겹쳐 올라오며 화면을 채운다(부드러운 이징).
                float A(float t, float d)
                {
                    float tin = t - d;
                    if (tin < s.fogIn.x) return 0f;
                    if (tin < s.fogIn.y) return Sm(s.fogIn.x, s.fogIn.y, tin);
                    if (t < s.fogOut.x) return 1f;
                    if (t < s.fogOut.y) return 1f - (t - s.fogOut.x) / Mathf.Max(0.001f, s.fogOut.y - s.fogOut.x);
                    return 0f;
                }
                void Alpha(string path, float mul, float delay) => AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(MeshRenderer), "material._BaseColor.a"), Sample(t => A(t, delay) * mul, end));
                Alpha("FogDome", 1f, s.fogLayerStagger * 2.5f);
                Alpha("FogFloor", 0.97f, s.fogLayerStagger * 0.5f);
                Alpha("VortexPivotA/FogVortexA", 0.95f, s.fogLayerStagger * 4f);
                Alpha("VortexPivotB/FogVortexB", 0.7f, s.fogLayerStagger * 5f);
                // 바닥 포그는 중앙에서 바깥으로 원형으로 퍼지듯 커진다(알파 페이드와 함께).
                GameObject floorGo = Find("FogFloor");
                if (floorGo != null)
                {
                    EditorCurveBinding fb = EditorCurveBinding.FloatCurve("FogFloor", typeof(Transform), "m_LocalScale.x");
                    AnimationCurve prevX = AnimationUtility.GetEditorCurve(clip, fb);
                    Vector3 baseScale = prevX != null && prevX.length > 0 ? new Vector3(prevX.keys[prevX.length - 1].value, prevX.keys[prevX.length - 1].value, prevX.keys[prevX.length - 1].value) : floorGo.transform.localScale;
                    AnimationCurve prevY = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("FogFloor", typeof(Transform), "m_LocalScale.y"));
                    AnimationCurve prevZ = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("FogFloor", typeof(Transform), "m_LocalScale.z"));
                    if (prevY != null && prevY.length > 0) baseScale.y = prevY.keys[prevY.length - 1].value;
                    if (prevZ != null && prevZ.length > 0) baseScale.z = prevZ.keys[prevZ.length - 1].value;
                    float fd = s.fogLayerStagger * 0.5f;
                    float Grow(float t) => 0.1f + 0.9f * Sm(s.fogIn.x + fd, s.fogIn.y + fd, t);
                    AnimationUtility.SetEditorCurve(clip, fb, Sample(t => baseScale.x * Grow(t), end));
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("FogFloor", typeof(Transform), "m_LocalScale.y"), Sample(t => baseScale.y * Grow(t), end));
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("FogFloor", typeof(Transform), "m_LocalScale.z"), Sample(t => baseScale.z * Grow(t), end));
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("FogDome", typeof(Transform), "localEulerAnglesRaw.y"), AnimationCurve.Linear(0f, 0f, end, s.domeSpinDegPerSec * end));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("VortexPivotA/FogVortexA", typeof(Transform), "localEulerAnglesRaw.z"), AnimationCurve.Linear(0f, 0f, end, s.vortexASpinDegPerSec * end));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("VortexPivotB/FogVortexB", typeof(Transform), "localEulerAnglesRaw.z"), AnimationCurve.Linear(0f, 0f, end, s.vortexBSpinDegPerSec * end));
                EditorUtility.SetDirty(clip);
            }
            TrackAsset ft = FindTrack(tl, "FX_Fog");
            if (ft != null)
            {
                foreach (TimelineClip c in ft.GetClips())
                {
                    c.start = s.fogIn.x - 0.1f;
                    c.duration = s.fogOut.y + 0.3f - c.start;
                }
            }
            sb.AppendLine("포그: 텍스처·입자 색·알파/회전 곡선 갱신 (in " + s.fogIn + ", out " + s.fogOut + ")");
        }

        // ------------------------------------------------------------------ ghosts
        private static void ApplyGhosts(PlayableDirector dir, TimelineAsset tl, UltimateCutsceneStyle s, float end, StringBuilder sb)
        {
            var caster = FindTrack(tl, "Caster") as AnimationTrack;
            var casterMove = caster?.GetChildTracks().FirstOrDefault(t => t.name == "Caster_Move") as AnimationTrack;
            if (caster == null || casterMove == null || casterMove.infiniteClip == null)
            {
                return;
            }
            List<TimelineClip> src = caster.GetClips().OrderBy(c => c.start).ToList();
            string[] ids = { "A", "B" };
            Color[] cols = { s.ghostColorA, s.ghostColorB };
            float[] alpha = { s.ghostAlphaA, s.ghostAlphaB };
            float[] delay = { s.ghostDelayA, s.ghostDelayB };
            Color[] dissolve = { s.dissolveColorA, s.dissolveColorB };
            var winList = new List<Vector2> { s.ghostWindowDash };
            if (s.ghostWindowFrames != null && s.ghostWindowFrames.Length > 0)
            {
                foreach (Vector2 fr in s.ghostWindowFrames) winList.Add(new Vector2(fr.x / s.ghostFrameRate, fr.y / s.ghostFrameRate));
            }
            else
            {
                TimelineClip gb1 = src.FirstOrDefault(c => c.displayName == "Take_B1_Swing");
                TimelineClip gb6 = src.FirstOrDefault(c => c.displayName == "Take_B6_Fall");
                if (gb1 != null && gb6 != null) winList.Add(new Vector2((float)gb1.start + s.ghostSwingStart, (float)gb6.end - s.ghostSwingEnd));
            }
            Vector2[] win = winList.ToArray();
            for (int g = 0; g < 2; g++)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(Root + "Prefabs/M_UltGhost" + ids[g] + ".mat");
                if (mat != null)
                {
                    mat.SetColor("_BaseColor", new Color(cols[g].r, cols[g].g, cols[g].b, 0f));
                    EditorUtility.SetDirty(mat);
                }
                var tk = FindTrack(tl, "Ghost_" + ids[g]) as AnimationTrack;
                var mv = FindTrack(tl, "Ghost_" + ids[g] + "_Move") as AnimationTrack;
                GameObject go = Find("Ghost" + ids[g]);
                if (tk == null || mv == null || mv.infiniteClip == null || go == null)
                {
                    continue;
                }
                List<TimelineClip> mine = tk.GetClips().OrderBy(c => c.start).ToList();
                for (int i = 0; i < mine.Count && i < src.Count; i++)
                {
                    mine[i].start = src[i].start + delay[g];
                }
                AnimationClip ic = mv.infiniteClip;
                foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(ic).Where(b => b.type == typeof(Transform)).ToList())
                {
                    AnimationUtility.SetEditorCurve(ic, b, null);
                }
                foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(casterMove.infiniteClip))
                {
                    Keyframe[] ks = AnimationUtility.GetEditorCurve(casterMove.infiniteClip, b).keys;
                    for (int i = 0; i < ks.Length; i++)
                    {
                        ks[i].time = Mathf.Min(end, ks[i].time + delay[g]);
                    }
                    var nc = new AnimationCurve(ks);
                    for (int i = 0; i < nc.length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(nc, i, AnimationUtility.TangentMode.ClampedAuto);
                        AnimationUtility.SetKeyRightTangentMode(nc, i, AnimationUtility.TangentMode.ClampedAuto);
                    }
                    AnimationUtility.SetEditorCurve(ic, b, nc);
                }
                float peak = alpha[g];
                float A(float t)
                {
                    float a = 0f;
                    foreach (Vector2 w in win)
                    {
                        a = Mathf.Max(a, peak * Mathf.Clamp01(Mathf.Min((t - w.x) / Mathf.Max(0.001f, s.ghostFadeIn), (w.y - t) / Mathf.Max(0.001f, s.ghostFadeOut))));
                    }
                    return a;
                }
                foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    string path = AnimationUtility.CalculateTransformPath(r.transform, go.transform);
                    AnimationUtility.SetEditorCurve(ic, EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "material._BaseColor.a"), Sample(A, end));
                    AnimationUtility.SetEditorCurve(ic, EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "material._BaseColor.r"), AnimationCurve.Constant(0f, end, cols[g].r));
                    AnimationUtility.SetEditorCurve(ic, EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "material._BaseColor.g"), AnimationCurve.Constant(0f, end, cols[g].g));
                    AnimationUtility.SetEditorCurve(ic, EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "material._BaseColor.b"), AnimationCurve.Constant(0f, end, cols[g].b));
                }
                EditorUtility.SetDirty(ic);
                // 잔상 입자(디졸브)도 잔상이 보이는 구간마다 한 번씩만 나오도록 클립을 구간 수만큼 다시 만든다.
                TrackAsset ct = FindTrack(tl, "FX_Ghost_" + ids[g]);
                GameObject emitGo = Find("Ghost" + ids[g] + "_Dissolve");
                if (ct != null && emitGo != null)
                {
                    foreach (TimelineClip oldClip in ct.GetClips().ToList())
                    {
                        tl.DeleteClip(oldClip);
                    }
                    for (int wi = 0; wi < win.Length; wi++)
                    {
                        UltimateCutsceneFinale.ControlClip(dir, ct, emitGo, win[wi].x, win[wi].y - win[wi].x, (uint)(11 + g * 7 + wi));
                    }
                }
                GameObject emit = Find("Ghost" + ids[g] + "_Dissolve");
                if (emit != null)
                {
                    foreach (ParticleSystem ps in emit.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        ParticleSystem.MainModule m = ps.main;
                        m.startColor = new ParticleSystem.MinMaxGradient(dissolve[g], Color.white);
                    }
                }
            }
            sb.AppendLine("잔상: 색/알파/지연/구간 갱신");
        }

        // ------------------------------------------------------------------ hit fx colours
        private static void ApplyHitColors(UltimateCutsceneStyle s, StringBuilder sb)
        {
            foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                ParticleSystem.MainModule m = ps.main;
                switch (ps.name)
                {
                    case "Hit_Ring": m.startColor = new ParticleSystem.MinMaxGradient(s.hitRingA, s.hitRingB); break;
                    case "Hit_Flare": m.startColor = new ParticleSystem.MinMaxGradient(s.hitFlareA, s.hitFlareB); break;
                    case "Hit_Sparks": m.startColor = new ParticleSystem.MinMaxGradient(s.hitSparkA, s.hitSparkB); break;
                    case "Land_Ring": m.startColor = new ParticleSystem.MinMaxGradient(s.landRingA, s.landRingB); break;
                    case "Land_Dust": m.startColor = new ParticleSystem.MinMaxGradient(s.landDustA, s.landDustB); break;
                }
            }
            sb.AppendLine("타격 FX 색 갱신");
        }

        // ------------------------------------------------------------------ look (bloom / exposure)
        private static void ApplyLook(PlayableDirector dir, TimelineAsset tl, UltimateCutsceneStyle s, float end, StringBuilder sb)
        {
            const string profilePath = Root + "Volumes/VP_Ult_Look.asset";
            var vp = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (vp == null)
            {
                vp = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(vp, profilePath);
            }
            if (!vp.TryGet(out Bloom bloom)) bloom = vp.Add<Bloom>(true);
            bloom.threshold.Override(s.bloomThreshold);
            bloom.intensity.Override(s.bloomIntensity);
            bloom.scatter.Override(s.bloomScatter);
            bloom.tint.Override(s.bloomTint);
            bloom.highQualityFiltering.Override(true);
            if (!vp.TryGet(out ColorAdjustments ca)) ca = vp.Add<ColorAdjustments>(true);
            ca.postExposure.Override(s.lookExposure);
            ca.saturation.Override(s.lookSaturation);
            ca.colorFilter.Override(s.lookColorFilter);
            EditorUtility.SetDirty(vp);

            GameObject g = Find("Volume_Look");
            if (g == null)
            {
                g = new GameObject("Volume_Look");
                g.transform.SetParent(dir.transform, false);
                var v = g.AddComponent<Volume>();
                v.isGlobal = true;
                v.priority = 4f;
                v.weight = 0f;
                v.sharedProfile = vp;
                g.AddComponent<Animator>();
            }
            var tr = FindTrack(tl, "Volume_Look") as AnimationTrack;
            if (tr == null)
            {
                tr = (AnimationTrack)tl.CreateTrack(typeof(AnimationTrack), null, "Volume_Look");
            }
            if (tr.infiniteClip == null)
            {
                tr.CreateInfiniteClip("Volume_Look_Clip");
            }
            Vector4 t4 = s.lookVolumeTimes;
            float W(float t) => Mathf.Clamp01(Mathf.Min((t - t4.x) / Mathf.Max(0.001f, t4.y - t4.x), (t4.w - t) / Mathf.Max(0.001f, t4.w - t4.z)));
            AnimationUtility.SetEditorCurve(tr.infiniteClip, EditorCurveBinding.FloatCurve("", typeof(Volume), "weight"), Sample(W, end));
            dir.SetGenericBinding(tr, g.GetComponent<Animator>());
            sb.AppendLine("룩: Bloom(" + s.bloomThreshold + "/" + s.bloomIntensity + "/" + s.bloomScatter + ") 노출 " + s.lookExposure + "EV");
        }

        // ------------------------------------------------------------------ length / UI / cut volume
        /// <summary>끝 시각 뒤로 남아 있는 인피니트 클립의 곡선 키를 잘라, 타임라인이 짧아졌을 때 끝에 빈 구간이 남지 않게 한다.</summary>
        private static void TrimInfinite(TimelineAsset tl, float end)
        {
            void Walk(TrackAsset t)
            {
                if (t is AnimationTrack at && at.infiniteClip != null)
                {
                    AnimationClip ic = at.infiniteClip;
                    foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(ic).ToList())
                    {
                        AnimationCurve c = AnimationUtility.GetEditorCurve(ic, b);
                        if (c == null || c.length < 2 || c[c.length - 1].time <= end + 0.001f)
                        {
                            continue;
                        }
                        var keys = c.keys.Where(k => k.time <= end + 0.001f).ToList();
                        if (keys.Count == 0)
                        {
                            keys.Add(c[0]);
                        }
                        if (keys[keys.Count - 1].time < end - 0.001f)
                        {
                            keys.Add(new Keyframe(end, c.Evaluate(end)));
                        }
                        AnimationUtility.SetEditorCurve(ic, b, new AnimationCurve(keys.ToArray()));
                    }
                    EditorUtility.SetDirty(ic);
                }
                foreach (TrackAsset ch in t.GetChildTracks())
                {
                    Walk(ch);
                }
            }
            foreach (TrackAsset t in tl.GetRootTracks())
            {
                Walk(t);
            }
        }

        /// <summary>타임라인이 길어졌을 때 끝까지 이어지던 클립(카메라·UI·FX 이동 등)을 새 끝까지 늘린다.</summary>
        private static void ExtendClips(TimelineAsset tl, float oldEnd, float end)
        {
            void Walk(TrackAsset t)
            {
                if (!(t is ControlTrack))
                {
                    foreach (TimelineClip c in t.GetClips())
                    {
                        if (c.end >= oldEnd - 0.05 && Mathf.Abs((float)c.end - end) > 0.0005f)
                        {
                            c.duration = end - c.start;
                        }
                    }
                }
                foreach (TrackAsset ch in t.GetChildTracks())
                {
                    Walk(ch);
                }
            }
            foreach (TrackAsset t in tl.GetRootTracks())
            {
                Walk(t);
            }
        }

        private static void ApplyUI(TimelineAsset tl, UltimateCutsceneStyle s, float land, float end, StringBuilder sb)
        {
            float tBreak = land + s.breakTime;
            float ret0 = tBreak + s.returnStartAfterBreak;
            float ret1 = ret0 + s.returnDuration;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animation/ULT_UI.anim");
            if (clip != null)
            {
                AnimationCurve Old(string path, System.Type ty, string prop) => AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, ty, prop));
                AnimationCurve oFade = Old("Fade", typeof(Image), "m_Color.a");
                AnimationCurve oBt = Old("BarTop", typeof(RectTransform), "m_SizeDelta.y");
                AnimationCurve oBb = Old("BarBottom", typeof(RectTransform), "m_SizeDelta.y");
                AnimationCurve oSp = Old("SpeedLines", typeof(Image), "m_Color.a");
                AnimationCurve oWx = Old("Wipe", typeof(RectTransform), "m_AnchoredPosition.x");
                // 마지막 검은 페이드는 없앤다. 레터박스는 복귀 구간에서 풀린다.
                float Bars(AnimationCurve o, float t) => t < land ? o.Evaluate(t) : Mathf.Lerp(130f, 0f, Sm(ret0, ret1, t));
                if (oFade != null) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Fade", typeof(Image), "m_Color.a"), Sample(t => t < 5f ? Mathf.Min(oFade.Evaluate(t), 0.01f) : 0f, end));
                if (oBt != null) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("BarTop", typeof(RectTransform), "m_SizeDelta.y"), Sample(t => Bars(oBt, t), end));
                if (oBb != null) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("BarBottom", typeof(RectTransform), "m_SizeDelta.y"), Sample(t => Bars(oBb, t), end));
                if (oSp != null) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("SpeedLines", typeof(Image), "m_Color.a"), Sample(t => t < 5f ? oSp.Evaluate(t) : 0f, end));
                if (oWx != null && oWx.length > 0)
                {
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Wipe", typeof(RectTransform), "m_AnchoredPosition.x"), Linear(new List<Keyframe>(oWx.keys.Where(k => k.time < 1.6f).Concat(new[] { new Keyframe(end, 4800f) }))));
                }
                EditorUtility.SetDirty(clip);
            }

            // 1차 공격 뒤 화면을 덮던 분홍 와이프 패널: 기본은 끈다(포그·배경 어둠이 자연스럽게 채우도록).
            GameObject wipeGo = Find("Wipe");
            if (wipeGo != null) wipeGo.SetActive(s.enableWipe);

            // 컷 볼륨(비네트 등)은 깨짐 이후 복귀 구간에서 0 으로 풀어 원래 화면 톤으로 돌아간다.
            var cut = FindTrack(tl, "Volume_Cut") as AnimationTrack;
            if (cut != null && cut.infiniteClip != null)
            {
                EditorCurveBinding b = EditorCurveBinding.FloatCurve("", typeof(Volume), "weight");
                AnimationCurve o = AnimationUtility.GetEditorCurve(cut.infiniteClip, b);
                if (o != null)
                {
                    float plateau = Mathf.Min(4.75f, tBreak);
                    float hold = o.Evaluate(plateau);
                    float W(float t)
                    {
                        if (t <= plateau) return o.Evaluate(t);
                        if (t <= tBreak) return Mathf.Lerp(hold, 0.6f * hold, Sm(plateau, tBreak, t));
                        return 0.6f * hold * (1f - Sm(tBreak, ret1, t));
                    }
                    AnimationUtility.SetEditorCurve(cut.infiniteClip, b, Sample(W, end));
                }
            }
            sb.AppendLine("UI: 마지막 검은 페이드 제거, 레터박스는 " + ret0.ToString("F2") + "~" + ret1.ToString("F2") + "s 에 해제");
        }

        // ------------------------------------------------------------------ pulses / shake / flash / markers
        private static void ApplyHitPulses(TimelineAsset tl, UltimateCutsceneStyle s, float[] sw, float land, float end, StringBuilder sb)
        {
            var tr = FindTrack(tl, "Volume_Hit") as AnimationTrack;
            if (tr == null || tr.infiniteClip == null) return;
            var hits = new List<float[]> { new[] { s.impactTime, 1f, 0.16f } };
            for (int i = 0; i < 5; i++) hits.Add(new[] { sw[i], i == 4 ? s.hitPulseLastSwing : s.hitPulseSwing, i == 4 ? 0.16f : 0.12f });
            hits.Add(new[] { land - 0.2f, 0.8f * s.hitPulseLand, 0.15f });
            hits.Add(new[] { land - 0.14f, 0.8f * s.hitPulseLand, 0.15f });
            hits.Add(new[] { land, s.hitPulseLand, 0.35f });
            hits.Add(new[] { land + 0.2f, 0.9f * s.hitPulseLand, 0.2f });
            hits.Add(new[] { land + 0.33f, s.hitPulseLand, 0.3f });
            hits.Add(new[] { land + s.breakTime, 1.0f, 0.3f });
            float W(float t)
            {
                float a = 0f;
                foreach (float[] h in hits) if (t >= h[0]) a = Mathf.Max(a, h[1] * Mathf.Exp(-(t - h[0]) / h[2]));
                return a < 0.01f ? 0f : a;
            }
            AnimationUtility.SetEditorCurve(tr.infiniteClip, EditorCurveBinding.FloatCurve("", typeof(Volume), "weight"), Sample(W, end));
            sb.AppendLine("피격 펄스 곡선 갱신");
        }

        // ------------------------------------------------------------------ camera
        private struct Shot
        {
            public float t0, t1;
            public Vector3 pa, pb, la, lb;
            public float fa, fb, da, db;
            public bool lin;
        }

        private static Shot MakeShot(float t0, float t1, Vector3 pa, Vector3 pb, Vector3 la, Vector3 lb, float fa, float fb, float da, float db, bool lin)
        {
            return new Shot { t0 = t0, t1 = t1, pa = pa, pb = pb, la = la, lb = lb, fa = fa, fb = fb, da = da, db = db, lin = lin };
        }

        private static void ApplyCamera(UltimateCutsceneStyle s, float[] sw, float land, float end, StringBuilder sb)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animation/ULT_CamMove.anim");
            if (clip == null) return;
            float pourStart = land - s.pourCameraLead;
            float pourMoveEnd = pourStart + s.pourCameraMoveTime;
            float ret0 = land + s.breakTime + s.returnStartAfterBreak;
            float ret1 = ret0 + s.returnDuration;
            Vector3 driftEnd = s.pourCameraPos + s.pourCameraDrift;
            Vector3 s1End = new Vector3(2.2f, 1.7f, 0.9f);
            Vector3 s1EndLook = new Vector3(-0.3f, 1.6f, 4.2f);
            var shots = new List<Shot>
            {
                MakeShot(0f, 0.6f, s.battleCameraPos, new Vector3(2.35f, 1.5f, -1.1f), s.battleCameraLook, new Vector3(-0.3f, 1.2f, 1.8f), s.battleCameraFov, 58f, 0f, -2f, true),
                MakeShot(0.6f, 1.3f, new Vector3(2.35f, 1.5f, -1.1f), new Vector3(2.7f, 1.75f, -0.1f), new Vector3(-0.3f, 1.2f, 1.8f), new Vector3(-0.3f, 1.4f, 2.9f), 58f, 50f, -2f, 0f, true),
                MakeShot(1.3f, pourStart, new Vector3(2.7f, 1.75f, -0.1f), s1End, new Vector3(-0.3f, 1.4f, 2.9f), s1EndLook, 50f, 47f, 0f, 0f, false),
                MakeShot(pourStart, pourMoveEnd, s1End, s.pourCameraPos, s1EndLook, s.pourCameraLook, 47f, s.pourCameraFov, 0f, 0f, false),
                MakeShot(pourMoveEnd, ret0, s.pourCameraPos, driftEnd, s.pourCameraLook, s.pourCameraLook, s.pourCameraFov, s.pourCameraFov - 4f, 0f, 0f, false),
                MakeShot(ret0, ret1, driftEnd, s.battleCameraPos, s.pourCameraLook, s.battleCameraLook, s.pourCameraFov - 4f, s.battleCameraFov, 0f, 0f, false),
                MakeShot(ret1, end, s.battleCameraPos, s.battleCameraPos, s.battleCameraLook, s.battleCameraLook, s.battleCameraFov, s.battleCameraFov, 0f, 0f, false),
            };

            var px = new List<Keyframe>(); var py = new List<Keyframe>(); var pz = new List<Keyframe>();
            var rx = new List<Keyframe>(); var ry = new List<Keyframe>(); var rz = new List<Keyframe>();
            var kf = new List<Keyframe>(); var kd = new List<Keyframe>();
            var cut = new List<int>();
            float prevYaw = 0f;
            const float dt = 1f / 60f;
            foreach (Shot sh in shots)
            {
                if (sh.t1 <= sh.t0 + dt) continue;
                bool isLast = sh.t1 >= end - 0.001f;
                float tEnd = isLast ? end : sh.t1 - dt;
                int n = Mathf.Max(1, Mathf.RoundToInt((tEnd - sh.t0) / dt));
                bool first = true;
                for (int i = 0; i <= n; i++)
                {
                    float t = sh.t0 + (tEnd - sh.t0) * ((float)i / n);
                    float u = Mathf.Clamp01((float)i / n);
                    float e = sh.lin ? u : u * u * (3f - 2f * u);
                    Vector3 p = Vector3.Lerp(sh.pa, sh.pb, e);
                    Vector3 l = Vector3.Lerp(sh.la, sh.lb, e);
                    Vector3 eu = Quaternion.LookRotation(l - p, Vector3.up).eulerAngles;
                    float pitch = eu.x > 180f ? eu.x - 360f : eu.x;
                    float yaw = eu.y;
                    if (first) { prevYaw = yaw; first = false; }
                    else
                    {
                        while (yaw - prevYaw > 180f) yaw -= 360f;
                        while (yaw - prevYaw < -180f) yaw += 360f;
                        prevYaw = yaw;
                    }
                    px.Add(new Keyframe(t, p.x)); py.Add(new Keyframe(t, p.y)); pz.Add(new Keyframe(t, p.z));
                    rx.Add(new Keyframe(t, pitch)); ry.Add(new Keyframe(t, yaw)); rz.Add(new Keyframe(t, 0f));
                    float fov = Mathf.Lerp(sh.fa, sh.fb, e);
                    float dutch = Mathf.Lerp(sh.da, sh.db, e);
                    foreach (float[] k in FovKicks(s, sw, land)) if (t >= k[0]) fov += k[1] * Mathf.Exp(-(t - k[0]) / k[2]);
                    for (int w = 0; w < sw.Length; w++) if (t >= sw[w]) dutch += ((w % 2 == 0) ? 2.4f : -2.4f) * Mathf.Exp(-(t - sw[w]) / 0.15f);
                    if (t >= land) dutch += -3.5f * Mathf.Exp(-(t - land) / 0.3f);
                    if (t >= land + s.breakTime) dutch += 3.2f * Mathf.Exp(-(t - land - s.breakTime) / 0.18f);
                    if (t >= s.impactTime) dutch += -2.0f * Mathf.Exp(-(t - s.impactTime) / 0.2f);
                    kf.Add(new Keyframe(t, fov)); kd.Add(new Keyframe(t, dutch));
                    if (i == n && !isLast) cut.Add(px.Count - 1);
                }
            }

            AnimationCurve Make(List<Keyframe> ks)
            {
                var c = new AnimationCurve(ks.ToArray());
                for (int i = 0; i < c.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(c, i, cut.Contains(i) ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear);
                }
                return c;
            }
            void Set(System.Type ty, string prop, List<Keyframe> ks) => AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("CM_Camera_02", ty, prop), Make(ks));
            Set(typeof(Transform), "m_LocalPosition.x", px); Set(typeof(Transform), "m_LocalPosition.y", py); Set(typeof(Transform), "m_LocalPosition.z", pz);
            Set(typeof(Transform), "localEulerAnglesRaw.x", rx); Set(typeof(Transform), "localEulerAnglesRaw.y", ry); Set(typeof(Transform), "localEulerAnglesRaw.z", rz);
            Set(typeof(CinemachineCamera), "Lens.FieldOfView", kf); Set(typeof(CinemachineCamera), "Lens.Dutch", kd);
            EditorUtility.SetDirty(clip);
            sb.AppendLine("카메라: 쏟아짐 카메라 " + pourStart.ToString("F2") + "s 부터 이동 → " + s.pourCameraPos + " FOV " + s.pourCameraFov + ", 복귀 " + ret0.ToString("F2") + "~" + ret1.ToString("F2") + "s (전투 카메라 자세로 종료)");
        }

        private static List<float[]> FovKicks(UltimateCutsceneStyle s, float[] sw, float land)
        {
            var k = new List<float[]> { new[] { s.impactTime, -18f, 0.12f } };
            foreach (float x in sw) k.Add(new[] { x, -6.5f, 0.12f });
            k.Add(new[] { land - 0.2f, -6f, 0.15f });
            k.Add(new[] { land - 0.14f, -6f, 0.15f });
            k.Add(new[] { land, -17f, 0.25f });
            k.Add(new[] { land + 0.2f, -8f, 0.2f });
            k.Add(new[] { land + 0.33f, -8f, 0.2f });
            k.Add(new[] { land + s.breakTime, -12f, 0.16f });
            return k;
        }

        private static void ApplyShake(UltimateCutsceneStyle s, float[] sw, float land, float end, StringBuilder sb)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animation/ULT_CamMove.anim");
            if (clip == null) return;
            float m = s.shakeMultiplier;
            var ev = new List<float[]> { new[] { s.dashRumbleTime, 1.0f * m, 0.2f }, new[] { s.impactTime, s.shakeImpact * m, 0.35f } };
            for (int i = 0; i < 5; i++) ev.Add(new[] { sw[i], (i == 4 ? s.shakeLastSwing : s.shakeSwing) * m, i == 4 ? 0.2f : 0.18f });
            ev.Add(new[] { land - 0.2f, s.shakePour * m, 0.3f });
            ev.Add(new[] { land - 0.14f, s.shakePour * m, 0.3f });
            ev.Add(new[] { land, s.shakeLand * m, 0.6f });
            ev.Add(new[] { land + 0.2f, s.shakePour * 1.3f * m, 0.4f });
            ev.Add(new[] { land + 0.33f, s.shakePour * 1.4f * m, 0.45f });
            ev.Add(new[] { land + s.breakTime, s.breakShake * m * 0.35f, 0.28f });
            float A(float t)
            {
                float a = 0f;
                foreach (float[] e in ev) if (t >= e[0]) a = Mathf.Max(a, e[1] * Mathf.Exp(-(t - e[0]) / e[2]));
                return a < 0.01f ? 0f : a;
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("CM_Camera_02", typeof(CinemachineBasicMultiChannelPerlin), "AmplitudeGain"), Sample(A, end));
            EditorUtility.SetDirty(clip);
            sb.AppendLine("카메라 쉐이크 진폭 곡선 갱신 (x" + m + ")");
        }

        private static void ApplyFlash(UltimateCutsceneStyle s, float[] sw, float land, float end, StringBuilder sb)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animation/ULT_UI.anim");
            if (clip == null) return;
            var fl = new List<float[]> { new[] { s.impactTime, 1.0f, 0.0167f, Mathf.Max(0.033f, s.flashHoldUntil - s.impactTime), s.flashHoldDecay } };
            for (int i = 0; i < 5; i++) fl.Add(new[] { sw[i], i == 4 ? s.flashSwing * 1.33f : s.flashSwing, 0.0167f, 0.0167f, i == 4 ? 0.08f : 0.06f });
            fl.Add(new[] { land, s.flashLand, 0.0167f, 0.05f, 0.18f });
            fl.Add(new[] { land + 0.33f, s.flashLandSecond, 0.02f, 0.02f, 0.12f });
            fl.Add(new[] { s.whiteoutTime, s.whiteoutPeak, 0.08f, 0.05f, 0.35f });
            if (s.enableBurst) fl.Add(new[] { land + s.breakTime, s.breakFlash, 0.0167f, 0.02f, 0.15f });
            float A(float t)
            {
                float a = 0f;
                foreach (float[] e in fl) a = Mathf.Max(a, Pulse(t, e[0], e[1], e[2], e[3], e[4]));
                return a;
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Flash", typeof(Image), "m_Color.a"), Sample(A, end));
            EditorUtility.SetDirty(clip);
            sb.AppendLine("화면 플래시 곡선 갱신");
        }

        private static void ApplyMarkers(TimelineAsset tl, UltimateCutsceneStyle s, float[] sw, float land, float end, StringBuilder sb)
        {
            if (tl.markerTrack == null) return;
            var times = new List<double> { s.impactTime };
            times.AddRange(sw.Select(x => (double)x));
            times.AddRange(new double[] { land - 0.2, land - 0.14, land, land + 0.2, land + 0.33 });
            List<IMarker> markers = tl.markerTrack.GetMarkers().ToList();
            for (int i = 0; i < times.Count; i++)
            {
                string n = "Hit_" + (i + 1).ToString("00");
                IMarker mk = markers.FirstOrDefault(x => x is UnityEngine.Object o && o.name == n);
                if (mk != null) mk.time = times[i];
            }
            IMarker endMarker = markers.FirstOrDefault(x => x is UnityEngine.Object o && o.name == "End");
            if (endMarker != null)
            {
                endMarker.time = end;   // 끝 마커는 복귀 카메라가 끝나는 시각에 맞춘다
            }
            sb.AppendLine("Hit 마커 시각 갱신, End 마커 " + end.ToString("F2") + "s");
        }
    }
}
