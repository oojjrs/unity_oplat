# `MyNetChatServiceInterface`

현재 방의 채팅 채널에 참가하고 문자열 메시지를 주고받는다. 방 참가와 채팅 참가는 별도 작업이다.

## 메서드

| 메서드 | 설명 |
| --- | --- |
| `JoinAsync(JoinConfigInterface, JoinResultInterface)` | 방의 채팅 수신을 시작한다. 성공 시 `OnOk(roomId)`를 호출한다. |
| `SendAsync(SendConfigInterface, SendResultInterface)` | 참가한 채팅에 메시지를 보낸다. 성공 시 `OnOk(roomId)`를 호출한다. |
| `ExitAsync(ExitConfigInterface, ExitResultInterface)` | 채팅 수신을 끝낸다. 성공 시 `OnOk(roomId)`를 호출한다. |
| `MessageByteCountMax` | 현재 플랫폼에서 허용하는 UTF-8 메시지 최대 크기다. |

각 Result 인터페이스는 `MyNetInterface.CatchInterface`를 상속한다.

## Config 인터페이스

| 타입 | 멤버 |
| --- | --- |
| `JoinConfigInterface` | `CancellationToken`, `RoomId` |
| `ExitConfigInterface` | `CancellationToken`, `RoomId` |
| `SendConfigInterface` | `CancellationToken`, `RoomId`, `Message` |

메시지는 비어 있거나 공백일 수 없으며 `MessageByteCountMax`를 넘을 수 없다. 한계는 Anonymous 4096 bytes, Steam 4089 bytes이므로 값을 하드코딩하지 말고 속성에서 조회한다.

수신 메시지는 초기화 때 등록한 [`MyNetChatResultInterface`](MyNetChatResultInterface.md)로 전달된다. Steam의 Chat 작업은 Unity 메인 스레드에서 호출한다.
