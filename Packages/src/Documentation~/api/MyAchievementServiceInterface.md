# `MyAchievementServiceInterface`

Steam 등록 항목과 대응하는 JSON을 Anonymous·Ugsymous에 등록하며, Steam에서는 App Admin에 게시한 정의를 사용한다.

## 1. 개발용 업적 목록 등록

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
    {
      "id": 0,
      "key": "first_win",
      "displayName": "First Win",
      "description": "Win a game.",
      "setBy": "Client",
      "hidden": false,
      "achievedIcon": "Achievements/first_win",
      "unachievedIcon": "Achievements/first_win_locked",
      "statKey": "wins",
      "targetValue": 1,
      "localizations": [
        { "language": "koreana", "displayName": "첫 승리", "description": "게임에서 승리하세요." }
      ]
    }
  ]
}
```

| Steam 등록 항목 | JSON |
| --- | --- |
| ID | `id` (생략 시 로컬 자동 배정) |
| API Name | `key` |
| Display Name / Description | `displayName` / `description` |
| Set By | `setBy`: `Client`, `GameServer` (`GS`), `OfficialGameServer` (`Official GS`) |
| Hidden | `hidden` |
| Achieved Icon / Unachieved Icon | `achievedIcon` / `unachievedIcon` (게임에서 해석할 이미지 참조) |
| Progress Stat / Unlock Value | `statKey` / `targetValue` (함께 지정 또는 생략) |
| Localization | `localizations`: `language`, `displayName`, `description` |
| 이전 JSON | `title` → `displayName`, `desc` → `description` |

전체 구조는 [`stats.example.json`](../stats.example.json), Steam 등록 기준은 [Valve 업적 안내](https://partner.steamgames.com/doc/features/achievements/ach_guide)를 참조한다.

## 2. 달성 여부와 달성 처리

```csharp
await service.Achievements.UnlockAsync("first_win", cancellationToken);
var isUnlocked = await service.Achievements.IsUnlockedAsync("first_win", cancellationToken);
var definition = await service.Achievements.GetDefinitionAsync("first_win", "koreana", cancellationToken);
var visible = await service.Achievements.GetDefinitionsAsync("koreana", false, cancellationToken);
var iconReference = isUnlocked ? definition.AchievedIcon : definition.UnachievedIcon;
```

로컬 목록 조회는 미달성 Hidden 업적을 제외하며 `includeHidden = true`로 포함하고, 언어는 요청값·`english`·기본 문자열 순으로 선택한다; Steam 표시 문자열은 Steam의 현재 게임 언어를 따른다.

## 3. 통계 기반 진행도

```csharp
await service.Stats.AddAsync("wins", 1, cancellationToken);
var wins = await service.Stats.GetIntAsync("wins", cancellationToken);
var isUnlocked = await service.Achievements.IsUnlockedAsync("first_win", cancellationToken);
```

로컬은 등록·통계 갱신 때 목표값을 검사해 자동 해제하며, Steam의 Progress Stat·Unlock Value는 App Admin에도 동일하게 등록해야 한다.

## 4. 초기화

```csharp
await service.Achievements.ResetAsync("first_win", cancellationToken);
await service.Achievements.ResetAsync(cancellationToken);
await service.ResetAllProgressAsync(cancellationToken);
```

개발용 초기화는 연결 통계를 유지한 채 업적을 잠그며 전체 진행도 초기화는 통계도 기본값으로 돌린다; 로컬은 Set By 권한을 먼저 검사하고 통계·업적 저장은 파일 간 트랜잭션이 아니다.
