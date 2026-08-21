# Steam 플랫폼

`Steam`은 Steamworks.NET을 통해 계정, Remote Storage, Lobby와 reliable P2P 메시지를 제공한다.

## 초기화

- Steam 구현은 소비 프로젝트에 `STEAMWORKS_NET`이 정의된 경우에만 컴파일된다. Steamworks.NET 기본 설정은 Standalone 대상에 이 심볼을 추가한다.
- 플레이어 빌드는 `SteamAPI.RestartAppIfNecessary`를 먼저 호출한다. `IsRestartRequired`가 `true`이면 다른 서비스에 접근하지 말고 현재 프로세스를 즉시 종료한다.
- 재실행이 필요하지 않으면 `SteamAPI.InitEx`를 실행하고 실제 App ID가 `CallbackInterface.AppId`와 같은지 확인한다. App ID는 0보다 큰 게임 App ID여야 하며 Depot ID가 아니다.
- Unity Editor에서는 Steam 클라이언트를 먼저 실행하고 현재 작업 디렉터리의 `steam_appid.txt`에 같은 App ID를 둔다. 이 개발용 파일은 배포 빌드에 포함하지 않는다.
- Play Mode 중 스크립트를 다시 컴파일했다면 Play Mode를 재시작한다. Steam 실행 중 표시는 Unity Editor를 종료할 때까지 남을 수 있다.

초기화된 플랫폼 오브젝트가 매 프레임 Steam 콜백을 처리하며, 오브젝트가 파괴될 때 Steam API를 종료한다.

## Steam Cloud

Steamworks App Admin에서 사용자별 byte quota와 file count를 설정하고 Cloud 설정을 저장·게시해야 한다. Storage Task 완료는 현재 프로세스의 `ISteamRemoteStorage` 작업 완료를 뜻하며, 기기 간 업로드·다운로드는 Steam 클라이언트의 후속 동기화가 담당한다.

모든 Storage 작업은 Unity 메인 스레드에서 시작해야 한다. 취소는 백엔드 전달 전까지만 즉시 적용되며, 전달된 작업은 실제 결과까지 기다려 성공을 취소로 오인하거나 변경을 롤백하지 않는다.

## Lobby와 P2P

- 방은 Steam Lobby, 게임 요청과 응답은 `ISteamNetworkingMessages`의 reliable P2P 메시지로 처리한다.
- 방 `Id`는 Lobby SteamID의 10진수 문자열이고 `Code`는 같은 값을 표현한 13자리 Base32 문자열이다.
- `IsPrivate` 방은 `Invisible` Lobby로 만들어 일반 목록에서 제외하지만 보안 경계는 아니다.
- `IsLocked`이거나 정원이 찬 방은 Steam 검색 결과에서 제외된다. 한 번의 목록 조회는 최대 50개다.
- 비밀번호는 참가 후 호스트가 확인하며, 강퇴는 클라이언트가 제어 메시지에 따라 나가는 협조형 동작이다. 변조된 클라이언트를 Lobby 자체에서 강제로 제거하지는 못한다.
- `Public` 필드는 Lobby metadata, `Member` 필드는 승인된 멤버의 P2P 스냅샷, `Private` 필드는 해당 클라이언트 메모리에만 둔다.
- 멤버 스냅샷 한 개는 64 KiB 이하여야 한다. 플레이어 갱신 결과를 확인할 수 없으면 상태 불일치를 막기 위해 해당 멤버가 방을 나간다.
- Steam의 자동 호스트 이전은 지원하지 않는다. 원래 호스트가 나가거나 Lobby 소유자가 바뀌면 방을 종료한다.

Chat·Lobby·Room·Player 작업은 Unity 메인 스레드에서 호출한다. `Member.Send`와 `Host.Send`는 Steam에서 다른 스레드에서도 큐에 넣을 수 있지만, 플랫폼 간 이식성을 위해 공통 코드는 메인 스레드에서 호출하는 편이 안전하다.

`CreateAsync`와 `JoinAsync`는 네이티브 요청을 직접 취소할 수 없어 늦게 생성되거나 참가된 Lobby를 정리한 뒤 완료될 수 있다. 패키지는 P2P 채널 `45831`을 사용하므로 같은 API의 전역 session callback을 다른 시스템도 다룬다면 dispatcher와 소유권 정책을 공유해야 한다.

저장소의 `steam_appid.txt`는 공유 테스트 앱 Spacewar `480`을 사용한다. 실제 제품 검증과 배포에는 해당 Steamworks 앱의 App ID를 사용한다.
