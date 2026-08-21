# `MyNetInterface`

Lobby, 방, 플레이어, 채팅과 요청·응답 메시지 서비스의 진입점이다.

## 서비스

| 멤버 | 설명 |
| --- | --- |
| `Lobby` | 공개 방 목록 조회와 polling |
| `Room` | 방 생성·참가·수정·퇴장 |
| `Player` | 현재 플레이어의 필드 갱신 |
| `Chat` | 방 채팅 참가·전송·퇴장 |
| `Member` | 멤버에서 호스트로 요청 전송 |
| `Host` | 호스트에서 승인된 멤버로 응답 전송 |
| `UseLocal` | 현재 프로세스의 결과 처리기로 직접 전달할지 여부 |

`UseLocal = true`이면 현재 방이나 호스트 상태와 관계없이 Member/Host 메시지와 Chat 호출을 로컬 결과 처리기에 전달한다. Steam Lobby를 자동으로 만들지는 않는다. `false`로 되돌리면 이후 전송부터 현재 플랫폼의 방 경로를 사용한다.

## `CatchInterface`

네트워크 비동기 작업의 결과 인터페이스가 공통으로 상속한다.

| 콜백 | 의미 |
| --- | --- |
| `OnBusy()` | 다른 작업이 진행 중이거나 플랫폼이 요청을 받을 수 없다. |
| `OnFailed(FailureEnum)` | 예상 가능한 입력·권한·방 상태 오류다. |
| `OnException(MyNetSessionException)` | 플랫폼 또는 전송 중 발생한 예외다. |

취소된 작업은 실패 콜백 대신 `OperationCanceledException`으로 완료될 수 있다.

`FailureEnum`은 `EmptyCode`, `EmptyMessage`, `EmptyPlayerId`, `EmptyRoomId`, `MessageTooLong`, `NotFoundRoom`, `NotPermitted`를 제공한다.

## `Field`

방과 플레이어에 붙이는 `key`, `value`, `visibility` 데이터다.

| `VisibilityEnum` | 공개 범위 |
| --- | --- |
| `Public` | Lobby 목록과 승인된 방 구성원 |
| `Member` | 승인된 방 구성원 |
| `Private` | 방 필드는 호스트, 플레이어 필드는 해당 플레이어 |

Update 작업은 전달한 필드를 key 기준으로 기존 값에 병합한다. 플랫폼별 null·중복 key 처리가 다를 수 있으므로 이식 가능한 데이터는 명시적인 고유 key와 문자열 값을 사용한다.

Steam의 필드 크기와 스냅샷 제한은 [Steam 플랫폼](../platforms/steam.md)을 참고한다.
