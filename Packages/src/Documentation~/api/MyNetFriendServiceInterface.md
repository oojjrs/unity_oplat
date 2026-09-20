# 친구와 게임 초대 계약

`MyNetFriendServiceInterface`, `MyNetFriendInterface`, `MyNetFriendResultInterface`는 친구 목록과 게임 초대의 플랫폼 공통 계약이다.

현재 Anonymous와 Steam에서 친구 목록 조회·반복 조회·추가·초대 발송과 수신을 지원한다. Ugsymous는 `Friend` 접근 시 `NotSupportedException`을 발생시킨다.

## 친구 스냅샷

`MyNetFriendInterface`는 한 번 조회한 친구의 상태를 나타낸다. 조회 후의 변경은 다음 목록 결과로 전달한다.

| 멤버 | 의미 |
| --- | --- |
| `string Id` | 플랫폼의 계정 ID. 게임에서 해석하지 않는 문자열이다. |
| `string Nickname` | 표시 이름. 알 수 없으면 ID를 사용한다. |
| `string RoomId` | 같은 게임에서 조회 가능한 참여 대상 방 ID. 미접속·다른 게임·방 없음·비공개이면 빈 문자열이다. |
| `StateEnum State` | 플랫폼에서 관측되는 친구 상태. 아래 상태 값을 사용한다. |

문자열 프로퍼티는 null을 반환하지 않는다. 접속 상태와 방 ID는 입장 가능 여부를 보장하지 않는다. 친구 목록의 순서는 보장하지 않으며 같은 ID는 한 번만 반환한다.

`MyNetFriendInterface.StateEnum`은 다음 값을 제공한다. 숫자 값은 현재 프로젝트의 Steamworks.NET `EPersonaState`와 맞추되 공개 인터페이스는 Steam SDK 형식에 의존하지 않는다.

| 상태 | 값 | 의미 |
| --- | --- | --- |
| `Offline` | 0 | 오프라인 |
| `Online` | 1 | 온라인 |
| `Busy` | 2 | 바쁨·방해 금지 |
| `Away` | 3 | 자리 비움 |
| `Snooze` | 4 | 장시간 자리 비움 |
| `LookingToTrade` | 5 | 거래 희망 |
| `LookingToPlay` | 6 | 함께 플레이할 상대를 찾는 중 |
| `Invisible` | 7 | 온라인이지만 친구에게 오프라인으로 표시 |

Anonymous는 서버에 접속 세션이 있으면 `Online`, 없으면 `Offline`을 반환한다. Steam은 실제로 조회된 persona 상태를 매핑한다. 현재 프로젝트의 Steamworks.NET에는 `Invisible`이 정의되어 있지만 친구에게는 이 상태가 공개되지 않으므로, 오프라인으로 관측된 친구를 `Invisible`로 추정하지 않는다. Valve 웹 문서의 상태 표에는 `Invisible`이 누락되어 있어 이 값은 프로젝트에서 사용하는 SDK 정의를 기준으로 한다.

친구 상태와 게임 실행·방 참여 정보는 별개다. 다른 게임을 실행 중인 Steam 친구도 `Online`일 수 있지만 참여 대상 방은 반환하지 않는다.

## 목록 조회

| 메서드 | 동작 |
| --- | --- |
| `RefreshAsync(ResultInterface result)` | 목록을 한 번 조회한다. 호출자 취소 토큰은 없으며 플랫폼 수명을 따른다. |
| `StartAsync(ConfigInterface config, ResultInterface result)` | 즉시 한 번 조회한 뒤 반복 조회를 시작한다. 첫 조회가 끝나면 Task가 완료된다. |
| `Stop()` | 반복 조회를 중지한다. 여러 번 호출해도 같은 결과다. |

`ConfigInterface`는 `CancellationToken`과 `PollingDelaySeconds`를 제공한다. 간격은 최소 1초이며, 각 조회가 완료된 시점부터 다음 조회까지의 대기 시간이다. 새 Start는 기존 반복 조회를 교체하며 교체된 처리기로 추가 결과를 보내지 않는다. 취소 또는 Stop 이후에는 해당 반복 조회 결과를 전달하지 않는다. 방에 참여한 동안에도 친구 목록 갱신은 계속된다.

Anonymous는 기존 조회가 진행 중이면 새 Start의 첫 조회를 기다렸다가 실행한다. Stop·재시작·취소 이후 이전 반복 조회의 성공·오류 콜백은 전달하지 않는다. 이미 전송한 요청의 응답은 끝까지 수신한 뒤 다음 요청을 보내므로, 취소된 Start의 Task 완료는 진행 중인 응답 수신까지 지연될 수 있다. 아직 전송하지 않은 조회는 취소할 수 있다. Stop은 별도로 호출한 Refresh를 취소하지 않는다. 조회 오류를 `OnException`으로 전달한 뒤에는 같은 간격으로 반복 조회를 계속하며, 플랫폼 종료 시 반복 조회도 종료한다.

`ResultInterface.OnOk(IEnumerable<MyNetFriendInterface> friends)`는 전체 스냅샷을 반환한다. 친구가 없으면 빈 목록이다. Anonymous에서 Refresh가 진행 중인 동안 다시 호출하면 `OnBusy`로 완료한다. `UseLocal` 값과 관계없이 Anonymous는 기존 로컬 서버에 요청하고 Steam은 Steam 클라이언트의 현재 친구 정보를 읽는다. 플랫폼 조회 실패는 `OnException`으로 전달하며 빈 목록으로 숨기지 않는다.

## 친구 추가 요청

`RequestAddAsync(RequestAddConfigInterface config, RequestAddResultInterface result)`는 `PlayerId`에 해당하는 계정의 친구 추가 절차를 시작한다. Config에는 `CancellationToken`도 전달한다.

`OnOk(playerId)`는 플랫폼이 추가 절차를 시작했다는 뜻이다. 실제 친구 관계 성립은 이후 목록 결과로 확인한다. Steam에서는 사용자가 추가 화면을 닫거나 상대가 수락하지 않을 수도 있다.

Steam은 유효한 개인 Steam ID를 대상으로 Overlay의 `friendadd` 화면을 연다. 자기 자신을 지정했거나 Steam Overlay를 사용할 수 없으면 `NotPermitted`로 완료한다.

Anonymous는 요청자의 친구 목록에 대상 ID를 즉시 저장한다. 계정 실존·본인 확인·상대 승인 없이 미접속 ID도 등록하며 중복 등록은 성공으로 처리한다. 상대 목록을 자동 수정하지 않는다. 추가된 친구는 다음 Refresh 또는 반복 조회에서 확인한다.

Anonymous의 친구 목록은 접속 세션과 별개인 서버 측 저장 공간에 Project Key·App ID·계정별로 영속 저장한다. 저장을 완료한 뒤 성공을 반환하며, 같은 계정으로 재접속하거나 서버를 재시작해도 복원한다. 접속 상태와 방 ID는 저장하지 않고 조회 시점의 서버 상태에서 계산한다. 파일 경로와 직렬화 형식은 공개 API에 노출하지 않는다.

Anonymous에서 추가 요청이 진행 중이면 다음 추가 호출은 `OnBusy`로 완료한다. 전송 전 취소는 저장하지 않으며, 전송 후 취소는 이미 시작한 저장을 되돌리지 않는다. 응답을 수신한 뒤 취소로 완료하고 성공·오류 콜백은 보내지 않는다. 저장 실패는 `OnException`으로 전달한다.

## 초대 발송

`InviteAsync(InviteConfigInterface config, InviteResultInterface result)`는 `RoomId`의 방으로 `PlayerId`를 초대한다. Config에는 `CancellationToken`도 전달한다. 호출자는 해당 방의 구성원이어야 한다.

`OnOk(roomId, playerId)`는 플랫폼으로 발송 요청을 넘겼다는 뜻이며 상대에게 도착했거나 상대가 수락했다는 보장은 없다. Anonymous는 접속한 대상에게 즉시 전달하고 오프라인 초대를 보관하지 않는다. Steam의 발송 결과 역시 전달 확인으로 해석하지 않는다.

Steam은 호출자가 현재 참가한 Lobby와 `RoomId`가 같고 대상이 현재 Steam 친구일 때 `InviteUserToLobby`를 호출한다. Steam이 요청을 거절하면 `NotPermitted`로 완료한다.

Anonymous는 같은 Project Key·App ID로 접속한 대상에게만 초대를 전달한다. 호출자가 해당 방의 구성원이 아니거나 대상이 미접속이면 `NotPermitted`, 방이 없으면 `NotFoundRoom`으로 완료한다. 초대 요청이 진행 중이면 다음 초대 호출은 `OnBusy`로 완료한다. 전송 후 취소는 이미 전달된 초대를 되돌리지 않는다.

## 초대 수신과 참여 요청

`MyNetFriendResultInterface`는 목록 조회와 독립적으로 플랫폼 수명 동안 연결할 결과 처리기다. 목록 Stop으로 초대 수신을 중지하지 않는다.

`MyPlatformInitializer.CallbackInterface.FriendResult`로 처리기를 제공한다. 생략하면 수신 초대를 버린다.

| 콜백 | 의미 |
| --- | --- |
| `OnInvited(playerId, roomId)` | 초대가 도착했다. 아직 수락하지 않았다. |
| `OnJoinRequested(playerId, roomId)` | 플랫폼 UI에서 초대를 수락하거나 친구의 게임 참여를 선택했다. |

콜백의 `roomId`는 비어 있지 않다. `OnInvited`의 `playerId`는 초대한 계정이다. 시작 인자로 받은 참여 요청처럼 보낸 사람을 알 수 없는 경우 `OnJoinRequested`의 `playerId`는 빈 문자열이다. 수신 콜백은 Unity 메인 스레드에서 전달한다.

참여 요청 전에 반드시 초대 수신 콜백이 발생하는 것은 아니다. 게임 시작 인자로 들어온 요청은 플랫폼과 결과 처리기가 준비된 뒤 전달한다. 플랫폼 UI와 게임 UI가 같은 방의 참여를 중복 요청할 수 있으므로 게임은 이미 참여 중인 대상과 진행 중인 참여 작업을 구분한다.

Anonymous는 플랫폼 UI가 없으므로 `OnJoinRequested`를 발생시키지 않는다. 게임 UI는 `OnInvited`로 받은 방 ID를 보관하고 사용자가 수락하면 기존 Join을 호출한다.

Steam은 같은 앱의 `LobbyInvite_t`를 `OnInvited`로, 실행 중 받은 `GameLobbyJoinRequested_t`를 `OnJoinRequested`로 전달한다. 초대 수락으로 게임이 시작된 경우 Steam 시작 인자와 운영체제 명령행의 `+connect_lobby`를 읽어 첫 Update에서 보낸 사람 ID가 빈 `OnJoinRequested`를 한 번 전달한다.

게임 내 초대 수락 버튼과 친구의 게임 참여 버튼은 방 ID를 기존 `MyNetRoomServiceInterface.JoinAsync`에 전달한다. 플랫폼 UI의 `OnJoinRequested`도 같은 게임 측 참여 처리로 연결한다. 별도의 친구 전용 입장 API는 두지 않는다. 현재 방에서 나갈지, 비밀번호를 입력받을지는 게임이 결정하며 최종 성공은 Join 결과로 판단한다.

## 결과와 실패

각 작업 결과는 기존 `MyNetInterface.CatchInterface`를 따른다. 일회 작업은 성공·실패·예외·Busy 중 한 가지 결과를 전달한다. 취소는 `OperationCanceledException`으로 완료하며 성공 콜백을 보내지 않는다. 이미 시작한 플랫폼 UI나 발송된 메시지를 취소로 되돌리지는 않는다. 요청과 결과 처리는 Unity 메인 스레드를 사용한다.

빈 대상 ID는 `EmptyPlayerId`, 빈 방 ID는 `EmptyRoomId`, 사라진 방은 `NotFoundRoom`으로 처리한다. 해당 방의 구성원이 아니거나 플랫폼에서 요청을 처리할 수 없는 경우는 `NotPermitted`다. Anonymous에서 초대 대상이 미접속인 경우와 Steam에서 대상 ID·친구 관계·Overlay·현재 Lobby 조건이 맞지 않는 경우도 `NotPermitted`다. 미접속은 친구 추가를 거절하는 이유가 아니다. 플랫폼 예외와 서버 저장 실패는 `OnException`으로 전달한다.

## Steam 매핑

| 공통 계약 | Steam 구현 경로 |
| --- | --- |
| 친구 목록과 표시 이름 | `GetFriendCount`, `GetFriendByIndex`, `GetFriendPersonaName` |
| 접속 상태와 같은 게임의 방 | `GetFriendPersonaState`, `GetFriendGamePlayed` |
| 친구 추가 절차 시작 | `ActivateGameOverlayToUser("friendadd", steamId)` |
| 초대 발송 | `InviteUserToLobby` |
| 초대 도착 | 같은 게임의 `LobbyInvite_t` |
| 참여 요청 | `GameLobbyJoinRequested_t`, 시작 인자 `+connect_lobby` |

Steam의 Invisible 로비는 친구에게 일반 로비 정보로 노출되지 않으므로 비공개 방의 직접 참여 대상은 제공하지 않는다. 초대로 받은 방 ID는 기존 입장 경로로 전달한다.

Anonymous에서 공통 목록·초대 흐름을 검증할 수 있다. Steam의 친구 승인, Overlay, 게임 실행, 상태 공개 범위, 전달 지연은 Steam 클라이언트와 서로 다른 계정으로 별도로 확인해야 한다.

공식 API: [ISteamFriends](https://partner.steamgames.com/doc/api/ISteamFriends?l=english), [ISteamMatchmaking](https://partner.steamgames.com/doc/api/ISteamMatchmaking?l=english).
