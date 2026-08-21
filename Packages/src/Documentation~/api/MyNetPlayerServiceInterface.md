# `MyNetPlayerServiceInterface`

현재 방의 로컬 플레이어 필드를 갱신한다.

## `UpdateAsync`

`UpdateAsync(UpdateConfigInterface config, UpdateResultInterface result)`를 호출한다. 성공하면 `result.OnOk(MyNetPlayerInterface player)`로 갱신된 플레이어 스냅샷을 받는다.

`UpdateResultInterface`는 `MyNetInterface.CatchInterface`를 상속한다.

## `UpdateConfigInterface`

| 멤버 | 설명 |
| --- | --- |
| `CancellationToken CancellationToken` | 작업 취소 토큰 |
| `string RoomId` | 현재 방 ID |
| `string PlayerId` | 갱신할 로컬 플레이어 ID |
| `IEnumerable<MyNetInterface.Field> PlayerFields` | key 기준으로 병합할 플레이어 필드 |

다른 플레이어의 ID는 갱신할 수 없다. 요청자는 전달한 Result에서 결과를 받고, 다른 방 구성원은 초기화 때 선택적으로 등록한 `PlayerResult`에서 최신 플레이어 스냅샷을 받는다. 생략하면 이 알림을 버린다.
