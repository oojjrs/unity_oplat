# `MyNetMemberServiceInterface`

멤버가 현재 방의 호스트로 게임 요청을 보낸다.

## 멤버

```csharp
void Send(MyNetRequest request);
```

요청은 전송 큐에 들어가며 호출별 성공 결과를 반환하지 않는다. 대상 지정도 지원하지 않는다. 중요한 메시지는 페이로드에 correlation ID를 넣고 [`MyNetHostServiceInterface`](MyNetHostServiceInterface.md)의 응답으로 애플리케이션 수준 확인 절차를 만든다.

Steam 구현은 다른 스레드에서도 큐에 넣을 수 있지만 Anonymous 구현은 같은 보장을 제공하지 않는다. 플랫폼 공통 코드는 Unity 메인 스레드에서 호출한다.

페이로드 정의와 지원 타입은 [네트워크 페이로드](network-payloads.md)를 참고한다.
