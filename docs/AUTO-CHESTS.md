# 0.4.9 — 상자 자동 열기 / Auto-open chests / 宝箱の自動開封

## 한국어

상단 **상자 자동 열기**를 켜면 **일반 상자와 이모티콘 상자 모두** 게임에서 준비됐고 필요한 게임 포인트가 있을 때 하나씩 엽니다. 기본은 꺼짐이며 선택은 저장합니다. 수동으로 열 때와 같은 게임 포인트를 사용하고, 게임의 결과 연출·자동 장착 설정을 그대로 따릅니다.

1. 기존 머지어와 봉고캣을 닫고 0.4.9 EXE를 실행합니다. 연결 플러그인 **0.2.3**이 필요하며 연결 도우미가 기존 소유권을 확인해 교체합니다. 요청되면 **설치하고 게임 재시작**을 누르세요.
2. 연결을 꺼 둔 상태라면 자동으로 복원하지 않습니다. 연결 도우미에서 직접 복원한 뒤 이용합니다.
3. **상자 자동 열기**를 켭니다. 방에 입장하지 않아도 정상 게임 상태와 새 플러그인이 준비되면 동작합니다. 두 종류가 동시에 준비돼도 한 작업이 끝난 뒤 다음 것을 엽니다.
4. 옵션을 끄거나 머지어를 닫으면 최대 6초 안에 새 요청을 멈춥니다. 게임에서 이미 시작한 열기와 게임 자체 재시도는 완료됩니다. 머지어가 멈추거나 갑자기 종료돼도 새 요청 권한은 만료됩니다.

게임의 준비 시간·포인트 검사·상자 버튼 상태를 따릅니다. 타이머·포인트를 고치거나 선물 열기·Steam 마켓·결제 화면을 조작하지 않습니다. 같은 준비 상태에 요청한 상자는 결과가 불확실해도 반복해서 요청하지 않습니다. 자동 재시도 대신 껐다 켜서 다시 요청할 수 있으며, 게임이 이미 다른 상자를 처리 중이면 기다립니다.

게임 구조가 바뀌어 필요한 필드·메서드가 없으면 자동 열기는 사용하지 않습니다. 기존 채팅 기능과는 분리되어 있습니다. 기존 0.2.2 플러그인은 채팅에 사용할 수 있지만 자동 열기는 0.2.3 설치와 게임 재시작이 필요합니다.

### 기록과 검증

사용자 앱 폴더의 `auto-chests.txt`는 표시 선택이며, 게임 폴더 `BepInEx/BongoChat/auto-chests.json`은 최대 5초짜리 요청 권한입니다. 게임 세션이 바뀌거나 파일이 낡거나 잘못되면 요청하지 않습니다. 이 파일들은 배포 ZIP·소스 업로드에서 제외합니다.

373개 격리 검사: 기존 311 + 한글 Enter 13 + 정책·UI 29 + 실제 브리지 어댑터를 모의 게임 API에 연결한 20개. 순차 처리, 포인트 부족, 재고 시간, 수동 열기 중 대기, 반복 방지, 만료·잘못된 세션·큰 파일·끄기/종료·이전 비동기 작업 차단, 세 언어와 좁은 창을 검사했습니다. 설치된 게임 어셈블리는 API 확인·컴파일용으로 읽었으며 실제 게임 파일·포인트·상자는 변경하지 않았습니다. **실제 계정의 개봉·보상 수신은 아직 확인 전**입니다.

## English

Enable **Auto-open chests** to open both normal and emote chests, one at a time, when the game's timer and game-point requirements are met. It uses the same points, animations and auto-equip settings as a manual opening. Default off; the preference is saved. No room membership is required once a fresh compatible game connection is available.

Close the old companion and game and run 0.4.9. Bridge **0.2.3** is required; the helper upgrades owned files and restarts the game when requested. Paused connections remain paused until explicitly restored. Off/exit stops new requests within six seconds; an already-started game operation can finish. Crashed/frozen companions cannot renew the short-lived permission. Uncertain openings are not repeatedly requested; toggle off/on to explicitly retry. Native busy/button/currency/readiness guards remain intact. No timer/currency edits, gifts, marketplace or checkout operations are performed.

Preferences and the game-session-specific five-second lease stay local and are excluded from packages. 373 isolated checks passed, including 29 policy/UI and 20 actual bridge-adapter checks against a simulated game API. Game files were read for API inspection/build only. **Live opening and rewards on a real account are not yet verified.**

## 日本語

**宝箱を自動で開く**をオンにすると、準備時間とゲーム内ポイントが揃った通常・絵文字の宝箱を1つずつ開きます。手動と同じポイントを消費し、ゲームの演出・自動装備設定に従います。初期値はオフで設定を保存します。正常なゲーム接続があればルーム参加は不要です。

旧アプリとゲームを終了して0.4.9を実行してください。接続プラグイン **0.2.3** への更新とゲーム再起動が必要です。接続停止中なら自動で復元しません。オフ/終了で6秒以内に新規要求を止め、処理中のゲーム操作は完了します。アプリ停止時も短い権限が失効します。不確実な開封を繰り返さず、オフ/オンで明示的に再試行できます。ゲームの時間・ポイント・ボタン・処理中判定を守り、贈り物・マーケット・決済を操作しません。

設定と5秒のゲームセッション別権限はローカルのみで配布しません。373件の隔離テストに合格し、29件のポリシー/UIと模擬ゲームAPIで実際の接続アダプターを動かす20件を含みます。ゲーム原本はAPI確認・ビルドのため読んだだけです。**実アカウントの開封・報酬受信は未確認です。**