# `MyNetHostResultInterface`

멤버가 보낸 게임 요청을 호스트 측에서 처리한다.

## 멤버

| 멤버 | 설명 |
| --- | --- |
| `OnReceived(MyNetRequest request)` | 큐에서 꺼낸 요청 하나를 처리한다. |
| `OnFinishThisHandling()` | 현재 프레임 또는 처리 주기의 요청 큐를 모두 전달한 뒤 한 번 호출된다. |

이 처리기는 `MyPlatformInitializer.CallbackInterface.HostResult`로 제공하며 플랫폼 서비스가 살아 있는 동안 유효해야 한다.
