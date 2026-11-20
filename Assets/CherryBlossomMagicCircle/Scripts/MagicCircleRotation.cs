using UnityEngine;

public class MagicCircleRotation : MonoBehaviour
{
    [Header("Ring Pivots")]
    [SerializeField] private Transform outerRing;
    [SerializeField] private Transform midRing;
    [SerializeField] private Transform centerRing;

    [Header("Rotation Speed")]
    [SerializeField] private float outerSpeed = -7f;
    [SerializeField] private float midSpeed = 11f;
    [SerializeField] private float centerSpeed = 1.5f;

    private void Update()
    {
        if (outerRing != null)
            outerRing.Rotate(Vector3.up, outerSpeed * Time.deltaTime, Space.Self);

        if (midRing != null)
            midRing.Rotate(Vector3.up, midSpeed * Time.deltaTime, Space.Self);

        if (centerRing != null)
            centerRing.Rotate(Vector3.up, centerSpeed * Time.deltaTime, Space.Self);
    }
}