# 0.4.7 — 내 전송문·IME 보호 / Own sends and IME / 自分の送信とIME

## 한국어

### 내 메시지가 게임에만 보이던 경우

게임의 `CatSpeech.SendSpeech`는 방 전송을 생략해도 내 고양이 말풍선을 띄울 수 있습니다. 기존 브리지는 게임의 방 전송 메서드가 호출되었는지 관찰하며, 그 응답만으로 모든 사람에게 도착했음을 보장하지 않습니다. 머지어가 실제 수신 기록만 기다리면 내 전송문이 비어 보일 수 있습니다.

- 게임에서 전송 요청 응답을 받으면 내 글을 별도 로컬 기록으로 표시합니다. 성공 응답은 **게임 처리 · 방 수신 미확인**, 거절 응답은 **전송 거절 · 내 PC 기록**입니다. 방 수신 메시지와 구분합니다.
- 같은 방·게임 세션에서 내 메시지로 수신한 같은 본문을 1:1로 대응해 실제 수신 행으로 교체합니다. 요청 시각 1초 전부터 2분 후까지를 확인하며, 한 수신을 두 번 쓰지 않습니다. 다른 사람의 같은 문장은 합치지 않습니다. 브리지에는 요청 ID가 포함된 수신 확인이 없으므로 시간·본문 대응은 추정입니다.
- 내 전송 기록은 사용자 앱 폴더의 `own-sends`에 설치별 최근 최대 500개를 저장합니다. 거절된 글도 포함되며 대화 저장에는 상태를 명시합니다. 디스크 저장에 성공한 기록은 재실행 후에도 남습니다. 게임 원본 JSONL을 쓰거나 고치지 않습니다.
- 일반 대화와 로컬 행을 합쳐 최근 500개로 제한하며 정확한 야옹은 기존 묶음·숨김 경로를 유지합니다. 검색이 켜져 있으면 내 글에도 적용합니다.
- 요청 파일 쓰기 실패나 응답 유실은 기존 초안 복원·전송문 복원 경로를 사용합니다. 자동 재전송하지 않습니다. 전송 거절은 게임 필터를 우회하지 않습니다.

### 입력 조합 보호

한영 상태가 입력 도중 변한다는 보고는 해당 PC에서 재현하지 못했습니다. 다음은 앱 안의 간섭 경로를 줄이는 수정이며 외부 게임·드라이버·운영체제 문제의 확정 해결 주장은 아닙니다.

- 조합 중에는 입력창의 제한 길이를 바꾸거나 대화의 텍스트 컨트롤을 다시 만들지 않습니다. 수신·기록 읽기는 계속하고 조합 확정 뒤 화면을 갱신합니다.
- PreviewTextInput에서 조합 완료로 즉시 취급하지 않고, 입력창이 확정 문자를 삽입한 다음 작업 순서에서 완료 처리합니다. 이전 조합의 콜백으로 새 조합을 끝내지 않습니다.
- 늦은 실패 응답도 조합 중인 빈 입력창에 예전 글을 복원하지 않습니다. 복원 버튼용으로 따로 보관합니다. 조합 중 전송도 하지 않습니다.
- 전송 버튼은 클릭 시 키보드 포커스를 가져가지 않습니다. 읽기 전용 말풍선은 선택·Ctrl+C를 유지하면서 별도의 IME 문서 전환을 중지합니다.
- 한글/영문 상태·키보드 배열을 강제로 설정하거나 전역 키 입력 후크를 설치하지 않습니다. 게임·타블렛·Windows 설정을 변경하지 않습니다.

### GitHub 공개 접근

2026-10-29 소유자 설정에는 **This repository is currently public**으로 표시되지만, 비로그인 상태의 계정 프로필 API와 이 저장소·다른 공개 저장소 API가 404였습니다. 같은 환경의 `dotnet/wpf` API는 200이었습니다. 계정 공개 노출 제한 또는 서비스 문제를 의심하지만 사유는 미확정입니다. 이 제한은 앱의 한영/채팅 수정으로 복구할 수 없으며 GitHub 지원 확인이 필요합니다.

업데이트 조회는 원래부터 인증 토큰·로그인 없이 공개 메타데이터만 읽습니다. 404 안내를 공개 접근 오류로 구체화하고 다운로드 버튼은 로그인/편집 URL이나 `/releases/latest` 대신 공개 README로 엽니다. 계정 공개 접근 문제가 해소되지 않으면 이 링크도 404일 수 있습니다. 이를 해결됐다고 주장하지 않습니다.

## English

Own sends with a game response now appear as labeled local receipts: **Game processed · room receipt unconfirmed** or **Send rejected · local record**. Matching own received text in the same room/session replaces one local row once (request time minus 1 second through plus 2 minutes). This correlation is heuristic; it is not a server receipt ID or proof of delivery. Other players' text never confirms an own send. Separate per-installation storage retains up to 500 local receipts, including rejections, where disk saves succeed. Exports label them, and the game journal remains untouched. Existing meow grouping, filters and draft recovery remain; no automatic resend/filter bypass is added.

IME mitigation defers limit changes and message reconstruction during composition, waits until committed text insertion before completion, protects new composition from late draft restoration, and prevents Send/read-only message controls from taking an unnecessary IME context. No global layout, IME-mode forcing, hooks or driver changes occur. The reporter's spontaneous input-language switching has not been physically reproduced.

Repository settings are Public, but unauthenticated profile and repository checks returned 404 while a control repository returned 200. An account visibility restriction or GitHub issue is suspected, not confirmed. GitHub Support review is needed. The updater never uses login tokens; 404 is now reported as a public-access failure. Browser downloads open the public README rather than latest/edit/login routes. This cannot repair an account-level restriction.

## 日本語

ゲーム応答後の自分の送信文を、未確認または拒否と明記したローカル記録として表示します。同じルーム・セッション・本文の自分の受信を時刻で1対1対応し、1件にまとめます。他人の本文は使わず、対応は推定で到着証明ではありません。別保存は環境別最大500件で拒否も含み、テキスト保存にも状態を明記します。ゲーム原本は変更せず、鳴き声表示・下書き復元を維持し、自動再送・フィルター回避はしません。

IME変換中の入力欄制限・会話の再構築を延期し、確定文字の挿入後に更新します。遅い失敗で変換中の下書きを上書きせず、送信ボタン・読み取り専用表示による不要なIME切り替えを減らします。キーボード配列・全体設定・ドライバーを変更しません。報告PCの入力切り替え症状は未再現です。

設定はPublicでも未ログインのプロフィール・リポジトリが404で、比較した別リポジトリは200でした。アカウントの公開制限またはGitHub問題を疑っていますが未確定で、サポート確認が必要です。更新確認は元から未認証で、404の説明を明確化し、ダウンロードは公開READMEを開きます。アカウント制限をアプリで解除することはできません。

## Validation

311 isolated checks passed (previous 284 plus 27 own-send/input checks). Tests cover absent/delayed/duplicate echoes, identical sends matched once, wrong sender/session/room/time, restart retention, rejection labels, installation separation, the storage bound, selectable-message IME suspension, composition-delayed polling, protected drafts and stale completion callbacks. They use fixture files, not live room traffic. Actual Korean IME switching and Huion hardware still require reporter-PC retesting.

References: [Microsoft WPF InputMethod implementation](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Input/InputMethod.cs), [GitHub appeal/review process](https://docs.github.com/en/site-policy/acceptable-use-policies/github-appeal-and-reinstatement).