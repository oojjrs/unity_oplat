# `MyTime`

UTC 절대 시각을 100나노초 단위 tick으로 보관하는 불변 값 타입이다. `Stopwatch` timestamp와 로컬 시각은 저장하지 않는다.

## 생성

| 메서드 | 설명 |
| --- | --- |
| `FromUtcTicks(long)` | UTC tick에서 만든다. `DateTime` 범위를 벗어나면 예외를 던진다. |
| `FromUtcDateTime(DateTime)` | `DateTimeKind.Utc` 값에서 만든다. Local 또는 Unspecified 값은 거부한다. |
| `FromDateTimeOffset(DateTimeOffset)` | UTC로 정규화해 만든다. |
| `FromUnixTimeSeconds(long)` | Unix epoch 초에서 만든다. |
| `FromUnixTimeMilliseconds(long)` | Unix epoch 밀리초에서 만든다. |
| `TryParse(string, out MyTime)` | invariant UTC tick 문자열을 읽는다. |

`default(MyTime)`의 `UtcTicks`는 0이며 `IsDefined`가 `false`다. 미설정 시각으로 사용할 수 있다.

## 변환

| 멤버 | 설명 |
| --- | --- |
| `UtcTicks` | 저장된 UTC tick |
| `ToUtcDateTime()` | UTC `DateTime` 변환 |
| `ToLocalDateTime()` | 현재 운영체제 시간대의 표시용 `DateTime` 변환 |
| `ToDateTimeOffset()` | UTC `DateTimeOffset` 변환 |
| `ToUnixTimeSeconds()` | Unix epoch 초 변환 |
| `ToUnixTimeMilliseconds()` | Unix epoch 밀리초 변환 |
| `ToString()` | invariant UTC tick 문자열 |

`MyTime`끼리 비교할 수 있고 `TimeSpan`을 더하거나 뺄 수 있다. 두 `MyTime`의 차이는 `TimeSpan`이다. 범위를 벗어나는 연산은 예외를 던진다. `DateTime`과의 암시적 변환은 제공하지 않는다.
