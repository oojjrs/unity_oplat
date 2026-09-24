# `MyNetRoomSwitchHandlerInterface`

Steam 플랫폼 참여 요청을 게임 준비와 공통 방 전환에 연결하는 선택적 처리기다. `MyPlatformInitializer.CallbackInterface.RoomSwitchHandler`로 제공한다.

## 준비

`PrepareAsync(playerId, roomId, cancellationToken)`는 `GameLobbyJoinRequested_t`, `GameRichPresenceJoinRequested_t` 또는 시작 인자 `+connect_lobby`를 받은 뒤 방 전환 전에 한 번 호출된다. 게임은 이 Task에서 로비 씬 이동, 로딩 완료 대기와 플레이어별 참가 설정 준비를 끝낸다. 시작 인자에서 보낸 사람을 알 수 없으면 `playerId`는 빈 문자열이다.

성공 시 `PreparationInterface`를 반환한다.

| 멤버 | 설명 |
| --- | --- |
| `Password` | 참가 비밀번호. 없으면 `null` 또는 빈 문자열 |
| `PlayerFields` | 참가 플레이어의 초기 필드 |
| `PlayerNickname` | 참가 플레이어 표시 이름 |

대상 `roomId`와 작업 취소 토큰은 Oplat이 플랫폼 요청에서 직접 구성한다. 준비 결과가 `null`이거나 준비 중 예외가 발생하면 방을 바꾸지 않고 `OnException`을 호출한다.

## 최종 결과

이 인터페이스는 `MyNetRoomServiceInterface.JoinResultInterface`를 상속한다. 준비가 끝나면 Oplat이 `SwitchAsync`와 같은 공통 흐름으로 현재 방 확인, 필요한 퇴장 또는 방 닫기, 대상 방 참가를 수행하고 `OnOk`, `OnBusy`, `OnFailed`, `OnException` 중 하나로 최종 결과를 전달한다.

처리기를 제공하지 않은 기존 게임은 기존 계약을 유지하며 Steam 참여 요청을 `FriendResult.OnJoinRequested`로 받는다. Anonymous와 Ugsymous는 플랫폼 참여 UI가 없으므로 이 처리기를 자동 호출하지 않는다. 두 플랫폼의 게임 내 초대 수락은 게임이 준비를 끝낸 뒤 `Room.SwitchAsync`를 호출한다.
