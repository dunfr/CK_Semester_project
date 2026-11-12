using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CK.SemesterProject.UIAnimation.EditorTools
{
    // 여러 Panel을 함께 미리 볼 범위를 정한다.
    // 기준 오브젝트가 켜질 때 함께 켜지는 애니메이션을 Hierarchy 순서대로 모은다.
    public static class UIAnimationPreviewScope
    {
        // 대상의 부모를 넓은 쪽부터 돌려준다. 가장 바깥 Canvas에서 멈춘다.
        public static List<Transform> Roots(Transform target)
        {
            var roots = new List<Transform>();
            if (target == null)
            {
                return roots;
            }

            Transform top = null;
            for (Transform node = target; node != null; node = node.parent)
            {
                if (node.GetComponent<Canvas>() != null)
                {
                    top = node;
                }
            }
            for (Transform node = target.parent; node != null; node = node.parent)
            {
                roots.Insert(0, node);
                if (node == top)
                {
                    break;
                }
            }
            return roots;
        }

        // 따로 고르지 않았을 때의 기준이다. Canvas 바로 아래 패널이고, 없으면 Canvas다.
        public static Transform DefaultRoot(Transform target)
        {
            List<Transform> roots = Roots(target);
            if (roots.Count == 0)
            {
                return null;
            }
            return roots.Count > 1 && roots[0].GetComponent<Canvas>() != null ? roots[1] : roots[0];
        }

        public static List<UIAnimationPlayer> Players(Transform root)
        {
            if (root == null)
            {
                return new List<UIAnimationPlayer>();
            }
            return root.GetComponentsInChildren<UIAnimationPlayer>(true).Where(player => player.Preset != null).ToList();
        }

        // 기준과 대상 사이에 꺼진 오브젝트가 있으면 게임에서도 따로 켜야 나온다.
        public static bool OpensWithRoot(UIAnimationPlayer player, Transform root)
        {
            for (Transform node = player.transform; node != null && node != root; node = node.parent)
            {
                if (!node.gameObject.activeSelf)
                {
                    return false;
                }
            }
            return true;
        }

        public static int Depth(Transform node, Transform root)
        {
            int depth = 0;
            for (Transform parent = node.parent; parent != null && parent != root; parent = parent.parent)
            {
                depth++;
            }
            return depth;
        }

        // Popup은 '/'를 하위 메뉴로 나누므로 이름 사이는 다른 기호로 잇는다.
        public static string Label(List<Transform> roots, int index)
        {
            bool canvasFirst = roots[0].GetComponent<Canvas>() != null;
            if (index == 0 && canvasFirst)
            {
                return $"Canvas 전체 ({Clean(roots[0].name)})";
            }
            int first = canvasFirst ? 1 : 0;
            return string.Join(" › ", roots.Skip(first).Take(index - first + 1).Select(root => Clean(root.name)));
        }

        private static string Clean(string name)
        {
            return name.Replace('/', '∕');
        }
    }
}
