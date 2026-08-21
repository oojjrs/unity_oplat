# OOJJRS' Unity Platform

Unity 프로젝트의 플랫폼 인증 초기화, 사용자 파일 저장소와 방·메시지 네트워크 기능을 한 진입점으로 묶는 패키지다.

## 설치

소비 프로젝트의 `Packages/manifest.json`에 Oplat과 Steamworks.NET Git 패키지를 함께 선언한다.

```json
{
  "dependencies": {
    "com.oojjrs.oplat": "https://github.com/oojjrs/unity_oplat.git?path=/Packages/src",
    "com.rlabrecque.steamworks.net": "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#ba71581f1ed7349e8d0f17ddc6f135dd3bc8a6a3"
  }
}
```

Unity 패키지 manifest는 Git 의존성 위치를 전파하지 못하므로 두 항목을 소비 프로젝트에 직접 선언한다. Steamworks.NET은 호환성이 확인된 revision으로 고정한다.

## 사용법

1. GameObject에 `MyPlatformInitializer`를 추가한다.
2. 같은 GameObject의 컴포넌트에서 `MyPlatformInitializer.CallbackInterface`를 구현한다.
3. `AppId`로 앱 식별자를, `InitialType`으로 `MyPlatformTypeEnum.Anonymous` 또는 `MyPlatformTypeEnum.Steam`을 반환한다. `AnonymousInstanceId`에는 로컬 Anonymous 실행 인스턴스를 구분할 안정적인 값을 반환한다. Steam에는 Depot ID가 아닌 게임의 App ID를 사용한다.
4. 플랫폼 실행 결과를 `OnResult(MyPlatformServiceInterface service)`에서 처리한다.

`service.Account`, `service.Nickname`, `service.ProfileSprite`로 초기화된 플랫폼의 계정 식별자, 표시 이름, 프로필 이미지를 가져온다. `Anonymous`는 기기 고유 식별자와 기기 이름, 패키지 기본 프로필 이미지를 사용한다. 기기 식별자를 지원하지 않으면 계정도 기기 이름을 사용하고, 기기 이름도 지원하지 않으면 제품 이름으로 대체한다. Steam은 SteamID, 현재 persona name, 현재 사용자의 큰 크기 프로필 이미지를 사용한다. 프로필 이미지를 가져오지 못하면 `ProfileSprite`는 `null`이다.

`CallbackInterface.AnonymousInstanceId`는 `Anonymous`에서만 사용한다. `null`, 빈 문자열 또는 공백이면 기존 기기 기반 값을 그대로 사용한다. 값이 있으면 앞뒤 공백을 제거한 뒤 Account에 App ID와 함께 붙이고 Nickname에도 표시하여 같은 기기의 로컬 실행 인스턴스를 구분한다. 같은 논리 인스턴스에는 실행할 때마다 같은 값을 제공해야 하며, 명령행 인자나 테스트 런처 등 값의 출처는 소비 프로젝트가 결정한다.

`ProfileSprite`가 반환하는 `Sprite`의 수명은 플랫폼 서비스가 관리하므로 소비자가 직접 파괴하지 않는다.

플랫폼 서비스를 장기간 보관하는 경우 `service.IsAlive`를 확인한 뒤 접근한다.

`Account`는 클라이언트에서 조회한 식별 문자열이며 서버 인증 증명이 아니다.

패키지는 `Anonymous`와 `Steam`을 구현한다. 다른 `MyPlatformTypeEnum` 값은 아직 `NotImplementedException`을 발생시킨다.

플랫폼 구현은 `oojjrs.oplat` 어셈블리에서 자동 등록된다. 소비자는 `AnonymousPlatform`이나 `SteamPlatform` 같은 내부 구체 타입을 알거나 직접 등록할 필요가 없다.

플랫폼 구현은 별도 `DontDestroyOnLoad` GameObject의 `MonoBehaviour`로 생성된다. `MyPlatformInitializer`는 `OnResult(MyPlatformServiceInterface service)` 이후 삭제해도 플랫폼 수명 주기에 영향을 주지 않는다.

Steam 구현은 소비 프로젝트에 `STEAMWORKS_NET`이 정의된 경우에만 컴파일된다. Steamworks.NET은 기본 설정에서 Standalone 대상에 이 심볼을 자동으로 추가한다.

플레이어 빌드의 Steam은 `SteamAPI.RestartAppIfNecessary`를 먼저 호출한다. `service.IsRestartRequired`가 `true`라면 Steam이 라이브러리에 설치된 게임을 다시 실행하므로 소비자는 현재 프로세스를 즉시 종료해야 하며, 이 결과에서 계정이나 프로필 정보에 접근하지 않는다. 재실행이 필요하지 않으면 `SteamAPI.InitEx`로 초기화하고 실제 App ID가 `CallbackInterface.AppId`와 같은지 검증한다.

Unity Editor에서는 재실행을 요청하지 않는다. Steam 클라이언트를 먼저 실행하고 현재 작업 디렉터리의 개발용 `steam_appid.txt`에 같은 App ID를 제공한다. 개발용 파일은 배포 빌드에 포함하지 않는다. Steam은 Unity Editor 프로세스에 연결되므로 Play Mode를 멈춰도 실행 중 표시가 Editor 종료 전까지 남을 수 있다.

초기화 이후 `SteamPlatform`이 매 프레임 콜백을 처리하고, 플랫폼 오브젝트가 파괴될 때 Steam API를 종료한다. Play Mode 중 스크립트를 다시 컴파일했다면 Play Mode를 재시작한다.

## 플랫폼 저장소

`service.Storage`는 플랫폼별 사용자 저장소를 파일 단위 raw bytes API로 제공한다. 직렬화 형식은 소비 프로젝트가 결정하며, `FileByteCountMax`는 한 파일의 상한인 100 MiB를 반환한다. `WriteAsync`는 데이터를 새 파일로 쓰거나 기존 파일 전체를 덮어쓰고, 빈 배열도 0-byte 파일로 저장한다. `ReadAsync` 결과의 `IsFound`가 `false`이면 파일이 없으며 이때 `Data`는 빈 배열이다. `IsFound`가 `true`이면서 `Data`가 비어 있으면 존재하는 0-byte 파일이다. `ExistsAsync`는 존재 여부를, `DeleteAsync`는 존재하던 파일을 삭제했는지를 반환한다.

`ListAsync`는 `FileName`, `SizeBytes`, `LastWriteTimeUtc`를 가진 파일 스냅샷을 반환하며 파일이 없으면 빈 목록을 반환한다. 수정 시각은 UTC지만 플랫폼별 시각 해상도는 다를 수 있다. 파일명은 `/`를 구분자로 쓰는 portable relative path다. 절대 경로, `\\`, 빈 경로 구간, `.`이나 `..`, 지원 플랫폼에서 유효하지 않은 이름은 거부한다.

Anonymous 저장소는 로컬 서버를 거치지 않고 현재 프로세스가 `%LOCALAPPDATA%\oojjrs\Oplat\AnonymousStorage\v1\<project-key SHA-256>\<AppId>\users\<account SHA-256>\files\<logical path>`를 직접 사용한다. Project Key는 `Application.identifier`이고, 값이 없으면 company/product 이름으로 대체하며 프로젝트, App ID와 Account별로 격리한다. 이는 같은 Windows 계정의 Editor와 빌드에서 사용하는 로컬 개발용 저장소이며 신뢰할 수 있는 원격 서버 데이터베이스가 아니다.

Steam 저장소를 사용하려면 Steamworks App Admin에서 사용자별 byte quota와 file count를 설정하고 Cloud 설정을 저장·게시해야 한다. Storage Task의 완료는 현재 프로세스에서 `ISteamRemoteStorage` 작업이 완료됐다는 뜻이며, 다른 기기에서 사용할 수 있게 되는 후속 업로드·다운로드는 Steam 클라이언트의 Cloud 동기화가 담당한다. Steam Storage 호출은 Unity 메인 스레드에서 시작해야 한다. 취소는 작업이 백엔드에 전달되기 전까지만 즉시 적용되며, 전달된 뒤에는 변경을 롤백하거나 성공을 취소로 오인하지 않도록 실제 결과까지 기다린다.

## Steam 네트워크

`service.Net.UseLocal`은 런타임 중 언제든 변경할 수 있다. `true`이면 현재 방의 존재나 호스트 여부와 관계없이 `Member.Send` 요청과 `Host.Send` 응답을 현재 프로세스의 Host/Member 결과 처리기로 직접 전달한다. Anonymous와 Steam 모두 같은 로컬 전송을 지원하며, Steam Lobby를 자동 생성하지 않는다. `false`로 되돌리면 이후 전송부터 현재 플랫폼 방 경로를 사용한다.

`service.Net`의 Steam 구현은 별도 게임 서버 없이 Steam Lobby로 방을 관리하고 `ISteamNetworkingMessages`의 reliable P2P 메시지로 멤버 요청과 호스트 응답을 전달한다. Steam 방의 `Id`는 Lobby SteamID의 10진수 문자열이고, `Code`는 같은 ID를 13자리 Base32 문자열로 표현한 값이다.

`IsPrivate` 방은 Steam의 `Invisible` Lobby로 만든다. 패키지의 일반 Lobby 목록에서는 제외하면서 코드 참가를 유지하기 위한 선택이며, Steam 서비스나 변조된 클라이언트에 대한 보안·비밀성 경계는 아니다. `IsLocked` 방은 참가 불가로 설정되며, Steam은 참가 불가이거나 정원이 찬 Lobby를 검색 결과에서 제외하므로 Anonymous처럼 목록에 계속 표시되지 않는다. 한 번의 Lobby 조회는 Steam 제한에 맞춰 최대 50개를 반환한다.

Steam Lobby에는 네이티브 방 비밀번호와 강제 퇴장 기능이 없다. 비밀번호는 Lobby 참가 후 호스트가 검사하고, 강퇴는 호스트가 보낸 제어 메시지를 받은 클라이언트가 스스로 Lobby를 나가는 협조형 기능이다. 승인되지 않았거나 강퇴된 Steam 사용자는 패키지의 논리적 플레이어 목록과 게임 메시지 처리에서 제외하지만, 변조된 클라이언트가 Steam Lobby 자체에 남는 것까지 막지는 못한다.

방과 플레이어의 `Public` 필드는 Lobby metadata로 게시하고, `Member` 필드는 비밀번호 승인을 통과한 피어에게만 reliable P2P 스냅샷으로 전달하며, `Private` 필드는 해당 클라이언트 메모리에만 둔다. 멤버 스냅샷 한 개의 상한은 64 KiB이므로 필드와 플레이어가 이를 넘는 변경이나 입장은 실패한다. 플레이어 갱신 결과를 확인할 수 없으면 서로 다른 상태로 계속 진행하지 않도록 해당 멤버가 방을 나간다. 강퇴 작업의 `OnOk`는 Steam Lobby에서 물리적으로 제거됐다는 뜻이 아니라 논리적 플레이어 목록에서 제거되고 협조형 퇴장 메시지를 보냈다는 뜻이다.

Steam은 호스트가 나가면 다른 멤버에게 Lobby 소유권을 자동 이전하지만 이 패키지는 호스트 이전을 지원하지 않는다. 호스트가 나가거나 소유자가 바뀌면 방을 종료하고 남은 정상 클라이언트도 Lobby를 나간다.

`MyNetHostServiceInterface.Send`와 `MyNetMemberServiceInterface.Send`는 반환값이 없는 전송 큐 API다. 따라서 개별 Steam P2P 전송 실패는 호출자에게 결과 콜백으로 전달되지 않으며, 중요한 애플리케이션 메시지는 상위 프로토콜에서 응답이나 확인 메시지를 정의해야 한다.

방 또는 플레이어 정보가 갱신되면 요청자는 해당 `UpdateResultInterface.OnOk`로 결과를 받는다. 나머지 방 구성원은 방 갱신을 `RoomResult.OnOk`의 최신 방 스냅샷으로, 플레이어 갱신을 `PlayerResult.OnOk`의 최신 플레이어 스냅샷으로 받으며 각 결과는 자신의 가시성 권한에 맞게 필터링된다.

Lobby/Room/Player 작업과 플랫폼 종료는 Unity 메인 스레드에서 호출해야 한다. 두 `Send` 메서드는 다른 스레드에서도 큐에 넣을 수 있다. `CreateAsync`와 `JoinAsync` 취소는 Steam의 네이티브 요청을 직접 취소할 수 없으므로 그 결과를 확인하고 늦게 만들어지거나 참가된 Lobby를 정리한 뒤 완료된다. 패키지는 `ISteamNetworkingMessages` 채널 `45831`을 사용하며, 같은 피어에 대해 다른 시스템도 이 API의 전역 session callback을 관리한다면 하나의 dispatcher와 session 소유권 정책으로 조정해야 한다.

저장소의 개발용 `steam_appid.txt`는 Valve의 공유 테스트 App ID인 Spacewar `480`을 사용한다. 다른 개발 프로젝트도 같은 App ID를 사용하므로 패키지는 자체 프로토콜 metadata로 Lobby를 구분하지만, 실제 제품 검증과 배포에는 해당 Steamworks 앱의 App ID를 사용해야 한다.
