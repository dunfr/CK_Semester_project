using System.Collections.Generic;
using System.IO;
using CK.SemesterProject.Tutorial;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Editor
{
    /// <summary>
    /// TL_Battle_SlashFollow에 레퍼런스 영상(히트스톱 → 노란 폭발 → 가로 퍼짐 → 흰 반투명 번짐 → 흰 불투명 슬래시 → 파편)의
    /// 타격감 연출을 만든다. 텍스처·머티리얼·파티클 오브젝트·타임라인 트랙을 모두 코드로 생성하므로 다시 실행해도 같은 결과가 된다.
    /// 기존 슬래시/임팩트 이펙트와 카메라 프레이밍은 건드리지 않는다.
    /// </summary>
    internal static class TutorialSlashHitFeelBuilder
    {
        private const string Folder = "Assets/CK_Semester_Project/Graphics/Effects/SlashHitFeel";
        private const string PrefabPath = "Assets/CK_Semester_Project/Cinematics/Timelines/PF_Battle_SlashFollow.prefab";
        private const string RootName = "Slash Hit FX";
        private const string GroupName = "Slash Hit Feel";

        // 타임라인(타임라인 시간 = 베기 시퀀스 시간) 기준. 영상의 접촉 프레임(3.17s)이 이 타임라인에서는 HitTime이다.
        private const float HitTime = 1.00f;
        private const float HitStopReal = 0.20f;     // 영상: 3.20~3.40s(약 6프레임) 정지
        private const float HitStopScale = 0.06f;    // 정지 중 월드 속도
        private static float ReleaseTime => HitTime + HitStopReal * HitStopScale;

        // 접촉 프레임의 스크린 평면 기울기(도). 영상의 폭발은 거의 수평, 슬래시는 약간 올라가는 각.
        private const float Tilt = 12f;

        [MenuItem("CK/Build Slash Hit Feel FX")]
        private static void Build()
        {
            EditorApplication.ExecuteMenuItem("CK/Close Slash Follow Preview");
            var star = SaveTexture("T_HitBurstStar", 256, 256, StarPixel);
            var streak = SaveTexture("T_HitStreak", 512, 64, StreakPixel);
            var glow = SaveTexture("T_HitGlow", 128, 128, GlowPixel);
            var waveArc = SaveTexture("T_HitWaveArc", 512, 512, WaveArcPixel);
            var radial = SaveTexture("T_HitRadialLines", 256, 256, RadialPixel);
            var shard = SaveTexture("T_HitShard", 64, 64, ShardPixel);
            var shader = Shader.Find("CK/Effects/SlashHitFX");
            if (shader == null)
            {
                Debug.LogError("CK/Effects/SlashHitFX 셰이더를 찾지 못했습니다. 임포트가 끝난 뒤 다시 실행하세요.");
                return;
            }

            var gold = MakeMaterial("M_HitBurstGold", shader, star, new Color(1f, 0.72f, 0.06f, 1f) * 1.35f, additive: false, queue: 3150);
            var core = MakeMaterial("M_HitBurstCore", shader, star, new Color(1f, 0.95f, 0.72f, 1f) * 0.9f, additive: true, queue: 3160);
            var streakMat = MakeMaterial("M_HitStreak", shader, streak, new Color(1f, 0.93f, 0.62f, 1f) * 3.0f, additive: true, queue: 3140);
            var whiteGlow = MakeMaterial("M_HitGlowWhite", shader, glow, new Color(1f, 1f, 1f, 1f) * 0.75f, additive: true, queue: 3130);
            var pinkGlow = MakeMaterial("M_HitGlowPink", shader, glow, new Color(1f, 0.18f, 0.62f, 1f) * 2.4f, additive: true, queue: 3120);
            var radialMat = MakeMaterial("M_HitRadialLines", shader, radial, new Color(1f, 1f, 1f, 1f) * 1.3f, additive: true, queue: 3145);
            var crescentMat = MakeMaterial("M_HitWaveArc", shader, waveArc, new Color(1.7f, 1.7f, 1.8f, 1f), additive: false, queue: 3170);
            var shardMat = MakeMaterial("M_HitShard", shader, shard, new Color(1.25f, 1.25f, 1.3f, 1f), additive: false, queue: 3135, zTestAlways: false);
            AssetDatabase.SaveAssets();

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                BuildPrefab(prefabRoot, gold, core, streakMat, whiteGlow, pinkGlow, radialMat, crescentMat, shardMat);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            // 초승달 방식에서 호 방식으로 바꾸며 더 이상 쓰지 않는 생성물 정리(프리팹에서 참조가 사라진 뒤에 지운다)
            AssetDatabase.DeleteAsset(Folder + "/T_HitCrescent.png");
            AssetDatabase.DeleteAsset(Folder + "/M_HitCrescentWhite.mat");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Slash Hit Feel FX 생성 완료: " + PrefabPath);
        }

        private sealed class Fx
        {
            public string Name;
            public float Start;
            public float Life;
            public GameObject Object;
        }

        private static void BuildPrefab(GameObject root, Material gold, Material core, Material streak, Material whiteGlow,
            Material pinkGlow, Material radialMat, Material crescentMat, Material shardMat)
        {
            var director = root.GetComponent<PlayableDirector>();
            var timeline = (TimelineAsset)director.playableAsset;

            Transform old = root.transform.Find(RootName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }
            // 기존 임팩트 이펙트의 번쩍임 중심(HitFlashCore)과 같은 지점에 둔다. 기존 이펙트는 수정하지 않는다.
            Transform hitCore = null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "HitFlashCore")
                {
                    hitCore = child;
                    break;
                }
            }
            var fxRoot = new GameObject(RootName).transform;
            fxRoot.SetParent(root.transform, false);
            fxRoot.localPosition = hitCore != null
                ? root.transform.InverseTransformPoint(hitCore.position)
                : new Vector3(0.12f, 0.42f, -0.38f);

            float h = ReleaseTime;
            var list = new List<Fx>();

            // 1) 접촉 순간 터지는 노란 폭발(별 모양) + 흰 코어. 히트스톱 동안 거의 멈춘 채 유지되다가 풀리면 빠르게 사라진다.
            list.Add(Make(fxRoot, "HitBurstGold", HitTime, 0.11f, gold, 1, 0.11f, Tilt, new Vector2(2.3f, 2.3f), new Vector2(2.3f, 2.3f),
                SizeCurve(0.84f, 0.93f, 1.12f), AlphaFade(0.55f, 1f)));
            list.Add(Make(fxRoot, "HitBurstCore", HitTime, 0.09f, core, 1, 0.09f, Tilt - 18f, new Vector2(1.1f, 1.1f), new Vector2(1.1f, 1.1f),
                SizeCurve(0.9f, 0.96f, 1.1f), AlphaFade(0.5f, 1f)));
            // 2) 분홍 번짐(영상의 분홍 글로우)
            list.Add(Make(fxRoot, "HitGlowPink", HitTime, 0.55f, pinkGlow, 1, 0.55f, 0f, new Vector2(2.6f, 2.6f), new Vector2(2.6f, 2.6f),
                SizeCurve(0.35f, 0.55f, 1.0f, ease: true), AlphaFade(0.05f, 0.9f)));

            // 3) 히트스톱이 풀리는 순간 가로로 퍼지는 빛줄기
            var streaks = Make(fxRoot, "HitStreaks", h, 0.34f, streak, 7, 0.34f, Tilt, new Vector2(2.0f, 0.09f), new Vector2(3.6f, 0.2f),
                SeparateSize(new Keyframe(0f, 0.12f, 4f, 4f), new Keyframe(0.4f, 1f, 0.4f, 0.4f), new Keyframe(1f, 1.08f),
                    new Keyframe(0f, 1f), new Keyframe(0.5f, 0.7f), new Keyframe(1f, 0.1f)),
                AlphaFade(0.3f, 1f), rotationSpread: 7f, boxSize: new Vector3(0.6f, 0.55f, 0.01f));
            list.Add(streaks);

            // 4) 양 옆으로 납작하게 퍼지는 흰 반투명 번짐(가로로 긴 타원)
            list.Add(Make(fxRoot, "HitGlowWhite", h, 0.34f, whiteGlow, 1, 0.34f, Tilt, new Vector2(3.8f, 1.7f), new Vector2(3.8f, 1.7f),
                SizeCurve(0.2f, 0.78f, 1.0f, ease: true), AlphaFade(0.0f, 0.55f)));
            // 5) 방사형 흰 선
            list.Add(Make(fxRoot, "HitRadialLines", h + 0.05f, 0.26f, radialMat, 1, 0.26f, 0f, new Vector2(2.8f, 2.2f), new Vector2(2.8f, 2.2f),
                SizeCurve(0.38f, 0.82f, 1.0f, ease: true), AlphaFade(0.22f, 1f)));
            // 6) 좌우로 빠르게 번지는 얇은 반투명 흰 호(레퍼런스의 '(' 모양 충격파). 안쪽 호가 한 박자 늦게 따라간다.
            list.Add(WaveArc(fxRoot, "HitWaveArcLeft", h + 0.02f, 0.24f, crescentMat, Tilt, new Vector2(5.6f, 2.8f)));
            list.Add(WaveArc(fxRoot, "HitWaveArcRight", h + 0.02f, 0.24f, crescentMat, Tilt + 180f, new Vector2(5.6f, 2.8f)));
            list.Add(WaveArc(fxRoot, "HitWaveArcLeftInner", h + 0.06f, 0.22f, crescentMat, Tilt - 4f, new Vector2(4.0f, 2.2f)));
            list.Add(WaveArc(fxRoot, "HitWaveArcRightInner", h + 0.06f, 0.22f, crescentMat, Tilt + 176f, new Vector2(4.0f, 2.2f)));
            // 7) 사방으로 튀는 유리 파편
            list.Add(Shards(fxRoot, "HitShards", h + 0.10f, 0.58f, shardMat));

            // 타임라인 트랙: 이펙트별 Control 트랙 + 히트스톱·카메라 흔들림 트랙(그룹으로 묶음)
            foreach (TrackAsset track in new List<TrackAsset>(timeline.GetRootTracks()))
            {
                if (track.name == GroupName)
                {
                    foreach (TrackAsset child in new List<TrackAsset>(track.GetChildTracks()))
                    {
                        timeline.DeleteTrack(child);
                    }
                    timeline.DeleteTrack(track);
                }
            }
            var group = timeline.CreateTrack<GroupTrack>(null, GroupName);
            var feelTrack = timeline.CreateTrack<TutorialSlashHitFeelTrack>(group, "Hit Stop & Camera Shake");
            AddFeel(feelTrack, "Hit Stop", TutorialSlashHitFeelClip.FeelKind.HitStop, HitTime, HitStopReal, c => c.SlowScale = HitStopScale);
            AddFeel(feelTrack, "Impact Shake", TutorialSlashHitFeelClip.FeelKind.CameraShake, HitTime, 0.56f, c =>
            {
                c.Amplitude = 0.075f;
                c.RotationAmplitude = 0.85f;
                c.Frequency = 26f;
                c.DecayPower = 1.7f;
                c.HoldDuringHitStop = true;
            });
            // 히트스톱이 풀릴 때 한 번 더 짧게 흔들린다. 같은 트랙에서는 겹칠 수 없어 별도 트랙에 둔다.
            var releaseTrack = timeline.CreateTrack<TutorialSlashHitFeelTrack>(group, "Release Shake");
            AddFeel(releaseTrack, "Release Shake", TutorialSlashHitFeelClip.FeelKind.CameraShake, h, 0.26f, c =>
            {
                c.Amplitude = 0.05f;
                c.RotationAmplitude = 0.55f;
                c.Frequency = 30f;
                c.DecayPower = 1.4f;
                c.HoldDuringHitStop = false;
            });

            int seed = 4100;
            foreach (Fx fx in list)
            {
                fx.Object.SetActive(false);
                var track = timeline.CreateTrack<ControlTrack>(group, fx.Name);
                TimelineClip clip = track.CreateClip<ControlPlayableAsset>();
                clip.displayName = fx.Name;
                clip.start = fx.Start;
                clip.duration = fx.Life + 0.04f;
                var asset = (ControlPlayableAsset)clip.asset;
                string exposed = "HitFx_" + fx.Name;
                asset.sourceGameObject.exposedName = exposed;
                asset.updateParticle = true;
                asset.particleRandomSeed = (uint)(seed++);
                asset.updateDirector = false;
                asset.updateITimeControl = false;
                asset.active = true;
                asset.postPlayback = ActivationControlPlayable.PostPlaybackState.Revert;
                director.SetReferenceValue(exposed, fx.Object);
            }
            EditorUtility.SetDirty(timeline);
        }

        private static void AddFeel(TrackAsset track, string name, TutorialSlashHitFeelClip.FeelKind kind, float start, float duration,
            System.Action<TutorialSlashHitFeelClip> configure)
        {
            TimelineClip clip = track.CreateClip<TutorialSlashHitFeelClip>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;
            var asset = (TutorialSlashHitFeelClip)clip.asset;
            asset.Kind = kind;
            configure(asset);
        }

        // ---------- 파티클 ----------

        private static Fx Make(Transform parent, string name, float start, float life, Material material, int count, float duration,
            float tiltDegrees, Vector2 sizeMin, Vector2 sizeMax, ParticleSystem.MinMaxCurve? sizeCurve, Gradient colorOverLifetime,
            float rotationSpread = 0f, Vector3? boxSize = null)
        {
            return Make(parent, name, start, life, material, count, duration, tiltDegrees, sizeMin, sizeMax,
                new SizeSpec(sizeCurve ?? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f)), null), colorOverLifetime,
                rotationSpread, boxSize);
        }

        private static Fx Make(Transform parent, string name, float start, float life, Material material, int count, float duration,
            float tiltDegrees, Vector2 sizeMin, Vector2 sizeMax, SizeSpec size, Gradient colorOverLifetime, float rotationSpread = 0f,
            Vector3? boxSize = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = Mathf.Max(0.05f, duration);
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(sizeMin.x, sizeMax.x);
            main.startSizeY = new ParticleSystem.MinMaxCurve(sizeMin.y, sizeMax.y);
            main.startSizeZ = 1f;
            main.startRotation = new ParticleSystem.MinMaxCurve((tiltDegrees - rotationSpread) * Mathf.Deg2Rad, (tiltDegrees + rotationSpread) * Mathf.Deg2Rad);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = Mathf.Max(8, count * 2);
            main.useUnscaledTime = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            ps.useAutoRandomSeed = false;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = boxSize.HasValue;
            if (boxSize.HasValue)
            {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = boxSize.Value;
            }

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            if (size.Y.HasValue)
            {
                sol.separateAxes = true;
                sol.x = size.X;
                sol.y = size.Y.Value;
                sol.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
            }
            else
            {
                sol.separateAxes = false;
                sol.size = size.X;
            }

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(colorOverLifetime);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.maxParticleSize = 10f; // 기본 0.5(화면 절반)로 잘리지 않게 한다.
            renderer.minParticleSize = 0f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return new Fx { Name = name, Start = start, Life = life, Object = go };
        }

        // 가로로 더 넓게 번지는 호: 처음엔 작고 빠르게 커지다가(ease-out) 끝에서 느려지며 사라진다.
        private static Fx WaveArc(Transform parent, string name, float start, float life, Material material, float tiltDegrees, Vector2 size)
        {
            return Make(parent, name, start, life, material, 1, life, tiltDegrees, size, size,
                SeparateSize(new Keyframe(0f, 0.16f, 0f, 6f), new Keyframe(0.3f, 0.8f, 1.2f, 1.2f), new Keyframe(1f, 1f, 0.12f, 0f),
                    new Keyframe(0f, 0.45f, 0f, 3.5f), new Keyframe(0.3f, 0.82f, 0.8f, 0.8f), new Keyframe(1f, 1f, 0.1f, 0f)),
                AlphaFade(0.22f, 0.92f));
        }

        private static Fx Shards(Transform parent, string name, float start, float life, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = life;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.65f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.6f, 6.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.gravityModifier = 0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 80;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            ps.useAutoRandomSeed = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 38) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            shape.scale = new Vector3(1.5f, 1f, 1f);
            shape.rotation = new Vector3(0f, 0f, Tilt);

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.12f;
            limit.limit = 1.2f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-5f, 5f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(AlphaFade(0.55f, 1f));

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.1f, 1f), new Keyframe(1f, 0.65f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.maxParticleSize = 10f; // 기본 0.5(화면 절반)로 잘리지 않게 한다.
            renderer.minParticleSize = 0f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return new Fx { Name = name, Start = start, Life = life, Object = go };
        }

        private readonly struct SizeSpec
        {
            public readonly ParticleSystem.MinMaxCurve X;
            public readonly ParticleSystem.MinMaxCurve? Y;

            public SizeSpec(ParticleSystem.MinMaxCurve x, ParticleSystem.MinMaxCurve? y)
            {
                X = x;
                Y = y;
            }
        }

        private static ParticleSystem.MinMaxCurve? SizeCurve(float from, float mid, float to, bool ease = false)
        {
            var curve = ease
                ? new AnimationCurve(new Keyframe(0f, from, 0f, 5f), new Keyframe(0.35f, mid, 1.2f, 1.2f), new Keyframe(1f, to, 0.25f, 0f))
                : new AnimationCurve(new Keyframe(0f, from), new Keyframe(0.5f, mid), new Keyframe(1f, to));
            return new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static SizeSpec SeparateSize(Keyframe x0, Keyframe x1, Keyframe x2, Keyframe y0, Keyframe y1, Keyframe y2)
        {
            return new SizeSpec(new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(x0, x1, x2)),
                new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(y0, y1, y2)));
        }

        // 수명의 holdUntil까지 peak 알파를 유지한 뒤 0으로 사라진다.
        private static Gradient AlphaFade(float holdUntil, float peak)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(peak, 0f),
                    new GradientAlphaKey(peak, Mathf.Clamp01(holdUntil)),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        // ---------- 머티리얼 ----------

        private static Material MakeMaterial(string name, Shader shader, Texture2D texture, Color color, bool additive, int queue, bool zTestAlways = true)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive
                ? (float)UnityEngine.Rendering.BlendMode.One
                : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZTest", zTestAlways
                ? (float)UnityEngine.Rendering.CompareFunction.Always
                : (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            material.renderQueue = queue;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- 텍스처 ----------

        private delegate float PixelFunc(float u, float v);

        private static Texture2D SaveTexture(string name, int width, int height, PixelFunc func)
        {
            Directory.CreateDirectory(Folder);
            string path = Folder + "/" + name + ".png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = Mathf.Clamp01(func((x + 0.5f) / width, (y + 0.5f) / height));
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float AngleDiff(float a, float b)
        {
            float d = Mathf.Repeat(a - b + 180f, 360f) - 180f;
            return Mathf.Abs(d);
        }

        private static readonly (float angle, float length, float width)[] StarSpikes =
        {
            (0f, 1.00f, 4.5f), (180f, 0.96f, 4.5f),
            (22f, 0.64f, 4.0f), (-22f, 0.60f, 4.0f), (158f, 0.58f, 4.0f), (202f, 0.62f, 4.0f),
            (50f, 0.42f, 5.0f), (-48f, 0.40f, 5.0f), (130f, 0.40f, 5.0f), (230f, 0.44f, 5.0f),
            (88f, 0.54f, 3.6f), (-90f, 0.46f, 3.6f),
            (10f, 0.80f, 2.4f), (-9f, 0.74f, 2.4f), (171f, 0.72f, 2.4f), (191f, 0.78f, 2.4f),
        };

        // 8~16갈래 별 모양 폭발: 좌우로 긴 가시, 가운데 밝은 코어와 은은한 후광
        private static float StarPixel(float u, float v)
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            float r = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            float spikes = 0f;
            foreach ((float spikeAngle, float length, float width) in StarSpikes)
            {
                if (r >= length)
                {
                    continue;
                }
                float taper = 1f - r / length;
                float halfWidth = width * (0.55f + taper * 1.7f) + 0.8f;
                float d = AngleDiff(angle, spikeAngle);
                float value = Mathf.Exp(-(d * d) / (halfWidth * halfWidth)) * Mathf.Pow(taper, 0.9f);
                spikes = Mathf.Max(spikes, value);
            }
            float coreGlow = Mathf.Exp(-(r * r) / (0.16f * 0.16f));
            float halo = 0.28f * Mathf.Exp(-(r * r) / (0.5f * 0.5f));
            float edge = Smooth(1f, 0.92f, r);
            return Mathf.Max(spikes, Mathf.Max(coreGlow, halo)) * edge;
        }

        // 가로 빛줄기: 세로로 아주 얇고 양 끝이 뾰족하다
        private static float StreakPixel(float u, float v)
        {
            float y = (v - 0.5f) * 2f;
            float body = Mathf.Exp(-(y * y) / (0.045f * 0.045f));
            float halo = 0.22f * Mathf.Exp(-(y * y) / (0.3f * 0.3f));
            float length = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.55f);
            float tip = Smooth(0f, 0.08f, u) * Smooth(1f, 0.92f, u);
            return Mathf.Max(body, halo) * length * tip;
        }

        private static float GlowPixel(float u, float v)
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f) * Smooth(1f, 0.85f, r) + 0.18f * Mathf.Exp(-(r * r) / 0.05f);
        }

        // 왼쪽을 향한 얇은 '(' 호: 가운데가 가장 두껍고 양 끝은 바늘처럼 뾰족하며, 바깥에 은은한 빛번짐과 안쪽에 가는 보조 호가 있다.
        // 오른쪽 호는 같은 텍스처를 180도 돌려 쓴다.
        private static float WaveArcPixel(float u, float v)
        {
            float x = u - 0.5f;
            float y = v - 0.5f;
            float r = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            float d = AngleDiff(angle, 180f);

            float mainArc = 0f;
            if (d < 66f)
            {
                float t = 1f - d / 66f;
                float width = 0.0025f + 0.0145f * Mathf.Pow(t, 0.75f);
                float dr = (r - 0.44f) / width;
                float core = Mathf.Exp(-dr * dr);
                float glow = 0.22f * Mathf.Exp(-(dr * dr) / 14f) * t;
                mainArc = Mathf.Max(core, glow);
            }

            float innerArc = 0f;
            if (d < 44f)
            {
                float t = 1f - d / 44f;
                float width = 0.002f + 0.006f * Mathf.Pow(t, 0.8f);
                float dr = (r - 0.385f) / width;
                innerArc = 0.5f * Mathf.Exp(-dr * dr);
            }

            // 가장자리 페이드: 텍스처 경계에서 잘리지 않게
            return Mathf.Max(mainArc, innerArc) * Smooth(0.5f, 0.46f, r);
        }

        private static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
        }

        // 방사형 속도선: 중심은 비우고 바깥으로 갈수록 가늘어지는 길고 짧은 선들
        private static float RadialPixel(float u, float v)
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            float r = Mathf.Sqrt(x * x + y * y);
            if (r > 1f || r < 0.1f)
            {
                return 0f;
            }
            const int rays = 44;
            float angle = Mathf.Atan2(y, x);
            float cell = (angle + Mathf.PI) / (Mathf.PI * 2f) * rays;
            int index = Mathf.RoundToInt(cell);
            float jitter = Hash(index * 7 + 3) * 0.18f;
            float rayAngle = (index + jitter) / rays * Mathf.PI * 2f - Mathf.PI;
            float length = 0.45f + 0.55f * (Hash(index * 13 + 5) * 0.5f + 0.5f);
            if (r > length)
            {
                return 0f;
            }
            float arc = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, rayAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad * r;
            float taper = 1f - r / length;
            float width = 0.004f + 0.012f * taper;
            float brightness = 0.55f + 0.45f * (Hash(index * 29 + 11) * 0.5f + 0.5f);
            return Mathf.Exp(-(arc * arc) / (width * width)) * Mathf.Pow(taper, 0.7f) * brightness * Smooth(0.1f, 0.2f, r);
        }

        // 유리 파편: 비스듬한 삼각형
        private static float ShardPixel(float u, float v)
        {
            Vector2 p = new Vector2(u, v);
            Vector2 a = new Vector2(0.08f, 0.14f);
            Vector2 b = new Vector2(0.94f, 0.34f);
            Vector2 c = new Vector2(0.38f, 0.94f);
            float d1 = EdgeDistance(p, a, b);
            float d2 = EdgeDistance(p, b, c);
            float d3 = EdgeDistance(p, c, a);
            float inside = Mathf.Min(d1, Mathf.Min(d2, d3));
            float mask = Smooth(0f, 0.035f, inside);
            return mask * (0.55f + 0.45f * u);
        }

        private static float EdgeDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 e = b - a;
            Vector2 n = new Vector2(-e.y, e.x).normalized;
            return Vector2.Dot(p - a, n);
        }
    }
}
