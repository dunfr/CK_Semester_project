using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CK.SemesterProject.UI.Battle
{
    /// <summary>
    /// 적 체력바가 표시하는 속성.
    /// 전투 코어의 CK.SemesterProject.Battle.BattleElement 와 이름·값 순서가 같아서
    /// (EnemyHpElement)(int)battleElement 로 그대로 변환할 수 있다.
    /// </summary>
    public enum EnemyHpElement
    {
        None = 0,
        /// <summary>잔상 (각인에 강함)</summary>
        Afterimage = 1,
        /// <summary>각인 (망각에 강함)</summary>
        Imprint = 2,
        /// <summary>망각 (잔상에 강함)</summary>
        Oblivion = 3,
    }

    /// <summary>
    /// 적 머리 위 체력바. 세 속성 아이콘을 모두 갖고 있다가 현재 속성에 맞는 것만 켠다.
    /// </summary>
    [ExecuteAlways]
    public sealed class EnemyHpView : MonoBehaviour
    {
        [FormerlySerializedAs("attribute")]
        [SerializeField] private EnemyHpElement element = EnemyHpElement.Afterimage;
        [SerializeField, Range(0f, 1f)] private float hpRatio = 1f;

        [Header("References")]
        [SerializeField] private Image hpFill;
        [SerializeField] private GameObject afterimageIcon;
        [FormerlySerializedAs("engravingIcon")]
        [SerializeField] private GameObject imprintIcon;
        [SerializeField] private GameObject oblivionIcon;

        public EnemyHpElement Element => element;
        public float HpRatio => hpRatio;

        public void SetElement(EnemyHpElement value)
        {
            element = value;
            ApplyElement();
        }

        /// <summary>BattleElement 값을 (int)로 넘겨 받는 용도.</summary>
        public void SetElement(int battleElementValue)
        {
            SetElement((EnemyHpElement)battleElementValue);
        }

        public void SetHpRatio(float ratio)
        {
            hpRatio = Mathf.Clamp01(ratio);
            ApplyHp();
        }

        public void SetHp(float current, float max)
        {
            SetHpRatio(max > 0f ? current / max : 0f);
        }

        private void OnEnable()
        {
            ApplyElement();
            ApplyHp();
        }

        private void OnValidate()
        {
            ApplyElement();
            ApplyHp();
        }

        private void ApplyElement()
        {
            SetActive(afterimageIcon, element == EnemyHpElement.Afterimage);
            SetActive(imprintIcon, element == EnemyHpElement.Imprint);
            SetActive(oblivionIcon, element == EnemyHpElement.Oblivion);
        }

        private void ApplyHp()
        {
            if (hpFill != null)
            {
                hpFill.fillAmount = hpRatio;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
