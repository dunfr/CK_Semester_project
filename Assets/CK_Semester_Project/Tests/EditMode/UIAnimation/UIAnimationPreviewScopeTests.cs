using System.Collections.Generic;
using System.Linq;
using CK.SemesterProject.UIAnimation.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation.Tests
{
    public sealed class UIAnimationPreviewScopeTests
    {
        private const string Folder = "Assets/__UIAnimationPreviewScopeTests";

        private GameObject _canvas;
        private Transform _battle;
        private Transform _turn;
        private Transform _description;
        private UIAnimationPlayer _hud;
        private UIAnimationPlayer _skill;
        private UIAnimationPlayer _detail;

        // Canvas
        //   BattlePanel
        //     HUD (애니메이션)
        //     PlayerTurnPanel
        //       Skill (애니메이션)
        //       Description (꺼짐)
        //         Detail (애니메이션)
        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _battle = Node("BattlePanel", _canvas.transform);
            _hud = Animated("HUD", _battle);
            _turn = Node("PlayerTurnPanel", _battle);
            _skill = Animated("Skill", _turn);
            _description = Node("Description", _turn);
            _detail = Animated("Detail", _description);
            _description.gameObject.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void Roots_GoFromCanvasDownToTheParent()
        {
            List<Transform> roots = UIAnimationPreviewScope.Roots(_skill.transform);

            Assert.That(roots, Is.EqualTo(new[] { _canvas.transform, _battle, _turn }));
            Assert.That(UIAnimationPreviewScope.Label(roots, 0), Is.EqualTo("Canvas 전체 (Canvas)"));
            Assert.That(UIAnimationPreviewScope.Label(roots, 2), Is.EqualTo("BattlePanel › PlayerTurnPanel"));
        }

        [Test]
        public void DefaultRoot_IsThePanelRightUnderTheCanvas()
        {
            Assert.That(UIAnimationPreviewScope.DefaultRoot(_skill.transform), Is.EqualTo(_battle));
            Assert.That(UIAnimationPreviewScope.DefaultRoot(_hud.transform), Is.EqualTo(_battle));
        }

        [Test]
        public void Players_ListEveryAnimationUnderTheRootInHierarchyOrder()
        {
            List<UIAnimationPlayer> players = UIAnimationPreviewScope.Players(_battle);

            Assert.That(players, Is.EqualTo(new[] { _hud, _skill, _detail }));
            Assert.That(UIAnimationPreviewScope.Depth(_detail.transform, _battle), Is.EqualTo(2));
        }

        [Test]
        public void OpensWithRoot_IsFalseUnderAnInactiveObject()
        {
            _battle.gameObject.SetActive(false);

            Assert.That(UIAnimationPreviewScope.OpensWithRoot(_hud, _battle), Is.True, "기준 자신이 꺼져 있는 것은 상관없다");
            Assert.That(UIAnimationPreviewScope.OpensWithRoot(_skill, _battle), Is.True);
            Assert.That(UIAnimationPreviewScope.OpensWithRoot(_detail, _battle), Is.False);
            Assert.That(UIAnimationPreviewScope.Players(_battle).Where(player => UIAnimationPreviewScope.OpensWithRoot(player, _battle)),
                Is.EqualTo(new[] { _hud, _skill }));
        }

        private static Transform Node(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            return node.transform;
        }

        private static UIAnimationPlayer Animated(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.transform.SetParent(parent, false);
            UIAnimationGenerator.Create(node.GetComponent<Image>(), Folder, name, UIPointerEffectMask.None);
            return node.GetComponent<UIAnimationPlayer>();
        }
    }
}
