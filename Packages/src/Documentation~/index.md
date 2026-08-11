# OOJJRS' Unity Platform

Unity 프로젝트의 플랫폼 인증 초기화를 한 진입점으로 묶는 패키지다.

## 사용법

1. GameObject에 `MyPlatformInitializer`를 추가한다.
2. 같은 GameObject의 컴포넌트에서 `MyPlatformInitializer.CallbackInterface`를 구현한다.
3. `InitialType`으로 `MyPlatformTypeEnum.Anonymous`를 반환한다. Steam 어댑터 패키지를 설치한 프로젝트에서는 `MyPlatformTypeEnum.Steam`도 사용할 수 있다.
4. 플랫폼 초기화 이후 작업을 `OnOk(MyPlatformServiceInterface service)`에서 수행한다.

`service.Account`와 `service.Nickname`으로 초기화된 플랫폼의 계정 식별자와 표시 이름을 가져온다. `Anonymous`는 기기 고유 식별자와 기기 이름을 사용한다. 기기 식별자를 지원하지 않으면 계정도 기기 이름을 사용하고, 기기 이름도 지원하지 않으면 제품 이름으로 대체한다. Steam은 SteamID와 현재 persona name을 사용한다.

`Account`는 클라이언트에서 조회한 식별 문자열이며 서버 인증 증명이 아니다.

코어 패키지는 `Anonymous`를 구현한다. `com.oojjrs.oplat.steam`을 함께 설치하면 `Steam` 구현이 자동 등록된다. 다른 `MyPlatformTypeEnum` 값은 아직 `NotImplementedException`을 발생시킨다.

플랫폼 구현은 자체 어셈블리에서 자동 등록된다. 소비자는 `AnonymousPlatform`이나 `SteamPlatform` 같은 내부 구체 타입을 알거나 직접 등록할 필요가 없다.

플랫폼 구현은 별도 `DontDestroyOnLoad` GameObject의 `MonoBehaviour`로 생성된다. `MyPlatformInitializer`는 `OnOk(MyPlatformServiceInterface service)` 이후 삭제해도 플랫폼 수명 주기에 영향을 주지 않는다.

Steam 설치와 실행 조건은 Steam 어댑터 패키지 문서를 따른다.
