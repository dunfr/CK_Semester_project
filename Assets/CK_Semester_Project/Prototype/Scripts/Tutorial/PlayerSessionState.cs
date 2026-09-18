using CK.SemesterProject.Battle;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    public static class PlayerSessionState
    {
        // 씬 객체가 파괴되어도 같은 플레이 세션에서는 동일한 상태를 사용한다.
        public static PlayerRuntimeState Current { get; private set; } = new PlayerRuntimeState();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            Current = new PlayerRuntimeState();
        }
    }
}
