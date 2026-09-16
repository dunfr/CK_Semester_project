using System;
using UnityEngine;

namespace Semester.Enemies
{
    /// <summary>Gameplay hearing events; independent of AudioSource playback and field of view.</summary>
    public static class EnemyNoise
    {
        public static event Action<Vector3, float, GameObject> Emitted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Emitted = null;

        public static void Emit(Vector3 position, float radius, GameObject source = null)
        {
            if (radius > 0f) Emitted?.Invoke(position, radius, source);
        }
    }
}
