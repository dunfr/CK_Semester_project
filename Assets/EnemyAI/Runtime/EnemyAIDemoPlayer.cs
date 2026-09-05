using UnityEngine;
using UnityEngine.InputSystem;

namespace Semester.Enemies
{
    [RequireComponent(typeof(CharacterController), typeof(EnemyNoiseEmitter))]
    public class EnemyAIDemoPlayer : MonoBehaviour
    {
        [Header("테스트 씬 조작")]
        [Min(0f)] public float moveSpeed = 5f;
        public bool emitFootsteps = false;
        [Min(0.1f)] public float footstepInterval = 0.5f;
        public bool showInstructions = true;
        private CharacterController controller;
        private EnemyNoiseEmitter emitter;
        private float nextStep;

        private void Awake() { controller = GetComponent<CharacterController>(); emitter = GetComponent<EnemyNoiseEmitter>(); }
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            Vector3 input = new Vector3((keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f), 0f,
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            controller.Move((input.normalized * moveSpeed + Vector3.down * 9.81f) * Time.deltaTime);
            if (keyboard.spaceKey.wasPressedThisFrame) emitter.EmitNoise();
            if (emitFootsteps && input.sqrMagnitude > 0f && Time.time >= nextStep)
            {
                nextStep = Time.time + Mathf.Max(0.1f, footstepInterval);
                emitter.EmitNoise();
            }
        }

        private void OnGUI()
        {
            if (showInstructions) GUI.Box(new Rect(12f, 12f, 470f, 52f), "ENEMY AI TEST\nWASD: Move   |   Space: Emit noise   |   Select Enemy: Tune Inspector");
        }
    }
}
