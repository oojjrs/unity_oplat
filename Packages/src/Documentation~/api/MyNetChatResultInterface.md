# `MyNetChatResultInterface`

참가한 채팅에서 수신한 메시지를 처리한다.

## 멤버

```csharp
void OnReceived(string message, string playerId, string roomId);
```

| 인자 | 설명 |
| --- | --- |
| `message` | 수신한 문자열 메시지 |
| `playerId` | 발신 플레이어 ID |
| `roomId` | 메시지가 속한 방 ID |

이 처리기는 `MyPlatformInitializer.CallbackInterface.ChatResult`로 제공하며 플랫폼 서비스가 살아 있는 동안 유효해야 한다.
