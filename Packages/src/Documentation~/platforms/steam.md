# Steam 플랫폼

`Steam`은 Steamworks.NET을 통해 계정, Remote Storage, Lobby와 reliable P2P 메시지를 제공한다.

## 초기화

- Steam 구현은 소비 프로젝트에 `STEAMWORKS_NET`이 정의된 경우에만 컴파일된다. Steamworks.NET 기본 설정은 Standalone 대상에 이 심볼을 추가한다.
- 플레이어 빌드는 `SteamAPI.RestartAppIfNecessary`를 먼저 호출한다. `IsRestartRequired`가 `true`이면 다른 서비스에 접근하지 말고 현재 프로세스를 즉시 종료한다.
- 재실행이 필요하지 않으면 `SteamAPI.InitEx`를 실행하고 실제 App ID가 `CallbackInterface.AppId`와 같은지 확인한다. App ID는 0보다 큰 게임 App ID여야 하며 Depot ID가 아니다.
- Unity Editor에서는 Steam 클라이언트를 먼저 실행하고 현재 작업 디렉터리의 `steam_appid.txt`에 같은 App ID를 둔다. 이 개발용 파일은 배포 빌드에 포함하지 않는다.
- Play Mode 중 스크립트를 다시 컴파일했다면 Play Mode를 재시작한다. Steam 실행 중 표시는 Unity Editor를 종료할 때까지 남을 수 있다.

초기화된 플랫폼 오브젝트가 매 프레임 Steam 콜백을 처리하며, 오브젝트가 파괴될 때 Steam API를 종료한다.

## 시간

초기화 직후 `SteamUtils.GetServerRealTime()`의 Unix epoch 초를 기준점으로 잡고 이후 경과 시간을 monotonic clock으로 계산한다. `service.Time.IsSynchronized`는 `true`다. Steam 기준점은 초 단위이므로 tick 단위로 전진하더라도 실제 기준 정확도는 약 1초 수준이다.

채팅 메시지는 발신자의 Steam 서버 기준 `MyTime`을 Lobby 메시지에 포함해 `sentAt`으로 전달한다.

## Steam Cloud

Steamworks App Admin에서 사용자별 byte quota와 file count를 설정하고 Cloud 설정을 저장·게시해야 한다. Storage Task 완료는 현재 프로세스의 `ISteamRemoteStorage` 작업 완료를 뜻하며, 기기 간 업로드·다운로드는 Steam 클라이언트의 후속 동기화가 담당한다.

모든 Storage 작업은 Unity 메인 스레드에서 시작해야 한다. 취소는 백엔드 전달 전까지만 즉시 적용되며, 전달된 작업은 실제 결과까지 기다려 성공을 취소로 오인하거나 변경을 롤백하지 않는다.

## 친구

- `service.Net.Friend.RefreshAsync(result)`는 Steam의 즉시 친구 목록을 읽고 Steam ID, 표시 이름, persona 상태와 같은 앱의 참가 가능한 Lobby ID를 반환한다. 표시 이름을 알 수 없으면 Steam ID를 사용하고, 다른 게임이나 Lobby가 없는 친구의 `RoomId`는 빈 문자열이다.
- `StartAsync(config, result)`는 즉시 한 번 조회한 뒤 최소 1초 간격으로 반복하고 `Stop()`으로 중지한다. 방 참가 중에도 계속 조회하며 목록 중지는 초대 콜백을 중지하지 않는다.
- `RequestAddAsync(config, result)`는 유효한 개인 Steam ID의 `friendadd` Overlay를 연다. `OnOk`는 Overlay 요청을 넘겼다는 뜻이며 실제 친구 승인 결과가 아니다. Overlay를 사용할 수 없거나 자기 자신을 지정하면 `NotPermitted`다.
- `InviteAsync(config, result)`는 호출자가 현재 참가한 Lobby로 현재 Steam 친구를 초대한다. Steam이 발송 요청을 받았을 때만 `OnOk`를 호출하며 상대에게 도착하거나 수락했다는 보장은 없다.
- `Public` 또는 `FriendsOnly`이고 잠기지 않았으며 정원이 남은 Lobby에 승인된 동안 `connect` Rich Presence에 `+connect_lobby <Lobby SteamID>`를 게시한다. Steam 친구 메뉴의 게임 참여를 선택하면 실행 중에는 `GameRichPresenceJoinRequested_t`, 새 실행에는 시작 인자로 참가 요청이 전달된다. 방이 `Private`·잠금·만원 상태가 되거나 퇴장하면 `connect`를 제거한다.
- `LobbyInvite_t`는 Steam UI가 초대 알림과 수락 절차를 제공하므로 게임에 `FriendResult.OnInvited`로 전달하지 않는다. `RoomSwitchHandler`가 있으면 실행 중의 `GameLobbyJoinRequested_t`·`GameRichPresenceJoinRequested_t`와 새 실행의 `+connect_lobby`를 게임 준비, 현재 방 정리, 대상 방 참가와 최종 결과 흐름으로 처리한다. 처리기를 생략한 기존 게임에는 동일한 요청을 `FriendResult.OnJoinRequested`로 전달한다.

친구 서비스는 Unity 메인 스레드에서 호출한다. 친구 승인, Overlay 표시, 초대 표시·수락과 게임 재실행은 Steam 클라이언트와 서로 다른 두 계정으로 확인해야 한다.

## Lobby와 P2P

- 방은 Steam Lobby, 게임 요청과 응답은 `ISteamNetworkingMessages`의 reliable P2P 메시지로 처리한다.
- 방 `Id`는 Lobby SteamID의 10진수 문자열이고 `Code`는 같은 값을 표현한 13자리 Base32 문자열이다.
- `Visibility`는 `Public`, `FriendsOnly`, `Private`를 각각 같은 이름의 Steam Lobby 타입으로 매핑한다. `FriendsOnly`는 일반 목록에 나타나지 않지만 친구와 초대 대상이 참가할 수 있고, `Private`는 Steam 초대로만 참가할 수 있다. 일반 방 공개 범위에 특수 목적의 `Invisible` Lobby는 사용하지 않는다.
- `IsLocked`이거나 정원이 찬 방은 Steam 검색 결과에서 제외된다. 한 번의 목록 조회는 최대 50개다.
- Steam 클라이언트가 Steam 서버에 로그인되어 있지 않거나 Lobby 목록 요청이 I/O failure로 끝나면 반복 조회를 중지하고 `OnFailed(Disconnected)`를 호출한다.
- 비밀번호는 참가 후 호스트가 확인하며, 강퇴는 클라이언트가 제어 메시지에 따라 나가는 협조형 동작이다. 변조된 클라이언트를 Lobby 자체에서 강제로 제거하지는 못한다.
- `Public` 필드는 목록 조회용 Lobby metadata에도 게시한다. 승인된 멤버는 방의 `Public`·`Member` 필드, 공개 여부, 잠금, 정원, 제목과 플레이어 목록을 하나의 P2P 스냅샷으로 적용한다. `Private` 필드는 해당 클라이언트 메모리에만 둔다.
- 멤버 스냅샷 한 개는 64 KiB 이하여야 한다. 플레이어 갱신 결과를 확인할 수 없으면 상태 불일치를 막기 위해 해당 멤버가 방을 나간다.
- 플레이어 갱신 요청자는 호출 때 전달한 Result로, 나머지 구성원은 호스트를 포함해 초기화 때 등록한 `PlayerResult`로 결과를 받는다.
- 실패한 방·플레이어 알림과 게임 응답은 수신자별 순서를 유지해 재전송하며, 이미 전송을 수락한 수신자에게 중복 전송하지 않는다. 퇴장한 멤버의 대기 메시지는 제거한다.
- 송신 요청·응답 큐는 각각 256개다. 가득 차면 `Send`가 예외를 던지므로 호출자는 해당 요청을 보존하고 이후 재시도해야 한다. 수신 또는 동기화 큐의 한계로 일관성을 유지할 수 없으면 방을 나가고 `RoomResult.OnException`으로 알린다.
- 승인된 피어의 P2P 세션이 실패하면 이미 전송 수락된 메시지의 전달 여부를 확정할 수 없어 방을 나가고 오류를 알린다. 호스트에서 발생하면 방 전체를 닫는다.
- 프레임당 최대 32개의 P2P 메시지를 수신 처리한다. 이미 꺼낸 정상 메시지는 누적 바이트 수 때문에 버리지 않는다.
- Steam의 자동 호스트 이전은 지원하지 않는다. 원래 호스트가 나가거나 Lobby 소유자가 바뀌면 각 멤버가 Lobby를 나가고 `RoomResult.OnFailed(NotFoundRoom)`을 받는다.
- 패키지 `1.10.3`부터 통신 프로토콜은 3이며 방 공개 범위를 세 단계로 전달한다. 이전 프로토콜의 방은 검색·참가 대상에서 제외되므로 함께 플레이하는 모든 클라이언트를 업데이트해야 한다.

Chat·Lobby·Room·Player 작업은 Unity 메인 스레드에서 호출한다. `Member.Send`와 `Host.Send`는 Steam에서 다른 스레드에서도 큐에 넣을 수 있지만, 플랫폼 간 이식성을 위해 공통 코드는 메인 스레드에서 호출하는 편이 안전하다.

Steam의 `Send`는 패킷 객체를 큐에 적재한다. 원격 전송에는 Anonymous와 같은 `MyNetSerializer`와 `MyNetDeserializer`를 사용하며, Steam은 바이트 전송을 담당한다. 호스트 자신의 처리와 `UseLocal`은 직렬화 없이 원본 객체를 전달한다. 전송과 처리가 끝날 때까지 적재한 객체를 변경하지 않는다.

전송과 수신 결과 적용은 플랫폼의 `Update`에서 진행한다. 메인 스레드 정지나 백그라운드 실행 중단은 적용을 지연시킬 수 있다. Steam 서버 기준 시계는 잘못된 PC 시각의 큰 오차를 피하지만 초 미만의 멀티플레이 정밀도를 보장하지 않는다. 더 높은 정밀도가 필요하면 게임에서 왕복 시간과 호스트 시계 차이를 별도로 처리한다.

`CreateAsync`와 `JoinAsync`는 네이티브 요청을 직접 취소할 수 없어 늦게 생성되거나 참가된 Lobby를 정리한 뒤 완료될 수 있다. 패키지는 P2P 채널 `45831`을 사용하므로 같은 API의 전역 session callback을 다른 시스템도 다룬다면 dispatcher와 소유권 정책을 공유해야 한다.

`SwitchAsync`와 `RoomSwitchHandler`의 자동 전환은 진행 중인 Lobby 목록 조회가 끝난 뒤 방 전환을 계속한다. 다른 방 작업이 진행 중이거나 전환 요청 자체가 겹치면 기존과 같이 `OnBusy`로 완료한다.

저장소의 `steam_appid.txt`는 공유 테스트 앱 Spacewar `480`을 사용한다. 실제 제품 검증과 배포에는 해당 Steamworks 앱의 App ID를 사용한다.
