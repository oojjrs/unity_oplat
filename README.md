# unity_oplat

여러 PC 배포 플랫폼을 동일한 게임 코드에서 사용할 수 있도록 공통 계약과 플랫폼별 Unity 어댑터를 제공한다.

## 패키지

- `com.oojjrs.oplat`: 플랫폼 공통 계약과 Null/Fake 구현
- `com.oojjrs.oplat.steam`: Steamworks.NET 기반 Steam 구현

저장소 안의 각 패키지는 독립적으로 설치한다. Steam 패키지를 사용할 프로젝트는 코어와 Steamworks.NET도 프로젝트 `Packages/manifest.json`에 함께 선언해야 한다.

```json
{
  "dependencies": {
    "com.oojjrs.oplat": "https://github.com/oojjrs/unity_oplat.git?path=/Packages/Core",
    "com.oojjrs.oplat.steam": "https://github.com/oojjrs/unity_oplat.git?path=/Packages/Steam",
    "com.rlabrecque.steamworks.net": "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.0"
  }
}
```

Unity Package Manager는 Git 패키지의 `package.json`에서 다른 Git 패키지를 자동 설치하는 전이 의존성을 지원하지 않는다. `com.oojjrs.oplat.steam`의 `package.json`에는 필요한 패키지 버전을 명시하지만, Git URL은 소비 프로젝트의 `manifest.json`에 직접 선언한다.

Steamworks.NET Git 태그 `2025.164.0` 내부의 UPM 패키지 버전은 `2025.163.0`이므로 Steam 어댑터의 패키지 의존성은 `2025.163.0`으로 선언한다.

## 스토어별 빌드

이 저장소가 모노레포여도 소비 프로젝트에는 대상 스토어 패키지만 설치한다. Git URL의 `?path=/Packages/Steam`은 Steam 패키지만 가져오며, 이후 추가될 Epic·STOVE 패키지를 함께 포함하지 않는다.

- 공통 게임 assembly는 `com.oojjrs.oplat` 계약만 참조한다.
- Steam 조립 assembly는 `OOJJRS_STORE_STEAM` Build Profile define이 있을 때만 컴파일하고 `com.oojjrs.oplat.steam`을 참조한다. 에디터에서는 플랫폼 테스트를 위해 항상 컴파일된다.
- Epic·STOVE도 각각 별도 assembly와 Build Profile define을 사용한다.
- 플랫폼 SDK의 native plugin까지 빌드에서 확실히 제외하려면 스토어별 `manifest.json` 또는 빌드 진입 스크립트로 해당 어댑터 패키지만 설치한다. Build Profile define만으로 프로젝트 전체 manifest의 native plugin이 제거되지는 않는다.

따라서 여러 어댑터를 게임 manifest에 항상 전부 선언하지 않는다. 예를 들어 Steam 산출물은 Core, Steam, Steamworks.NET만 설치한 manifest와 Steam Build Profile을 함께 사용한다.

Steam 구현은 게임에서 App ID를 주입받고 플레이어 실행 시 Steam을 통한 재실행 여부를 먼저 검사한다. 프로세스 전역 Steam API 소유자는 하나만 허용하며, 상세 수명 주기와 ticket 사용법은 Steam 패키지 문서에 명시한다.
