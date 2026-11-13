# UnityOplat 작업 메모리

## 현재 요약과 진행 상태

- 마지막 대조: 2026-11-13T18:56:22+09:00. 기존 결정 기록과 로컬 Git 이력, Packages/src/package.json을 확인했다. 현재 버전은 1.13.8이다. 근거: R-20261113-memory-cleanup, R-20261113-invitation-version-commit.
- 입장 실패: 공통 FailureEnum의 IncorrectPassword=8, RoomFull=9, RoomLocked=10을 Anonymous·Steam·UGS 콜백에 전달한다. 기존 enum 0~7과 외부 콜백 시그니처는 유지한다. 근거: R-20261112-join-failure-complete.
- UGS: Lobby 직접 입장 후 Multiplayer 세션 연결, 재입장·RoomId 우선·취소·Busy 시점·예외 메시지 동작은 사용자가 전체 검토 후 유지하기로 했다. 친구 초대·퇴장·방 갱신·플레이어 갱신은 session.Id로 조회한다. 근거: R-20261112-ugs-session-id-lookup, R-20261113-reviewed-content.
- 초대: Oplat InviteAsync를 거친 Steam·Anonymous 초대는 호스트/서버가 계정을 확인하여 비밀번호 검사만 생략한다. 시간 만료 없이 입장 성공 시 1회 소모하고 방 종료 시 정리한다. 잠금·정원·일반 입장 비밀번호 검사는 유지하며, Steam UI 직접 발송과 UGS는 적용 대상이 아니다. 근거: R-20261113-invitation-lifetime, R-20261113-invitation-admission-complete.
- 호환성: Steam protocol은 5이며 4와 상호 입장하지 않는다. 공개 InviteAsync·JoinAsync·SwitchAsync 시그니처는 유지한다. 근거: R-20261113-invitation-protocol.
- 확인 한계: 기존 기록은 코드 정적 대조·형식·공백 검사 통과를 보고한다. 컴파일·실계정 네트워크·Play Mode 결과는 없으며 이번 메모리 정리로 실행 검증 상태가 바뀌지 않는다.
- 과거 전체 대화 복원은 완료되지 않았다. 미확인 출처를 조사 완료나 확정 결정으로 간주하지 않으며, 이후 요청과 관련된 주제만 원 출처에서 확인한다. 기존의 전수 조사 재개 상태는 R-20261113-memory-cleanup에서 대체했다.

## 결정 색인과 출처

- 실패 분류의 변경 이유: R-20260915-error-report → R-20261112-join-failure → R-20261112-join-failure-implementation → R-20261112-join-failure-complete.
- UGS 조회 키 수정: R-20261112-ugs-session-id-lookup. 부수 변경 축소 권고와 사용자 유지 결정: R-20261113-scope-review → R-20261113-reviewed-content.
- 초대 범위·수명·프로토콜: R-20261113-invitation-admission-request → R-20261113-invitation-lifetime → R-20261113-invitation-protocol → R-20261113-invitation-admission-complete. 버전 결정: R-20261113-invitation-version-commit.
- S0: [오류 메시지 출력 방식 확인](codex://threads/01a0a26f-6c09-7923-b229-a4aa390093fe). hostId local, H:\UnityOplat. 생성 Unix 1789431606, 갱신 Unix 1789431664. 턴 01a0a26f-74ff-7482-9cba-ed5e27168051의 startedAt=1789431608, completedAt=1789431664이며 개별 발언 시각과 구분한다.
- S1: [입장 실패 분류·UGS 조회 및 범위 검토](codex://threads/01a1cfae-5fa4-7052-87e1-438ae908ab0e). hostId local, H:\UnityOplat. 개별 턴·항목 ID와 발언 시각은 기존 기록에 미제공이다. 초기 미확인 채팅 목록의 조사 출처도 이 채팅이다.
- S2: [초대 대상 비밀번호 생략](codex://threads/01a1d186-ba65-77f2-81e2-4440a83b1791). hostId local, H:\UnityOplat. 별도 표시 외 턴·항목 ID와 개별 발언 시각은 미제공이다.
- S3: [Mines 소비 요구](codex://threads/01a1c4ef-3255-7540-802f-ca00bdf2bd3b). 사용자 항목 01a1d183-f833-72b1-bee3-f8eded25466e, Codex 최종 항목 msg_004917ffc79d0c26016af6266137ac87d0899f7c6876a61b6e. 정확한 원 발언 시각은 미확인이다.
- S4: [메모리 정리](codex://threads/01a1d451-f51b-7e50-9d9c-51b6cb2ece0f). hostId local, H:\UnityOplat, PC OOJJRS-PC, 작업자 Codex /root. 현재 턴·사용자 항목 ID와 개별 발언 시각은 미제공이다.
- 아래 시각은 기록 시각이다. 원 발언 시각이 없는 기록에 이를 대신 적용하지 않는다. 과거 기록의 기록자는 Codex /root이며 실제 PC 이름은 당시 미확인이다. 과거 구현·검증 결과는 해당 기록의 관찰 또는 보고이고, 이번 정리에서 다시 실행한 결과가 아니다.

## R-20260915-error-report

- 기록 시각: 2026-11-12T21:17:56+09:00. 출처 S0; 원 발언자 사용자 및 당시 Codex; 질문 주체 사용자, 진단 주체 당시 Codex. 상태: 과거 설명.
- 사용자 항목 01a0a26f-8c19-7282-a5b9-853448b5aa8a는 오류 메시지가 뭉뚱그려 나오는 이유와 출력 방식을 질문했다.
- Codex 최종 항목 msg_033f30862c02f425016aa88f62407887d08a4de61644ff3ff3은 Steam·Anonymous가 예외를 MyNetSessionException으로 감싸 원인을 InnerException에 보존하며 OnFailed는 FailureEnum 분류만 전달한다고 설명했다. UI의 e.Message 출력 여부는 호출 프로젝트 확인이 필요하다. 당시 도구 출력은 기록에 없으므로 독립 검증 사실로 취급하지 않는다.

## R-20261112-join-failure

- 기록 시각: 2026-11-12T21:17:56+09:00. 출처 S1; 원 발언자·질문 주체 사용자, 코드 해석 주체 Codex. 상태: 변경 전 진단이며 이후 구현이 당시 코드 상태를 대체한다.
- 당시 소스 관찰: Steam은 잠금·정원·비밀번호 오류를 AdmissionRejected/NotPermitted로 합쳤다. Anonymous 서버의 Forbidden/Conflict도 클라이언트에서 NotPermitted로 합쳤다. UGS의 조사 경로는 SessionException을 OnException으로 전달했으며 같은 NotPermitted 매핑이 없었다. 공통 FailureEnum에는 개별 세 원인이 없었다.
- 판단: Steam·Anonymous 호출자는 NotPermitted만으로 세 원인을 구분할 수 없으며, 세 플랫폼의 반환이 모두 같다고 일반화할 근거도 없었다. 이 진단은 입장 실패 경로의 정적 추적에 한정한다.

## R-20261112-join-failure-implementation

- 기록 시각: 2026-11-12T21:24:54+09:00. 출처 S1; 원 발언자·수정 결정 주체 사용자, 구현 판단 주체 Codex. 상태: R-20261112-join-failure의 확인 범위를 대체한 구현 결정이며 적용 결과는 다음 기록이다.
- 사용자 결정: 세 플랫폼 모두 실제 입장 실패 사유를 콜백까지 전달한다. 기존 enum 수치를 유지하며 세 원인을 추가하고, Anonymous 서버·클라이언트와 Steam P2P·Lobby 제어 메시지를 함께 변경한다. Steam wire 형식 변경에는 protocol 증가가 필요하다.
- UGS 판단 근거: 설치 SDK 2.3.1의 LobbyConverter.ToSessionException은 세 원인을 SessionError.Unknown으로 합치며 InnerException도 보존하지 않는다. 메시지 추정 대신 LobbyService의 타입 있는 입장 응답을 받고 성공한 Lobby를 ReconnectToSessionAsync로 연결한다. 부분 입장 실패·취소 시 정리와 원래 예외 보존이 필요하다.
- 당시 근거: Library/PackageCache/com.unity.services.multiplayer@0a90950aff70/Runtime/Multiplayer/Lobby/LobbyConverter.cs:221, Session/SessionManager.cs:204의 경로 및 ReconnectSessionOptions.WithNetworkHandler. [Unity Lobby 오류](https://docs.unity.com/en-us/mps-sdk/lobby-error-messages), [세션 참가](https://docs.unity.com/en-us/mps-sdk/join-session).

## R-20261112-join-failure-complete

- 기록 시각: 2026-11-12T21:30:47+09:00; 검토 결과 기록 2026-11-12T21:31:33+09:00. 출처 S1; 원 요청·승인 주체 사용자, 구현·정적 판단 주체 Codex. 상태: 구현 기록. 당시 보류한 UGS 조회 문제는 R-20261112-ugs-session-id-lookup으로 해소했고 protocol 4는 이후 5로 대체됐다.
- Anonymous는 원인별 ResultCodeEnum과 OnFailed 매핑을 연결하되 권한 거절·구 서버 일반 응답 fallback을 보존했다. Steam은 nullable admission 결과와 1바이트 사유를 양쪽 메시지에 적용했다. 사유를 ResetSession 전에 완료하고 첫 결과를 유지한다. 호스트 처리 오류는 별도 wire 표식으로 OnException, 연결 실패·방 소멸은 Disconnected/NotFoundRoom으로 전달했다. ProtocolVersion 3→4.
- Steam 판단 근거: Valve EChatRoomEnterResponse, SendLobbyChatMsg, ISteamNetworkingMessages 계약. 상대의 능동 종료가 SessionFailed 콜백을 발생시키지 않으므로 제어 메시지 전송 성공 후 기존 ClosePeer 자원 해제를 유지했다.
- UGS는 JoinLobbyByIdAsync/JoinLobbyByCodeAsync의 타입·Reason으로 분류하고 WithNetworkHandler(_net.Transport)로 기존 네트워크 처리기를 연결했다. 닉네임·플레이어 필드·visibility는 SDK와 같은 PlayerDataObject 변환을 유지한다. 사전 membership 조회로 신규 가입만 rollback하고 연결 완료 후 취소되면 새 session을 LeaveAsync한다. 정리 실패는 원래 예외와 AggregateException으로 보존한다. SDK 소스는 수정하지 않았다.
- 당시 정적 대조: MyNetRoomSwitcher의 ForwardTo를 통해 SwitchAsync에도 사유가 전달되며 외부 callback 시그니처는 유지된다. 반복 완료·정상 입장·방 소멸·연결 실패·timeout·취소·fallback을 소스와 SDK 계약으로 확인했다. 실행 검증 한계는 현재 요약에 남긴다.

## R-20261112-ugs-session-id-lookup

- 기록 시각: 2026-11-12T21:34:41+09:00; 적용 기록 2026-11-12T21:35:05+09:00; 검토 기록 2026-11-12T21:36:04+09:00. 출처 S1; 원 발언자·수정 승인 주체 사용자, 구현 범위 판단 Codex. 상태: 별도 문제를 보존하던 이전 판단을 사용자 수정 요청이 대체했다.
- 사용자 결정: UGS 세션 조회 키 불일치도 수정하고 앞선 변경량과 부수 동작을 설명한다. SDK의 SessionManager.RegisterSession은 Sessions[sessionType]에 등록하므로 roomId를 사전 키로 사용하면 존재하는 세션을 못 찾는다.
- 적용 판단: InviteAsync, Room.ExitAsync, Room.UpdateAsync, Player.UpdateAsync의 네 조회를 Sessions.Values.FirstOrDefault(value => value.Id == roomId)와 null 판정으로 바꾼다. 캐시·공통 API 추가 없이 기존 권한·실패·성공 콜백을 보존한다.
- 설명 대상으로 확인한 부수 동작: 같은 방 재입장 즉시 성공, RoomId 우선, lifetime 취소 연동, Busy 해제 후 콜백, 추가 typed 오류 매핑, 기타 예외의 바깥 메시지 일반화와 InnerException 보존. 이전 보고가 이들을 충분히 구분하지 못한 점을 다음 검토로 이어갔다.

## R-20261113-scope-review

- 기록 시각: 2026-11-13T05:22:15+09:00. 출처 S1; 원 발언자·질문 주체 사용자, 평가 주체 Codex. 상태: 축소 권고는 R-20261113-reviewed-content의 사용자 유지 결정으로 대체됨. 당시 코드 수정은 없었다.
- Codex 판단: enum 분리·실제 사유 보존·콜백 매핑과 별도 승인된 session.Id 조회는 요청에 필요하다. UGS의 SDK 정보 소실 대응은 필요하지만 Lobby 직접 입장→Multiplayer 연결을 유일한 최소 해법으로 단정할 수 없다. 선택한 경로의 부분 실패 정리는 필요하다.
- Codex 판단: 같은 방 재입장 즉시 성공, RoomId 우선 전환, Busy 해제 후 콜백, 바깥 예외 메시지 일반화는 세 원인 분리에 필수적이지 않다. 특히 메시지 일반화는 상세 원인 전달 취지에 맞지 않는 부수 변경이라 축소를 권고했다. 질문을 되돌림 승인으로 해석하지 않고 사용자 결정을 기다렸다.

## R-20261113-reviewed-content

- 기록 시각: 2026-11-13T05:28:25+09:00. 출처 S1; 원 발언자·내용 결정 주체 사용자, 분리 판단 주체 Codex. 상태: 현재 유효한 사용자 결정.
- 사용자는 각 부수 수정 이유를 설명받고 전체 내용을 검토한 뒤 코드를 그대로 유지하라고 확인했다. UGS 바깥 예외 메시지를 되돌리겠다는 Codex 후속 제안도 적용하지 않는다. 이전 축소 권고의 근거는 R-20261113-scope-review에 보존한다.
- 원인별 입장 실패 전달은 공통 enum과 세 플랫폼 송수신·콜백을 함께 유지해야 완결된다. UGS 네 서비스의 조회 키 수정은 독립된 기존 결함 수정이다.
- 버전 결정: 기존 파일 동작 변경 두 묶음에 patch를 각각 적용하여 실패 분류 1.13.6, 조회 키 1.13.7로 구분했다. README·CHANGELOG 새 문구나 관계없는 기능은 추가하지 않았다.

## R-20261113-invitation-admission-request

- 기록 시각: 2026-11-13T05:52:05+09:00. 출처 S2 및 S3; 원 발언자·요청 주체 사용자, 범위 해석 Codex. 상태: 구현 요청. 미확정이던 수명과 Steam UI 범위는 다음 기록에서 해소했다.
- 소비 요구: 원인별 실패 구분은 이미 사용처에 반영됐으며, Steam·Anonymous에서 호스트/서버가 방·계정별 초대를 확인해 비밀번호 검사만 생략해야 한다. 일반 참가자의 초대도 호스트가 확인하며 정원·잠금·방 소멸과 일반 입장 비밀번호 검사를 유지한다. 성공 시 소모하고 발송 실패·방 종료 시 정리한다.
- 범위 판단: 기존 InviteAsync·JoinAsync·SwitchAsync 공개 시그니처와 서버 권한 판정을 유지하고 UGS로 임의 확장하지 않는다. S3의 최신 6개 턴 확인은 전체 과거 이력 복원을 뜻하지 않는다.

## R-20261113-invitation-lifetime

- 기록 시각: 2026-11-13T05:53:25+09:00. 출처 S2; 질문 도구 call_65c738e9abce46bc921129031f962404의 0번 답변. 원 발언자·수명 결정 주체 사용자, 지원 범위 판단 Codex. 상태: 현재 유효.
- 사용자 확정: 시간 제한 없이 방이 유지되는 동안 유효하며 입장 성공 시 1회 소모하고 방 종료 시 정리한다. 요청 기록의 만료 기간 미확정을 해소했으며 10분 기본안은 채택하지 않았다.
- Codex 범위 판단: Oplat InviteAsync 초대만 지원한다. Steam UI 직접 발송은 발신자의 Oplat에서 확인할 계약이 없어 기존 비밀번호 검사를 유지한다. 일반 참가자는 호스트 승인 후 본인 계정으로 Steam 초대를 발송해 초대자 정체성을 보존한다.

## R-20261113-invitation-protocol

- 기록 시각: 2026-11-13T05:54:41+09:00. 출처 S2; 구현 판단 주체 Codex, 근거는 사용자 요청과 R-20261113-invitation-lifetime. 상태: 적용된 구현 결정.
- Steam은 호스트가 대기 기록을 먼저 승인하고 참가자가 Steam 발송 성공을 알리면 실제 초대 권한을 확정한다. 대기 기록만 있는 대상의 비밀번호 불일치 입장은 확정/취소까지 기다려 발송·입장 순서 경합을 처리한다. 잠금·정원을 우선 검사한다. 실패·취소·초대자 연결 종료는 대기 기록을 정리하지만 확정 권한은 입장 성공 또는 방 종료까지 유지한다.
- 기존 OperationGate, 방 ID·epoch, 개별 Guid로 경계를 유지한다. 새 메시지 교환에 따라 Steam protocol 4→5로 변경하며 구 버전과 상호 입장은 지원하지 않는다. InviteAsync는 호스트 확인까지 기다린다.
- Anonymous는 인증된 session.Account와 RoomSecret 수명의 계정 집합을 사용한다. 알림 큐 Send 성공 직후 기록하고 정상 입장 시 소모하며 방 제거 시 함께 정리한다.
- 공식 계약에 따른 제한: Valve InviteUserToLobby의 성공은 실제 수신·수락을 보장하지 않고 GameLobbyJoinRequested_t는 친구 참가와 초대를 구분하지 않는다. Steam UI 직접 초대 제외의 근거다.

## R-20261113-invitation-admission-complete

- 기록 시각: 2026-11-13T05:57:30+09:00. 출처 S2; 원 요청·수명 결정 주체 사용자, 구현·정적 검토 주체 Codex. 상태: 구현 기록. 당시 버전 1.13.7은 다음 기록의 1.13.8로 대체됐다.
- SteamNet.cs, AnonymousServer.cs, AnonymousServerRoom.cs, AnonymousServerJoinRoom.cs에 초대 권한을 구현했다. 동일 계정에 반복 초대해도 입장 권한이 횟수별로 누적되지 않는다. Steam 확정 초대는 초대자 퇴장만으로 사라지지 않으며 InviteAsync 대기 중 다른 방 작업과 겹치면 기존 OnBusy 계약을 사용한다.
- Anonymous는 인증된 초대자의 방 소속·대상 계정 확인과 알림 큐 등록 성공 후 기록하고 플레이어 추가 성공 시 소모한다. Steam 발송 성공과 Anonymous 큐 등록 성공은 실제 상대 수신·수락 보장이 아니다.
- 검토 중 Steam 입장 후처리 예외가 로스터를 롤백할 때 초대 권한을 잃지 않도록 소모를 후처리 성공 뒤로 옮겼다. 최종 메시지 송수신·취소·상관관계·방 정리·일반 입장/잠금/정원 분기와 소비 호출 형태를 정적으로 대조했다고 기록했다. 실행 검증 한계는 현재 요약에 남긴다.

## R-20261113-invitation-version-commit

- 기록 시각: 2026-11-13T05:59:08+09:00. 출처 S2; 원 발언자·버전 결정 주체 사용자, patch 분류 판단 Codex. 상태: 버전 결정은 현재 유효하고 당시 기록은 결과 전 범위 확정이었다.
- 사용자 지시에 따라 기존 네 파일의 입장·초대 동작 변경을 patch 1.13.7→1.13.8로 분류했다. Steam wire protocol 변경을 포함하지만 독립 기능 파일 추가와 공개 사용 방식·API 변경은 없다는 판단이었다.
- 당시 연동 판단: manifest와 lock은 file:src 참조이고 README 설치 URL에도 고정 버전이 없어 추가 변경하지 않았다. 공개 문서의 새 문구는 추가하지 않았다.

## R-20261113-memory-cleanup

- 기록 시각: 2026-11-13T18:56:22+09:00. 출처 S4; 기록자 Codex /root; 원 발언자·정리 요청 주체 사용자, 정리 범위 판단 Codex. 사용자 요청은 메모리 지침에 맞는 정리다. 상태: 요약·색인 및 기존 기록 정리.
- 판단 근거: 문서 작성 지침 4·5번은 다음 작업에 필요한 의도·제약·결정 이유·주의점·미해결 사항을 남기고 중복·재확인 가능한 내역과 전수 대화 조사를 줄이도록 한다. 기존 결정 기록 12개의 식별자·시각·주체·출처와 변경 이유를 보존하고 반복된 구현 설명·검사 내역·작업 범위 설명을 합쳤다.
- 초기 복원 기록의 전수 조사 재개 상태를 대체한다. 본문 미확인 69건의 목록 활동 시각은 결정 근거가 아니므로 목록은 제거하고 조사 출처 S1과 미확인 상태를 남겼다. 과거 결정을 복원 완료하거나 철회한 것으로 간주하지 않는다. 필요한 주제가 생기면 관련 출처만 확인한다.
- 도구로 확인한 현재 사실: 로컬 이력에 실패 분류, UGS 조회, 초대 기능 변경이 존재하고 package.json은 1.13.8이다. 따라서 예전 기록의 진행 중·미반영 상태를 현재 작업으로 이어 쓰지 않도록 요약과 대체 관계를 갱신했다. 코드 동작을 새로 검증한 것은 아니다.
- 보존한 미해결 사항: 컴파일·실계정 네트워크·Play Mode 실행 결과와 과거 미확인 대화 내용. 정리만으로 성공 결과나 새 제품 결정을 추가하지 않는다.
