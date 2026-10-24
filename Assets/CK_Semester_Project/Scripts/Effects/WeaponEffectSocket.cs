using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("CK/칼날 이펙트 소켓")]
public sealed class WeaponEffectSocket : MonoBehaviour
{
    public const string SocketName = "BladeSocket";

    [SerializeField, Tooltip("검을 움직이는 무기 본 아래의 BladeSocket 기준점입니다.")]
    private Transform _bladeSocket;

    public Transform BladeSocket => _bladeSocket;

    public static Transform Find(Transform actor)
    {
        if (actor == null)
        {
            return null;
        }
        foreach (WeaponEffectSocket binding in actor.GetComponentsInChildren<WeaponEffectSocket>(true))
        {
            if (binding._bladeSocket != null && binding._bladeSocket.IsChildOf(actor))
            {
                return binding._bladeSocket;
            }
        }
        foreach (Transform child in actor.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == SocketName)
            {
                return child;
            }
        }
        return null;
    }
}
