# UnityOplat 작업 메모리

## 현재 요약

- 2026-11-13T05:59:08+09:00 Steam·Anonymous 초대 대상의 비밀번호 생략 구현과 사용자 요청에 따른 package.json 1.13.8 적용을 마쳤다. 초대 권한은 시간 만료 없이 입장 성공 또는 방 종료까지 유효하다. 기존 코드 정적 검토·형식·공백 검사는 통과했고 컴파일·실접속은 미검증이다. 여섯 파일을 이번 커밋 범위로 확정했다. 근거: R-20261113-invitation-admission-complete, R-20261113-invitation-version-commit.

- 마지막 내용 확인: 2026-11-13T05:28:25+09:00. 사용자가 검토한 코드 전체를 그대로 유지하도록 확인했다. 근거: R-20261113-reviewed-content.
- 공통 FailureEnum의 IncorrectPassword, RoomFull, RoomLocked가 Anonymous·Steam·UGS 입장 실패 콜백까지 전달된다. Steam 통신 프로토콜은 초대 권한 메시지 추가로 5이며 기존 4와 상호 입장하지 않는다.
- UGS는 Lobby 입장 후 Multiplayer 세션 연결을 수행하며, 이전에 설명한 재입장·RoomId 우선·취소·Busy 시점·예외 메시지 동작도 검토된 코드 그대로 유지한다.
- UGS 친구 초대·퇴장·방 갱신·플레이어 갱신은 SDK 사전 키 대신 session.Id로 찾는다.
- 버전 정책: 기존 파일의 동작 수정 두 묶음에 각각 patch를 적용하여 원인별 입장 실패는 1.13.6, UGS 조회 키는 1.13.7이다. 이후 초대 대상 비밀번호 생략 변경은 patch 1.13.8로 적용했다. 현재 package.json은 1.13.8이다.
- 정적 추적·텍스트 형식·diff 공백 검사는 통과했다. 실제 컴파일·플랫폼 접속 결과는 없다.
- 초기 이력 복원은 여전히 진행 중이다. 아래 완료 범위와 남은 출처를 이어 읽어야 하며 파일 존재를 복원 완료로 간주하지 않는다.

## 초기 복원 범위와 진행

- 조사 기록 시각: 2026-11-12T21:17:56+09:00; 기록자 Codex /root; 확인된 hostId local, 작업 경로 H:\UnityOplat. 실제 PC 이름과 다른 호스트 상태는 미확인.
- 저장소: https://github.com/oojjrs/unity_oplat. 프로젝트 ID 5d312bc1-5903-4dae-bd44-2b2a531bdcae.
- local Codex 활성 목록 41페이지 2022건, 보관 목록 1페이지 14건을 마지막 nextCursor=null까지 조회했다. 현재 경로 또는 프로젝트 ID가 일치하는 활성 채팅은 현재 채팅 포함 71건이고 보관 목록에는 일치 항목이 없었다.
- 이전 기록 중 아래 R-20260915-error-report 출처 1건은 본문 전체 페이지를 확인했다. 나머지 69건은 목록만 확인했으며 본문 내용·결정·실행 성공을 추정하지 않았다.
- 다른 경로 H:\Mines, H:\UnityOauth, H:\oojjrs.github.io에도 Oplat 관련 목록 후보가 있었다. 해당 본문에서 저장소와의 관계 및 과거 체크아웃 별칭을 확인하는 단계는 남아 있다. ChatGPT 및 다른 호스트의 관련 기록 조사도 아직 하지 않았다.
- 재개 지점: 아래 본문 미확인 목록부터 read_thread를 호출하고 page.nextCursor가 끝날 때까지 읽는다. 이미 끝낸 활성·보관 목록과 아래 완료한 본문은 변경 증거가 없으면 반복해서 읽지 않는다.
- 이 초기 파일은 현재 요청과 실제 읽은 과거 기록을 보존하는 중간 상태다. 접근 가능한 전체 이력 복원 완료를 주장하지 않는다.

## R-20260915-error-report

- 기록 시각: 2026-11-12T21:17:56+09:00; 기록자 Codex /root; 원 발언자 사용자 및 당시 Codex; 의사결정 주체: 사용자의 확인 질문, 당시 Codex의 진단.
- 출처: [오류 메시지 출력 방식 확인](codex://threads/01a0a26f-6c09-7923-b229-a4aa390093fe); hostId local; cwd H:\UnityOplat.
- 출처 생성 시각 Unix 1789431606, 갱신 시각 1789431664. 개별 발언 시각 불명; 턴 01a0a26f-74ff-7482-9cba-ed5e27168051의 startedAt=1789431608, completedAt=1789431664. 기록 시각과 구분한다.
- 사용자 항목 01a0a26f-8c19-7282-a5b9-853448b5aa8a: 오류 메시지가 뭉뚱그려 나오는 이유와 현재 출력 방식 질문.
- 당시 Codex 최종 항목 msg_033f30862c02f425016aa88f62407887d08a4de61644ff3ff3: Steam·Anonymous가 예외를 MyNetSessionException으로 감싸며 원인은 InnerException에 보존되고, OnFailed는 FailureEnum 분류만 전달한다고 설명했다. UI에서 e.Message만 출력하는지는 호출 프로젝트 확인이 필요하다고 구분했다.
- 이 단락은 과거 설명 기록이다. 과거 도구 출력은 포함하지 않아 당시 실행 결과의 독립 검증 자료로 취급하지 않는다. 이번에는 아래 현재 소스로 입장 실패 경로만 별도 확인했다.

## R-20261112-join-failure

- 기록 시각: 2026-11-12T21:17:56+09:00; 기록자 Codex /root; 원 발언자 사용자; 질문 주체 사용자, 코드 해석 주체 Codex. 상태: 코드 읽기 완료.
- 출처: [현재 채팅](codex://threads/01a1cfae-5fa4-7052-87e1-438ae908ab0e); hostId local; cwd H:\UnityOplat. 현재 턴·사용자 항목 ID 및 정확한 발언 시각은 미제공.
- 사용자 질문: 현재 플랫폼이 비밀번호 오류·정원 초과·잠금 등을 모두 NotPermitted로 반환한다는 말이 맞는지 확인.
- 현재 도구로 확인한 사실: SteamNet.cs:2645가 잠금·정원 도달·비밀번호 불일치를 AdmissionRejected로 처리하고, 1454~1456이 입장 거절을 NotPermitted로 변환한다. 1535~1543에서 Steam의 Full 등도 NotPermitted로 합친다. 1647~1648에서 사전 잠금 검사도 같은 실패를 반환한다. JoinRoomAsync 974~993에서 이를 OnFailed로 전달한다.
- 현재 도구로 확인한 사실: AnonymousServerJoinRoom.cs:43~47에서 잠금·비밀번호 불일치=Forbidden, 정원 도달=Conflict. AnonymousNetRoomService.cs:140~147에서 두 응답을 NotPermitted로 합치고 163~165에서 OnFailed로 전달한다.
- 현재 도구로 확인한 사실: UgsymousNetServices.cs:502~517에서 JoinSessionByIdAsync/JoinSessionByCodeAsync를 호출하고 661~672의 RunAsync가 SessionException을 OnException(MyNetSessionException)으로 전달한다. 입장 실패를 NotPermitted로 매핑하는 코드는 이 경로에 없다.
- 현재 도구로 확인한 사실: MyNetInterface.cs:7~17에 WrongPassword, RoomFull, RoomLocked 같은 개별 FailureEnum은 없다.
- Codex 판단: Steam·Anonymous 기준으로 인용은 맞지만, 세 플랫폼 모두의 반환이 동일하다고 일반화하면 부정확하다. Steam·Anonymous 호출자는 NotPermitted 값만으로 세 원인을 구분할 수 없다.
- 확인 범위: 로컬 소스의 조건·응답 변환·콜백 연결을 정적으로 추적했다. 실제 플랫폼 접속이나 실행 테스트 결과로 확대 해석하지 않는다. 구현 변경은 없으며 이 메모리만 추가했다.

## 남은 본문 출처 색인

목록의 updatedAt은 초 단위 Unix 목록 활동 시각이며, 개별 발언 시각이나 결정의 최신성 순위가 아니다.

- 01a14508-5db3-7073-9e54-996f1389f7bc | 목록 updatedAt=1792160484 | 본문 미확인
- 01a1442e-9226-7f72-bab0-2a5d1acd6e44 | 목록 updatedAt=1792157826 | 본문 미확인
- 01a136e8-9e9b-7a90-93e8-5babc3edc589 | 목록 updatedAt=1791978325 | 본문 미확인
- 01a13568-6330-7380-bb6f-0678588e611b | 목록 updatedAt=1791924327 | 본문 미확인
- 01a136dd-3678-7cc0-b4b0-8b0320c4ad48 | 목록 updatedAt=1791923420 | 본문 미확인
- 01a128e7-5d97-7752-a2d3-bdd6d57d4911 | 목록 updatedAt=1791688311 | 본문 미확인
- 01a12452-dd1b-71c2-8a59-36703ad6518b | 목록 updatedAt=1791611664 | 본문 미확인
- 01a123a9-0e27-7ed2-a833-9811527a15f3 | 목록 updatedAt=1791602475 | 본문 미확인
- 01a12399-3b24-7c82-957d-c9a3ce93eba6 | 목록 updatedAt=1791599222 | 본문 미확인
- 01a12369-8ee3-7330-bd42-d2d697dd15f9 | 목록 updatedAt=1791598084 | 본문 미확인
- 01a0d875-3768-7060-8ed3-8c80f197e944 | 목록 updatedAt=1791178889 | 본문 미확인
- 01a109fd-e42f-71b2-a20e-d5c7645d5763 | 목록 updatedAt=1791169584 | 본문 미확인
- 01a109ab-13a4-7391-ad85-fda033879fc1 | 목록 updatedAt=1791164043 | 본문 미확인
- 01a0d0fa-81bc-7b21-ae0f-a957179b4eff | 목록 updatedAt=1790215967 | 본문 미확인
- 01a0d0fd-e7e3-7ac0-96d6-33c98709d24b | 목록 updatedAt=1790213891 | 본문 미확인
- 01a0d0e7-98d8-78f0-b136-fa61d89b403e | 목록 updatedAt=1790211985 | 본문 미확인
- 01a0c3c9-3fa1-7052-8904-c3fbe18079cf | 목록 updatedAt=1790078741 | 본문 미확인
- 01a0c3a7-2d02-7e51-8b89-201237547280 | 목록 updatedAt=1789989507 | 본문 미확인
- 01a0bd3f-a9a4-73e3-8cba-9be378479c52 | 목록 updatedAt=1789988419 | 본문 미확인
- 01a0beb0-56ea-7573-b403-955c94f94923 | 목록 updatedAt=1789908877 | 본문 미확인
- 01a0be7a-db9c-7103-afc9-17e9ff6ef388 | 목록 updatedAt=1789905384 | 본문 미확인
- 01a0be51-64b6-74a0-8fd9-aee5d76c6f9e | 목록 updatedAt=1789901641 | 본문 미확인
- 01a0be55-f2ba-7d62-b041-dca31f6bfe2b | 목록 updatedAt=1789900208 | 본문 미확인
- 01a0bd63-80fc-7c53-8da8-8e5aeedfa62f | 목록 updatedAt=1789884150 | 본문 미확인
- 01a0bcfa-67e6-7510-801e-bd26b0b33dd1 | 목록 updatedAt=1789883597 | 본문 미확인
- 01a0bd33-8dfb-7e82-a51a-61b8f7eb4809 | 목록 updatedAt=1789881749 | 본문 미확인
- 01a0bcd1-2211-7aa3-8f6b-acab4566b0cb | 목록 updatedAt=1789877280 | 본문 미확인
- 01a09a6e-c3f4-7991-8fdb-5c0c6e99415a | 목록 updatedAt=1789876418 | 본문 미확인
- 01a0bc12-da1d-7832-81ec-32b090d5e6c8 | 목록 updatedAt=1789865581 | 본문 미확인
- 01a0bc18-4cb6-7a20-b68a-510c832953cf | 목록 updatedAt=1789862878 | 본문 미확인
- 01a0bbd8-f7f6-7e02-bd1f-7d3ea4b6aad3 | 목록 updatedAt=1789858319 | 본문 미확인
- 01a0b8df-decf-78d0-847c-679bd62116d5 | 목록 updatedAt=1789814397 | 본문 미확인
- 01a0a26e-b201-7512-ae21-907fe22e84c2 | 목록 updatedAt=1789432202 | 본문 미확인
- 01a09a34-b90e-7013-84c0-26c6f08957bc | 목록 updatedAt=1789293811 | 본문 미확인
- 01a097d1-d045-7e43-ac79-54f016a5e9d6 | 목록 updatedAt=1789270368 | 본문 미확인
- 01a0947c-4386-7222-9152-068e86867677 | 목록 updatedAt=1789198367 | 본문 미확인
- 01a09465-d9fc-7822-aa28-060bda394f54 | 목록 updatedAt=1789196738 | 본문 미확인
- 01a09461-836b-7e43-b59d-8b72405374b2 | 목록 updatedAt=1789195997 | 본문 미확인
- 01a04e7f-ef94-7b10-b5bf-68c9b3c30a9e | 목록 updatedAt=1788071075 | 본문 미확인
- 01a0269e-28bc-7742-b65c-93a64a5b7171 | 목록 updatedAt=1787355225 | 본문 미확인
- 01a02698-f86d-7c30-9c7e-dc0a7d6c2a2b | 목록 updatedAt=1787354216 | 본문 미확인
- 01a0248e-1872-7dc3-a0f8-4628c161c2df | 목록 updatedAt=1787353962 | 본문 미확인
- 01a02445-07d7-7b30-b9ce-d5e3b28c6814 | 목록 updatedAt=1787319471 | 본문 미확인
- 01a00fdc-0952-7cb1-858d-d9a5fe6d2402 | 목록 updatedAt=1786975121 | 본문 미확인
- 01a00f8f-9203-78a2-811f-b81d1ef02b81 | 목록 updatedAt=1786968698 | 본문 미확인
- 01a00f4e-e38d-7242-a638-7b0dbca2b470 | 목록 updatedAt=1786963808 | 본문 미확인
- 01a00df4-a135-7663-854a-81e54d9c8273 | 목록 updatedAt=1786940909 | 본문 미확인
- 01a00d56-c845-7622-b623-cfacd773a257 | 목록 updatedAt=1786933524 | 본문 미확인
- 01a00d15-a977-72d0-b8b8-c8ea1c793938 | 목록 updatedAt=1786930119 | 본문 미확인
- 01a00b14-dd73-7d81-b15c-fa1fa9416a2c | 목록 updatedAt=1786926911 | 본문 미확인
- 01a00d1b-4a9b-7652-9ad1-eca52396397c | 목록 updatedAt=1786926829 | 본문 미확인
- 01a009f0-5e4a-7bc1-8a17-253400376c61 | 목록 updatedAt=1786891959 | 본문 미확인
- 01a0095d-b843-7492-9c18-ad4f0b46b1e2 | 목록 updatedAt=1786881401 | 본문 미확인
- 01a009bf-9ea0-7b01-a4b4-b7951e64f208 | 목록 updatedAt=1786873121 | 본문 미확인
- 01a00994-2906-7d13-9779-32a50105e81e | 목록 updatedAt=1786869905 | 본문 미확인
- 01a00978-1e38-7702-a74d-7d467219b5fc | 목록 updatedAt=1786866615 | 본문 미확인
- 01a00948-0a37-7251-8588-a578ab6a5bf8 | 목록 updatedAt=1786863836 | 본문 미확인
- 01a0093f-fbfe-78d1-90fa-c8f955ebb97a | 목록 updatedAt=1786862018 | 본문 미확인
- 01a00803-c5fd-78e0-abcc-8a2e025dad0c | 목록 updatedAt=1786859633 | 본문 미확인
- 01a0030f-7f35-7933-bd2a-777eca74c88f | 목록 updatedAt=1786844278 | 본문 미확인
- 019ff5be-8f3f-7f90-9072-0cf0d8b026ab | 목록 updatedAt=1786539402 | 본문 미확인
- 019ff562-0763-79f2-8c14-d48cc01fcca2 | 목록 updatedAt=1786534326 | 본문 미확인
- 019ff08f-fbc5-7ec3-a087-e861a8c66a2b | 목록 updatedAt=1786449353 | 본문 미확인
- 019fe831-2acb-7ff0-abc2-7c13c5928a3e | 목록 updatedAt=1786447145 | 본문 미확인
- 019fe5e0-1c2d-7932-8e55-f9d361581d14 | 목록 updatedAt=1786282303 | 본문 미확인
- 019fe64a-68b4-79b2-89c5-fea84258b3b4 | 목록 updatedAt=1786281332 | 본문 미확인
- 019fe61d-0270-74f3-943d-337c239287b6 | 목록 updatedAt=1786275058 | 본문 미확인
- 019fe60d-527d-7e71-bc61-17a0492196c6 | 목록 updatedAt=1786272093 | 본문 미확인
- 019fe5d6-216e-7991-852c-4e4b7a74d217 | 목록 updatedAt=1786268688 | 본문 미확인


## R-20261112-join-failure-implementation

- 기록 시각: 2026-11-12T21:24:54+09:00; 기록자 Codex /root; 원 발언자·수정 결정 주체 사용자, 구현 판단 주체 Codex. 출처 현재 채팅 01a1cfae-5fa4-7052-87e1-438ae908ab0e, hostId local, H:\UnityOplat. 개별 턴 ID와 발언 시각은 미제공.
- 사용자 승인: 원인별 오류 enum을 나누고 세 플랫폼 모두 실제 실패 사유가 콜백까지 전달되게 수정. 이전 확인-only 범위를 이 구현 요청이 대체한다.
- 구현 결정: 기존 enum 수치를 보존하며 IncorrectPassword, RoomFull, RoomLocked 추가. Anonymous 서버 응답과 클라이언트 매핑을 함께 수정. Steam은 P2P·Lobby 제어 메시지 양쪽에 동일 사유를 전달하고 새 wire 형식의 프로토콜 번호를 올린다.
- UGS 현재 SDK 2.3.1 관찰: LobbyConverter.ToSessionException이 세 원인을 SessionError.Unknown으로 합치고 InnerException을 보존하지 않는다. 메시지 추정 대신 LobbyService의 typed 입장 응답을 받고 성공한 Lobby를 ReconnectToSessionAsync로 연결한다. 설치 SDK의 ReconnectSessionOptions.WithNetworkHandler 및 SessionManager.ReconnectAsync 경로를 확인했다. 부분 입장 실패·취소 시 정리와 원래 예외 보존이 필요하다.
- 근거: Packages/src/Runtime의 입장 경로 및 Library/PackageCache/com.unity.services.multiplayer@0a90950aff70/Runtime/Multiplayer/Lobby/LobbyConverter.cs:221, Session/SessionManager.cs:204. Unity 공식 https://docs.unity.com/en-us/mps-sdk/lobby-error-messages 및 https://docs.unity.com/en-us/mps-sdk/join-session.
- 범위: 패키지 일반 C# 수정. 문서 본문·패키지 버전·Git 완료 작업은 현재 단계에 포함하지 않는다. 저장소 연결 Projects 조회 결과는 빈 목록이다. 구현 진행 중이며 실행 성공은 아직 주장하지 않는다.

## R-20261112-join-failure-complete

- 기록 시각: 2026-11-12T21:30:47+09:00; 기록자 Codex /root; 원 요청자 및 승인 주체 사용자, 구현·정적 판단 주체 Codex. 출처 [현재 채팅](codex://threads/01a1cfae-5fa4-7052-87e1-438ae908ab0e), hostId local, H:\UnityOplat. 현재 턴·항목 ID와 개별 발언 시각 불명.
- R-20261112-join-failure-implementation의 계획을 6개 기존 C# 파일에 적용했다. 기존 0~7 FailureEnum 수치는 유지하고 IncorrectPassword=8, RoomFull=9, RoomLocked=10을 추가했다.
- Anonymous: 서버 세부 ResultCodeEnum 추가 및 원인별 응답, JoinAsync의 OnFailed 매핑을 연결했다. 권한 거절과 이전 서버의 일반 응답 fallback은 기존대로 남긴다.
- Steam: 정원 Full 및 잠금 사전 검사 분리, nullable 실패 결과 기반 admission 완료, P2P와 Lobby 제어 메시지에 1바이트 사유 추가. 제어 메시지는 사유를 먼저 완료한 후 ResetSession을 호출한다. 중복 완료가 첫 결과를 덮어쓰지 않는다. 호스트 처리 오류는 별도 wire 표식으로 OnException에 전달하며 연결 실패/방 소멸은 Disconnected/NotFoundRoom으로 전달한다. ProtocolVersion 3→4.
- Steam 근거: Valve EChatRoomEnterResponse, SendLobbyChatMsg, ISteamNetworkingMessages 공식 문서. 상대의 능동 종료는 SessionFailed 콜백을 발생시키지 않는다는 계약을 확인했으므로 제어 메시지 전송 성공 후 기존 ClosePeer 자원 해제 동작을 보존했다.
- UGS: SDK가 Lobby 예외 정보를 버리기 전 실제 JoinLobbyByIdAsync/JoinLobbyByCodeAsync의 타입과 Reason으로 분류한다. 성공 후 ReconnectSessionOptions.WithNetworkHandler(_net.Transport)로 기존 네트워크 처리기를 연결한다. 닉네임·플레이어 필드·visibility는 SDK와 같은 PlayerDataObject 변환으로 유지한다. 입장 전 membership 조회로 신규 가입만 rollback하며, 연결 완료 후 취소 시 새 session을 LeaveAsync한다. 정리 실패는 원래 예외와 AggregateException으로 함께 보존한다. 문구 분석이나 SDK 소스 수정은 없다.
- 정적 추적: MyNetRoomSwitcher가 전달받은 FailureEnum을 그대로 ForwardTo하므로 SwitchAsync에도 세 사유가 전달된다. 기존 외부 callback 시그니처는 유지된다. 반복 완료, 정상 입장, 방 소멸, 연결 실패, timeout·취소와 fallback 경로를 소스 및 설치 SDK 계약으로 확인했다.
- 참고: 현재 SDK의 Sessions 사전은 session type 키를 사용하지만 기존 UGS ExitAsync/UpdateAsync는 roomId 조회를 사용한다. 이번 변경 이전부터 존재하는 별도 문제이므로 범위 밖 코드로 보존했다. 이번 요청은 신규 방 입장 오류 분류이며 해당 별도 수정은 하지 않았다.
- 문서·CHANGELOG 새 문구는 승인 범위가 아니므로 수정하지 않았다. 패키지 버전과 Git 완료 작업은 미수행. 실제 Editor 컴파일·플랫폼 접속은 수행하지 않았다. 다음 단계는 사용자 변경 검토와 필요시 실행 검증이다.

- 검토 결과 기록 (2026-11-12T21:31:33+09:00, Codex /root): 6개 C# 파일과 메모리의 공통 형식 검사가 모두 OK였고 원본 BOM·CRLF를 보존했다. git diff --check도 통과했다. 전체 scoped diff에서 입장 사유 생산·송신·수신·OnFailed 매핑을 확인했고, 마지막으로 Steam의 새 if/else 두 곳에 프로젝트 규약에 맞는 중괄호를 보완했다.

## R-20261112-ugs-session-id-lookup

- 기록 시각: 2026-11-12T21:34:41+09:00; 기록자 Codex /root; 원 발언자·수정 승인 주체 사용자, 구현 범위 판단 Codex. 출처 현재 채팅 01a1cfae-5fa4-7052-87e1-438ae908ab0e, hostId local, H:\UnityOplat. 턴·항목 ID 및 발언 시각 미제공.
- 사용자 지시: UGS 퇴장·갱신의 세션 조회 키 불일치도 수정하고, 앞선 변경량이 큰 이유와 별도 변경점을 설명.
- 현재 근거: 설치 SDK SessionManager.RegisterSession은 Sessions[sessionType]에 등록한다. UgsymousNetServices의 InviteAsync, Room.ExitAsync, Room.UpdateAsync, Player.UpdateAsync가 roomId로 TryGetValue해 존재하는 세션도 못 찾는다.
- 수정 판단: 동일 원인의 네 조회를 Sessions.Values의 session.Id 비교로 교체한다. 캐시나 공통 API를 새로 만들지 않고 각 기존 동작·오류 처리를 보존한다. 이전 기록의 '범위 밖으로 보존' 상태를 이번 명시적 수정 승인이 대체한다.
- 설명할 기존 변경: 6개 C# 파일 +210/-37. Steam의 양쪽 거절 경로, nullable admission 결과, protocol 4, 연결 단절·방 소멸·호스트 오류 분류. UGS의 Lobby 입장/Multiplayer 재연결, 사전 membership 조회와 실패·취소 정리, 같은 방 재입장 즉시 성공, RoomId 우선, lifetime 취소 연동, Busy 해제 후 콜백, 추가 typed 오류 매핑. 새 CodexWorkMemory.md는 프로젝트 규칙에 따른 별도 작업 기록이며 런타임 기능이 아니다. 앞선 최종 응답이 부수 동작을 모두 설명하지 못한 점을 사용자에게 명시한다.

- 적용 결과 (2026-11-12T21:35:05+09:00, Codex /root): UgsymousNetServices.cs의 네 TryGetValue 조회를 Values.FirstOrDefault(value => value.Id == roomId)와 null 판정으로 대체했다. 이번 추가 코드 변경은 이 파일 +8/-4이며, 기존 실패·권한·성공 콜백 분기는 보존했다. 사용자에게 앞선 작업의 규모·통신/입장 경로 변경 및 추가 동작을 구분해서 보고한다.

- 검증 결과 (2026-11-12T21:36:04+09:00, Codex /root): 네 수정 hunk를 SDK의 Sessions[sessionType] 등록과 대조했고, Ugsymous 범위에 Sessions.TryGetValue 사용은 남지 않았다. 변경 C#과 메모리의 형식 검사는 OK, git diff --check 통과. 실제 컴파일·접속은 수행하지 않았다. 추가 설명 대상에는 UGS 기타 예외의 바깥 메시지를 일반 문구로 감싸고 원본을 InnerException에 보존하는 변경도 포함한다.

## R-20261113-scope-review

- 기록 시각: 2026-11-13T05:22:15+09:00; 기록자 Codex /root; 원 발언자 사용자; 평가 주체 Codex. 출처 현재 채팅 01a1cfae-5fa4-7052-87e1-438ae908ab0e, hostId local, H:\UnityOplat. 사용자 턴·항목 ID 및 정확한 발언 시각 미제공.
- 사용자 질문: 필요한 일만 수행한 것이 맞는지 재검토.
- 관찰: 현재 diff는 6개 C# 파일 +218/-41이며 앞선 구현·조회 키 수정이 유지되어 있다. 이번 검토에서 C#은 수정하지 않았다.
- Codex 판단: enum 원인 분리, 실제 서버/통신 사유 보존과 콜백 매핑, 후속 승인된 UGS session.Id 조회는 요청에 필요하다. SDK가 원인을 소실시키는 UGS 경계 대응도 필요하지만, 선택한 Lobby 직접 입장→Multiplayer 연결 경로 자체를 유일한 최소 해법이라고 단정할 수 없다. 그 경로의 부분 실패 정리는 경로 변경에 딸린 필요한 처리다.
- Codex 판단: UGS 같은 방 재입장 즉시 성공, RoomId 우선 전환, Busy 해제 후 콜백, 기타 예외의 바깥 메시지를 일반 문구로 교체한 것은 세 원인 분리에 필수적이지 않다. 특히 바깥 메시지 일반화는 상세 원인 전달 취지에 맞지 않는 부수 변경이다. 이전 보고가 이를 충분히 구분하지 못했다.
- 상태: 필요한 부분과 별도 개선을 섞은 것으로 평가하며 해당 부수 변경의 축소가 타당하다는 Codex 의견이다. 사용자의 현재 발언을 코드 되돌림 승인으로 해석하지 않았고 구현은 보존했다.

## R-20261113-reviewed-content

- 기록 시각: 2026-11-13T05:28:25+09:00; 기록자 Codex /root; 원 발언자·내용 결정 주체 사용자, 분리 판단 주체 Codex. 출처 현재 채팅 01a1cfae-5fa4-7052-87e1-438ae908ab0e, hostId local, H:\UnityOplat. 개별 발언 시각과 턴·항목 ID는 미제공.
- 사용자는 각 부수 수정 이유를 설명받은 뒤 전체 내용을 검토했으며 코드를 그대로 유지하라고 확인했다. 따라서 UGS 예외 바깥 메시지를 되돌리겠다는 Codex의 후속 제안은 적용하지 않는다. 실제로 해당 소스의 추가 편집은 수행하지 않았다.
- 이전 R-20261113-scope-review의 축소 권고는 Codex의 검토 의견이며, 현재 구현 내용을 보존한다는 사용자의 최신 확인이 이후 작업 기준이다. 사용자 확인 이전의 근거와 판단은 원기록에 남긴다.
- 기능별 완결 단위는 (1) 공통 enum과 세 플랫폼 입장 실패 사유 전달, (2) UGS 네 서비스의 방 ID 세션 조회 수정이다. 공통 enum만 또는 플랫폼 송신/수신 한쪽만 분리하면 일관된 실패 계약이 완성되지 않으므로 첫 묶음에 함께 둔다. 두 번째 묶음은 입장 오류 변경과 독립된 기존 결함 수정이다.
- 사용자 패키지 버전 규칙의 기존 파일 동작 변경=patch에 따라 두 묶음은 1.13.6과 1.13.7로 구분한다. 원본 인코딩·개행을 보존하며 README나 CHANGELOG의 새 문구는 추가하지 않는다.
- 범위 확인: 기존 여섯 C# 파일 전체, package.json 버전, 관련 CodexWorkMemory.md 기록만 포함한다. 관계없는 변경이나 새 기능은 추가하지 않는다. 초기 이력 복원의 미확인 범위는 여전히 미완료다.

## R-20261113-invitation-admission-request

- 기록 시각: 2026-11-13T05:52:05+09:00; 기록자 Codex /root; 원 발언자·수정 요청 주체 사용자, 범위 해석 Codex. 현재 출처 [채팅](codex://threads/01a1d186-ba65-77f2-81e2-4440a83b1791), hostId local, cwd H:\UnityOplat. 현재 턴·항목 ID와 정확한 발언 시각은 미제공.
- 사용자 요청: Mines의 연결된 채팅에서 필요하다고 정리한 Oplat 기능을 이 저장소에서 처리. 참조 채팅 01a1c4ef-3255-7540-802f-ca00bdf2bd3b의 사용자 항목 01a1d183-f833-72b1-bee3-f8eded25466e 및 Codex 최종 항목 msg_004917ffc79d0c26016af6266137ac87d0899f7c6876a61b6e를 확인했다. 정확한 원 발언 시각은 미확인.
- 확인한 소비 요구: 기존 원인별 실패 구분은 이미 사용처에 반영됐으며, 현재 필요한 것은 Steam·Anonymous에서 호스트/서버가 방과 계정별 유효한 초대 기록을 확인해 비밀번호 검사만 생략하는 기능이다. 일반 참가자의 초대도 호스트가 확인해야 한다. 정원·잠금·방 소멸과 일반 입장 비밀번호 검사는 유지한다. 성공 시 초대를 소모하고 발송 실패·방 종료 시 정리한다.
- Codex 작업 판단: 기존 InviteAsync·JoinAsync·SwitchAsync 공개 시그니처를 유지하고 서버 권한으로 판정한다. 초대 만료 기간과 Steam UI 직접 발송 지원 범위는 현재 소비 기록에서 미확정이며 현 구현·공식 계약을 보고 최소 범위를 결정한다. UGS에 동일 기능을 임의로 확장하지 않는다. 문서 본문·버전·Git 완료 작업은 아직 범위에 포함하지 않는다.
- 시작 근거: Git working tree는 깨끗했고 package.json 1.13.7, origin=https://github.com/oojjrs/unity_oplat. 연결된 Projects 조회는 빈 목록이다. 초기 역사 복원 미완료 상태와 기존 출처 색인은 보존하며 이번에는 연결된 소비 채팅의 최신 6개 턴 본문을 확인했다.

## R-20261113-invitation-lifetime

- 기록 시각: 2026-11-13T05:53:25+09:00; 기록자 Codex /root; 원 발언자·결정 주체 사용자. 현재 채팅 01a1d186-ba65-77f2-81e2-4440a83b1791, hostId local, H:\UnityOplat. 질문 도구 call_65c738e9abce46bc921129031f962404의 0번 답변이며 정확한 발언 시각은 미제공.
- 사용자 확정: 초대 권한은 시간 제한 없이 해당 방이 유지되는 동안 유효. 입장 성공 시 1회 소모하고 방 종료 시 모두 정리하는 조건은 공통이다. R-20261113-invitation-admission-request의 만료 기간 미확정을 해소하며 10분 기본안은 채택하지 않는다.
- Codex 범위 판단: Oplat InviteAsync 발송 경로의 초대를 지원한다. Steam UI 직접 발송은 발신자의 Oplat에서 확인할 수 있는 계약이 없으므로 이번 자동 허용 대상에서 제외하고 기존 비밀번호 검사를 유지한다. 일반 참가자 초대는 본인이 Steam 초대를 발송해 초대한 계정 정체성을 보존하고, 호스트 승인 후 발송하도록 연결한다.

## R-20261113-invitation-protocol

- 기록 시각: 2026-11-13T05:54:41+09:00; 기록자·구현 판단 주체 Codex /root. 현재 채팅 01a1d186-ba65-77f2-81e2-4440a83b1791, hostId local, H:\UnityOplat. 사용자 요청과 R-20261113-invitation-lifetime 안에서의 구현 결정이다.
- Steam은 초대한 참가자의 계정으로 초대를 발송한다. 호스트에 대상 계정의 대기 기록을 먼저 승인받고, Steam 발송 성공을 전달받은 호스트만 실제 초대 권한을 확정한다. 대기 기록만 있는 계정의 비밀번호 불일치 입장은 발송 확정/취소까지 대기해 초대-입장 순서 경합을 처리한다. 잠금·정원은 우선 검사한다. 실패·취소·초대자의 연결 종료는 대기 기록을 정리하며 확정된 초대는 입장 성공 또는 방 종료까지 유지한다.
- 기존 OperationGate와 방 ID·epoch 경계 및 개별 요청 Guid를 사용하고 새 메시지 교환에 맞춰 Steam protocol을 4에서 5로 올린다. 기존 protocol 4와 새 방의 상호 입장은 지원하지 않는다. InviteAsync 결과는 호스트 확인까지 기다리며 외부 시그니처는 바뀌지 않는다.
- Anonymous는 기존 서버 인증 session.Account와 RoomSecret 수명에 계정 집합을 보관한다. 초대 메시지 큐의 Send 성공 직후 기록하고 정상 입장 시 소모한다. 서버 자체의 방 제거로 기록도 사라진다.
- 공식 근거: Valve ISteamMatchmaking.InviteUserToLobby는 요청 성공만 반환하고 실제 수신/수락을 보장하지 않으며 GameLobbyJoinRequested_t는 친구 참가와 초대를 구분하지 않는다. 직접 Steam UI 초대는 범위 밖이다. 공개 문서 새 문구·버전은 이 단계에서 수정하지 않는다.

## R-20261113-invitation-admission-complete

- 기록 시각: 2026-11-13T05:57:30+09:00; 기록자·구현 및 정적 검토 주체 Codex /root, 원 요청·유효기간 결정 주체 사용자. 출처 [현재 채팅](codex://threads/01a1d186-ba65-77f2-81e2-4440a83b1791), hostId local, H:\UnityOplat. 현재 턴·항목 ID와 정확한 사용자 발언 시각 미제공.
- 실제 변경: SteamNet.cs, AnonymousServer.cs, AnonymousServerRoom.cs, AnonymousServerJoinRoom.cs의 기존 네 C# 파일에 초대 권한을 구현했다. 공개 InviteAsync·JoinAsync·SwitchAsync 시그니처와 기존 일반 비밀번호·잠금·정원 검사는 유지한다. 초대 권한은 시간 제한 없이 방 수명 동안 유지하며 성공한 입장에서 소모한다. 동일 계정에 여러 번 초대해도 여러 번 입장할 권한으로 누적하지 않는다.
- Steam: 일반 참가자의 초대는 방 ID·epoch와 Guid로 상관관계를 맞춰 호스트가 승인한다. 초대 발송 주체는 기존 참가자로 유지한다. 발송 확인 전 대기 기록은 비밀번호 생략 권한이 아니며, 먼저 도착한 입장은 발송 확정 또는 취소 후 재판정한다. 취소·발송 실패·초대자 퇴장은 대기 기록을 정리하고 방 종료는 모든 기록을 지운다. 확정된 초대는 초대자의 이후 퇴장만으로 없어지지 않는다. InviteAsync가 호스트의 확인까지 기다리므로 다른 방 작업과 겹치면 기존 OnBusy 계약을 사용한다. Steam protocol은 5다.
- Anonymous: 서버가 인증된 초대자 방 소속과 대상 계정을 확인한 뒤 알림 큐 등록에 성공하면 RoomSecret의 계정 집합에 기록한다. 성공한 플레이어 추가에서 소모하고 RoomSecret 제거 시 함께 정리한다. Steam API의 발송 성공과 Anonymous 큐 등록 성공은 실제 상대 수신·수락을 보장한다는 뜻이 아니다.
- 검토·검증: 전체 C# diff와 실제 소비 호출 형태, 새 메시지 송수신·구문·취소·상관관계·방 정리·비초대/잠금/정원 분기를 정적으로 대조했다. 첫 5파일 공통 형식 검사와 diff --check는 통과했다. 입장 후처리 예외로 기존 로스터가 롤백되는 경우 초대 권한을 잃지 않도록 Steam 소모 위치를 후처리 성공 뒤로 조정했다. 해당 최종 코드의 형식·공백 검사와 소모 위치 재확인도 통과했다. 컴파일·실계정 네트워크·Play Mode 검증은 수행하지 않았다.
- 범위·제한: Oplat InviteAsync를 거친 Steam·Anonymous 초대만 지원한다. Steam UI 직접 발송과 UGS 동작은 변경하지 않았다. Steam protocol 4와 5는 상호 입장하지 않는다. package.json은 1.13.7이며 버전 결정, 문서/CHANGELOG 새 문구, 스테이징·커밋은 수행하지 않았다. 구현과 관련 메모리는 미커밋 상태다.
- 종료 대조: 사용처 요청·사용자 수명 결정·구현·검증 한계·호환성 변경을 현재 요약에 반영했다. 다음은 사용자 코드 검토와 사용처의 새 패키지 소비다. 전체 과거 이력 초기 복원은 기존 색인 그대로 미완료이며 이번 최신 소비 요구 확인을 전체 이력 복원으로 확대하지 않는다.

## R-20261113-invitation-version-commit

- 기록 시각: 2026-11-13T05:59:08+09:00; 기록자 Codex /root; 원 발언자·버전 변경 및 커밋 승인 주체 사용자, patch 분류와 범위 판단 Codex. 출처 [현재 채팅](codex://threads/01a1d186-ba65-77f2-81e2-4440a83b1791), hostId local, H:\UnityOplat. 사용자 발언은 “버전 올리고 커밋해”이며 턴·항목 ID와 정확한 발언 시각은 미제공.
- 버전 결정 및 실제 수정: 독립적으로 소비되는 기능 파일 추가 없이 기존 네 파일의 입장·초대 동작을 변경했으므로 oojjrs-unity-package-release 규칙에 따라 patch 1.13.7→1.13.8을 적용했다. Steam wire protocol 5 변경을 포함하지만 공개 사용 방식과 API는 유지한다. manifest와 lock은 file:src 참조이며 README 설치 URL에도 고정 버전이 없어 연동 수정은 필요하지 않다.
- 최종 후보 범위: 앞서 보고한 네 C# 파일, Packages/src/package.json, 이 작업의 CodexWorkMemory.md 기록을 하나의 원자적 커밋 대상으로 확정한다. 현재 index는 비어 있었고 working tree의 다섯 변경 경로는 기존 보고 범위와 일치했다. 저장소 연결 보드 없음은 이 채팅의 조회 결과를 재사용한다.
- 누락 대조: 소비 요구, 시간 만료 없음·1회 소모, 일반 참가자의 호스트 확인, 발송 순서 경합·실패 정리, Steam protocol 4와의 비호환, 직접 Steam UI 초대 제외 및 컴파일·실접속 미검증을 이전 기록과 대조했다. 코드 내용은 직전 검토 결과를 그대로 유지한다. 버전·메모리의 형식과 최종 staged diff를 확인한 뒤 “초대 대상 비밀번호 생략 추가”로 커밋한다. 이 기록은 커밋 전 범위 확정이며 성공이나 식별자를 미리 주장하지 않는다.
