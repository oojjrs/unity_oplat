# `MyNetHostResultInterface`

멤버가 보낸 게임 요청을 호스트 측에서 처리한다.

## 멤버

| 멤버 | 설명 |
| --- | --- |
| `OnReceived(MyNetRequest request)` | 큐에서 꺼낸 요청 하나를 처리한다. |
| `OnFinishThisHandling()` | 현재 프레임 또는 처리 주기의 요청 큐를 모두 전달한 뒤 한 번 호출된다. |

멤버 요청을 처리하려면 이 처리기를 `MyPlatformInitializer.CallbackInterface.HostResult`로 제공한다. 생략하면 수신 요청을 버린다.
