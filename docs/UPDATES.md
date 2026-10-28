# 0.4.7 public-access clarification

Repository settings already show Public. Unauthenticated account/profile and repository checks returned 404 while a control public repository API returned 200 (2026-10-29). The cause needs GitHub Support review; this release does not claim to restore public access. Update checks do not require login or send credentials. The 404 message now names public visibility/account status, and the browser button opens the public repository README (`#readme`) instead of `/releases/latest`. If account visibility remains restricted, that page can also return 404. See [0.4.7 details](RELIABILITY-0.4.7.md).

# 0.4.6 — Update checks / 업데이트 확인 / 更新確認

## 한국어

앱 시작 시 한 번과 **업데이트 확인** 버튼을 눌렀을 때만 조회합니다. 주기적인 자동 확인은 없습니다.

- 기존에는 `/releases/latest` 한 개만 읽었습니다. 이제 최근 릴리스 20개 중 **태그가 vX.Y.Z 형식이고, 정식 공개되었으며, 이 저장소의 EXE 업로드가 끝난 버전**을 숫자로 비교해 가장 높은 버전을 고릅니다. GitHub의 Latest 지정이나 목록 순서 때문에 새 버전을 놓치지 않습니다.
- 목록 조회가 실패하거나 응답이 잘못되면 기존 최신 단일 릴리스 경로로 한 번 더 확인합니다. 조회 제한(403/429)이면 추가 요청 없이 안내합니다. 요청당 최대 8초, 전체 최대 두 요청이며 화면 스레드 밖에서 처리합니다.
- 시작 시에도 결과를 표시합니다. **현재 실행 버전·확인 시각**을 함께 보여주고, 실패를 ‘최신 버전’으로 표시하지 않습니다. 404는 릴리스 정보 없음으로, 연결 오류·조회 제한은 해당 안내로 표시합니다.
- 이미 찾은 업데이트는 재확인이 실패해도 다운로드 링크를 유지합니다. 다운로드 버튼은 고정된 이 저장소의 HTTPS 릴리스 페이지를 브라우저로 엽니다.
- 파일 자동 다운로드·설치·실행은 없습니다. 공개 메타데이터 요청에는 앱 이름/버전만 사용하고 로그인 토큰·대화·방/Steam ID는 보내지 않습니다. 캐시를 재사용하지 않으며 초안·사전 공개·미완료 업로드·외부 저장소 파일은 제외합니다.

구버전의 확인 코드는 배포만으로 바뀌지 않습니다. **0.4.6은 직접 내려받아 기존 머지어를 종료하고 실행하세요.** 이후부터 개선된 확인 기능을 사용합니다. GitHub 공개 상태, 인터넷/방화벽, 서비스 오류나 조회 제한 때문에 확인이 실패할 가능성은 있으며 이 경우 안내 버튼으로 직접 확인할 수 있습니다.

## English

Check once at startup or on **Check updates** only. Compare the highest ready stable vX.Y.Z version among the 20 most recent releases instead of relying solely on GitHub's Latest label. Drafts, prereleases, unfinished uploads and foreign asset URLs are excluded. A failed/invalid list request falls back once to the single latest-release endpoint; rate limiting stops further requests. Each request is bounded to eight seconds off the UI thread (at most two per check).

Startup results and failures are visible with installed version/check time. Failed discovery never claims the app is current; existing update links survive failed rechecks. Only a user click opens a fixed repository HTTPS release page. There is no automatic binary download, installation or execution, and no chat, room/Steam ID or login-token transmission. Old binaries cannot gain this fix remotely: download 0.4.6 manually once and close the old app before running it.

## 日本語

起動時1回、または「更新を確認」を押したときだけ取得します。Latest指定だけに依存せず、最近20件からEXEの準備が完了した最大の正式vX.Y.Z版を選びます。下書き・事前公開・未完了・他リポジトリのファイルは対象外です。失敗や不正な応答時には単体の最新リリース経路で1回再確認し、回数制限時は追加要求を止めます。UIスレッド外で1要求8秒以内、最大2要求です。

起動時にも結果・失敗・現在のバージョン・確認時刻を表示します。失敗を最新版と誤表示せず、既知の更新リンクは再確認失敗時にも保持します。ボタンで固定リポジトリのHTTPSページを開くだけで、実行ファイルの自動取得・インストール・実行はありません。会話・ルーム/Steam ID・トークンは送信しません。旧版のコードは遠隔で変わらないため、今回の0.4.6は手動で取得して旧版を終了後に実行してください。

## Validation

48 update checks cover highest-version selection, numeric ordering, stable/ready/repository eligibility, bounded malformed metadata, fallback request counts, controlled total failure, startup/manual behavior, visible startup failure and 404 explanation, installed version/time, saved offers, language switching, narrow window layout and no periodic checks. All tests use synthetic metadata and isolated UI fixtures. A live unauthenticated request in this environment returned 404; this is not proof that the reporting user's PC had the same cause, and successful live API discovery is not claimed for this release. Browser publication and downloadable assets are verified separately.

GitHub API reference: [List releases](https://docs.github.com/en/rest/releases/releases#list-releases), [API versions](https://docs.github.com/en/rest/about-the-rest-api/api-versions).