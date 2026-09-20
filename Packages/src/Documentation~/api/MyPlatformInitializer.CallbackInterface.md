# `MyPlatformInitializer.CallbackInterface`

`MyPlatformInitializer`가 플랫폼 설정과 장기 네트워크 결과 처리기를 받는 초기화 인터페이스다.

## 연결

1. GameObject에 `MyPlatformInitializer`를 추가한다.
2. 같은 GameObject의 컴포넌트에서 `CallbackInterface`를 구현한다.
3. `AppId`, `InitialType`, `OnResult`를 구현한다.
4. 필요한 네트워크 결과 처리기만 선택적으로 제공한다.

초기화기는 `Awake`에서 같은 GameObject의 `CallbackInterface`를 찾는다. 생략한 선택 항목은 기본값 또는 no-op 처리기를 사용한다.

## 멤버

| 멤버 | 설명 |
| --- | --- |
| `string AnonymousInstanceId` | 선택 사항. Anonymous에서는 공백을 제거한 값을 계정 ID와 닉네임으로 사용하고, Ugsymous에서는 UGS 인증 profile을 결정한다. 기본값은 `null`이다. Steam에서는 사용하지 않는다. |
| `uint AppId` | 앱 식별자. Steam은 0보다 큰 게임 App ID를 사용한다. |
| `MyPlatformTypeEnum InitialType` | 초기화할 플랫폼. 현재 지원 값은 `Anonymous`, `Steam`, `Ugsymous`다. |
| `MyNetChatResultInterface ChatResult` | 선택 사항. 수신 채팅 처리기 |
| `MyNetFriendResultInterface FriendResult` | 선택 사항. 친구 초대와 참여 요청 처리기 |
| `MyNetHostResultInterface HostResult` | 선택 사항. 호스트가 받을 요청 처리기 |
| `MyNetMemberResultInterface MemberResult` | 선택 사항. 멤버가 받을 응답 처리기 |
| `MyNetPlayerServiceInterface.UpdateResultInterface PlayerResult` | 선택 사항. 다른 구성원의 플레이어 갱신 알림 처리기 |
| `MyNetRoomSwitchHandlerInterface RoomSwitchHandler` | 선택 사항. Steam 플랫폼 참여 요청의 게임 준비와 최종 전환 결과 처리기 |
| `MyNetRoomServiceInterface.UpdateResultInterface RoomResult` | 선택 사항. 다른 구성원의 방 갱신 알림 처리기 |
| `void OnResult(MyPlatformServiceInterface service)` | 초기화 또는 Steam 재실행 판단이 끝나면 호출된다. |

`OnResult` 이후 초기화기 컴포넌트는 제거해도 된다. 실제 플랫폼 구현은 별도 `DontDestroyOnLoad` GameObject에서 유지된다.

Steam 플레이어 빌드에서 `service.IsRestartRequired`가 `true`이면 `Account`, `Net`, `Storage` 등에 접근하지 말고 `Application.Quit()` 등으로 현재 프로세스를 종료한다.

`RoomSwitchHandler`를 생략하면 기존 게임과 같이 Steam 참여 요청을 `FriendResult.OnJoinRequested`로 받는다. 제공하면 Oplat이 준비 처리기를 호출한 뒤 공통 방 전환을 수행하고 최종 결과만 같은 처리기에 전달한다.

플랫폼별 설정은 [Anonymous](../platforms/anonymous.md), [Steam](../platforms/steam.md), [Ugsymous](../platforms/ugsymous.md)를 참고한다.
