# UnityOplat

Unity 프로젝트의 플랫폼 인증 초기화를 한 진입점으로 묶는 `com.oojjrs.oplat` 패키지다.

코어 패키지는 `Anonymous` 초기화를 제공하고, Steam 어댑터 패키지를 함께 설치하면 `Steam` 초기화를 사용할 수 있다. 다른 `MyPlatformTypeEnum` 값을 선택하면 아직 `NotImplementedException`이 발생한다.

## 설치

Steam 프로젝트는 `Packages/manifest.json`에 코어, Steam 어댑터, Steamworks.NET을 모두 선언하고 Standalone 빌드 프로필에 `STEAMWORKS_NET` scripting define을 추가한다.

```json
{
  "dependencies": {
    "com.oojjrs.oplat": "https://github.com/oojjrs/unity_oplat.git?path=/Packages/src",
    "com.oojjrs.oplat.steam": "https://github.com/oojjrs/unity_oplat.git?path=/Packages/Steam",
    "com.rlabrecque.steamworks.net": "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#ba71581f1ed7349e8d0f17ddc6f135dd3bc8a6a3"
  }
}
```

Git 패키지의 의존성만으로는 다른 Git 패키지의 위치를 해석할 수 없으므로 소비 프로젝트 manifest에 세 항목을 직접 선언한다.

## 사용

1. GameObject에 `MyPlatformInitializer`를 추가한다.
2. 같은 GameObject의 컴포넌트에서 `MyPlatformInitializer.CallbackInterface`를 구현한다.
3. `InitialType`으로 `Anonymous` 또는 `Steam`을 반환하고, 초기화 이후 작업은 `OnOk()`에서 시작한다.

플랫폼별 구현은 별도 어셈블리에서 자동 등록되므로 소비자가 `AnonymousPlatform`이나 `SteamPlatform` 같은 구체 타입을 직접 참조하지 않는다.

플랫폼 구현은 별도 `DontDestroyOnLoad` GameObject의 `MonoBehaviour`로 생성된다. 따라서 `MyPlatformInitializer`는 `OnOk()` 이후 삭제해도 된다.

`Steam`은 Steamworks.NET의 `SteamAPI.InitEx`로 초기화한다. 이후 `SteamPlatform`이 매 프레임 콜백을 처리하고, 플랫폼 오브젝트가 파괴될 때 Steam API를 종료한다. Play Mode 중 스크립트를 다시 컴파일했다면 Play Mode를 재시작한다.

Steam 클라이언트가 실행 중이어야 하며 App ID는 Steam 실행 컨텍스트 또는 개발용 `steam_appid.txt`로 제공한다. 개발용 파일은 배포 빌드에 포함하지 않는다.

## 구조

- `Packages/src`: 코어 런타임과 익명 인증 구현
- `Packages/Steam`: Steamworks.NET 초기화 어댑터
