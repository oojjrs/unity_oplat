# `MyNetRoomInterface`

특정 시점의 방 상태를 나타내는 읽기 전용 스냅샷이다.

## 멤버

| 멤버 | 설명 |
| --- | --- |
| `string Id` | 플랫폼 방 ID |
| `string Code` | 사용자 입력용 참가 코드 |
| `string Title` | 방 제목 |
| `string HostId` | 호스트 플레이어 ID |
| `MyNetPlayerInterface Host` | 호스트 플레이어 스냅샷 |
| `IEnumerable<MyNetPlayerInterface> Players` | 공개 범위가 적용된 플레이어 스냅샷 |
| `int PlayerCount` | 현재 플레이어 수 |
| `int PlayerCountAvailable` | 남은 자리 수 |
| `int PlayerCountMax` | 최대 플레이어 수 |
| `bool HasPassword` | 비밀번호 설정 여부 |
| `bool IsLocked` | 참가 잠금 여부 |
| `bool IsPrivate` | 일반 Lobby 목록 비공개 여부 |
| `GetData(string key)` | 현재 호출자에게 공개된 방 필드 값을 조회한다. 없으면 `null`이다. |

방이나 플레이어가 변경되면 기존 객체를 갱신하는 대신 새 스냅샷이 결과 처리기로 전달될 수 있으므로 최신 결과로 교체해 사용한다.
