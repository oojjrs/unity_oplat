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

1. GameObject에 `MyPlatformInitializer`를 추가한다.
2. 같은 GameObject의 컴포넌트에서 `MyPlatformInitializer.CallbackInterface`를 구현한다.
3. `AppId`, `InitialType`, `AnonymousInstanceId`와 Chat·Host·Member·Player·Room 결과 처리기를 제공한다.
4. `OnResult(MyPlatformServiceInterface service)`에서 초기화된 서비스를 받는다.

플레이어 빌드에서 `service.IsRestartRequired`가 `true`이면 Steam 재실행을 위해 현재 프로세스를 즉시 종료한다.

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
