using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CK.SemesterProject.Rendering
{
    // 동적 객체(캐릭터·몬스터) 전용 Bloom. 항목 설명은 SceneBloomSettings에 있다.
    [Serializable, VolumeComponentMenu("Semester/Scene Bloom/Dynamic Object Bloom")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class DynamicObjectBloom : SceneBloomSettings
    {
    }
}
