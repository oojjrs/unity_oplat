# OOJJRS' Unity Platform

Unity 프로젝트의 플랫폼 인증 초기화를 한 진입점으로 묶는 패키지다.

## 사용법

1. GameObject에 `MyPlatformInitializer`를 추가한다.
2. 같은 GameObject의 컴포넌트에서 `MyPlatformInitializer.CallbackInterface`를 구현한다.
3. `InitialType`으로 `MyPlatformTypeEnum.Anonymous`를 반환한다.
4. 플랫폼 초기화 이후 작업을 `OnOk()`에서 수행한다.

현재는 `Anonymous`만 구현되어 있다. 다른 `MyPlatformTypeEnum` 값은 아직 `NotImplementedException`을 발생시킨다.

플랫폼 구현은 자체 어셈블리에서 자동 등록된다. 소비자는 `AnonymousPlatform` 같은 내부 구체 타입을 알거나 직접 등록할 필요가 없다.
