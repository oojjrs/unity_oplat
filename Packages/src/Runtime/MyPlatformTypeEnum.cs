namespace oojjrs.oplat
{
    public enum MyPlatformTypeEnum
    {
        // 에디터에서 개발 모드로 사용할 때 쓰는 거. PC 기반 Standalone과는 다르다.
        Anonymous,
        Apple,
        AppleGameCenter,
        // ProcessAuthenticationTokens, 진짜 커스텀이 아니라 UGS가 제공하는 커스텀 기능임.
        Custom,
        Epic,
        Facebook,
        Google,
        GooglePlayGames,
        OpenId,
        // PC 기반 실행이지만 스팀 같은 별도의 인증이 붙지 않은 모드. UGS에서는 anonymous를 쓸.. 계획.
        Standalone,
        Steam,
        // UGS가 지원 안 해주는 녀석이라서, 내부적으로는 Custom ID를 쓰고 스토브네 verify API까지 호출하라는데 ㅡㅡ 아오 씨
        Stove,
        // 유니티 개발자 계정이 아니고 구글/애플 계정 같은 유저용 유니티 계정이다.
        Unity,
        // 니가 제공하는 유저와 패스워드 시스템. 이게 진짜 제3자 커스터마이징이다.
        Yours,
    }
}
