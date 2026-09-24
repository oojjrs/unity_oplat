# Anonymous 플랫폼

`Anonymous`는 외부 플랫폼 SDK 없이 로컬 개발과 다중 실행 테스트에 사용하는 구현이다.

## 계정과 프로필

- `Account`는 `SystemInfo.deviceUniqueIdentifier`를 사용하고, 지원되지 않으면 기기 이름과 제품 이름 순으로 대체한다.
- `Nickname`은 기기 이름을 사용하고, 지원되지 않으면 제품 이름으로 대체한다.
- `ProfileSprite`는 패키지의 기본 프로필 이미지를 사용한다.
- `AnonymousInstanceId`가 있으면 공백을 제거한 값을 `Account`와 `Nickname`으로 사용한다.

같은 논리 인스턴스에는 실행할 때마다 같은 `AnonymousInstanceId`를 제공해야 한다. 명령행 인자나 테스트 런처 등 값의 출처는 소비 프로젝트가 정한다.

## 시간

`service.Time`은 플랫폼 생성 시점의 `DateTime.UtcNow`를 기준점으로 잡고 이후 경과 시간을 monotonic clock으로 계산한다. 외부 서버와 동기화하지 않으므로 `IsSynchronized`는 `false`다.

원격 채팅의 `sentAt`은 Anonymous 로컬 서버가 메시지를 접수한 UTC 시각이다. `UseLocal` 채팅은 플랫폼 시간 서비스의 시각을 사용한다.

## 저장소

Anonymous 저장소는 현재 Windows 계정의 다음 경로 아래에 파일을 직접 저장한다.

```text
%LOCALAPPDATA%\oojjrs\Oplat\AnonymousStorage\v1\AppId=<AppId>\users\Account=<Account>\files\<logical path>
```

`AppId=`와 `Account=` 접두사 뒤에 실제 값을 사용하므로 폴더 이름에서 경로의 출처를 확인할 수 있다. Account에 경로 문자로 사용할 수 없는 값이 있으면 URI 방식으로 이스케이프한다. App ID와 Account별로 격리되는 로컬 개발 저장소이며 신뢰할 수 있는 원격 데이터베이스가 아니다. App ID는 소비 프로젝트마다 고유한 값을 사용해야 한다. 이전 Project Key·해시 기반 경로의 데이터는 자동으로 이전하지 않는다.

Unity 에디터의 `Tools > Oplat > Open Anonymous Storage Folder` 메뉴로 위 경로의 `v1` 폴더를 탐색기에서 연다. 플레이 모드나 로컬 서버 실행 여부와 관계없이 사용할 수 있으며, 폴더가 없으면 생성한다.

## 네트워크

Anonymous 네트워크는 `127.0.0.1:45831`의 로컬 서버를 사용한다. 채팅 메시지 한계는 `service.Net.Chat.MessageByteCountMax`에서 조회한다.

Lobby 조회 중 로컬 서버의 스트림 또는 소켓 연결이 종료되면 반복 조회를 중지하고 `OnFailed(Disconnected)`를 호출한다. 종료된 연결과 현재 방 상태를 버리며, 이후 네트워크 요청에서 로컬 서버 시작을 다시 시도하고 새 연결로 재인증한다.

방장이 나가거나 연결이 끊기면 로컬 서버가 방을 삭제하고 남은 멤버를 모두 내보낸다. 멤버는 `RoomResult.OnFailed(NotFoundRoom)`을 받아 로비 전환을 처리할 수 있으며, `UseLocal` 사용 여부와 관계없이 이 방 수명주기 알림을 받는다.

플랫폼 공통 API는 [인터페이스 문서](../index.md)를 참고한다.

## 친구

`service.Net.Friend.RefreshAsync(result)`는 기존 로컬 서버에 친구 목록을 요청한다. 서버가 다음 계정별 파일을 읽고 현재 접속 세션과 방 정보를 합쳐 스냅샷을 반환한다.

```text
%LOCALAPPDATA%\oojjrs\Oplat\AnonymousServer\v1\AppId=<AppId>\users\Account=<Account>\friends.json
```

파일 내용은 계정 ID의 JSON 배열이다. `AnonymousInstanceId`를 제공한 실행 인스턴스의 계정 ID는 해당 값과 같다. 경로의 App ID와 Account 표기 및 이스케이프 규칙은 Anonymous 저장소와 같다. 파일과 디렉터리가 없으면 빈 목록이며, 잘못된 JSON·읽기 권한 오류 등은 조회 실패로 전달한다. Refresh는 파일을 만들거나 수정하지 않는다.

```json
["alice", "bob"]
```

Unity 에디터의 `Tools > Oplat > Open Anonymous Friend List` 메뉴로 해당 파일을 기본 앱에서 연다. 플레이 중이면 초기화된 Anonymous 인스턴스를 사용하고, 편집 모드에서는 선택한 `MyPlatformInitializer` 또는 열린 씬의 유일한 Anonymous 초기화기 설정에서 App ID와 인스턴스 ID를 읽는다. 초기화기가 여러 개면 원하는 GameObject를 먼저 선택한다. 파일이나 디렉터리가 없으면 빈 배열 `[]`로 생성한다.

같은 ID는 한 번만 반환한다. `Id`와 `Nickname`은 파일에 입력한 계정 ID를 그대로 반환한다. 같은 App ID에서 해당 계정 ID로 인증한 세션이 있으면 `Online`, 없으면 `Offline`이다. 접속한 친구가 `Public` 또는 `FriendsOnly` 방에 있으면 `RoomId`를 제공하며, 방이 없거나 `Private`이면 빈 문자열이다. Lobby 목록에는 `Public` 방만 반환한다.

반복 조회는 `service.Net.Friend.StartAsync(config, result)`로 시작하고 `Stop()`으로 중지한다. 최소 1초 간격이며 방 참가 중에도 계속 조회한다. 재시작·중지·취소 이후 이전 반복 조회의 결과는 전달하지 않는다.

`service.Net.Friend.RequestAddAsync(config, result)`는 요청자의 목록에 대상 계정 ID를 저장한다. 미접속 대상도 등록할 수 있고 같은 값의 중복 등록은 성공으로 처리한다. 상대 목록은 변경하지 않는다. 저장 완료 후 `OnOk(playerId)`가 호출되며 다음 Refresh 또는 반복 조회에서 추가된 친구를 확인한다.

`service.Net.Friend.InviteAsync(config, result)`는 호출자가 참가한 방으로 같은 App ID에서 계정 ID가 일치하는 접속 대상을 초대한다. 대상의 `MyPlatformInitializer.CallbackInterface.FriendResult`에 호출자의 계정 ID와 방 ID를 전달하며 오프라인 초대는 저장하지 않는다. Anonymous에는 플랫폼 참여 UI가 없으므로 `OnJoinRequested`는 발생하지 않는다. 게임 UI에서 초대를 수락하면 준비를 끝낸 뒤 받은 방 ID로 `Room.SwitchAsync`를 호출한다. Oplat이 현재 방 퇴장 또는 닫기와 새 방 참가를 순서대로 수행한다.

실행 인스턴스는 같은 버전을 사용해야 하며, 인증 메시지 형식이 변경되었으므로 이전 버전의 로컬 서버는 종료한 뒤 다시 실행한다.
