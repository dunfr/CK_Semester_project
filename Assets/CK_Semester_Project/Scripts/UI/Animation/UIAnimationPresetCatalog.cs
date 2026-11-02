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
            "먼저 밝아지고 작게 튀어 정착합니다.", "짧게 드러나고 차분히 사라집니다.",
            "힘을 모아 커진 뒤 부드럽게 정착합니다.", "가로를 먼저 펼치고 높이를 정돈합니다.",
            "세로를 먼저 펼치고 너비를 정돈합니다.", "물속 판처럼 비스듬히 떠올라 정착합니다.",
            "사선으로 진입한 카드가 마지막에 탄력을 줍니다.", "정보가 먼저 드러나고 패널이 정확히 멈춥니다.",
            "빛의 경계가 지나간 뒤 배너가 정렬됩니다.", "가느다란 선에서 가로, 세로 순으로 켜집니다.",
            "눌린 카드가 계단식으로 펴지며 정착합니다.", "날카로운 절단면 뒤로 카드가 빠르게 들어옵니다.",
            "기울어진 목록이 옆으로 쓸려 들어와 정렬됩니다.", "얇은 빛이 펼쳐진 뒤 패널이 또렷해집니다.",
            "작은 창이 조용히 떠올라 부드럽게 멈춥니다.", "짧은 점멸 뒤 위치와 크기가 단단히 고정됩니다.",
            "도장이 찍히듯 내려앉고 살짝 되튑니다.", "고전 창이 단계적으로 펼쳐져 읽는 자리를 만듭니다.",
            "가는 종이가 가로로 열린 뒤 세로로 펼쳐집니다.", "카드가 원호를 따라 떠오르며 천천히 돌아옵니다.",
            "배너가 옆에서 강하게 들어와 충격을 가라앉힙니다.", "본문을 먼저 보여 주고 창이 조용히 올라옵니다.",
            "보상이 떠오른 뒤 작게 반짝이듯 정착합니다."
        };

        private static readonly string[] StayNames = { "가만히 있기", "둥실 떠 있기 (페르소나풍)", "은은하게 깜빡이기" };
        private static readonly string[] PointerNames = { "살짝 커지기", "옆으로 밀리기 (목록 선택)", "기울며 튀어나오기", "밝아지기 (하이라이트)" };
    }
}
