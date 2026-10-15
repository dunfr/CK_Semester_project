using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    // 기존 스킬 버튼/아이콘 사이를 잇는 장식만 메시로 그린다.
    public sealed class BattleSkillArcGraphic : UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertices)
        {
            vertices.Clear();
            DrawArc(vertices, 238f, 26f, new Color(.03f, .12f, .22f, .8f));
            DrawArc(vertices, 223f, 2f, new Color(.2f, .65f, .95f, .65f));
            DrawArc(vertices, 254f, 3f, new Color(.35f, .8f, 1f, .9f));
        }

        private static void DrawArc(UnityEngine.UI.VertexHelper vertices, float radius, float width, Color tint)
        {
            const int segments = 48;
            Vector2 center = new Vector2(-235f, 0f);
            for (int index = 0; index < segments; index++)
            {
                float angleA = Mathf.Lerp(-68f, 68f, index / (float)segments) * Mathf.Deg2Rad;
                float angleB = Mathf.Lerp(-68f, 68f, (index + 1) / (float)segments) * Mathf.Deg2Rad;
                Vector2 a = new Vector2(Mathf.Cos(angleA), Mathf.Sin(angleA));
                Vector2 b = new Vector2(Mathf.Cos(angleB), Mathf.Sin(angleB));
                int start = vertices.currentVertCount;
                vertices.AddVert(center + a * (radius - width * .5f), tint, Vector2.zero);
                vertices.AddVert(center + a * (radius + width * .5f), tint, Vector2.zero);
                vertices.AddVert(center + b * (radius + width * .5f), tint, Vector2.zero);
                vertices.AddVert(center + b * (radius - width * .5f), tint, Vector2.zero);
                vertices.AddTriangle(start, start + 1, start + 2);
                vertices.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
