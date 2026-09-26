# `MyStatsServiceInterface`

Steam `INT`·`FLOAT`·`AVGRATE` 통계를 Anonymous 로컬 서버와 Ugsymous에서도 같은 호출 순서로 다룬다.

## 1. 개발용 통계 목록 등록

치트 기능이 활성화된 개발 빌드에서 [`stats.example.json`](../stats.example.json)과 같은 목록 파일을 읽어 전달한다.

```csharp
var definitionsJson = File.ReadAllText(path);
await service.Stats.EnsureAsync(definitionsJson, cancellationToken);
```

```json
{
  "stats": [
    { "key": "wins", "type": "INT", "defaultValue": 0 },
    { "key": "distance", "type": "FLOAT", "defaultValue": 0.0 },
    { "key": "points_per_hour", "type": "AVGRATE", "defaultValue": 0.0, "windowSeconds": 72000.0 }
  ]
}
```

- `type`은 `INT`, `FLOAT`, `AVGRATE` 중 하나다.
- `defaultValue`를 생략하면 0이다.
- `windowSeconds`는 `AVGRATE`에만 필요하며 0보다 커야 한다.
- 키는 파일 안에서 중복될 수 없다.

Anonymous와 Ugsymous는 처음 보는 항목을 등록하고, 이미 등록된 항목의 타입·기본값·Window가 다르면 오류를 낸다. Steam은 App Admin에 저장·게시한 API Name을 조회한다. Steam 클라이언트 API는 `FLOAT`와 `AVGRATE`의 설정 타입을 비파괴 조회로 구분하지 못하므로, AVGRATE 타입 불일치는 첫 `UpdateAverageRateAsync`에서 확인된다.

개별 등록이 필요하면 같은 정의를 직접 전달한다.

```csharp
await service.Stats.EnsureAsync("wins", 0, cancellationToken);
await service.Stats.EnsureAsync("distance", 0f, cancellationToken);
await service.Stats.EnsureAverageRateAsync("points_per_hour", 0f, 72000d, cancellationToken);
```

## 2. 통계 갱신

```csharp
await service.Stats.AddAsync("wins", 1, cancellationToken);
await service.Stats.AddAsync("distance", 12.5f, cancellationToken);
await service.Stats.UpdateAverageRateAsync("points_per_hour", 77f, 810d, cancellationToken);
```

`UpdateAverageRateAsync`의 `count`는 직전 호출 이후 누적량이고 `sessionLengthSeconds`는 같은 구간의 초 단위 길이다. Anonymous와 Ugsymous에서 등록되지 않은 키 또는 다른 타입의 키를 갱신하면 오류가 난다. Steam도 App Admin에 없거나 게시되지 않은 키 또는 다른 타입이면 오류가 난다.

## 3. 통계 초기화

```csharp
await service.Stats.ResetAsync(cancellationToken);
await service.Stats.ResetAsync("wins", cancellationToken);
```

전체 초기화는 등록된 기본값으로 되돌린다. 키 초기화는 해당 키만 기본값으로 되돌린다. Steam의 키 초기화는 같은 실행에서 먼저 `Ensure`한 `INT`·`FLOAT`에만 지원하며, Steam이 단일 `AVGRATE` 초기화 API를 제공하지 않으므로 `AVGRATE`는 전체 초기화를 사용해야 한다.
