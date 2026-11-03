namespace CK.SemesterProject.UIAnimation
{
    public static class UIAnimationPresetCatalog
    {
        public static string Name(UIAnimationStyle style) => Names[(int)style];
        public static string Description(UIAnimationStyle style) => Descriptions[(int)style];
        public static string Name(UIStayStyle style) => StayNames[(int)style];
        public static string Name(UIPointerStyle style) => PointerNames[(int)style];

        private static readonly string[] Names =
        {
            "기본/팝업", "기본/페이드", "기본/바운스 팝업", "기본/가로로 펼치기", "기본/세로로 펼치기",
            "페르소나풍/수중 패널", "페르소나풍/사선 카드", "스타레일풍/정보 슬라이드", "스타레일풍/배너 와이프",
            "젠레스풍/TV 켜기", "젠레스풍/스냅 팝", "페르소나풍/슬래시 컷인", "페르소나풍/기울어진 목록",
            "스타레일풍/홀로그램 패널", "스타레일풍/차분한 팝업", "젠레스풍/글리치 컷", "젠레스풍/스티커 스탬프",
            "JRPG풍/고전 명령창", "JRPG풍/양피지 두루마리", "JRPG풍/소환 카드", "JRPG풍/승리 배너",
            "JRPG풍/대화창", "JRPG풍/아이템 획득"
        };

        private static readonly string[] Descriptions =
        {
            "96% 크기에서 작게 힘을 모아 살짝 커졌다가 빠르게 정착합니다.", "알파로 부드럽게 드러나며 크기는 2.5%만 변합니다.",
            "작게 힘을 모아 두 번 튄 뒤 조용히 멈춥니다.", "가로가 먼저 펼쳐지고 높이가 뒤늦게 작게 정착합니다.",
            "너비를 먼저 맞춘 뒤 세로가 길게 펼쳐져 정착합니다.", "부드러운 사선 경계로 드러나며 천천히 부유하고 드물게 빛납니다.",
            "카드가 이동하며 기울기를 준비한 뒤 반대편을 살짝 넘어 정렬합니다.", "오른쪽에서 반동 없이 멈추고 얇은 빛 띠가 한 번 지납니다.",
            "가로의 부드러운 와이프 경계와 빛 띠가 함께 지나갑니다.", "가는 선이 가로·세로로 켜지며 번쩍임과 스캔라인이 잦아듭니다.",
            "12fps 스케일로 딱딱 끊어 펼쳐지고 착지 프레임에 색이 갈라집니다.", "큰 기울기와 사선 컷으로 들어와 짧게 번쩍이고 착지 때 색이 갈라집니다.",
            "목록 항목이 짧게 사선으로 들어온 뒤 각도가 늦게 정렬됩니다.", "짧은 알파 점멸과 약한 색 분리 뒤 희미한 스캔라인으로 대기하고 세로 접힘과 디졸브로 사라집니다.",
            "아주 작은 크기 변화와 알파만으로 조용히 정착합니다.", "강한 띠 어긋남과 색 분리가 계단식으로 잦아들고 디졸브로 나갑니다.",
            "큰 도장이 회전하며 눌렸다 되튀고 착지 순간 번쩍인 뒤 각도가 늦게 정착합니다.", "10fps로 세로를 단계적으로 펼치며 대기 중에는 멈춰 있습니다.",
            "부드러운 세로 와이프로 종이를 펼치고 끝에 높이가 살짝 튕깁니다.", "짧은 번쩍임과 빛 띠 속에서 가로로 뒤집히듯 열리고 디졸브로 사라집니다.",
            "옆에서 강하게 들어와 조금 넘어 멈추며 대기 중 드물게 빛 띠가 지납니다.", "짧게 위로 올라와 빠르게 멈추고 읽는 동안 고정됩니다.",
            "작게 팝업하며 번쩍임과 빛 띠를 보이고 대기 중 드물게 반짝입니다."
        };

        private static readonly string[] StayNames = { "가만히 있기", "둥실 떠 있기 (페르소나풍)", "은은하게 깜빡이기" };
        private static readonly string[] PointerNames = { "살짝 커지기", "옆으로 밀리기 (목록 선택)", "기울며 튀어나오기", "밝아지기 (하이라이트)" };
    }
}
