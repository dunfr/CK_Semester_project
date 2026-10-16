using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class BattleVfxTestSceneTests
    {
        [UnityTearDown]
        public IEnumerator StopBattle()
        {
            GameObject controls = GameObject.Find("VFX Test Controls");
            if (controls != null)
            {
                Component lab = controls.GetComponent("BattleVfxTestController");
                Component battle = (Component)lab.GetType().GetProperty("Battle").GetValue(lab);
                battle.GetType().GetMethod("CancelBattle").Invoke(battle, null);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllSkillStagesSpawnConfiguredEffectsAndCanRestart()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CK_Semester_Project/Prototype/Scenes/BattleVfxTest.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            yield return new WaitForSeconds(.25f);
            Component lab = GameObject.Find("VFX Test Controls").GetComponent("BattleVfxTestController");
            Component battle = (Component)lab.GetType().GetProperty("Battle").GetValue(lab);
            battle.GetType().GetField("_actionSeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(battle, .3f);
            object enhancement = battle.GetType().GetField("_basicSkillEnhancement", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(battle);
            string[] colors = { "Red", "Purple", "Blue" };
            for (int stage = 1; stage <= 3; stage++)
            {
                Assert.That((int)battle.GetType().GetProperty("TestSkillStage").GetValue(battle), Is.EqualTo(stage));
                for (int skill = 0; skill < 3; skill++)
                {
                    lab.GetType().GetMethod("RestartBattle").Invoke(lab, null);
                    Assert.That((bool)battle.GetType().GetProperty("IsInBattle").GetValue(battle), Is.True);
                    float deadline = Time.time + 5f;
                    while ((!(bool)battle.GetType().GetProperty("CanChooseAction").GetValue(battle)
                        || !(bool)battle.GetType().GetProperty("HasSelectedBattleTarget").GetValue(battle)) && Time.time < deadline)
                    {
                        yield return null;
                    }
                    Assert.That((bool)battle.GetType().GetProperty("CanChooseAction").GetValue(battle), Is.True);
                    Assert.That((bool)battle.GetType().GetProperty("HasSelectedBattleTarget").GetValue(battle), Is.True);
                    var skills = (IReadOnlyList<SkillData>)battle.GetType().GetProperty("Skills").GetValue(battle);
                    var effect = (GameObject)enhancement.GetType().GetMethod("GetEffect").Invoke(enhancement, new object[] { skills[skill].Id, stage });
                    battle.GetType().GetMethod("UseSkill").Invoke(battle, new object[] { skill });
                    bool sawCharge = false;
                    bool sawEffect = effect == null;
                    deadline = Time.time + 2f;
                    while (Time.time < deadline && (!sawCharge || !sawEffect))
                    {
                        string[] names = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                            .Select(particle => particle.transform.root.name).ToArray();
                        sawCharge |= Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                            .Any(item => item.name == "FuchsiaCharge" + colors[skill] + "(Clone)");
                        sawEffect |= effect != null && names.Any(name => name == effect.name + "(Clone)");
                        yield return null;
                    }
                    Assert.That(sawCharge, Is.True, "차징 누락: 단계 " + stage + " 스킬 " + skill);
                    Assert.That(sawEffect, Is.True, "타격 이펙트 누락: 단계 " + stage + " 스킬 " + skill);
                }
                if (stage < 3)
                {
                    lab.GetType().GetMethod("NextStage").Invoke(lab, null);
                }
            }
            lab.GetType().GetMethod("NextStage").Invoke(lab, null);
            Assert.That((int)battle.GetType().GetProperty("TestSkillStage").GetValue(battle), Is.EqualTo(1));
            battle.GetType().GetMethod("CancelBattle").Invoke(battle, null);
        }
    }
}
