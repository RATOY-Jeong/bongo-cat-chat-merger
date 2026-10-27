# 0.4.5 — 야옹 메시지 숨기기 / Hide meows / 鳴き声を非表示

## 한국어

상단 **이모티콘 표시 바로 아래**의 **야옹 메시지 숨기기**를 켜면 게임의 언어별 울음소리 문구와 **메시지 전체가 정확히 같은 경우만** 대화 목록과 검색에서 숨깁니다. 기본값은 꺼짐이며 선택은 다음 실행·앱 업데이트에도 유지됩니다.

- 숨김: <code>야옹 야옹</code>, <code>Meow Meow</code>, <code>にゃんにゃ～ん</code> 등 아래 목록의 정확한 문구.
- 계속 표시: <code>우리 고양이가 야옹 야옹 하네</code>, <code>야옹 야옹!</code>, <code>Meow Meow is a cat sound</code>, 다른 문장이 붙은 여러 줄 메시지.
- 부분 문자열 검색, 공백 제거, 대소문자 변환, 유사어 추측은 하지 않습니다. 앞뒤 공백이나 문장부호가 추가된 메시지도 정확히 같은 문구가 아니면 숨기지 않습니다.
- 이모티콘 표시와 독립적으로 동작합니다. 이모티콘 이름이 울음소리 문구와 같아도 이 설정으로 숨기지 않습니다.
- 끄면 받은 울음소리 메시지가 즉시 다시 표시됩니다. 원본 기록은 삭제·변경하지 않으므로 기존 대화 저장에는 숨긴 메시지도 포함됩니다.
- 게임에서 수신하는 채팅은 본문만으로 구분하므로 사람이 같은 문구만 직접 입력해도 숨겨집니다.

게임 파일은 변경하지 않습니다. 기존 연결 브리지 0.2.2를 그대로 사용하며 이 기능을 위해 게임 재시작이나 플러그인 교체가 필요하지 않습니다. 이전 머지어를 종료하고 새 EXE를 실행하면 됩니다.

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

Enable **Hide meows**, directly below **Show emotes**, to hide chat messages whose entire text exactly matches a known localized game meow phrase. It is off by default, and your choice persists. Messages that merely mention a meow remain visible. No trimming, case folding, substring matching or fuzzy matching is used. Emotes are independent. Turn the toggle off to restore retained messages. Original journals and existing exports are unchanged. Manually typing the exact same phrase is indistinguishable and is also hidden.

The list was checked against 27 locale tables in installed game build 25936063. Future translation changes may require a list update. No game binaries or localization bundles are redistributed. This is an app-only display setting; the existing bridge is unchanged.

## 日本語

上部の **絵文字を表示** のすぐ下にある **鳴き声を非表示** をオンにすると、ゲームの各言語の鳴き声と**全文が正確に一致するチャットだけ**を非表示にします。初期値はオフで、選択は次回起動やアプリ更新後も保持されます。鳴き声が文章の一部に含まれるだけなら表示します。空白除去・大文字小文字変換・部分一致・類似判定はしません。絵文字の表示とは独立しています。

オフにすれば保持したメッセージを再表示できます。記録は削除・変更せず、従来の会話保存には非表示の文も含まれます。同じ鳴き声だけを手入力した場合も区別できないため非表示になります。27ロケールの翻訳を確認しましたが、将来の翻訳変更には一覧の更新が必要になる場合があります。ゲームファイルや接続プラグインは変更しません。

## 検証 / Verification / 검증

기존 204개 + 울음소리 표시 39개 = 243개 격리 검사. 정확한 다국어 문구, 언급·문장부호·공백·대소문자·여러 줄 보존, 검색·신규 수신·다시 표시, 이모티콘 독립, 설정 저장·복원·앱 폴더 이관·잘못된 저장값, 세 언어 UI와 기존 창 최소 크기를 확인했습니다. 게임 파일은 읽기만 했고 실제 게임 설치·복구 상태는 변경하지 않았습니다. 타블렛·빈 고양이의 새 해결 확인은 아닙니다.