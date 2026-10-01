using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    public sealed class TutorialLevelTrigger : MonoBehaviour
    {
        [SerializeField] private TutorialLevelFlow _flow;
        [SerializeField] private TutorialLevelStage _destination;
        [SerializeField] private bool _collapse;

        private void OnTriggerStay(Collider other)
        {
            if (other.GetComponent<PlayerMovement>() == null)
            {
                return;
            }
            if (_collapse)
            {
                _flow.CollapseDuct();
            }
            else
            {
                _flow.EnterZone(_destination);
            }
        }
    }
}
