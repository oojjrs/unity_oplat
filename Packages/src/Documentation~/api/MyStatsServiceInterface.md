# `MyStatsServiceInterface`

Steam 등록 항목과 대응하는 JSON을 Anonymous 로컬 서버·Ugsymous에 등록하며, Steam에서는 App Admin에 게시한 정의와 제약을 사용한다.

## 1. 개발용 통계 목록 등록

```csharp
var definitionsJson = File.ReadAllText(path);
await service.Stats.EnsureAsync(definitionsJson, cancellationToken);
await service.Stats.EnsureAsync("wins", 0, cancellationToken);
await service.Stats.EnsureAsync("distance", 0f, cancellationToken);
await service.Stats.EnsureAverageRateAsync("points_per_second", 0f, 72000d, cancellationToken);
```

```json
{
  "stats": [
    {
      "id": 0,
      "key": "wins",
      "type": "INT",
      "displayName": "Wins",
      "setBy": "Client",
      "incrementOnly": true,
      "maxChange": 1,
      "minValue": 0,
      "maxValue": 1000000,
      "defaultValue": 0,
      "aggregated": true,
      "localizations": [
        { "language": "koreana", "displayName": "승리 횟수" }
      ]
    },
    { "key": "distance", "type": "FLOAT", "defaultValue": 0.0 },
    { "key": "points_per_hour", "type": "AVGRATE", "defaultValue": 0.0, "window": 20.0, "secondsPerUnit": 3600.0 }
  ]
}
```

| Steam 등록 항목 | JSON |
| --- | --- |
| ID | `id` (생략 시 로컬 자동 배정) |
| API Name / Type | `key` / `type`: `INT`, `FLOAT`, `AVGRATE` |
| Display Name | `displayName` |
| Set By | `setBy`: `Client`, `GameServer` (`GS`), `OfficialGameServer` (`Official GS`) |
| Increment Only / Max Change | `incrementOnly` / `maxChange` |
| Min Value / Max Value / Default Value | `minValue` / `maxValue` / `defaultValue` (기본 0) |
| Aggregated | `aggregated` |
| Window | `window` (AVGRATE만 사용) |
| Window 단위 변환 | `secondsPerUnit` (기본 1, 시간당 값은 3600) |
| Localization | `localizations`: `language`, `displayName` |
| 이전 JSON | `windowSeconds` → `window` + `secondsPerUnit = 1` |

전체 구조는 [`stats.example.json`](../stats.example.json), 등록 기준은 [Valve 통계 안내](https://partner.steamgames.com/doc/features/achievements)를 참조하며 로컬의 기존 타입·기본값·Window 단위와 업적 목표는 변경할 수 없다.

## 2. 통계 갱신

```csharp
await service.Stats.SetAsync("distance", 12.5f, cancellationToken);
await service.Stats.AddAsync("wins", 1, cancellationToken);
await service.Stats.UpdateAverageRateAsync("points_per_hour", 77f, 810d, cancellationToken);

var server = oojjrs.oplat.anonymous.MyAnonymousProgressServer.GetServices(appId, account, MyProgressDefinitions.WriterEnum.GameServer);
await server.Stats.AddAsync("server_wins", 1, cancellationToken);
var officialServer = oojjrs.oplat.anonymous.MyAnonymousProgressServer.GetServices(appId, account, MyProgressDefinitions.WriterEnum.OfficialGameServer);
await officialServer.Stats.AddAsync("official_wins", 1, cancellationToken);
```

로컬은 Set By·증가 전용·변경량·범위를 검사하고 위반 시 값을 유지하며, 서버 작성자는 신뢰하는 서버 프로세스에서 사용한다; AVGRATE 호출 길이는 초 단위이며 `secondsPerUnit`으로 Window 단위에 변환한다.

## 3. 현재 통계 조회

```csharp
var wins = await service.Stats.GetIntAsync("wins", cancellationToken);
var distance = await service.Stats.GetFloatAsync("distance", cancellationToken);
var pointsPerHour = await service.Stats.GetAverageRateAsync("points_per_hour", cancellationToken);
var definition = await service.Stats.GetDefinitionAsync("wins", "koreana", cancellationToken);
var definitions = await service.Stats.GetDefinitionsAsync("koreana", cancellationToken);
var totalWins = await service.Stats.GetGlobalIntAsync("wins", cancellationToken);
var totalDistance = await service.Stats.GetGlobalFloatAsync("distance", cancellationToken);
```

Aggregated 조회는 Anonymous의 같은 AppID에 저장된 계정 값을 합산하고 Steam은 `RequestGlobalStats`·`GetGlobalStat`을 사용하며, Ugsymous는 전역 집계 백엔드가 없어 지원하지 않는다.

## 4. 통계 초기화

```csharp
await service.Stats.ResetAsync(cancellationToken);
await service.Stats.ResetAsync("wins", cancellationToken);
await service.ResetAllProgressAsync(cancellationToken);
```

개발용 초기화는 기본값으로 돌리며 로컬은 Set By 권한을 먼저 검사한다; Steam에서 단일 키 초기화는 같은 실행에서 등록 확인한 INT·FLOAT만 지원하고 AVGRATE는 전체 초기화를 사용한다.
