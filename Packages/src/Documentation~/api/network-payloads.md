# 네트워크 페이로드

`MyNetRequest`와 `MyNetResponse`를 상속한 구체 타입으로 게임 요청과 응답을 정의한다.

```csharp
public sealed class MoveRequest : MyNetRequest
{
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class MoveResponse : MyNetResponse
{
    public bool Accepted { get; set; }
}
```

```csharp
service.Net.Member.Send(new MoveRequest { X = 3, Y = 5 });
service.Net.Host.Send(new MoveResponse { Accepted = true });
```

## 직렬화 규칙

`MyNetSerializer`와 `MyNetDeserializer`는 다음 데이터를 지원한다.

- public instance getter와 setter가 모두 있는 속성
- `string`, `float`, `long`, `int`, `short`, `byte`, `bool`, `double`, `char`, `DateTime`, `TimeSpan`, enum
- 위 타입의 배열, 중첩 객체와 `ValueTuple`

`TimeSpan`은 ticks를 부호 있는 64비트 정수로 기록하여 정밀도와 음수 값을 보존한다. `TimeSpan`을 포함하는 페이로드는 송수신 양쪽에 해당 타입을 지원하는 직렬화기가 필요하다.

속성은 이름순으로 기록되고 field는 무시된다. 객체는 매개변수 없는 생성자로 만들 수 있어야 하며 송수신 양쪽에 같은 assembly-qualified 타입이 로드되어야 한다. 타입명이나 속성 구조를 바꾸면 기존 wire data와 호환되지 않을 수 있다.

`uint`, `ulong`, 컬렉션 등 목록에 없는 타입은 지원된다고 가정하지 않는다. `MyNetDeserializer`는 패키지 페이로드용이며 임의의 신뢰하지 않는 Stream을 처리하는 범용 역직렬화기로 사용하지 않는다.

직렬화 또는 플랫폼 메시지 크기 제한을 넘은 요청·응답은 전달되지 않을 수 있다. `Send`에는 개별 결과 콜백이 없으므로 중요한 프로토콜은 correlation ID, 응답과 timeout을 직접 정의한다.

Steam과 Anonymous는 원격 요청·응답을 `MyNetSerializer`로 기록하고 `MyNetDeserializer`로 복원한다. `Send`는 패킷 객체를 큐에 적재하며 원격 전송 시 직렬화한다. 호스트 자신의 수신과 `UseLocal`은 직렬화 없이 원본 객체를 전달한다. 전송과 처리가 끝날 때까지 적재한 객체를 변경하지 않는다. Steam의 방·호스트 상태 불일치와 송신 큐 초과는 `Send`에서 동기 예외로 전달한다.

Result를 받는 비동기 네트워크 작업의 예외는 해당 Result 인터페이스의 `OnException(MyNetSessionException)`으로 전달된다.
