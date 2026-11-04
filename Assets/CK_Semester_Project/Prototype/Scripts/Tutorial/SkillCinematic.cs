using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

namespace CK.SemesterProject.Tutorial
{
    // 이펙터가 만든 타임라인 연출(시네머신 카메라·카메라 스택·볼륨 포함)을 전투 중에 그대로 재생한다.
    // 평소에는 꺼 두고, 재생하는 동안만 켜서 연출 카메라로 화면을 넘긴 뒤 끝나면 전투 카메라로 돌아온다.
    public sealed class SkillCinematic : MonoBehaviour
    {
        [SerializeField, Tooltip("연출 전체를 재생하는 타임라인 Director")]
        private PlayableDirector _director;
        [SerializeField, Tooltip("연출이 쓰는 카메라(시네머신 Brain이 붙은 카메라)")]
        private Camera _cinematicCamera;
        [SerializeField, Min(0f), Tooltip("스킬 사용 후 연출을 시작하기까지의 시간(초)")]
        private float _startDelay;
        [SerializeField, Min(0f), Tooltip("타임라인이 끝난 뒤 연출 화면을 더 유지하는 시간(초)")]
        private float _holdSeconds = 0.2f;

        private Coroutine _playing;
        private Camera _battleCamera;

        public float Duration => _startDelay + (_director != null ? (float)_director.duration : 0f) + _holdSeconds;

        // 전투 카메라 대신 연출 카메라로 타임라인을 처음부터 재생하고, 전체 길이(초)를 돌려준다.
        public float Play(Camera battleCamera)
        {
            if (_director == null)
            {
                return 0f;
            }
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            if (_playing != null)
            {
                StopCoroutine(_playing);
                Finish();
            }
            _battleCamera = battleCamera;
            _playing = StartCoroutine(Run());
            return Duration;
        }

        private IEnumerator Run()
        {
            SetCinematicVisible(false);
            if (_startDelay > 0f)
            {
                yield return new WaitForSeconds(_startDelay);
            }
            SetCinematicVisible(true);
            _director.time = 0;
            _director.Evaluate();
            _director.Play();
            yield return new WaitForSeconds((float)_director.duration + _holdSeconds);
            Finish();
            gameObject.SetActive(false);
        }

        private void SetCinematicVisible(bool visible)
        {
            if (_battleCamera != null)
            {
                _battleCamera.enabled = !visible;
            }
            if (_cinematicCamera != null)
            {
                _cinematicCamera.gameObject.SetActive(visible);
            }
        }

        private void Finish()
        {
            if (_director != null)
            {
                _director.Stop();
            }
            SetCinematicVisible(false);
            if (_battleCamera != null)
            {
                _battleCamera.enabled = true;
            }
            _playing = null;
        }

        private void OnDisable()
        {
            if (_playing != null)
            {
                StopCoroutine(_playing);
                Finish();
            }
        }
    }
}
