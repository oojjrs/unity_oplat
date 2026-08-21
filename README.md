# UnityOplat

Unity 프로젝트에서 `Anonymous`·`Steam` 플랫폼 초기화, 사용자 파일 저장소, Lobby/P2P 네트워크를 하나의 서비스로 제공하는 Unity 6 패키지다.

## 설치

Unity `6000.0` 이상 프로젝트의 `Packages/manifest.json`에 다음 의존성을 추가한다.

```json
{
  "dependencies": {
    "com.oojjrs.oplat": "https://github.com/oojjrs/unity_oplat.git?path=/Packages/src",
    "com.rlabrecque.steamworks.net": "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#ba71581f1ed7349e8d0f17ddc6f135dd3bc8a6a3"
  }
}
```

## 초기화

아래 내용을 `PlatformBootstrap.cs`로 저장하고 GameObject에 추가한다. `MyPlatformInitializer`는 자동으로 함께 추가된다.

```csharp
using oojjrs.oplat;
using UnityEngine;

[RequireComponent(typeof(MyPlatformInitializer))]
public sealed class PlatformBootstrap : MonoBehaviour, MyPlatformInitializer.CallbackInterface
{
    public MyPlatformServiceInterface Platform { get; private set; }

    uint MyPlatformInitializer.CallbackInterface.AppId => 1;
    MyPlatformTypeEnum MyPlatformInitializer.CallbackInterface.InitialType => MyPlatformTypeEnum.Anonymous;

    void MyPlatformInitializer.CallbackInterface.OnResult(MyPlatformServiceInterface service)
    {
        Platform = service;
    }
}
```

전체 콜백 구성은 [`MyPlatformInitializer.CallbackInterface`](Packages/src/Documentation~/api/MyPlatformInitializer.CallbackInterface.md)를 참고한다.

## 주요 기능

| 기능 | 사용 |
| --- | --- |
| 계정과 프로필 | `service.Account`, `service.Nickname`, `service.ProfileSprite` |
| 파일 저장소 | `service.Storage.WriteAsync`, `ReadAsync`, `ListAsync`, `ExistsAsync`, `DeleteAsync` |
| Lobby와 방 | `service.Net.Lobby`로 목록 조회, `service.Net.Room`으로 생성·참가·수정·퇴장 |
| 플레이어와 채팅 | `service.Net.Player`로 플레이어 갱신, `service.Net.Chat`으로 채팅 참가·전송·퇴장 |
| 게임 메시지 | `service.Net.Member.Send(request)`로 요청, `service.Net.Host.Send(response)`로 응답 |
| 로컬 전송 | `service.Net.UseLocal = true`로 현재 프로세스의 결과 처리기에 직접 전달 |

인터페이스별 설정값, 결과 처리, 플랫폼 차이와 제약은 [패키지 문서](Packages/src/Documentation~/index.md)를 참고한다.
