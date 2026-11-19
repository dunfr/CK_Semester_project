namespace CK.SemesterProject.Rendering
{
    // 화면 안티앨리어싱 방식. 숫자는 저장 값으로 쓰이므로 바꾸지 않는다.
    public enum AntiAliasingMode
    {
        // 끄기.
        Off = 0,
        // SMAA(기본값). 한 프레임만 보고 계단 현상을 다듬는다. 어느 기기에서나 동작한다.
        Smaa = 1,
        // AMD FSR 2. 여러 프레임을 모아 다듬는다. 지원하지 않는 기기에서는 SMAA로 대신한다.
        Fsr2 = 2,
    }
}
