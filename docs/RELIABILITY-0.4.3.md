# 0.4.3 — 전송문 분리와 게임 연결 끄기

## 입력창에 보낸 글이 남는 문제

0.4.2는 전송 결과가 도착할 때까지 원문을 편집기에 남겼습니다. 그 사이 사용자가 글을 이어 쓰면 현재 내용과 전송문이 달라져 성공 응답을 받아도 원문이 남는 흐름을 검사로 재현했습니다. 실제 보고 PC의 모든 발생 경로가 확정됐다는 뜻은 아닙니다.

0.4.3은 검증을 통과한 전송문을 별도의 대기 요청으로 옮기고 입력창을 바로 비웁니다. 늦은 성공 응답은 다음 초안을 건드리지 않습니다. 파일 기록 실패나 게임의 명시적인 거절은 편집기가 그대로 비어 있을 때 원문을 복원합니다. 그 사이 편집했다면 원문을 따로 보관합니다. 응답 시간 초과는 이미 전송됐을 가능성이 있으므로 원문을 입력창에 자동으로 되돌리지 않습니다.

보관된 글이 있으면 **이전 전송문 복원**이 표시됩니다. 입력창이 비어 있고 전송 대기 중이 아닐 때 누를 수 있습니다. 먼저 대화에 이미 표시됐는지 확인하세요. 복원만 하며 자동 전송하지 않습니다. 여러 원문은 발생 순서대로 복원됩니다. 이 보관함은 메모리에만 있으며 앱 종료 후 유지되지 않습니다.

## 봉고캣 화면의 정보 없는 고양이

두 마리의 빈 고양이가 나타난다는 원격 보고는 아직 재현하지 못했습니다. 타블렛 또는 특정 코드가 원인이라고 확정하지 않았습니다. 머지어 창을 닫아도 설치된 게임 연결 구성요소는 다음 게임 실행에도 로드되므로 재부팅만으로 원인을 배제할 수 없습니다.

1. 이전 머지어를 완전히 종료하고 0.4.3을 실행합니다.
2. 아래 **연결 도우미**를 우클릭 → **연결 기능 끄고 게임 재시작**을 선택합니다.
3. 확인창에 동의하면 게임에 정상 종료를 요청하고 연결 로더를 비활성화한 뒤 게임을 다시 실행합니다. 게임이 종료되지 않으면 강제 종료하지 않고 중단합니다.
4. 같은 방에서 빈 고양이가 사라지는지 확인합니다. 이 상태에서는 머지어의 채팅 수신·전송이 중지됩니다.
5. **연결 복원하고 게임 재시작**을 누르면 원래 연결 기능을 되돌립니다. 끄기/복원 전후 결과를 알려주세요.

앱이 소유한 것으로 확인한 winhttp.dll만 winhttp.dll.bongochat-paused로 이름을 바꿉니다. 이 이름은 게임이 찾는 연결 로더 이름이 아니므로 연결 플러그인과 동봉 런타임이 게임 실행에 개입하지 않게 비교합니다. 원본 게임 EXE·관리형 어셈블리·게임 설정·인벤토리·대화·이모티콘 캐시는 삭제하지 않습니다. 다른 파일이 같은 자리에 생겼거나 백업 해시가 바뀌면 덮어쓰지 않고 중단합니다. 같은 설치 잠금을 사용하며 실행 중인 게임의 파일은 변경하지 않습니다. 보호된 폴더에 쓰기 권한이 없을 때만 해당 작업용 관리자 권한을 요청합니다.

꺼 둔 상태는 새 머지어를 다시 실행해도 유지되며 자동 연결 준비가 몰래 복원하지 않습니다. 0.4.2 이하의 구버전은 이 상태를 이해하지 못하므로 함께 실행하지 마세요. 이 기능은 원인 구분 및 영향 차단용이며 빈 고양이 자체의 해결 확인으로 간주하지 않습니다.

## 검사 범위

200개 격리 검사를 통과했습니다: 설치/복구 63, 언어 25, 기존 기능 21, 재연결 12, 펜 호환 11, 신뢰성 37, 업데이트 31. 전송 중 입력·동일 문장 재입력·거절·파일 쓰기 실패·응답 유실·수동 복원, 연결 끄기/복원·재실행 유지·외부 파일 보존을 검사했습니다. 라이브 게임 설치는 변경하지 않았습니다. 휴이온 실기 증상과 Windows 10 실기 확인은 별도로 필요합니다. 게임 연결 브리지는 기존 0.2.2 바이너리를 그대로 사용합니다.

## English

0.4.3 separates the outgoing message from the next editable draft immediately. A delayed success never clears a new draft. Definite failures restore an untouched editor; otherwise the original is available via **Restore previous message**. Unknown results are retained separately, preventing a possibly sent message from reappearing in the editor. Restore does not resend; the recovery queue is kept in memory only.

For two empty cats reported in the game, right-click **Connection helper → Disable connection and restart game**, then compare the same room. Use **Restore connection and restart game** to restore it. This temporarily disables our verified loader, preserving game files/data/history. Pausing survives app restarts; do not run older app versions during the comparison. The empty-cat cause and the Huion freeze remain unconfirmed. 200 isolated checks passed; this is not a physical-hardware validation.

## 日本語

0.4.3では送信文を入力欄からすぐに分離し、次の下書きを編集できます。遅れて届いた成功応答で新しい下書きを消しません。明確な失敗の場合は未編集の入力欄に原文を戻し、それ以外は **前の送信文を復元** から取り出せます。結果不明の場合は送信済みの可能性があるため自動で入力欄に戻しません。復元だけで再送はしません。保管した文はアプリ終了までメモリに保持します。

ゲーム内に空の猫が2匹現れる報告には、**接続サポートを右クリック → 接続機能を無効にしてゲームを再起動** で同じルームを比較してください。**接続機能を復元してゲームを再起動** で戻せます。確認済みの接続ローダーだけを一時無効化し、ゲームデータや会話を保持します。再起動後も無効状態を保ちます。比較中は旧版を実行しないでください。空の猫およびHuionフリーズの原因・改善は未確認です。200件の隔離テストに合格しましたが、実機検証ではありません。