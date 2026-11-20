# 0.4.12 — 게임 업데이트 후 입장 오류 / Joining rooms / 入室エラー

## 한국어

증상: 게임에서는 방 입장에 실패하지만 머지어는 연결됨으로 표시됩니다. 연결 기능을 꺼 게임을 다시 시작하면 입장이 되고, 이전 연결 환경을 복원하면 재발했습니다.

이 보고 PC의 게임 빌드26784007/Unity6000.2.8에서 입장 콜백 `DiscordMultiplayerActivity.OnLobbyEntered`가 `TypeLoadException: VTable setup of type Newtonsoft.Json.Linq.JObject failed`로 중단되는 것을 확인했습니다. 게임의 축소된 Json.NET13.0.2에는 일부 인터페이스 구현이 제거돼 있지만 연결 환경은 전체 Mono 시스템 라이브러리를 사용합니다. 둘을 섞으면 `JObject`/`JContainer` 타입 로딩이 실패하고 후속 방 입장 처리가 중단될 수 있습니다. Steam 방 정보 자체는 남아 기존 머지어가 연결됨으로 판단했습니다.

0.4.12는 공식 NuGet의 같은 계열 **Newtonsoft.Json13.0.2 / lib/net45** 전체 DLL을 연결용 `BepInEx/unstripped`에 포함합니다. 게임의 원본 `Managed` 파일은 수정하지 않습니다. 플러그인0.2.5는 준비 완료 표시 전에 실제 JObject 생성과 인터페이스 호환성을 검사하며 실패하면 연결 오류로 보고합니다. 일반 재연결은 중단된 연결을 복원하지 않습니다.

**적용:** 기존 머지어를 완전히 닫고 0.4.12 EXE를 실행하세요. 중단 상태면 **연결 복원하고 게임 재시작**, 업데이트 안내면 **설치하고 게임 재시작**을 누르세요. 방에 입장해 채팅을 확인합니다. 필요한 JSON 라이브러리와 새 플러그인은 EXE에 포함돼 있습니다. 대화·사용자 설정·바뀐 외부 파일은 기존 소유권 규칙에 따라 보존합니다.

**검증:** 기존448 + 새15 = **463개 격리 검사** 통과. 현재 게임의 축소 JSON은 별도 CLR 검사에서 인터페이스 구현 누락을 재현했고, 공식 전체 라이브러리에서는 같은 검사·JObject/JArray·한글/일본어 JSON 처리가 통과했습니다. 설치·이전 구성 업그레이드·원본/기록 보존·중단 상태 재연결을 검사했습니다. 사용자에게 새 실행파일을 제공해 **실제 방 입장과 채팅 수신 정상 동작**을 확인했고, 재시작 후 현재 Player.log의 JSON 타입 오류와 예외는0건이었습니다. 이는 모든 향후 게임 업데이트나 다른 PC의 검증을 뜻하지 않습니다. 상자 선물/타블렛의 새 실기 검증은 하지 않았습니다.

## English

The reporting user's game could not enter a room while the companion still showed Connected. Disabling the connection runtime allowed entry; restoring the older runtime reproduced the failure. Game build26784007's Discord lobby callback failed loading stripped `Newtonsoft.Json.Linq.JObject` against the full Mono system interfaces bundled for the loader.

0.4.12 adds the official full **Json.NET13.0.2 net45** DLL under `BepInEx/unstripped`, preserving original game Managed files. Bridge0.2.5 checks JObject parsing/interface compatibility before reporting ready. Ordinary Reconnect no longer restores an intentionally paused loader.

Close the old companion and run0.4.12. Use **Restore connection & restart game** if paused, or **Set up & restart game** when requested. All required files are embedded. **463 isolated checks passed. The reporting user confirmed actual room entry and chat reception, and the new game log contained no JSON type errors/exceptions.** Future game updates and unrelated hardware issues remain separate.

## 日本語

接続アプリは接続済みでも、ゲームのDiscord入室コールバックが縮小版Json.NETと完全版Monoインターフェイスの不整合で停止していました。接続停止で復旧し、旧環境の復元で再発することを報告PCで確認しました。

0.4.12は公式の完全版 **Json.NET13.0.2 net45** を接続用フォルダーへ同梱し、ゲーム原本を保持します。0.2.5はJObjectの互換性を確認してから準備完了を表示します。通常の再接続は停止中の接続を復元しません。

旧アプリを終了し0.4.12 EXEを実行し、停止中なら**接続を復元してゲームを再起動**、更新が必要なら**セットアップしてゲームを再起動**を実行してください。**隔離テスト463件合格。報告ユーザーの実PCで入室・チャット受信と新ログの例外0件を確認しました。** 将来のゲーム更新や他のハードウェアの検証とは区別します。

## Dependency provenance

Official package: https://www.nuget.org/packages/Newtonsoft.Json/13.0.2 (MIT).

Package SHA256: `112CA3B7F47BCBD743BEFCF949FB68CE4F9EFF73AD9F7F1B39C2C61A1EBD3ADD`.

Unmodified `lib/net45/Newtonsoft.Json.dll` SHA256: `C5C83BBC1741BE6FF4C490C0AEE34C162945423EC577C646538B2D21CE13199E`.

Run `scripts/Check.ps1` after building for the isolated suite. `tests/JsonRuntimeProbe.cs` calls the production startup compatibility check in a clean process. Game originals and private logs are not redistributed.