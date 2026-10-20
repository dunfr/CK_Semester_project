#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Fixture = CK.SemesterProject.Battle.Tests.BattleVfxIndependentTestFixture;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class BattleVfxSavedEffectLibraryTests
    {
        [Test]
        public void SavedEntryUsesIndependentMetadataAndKeepsArtistNotesAcrossOverwrite()
        {
            using (var fixture = new Fixture())
            {
                string displayName = "벚꽃 저장 " + fixture.Identifier;
                GameObject saved = fixture.SaveAs(displayName);
                string path = AssetDatabase.GetAssetPath(saved);
                AssetImporter importer = AssetImporter.GetAtPath(path);
                object authoredPlayback = fixture.GetPlayback(saved, out _);
                Fixture.SetField(authoredPlayback, "PlaybackSpeed", .37f);
                Fixture.SetField(authoredPlayback, "StartDelay", .12f);
                Fixture.SetField(authoredPlayback, "Lifetime", 1.8f);
                importer.userData = "ArtistNote:preserve this line\nCK.BattleVfxSource:{\"OriginalPrefabGuid\":\""
                    + AssetDatabase.AssetPathToGUID(fixture.SourcePrefabPath) + "\",\"DisplayName\":\"" + displayName
                    + "\",\"HasPlayback\":true,\"BasePlaybackSpeed\":1.7,\"Placement\":"
                    + JsonUtility.ToJson(authoredPlayback) + "}";
                importer.SaveAndReimport();

                saved = fixture.Overwrite(saved);
                object entry = FindEntry(path);
                object expected = fixture.GetPlayback(saved, out float baseSpeed);
                object actual = Fixture.Field<object>(entry, "Placement");

                Assert.That(Fixture.Field<string>(entry, "DisplayName"), Is.EqualTo(displayName));
                Assert.That(entry.GetType().GetField("Usage"), Is.Null);
                Assert.That(actual.GetType().FullName, Is.EqualTo(Fixture.PlacementTypeName));
                Fixture.AssertVector(Fixture.Field<Vector3>(actual, "Position"), Fixture.Field<Vector3>(expected, "Position"));
                Fixture.AssertVector(Fixture.Field<Vector3>(actual, "Rotation"), Fixture.Field<Vector3>(expected, "Rotation"));
                Assert.That(Fixture.Field<float>(entry, "BasePlaybackSpeed"), Is.EqualTo(baseSpeed));
                Assert.That(baseSpeed, Is.EqualTo(1.7f));
                Assert.That(Fixture.Field<float>(actual, "PlaybackSpeed"), Is.EqualTo(.37f));
                Assert.That(Fixture.Field<float>(actual, "StartDelay"), Is.EqualTo(.12f));
                Assert.That(Fixture.Field<float>(actual, "Lifetime"), Is.EqualTo(1.8f));
                Assert.That(AssetImporter.GetAtPath(path).userData, Does.Contain("ArtistNote:preserve this line"));
                Assert.That(fixture.GetSourceGuid(saved), Is.EqualTo(AssetDatabase.AssetPathToGUID(fixture.SourcePrefabPath)));
            }
        }

        [Test]
        public void LegacyMetadataNameAndSelfReferenceFallbackRemainReadableWithoutRecursion()
        {
            using (var fixture = new Fixture())
            {
                GameObject saved = fixture.SaveAs();
                string oldPath = AssetDatabase.GetAssetPath(saved);
                string legacyName = "Vfx_" + Guid.NewGuid().ToString("N") + "_SK00_Charge";
                Assert.That(AssetDatabase.RenameAsset(oldPath, legacyName), Is.Empty);
                string path = oldPath.Substring(0, oldPath.LastIndexOf('/') + 1) + legacyName + ".prefab";
                fixture.SnapshotPaths.Add(path);
                AssetImporter importer = AssetImporter.GetAtPath(path);
                string originalGuid = AssetDatabase.AssetPathToGUID(fixture.SourcePrefabPath);
                importer.userData = "ArtistNote:legacy\nCK.BattleVfxSource:{\"OriginalPrefabGuid\":\"" + originalGuid + "\"}";
                importer.SaveAndReimport();

                Assert.That(Fixture.Field<string>(FindEntry(path), "DisplayName"), Is.EqualTo(fixture.SourcePrefab.name));

                importer = AssetImporter.GetAtPath(path);
                string savedGuid = AssetDatabase.AssetPathToGUID(path);
                importer.userData = "CK.BattleVfxSource:{\"OriginalPrefabGuid\":\"" + savedGuid + "\"}";
                importer.SaveAndReimport();

                Assert.That(Fixture.Field<string>(FindEntry(path), "DisplayName"),
                    Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(path).name));
            }
        }

        [Test]
        public void LegacySavedFilenameAndPlaybackKeepTimingWithoutSkillConnectionFields()
        {
            using (var fixture = new Fixture())
            {
                GameObject saved = fixture.SaveAs();
                string oldPath = AssetDatabase.GetAssetPath(saved);
                string legacyName = "Vfx_" + Guid.NewGuid().ToString("N") + "_SK00_Stage1_Hit";
                Assert.That(AssetDatabase.RenameAsset(oldPath, legacyName), Is.Empty);
                string path = oldPath.Substring(0, oldPath.LastIndexOf('/') + 1) + legacyName + ".prefab";
                fixture.SnapshotPaths.Add(path);
                AssetImporter importer = AssetImporter.GetAtPath(path);
                importer.userData = "CK.BattleVfxSource:{\"OriginalPrefabGuid\":\""
                    + AssetDatabase.AssetPathToGUID(fixture.SourcePrefabPath)
                    + "\",\"DisplayName\":\"이전 저장본\",\"HasPlayback\":true,\"BasePlaybackSpeed\":1.7,"
                    + "\"Placement\":{\"Enabled\":true,\"Anchor\":1,\"Follow\":true,"
                    + "\"Position\":{\"x\":9,\"y\":8,\"z\":7},\"Rotation\":{\"x\":12,\"y\":34,\"z\":56},"
                    + "\"Scale\":1.2,\"PlaybackSpeed\":0.37,\"StartDelay\":0.12,\"Lifetime\":1.8}}";
                importer.SaveAndReimport();

                object entry = FindEntry(path);
                object placement = Fixture.Field<object>(entry, "Placement");

                Assert.That(Fixture.Field<string>(entry, "DisplayName"), Is.EqualTo("이전 저장본"));
                Assert.That(entry.GetType().GetField("Usage"), Is.Null);
                Fixture.AssertVector(Fixture.Field<Vector3>(placement, "Position"), new Vector3(9f, 8f, 7f));
                Fixture.AssertVector(Fixture.Field<Vector3>(placement, "Rotation"), new Vector3(12f, 34f, 56f));
                Assert.That(Fixture.Field<float>(placement, "Scale"), Is.EqualTo(1.2f));
                Assert.That(Fixture.Field<float>(placement, "PlaybackSpeed"), Is.EqualTo(.37f));
                Assert.That(Fixture.Field<float>(placement, "StartDelay"), Is.EqualTo(.12f));
                Assert.That(Fixture.Field<float>(placement, "Lifetime"), Is.EqualTo(1.8f));
                Assert.That(Fixture.Field<float>(entry, "BasePlaybackSpeed"), Is.EqualTo(1.7f));
                Assert.That(placement.GetType().GetField("Follow"), Is.Null);
            }
        }

        [Test]
        public void DeleteRemovesOnlyChosenSnapshotAndMatchingSceneMarkers()
        {
            using (var fixture = new Fixture())
            {
                GameObject saved = fixture.SaveAs();
                string path = AssetDatabase.GetAssetPath(saved);
                Component marker = fixture.Source.GetComponent(Fixture.TemplateType);
                GameObject unrelated = fixture.CreateObject("Unrelated Authoring Object");
                Component otherMarker = fixture.AddMarker(unrelated);
                otherMarker.GetType().GetMethod("SetSavedPrefab")
                    .Invoke(otherMarker, new object[] { fixture.SourcePrefab });

                Fixture.FindType(Fixture.LibraryTypeName).GetMethod("Delete").Invoke(null, new object[] { saved });

                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Null);
                Assert.That(marker.GetType().GetProperty("SavedPrefab").GetValue(marker), Is.Null);
                Assert.That(otherMarker.GetType().GetProperty("SavedPrefab").GetValue(otherMarker),
                    Is.EqualTo(fixture.SourcePrefab));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(fixture.SourcePrefabPath), Is.Not.Null);
                Assert.That(Entries().Any(entry => Fixture.Field<string>(entry, "AssetPath") == path), Is.False);
            }
        }

        [Test]
        public void OriginalPrefabCannotBeDeletedAndDoesNotAppearInSavedList()
        {
            using (var fixture = new Fixture())
            {
                GameObject saved = fixture.SaveAs();
                string savedPath = AssetDatabase.GetAssetPath(saved);

                var exception = Assert.Throws<TargetInvocationException>(() => Fixture.FindType(Fixture.LibraryTypeName)
                    .GetMethod("Delete").Invoke(null, new object[] { fixture.SourcePrefab }));

                Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(fixture.SourcePrefabPath), Is.Not.Null);
                Assert.That(fixture.SavedPrefab(fixture.Source), Is.EqualTo(saved));
                Assert.That(Entries().Any(entry => Fixture.Field<string>(entry, "AssetPath") == fixture.SourcePrefabPath),
                    Is.False);
                Assert.That(FindEntry(savedPath), Is.Not.Null);
            }
        }

        private static object FindEntry(string path)
        {
            return Entries().Single(entry => Fixture.Field<string>(entry, "AssetPath") == path);
        }

        private static IEnumerable<object> Entries()
        {
            return ((Array)Fixture.FindType(Fixture.LibraryTypeName).GetMethod("GetEntries").Invoke(null, null))
                .Cast<object>();
        }
    }
}
#endif
