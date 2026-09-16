using UnityEngine;

namespace Semester.Enemies
{
    [AddComponentMenu("Enemy AI/Noise Emitter")]
    public class EnemyNoiseEmitter : MonoBehaviour
    {
        [Header("소리 감지 이벤트")]
        [Min(0f), Tooltip("이 소리가 도달하는 최대 거리(m). 적의 청각 범위도 적용됩니다.")]
        public float noiseRadius = 12f;
        [Tooltip("선택: 감지 이벤트와 함께 재생할 실제 오디오")]
        public AudioSource audioSource;

        // Animation Event / UnityEvent / gameplay code can call this.
        [ContextMenu("Emit Noise (Play Mode)")]
        public void EmitNoise()
        {
            if (!Application.isPlaying) return;
            EnemyNoise.Emit(transform.position, noiseRadius, gameObject);
            if (audioSource != null) audioSource.Play();
        }
    }
}
