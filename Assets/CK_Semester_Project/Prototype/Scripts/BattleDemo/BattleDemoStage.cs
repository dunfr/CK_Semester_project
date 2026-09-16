using UnityEngine;

namespace CK.SemesterProject.Battle.Demo
{
    [RequireComponent(typeof(BattleDemoController))]
    public sealed class BattleDemoStage : MonoBehaviour
    {
        [SerializeField, Tooltip("플레이어, 센티널 A, 센티널 B 순서의 씬 모델")]
        private Transform[] _figures;
        private readonly string[] _ids = { "player", "sentinel_a", "sentinel_b" };
        private Vector3[] _positions;
        private Quaternion[] _rotations;
        private BattleDemoController _controller;

        private void Awake()
        {
            _controller = GetComponent<BattleDemoController>();
            if (_figures == null || _figures.Length != _ids.Length)
            {
                Debug.LogError("BattleDemoStage: 전투 모델 3개를 연결해야 합니다.", this);
                enabled = false;
                return;
            }
            _positions = new Vector3[_figures.Length];
            _rotations = new Quaternion[_figures.Length];
            for (int i = 0; i < _figures.Length; i++)
            {
                _positions[i] = _figures[i].localPosition;
                _rotations[i] = _figures[i].localRotation;
            }
        }

        private void LateUpdate()
        {
            BattleActionResult result = _controller.PendingResult;
            for (int i = 0; i < _figures.Length; i++)
            {
                CombatantState state = _controller.GetDisplayedState(_ids[i]);
                float pulse = 0;
                if (result != null && result.Request.ActorId == _ids[i] && !result.WasSkipped
                    && result.Request.Kind == BattleActionKind.Skill)
                {
                    pulse = Mathf.Sin(Mathf.Clamp01(_controller.PresentationProgress / 0.55f) * Mathf.PI);
                }
                Vector3 direction = i == 0 ? (_positions[1] - _positions[0]).normalized : (_positions[0] - _positions[i]).normalized;
                _figures[i].localPosition = _positions[i] + direction * pulse * 0.75f;
                _figures[i].localRotation = _rotations[i] * (state.IsDead ? Quaternion.Euler(0, 0, 83) : Quaternion.identity);
                _figures[i].localScale = state.IsDead ? Vector3.one * 0.65f : Vector3.one;
            }
        }
    }
}
