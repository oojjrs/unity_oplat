# `MyNetLobbyServiceInterface`

참가 가능한 공개 방 목록을 한 번 조회하거나 일정 간격으로 반복 조회한다.

## 메서드

| 메서드 | 설명 |
| --- | --- |
| `RefreshAsync(ResultInterface result)` | 방 목록을 한 번 조회한다. 호출자 CancellationToken 인자는 없다. |
| `StartAsync(ConfigInterface config, ResultInterface result)` | 즉시 한 번 조회한 뒤 polling을 시작한다. |
| `Stop()` | 현재 polling을 중지한다. |

Polling 간격은 최소 1초이며 방에 참가한 동안 자동 조회가 멈춘다.

## `ConfigInterface`

| 멤버 | 설명 |
| --- | --- |
| `CancellationToken CancellationToken` | 첫 조회와 이후 polling의 취소 토큰 |
| `int PollingDelaySeconds` | 반복 조회 간격. 1보다 작으면 1초로 처리한다. |

## `ResultInterface`

`MyNetInterface.CatchInterface`를 구현하고 `OnOk(IEnumerable<MyNetRoomInterface> rooms)`에서 방 스냅샷 목록을 받는다. 방이 없으면 빈 목록이다.

Private 방은 일반 목록에서 제외된다. Steam은 한 번에 최대 50개를 반환하며 잠겼거나 정원이 찬 방도 검색 결과에서 제외할 수 있다. 자세한 내용은 [Steam 플랫폼](../platforms/steam.md)을 참고한다.
