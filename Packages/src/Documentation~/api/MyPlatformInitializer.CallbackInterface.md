# `MyPlatformInitializer.CallbackInterface`

`MyPlatformInitializer`가 플랫폼 설정과 장기 네트워크 결과 처리기를 받는 초기화 인터페이스다.

## 연결

1. GameObject에 `MyPlatformInitializer`를 추가한다.
2. 같은 GameObject의 컴포넌트에서 `CallbackInterface`를 구현한다.
3. 아래 속성과 결과 처리기를 모두 제공한다.
4. `OnResult`에서 반환된 서비스를 보관한다.

초기화기는 `Awake`에서 같은 GameObject의 `CallbackInterface`를 찾는다. 결과 처리기 다섯 개는 모두 `null`이 아니어야 하며 플랫폼 서비스가 살아 있는 동안 유효해야 한다.

## 멤버

| 멤버 | 설명 |
| --- | --- |
| `string AnonymousInstanceId` | 같은 기기의 Anonymous 실행 인스턴스를 구분하는 안정적인 값. Steam에서는 사용하지 않는다. |
| `uint AppId` | 앱 식별자. Steam은 0보다 큰 게임 App ID를 사용한다. |
| `MyPlatformTypeEnum InitialType` | 초기화할 플랫폼. 현재 지원 값은 `Anonymous`, `Steam`이다. |
| `MyNetChatResultInterface ChatResult` | 수신 채팅 처리기 |
| `MyNetHostResultInterface HostResult` | 호스트가 받을 요청 처리기 |
| `MyNetMemberResultInterface MemberResult` | 멤버가 받을 응답 처리기 |
| `MyNetPlayerServiceInterface.UpdateResultInterface PlayerResult` | 다른 구성원의 플레이어 갱신 알림 처리기 |
| `MyNetRoomServiceInterface.UpdateResultInterface RoomResult` | 다른 구성원의 방 갱신 알림 처리기 |
| `void OnResult(MyPlatformServiceInterface service)` | 초기화 또는 Steam 재실행 판단이 끝나면 호출된다. |

`OnResult` 이후 초기화기 컴포넌트는 제거해도 된다. 실제 플랫폼 구현은 별도 `DontDestroyOnLoad` GameObject에서 유지된다.

Steam 플레이어 빌드에서 `service.IsRestartRequired`가 `true`이면 `Account`, `Net`, `Storage` 등에 접근하지 말고 `Application.Quit()` 등으로 현재 프로세스를 종료한다.

플랫폼별 설정은 [Anonymous](../platforms/anonymous.md)와 [Steam](../platforms/steam.md)을 참고한다.
