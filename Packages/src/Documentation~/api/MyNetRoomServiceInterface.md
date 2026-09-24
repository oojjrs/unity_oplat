# `MyNetRoomServiceInterface`

방을 생성하고 참가하며, 방 정보와 구성원을 변경한다.

## 메서드

| 메서드 | 성공 결과 |
| --- | --- |
| `CreateAsync(CreateConfigInterface, CreateResultInterface)` | 생성하고 참가한 `MyNetRoomInterface` |
| `JoinAsync(JoinConfigInterface, JoinResultInterface)` | 참가한 `MyNetRoomInterface` |
| `SwitchAsync(JoinConfigInterface, JoinResultInterface)` | 전환 후 참가한 `MyNetRoomInterface` |
| `UpdateAsync(UpdateConfigInterface, UpdateResultInterface)` | 갱신된 `MyNetRoomInterface` |
| `ExitAsync(ExitConfigInterface, ExitResultInterface)` | 처리한 `roomId`, `playerId` |

각 Result 인터페이스는 `MyNetInterface.CatchInterface`를 상속한다.

## `CreateConfigInterface`

| 멤버 | 설명 |
| --- | --- |
| `CancellationToken` | 작업 취소 토큰 |
| `IsLocked` | 생성 직후 참가를 잠글지 여부 |
| `MaxPlayers` | 최대 플레이어 수 |
| `Password` | 참가 비밀번호. 없으면 `null` 또는 빈 문자열 |
| `PlayerFields` | 생성자 플레이어의 초기 필드 |
| `PlayerNickname` | 생성자 표시 이름 |
| `RoomFields` | 방의 초기 필드 |
| `Title` | 방 제목 |
| `Visibility` | `Public`, `FriendsOnly`, `Private` 중 생성할 방의 공개 범위 |

## `JoinConfigInterface`

| 멤버 | 설명 |
| --- | --- |
| `CancellationToken` | 작업 취소 토큰 |
| `RoomId` | 참가할 방 ID. 값이 있으면 `Code`보다 우선한다. |
| `Code` | RoomId가 없을 때 사용할 참가 코드 |
| `Password` | 방 비밀번호 |
| `PlayerFields` | 참가 플레이어의 초기 필드 |
| `PlayerNickname` | 참가 플레이어 표시 이름 |

`JoinAsync`는 기존과 같이 현재 방을 정리하지 않고 대상 방 참가만 시도한다. 이미 다른 방에 참가한 상태의 처리 방식도 기존 플랫폼 구현을 유지한다.

`SwitchAsync`는 같은 `JoinConfigInterface`와 `JoinResultInterface`를 사용하는 상위 전환 API다. 대상이 현재 방이면 현재 방 스냅샷으로 즉시 성공한다. 현재 방이 없으면 대상 방에 참가한다. 다른 방에 참가 중이면 로컬 플레이어 ID로 `ExitAsync`를 먼저 수행한 뒤 대상 방에 참가한다. 이때 현재 플레이어가 방장이면 기존 `ExitAsync` 계약에 따라 방을 닫고, 일반 멤버이면 방을 나간다.

전환 중 다른 `SwitchAsync` 또는 플랫폼 참여 요청이 들어오면 새 요청은 `OnBusy`로 끝난다. 퇴장에 실패하면 참가를 시도하지 않고 해당 실패를 그대로 반환한다. 퇴장은 성공했지만 새 방 참가가 실패하면 플레이어는 방이 없는 상태가 되며 참가 실패를 반환한다. 취소는 기존 작업과 같이 `OperationCanceledException`으로 완료한다.

기존 외부 구현의 소스 호환성을 위해 인터페이스 기본 구현은 `JoinAsync`로 연결된다. 패키지에 포함된 Anonymous, Steam, Ugsymous 구현은 모두 위의 전체 전환 동작을 제공한다.

## `UpdateConfigInterface`

| 멤버 | 설명 |
| --- | --- |
| `CancellationToken` | 작업 취소 토큰 |
| `RoomId` | 현재 방 ID |
| `RoomFields` | key 기준으로 병합할 방 필드 |
| `Visibility` | 변경할 방의 공개 범위 |

방 갱신은 호스트만 할 수 있다. 현재 API는 생성 이후 `IsLocked`, 제목, 비밀번호와 최대 인원을 변경하지 않는다. 공개 범위별 목록·친구 노출 의미는 [`MyNetRoomInterface.VisibilityEnum`](MyNetRoomInterface.md#visibilityenum)을 참고한다.

## `ExitConfigInterface`

| 멤버 | 설명 |
| --- | --- |
| `CancellationToken` | 작업 취소 토큰 |
| `RoomId` | 현재 방 ID |
| `PlayerId` | 내보낼 플레이어 ID |

호스트가 자기 ID를 지정하면 방을 닫고, 다른 플레이어 ID를 지정하면 강퇴한다. 일반 멤버는 자기 ID로만 나갈 수 있다. Steam 강퇴의 한계와 호스트 이전 정책은 [Steam 플랫폼](../platforms/steam.md)을 참고한다.

요청자는 메서드에 전달한 Result에서 결과를 받고, 다른 구성원은 초기화 때 선택적으로 등록한 `RoomResult`에서 최신 방 스냅샷을 받는다. 방장이 방을 닫거나 현재 플레이어가 강퇴되어 방에서 나가면 `RoomResult.OnFailed(NotFoundRoom)`을 받는다. 생략하면 이 알림을 버린다.
