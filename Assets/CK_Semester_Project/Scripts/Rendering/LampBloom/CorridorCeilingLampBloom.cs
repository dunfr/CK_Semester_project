using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CK.SemesterProject.Rendering
{
    // 복도 천장 조명 전용 Bloom. 항목 설명은 LampBloomSettings에 있다.
    [Serializable, VolumeComponentMenu("Semester/Lamp Bloom/Corridor Ceiling Lamp Bloom")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class CorridorCeilingLampBloom : LampBloomSettings
    {
    }
}
