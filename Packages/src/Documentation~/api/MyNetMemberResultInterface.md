# `MyNetMemberResultInterface`

호스트가 보낸 게임 응답을 멤버 측에서 처리한다.

## 멤버

| 멤버 | 설명 |
| --- | --- |
| `OnReceived(MyNetResponse response)` | 큐에서 꺼낸 응답 하나를 처리한다. |
| `OnFinishThisHandling()` | 현재 프레임 또는 처리 주기의 응답 큐를 모두 전달한 뒤 한 번 호출된다. |

이 처리기는 `MyPlatformInitializer.CallbackInterface.MemberResult`로 제공하며 플랫폼 서비스가 살아 있는 동안 유효해야 한다.
