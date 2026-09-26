# UnityOplat 패키지 문서

`com.oojjrs.oplat`의 초기화와 공개 인터페이스를 기능별로 정리한다.

## 시작하기

| 문서 | 설명 |
| --- | --- |
| [`MyPlatformInitializer.CallbackInterface`](api/MyPlatformInitializer.CallbackInterface.md) | 플랫폼 설정, 결과 처리기 연결, 초기화 완료 처리 |
| [`MyPlatformServiceInterface`](api/MyPlatformServiceInterface.md) | 계정·프로필·저장소·네트워크 진입점과 서비스 수명 |

## 통계

| 문서 | 설명 |
| --- | --- |
| [`MyStatsServiceInterface`](api/MyStatsServiceInterface.md) | JSON 정의 목록, `INT`·`FLOAT`·`AVGRATE` 갱신, 전체·키 초기화 |

## 시간

| 문서 | 설명 |
| --- | --- |
| [`MyTime`](api/MyTime.md) | 네트워크로 전달할 수 있는 UTC 절대 시각 값과 변환·연산 |
| [`MyTimeServiceInterface`](api/MyTimeServiceInterface.md) | 플랫폼 기준 UTC 시계와 동기화 상태 |

## 저장소

| 문서 | 설명 |
| --- | --- |
| [`MyStorageServiceInterface`](api/MyStorageServiceInterface.md) | raw byte 파일의 조회·저장·목록·삭제 |

## 네트워크

| 문서 | 설명 |
| --- | --- |
| [`MyNetInterface`](api/MyNetInterface.md) | 네트워크 서비스 진입점, 공통 실패, 필드 공개 범위, 로컬 전송 |
| [친구와 게임 초대 계약](api/MyNetFriendServiceInterface.md) | 친구 스냅샷과 서비스 계약. Anonymous, Steam, Ugsymous의 친구 추가·목록 조회·초대 지원 |
| [`MyNetLobbyServiceInterface`](api/MyNetLobbyServiceInterface.md) | 공개 방 목록의 일회 조회와 반복 조회 |
| [`MyNetRoomServiceInterface`](api/MyNetRoomServiceInterface.md) | 방 생성·참가·수정·퇴장 |
| [`MyNetRoomSwitchHandlerInterface`](api/MyNetRoomSwitchHandlerInterface.md) | 플랫폼 참여 요청의 게임 준비와 공통 방 전환 결과 처리 |
| [`MyNetRoomInterface`](api/MyNetRoomInterface.md) | 방 스냅샷 조회 |
| [`MyNetPlayerServiceInterface`](api/MyNetPlayerServiceInterface.md) | 현재 플레이어의 필드 갱신 |
| [`MyNetPlayerInterface`](api/MyNetPlayerInterface.md) | 플레이어 스냅샷 조회 |
| [`MyNetChatServiceInterface`](api/MyNetChatServiceInterface.md) | 방 채팅 참가·전송·퇴장 |
| [`MyNetChatResultInterface`](api/MyNetChatResultInterface.md) | 수신 채팅 처리 |
| [`MyNetMemberServiceInterface`](api/MyNetMemberServiceInterface.md) | 멤버 요청 전송 |
| [`MyNetMemberResultInterface`](api/MyNetMemberResultInterface.md) | 호스트 응답 처리 |
| [`MyNetHostServiceInterface`](api/MyNetHostServiceInterface.md) | 호스트 응답 전송 |
| [`MyNetHostResultInterface`](api/MyNetHostResultInterface.md) | 멤버 요청 처리 |
| [네트워크 페이로드](api/network-payloads.md) | `MyNetRequest`, `MyNetResponse`, 직렬화 지원 범위 |

## 플랫폼별 동작

| 문서 | 설명 |
| --- | --- |
| [Anonymous](platforms/anonymous.md) | 로컬 계정, 저장소와 개발용 네트워크 |
| [Steam](platforms/steam.md) | 초기화, Steam Cloud, 친구, Lobby/P2P 제약 |
| [Ugsymous](platforms/ugsymous.md) | UGS 익명 인증, Cloud Save, Friends, Session·Relay·Vivox |
