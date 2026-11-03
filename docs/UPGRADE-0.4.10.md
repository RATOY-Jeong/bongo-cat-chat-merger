# 0.4.10 — 연결 중 플러그인 업그레이드 / Live bridge upgrade / 接続中の更新

## 한국어

0.4.9에서 게임이 실행 중이고 채팅 상태가 최신이면 설치 검사를 건너뛰었습니다. 따라서 채팅용 플러그인 0.2.2가 계속 실행돼 상자 자동 열기에 필요한 0.2.3이 적용되지 않을 수 있었습니다. 기존 연결 상태를 읽는 과정이 재시작 안내까지 지웠습니다.

이제 설치 파일의 소유권·해시를 먼저 검사한 뒤 실행 중인 플러그인 버전도 확인합니다. 오래된 플러그인이거나 교체할 파일이 있으면 **설치하고 게임 재시작**을 표시하며, 이전 플러그인의 연결 신호가 와도 안내를 유지합니다. 디스크의 DLL을 교체했더라도 게임이 이전 DLL을 실행 중이면 재시작을 요청합니다. 최신 파일과 최신 실행 버전이 모두 확인되면 재시작하지 않습니다.

기존 머지어를 닫고 0.4.10 EXE를 실행하세요. 게임이 켜져 있으면 **설치하고 게임 재시작**을 누르세요. 게임이 꺼져 있으면 필요한 파일을 먼저 설치한 뒤 게임을 실행합니다. 새 플러그인 파일은 EXE에 포함돼 별도로 찾을 필요가 없습니다. 연결을 꺼 둔 설정은 그대로 유지합니다.

상자 옵션을 켰을 때 구버전이면 연결 준비 검사를 시작합니다. 새 플러그인인데 게임의 상자 API가 달라진 경우는 업데이트 필요와 구분해 안내합니다. 자동 열기는 기본 꺼짐이며 게임 포인트를 사용합니다.

396개 격리 검사(기존373 + 업그레이드 흐름23)를 통과했습니다. 실제 설치 폴더는 상태와 소유권·해시만 읽었으며 플러그인 교체나 실제 개봉은 수행하지 않았습니다. 실행 중인 가상 게임으로 업데이트 건너뛰기, 메모리/디스크 버전 차이, 안내 유지, 외부 파일 변경 보존을 검사했습니다. 브리지는 0.4.9에 포함된 0.2.3과 같은 바이너리입니다.

## English

0.4.9 could skip installation inspection whenever live chat was already connected, leaving bridge 0.2.2 loaded. Polling could also remove the restart action. Setup now verifies ownership/file hashes before accepting a current running bridge. Older loaded versions require a restart even if the DLL on disk is already current. A verified current installation connects without restarting.

Close the old companion, run 0.4.10, and click **Set up & restart game** if requested. Bridge 0.2.3 is embedded in the EXE. Paused connections stay paused. Enabling chests on an older bridge starts setup inspection; unsupported native chest APIs have a separate explanation.

396 isolated checks passed, including 23 new upgrade-flow checks using a running synthetic game. No real game installation or account chest operation was performed. The bridge binary remains the same 0.2.3 shipped with 0.4.9.

## 日本語

0.4.9ではチャット接続済みの場合にインストール確認が省略され、古いプラグイン0.2.2が動き続けることがありました。再起動案内が消える問題も修正しました。ファイルの所有情報・ハッシュと実行中の版を確認し、必要なら再起動を案内します。ディスク上が最新版でも実行中が古ければ再起動が必要です。

旧アプリを終了して0.4.10 EXEを実行し、必要なら**セットアップしてゲームを再起動**を押してください。0.2.3はEXEに含まれます。停止中の接続は自動で復元しません。宝箱をオンにした際も旧版なら確認を開始し、ゲームAPI未対応とは区別します。

396件の隔離テストに合格しました。追加23件は実行中の模擬ゲームによる更新フローです。実ゲームのファイル更新・実アカウントでの開封は実施していません。接続バイナリは0.4.9と同じ0.2.3です。