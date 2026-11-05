using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>대사창 하단 버튼(AUTO/SKIP/LOG). 마우스를 올리거나 켜져 있으면 밝은 스프라이트로 바뀐다.</summary>
    public sealed class CutsceneDialogueButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private static readonly Color LightLabel = new Color(0.9f, 0.92f, 0.97f);
        private static readonly Color DarkLabel = new Color(0.16f, 0.19f, 0.27f);

        private Image _image;
        private TMP_Text _label;
        private Sprite _normal;
        private Sprite _hover;
        private bool _hovered;
        private bool _on;

        public event Action Clicked;

        public void Setup(Image image, TMP_Text label, Sprite normal, Sprite hover)
        {
            _image = image;
            _label = label;
            _normal = normal;
            _hover = hover;
            Refresh();
        }

        public void SetOn(bool on)
        {
            if (_on != on)
            {
                _on = on;
                Refresh();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Refresh();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke();
        }

        private void OnDisable()
        {
            _hovered = false;
            Refresh();
        }

        private void Refresh()
        {
            bool lit = _hovered || _on;
            if (_image != null)
            {
                Sprite sprite = lit ? _hover : _normal;
                if (sprite != null)
                {
                    _image.sprite = sprite;
                }
                else
                {
                    _image.color = lit ? new Color(0.85f, 0.92f, 1f, 0.95f) : new Color(0.2f, 0.12f, 0.3f, 0.9f);
                }
            }
            if (_label != null)
            {
                _label.color = lit ? DarkLabel : LightLabel;
            }
        }
    }
}
