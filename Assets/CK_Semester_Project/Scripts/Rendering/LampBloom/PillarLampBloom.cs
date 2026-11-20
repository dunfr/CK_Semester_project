using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CK.SemesterProject.Rendering
{
    // 기둥 조명 전용 Bloom. 항목 설명은 LampBloomSettings에 있다.
    [Serializable, VolumeComponentMenu("Semester/Lamp Bloom/Pillar Lamp Bloom")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class PillarLampBloom : LampBloomSettings
    {
    }
}
