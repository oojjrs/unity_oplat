# Anonymous 플랫폼

`Anonymous`는 외부 플랫폼 SDK 없이 로컬 개발과 다중 실행 테스트에 사용하는 구현이다.

## 계정과 프로필

- `Account`는 `SystemInfo.deviceUniqueIdentifier`를 사용하고, 지원되지 않으면 기기 이름과 제품 이름 순으로 대체한다.
- `Nickname`은 기기 이름을 사용하고, 지원되지 않으면 제품 이름으로 대체한다.
- `ProfileSprite`는 패키지의 기본 프로필 이미지를 사용한다.
- `AnonymousInstanceId`가 있으면 공백을 제거한 값을 `Account`와 `Nickname`에 반영해 같은 기기의 실행 인스턴스를 구분한다.

같은 논리 인스턴스에는 실행할 때마다 같은 `AnonymousInstanceId`를 제공해야 한다. 명령행 인자나 테스트 런처 등 값의 출처는 소비 프로젝트가 정한다.

## 저장소

Anonymous 저장소는 현재 Windows 계정의 다음 경로 아래에 파일을 직접 저장한다.

```text
%LOCALAPPDATA%\oojjrs\Oplat\AnonymousStorage\v1\<project-key SHA-256>\<AppId>\users\<account SHA-256>\files\<logical path>
```

Project Key는 `Application.identifier`이며, 값이 없으면 company/product 이름으로 대체한다. 프로젝트, App ID와 Account별로 격리되는 로컬 개발 저장소이며 신뢰할 수 있는 원격 데이터베이스가 아니다.

## 네트워크

Anonymous 네트워크는 `127.0.0.1:45831`의 로컬 서버를 사용한다. 채팅 메시지 한계는 `service.Net.Chat.MessageByteCountMax`에서 조회한다.

플랫폼 공통 API는 [인터페이스 문서](../index.md)를 참고한다.
