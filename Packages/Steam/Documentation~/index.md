# OOJJRS' Unity Steam Platform

`com.oojjrs.oplat`의 Steamworks.NET 초기화 어댑터다.

## 설치

소비 프로젝트의 `Packages/manifest.json`에 `com.oojjrs.oplat`, `com.oojjrs.oplat.steam`, `com.rlabrecque.steamworks.net`을 모두 선언한다. Steamworks.NET은 이 패키지의 의존성 버전과 호환되는 Git revision을 직접 지정한다. Standalone 빌드 프로필에는 `STEAMWORKS_NET` scripting define을 추가한다.

## 사용

`MyPlatformInitializer.CallbackInterface.InitialType`으로 `MyPlatformTypeEnum.Steam`을 반환한다. 초기화가 끝난 뒤 `MyPlatformInitializer`는 삭제해도 된다. 별도 `DontDestroyOnLoad` GameObject에 생성된 `SteamPlatform`이 Steam 수명 주기를 계속 소유한다.

`OnOk(MyPlatformServiceInterface service)`로 전달된 서비스의 `Account`는 현재 사용자의 SteamID 문자열이고, `Nickname`은 현재 Steam persona name이다.

SteamID 문자열 자체는 인증 티켓이 아니므로 서버 인증 증명으로 사용하지 않는다.

초기화는 `SteamAPI.InitEx`를 사용한다. 초기화 후 `SteamPlatform`이 매 프레임 콜백을 처리하고, 플랫폼 오브젝트가 파괴될 때 Steam API를 종료한다. 한 프로세스에는 활성 `SteamPlatform`을 하나만 둔다. Play Mode 중 스크립트를 다시 컴파일했다면 Play Mode를 재시작한다.

Steam 클라이언트가 실행 중이어야 하며 App ID는 Steam 실행 컨텍스트 또는 개발용 `steam_appid.txt`로 제공한다. 개발용 파일은 배포 빌드에 포함하지 않는다.
