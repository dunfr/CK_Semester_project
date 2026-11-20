using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CK.SemesterProject.Rendering
{
    // 배경(동적 객체가 아닌 모든 것) 전용 Bloom. 항목 설명은 SceneBloomSettings에 있다.
    [Serializable, VolumeComponentMenu("Semester/Scene Bloom/Background Bloom")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class BackgroundBloom : SceneBloomSettings
    {
    }
}
