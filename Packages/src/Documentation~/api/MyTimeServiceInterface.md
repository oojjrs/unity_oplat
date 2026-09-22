# `MyTimeServiceInterface`

플랫폼 초기화 때 정한 UTC 기준점에 monotonic 경과 시간을 더해 현재 시각을 제공한다. 운영체제 시계가 실행 중 바뀌어도 `UtcNow`는 그 변경을 따라 점프하지 않는다.

## 멤버

| 멤버 | 설명 |
| --- | --- |
| `MyTime UtcNow` | 현재 UTC 절대 시각 |
| `bool IsSynchronized` | 기준점이 플랫폼 서버 시각에서 왔는지 여부 |
| `TimeSpan LocalClockOffset` | 기준점과 초기화 당시 로컬 UTC의 차이 |

Steam은 `SteamUtils.GetServerRealTime()`을 기준점으로 사용하므로 동기화 상태다. Anonymous와 Ugsymous는 로컬 `DateTime.UtcNow`를 기준점으로 사용하므로 동기화 상태가 아니다. Ugsymous는 시간 하나를 위해 Cloud Code를 필수 의존성으로 추가하지 않는다.

Steam 서버 시각은 초 단위이므로 `UtcNow`가 tick 단위로 전진하더라도 기준점 정확도가 프레임 단위 동기화를 보장하지는 않는다. 더 높은 멀티플레이 정밀도가 필요하면 게임이나 네트워크 계층에서 호스트 왕복 시간 보정을 추가한다.
