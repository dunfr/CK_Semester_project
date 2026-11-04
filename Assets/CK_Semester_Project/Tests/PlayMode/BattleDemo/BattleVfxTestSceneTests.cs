using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class BattleVfxTestSceneTests
    {
        private const string ScenePath = "Assets/CK_Semester_Project/Prototype/Scenes/BattleVfxTest.unity";

        [UnitySetUp]
        public IEnumerator LoadWorkspace()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator ReleaseMouse()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorkspaceKeepsDummyStillWithoutStartingCombat()
        {
            GameObject controls = GameObject.Find("VFX Test Controls");
            Assert.That(controls, Is.Not.Null);
            Assert.That(controls.GetComponent("BattleVfxTestController"), Is.Not.Null);
            Assert.That(controls.GetComponentsInChildren<UnityEngine.UI.Graphic>(true), Is.Empty);
            Assert.That(controls.GetComponent<Canvas>(), Is.Null);
            Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Any(component => component.GetType().Name == "TutorialBattleController"), Is.False);
            Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Any(component => component.GetType().Name == "PrototypeHudPresenter"), Is.False);
            Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Any(component => component.GetType().Name == "BattleVfxSavedEffects"), Is.True);

            GameObject dummy = GameObject.Find("VFX Training Dummy");
            Assert.That(dummy, Is.Not.Null);
            Vector3 position = dummy.transform.position;
            Quaternion rotation = dummy.transform.rotation;
            Assert.That(((Behaviour)dummy.GetComponent("EnemyStateMachine")).enabled, Is.False);
            Assert.That(dummy.GetComponent<NavMeshAgent>().enabled, Is.False);

            yield return new WaitForSeconds(.25f);

            Assert.That(Vector3.Distance(dummy.transform.position, position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(dummy.transform.rotation, rotation), Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator RestartRestoresPlayerAndDummyWithoutChangingSavedEffects()
        {
            Component controls = GameObject.Find("VFX Test Controls").GetComponent("BattleVfxTestController");
            FieldInfo playerField = controls.GetType().GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo dummyField = controls.GetType().GetField("_dummy", BindingFlags.Instance | BindingFlags.NonPublic);
            Component player = (Component)playerField.GetValue(controls);
            Component dummy = (Component)dummyField.GetValue(controls);
            Pose playerHome = (Pose)controls.GetType().GetField("_playerHome", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controls);
            Pose dummyHome = (Pose)controls.GetType().GetField("_dummyHome", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controls);
            GameObject library = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Single(component => component.GetType().Name == "BattleVfxSavedEffects").gameObject;
            Vector3 libraryPosition = library.transform.position;

            player.GetType().GetMethod("Teleport").Invoke(player, new object[] { playerHome.position + Vector3.right * 3f });
            player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            dummy.transform.SetPositionAndRotation(dummyHome.position + Vector3.back * 2f, Quaternion.identity);
            dummy.gameObject.SetActive(false);
            controls.GetType().GetMethod("RestartPreview").Invoke(controls, null);

            Assert.That(Vector3.Distance(player.transform.position, playerHome.position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(player.transform.rotation, playerHome.rotation), Is.LessThan(.01f));
            Assert.That(Vector3.Distance(dummy.transform.position, dummyHome.position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(dummy.transform.rotation, dummyHome.rotation), Is.LessThan(.01f));
            Assert.That(dummy.gameObject.activeSelf, Is.True);
            Assert.That(((Behaviour)dummy).enabled, Is.False);
            Assert.That(dummy.GetComponent<NavMeshAgent>().enabled, Is.False);
            Assert.That(library.transform.position, Is.EqualTo(libraryPosition));
            Assert.That(library.activeSelf, Is.True);
            yield return null;
        }
    }
}
