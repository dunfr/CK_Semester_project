#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Fixture = CK.SemesterProject.Battle.Tests.BattleVfxIndependentTestFixture;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class BattleVfxScenePlacementSaverTests
    {
        private const string SlashTypeName = "CK.SemesterProject.Tutorial.TutorialSlashVfxPlacement";
        private const string ChargeTypeName = "CK.SemesterProject.Tutorial.TutorialChargeVfxPlacement";

        [Test]
        public void SnapshotPreservesChildrenAndRecreatesPoseRelativeToRotatedScaledDummy()
        {
            using (var fixture = new Fixture())
            {
                GameObject source = fixture.Source;
                source.AddComponent(Fixture.FindType(SlashTypeName));
                source.transform.Find("Particles").gameObject.AddComponent(Fixture.FindType(ChargeTypeName));
                Vector3 position = source.transform.position;
                Quaternion rotation = source.transform.rotation;
                Vector3 scale = source.transform.lossyScale;
                Transform child = source.transform.Find("Particles");
                Vector3 childPosition = child.position;
                Quaternion childRotation = child.rotation;
                Vector3 offset = Quaternion.Inverse(fixture.Dummy.rotation) * (position - fixture.Dummy.position);
                Quaternion relativeRotation = Quaternion.Inverse(fixture.Dummy.rotation) * rotation;

                GameObject saved = fixture.SaveAs();
                object placement = fixture.GetPlayback(saved, out float baseSpeed);

                Assert.That(placement.GetType().FullName, Is.EqualTo(Fixture.PlacementTypeName));
                Assert.That(placement.GetType().GetField("Enabled"), Is.Null);
                Assert.That(placement.GetType().GetField("Anchor"), Is.Null);
                Assert.That(placement.GetType().GetField("Follow"), Is.Null);
                Fixture.AssertVector(Fixture.Field<Vector3>(placement, "Position"), offset);
                Fixture.AssertRotation(Quaternion.Euler(Fixture.Field<Vector3>(placement, "Rotation")), relativeRotation);
                Assert.That(Fixture.Field<float>(placement, "Scale"), Is.EqualTo(1f));
                Assert.That(baseSpeed, Is.EqualTo(1f));
                Fixture.AssertVector(saved.transform.localPosition, Vector3.zero);
                Fixture.AssertRotation(saved.transform.localRotation, Quaternion.identity);
                Fixture.AssertVector(saved.transform.localScale, scale);
                Assert.That(saved.GetComponent(Fixture.TemplateType), Is.Null);
                Assert.That(((Behaviour)saved.GetComponent(Fixture.FindType(SlashTypeName))).enabled, Is.False);
                Assert.That(((Behaviour)saved.GetComponentInChildren(Fixture.FindType(ChargeTypeName), true)).enabled,
                    Is.False);
                Assert.That(((Behaviour)source.GetComponent(Fixture.FindType(SlashTypeName))).enabled, Is.True);

                GameObject instance = fixture.Instantiate(saved);
                Fixture.Apply(placement, instance, fixture.Dummy);
                Fixture.AssertVector(instance.transform.position, position);
                Fixture.AssertRotation(instance.transform.rotation, rotation);
                Fixture.AssertVector(instance.transform.lossyScale, scale);
                Fixture.AssertVector(instance.transform.Find("Particles").position, childPosition);
                Fixture.AssertRotation(instance.transform.Find("Particles").rotation, childRotation);
                Fixture.AssertVector(instance.transform.Find("Particles").localScale, child.localScale);
                Assert.That(instance.transform.Find("Particles").GetComponent<ParticleSystem>().main.simulationSpeed,
                    Is.EqualTo(.37f));
                Assert.That(instance.transform.Find("Inspector Added Child").gameObject.activeSelf, Is.False);

                fixture.Dummy.SetPositionAndRotation(new Vector3(-8f, 5f, 3f), Quaternion.Euler(10f, 210f, -7f));
                GameObject moved = fixture.Instantiate(saved);
                Fixture.Apply(placement, moved, fixture.Dummy);
                Vector3 movedPosition = fixture.Dummy.position + fixture.Dummy.rotation * offset;
                Quaternion movedRotation = fixture.Dummy.rotation * relativeRotation;
                Fixture.AssertVector(moved.transform.position, movedPosition);
                Fixture.AssertRotation(moved.transform.rotation, movedRotation);
                Fixture.AssertVector(moved.transform.lossyScale, scale);
                Fixture.AssertVector(moved.transform.Find("Particles").position,
                    Matrix4x4.TRS(movedPosition, movedRotation, scale).MultiplyPoint3x4(child.localPosition));
                Fixture.AssertVector(source.transform.position, position);
                Fixture.AssertRotation(source.transform.rotation, rotation);
                Fixture.AssertVector(source.transform.lossyScale, scale);
                Assert.That(source.activeSelf, Is.True);
                Assert.That(source.transform.parent, Is.Null);
                Assert.That(fixture.SourcePrefab.transform.Find("Particles").GetComponent<BoxCollider>(), Is.Null);
            }
        }

        [Test]
        public void OverwriteKeepsSnapshotGuidAndBakesNewChildAndRootValues()
        {
            using (var fixture = new Fixture())
            {
                GameObject first = fixture.SaveAs();
                string path = AssetDatabase.GetAssetPath(first);
                string guid = AssetDatabase.AssetPathToGUID(path);
                string name = fixture.GetDisplayName(first);
                fixture.Source.transform.localScale = new Vector3(.4f, 1.5f, 2.2f);
                Transform child = fixture.Source.transform.Find("Particles");
                child.localPosition = new Vector3(4.2f, -1.3f, 6.4f);
                child.GetComponent<BoxCollider>().size = new Vector3(4f, 5f, 6f);
                var main = child.GetComponent<ParticleSystem>().main;
                main.simulationSpeed = .81f;
                var added = new GameObject("Added On Second Save");
                added.transform.SetParent(fixture.Source.transform, false);
                added.transform.localRotation = Quaternion.Euler(23f, 41f, 67f);

                GameObject second = fixture.Overwrite(first);

                Assert.That(AssetDatabase.GetAssetPath(second), Is.EqualTo(path));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(fixture.GetDisplayName(second), Is.EqualTo(name));
                Assert.That(fixture.SnapshotPaths, Has.Count.EqualTo(1));
                Assert.That(fixture.Source.GetComponents(Fixture.TemplateType).Length, Is.EqualTo(1));
                Assert.That(second.GetComponent(Fixture.TemplateType), Is.Null);
                Fixture.AssertVector(second.transform.localScale, fixture.Source.transform.lossyScale);
                Fixture.AssertVector(second.transform.Find("Particles").localPosition, child.localPosition);
                Fixture.AssertVector(second.transform.Find("Particles").GetComponent<BoxCollider>().size,
                    new Vector3(4f, 5f, 6f));
                Assert.That(second.transform.Find("Particles").GetComponent<ParticleSystem>().main.simulationSpeed,
                    Is.EqualTo(.81f));
                Fixture.AssertRotation(second.transform.Find("Added On Second Save").localRotation,
                    added.transform.localRotation);
            }
        }

        [Test]
        public void SavedFilenameHasUniqueIdentityWithoutSkillOrEnhancementSlot()
        {
            using (var fixture = new Fixture())
            {
                GameObject saved = fixture.SaveAs();
                string name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(saved));

                Assert.That(name, Does.StartWith("Vfx_"));
                Assert.That(name.Length, Is.EqualTo(36));
                Assert.That(Guid.TryParseExact(name.Substring(4), "N", out _), Is.True);
                Assert.That(fixture.GetSourceGuid(saved), Is.EqualTo(AssetDatabase.AssetPathToGUID(fixture.SourcePrefabPath)));
            }
        }
    }
}
#endif
