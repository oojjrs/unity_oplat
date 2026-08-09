# UnityOplat

Unity 프로젝트의 플랫폼 인증 초기화를 한 진입점으로 묶는 `com.oojjrs.oplat` 패키지다.

현재는 `Anonymous` 초기화만 구현되어 있다. 다른 `MyPlatformTypeEnum` 값을 선택하면 아직 `NotImplementedException`이 발생한다.

## 사용

1. GameObject에 `MyPlatformInitializer`를 추가한다.
2. 같은 GameObject의 컴포넌트에서 `MyPlatformInitializer.CallbackInterface`를 구현한다.
3. `InitialType`으로 `Anonymous`를 반환하고, 초기화 이후 작업은 `OnOk()`에서 시작한다.

플랫폼별 구현은 별도 어셈블리에서 자동 등록되므로 소비자가 `AnonymousPlatform` 같은 구체 타입을 직접 참조하지 않는다.

## 구조

- `Runtime`: 런타임 코드와 에셋
- `Runtime/Anonymous`: 익명 인증 구현과 자동 등록 코드
- `Documentation~`: 패키지 사용자 문서
