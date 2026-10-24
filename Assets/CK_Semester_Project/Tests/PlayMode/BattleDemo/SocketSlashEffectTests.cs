#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class SocketSlashEffectTests
    {
        [UnityTest]
        public IEnumerator BladeFollowsSocketWhileReleasedImpactRemainsAtContact()
        {
            using (var fixture = new SlashFixture())
            {
                fixture.Attach();
                fixture.Root.SetActive(true);
                Assert.That(fixture.Root.transform.parent, Is.EqualTo(fixture.Socket));
                AssertVector(fixture.Root.transform.localPosition, fixture.Offset);
                AssertRotation(fixture.Root.transform.localRotation, fixture.Rotation);
                AssertVector(fixture.Root.transform.localScale, fixture.Scale);
                Assert.That(fixture.LegacyPlacement.enabled, Is.False,
                    "The former enemy placement must not override the weapon socket.");
                Assert.That(fixture.OriginalImpact.activeSelf, Is.False);

                fixture.Socket.SetPositionAndRotation(new Vector3(2f, 3f, -4f), Quaternion.Euler(26f, 121f, -17f));
                yield return null;

                AssertVector(fixture.Root.transform.position, fixture.Socket.TransformPoint(fixture.Offset));
                AssertRotation(fixture.Root.transform.rotation, fixture.Socket.rotation * fixture.Rotation);
                GameObject impact = fixture.ReleaseImpact();
                Assert.That(impact, Is.Not.Null);
                Assert.That(fixture.ImpactInstance, Is.EqualTo(impact));
                Assert.That(impact.transform.parent, Is.Null);
                Assert.That(Vector3.Distance(impact.transform.position, fixture.Target.position), Is.LessThan(3f));
                Assert.That(impact.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(1));
                Assert.That(fixture.ReleaseImpact(), Is.EqualTo(impact), "One strike must not release duplicate impacts.");
                Vector3 contactPosition = impact.transform.position;
                Quaternion contactRotation = impact.transform.rotation;

                fixture.Socket.position += new Vector3(50f, 10f, -30f);
                fixture.Target.position += new Vector3(-12f, 0f, 16f);
                yield return null;

                AssertVector(impact.transform.position, contactPosition);
                AssertRotation(impact.transform.rotation, contactRotation);
                fixture.FinishSwing();
                Assert.That(fixture.Root.activeSelf, Is.True, "The released impact must be allowed to finish.");
                Assert.That(fixture.BladeRenderer.enabled, Is.False);
                Assert.That(fixture.BladeLight.enabled, Is.False);
                Assert.That(impact.activeInHierarchy, Is.True);
                Assert.That(fixture.OriginalImpact.transform.parent, Is.EqualTo(fixture.Root.transform));

                fixture.ClearImpact();
                yield return null;
                Assert.That(fixture.ImpactInstance, Is.Null);
                Assert.That(impact == null, Is.True);
                Assert.That(fixture.OriginalImpact != null, Is.True, "Only the released clone may be removed.");
            }
        }

        [UnityTest]
        public IEnumerator ReattachRestoresBladeAndDisableRemovesOwnedImpactWithoutDestroyingAuthoredChildren()
        {
            using (var fixture = new SlashFixture())
            {
                fixture.Attach();
                fixture.Root.SetActive(true);
                GameObject previousImpact = fixture.ReleaseImpact();
                fixture.FinishSwing();
                Assert.That(fixture.BladeRenderer.enabled, Is.False);
                Assert.That(fixture.BladeLight.enabled, Is.False);

                fixture.Attach();
                yield return null;

                Assert.That(previousImpact == null, Is.True);
                Assert.That(fixture.ImpactInstance, Is.Null);
                Assert.That(fixture.BladeRenderer.enabled, Is.True);
                Assert.That(fixture.BladeLight.enabled, Is.True);
                Assert.That(fixture.OriginalImpact.activeSelf, Is.False);
                AssertVector(fixture.Root.transform.localPosition, fixture.Offset);
                AssertRotation(fixture.Root.transform.localRotation, fixture.Rotation);
                GameObject currentImpact = fixture.ReleaseImpact();
                Assert.That(currentImpact, Is.Not.Null);

                fixture.Root.SetActive(false);
                yield return null;

                Assert.That(fixture.ImpactInstance, Is.Null);
                Assert.That(currentImpact == null, Is.True);
                Assert.That(fixture.OriginalImpact != null, Is.True);
                Assert.That(fixture.OriginalImpact.transform.parent, Is.EqualTo(fixture.Root.transform));
                Assert.That(fixture.OriginalImpact.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(1));
                Assert.That(fixture.Socket.childCount, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator ActualPrefabImpactCloneKeepsAuthoredScaleAfterOriginalOnEnableShrinksItsFirstFrame()
        {
            using (var fixture = new SlashFixture())
            {
                const string prefabPath = "Assets/CK_Semester_Project/Prototype/Prefabs/BattleVfxTest/FuchsiaSkill01SakuraSlash.prefab";
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab, Is.Not.Null);
                Type streakType = FindType("SakuraImpactStreakPlayer");
                Component authoredStreak = prefab.GetComponentsInChildren(streakType, true).Single();
                Vector3 authoredScale = authoredStreak.transform.localScale;
                GameObject instance = Object.Instantiate(prefab);
                try
                {
                    Component original = instance.GetComponentsInChildren(streakType, true).Single();
                    AssertVector(original.transform.localScale, EvaluateStreakScale(authoredStreak, authoredScale, 0f));
                    Assert.That(original.transform.localScale.x, Is.LessThan(authoredScale.x),
                        "This prefab must exercise the OnEnable frame-zero shrinkage that caused the regression.");
                    Component slash = instance.GetComponent(FindType("SocketSlashEffect"));
                    Assert.That(slash, Is.Not.Null);
                    slash.GetType().GetMethod("Attach").Invoke(slash, new object[] { fixture.Socket });
                    GameObject impact = (GameObject)slash.GetType().GetMethod("ReleaseImpact").Invoke(slash,
                        new object[] { fixture.Target, fixture.Camera });
                    Assert.That(impact, Is.Not.Null);
                    Component clone = impact.GetComponentsInChildren(streakType, true).Single();

                    AssertVector((Vector3)streakType.GetProperty("AuthoredLocalScale").GetValue(clone), authoredScale);
                    AssertVector(clone.transform.localScale, EvaluateStreakScale(authoredStreak, authoredScale, 0f));
                    Assert.That(original.gameObject.activeSelf, Is.False);
                    Assert.That(original.transform.IsChildOf(instance.transform), Is.True);
                    yield return null;

                    streakType.GetMethod("Preview").Invoke(clone, new object[] { .5f });
                    AssertVector(clone.transform.localScale, EvaluateStreakScale(authoredStreak, authoredScale, .5f));
                    AssertVector((Vector3)streakType.GetProperty("AuthoredLocalScale").GetValue(clone), authoredScale);
                    AssertVector(authoredStreak.transform.localScale, authoredScale);
                    slash.GetType().GetMethod("ClearImpact").Invoke(slash, null);
                    yield return null;
                    Assert.That(impact == null, Is.True);
                }
                finally
                {
                    if (instance != null)
                    {
                        Object.DestroyImmediate(instance);
                    }
                }
            }
        }

        private static Vector3 EvaluateStreakScale(Component player, Vector3 authoredScale, float time)
        {
            AnimationCurve length = (AnimationCurve)player.GetType()
                .GetField("lengthCurve", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            AnimationCurve thickness = (AnimationCurve)player.GetType()
                .GetField("thicknessCurve", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            return new Vector3(authoredScale.x * length.Evaluate(time),
                authoredScale.y * thickness.Evaluate(time), authoredScale.z);
        }

        [UnityTest]
        public IEnumerator ActualPresentationSpawnsOnChildAnimatorBladeSocketAndRestoresActorOnCancel()
        {
            const string characterPath = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Prefabs/FuchsiaUpdatedAnimated.prefab";
            const string enhancementPath = "Assets/CK_Semester_Project/Prototype/Data/BattleVfxTest/BattleVfxTestEnhancement.asset";
            GameObject characterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(characterPath);
            ScriptableObject enhancement = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(enhancementPath);
            Assert.That(characterPrefab, Is.Not.Null);
            Assert.That(enhancement, Is.Not.Null);
            string enhancementBefore = UnityEditor.EditorJsonUtility.ToJson(enhancement);
            var actor = new GameObject("Presentation Actor With Child Animator");
            actor.transform.SetPositionAndRotation(new Vector3(-4f, 0f, -1f), Quaternion.Euler(0f, 20f, 0f));
            Vector3 actorHome = actor.transform.position;
            Quaternion actorRotation = actor.transform.rotation;
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = "Presentation Socket Test Target";
            target.transform.position = new Vector3(2f, 0f, 5f);
            Vector3 targetHome = target.transform.position;
            object presentation = null;
            GameObject blade = null;
            GameObject impact = null;
            try
            {
                GameObject character = Object.Instantiate(characterPrefab, actor.transform, false);
                Animator childAnimator = character.GetComponentsInChildren<Animator>(true)
                    .First(item => item.isActiveAndEnabled);
                Assert.That(actor.GetComponent<Animator>(), Is.Null);
                Transform socket = (Transform)FindType("WeaponEffectSocket").GetMethod("Find")
                    .Invoke(null, new object[] { actor.transform });
                Assert.That(socket, Is.Not.Null, "The actual animated character must carry a blade socket.");
                Type presentationType = FindType("CK.SemesterProject.Tutorial.TutorialBattleActionPresentation");
                presentation = Activator.CreateInstance(presentationType,
                    new object[] { actor.transform, target.transform, 1.5f, "monster", "player", enhancement, 1 });
                Assert.That(presentationType.GetField("_animator", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(presentation), Is.EqualTo(childAnimator));
                object result = CreatePresentationResult();
                const float seconds = 1.2f;
                float chargeFraction = (float)enhancement.GetType().GetMethod("GetChargeProgressFraction")
                    .Invoke(enhancement, new object[] { "SK00", 1, seconds });
                GameObject effectPrefab = (GameObject)enhancement.GetType().GetMethod("GetEffect")
                    .Invoke(enhancement, new object[] { "SK00", 1 });
                Assert.That(effectPrefab, Is.Not.Null);
                Action<float> sampleAttack = progress => presentationType.GetMethod("Sample").Invoke(presentation,
                    new object[] { result, chargeFraction + (1f - chargeFraction) * progress, seconds });

                sampleAttack(.3f);

                blade = (GameObject)presentationType.GetField("_effectInstance", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(presentation);
                Assert.That(blade, Is.Not.Null, "The slash must start before the .35 impact point.");
                Assert.That(blade.transform.parent, Is.EqualTo(socket));
                AssertVector(blade.transform.localPosition, effectPrefab.transform.localPosition);
                AssertRotation(blade.transform.localRotation, effectPrefab.transform.localRotation);
                Type slashType = FindType("SocketSlashEffect");
                Component slash = blade.GetComponent(slashType);
                Assert.That(slash, Is.Not.Null);
                Assert.That(slashType.GetProperty("ImpactInstance").GetValue(slash), Is.Null);
                yield return null;
                Assert.That(blade.transform.parent, Is.EqualTo(socket));
                AssertVector(blade.transform.position, socket.TransformPoint(effectPrefab.transform.localPosition));

                sampleAttack(.36f);

                impact = (GameObject)slashType.GetProperty("ImpactInstance").GetValue(slash);
                Assert.That(impact, Is.Not.Null);
                Assert.That(impact.transform.parent, Is.Null);
                Assert.That(Vector3.Distance(impact.transform.position, target.transform.position), Is.LessThan(3f));
                Vector3 contactPosition = impact.transform.position;

                sampleAttack(.5f);

                Assert.That(blade.transform.parent, Is.EqualTo(socket));
                Assert.That(blade.GetComponentsInChildren<Renderer>(true)
                    .Where(item => item.gameObject.activeInHierarchy).All(item => !item.enabled), Is.True);
                Assert.That(blade.GetComponentsInChildren<Light>(true)
                    .Where(item => item.gameObject.activeInHierarchy).All(item => !item.enabled), Is.True);
                Assert.That(slashType.GetProperty("ImpactInstance").GetValue(slash), Is.EqualTo(impact));
                AssertVector(impact.transform.position, contactPosition);
                presentationType.GetMethod("Restore", Type.EmptyTypes).Invoke(presentation, null);
                AssertVector(actor.transform.position, actorHome);
                AssertRotation(actor.transform.rotation, actorRotation);
                AssertVector(target.transform.position, targetHome);
                yield return null;

                Assert.That(blade == null, Is.True);
                Assert.That(impact == null, Is.True);
                Assert.That(actor.GetComponentsInChildren(slashType, true), Is.Empty);
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(enhancement), Is.EqualTo(enhancementBefore));
            }
            finally
            {
                if (presentation != null)
                {
                    presentation.GetType().GetMethod("Restore", Type.EmptyTypes).Invoke(presentation, null);
                }
                if (impact != null)
                {
                    Object.DestroyImmediate(impact);
                }
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(target);
            }
        }

        private static BattleActionResult CreatePresentationResult()
        {
            var skill = new SkillData("SK00", "Socket Slash", 10);
            var player = new CombatantData("player", "Player", BattleTeam.Player, 1000, 100, 50, new[] { skill });
            var monster = new CombatantData("monster", "Monster", BattleTeam.Monster, 1000, 100, 50,
                new[] { new SkillData("counter", "Counter", 1) });
            var session = new BattleSession(randomSeed: 17);
            session.Start(new[] { new BattleParticipant("player", player), new BattleParticipant("monster", monster) });
            BattleSnapshot state = session.GetSnapshot();
            Assert.That(state.CurrentActorId, Is.EqualTo("player"));
            var request = new BattleActionRequest(state.TurnId, "player", BattleActionKind.Skill, "SK00", "monster");
            Assert.That(session.TrySubmit(request, out BattleActionResult result, out BattleActionError error),
                Is.True, error.ToString());
            return result;
        }

        private static Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name))
                .First(type => type != null);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.001f));
        }

        private static void AssertRotation(Quaternion actual, Quaternion expected)
        {
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(.05f));
        }

        private sealed class SlashFixture : IDisposable
        {
            private readonly GameObject _actor;
            private readonly GameObject _target;
            private readonly GameObject _camera;
            private readonly Component _slash;

            public GameObject Root { get; }
            public Transform Socket { get; }
            public Transform Target => _target.transform;
            public Camera Camera => _camera.GetComponent<Camera>();
            public GameObject OriginalImpact { get; }
            public Renderer BladeRenderer { get; }
            public Light BladeLight { get; }
            public Behaviour LegacyPlacement { get; }
            public Vector3 Offset => new Vector3(.2f, .3f, -.4f);
            public Quaternion Rotation => Quaternion.Euler(12f, 23f, 34f);
            public Vector3 Scale => new Vector3(.7f, 1.1f, 1.3f);
            public GameObject ImpactInstance => (GameObject)_slash.GetType().GetProperty("ImpactInstance").GetValue(_slash);

            public SlashFixture()
            {
                _actor = new GameObject("Socket Slash Test Actor");
                var socket = new GameObject("BladeSocket");
                socket.transform.SetParent(_actor.transform, false);
                Socket = socket.transform;
                Socket.SetLocalPositionAndRotation(new Vector3(-2f, 1f, 3f), Quaternion.Euler(0f, 32f, 0f));
                _target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _target.name = "Socket Slash Test Target";
                _target.transform.position = new Vector3(12f, 0f, -8f);
                _camera = new GameObject("Socket Slash Test Camera");
                _camera.transform.position = _target.transform.position + new Vector3(0f, 2f, -5f);
                _camera.transform.LookAt(_target.transform.position);
                _camera.AddComponent<Camera>().enabled = false;
                Root = new GameObject("Socket Slash Test Effect");
                Root.SetActive(false);
                Root.transform.SetLocalPositionAndRotation(Offset, Rotation);
                Root.transform.localScale = Scale;
                GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Quad);
                blade.name = "Blade Visual";
                blade.transform.SetParent(Root.transform, false);
                BladeRenderer = blade.GetComponent<Renderer>();
                var light = new GameObject("Blade Light");
                light.transform.SetParent(Root.transform, false);
                BladeLight = light.AddComponent<Light>();
                BladeLight.intensity = 2f;
                OriginalImpact = new GameObject("Authored Impact");
                OriginalImpact.transform.SetParent(Root.transform, false);
                OriginalImpact.transform.localPosition = new Vector3(.1f, .2f, .3f);
                ParticleSystem particles = OriginalImpact.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false;
                main.loop = false;
                main.startLifetime = 2f;
                LegacyPlacement = (Behaviour)Root.AddComponent(FindType("CK.SemesterProject.Tutorial.TutorialSlashVfxPlacement"));
                _slash = Root.AddComponent(FindType("SocketSlashEffect"));
                _slash.GetType().GetField("_impactRoots", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(_slash, new[] { OriginalImpact.transform });
            }

            public void Attach()
            {
                Invoke("Attach", Socket);
            }

            public GameObject ReleaseImpact()
            {
                return (GameObject)Invoke("ReleaseImpact", Target, _camera.GetComponent<Camera>());
            }

            public void FinishSwing()
            {
                Invoke("FinishSwing");
            }

            public void ClearImpact()
            {
                Invoke("ClearImpact");
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_actor);
                Object.DestroyImmediate(_target);
                Object.DestroyImmediate(_camera);
                if (Root != null)
                {
                    Object.DestroyImmediate(Root);
                }
            }

            private object Invoke(string method, params object[] arguments)
            {
                return _slash.GetType().GetMethod(method).Invoke(_slash, arguments);
            }
        }
    }
}
#endif
