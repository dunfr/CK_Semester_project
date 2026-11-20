using CK.SemesterProject.Effects;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.Prototype
{
    // 전투 진입 전환 연출 확인용. 1 = 겹 띠 페이드, 2 = 깨짐·갈라짐. 키를 누를 때만 재생한다.
    // 화면이 가려진 순간 카메라를 돌려(또는 지정한 오브젝트를 바꿔) 새 화면으로 넘어간 것처럼 보이게 한다.
    public sealed class BattleTransitionTester : MonoBehaviour
    {
        [SerializeField] private BattleTransitionPlayer _player;
        [Tooltip("가려진 순간 켜고 끄기를 뒤바꿀 오브젝트. 비어 있으면 카메라를 회전한다.")]
        [SerializeField] private GameObject[] _swapTargets;
        [Tooltip("_swapTargets가 비었을 때 가려진 순간 카메라를 돌리는 각도(도). 0이면 화면을 바꾸지 않는다.")]
        [SerializeField] private float _fallbackYaw = 120f;
        [Tooltip("화면 왼쪽 위에 조작 안내를 표시")]
        [SerializeField] private bool _showHint = true;

        private Camera _camera;

        private void Awake()
        {
            if (_player == null)
            {
                _player = GetComponent<BattleTransitionPlayer>();
            }
            _camera = Camera.main;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _player == null || _player.IsPlaying)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                _player.Play(BattleTransitionStyle.LayeredStripe, SwapView);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                _player.Play(BattleTransitionStyle.ShatterSplit, SwapView);
            }
        }

        private void OnGUI()
        {
            if (!_showHint)
            {
                return;
            }
            GUI.Label(new Rect(16f, 16f, 480f, 24f), "[1] 겹 띠 페이드   [2] 깨짐·갈라짐");
        }

        private void SwapView()
        {
            if (_swapTargets != null && _swapTargets.Length > 0)
            {
                foreach (GameObject target in _swapTargets)
                {
                    if (target != null)
                    {
                        target.SetActive(!target.activeSelf);
                    }
                }
                return;
            }

            if (_camera != null && !Mathf.Approximately(_fallbackYaw, 0f))
            {
                _camera.transform.Rotate(0f, _fallbackYaw, 0f, Space.World);
            }
        }
    }
}
