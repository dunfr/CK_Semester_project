using CK.SemesterProject.Battle;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    public static class PlayerSessionState
    {
        // 씬 객체가 파괴되어도 같은 플레이 세션에서는 동일한 상태를 사용한다.
        public static PlayerRuntimeState Current { get; private set; } = new PlayerRuntimeState();

        /// <summary>플레이 세션 상태(HP·메모리·승리 횟수)를 처음 상태로 되돌린다. 게임을 처음부터 다시 시작할 때 쓴다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetSession()
        {
            Current = new PlayerRuntimeState();
        }
    }
}
