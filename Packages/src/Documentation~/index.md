# OOJJRS' Unity Platform

Unity 프로젝트의 플랫폼 인증 초기화를 한 진입점으로 묶는 패키지다.

## 설치

소비 프로젝트의 `Packages/manifest.json`에 Oplat과 Steamworks.NET Git 패키지를 함께 선언한다.

```json
{
  "dependencies": {
    "com.oojjrs.oplat": "https://github.com/oojjrs/unity_oplat.git?path=/Packages/src",
    "com.rlabrecque.steamworks.net": "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#ba71581f1ed7349e8d0f17ddc6f135dd3bc8a6a3"
  }
}
```

Unity 패키지 manifest는 Git 의존성 위치를 전파하지 못하므로 두 항목을 소비 프로젝트에 직접 선언한다. Steamworks.NET은 호환성이 확인된 revision으로 고정한다.

## 사용법

1. GameObject에 `MyPlatformInitializer`를 추가한다.
2. 같은 GameObject의 컴포넌트에서 `MyPlatformInitializer.CallbackInterface`를 구현한다.
3. `InitialType`으로 `MyPlatformTypeEnum.Anonymous` 또는 `MyPlatformTypeEnum.Steam`을 반환한다.
4. 플랫폼 초기화 이후 작업을 `OnOk(MyPlatformServiceInterface service)`에서 수행한다.

`service.Account`, `service.Nickname`, `service.ProfileImage`로 초기화된 플랫폼의 계정 식별자, 표시 이름, 프로필 이미지를 가져온다. `Anonymous`는 기기 고유 식별자와 기기 이름, 패키지 기본 프로필 이미지를 사용한다. 기기 식별자를 지원하지 않으면 계정도 기기 이름을 사용하고, 기기 이름도 지원하지 않으면 제품 이름으로 대체한다. Steam은 SteamID, 현재 persona name, 현재 사용자의 중간 크기 프로필 이미지를 사용한다. 프로필 이미지를 가져오지 못하면 `ProfileImage`는 `null`이다.

`ProfileImage`가 반환하는 `Sprite`의 수명은 플랫폼 서비스가 관리하므로 소비자가 직접 파괴하지 않는다.

플랫폼 서비스를 장기간 보관하는 경우 `service.IsAlive`를 확인한 뒤 접근한다.

`Account`는 클라이언트에서 조회한 식별 문자열이며 서버 인증 증명이 아니다.

패키지는 `Anonymous`와 `Steam`을 구현한다. 다른 `MyPlatformTypeEnum` 값은 아직 `NotImplementedException`을 발생시킨다.

플랫폼 구현은 `oojjrs.oplat` 어셈블리에서 자동 등록된다. 소비자는 `AnonymousPlatform`이나 `SteamPlatform` 같은 내부 구체 타입을 알거나 직접 등록할 필요가 없다.

플랫폼 구현은 별도 `DontDestroyOnLoad` GameObject의 `MonoBehaviour`로 생성된다. `MyPlatformInitializer`는 `OnOk(MyPlatformServiceInterface service)` 이후 삭제해도 플랫폼 수명 주기에 영향을 주지 않는다.

Steam 구현은 소비 프로젝트에 `STEAMWORKS_NET`이 정의된 경우에만 컴파일된다. Steamworks.NET은 기본 설정에서 Standalone 대상에 이 심볼을 자동으로 추가한다. Steam 클라이언트가 실행 중이어야 하며 App ID는 Steam 실행 컨텍스트 또는 개발용 `steam_appid.txt`로 제공한다. 개발용 파일은 배포 빌드에 포함하지 않는다.

초기화는 `SteamAPI.InitEx`를 사용한다. 초기화 후 `SteamPlatform`이 매 프레임 콜백을 처리하고, 플랫폼 오브젝트가 파괴될 때 Steam API를 종료한다. Play Mode 중 스크립트를 다시 컴파일했다면 Play Mode를 재시작한다.
