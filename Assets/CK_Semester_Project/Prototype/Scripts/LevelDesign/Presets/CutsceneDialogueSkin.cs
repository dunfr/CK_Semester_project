using TMPro;
using UnityEngine;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 컷씬 대사창 스킨(스프라이트·글꼴). Resources/CutsceneDialogueSkin 에 두면 대사창이 자동으로 불러온다.
    /// </summary>
    [CreateAssetMenu(fileName = "CutsceneDialogueSkin", menuName = "CK/Level Design/Cutscene Dialogue Skin")]
    public sealed class CutsceneDialogueSkin : ScriptableObject
    {
        public const string ResourcePath = "CutsceneDialogueSkin";

        [Header("대사창")]
        [Tooltip("탭 두 개(분류/화자)가 붙은 하단 대사창 프레임")]
        public Sprite Frame;
        [Tooltip("분류 탭 아이콘")]
        public Sprite CategoryIcon;
        [Tooltip("입력 대기 표시 화살표")]
        public Sprite NextArrow;
        [Tooltip("대사 기록(LOG) 창 프레임. 9-slice 스프라이트")]
        public Sprite LogFrame;

        [Header("버튼 (기본 / 마우스 올림·켜짐)")]
        public Sprite AutoNormal;
        public Sprite AutoHover;
        public Sprite SkipNormal;
        public Sprite SkipHover;
        public Sprite LogNormal;
        public Sprite LogHover;

        [Header("글꼴")]
        public TMP_FontAsset RegularFont;
        public TMP_FontAsset BoldFont;
        public TMP_FontAsset ExtraBoldFont;

        private static CutsceneDialogueSkin _cached;

        public static CutsceneDialogueSkin Load()
        {
            if (_cached == null)
            {
                _cached = Resources.Load<CutsceneDialogueSkin>(ResourcePath);
            }
            return _cached;
        }
    }
}
