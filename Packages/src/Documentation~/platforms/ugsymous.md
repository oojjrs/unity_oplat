# Ugsymous 플랫폼

`Ugsymous`는 Unity Gaming Services의 익명 인증을 사용하는 구현이다. 기존 `Anonymous` 로컬 구현이나 `Standalone` enum 값과는 별개다.

## 구성

- Authentication의 `SignInAnonymouslyAsync`로 로그인한다.
- `AnonymousInstanceId`가 있으면 그 값의 해시로 UGS authentication profile을 선택한다.
- Cloud Save Player Files에 사용자 파일을 저장한다. 공개 저장소 계약의 경로형 파일명을 유지하기 위해 내부 키는 SHA-256으로 변환하고 원래 이름은 파일 헤더에 보존한다.
- Multiplayer Services의 Session으로 로비, 방, 플레이어를 관리한다.
- Host/Member 패킷은 Session의 custom `INetworkHandler`가 Relay와 Unity Transport의 reliable fragmentation pipeline으로 전달한다.
- 채팅은 Vivox text-only group channel을 사용한다.

## 프로젝트 설정

Unity Dashboard에서 프로젝트를 연결하고 Authentication, Cloud Save, Multiplayer Services·Relay, Vivox를 활성화해야 한다. `CallbackInterface.AppId`는 Ugsymous에서 사용하지 않는다.

Session 목록 정보인 `ISessionInfo`에는 참가 코드, private 여부와 플레이어 목록이 없으므로 Lobby 결과의 `Code`는 빈 문자열, `IsPrivate`는 `false`, `Players`는 빈 목록이다. 방에 참가하거나 생성한 뒤 얻는 Room 결과에는 전체 Session 정보가 제공된다.

Vivox 메시지 한계와 UTP 패킷 한계는 각각 `Chat.MessageByteCountMax`와 concrete transport 내부 제한을 따른다. 공개 API 호출과 결과 callback 처리는 Unity 메인 스레드에서 수행한다.
