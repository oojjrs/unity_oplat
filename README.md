# UnityOplat

Unity 프로젝트의 플랫폼 인증 초기화를 한 진입점으로 묶는 `com.oojjrs.oplat` 패키지다.

하나의 패키지와 `oojjrs.oplat` 어셈블리에서 `Anonymous`와 `Steam` 초기화를 제공한다. 다른 `MyPlatformTypeEnum` 값을 선택하면 아직 `NotImplementedException`이 발생한다.

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

## 사용

1. GameObject에 `MyPlatformInitializer`를 추가한다.
2. 같은 GameObject의 컴포넌트에서 `MyPlatformInitializer.CallbackInterface`를 구현한다.
3. `AppId`로 앱 식별자를, `InitialType`으로 `Anonymous` 또는 `Steam`을 반환한다. `AnonymousInstanceId`에는 로컬 Anonymous 실행 인스턴스를 구분할 안정적인 값을 반환한다. Steam에는 Depot ID가 아닌 게임의 App ID를 사용한다.
4. 플랫폼 실행 결과는 `OnResult(MyPlatformServiceInterface service)`에서 처리한다.

`service.Account`, `service.Nickname`, `service.ProfileSprite`로 계정 식별자, 표시 이름, 프로필 이미지를 가져온다. `Anonymous`는 기기 고유 식별자와 기기 이름, 패키지 기본 프로필 이미지를 사용하며 플랫폼에서 지원하지 않는 값은 기기 이름이나 제품 이름으로 대체한다. Steam은 SteamID, 현재 persona name, 현재 사용자의 큰 크기 프로필 이미지를 사용한다. 프로필 이미지를 가져오지 못하면 `ProfileSprite`는 `null`이다.

`CallbackInterface.AnonymousInstanceId`는 `Anonymous`에서만 사용한다. `null`, 빈 문자열 또는 공백이면 기존 기기 기반 값을 그대로 사용한다. 값이 있으면 앞뒤 공백을 제거한 뒤 Account에 App ID와 함께 붙이고 Nickname에도 표시하여 같은 기기의 로컬 실행 인스턴스를 구분한다. 같은 논리 인스턴스에는 실행할 때마다 같은 값을 제공해야 하며, 명령행 인자나 테스트 런처 등 값의 출처는 소비 프로젝트가 결정한다.

`ProfileSprite`가 반환하는 `Sprite`의 수명은 플랫폼 서비스가 관리하므로 소비자가 직접 파괴하지 않는다.

플랫폼 서비스를 장기간 보관하는 경우 `service.IsAlive`를 확인한 뒤 접근한다.

`Account`는 클라이언트에서 조회한 식별 문자열이며 서버 인증 증명이 아니다.

플랫폼별 구현은 패키지 내부에서 자동 등록되므로 소비자가 `AnonymousPlatform`이나 `SteamPlatform` 같은 구체 타입을 직접 참조하지 않는다.

플랫폼 구현은 별도 `DontDestroyOnLoad` GameObject의 `MonoBehaviour`로 생성된다. 따라서 `MyPlatformInitializer`는 `OnResult(MyPlatformServiceInterface service)` 이후 삭제해도 된다.

플레이어 빌드의 `Steam`은 `SteamAPI.RestartAppIfNecessary`를 먼저 호출한다. `service.IsRestartRequired`가 `true`라면 Steam이 라이브러리에 설치된 게임을 다시 실행하므로 소비자는 현재 프로세스를 즉시 종료해야 하며, 이 결과에서 계정이나 프로필 정보에 접근하지 않는다. 재실행이 필요하지 않으면 `SteamAPI.InitEx`로 초기화하고 실제 App ID가 `CallbackInterface.AppId`와 같은지 검증한다.

Unity Editor에서는 재실행을 요청하지 않는다. Steam 클라이언트를 먼저 실행하고 현재 작업 디렉터리의 개발용 `steam_appid.txt`에 같은 App ID를 제공한다. 개발용 파일은 배포 빌드에 포함하지 않는다. Steam은 Unity Editor 프로세스에 연결되므로 Play Mode를 멈춰도 실행 중 표시가 Editor 종료 전까지 남을 수 있다.

초기화 이후 `SteamPlatform`이 매 프레임 콜백을 처리하고, 플랫폼 오브젝트가 파괴될 때 Steam API를 종료한다. Play Mode 중 스크립트를 다시 컴파일했다면 Play Mode를 재시작한다.

Steam 구현은 소비 프로젝트에 `STEAMWORKS_NET`이 정의된 경우에만 컴파일된다. Steamworks.NET은 기본 설정에서 Standalone 대상에 이 심볼을 자동으로 추가하며, 심볼이 없는 빌드에서는 `Anonymous`를 사용할 수 있다.

## 구조

- `Packages/src`: 코어 런타임과 Anonymous 및 Steam 구현
