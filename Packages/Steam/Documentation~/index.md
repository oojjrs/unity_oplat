# OOJJRS' Unity Steam Platform

Steamworks.NET 기반 `com.oojjrs.oplat` 구현이다.

## 설치

Git 기반 Unity Package Manager는 패키지의 `package.json`에서 다른 Git 패키지를 자동 설치하지 못한다. 소비 프로젝트의 `Packages/manifest.json`에 다음 세 항목을 직접 선언한다.

```json
{
  "dependencies": {
    "com.oojjrs.oplat": "https://github.com/oojjrs/unity_oplat.git?path=/Packages/Core",
    "com.oojjrs.oplat.steam": "https://github.com/oojjrs/unity_oplat.git?path=/Packages/Steam",
    "com.rlabrecque.steamworks.net": "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.0"
  }
}
```

`com.oojjrs.oplat.steam`의 패키지 의존성은 필요한 호환 버전을 기록하며, 위 직접 선언이 실제 Git 위치를 제공한다.

Steamworks.NET Git 태그 `2025.164.0` 내부의 UPM 패키지 버전은 `2025.163.0`이므로 이 패키지의 의존성 선언은 `2025.163.0`을 사용한다.

이 패키지는 Steam용 소비 프로젝트 또는 Steam용 manifest variant에만 설치한다. Build Profile의 scripting define은 Steam 조립 assembly를 선택할 수 있지만, 프로젝트 manifest에 설치된 Steamworks.NET native plugin 자체를 제거하지는 않는다.

런타임 assembly는 에디터 또는 `OOJJRS_STORE_STEAM` scripting define이 설정된 Build Profile에서만 컴파일된다.

## 런타임

1. 게임의 Steam App ID를 전달해 `SteamPlatform`을 만든다.
2. `PlatformInterface.InitializeAsync`를 호출한다.
3. Unity Update에서 `PlatformInterface.RunCallbacks`를 호출한다.
4. Unity Authentication에 로그인할 때 `PlatformAuthenticationInterface.CreateTicketAsync`로 Web API ticket을 발급한다.
5. 로그인 요청이 끝나면 ticket을 `Dispose`한다.
6. 앱 종료 시 `PlatformInterface.Shutdown`을 호출한다.

기본 인증 identity는 Unity Authentication 공식 예제와 동일한 `unityauthenticationservice`이다. Unity Authentication에 넘기는 identity와 Steam ticket 발급에 사용한 identity가 반드시 같아야 한다.

플레이어 빌드에서는 `InitializeAsync`가 다른 Steam API보다 먼저 `SteamAPI.RestartAppIfNecessary`를 실행한다. 재실행이 필요하면 `SteamRestartRequiredException`을 던지므로 게임은 즉시 종료해야 한다. 에디터에서는 프로젝트 루트의 `steam_appid.txt`를 사용하며, 초기화 뒤 실제 App ID가 생성자에 전달한 값과 같은지 검증한다.

Steam API는 프로세스 전역 상태이므로 동시에 초기화된 `SteamPlatform`은 하나만 허용한다. 발급한 ticket은 반드시 `Dispose`하고, 컴포넌트 비활성화나 앱 종료 때 `Shutdown`한 뒤 다시 활성화할 경우 `InitializeAsync`를 다시 호출한다.
