using UnityEngine;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 유리 벽(3-3)을 전용 레이어로 옮긴다. 적 시야(Sight Blocking Layers)에서 이 레이어를 빼면
    /// 유리 너머로 서로 보이지만, 충돌과 전투 진입 판정은 그대로 막는다.
    /// </summary>
    public sealed class GlassLayerSetup : MonoBehaviour
    {
        [SerializeField, Tooltip("적 시야를 통과시킬 유리 오브젝트")]
        private GameObject[] _glassObjects;
        [SerializeField, Tooltip("Tags and Layers에 등록된 유리 레이어 이름")]
        private string _layerName = "LevelGlass";

        private void Awake()
        {
            int layer = LayerMask.NameToLayer(_layerName);
            if (layer < 0)
            {
                Debug.LogError("GlassLayerSetup: '" + _layerName + "' 레이어가 없습니다.", this);
                return;
            }
            foreach (GameObject glass in _glassObjects)
            {
                if (glass != null)
                {
                    glass.layer = layer;
                }
            }
        }
    }
}
