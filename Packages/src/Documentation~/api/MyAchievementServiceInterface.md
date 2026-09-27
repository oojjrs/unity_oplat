# `MyAchievementServiceInterface`

Steam 업적의 등록 확인, 달성 여부 조회, 달성 및 초기화를 Anonymous와 Ugsymous에서 같은 호출로 다룬다.

## 1. 개발용 업적 목록 등록

치트 기능이 활성화된 개발 빌드에서 [`stats.example.json`](../stats.example.json)과 같은 공통 진행도 목록을 통계와 업적 서비스에 전달한다.

```csharp
var definitionsJson = File.ReadAllText(path);
await service.Stats.EnsureAsync(definitionsJson, cancellationToken);
await service.Achievements.EnsureAsync(definitionsJson, cancellationToken);
```

```json
{
  "stats": [
    { "key": "wins", "type": "INT", "defaultValue": 0 }
  ],
  "achievements": [
    { "key": "first_win", "statKey": "wins", "targetValue": 1 },
    { "key": "win_10", "statKey": "wins", "targetValue": 10 },
    { "key": "find_secret" }
  ]
}
```

- `key`는 플랫폼 업적 API Name이다.
- `statKey`와 `targetValue`는 함께 생략하거나 함께 지정한다.
- `statKey`는 같은 파일의 통계 키를 참조해야 하고, `targetValue`는 해당 통계 타입으로 표현할 수 있어야 한다.
- Anonymous와 Ugsymous는 처음 보는 업적을 등록하고, 기존 정의가 다르면 오류를 낸다.
- Steam은 App Admin에 저장·게시된 API Name을 확인한다. 런타임 API로 Progress Stat과 Unlock Value 설정값은 확인할 수 없으므로 JSON과 App Admin 값은 개발자가 같게 유지한다.

## 2. 달성 여부와 달성 처리

```csharp
var isUnlocked = await service.Achievements.IsUnlockedAsync("win_10", cancellationToken);
if (isUnlocked == false)
    await service.Achievements.UnlockAsync("win_10", cancellationToken);
```

`UnlockAsync`는 이미 달성한 업적에 다시 호출해도 성공한다. Anonymous와 Ugsymous는 등록하지 않은 키를 오류로 처리한다. Steam은 App Admin에 없거나 게시되지 않은 키를 오류로 처리한다.

## 3. 통계 기반 진행도

업적 서비스는 별도의 진행도 값을 저장하지 않는다. 현재 통계와 게임이 보관한 목표값을 사용한다.

```csharp
var wins = await service.Stats.GetIntAsync("wins", cancellationToken);
var progress = $"{wins} / 10";
if (wins >= 10)
    await service.Achievements.UnlockAsync("win_10", cancellationToken);
```

통계 갱신은 업적을 자동으로 해제하지 않는다. 게임이 달성 시점을 판정해 모든 플랫폼에서 같은 `UnlockAsync`를 호출한다.

## 4. 초기화

```csharp
await service.Achievements.ResetAsync("win_10", cancellationToken);
await service.Achievements.ResetAsync(cancellationToken);
await service.ResetAllProgressAsync(cancellationToken);
```

- 키 초기화는 지정한 업적만 잠그고 연결 통계는 유지한다.
- 업적 전체 초기화는 등록된 업적만 잠그고 통계는 유지한다. Steam에서는 같은 실행에서 `EnsureAsync`로 확인한 목록을 사용한다.
- `ResetAllProgressAsync`는 모든 통계를 기본값으로 되돌리고 모든 업적을 잠근다.
- Steam은 `ResetAllStats(true)` 뒤 현재 통계와 업적을 다시 동기화한다.
- Anonymous와 Ugsymous는 분리된 통계·업적 파일을 차례로 저장하므로 전체 진행도 초기화는 파일 간 트랜잭션이 아니다. 중간 실패 시 오류를 처리하고 다시 호출한다.

Steam의 `ResetAllStats`는 개발 테스트용 API다. `ResetAllProgressAsync`도 치트 또는 명시적인 개발 도구 흐름에서만 호출한다.
