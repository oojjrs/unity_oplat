# UnityOplat

Unity 프로젝트의 플랫폼 인증 초기화를 한 진입점으로 묶는 `com.oojjrs.oplat` 패키지다.

하나의 패키지와 `oojjrs.oplat` 어셈블리에서 `Anonymous`와 `Steam` 초기화를 제공한다. 다른 `MyPlatformTypeEnum` 값을 선택하면 아직 `NotImplementedException`이 발생한다.

## 설치

Steamworks.NET은 OpenUPM에서 전이 설치된다. 소비 프로젝트의 `Packages/manifest.json`에 OpenUPM scoped registry와 Oplat 패키지만 선언한다.

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.rlabrecque.steamworks.net"
      ]
    }
  ],
  "dependencies": {
    "com.oojjrs.oplat": "https://github.com/oojjrs/unity_oplat.git?path=/Packages/src"
  }
}
```

기존 `scopedRegistries`나 `dependencies`가 있다면 위 항목을 해당 배열과 객체에 합친다. Oplat은 Steamworks.NET `2025.164.1`에 의존한다.

## 사용

1. GameObject에 `MyPlatformInitializer`를 추가한다.
2. 같은 GameObject의 컴포넌트에서 `MyPlatformInitializer.CallbackInterface`를 구현한다.
3. `InitialType`으로 `Anonymous` 또는 `Steam`을 반환하고, 초기화 이후 작업은 `OnOk(MyPlatformServiceInterface service)`에서 시작한다.

`service.Account`와 `service.Nickname`으로 계정 식별자와 표시 이름을 가져온다. `Anonymous`는 기기 고유 식별자와 기기 이름을 사용하며, 플랫폼에서 지원하지 않는 값은 기기 이름이나 제품 이름으로 대체한다. Steam은 SteamID와 현재 persona name을 사용한다.

`Account`는 클라이언트에서 조회한 식별 문자열이며 서버 인증 증명이 아니다.

플랫폼별 구현은 패키지 내부에서 자동 등록되므로 소비자가 `AnonymousPlatform`이나 `SteamPlatform` 같은 구체 타입을 직접 참조하지 않는다.

플랫폼 구현은 별도 `DontDestroyOnLoad` GameObject의 `MonoBehaviour`로 생성된다. 따라서 `MyPlatformInitializer`는 `OnOk(MyPlatformServiceInterface service)` 이후 삭제해도 된다.

`Steam`은 Steamworks.NET의 `SteamAPI.InitEx`로 초기화한다. 이후 `SteamPlatform`이 매 프레임 콜백을 처리하고, 플랫폼 오브젝트가 파괴될 때 Steam API를 종료한다. Play Mode 중 스크립트를 다시 컴파일했다면 Play Mode를 재시작한다.

Steam 클라이언트가 실행 중이어야 하며 App ID는 Steam 실행 컨텍스트 또는 개발용 `steam_appid.txt`로 제공한다. 개발용 파일은 배포 빌드에 포함하지 않는다.

Steam 구현은 Windows, macOS, Linux Standalone 대상에서만 컴파일된다. 다른 대상에서는 `Anonymous`를 사용할 수 있다.

## 구조

- `Packages/src`: 코어 런타임과 Anonymous 및 Steam 구현
