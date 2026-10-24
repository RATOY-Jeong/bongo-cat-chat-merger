# 更新・업데이트 확인 / Update notifications — 0.4.2

## 한국어
- 앱 시작 시 한 번과 ‘업데이트 확인’ 버튼 클릭 때만 GitHub 정식 최신 릴리스를 조회합니다. 주기적인 확인은 없습니다.
- 현재보다 버전 번호가 크고 실행파일 업로드가 완료된 정식 릴리스에만 알림을 표시합니다. 임시 저장·사전 공개 버전이나 EXE 없는 릴리스는 대상이 아닙니다.
- ‘최신 업데이트가 있습니다!’ 아래 ‘다운로드 페이지’ 버튼은 기본 브라우저에서 이 저장소의 릴리스 페이지를 엽니다. 앱이 실행파일을 내려받거나 설치·실행하는 기능은 없습니다.
- GitHub 공개 API에 앱 이름/버전을 포함한 일반 조회 요청을 보냅니다. 로그인 토큰이나 대화 내용·방 ID·Steam ID는 보내지 않습니다.
- 조회는 화면 스레드 밖에서 수행하며 전체 8초 제한을 둡니다. 연결 오류·조회 제한을 안내하고, 이미 찾은 업데이트 링크는 재확인 실패 때도 보존합니다.
- 버전은 숫자로 비교해 0.10.0을 0.9.9보다 새 버전으로 판단하며, 같거나 이전 버전으로의 설치를 제안하지 않습니다.
- GitHub 원격 응답의 임의 링크를 실행하지 않고 이 저장소의 HTTPS 릴리스 페이지 주소를 구성합니다.

새 버전을 배포할 때는 태그를 v0.4.3 같은 형식으로 만들고, BongoCatChatMerger 실행 EXE를 첨부한 뒤 정식 최신 릴리스로 공개하면 됩니다. 기존 0.4.2 사용자는 다음 앱 시작이나 버튼 클릭 때 알림을 받습니다. 0.4.1과 0.4.2-rc1에는 이 기능이 없으므로 이번 0.4.2는 한 번 직접 내려받아야 합니다.

## English
Checks occur once at startup and on the Check updates button only, with no scheduled polling. Only a newer full release with a finished EXE upload produces a notification. Download page opens the fixed repository's release page in the browser. There is no binary download, installation or execution by the app. Requests contain no chat, room/Steam IDs or login tokens. Network work is off the UI thread with an eight-second overall deadline, and an existing offer survives failed rechecks. Numeric version comparison prevents downgrade offers. Publish a future full release with a vX.Y.Z tag and a ready BongoCatChatMerger EXE to notify existing users.

## 日本語
起動時に1回、または「更新を確認」ボタンを押したときだけ確認します。定期確認はありません。EXEのアップロードが完了した新しい正式版だけを通知します。「ダウンロード」は固定のGitHubリポジトリ内のリリースページをブラウザーで開きます。アプリによる実行ファイルのダウンロード・インストール・実行は行いません。会話、ルーム/Steam ID、ログイントークンは送信しません。通信はUIスレッドの外で行い、全体で8秒以内に終了します。再確認の失敗時も既知のリンクを保持します。

## Verification
- 31 update-specific checks cover release eligibility, numeric versions, malformed/bounded data, repository/link restrictions, nonblocking startup, duplicate clicks, manual rechecks, English/Korean/Japanese UI, offline/rate-limit behavior, one startup check only, and minimum window layout.
- A live unauthenticated GitHub API check succeeded before publication. Live discovery of the new full release is checked after publication.
- The full suite totals 179 checks. Simulated update releases used in UI tests are not real published versions.
- Huion whole-window freeze confirmation and Windows 10 physical execution remain pending; these update checks do not establish either.