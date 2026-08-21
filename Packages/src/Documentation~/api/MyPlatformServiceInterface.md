# `MyPlatformServiceInterface`

초기화된 계정 정보, 저장소와 네트워크 서비스의 최상위 진입점이다.

## 멤버

| 멤버 | 설명 |
| --- | --- |
| `string Account` | 플랫폼 계정 식별 문자열 |
| `string Nickname` | 표시 이름 |
| `Sprite ProfileSprite` | 프로필 이미지. 가져오지 못하면 `null`이다. |
| `bool IsAlive` | 플랫폼 서비스가 초기화된 상태로 살아 있는지 여부 |
| `bool IsRestartRequired` | Steam이 현재 프로세스의 종료와 재실행을 요청했는지 여부 |
| `MyStorageServiceInterface Storage` | 사용자 파일 저장소 |
| `MyNetInterface Net` | Lobby, 방, 플레이어, 채팅과 게임 메시지 서비스 |

서비스를 장기간 보관하면 접근 전에 `IsAlive`를 확인한다. `ProfileSprite`의 수명은 서비스가 관리하므로 소비자가 직접 파괴하지 않는다.

`Account`는 클라이언트에서 조회한 식별 문자열이며 서버 인증 증명이 아니다. 신뢰 경계에서 사용하려면 해당 플랫폼의 서버 측 인증 절차를 별도로 구현한다.

Steam 재실행 흐름과 플랫폼 차이는 [Steam](../platforms/steam.md)과 [Anonymous](../platforms/anonymous.md)를 참고한다.
