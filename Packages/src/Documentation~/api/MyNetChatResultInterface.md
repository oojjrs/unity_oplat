# `MyNetChatResultInterface`

참가한 채팅에서 수신한 메시지를 처리한다.

## 멤버

```csharp
void OnReceived(string message, string playerId, string roomId, MyTime sentAt);
```

| 인자 | 설명 |
| --- | --- |
| `message` | 수신한 문자열 메시지 |
| `playerId` | 발신 플레이어 ID |
| `roomId` | 메시지가 속한 방 ID |
| `sentAt` | Oplat이 정한 발송 UTC 시각 |

채팅을 수신하려면 이 처리기를 `MyPlatformInitializer.CallbackInterface.ChatResult`로 제공한다. 생략하면 수신 채팅을 버린다.

Anonymous 원격 채팅은 로컬 서버가 접수한 시각, Steam은 발신자의 Steam 서버 기준 시각, Ugsymous는 발신자의 로컬 기준 시각을 전달한다. Ugsymous 메타데이터에 시각이 없는 메시지는 수신 시각으로 대체한다. 화면에 현지 시각을 표시할 때만 `sentAt.ToLocalDateTime()`으로 변환한다.
