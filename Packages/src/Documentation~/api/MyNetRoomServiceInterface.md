# `MyNetRoomServiceInterface`

방을 생성하고 참가하며, 방 정보와 구성원을 변경한다.

## 메서드

| 메서드 | 성공 결과 |
| --- | --- |
| `CreateAsync(CreateConfigInterface, CreateResultInterface)` | 생성하고 참가한 `MyNetRoomInterface` |
| `JoinAsync(JoinConfigInterface, JoinResultInterface)` | 참가한 `MyNetRoomInterface` |
| `UpdateAsync(UpdateConfigInterface, UpdateResultInterface)` | 갱신된 `MyNetRoomInterface` |
| `ExitAsync(ExitConfigInterface, ExitResultInterface)` | 처리한 `roomId`, `playerId` |

각 Result 인터페이스는 `MyNetInterface.CatchInterface`를 상속한다.

## `CreateConfigInterface`

| 멤버 | 설명 |
| --- | --- |
| `CancellationToken` | 작업 취소 토큰 |
| `IsLocked` | 생성 직후 참가를 잠글지 여부 |
| `IsPrivate` | 일반 Lobby 목록에서 숨길지 여부 |
| `MaxPlayers` | 최대 플레이어 수 |
| `Password` | 참가 비밀번호. 없으면 `null` 또는 빈 문자열 |
| `PlayerFields` | 생성자 플레이어의 초기 필드 |
| `PlayerNickname` | 생성자 표시 이름 |
| `RoomFields` | 방의 초기 필드 |
| `Title` | 방 제목 |

## `JoinConfigInterface`

| 멤버 | 설명 |
| --- | --- |
| `CancellationToken` | 작업 취소 토큰 |
| `RoomId` | 참가할 방 ID. 값이 있으면 `Code`보다 우선한다. |
| `Code` | RoomId가 없을 때 사용할 참가 코드 |
| `Password` | 방 비밀번호 |
| `PlayerFields` | 참가 플레이어의 초기 필드 |
| `PlayerNickname` | 참가 플레이어 표시 이름 |

## `UpdateConfigInterface`

| 멤버 | 설명 |
| --- | --- |
| `CancellationToken` | 작업 취소 토큰 |
| `RoomId` | 현재 방 ID |
| `IsPrivate` | 변경할 공개 여부 |
| `RoomFields` | key 기준으로 병합할 방 필드 |

방 갱신은 호스트만 할 수 있다. 현재 API는 생성 이후 `IsLocked`, 제목, 비밀번호와 최대 인원을 변경하지 않는다.

## `ExitConfigInterface`

| 멤버 | 설명 |
| --- | --- |
| `CancellationToken` | 작업 취소 토큰 |
| `RoomId` | 현재 방 ID |
| `PlayerId` | 내보낼 플레이어 ID |

호스트가 자기 ID를 지정하면 방을 닫고, 다른 플레이어 ID를 지정하면 강퇴한다. 일반 멤버는 자기 ID로만 나갈 수 있다. Steam 강퇴의 한계와 호스트 이전 정책은 [Steam 플랫폼](../platforms/steam.md)을 참고한다.

요청자는 메서드에 전달한 Result에서 결과를 받고, 다른 구성원은 초기화 때 등록한 `RoomResult`에서 최신 방 스냅샷을 받는다.
