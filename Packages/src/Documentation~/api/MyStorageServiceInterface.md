# `MyStorageServiceInterface`

플랫폼별 사용자 저장소를 파일 단위 raw byte API로 제공한다.

## 사용

```csharp
await service.Storage.WriteAsync("save/player.dat", data, cancellationToken);

var read = await service.Storage.ReadAsync("save/player.dat", cancellationToken);
if (read.IsFound)
{
    var loadedData = read.Data;
}
```

직렬화 형식, 버전 관리와 재시도 정책은 소비 프로젝트가 정한다.

## 멤버

| 멤버 | 설명 |
| --- | --- |
| `int FileByteCountMax` | 파일 하나의 최대 크기. 현재 100 MiB다. |
| `WriteAsync(fileName, data, token)` | 새 파일을 쓰거나 기존 파일 전체를 덮어쓴다. 빈 배열은 0-byte 파일로 저장한다. |
| `ReadAsync(fileName, token)` | 파일 존재 여부와 전체 데이터를 반환한다. |
| `ExistsAsync(fileName, token)` | 파일 존재 여부를 반환한다. |
| `DeleteAsync(fileName, token)` | 존재하던 파일을 삭제했으면 `true`를 반환한다. |
| `ListAsync(token)` | 현재 파일의 읽기 전용 스냅샷을 반환한다. |

## 결과 타입

`ReadResult.IsFound`가 `false`이면 파일이 없고 `Data`는 빈 배열이다. `IsFound`가 `true`이면서 `Data.Length == 0`이면 존재하는 0-byte 파일이다.

`FileInfo`는 `FileName`, `SizeBytes`, `LastWriteTimeUtc`를 제공한다. 수정 시각은 UTC지만 해상도는 플랫폼에 따라 다를 수 있다.

## 파일명 규칙

- UTF-8 기준 최대 259 bytes다.
- `/`를 구분자로 사용하는 상대 경로여야 한다.
- 절대 경로, `\`, 빈 구간, `.`과 `..`, 끝의 공백·마침표, 지원 플랫폼에서 금지된 문자와 예약 이름은 사용할 수 없다.
- `data`는 `null`일 수 없고 `FileByteCountMax`를 넘을 수 없다.

저장 위치, Cloud 동기화와 스레드 제약은 [Anonymous](../platforms/anonymous.md)와 [Steam](../platforms/steam.md)을 참고한다.
