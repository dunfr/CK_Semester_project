using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Editor
{
    /// <summary>
    /// Rebuilds the "Pink Impact Fan" hit effect used by TL_Battle_SlashFollow.
    /// Reference: frames 71-77 (30fps) of the Honkai-style hit - white-hot flash, a fan of pink streaks with
    /// white cores, black wedges between the beams, and a streak-aligned dissolve that breaks the lines up.
    /// Everything is a particle system + Shader Graph (SG_ImpactFanRef); all colours / lighting are material parameters.
    /// </summary>
    internal static class TutorialSlashImpactFanBuilder
    {
        private const string Folder = "Assets/CK_Semester_Project/Graphics/Effects/SlashImpactFan";
        private const string PrefabPath = "Assets/CK_Semester_Project/Cinematics/Timelines/PF_Battle_SlashFollow.prefab";
        private const string EffectName = "Pink Impact Fan";
        private const string Binding = "PinkImpactFanObject";
        private const double ClipDuration = 0.36;
        private const float FanDelay = 0.02f; // flash leads, the fan opens ~1 frame later (reference f71 -> f72)

        [MenuItem("CK/Build Pink Impact Fan")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/SG_ImpactFanRef.shadergraph");
            if (shader == null)
            {
                Debug.LogError("SG_ImpactFanRef Shader Graph import must finish before building.");
                return;
            }
            EditorApplication.ExecuteMenuItem("CK/Close Slash Follow Preview");

            // ---------------------------------------------------------------- materials (all tunable in the Inspector)
            // NOTE: the scene volume has no tonemapping, so HDR intensity clips hue. Pink stays ~1.5x (reads hot pink),
            // the white core carries the high HDR values that bloom.
            var pink = new Color(1f, 0.012f, 0.16f);
            var white = new Color(1f, 0.93f, 1f);
            // Soft radial-ray cone: the dense white-pink body of the fan near the contact point
            Material cone = Mat("M_ImpactFanRef_Cone", shader, "T_ImpactFanRef_Cone", 3098, m =>
            {
                m.SetColor("_CoreColor", white); m.SetFloat("_CoreIntensity", 7f);
                m.SetFloat("_CoreThreshold", 0.10f); m.SetFloat("_CoreSoftness", 0.6f);
                m.SetColor("_RimColor", pink); m.SetFloat("_RimIntensity", 1.6f);
                m.SetFloat("_DarkEdgeAmount", 0f);
                m.SetFloat("_Opacity", 1f); m.SetFloat("_DissolveSoftness", 0.3f);
            });
            // Soft pink body under the beams
            Material glow = Mat("M_ImpactFanRef_Glow", shader, "T_ImpactFanRef_Beam", 3099, m =>
            {
                m.SetColor("_CoreColor", new Color(1f, 0.6f, 0.9f)); m.SetFloat("_CoreIntensity", 1.2f);
                m.SetFloat("_CoreThreshold", 0.5f); m.SetFloat("_CoreSoftness", 0.5f);
                m.SetColor("_RimColor", pink); m.SetFloat("_RimIntensity", 1.4f);
                m.SetFloat("_DarkEdgeAmount", 0f);
                m.SetFloat("_Opacity", 0.38f); m.SetFloat("_DissolveSoftness", 0.3f);
            });
            // Black wedges (sit between the pink beams, above the pink body)
            Material dark = Mat("M_ImpactFanRef_Dark", shader, "T_ImpactFanRef_Dark", 3104, m =>
            {
                m.SetColor("_DarkColor", new Color(0.012f, 0f, 0.022f)); m.SetFloat("_DarkIntensity", 1f);
                m.SetFloat("_DarkAmount", 1f); m.SetFloat("_DarkEdgeAmount", 0f);
                m.SetFloat("_Opacity", 0.86f); m.SetFloat("_DissolveSoftness", 0.3f);
            });
            // Wide pink beams (white inner glow, darker wine-red edges)
            Material beam = Mat("M_ImpactFanRef_Beam", shader, "T_ImpactFanRef_Beam", 3108, m =>
            {
                m.SetColor("_CoreColor", white); m.SetFloat("_CoreIntensity", 4f);
                m.SetFloat("_CoreThreshold", 0.30f); m.SetFloat("_CoreSoftness", 0.5f);
                m.SetColor("_RimColor", pink); m.SetFloat("_RimIntensity", 1.6f);
                m.SetFloat("_DarkEdgeAmount", 0.5f); m.SetFloat("_DarkEdgeWidth", 0.6f);
                m.SetFloat("_Opacity", 0.9f); m.SetFloat("_DissolveSoftness", 0.25f);
            });
            // Pink hair streaks with a white-hot core
            Material streak = Mat("M_ImpactFanRef_Streak", shader, "T_ImpactFanRef_Streak", 3110, m =>
            {
                m.SetColor("_CoreColor", white); m.SetFloat("_CoreIntensity", 8f);
                m.SetFloat("_CoreThreshold", 0.10f); m.SetFloat("_CoreSoftness", 0.6f);
                m.SetColor("_RimColor", pink); m.SetFloat("_RimIntensity", 1.7f);
                m.SetFloat("_DarkEdgeAmount", 0.35f); m.SetFloat("_DarkEdgeWidth", 0.5f);
                m.SetFloat("_Opacity", 1f); m.SetFloat("_DissolveSoftness", 0.22f);
            });
            // White-hot star flash at the contact point
            Material flash = Mat("M_ImpactFanRef_Flash", shader, "T_ImpactFanRef_Flash", 3112, m =>
            {
                m.SetColor("_CoreColor", new Color(1f, 0.95f, 1f)); m.SetFloat("_CoreIntensity", 9f);
                m.SetFloat("_CoreThreshold", 0.32f); m.SetFloat("_CoreSoftness", 0.5f);
                m.SetColor("_RimColor", new Color(1f, 0.05f, 0.30f)); m.SetFloat("_RimIntensity", 2.5f);
                m.SetFloat("_DarkEdgeAmount", 0f);
                m.SetFloat("_Opacity", 1f); m.SetFloat("_DissolveSoftness", 0.3f);
            });
            // Small pink halo behind the flash
            Material halo = Mat("M_ImpactFanRef_Halo", shader, "T_ImpactFanRef_Halo", 3102, m =>
            {
                m.SetColor("_CoreColor", new Color(1f, 0.7f, 0.95f)); m.SetFloat("_CoreIntensity", 2f);
                m.SetFloat("_CoreThreshold", 0.4f); m.SetFloat("_CoreSoftness", 0.5f);
                m.SetColor("_RimColor", pink); m.SetFloat("_RimIntensity", 1.5f);
                m.SetFloat("_DarkEdgeAmount", 0f);
                m.SetFloat("_Opacity", 0.22f); m.SetFloat("_DissolveSoftness", 0.3f);
            });

            // ---------------------------------------------------------------- particle layers
            // Angles are clockwise-positive on screen (fan opens right / down-right, ~-12..+64 deg).
            var effect = new GameObject(EffectName);
            try
            {
                //            name                material  n   angle      length        width            life          spread seed
                Cone(effect, "Fan cone",          cone, 24f, 3.3f, 5.2f, 0.18f);
                Cone(effect, "Hot white cone",    cone, 22f, 1.9f, 2.0f, 0.14f);
                Fan(effect, "Pink glow body",     glow,   8, -10f, 56f, 1.4f, 2.8f, 0.40f, 0.80f, 0.13f, 0.20f, 0.020f, 2);
                Fan(effect, "Black wedges",       dark,  11, -16f, 54f, 1.7f, 3.1f, 0.24f, 0.60f, 0.17f, 0.26f, 0.025f, 1);
                Fan(effect, "Pink beams",         beam,  18,  -8f, 58f, 1.4f, 3.2f, 0.07f, 0.24f, 0.18f, 0.27f, 0.025f, 3);
                Fan(effect, "Hair streaks wide",  streak, 52, -12f, 64f, 1.0f, 3.5f, 0.010f, 0.040f, 0.17f, 0.29f, 0.030f, 4);
                Fan(effect, "Hair streaks dense", streak, 48,  6f, 46f, 0.8f, 2.4f, 0.008f, 0.026f, 0.13f, 0.22f, 0.020f, 5);
                Radial(effect, "Contact pink halo",  halo,  0.9f, 0.14f, 0.10f);
                Radial(effect, "Contact white flash", flash, 1.1f, 0.11f, 0.12f);
                PrefabUtility.SaveAsPrefabAsset(effect, Folder + "/PF_PinkImpactFan.prefab");
            }
            finally { Object.DestroyImmediate(effect); }

            // ---------------------------------------------------------------- timeline wiring
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var director = contents.GetComponent<PlayableDirector>();
                var timeline = (TimelineAsset)director.playableAsset;
                Vector3 pos = new Vector3(0.12f, 0.42f, -0.38f);
                Quaternion rot = Quaternion.identity;
                Vector3 scale = Vector3.one;
                Transform previous = contents.transform.Find(EffectName);
                if (previous != null)
                {
                    pos = previous.localPosition; rot = previous.localRotation; scale = previous.localScale; // keep hand placement
                    Object.DestroyImmediate(previous.gameObject);
                }
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PF_PinkImpactFan.prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, contents.transform);
                instance.name = EffectName;
                instance.transform.localPosition = pos;
                instance.transform.localRotation = rot;
                instance.transform.localScale = scale;
                instance.SetActive(false);
                ControlTrack track = null;
                double hitTime = 1.0;
                foreach (TrackAsset item in timeline.GetOutputTracks())
                {
                    if (item.name == EffectName) track = item as ControlTrack;
                    if (item.name == "Sakura Impact Playback")
                        foreach (TimelineClip clip in item.GetClips()) { hitTime = clip.start; break; }
                }
                if (track == null) track = timeline.CreateTrack<ControlTrack>(null, EffectName);
                TimelineClip control = null;
                foreach (TimelineClip clip in track.GetClips()) { control = clip; break; }
                bool created = control == null;
                if (created) control = track.CreateClip<ControlPlayableAsset>();
                if (created) control.start = hitTime; // an existing clip keeps its hand-tuned start
                control.duration = ClipDuration;
                control.displayName = "Pink fan / core / black wedge / dissolve";
                var playable = (ControlPlayableAsset)control.asset;
                playable.sourceGameObject.exposedName = Binding;
                playable.updateParticle = true;
                playable.searchHierarchy = true;
                playable.updateDirector = false;
                playable.updateITimeControl = false;
                playable.active = true;
                playable.particleRandomSeed = 173;
                playable.postPlayback = ActivationControlPlayable.PostPlaybackState.Inactive;
                director.SetReferenceValue(Binding, instance);
                EditorUtility.SetDirty(playable);
                EditorUtility.SetDirty(track);
                EditorUtility.SetDirty(timeline);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Pink Impact Fan installed at " + control.start + "s. Shader Graph errors: " + ShaderUtil.ShaderHasError(shader));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        // ---------------------------------------------------------------- helpers
        private static Texture2D Mask(string name)
        {
            string path = Folder + "/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && (importer.sRGBTexture || importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp
                                     || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.sRGBTexture = false;
                importer.alphaIsTransparency = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material Mat(string name, Shader shader, string mask, int queue, System.Action<Material> setup)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created) material = new Material(shader) { name = name };
            else material.shader = shader;
            material.SetTexture("_Mask", Mask(mask));
            material.SetColor("_DarkColor", new Color(0.012f, 0f, 0.02f));
            material.SetFloat("_DarkIntensity", 1f);
            material.SetFloat("_DarkAmount", 0f);
            material.SetFloat("_DissolveOffset", 0f);
            setup(material);
            material.renderQueue = queue;
            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        private static ParticleSystem Base(GameObject parent, string name, Material material, int seed, float spread, int count)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeZ = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(count, 1);
            main.useUnscaledTime = false;
            ps.useAutoRandomSeed = false;
            ps.randomSeed = (uint)(173 + seed * 31);
            var emission = ps.emission;
            emission.rateOverTime = 0;
            // staggered bursts: the fan "opens" over a couple of frames like the reference
            int steps = count > 1 ? 4 : 1;
            var bursts = new ParticleSystem.Burst[steps];
            int left = count;
            for (int i = 0; i < steps; i++)
            {
                int n = i == steps - 1 ? left : count / steps;
                left -= n;
                bursts[i] = new ParticleSystem.Burst(steps == 1 ? 0f : spread * i / (steps - 1), (short)n);
            }
            emission.SetBursts(bursts);
            var shape = ps.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.maxParticleSize = 4f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void Dissolve(ParticleSystem ps, float holdUntil)
        {
            // vertex-colour alpha is the dissolve progress (shader: progress = 1 - alpha)
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, holdUntil), new GradientAlphaKey(0, 1) });
            color.color = gradient;
        }

        private static void Fan(GameObject parent, string name, Material material, int count, float angleMin, float angleMax,
            float lengthMin, float lengthMax, float widthMin, float widthMax, float lifeMin, float lifeMax, float spread, int seed)
        {
            var ps = Base(parent, name, material, seed, spread, count);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startDelay = FanDelay;
            main.startSizeX = new ParticleSystem.MinMaxCurve(lengthMin, lengthMax);
            main.startSizeY = new ParticleSystem.MinMaxCurve(widthMin, widthMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(angleMin * Mathf.Deg2Rad, angleMax * Mathf.Deg2Rad);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            // length shoots out in ~2 frames, width thins to a hair while the dissolve breaks the line up
            size.x = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, .18f), new Keyframe(.10f, 1f), new Keyframe(1, 1.14f)));
            size.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, .45f), new Keyframe(.10f, 1f), new Keyframe(.45f, .72f), new Keyframe(1, .16f)));
            size.z = 1f;
            Dissolve(ps, 0.30f);
            ps.GetComponent<ParticleSystemRenderer>().pivot = new Vector3(0.5f, 0, 0); // origin at the quad's left end
        }

        private static void Cone(GameObject parent, string name, Material material, float angle, float length, float width, float life)
        {
            var ps = Base(parent, name, material, 7, 0f, 1);
            var main = ps.main;
            main.startLifetime = life;
            main.startDelay = FanDelay;
            main.startSizeX = length;
            main.startSizeY = width;
            main.startRotation = angle * Mathf.Deg2Rad;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            size.x = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, .35f), new Keyframe(.10f, 1f), new Keyframe(1, 1.1f)));
            size.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, .55f), new Keyframe(.12f, 1f), new Keyframe(1, .72f)));
            size.z = 1f;
            Dissolve(ps, 0.55f);
            ps.GetComponent<ParticleSystemRenderer>().pivot = new Vector3(0.5f, 0, 0); // apex at the contact point
        }

        private static void Radial(GameObject parent, string name, Material material, float size, float life, float holdUntilLife)
        {
            var ps = Base(parent, name, material, 9, 0f, 1);
            var main = ps.main;
            main.startLifetime = life;
            main.startSizeX = size;
            main.startSizeY = size;
            main.startRotation = 0f;
            var sizeCurve = ps.sizeOverLifetime;
            sizeCurve.enabled = true;
            sizeCurve.separateAxes = false;
            sizeCurve.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, .35f), new Keyframe(.2f, 1f), new Keyframe(1, 1.25f)));
            Dissolve(ps, Mathf.Clamp01(holdUntilLife / life * 0.5f));
        }
    }
}
