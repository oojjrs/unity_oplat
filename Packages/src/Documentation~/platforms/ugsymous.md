# Ugsymous 플랫폼

`Ugsymous`는 Unity Gaming Services의 익명 인증을 사용하는 구현이다. 기존 `Anonymous` 로컬 구현이나 `Standalone` enum 값과는 별개다.

## 구성

- Authentication의 `SignInAnonymouslyAsync`로 로그인한다.
- `AnonymousInstanceId`가 있으면 그 값의 해시로 UGS authentication profile을 선택한다.
- Cloud Save Player Files에 사용자 파일을 저장한다. 공개 저장소 계약의 경로형 파일명을 유지하기 위해 내부 키는 SHA-256으로 변환하고 원래 이름은 파일 헤더에 보존한다.
- Multiplayer Services의 Session으로 로비, 방, 플레이어를 관리한다.
- Friends로 친구 요청, 목록, presence와 방 초대를 관리한다.
- Host/Member 패킷은 Session의 custom `INetworkHandler`가 Relay와 Unity Transport의 reliable fragmentation pipeline으로 전달한다.
- 채팅은 Vivox text-only group channel을 사용한다.

## 프로젝트 설정

Unity Dashboard에서 프로젝트를 연결하고 Authentication, Cloud Save, Friends, Multiplayer Services·Relay, Vivox를 활성화해야 한다. `CallbackInterface.AppId`는 Ugsymous에서 사용하지 않는다.

> [!WARNING]
> 현재 패키지는 Ugsymous Friends의 코드 계약과 패키지 의존성만 제공한다. UGS 플랫폼을 실제 지원 대상으로 전환할 때는 Dashboard의 Friends 활성화, DSA 알림 요구사항, 사용할 UGS 환경을 먼저 확정하고 서로 다른 두 계정으로 친구 승인·presence·방 초대 왕복을 검증해야 한다.

패키지는 `com.unity.services.friends` 1.2.0에 의존한다. 초기화는 Core, Authentication 로그인, Friends, Vivox 순서로 진행하며 Friends 관계에는 presence와 profile을 포함한다.

Lobby 목록 조회는 공개 Lobby만 반환한다. 목록 결과의 `Code`는 빈 문자열, `IsPrivate`는 `false`, `Players`는 빈 목록이다. 방에 참가하거나 생성한 뒤 얻는 Room 결과에는 전체 Session 정보가 제공된다.

Lobby 조회가 네트워크 오류, bad gateway, service unavailable 또는 gateway timeout으로 끝나면 반복 조회를 중지하고 `OnFailed(Disconnected)`를 호출한다.

방장이 자기 퇴장을 요청하면 `LeaveAsync`로 호스트를 이전하지 않고 Session을 삭제해 모든 멤버를 내보낸다. 삭제되거나 강퇴된 멤버는 `RoomResult.OnFailed(NotFoundRoom)`을 받는다.

Vivox 메시지 한계와 UTP 패킷 한계는 각각 `Chat.MessageByteCountMax`와 concrete transport 내부 제한을 따른다. 공개 API 호출과 결과 callback 처리는 Unity 메인 스레드에서 수행한다.

## 친구와 방 초대

`Net.Friend.RequestAddAsync`는 UGS Player ID로 친구 요청을 보낸다. 상대가 먼저 보낸 요청이 있으면 친구 관계를 수락한다. 친구가 되기 전의 수신 요청 목록과 수락 UI는 공개 Oplat 계약에 포함하지 않으므로 게임이 별도로 제공해야 한다.

친구 목록을 갱신할 때 현재 공개 Session ID를 자신의 Friends presence activity에 게시한다. 친구의 presence가 `Online`, `Busy`, `Away` 중 하나일 때만 해당 activity의 Room ID를 노출한다. 비공개 Session은 presence에 Room ID를 게시하지 않는다.

`Net.Friend.InviteAsync`는 현재 참가한 Session ID를 온라인 친구에게 Friends 메시지로 보낸다. 수신 측은 `FriendResult.OnInvited`로 Player ID와 Room ID를 받고, 사용자가 수락하면 기존 `Room.JoinAsync`를 호출한다. UGS Friends에는 Steam과 같은 플랫폼 초대 수락 UI가 없으므로 `OnJoinRequested`는 발생하지 않는다. 메시지는 오프라인 보관용이 아니며 Friends 서비스가 허용하는 presence 상태에서만 발송한다.

친구 목록, presence, 초대는 같은 Unity 프로젝트와 UGS 환경 안에서 동작한다. 개발·스테이징·프로덕션 환경을 나누면 각 환경의 관계와 상태도 서로 분리된다.

UGS Friends를 사용하는 게임은 [Unity의 현재 DSA 알림 요구사항](https://docs.unity.com/en-us/friends/guides/get-started)도 별도로 검토하고 적용해야 한다.
