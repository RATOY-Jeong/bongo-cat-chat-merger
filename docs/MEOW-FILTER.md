# 0.4.6 — 야옹 묶음·숨김 / Group and hide meows / 鳴き声の集約・非表示

## 한국어

연속된 정확한 게임 울음소리는 언어·사람이 달라도 **야옹 23회 · 4명**처럼 한 줄로 묶습니다. 마우스를 올리면 사람별 횟수와 시간 범위를 확인할 수 있습니다. 중간에 일반 채팅이나 이모티콘이 있으면 다음 야옹은 새 묶음이 됩니다.

- 일반 채팅·이모티콘 **최근 500개**와 야옹 묶음을 별도로 보관합니다. 야옹은 숨겨도, 표시해도 이 한도를 차지하지 않으므로 야옹 때문에 일반 대화가 밀려나지 않습니다.
- **이모티콘 표시 바로 아래**의 **야옹 메시지 숨기기**를 켜면 묶음도 목록·검색에서 숨깁니다. 끄면 묶음으로 다시 표시합니다. 기본은 꺼짐이며 선택은 유지됩니다.
- 메시지 전체가 아래의 정확한 문구일 때만 묶습니다. `우리 고양이가 야옹 야옹 하네`, `야옹 야옹!`, 앞뒤 공백·추가 줄이 있는 문장은 그대로 둡니다. 부분 일치·공백 제거·대소문자 변환·추측은 없습니다.
- 이모티콘은 별도이며 같은 이름이어도 야옹으로 묶지 않습니다. 사람이 정확히 같은 문구만 직접 보내면 구분할 수 없으므로 묶습니다.
- 원본 JSONL은 삭제하거나 다시 쓰지 않습니다. 대화 저장에는 보관된 일반 대화와 야옹 묶음 요약을 포함하며, 숨김 설정이 켜져 있어도 묶음 요약을 포함합니다. 개별 원문은 게임 폴더의 원본 기록에 남습니다.
- 처음에는 기록을 뒤에서 읽어 일반 대화 500개를 찾고, 이후에는 새로 추가된 부분만 읽습니다. 실제 앱의 읽기 작업은 화면 스레드 밖에서 수행합니다. 500개보다 오래된 일반 대화는 화면에서 빠지지만 원본은 보존됩니다.
- 참여자 집계는 묶음당 최대 128명이며 초과 시 `128+`로 표시합니다. 툴팁에는 횟수순 최대 12명을 표시합니다. 전체 야옹 횟수는 계속 집계합니다.

게임 파일·연결 브리지 0.2.2는 변경하지 않습니다. 기존 머지어만 닫고 새 EXE를 실행하면 됩니다.

## 확인한 문구

2026-10-28 설치 게임 빌드 25936063의 번역 테이블에서 확인한 27개 언어·지역, 19개 고유 문구입니다. 게임이 향후 번역 문구나 지원 언어를 바꾸면 앱의 목록 갱신이 필요할 수 있습니다. 게임의 번역 번들·어셈블리는 배포하지 않습니다.

| 언어·지역 | 정확한 문구 |
|---|---|
| 한국어 | 야옹 야옹 |
| English | Meow Meow |
| 日本語 | にゃんにゃ～ん |
| 简体中文 | 喵~喵~ |
| 繁體中文 | 喵嗚～ |
| Français / Français canadien | Miaou Miaou |
| Deutsch / Magyar / Português (Brasil, Portugal) / Română | Miau Miau |
| Polski / Español (España, Latinoamérica) | Miau miau |
| Čeština | Mňau Mňau |
| Dansk | Mjav Mjau |
| Nederlands | Miauw Miauw |
| Suomi | Miu Mau |
| Ελληνικά | Νιάου Νιάου |
| עברית | מיאו מיאו |
| Italiano | Miao Miao |
| Norsk / Svenska | Mjau Mjau |
| Русский | Мяу Мяу |
| Türkçe | Miyav Miyav |
| Українська | Няв Няв |

## English

Consecutive exact localized meows become one neutral row, e.g. **Meows ×23 · 4 people**, with per-person counts and a time span in the tooltip. Ordinary chat or an emote ends the run. Meows never consume the **500 ordinary chat/emote events** limit, whether shown or hidden. **Hide meows**, below **Show emotes**, hides the grouped rows; turn it off to restore them. The setting is saved and defaults off. Only full ordinal exact matches qualify; messages mentioning meows remain ordinary conversation. Manually typing the identical phrase also qualifies.

Original journals are never rewritten or deleted. Text exports contain the retained ordinary conversation plus meow summaries, including hidden groups; individual originals remain in the journal. Initial history scans backwards for 500 ordinary events off the UI thread; later polls read appended bytes. Up to 128 contributors are tracked per group (128+ if more); the tooltip lists up to 12 most frequent contributors. The total meow count continues. No game/bridge changes are required.

## 日本語

全文が一致する連続した鳴き声を、言語や人が違っても **鳴き声 23回 · 4人** のように1行にまとめます。ツールチップで人ごとの回数と時間帯を表示します。通常の会話・絵文字が入ると次のグループになります。鳴き声は表示・非表示にかかわらず**通常の会話・絵文字500件**の枠に数えません。「絵文字を表示」の下の「鳴き声を非表示」で集約行を隠せます。オフで戻り、選択を保存します。初期値はオフです。全文の完全一致だけが対象で、文章の一部に含まれる場合は通常の会話として残します。同じ文だけを手入力した場合も対象です。

原本の記録は削除・上書きしません。会話保存には非表示でも鳴き声の集計を含み、個々の原文は記録ファイルに残ります。最初はバックグラウンドで通常会話500件まで逆順に読み、その後は追記部分だけを読みます。人ごとの集計は最大128人（超過時は128+）、ツールチップは回数順で最大12人です。総回数は集計を続けます。ゲーム・接続プラグインの変更は不要です。

## Verification / 검증 / 検証

284 isolated checks passed: 63 setup, 62 feature/language/reconnect, 11 tablet compatibility, 37 reliability, 48 update, 39 exact-meow filtering, 24 grouped-history checks. Grouped-history checks cover 15,000 multilingual meows beyond 2 MB while retaining 500 ordinary events, append-only reads, partial UTF-8, malformed/oversized records, replacement/truncation, cancellation, unchanged source hash, immutable snapshots, selected-message preservation, filter/search and background initial load. These are isolated fixtures; no new Huion, ghost-cat or Windows 10 hardware confirmation is claimed.