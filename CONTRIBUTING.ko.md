# EasyClipStash 기여 안내

관심 가져주셔서 감사합니다. 이 프로젝트는 의도적으로 범위를 좁게 유지하는 작은 도구입니다. 그래서 코드를 쓰기 전에 **아이디어가 범위에 맞는지 확인**하는 것이 가장 유용합니다.

[English](CONTRIBUTING.md) | **한국어**

## 시작하기 전에

[이 도구가 하지 않는 것](README.ko.md#이-도구가-하지-않는-것)을 읽어주세요. 화면 캡처, 이미지 편집, 화면 녹화, 업로드, 클립보드 기록 관리는 모두 의도적으로 범위 밖입니다. 목표는 한 가지를 잘 하는 것 — 클립보드에 있는 것을 파일로 저장하기입니다.

범위에 맞는지 확신이 안 서면 이슈나 [디스커션](https://github.com/JeongJongMun/EasyClipStash/discussions)을 먼저 열어주세요. 거절될 PR을 작성하는 것보다 빠릅니다.

## 도울 수 있는 일

- **버그 신고** — Windows 버전, 앱 버전(트레이 아이콘에 마우스를 올리면 보입니다), 재현 절차를 적어주세요.
- **기능 제안** — 떠올린 해결책만이 아니라 겪은 문제를 함께 적어주세요.
- **문서 개선** — 오타, 애매한 문장, 번역 수정 모두 환영합니다.
- **풀 리퀘스트** — 아래를 참고하세요.

## 개발 환경

**Windows**와 **.NET 10 SDK**가 필요합니다. WinForms를 쓰기 때문에 Linux나 macOS에서는 빌드할 수 없습니다.

```bash
git clone https://github.com/JeongJongMun/EasyClipStash.git
cd EasyClipStash
dotnet build -c Release
```

실행:

```bash
.\bin\Release\net10.0-windows\EasyClipStash.exe
```

이 앱은 창이 없습니다 — 시스템 트레이에서 시작합니다. 트레이 아이콘을 더블클릭하면 설정 창이 열립니다.

설정은 실행 파일과 같은 폴더의 `config.json`에 기록됩니다. 이 파일을 지우면 새로 설치한 상태가 됩니다.

릴리스가 실제로 배포하는 형태를 재현하려면:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

## 파일 구성

| 파일 | 역할 |
|---|---|
| `Program.cs` | 진입점, 중복 실행 방지 |
| `TrayApplicationContext.cs` | 트레이 아이콘, 메뉴, 알림 |
| `ClipboardSaver.cs` | 클립보드를 읽어 파일로 씀 |
| `FileNamer.cs` | 이름 규칙에 따라 다음 파일명을 만듦 |
| `AppConfig.cs` | `config.json` 로드/저장, 기본값, 마이그레이션 |
| `SettingsForm.cs`, `NamingPanel.cs` | 설정 창 |
| `Theme.cs`, `DarkComboBox.cs`, `DarkInputs.cs` | 다크 테마 |
| `Localization.cs` | 사용자에게 보이는 모든 문자열 |
| `Updater.cs` | 업데이트 확인, 내려받기, 검증, 자기 교체 |
| `HotkeyManager.cs`, `StartupManager.cs` | 전역 단축키, 시작 프로그램 등록 |

## 놓치기 쉬운 것

**사용자에게 보이는 모든 문자열은 `Localization.cs`에 두 언어로 들어갑니다.** `L` 클래스가 `L.Current`에 따라 한국어나 영어를 돌려줍니다. 문자열을 추가하면 두 언어를 모두 넣어주세요 — 번역이 빠진 것은 버그입니다.

**단일 파일 배포가 계속 동작해야 합니다.** 일반 빌드는 통과하는데 단일 파일 빌드만 깨지는 경우가 있습니다(임베드 리소스, 트리밍, 네이티브 라이브러리). CI가 publish 명령을 함께 돌리는 이유입니다.

**자동 업데이트는 실행 파일 이름과 릴리스 자산에 의존합니다.** 릴리스 zip 안의 `EasyClipStash.exe`를 찾고, 내려받은 파일을 발행된 SHA256과 대조합니다. 실행 파일 이름을 바꾸거나 릴리스 패키징 방식을 바꾸면 기존 사용자의 업데이트가 깨집니다.

**아직 테스트가 없습니다.** `FileNamer`는 순수 로직이라 테스트를 시작하기 좋은 지점입니다. 테스트가 생기면 CI가 이름 규칙 회귀를 잡아줍니다.

## 커밋 메시지

한 줄, 타입 뒤는 소문자, 마침표 없이:

```
타입 : 짧은 설명 (명령형)
```

이 저장소에서 쓰는 타입:

| 타입 | 용도 |
|---|---|
| `feat` | 사용자가 알아챌 수 있는 새 동작 |
| `fix` | 버그 수정 |
| `docs` | README, 이 문서, 주석만 바뀐 변경 |
| `ci` | 워크플로 |
| `refactor` | 동작 변화 없는 구조 정리 |
| `change` | 기존 동작이나 기본값의 의도적 변경 |

이력에 있는 예시:

```
feat : split tray folder menu into image and text
fix : use /assets/img as default markdown url path
ci : add build workflow for main and pull requests
```

## 풀 리퀘스트

1. `main`에서 브랜치를 따세요.
2. 한 PR에 한 가지 관심사만 담아주세요.
3. 로컬에서 빌드하고 **실제로 앱을 띄워 해당 동작을 확인**해주세요. GUI 도구라서 빌드가 되는 것이 기능이 동작한다는 뜻은 아닙니다.
4. 무엇을 바꿨고 어떻게 확인했는지 적어주세요.
5. CI가 통과해야 합니다. 빌드와 단일 파일 publish를 검증합니다.

수정하는 파일의 스타일(들여쓰기, 이름 짓기, 주석 밀도)에 맞춰주세요. 기존 주석은 한국어인데 새 주석은 어느 언어든 괜찮습니다. 한 파일 안에서는 일관되게 해주세요.

## 릴리스

릴리스는 메인테이너가 발행합니다. `v*` 태그를 푸시하면 워크플로가 빌드·패키징·SHA256 발행·GitHub 릴리스 생성까지 처리합니다. 버전은 태그에서 가져오므로 `EasyClipStash.csproj`를 고칠 필요가 없습니다.

## 라이선스

기여하시면 그 작업물이 [MIT 라이선스](LICENSE)로 배포되는 것에 동의하는 것으로 봅니다.
