# Anonymous 플랫폼

`Anonymous`는 외부 플랫폼 SDK 없이 로컬 개발과 다중 실행 테스트에 사용하는 구현이다.

## 계정과 프로필

- `Account`는 `SystemInfo.deviceUniqueIdentifier`를 사용하고, 지원되지 않으면 기기 이름과 제품 이름 순으로 대체한다.
- `Nickname`은 기기 이름을 사용하고, 지원되지 않으면 제품 이름으로 대체한다.
- `ProfileSprite`는 패키지의 기본 프로필 이미지를 사용한다.
- `AnonymousInstanceId`가 있으면 공백을 제거한 값을 `Account`와 `Nickname`에 반영해 같은 기기의 실행 인스턴스를 구분한다.

같은 논리 인스턴스에는 실행할 때마다 같은 `AnonymousInstanceId`를 제공해야 한다. 명령행 인자나 테스트 런처 등 값의 출처는 소비 프로젝트가 정한다.

## 저장소

Anonymous 저장소는 현재 Windows 계정의 다음 경로 아래에 파일을 직접 저장한다.

```text
%LOCALAPPDATA%\oojjrs\Oplat\AnonymousStorage\v1\<project-key SHA-256>\<AppId>\users\<account SHA-256>\files\<logical path>
```

Project Key는 `Application.identifier`이며, 값이 없으면 company/product 이름으로 대체한다. 프로젝트, App ID와 Account별로 격리되는 로컬 개발 저장소이며 신뢰할 수 있는 원격 데이터베이스가 아니다.

Unity 에디터의 `Tools > Oplat > Open Anonymous Storage Folder` 메뉴로 위 경로의 `v1` 폴더를 탐색기에서 연다. 플레이 모드나 로컬 서버 실행 여부와 관계없이 사용할 수 있으며, 폴더가 없으면 생성한다.

## 네트워크

Anonymous 네트워크는 `127.0.0.1:45831`의 로컬 서버를 사용한다. 채팅 메시지 한계는 `service.Net.Chat.MessageByteCountMax`에서 조회한다.

플랫폼 공통 API는 [인터페이스 문서](../index.md)를 참고한다.

## 친구 목록 조회

`service.Net.Friend.RefreshAsync(result)`는 기존 로컬 서버에 친구 목록을 요청한다. 서버가 다음 계정별 파일을 읽고 현재 접속 세션과 방 정보를 합쳐 스냅샷을 반환한다.

```text
%LOCALAPPDATA%\oojjrs\Oplat\AnonymousServer\v1\<project-key SHA-256>\<AppId>\users\<account SHA-256>\friends.json
```

파일 내용은 계정 ID의 JSON 배열이다. 해시는 UTF-8 문자열의 SHA-256을 소문자 16진수로 표현한다. 파일과 디렉터리가 없으면 빈 목록이며, 잘못된 JSON·읽기 권한 오류 등은 조회 실패로 전달한다. Refresh는 파일을 만들거나 수정하지 않는다.

```json
["friend-account-a", "friend-account-b"]
```

같은 ID는 한 번만 반환한다. 같은 Project Key·App ID의 접속 세션이 있으면 `Online`과 현재 닉네임을 반환하고, 없으면 `Offline`과 계정 ID를 표시 이름으로 반환한다. 접속한 친구가 공개 방에 있으면 `RoomId`를 제공하며, 방이 없거나 비공개이면 빈 문자열이다.

반복 조회는 `service.Net.Friend.StartAsync(config, result)`로 시작하고 `Stop()`으로 중지한다. 최소 1초 간격이며 방 참가 중에도 계속 조회한다. 재시작·중지·취소 이후 이전 반복 조회의 결과는 전달하지 않는다.

`service.Net.Friend.RequestAddAsync(config, result)`는 요청자의 목록에 대상 ID를 저장한다. 미접속 ID도 등록할 수 있고 중복 등록은 성공으로 처리한다. 상대 목록은 변경하지 않는다. 저장 완료 후 `OnOk(playerId)`가 호출되며 다음 Refresh 또는 반복 조회에서 추가된 친구를 확인한다. 초대는 아직 구현하지 않았다. 실행 인스턴스는 같은 버전을 사용해야 하며, 이번에 계정 연결 메시지에 Project Key·App ID가 추가되었으므로 이전 버전의 로컬 서버는 종료한 뒤 다시 실행한다.
