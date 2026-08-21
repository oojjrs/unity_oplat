# `MyNetPlayerInterface`

특정 시점의 플레이어 상태를 나타내는 읽기 전용 스냅샷이다.

## 멤버

| 멤버 | 설명 |
| --- | --- |
| `string Id` | 플랫폼 플레이어 ID |
| `string Nickname` | 방에서 사용하는 표시 이름 |
| `bool IsHost` | 현재 방 호스트 여부 |
| `GetData(string key)` | 현재 호출자에게 공개된 플레이어 필드 값을 조회한다. 없으면 `null`이다. |

플레이어가 변경되면 최신 결과 처리기가 전달한 스냅샷으로 교체해 사용한다.
