# Bongo Chat

Steam **Bongo Cat** 방 대화를 말풍선으로 모아 보여주는 Windows 보조 앱입니다. C# / WPF 채팅창과 BepInEx 수신 플러그인으로 구성합니다.

![화면 미리보기](docs/assets/preview.png)

## 다운로드와 실행

[최신 테스트 버전 다운로드](https://github.com/RATOY-Jeong/bongo-chat/releases/latest)에서 `BongoChat-0.1.zip`을 받으세요. GitHub의 `Source code` ZIP은 실행용 패키지가 아닙니다.

1. ZIP을 압축 해제하고 봉고캣을 완전히 종료합니다.
2. `Install.cmd`를 실행합니다. 게임 폴더를 자동으로 찾으며, 필요하면 `BongoCat.exe`를 선택합니다.
3. Steam에서 봉고캣을 다시 켜고 방에 들어갑니다.
4. `BongoChat.exe`를 실행합니다. 채팅은 기존 봉고캣에서 입력합니다.

상대방은 설치하지 않아도 됩니다. 제거는 실행용 ZIP의 `Uninstall.cmd`를 실행합니다.

## 저장소 구조

```text
src/
  app/          Windows 채팅창
  bridge/       봉고캣 메시지 수신 플러그인
scripts/        빌드·설치·제거 스크립트
docs/          사용 안내·검사 결과
  assets/       화면 미리보기
licenses/       외부 구성요소의 라이선스와 출처
```

## 빌드

실행용 ZIP의 `runtime` 폴더를 저장소 루트에 복사하고 Windows PowerShell에서 실행합니다.

```powershell
./scripts/Build.ps1
```

설치된 게임의 어셈블리와 .NET Framework C# 컴파일러를 사용하며, 결과는 `build/`에 생성됩니다. 게임 위치를 직접 지정하려면 `-GamePath`를 사용하세요.

소스에서 설치하려면 빌드 후 `./scripts/Install-Bridge.ps1`을 실행합니다. 별도로 압축 해제한 실행용 패키지를 쓰는 경우에는 `-PackagePath`로 그 폴더를 지정할 수 있습니다.

## 안내

- [사용 방법과 기록 위치](docs/USER-GUIDE.md)
- [검사 결과](docs/VALIDATION.md)
- [포함 구성요소와 라이선스](licenses/THIRD-PARTY-NOTICES.md)

초기 테스트 버전입니다. Windows x64 및 Bongo Cat 빌드 25723683에서 실제 방 메시지 기록을 확인했습니다. 기존 모드 로더가 있으면 자동 설치를 중단합니다. 설치 전 대화 복구와 이 창에서의 메시지 전송은 지원하지 않습니다. 비공식 보조 앱입니다.

