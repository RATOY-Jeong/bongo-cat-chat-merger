# 다른 사람 상자 자동 열기 / Auto-open others' chests / 他の人の宝箱

## 한국어

상단의 **상자 자동 열기** 아래에 **다른 사람 상자 자동 열기** 체크박스를 별도로 추가했습니다. 두 옵션은 독립적이며 새 옵션은 기본 꺼짐입니다. 선택은 따로 저장합니다.

**내 게임 포인트를 사용합니다.** 게임의 다른 사람 상자를 직접 클릭할 때와 같은 선물 동작입니다. 내가 포인트를 지불하고, 상대가 상자를 받습니다. 같은 방 다른 사람의 준비된 일반·이모티콘 상자를 하나씩 대상으로 하며 내 상자 열기를 켤 필요는 없습니다.

기존 머지어를 완전히 닫고 **0.4.11 EXE**를 실행하세요. 연결 플러그인 **0.2.4**는 EXE에 포함됩니다. 필요하면 **설치하고 게임 재시작**을 누르고 방에 들어간 다음 새 옵션을 켜세요. 중단해 둔 연결은 자동 복원하지 않습니다.

- 게임에 실제로 표시된 상자와 현재 방 멤버·종류별 준비 신호를 함께 확인합니다. 내 고양이, 방을 떠난 사람, 숨김/비활성/이미 누른/고정된 상자는 건너뜁니다.
- 내 게임 포인트가 부족하면 기다립니다. 내 상자 열기·Steam 교환·게임 선물 연출이 진행 중이면 다음 요청을 미룹니다. 두 자동화는 같은 순차 처리 간격을 공유합니다.
- 같은 방·사람·상자 종류의 준비 상태가 유지되는 동안 불확실한 요청을 반복하지 않습니다. UI가 사라지거나 다시 만들어져도 반복하지 않습니다. 실제 준비 해제 신호 이후 새 준비 상태, 또는 명시적인 끄기/켜기로 다시 시도할 수 있습니다.
- 머지어를 끄거나 종료·중단하면 최대 6초 안에 새 요청을 멈춥니다. 이미 게임이 시작한 포인트 차감·선물 처리는 완료될 수 있습니다. 다른 방으로 이동하면 이전 방의 허가로 새 요청을 보내지 않습니다.
- 자동으로 다른 플레이어/고양이/데이터를 생성하거나 게임의 대기시간·포인트 값을 바꾸지 않습니다. 게임의 기존 `OpenChestForMember.OnClicked` 경로를 사용합니다.

설정 `other-chests.txt`와 5초짜리 방·게임 세션별 허가 `other-chests.json`은 로컬에만 남기며 배포 파일에서 제외합니다. 보관 한도(4,096개 미확정 준비 상태)에 도달하면 새 요청을 중단합니다. 자동 반복 대신 옵션 끄기/켜기로 사용자가 재시도할 수 있습니다.

**검증:** 448개 격리 검사 통과. 설치 게임 API는 읽기·컴파일용으로 확인했고, 새 UI·독립 설정·권한 만료·방 변경·내 포인트 차감·종류별 선물·연출 대기·중복 억제·예외·실행 중 플러그인 교체 안내는 모의 게임으로 검사했습니다. **실제 다른 사람에게 상자/보상이 도착하는 실계정 검증은 아직 하지 않았으며 실제 게임 설치·포인트·상자를 변경하지 않았습니다.**

## English

A separate **Auto-open others' chests** checkbox sits below the existing own-chest option. It is off by default and saves its own preference. **It spends MY game points; the other player receives the chest.** This is the game's normal gift click, for ready normal and emote chests of other members in the same room. Own-chest automation can stay off.

Close the old companion and run **0.4.11**. The EXE includes bridge **0.2.4**; use **Set up & restart game** when requested, join a room, and opt in. Paused connections stay paused.

Only live visible/enabled native chest controls with matching current membership/readiness are selected. Self, departed members, hidden/prefab/already-clicked/frozen controls and insufficient points are skipped. Native exchanges, own openings and gift animations serialize requests. Uncertain gifts are latched per room/member/chest type until an actual not-ready signal or explicit off/on; recreating UI does not repeat them. Room/session-specific permission expires in five seconds, stopping new requests within six seconds of off/exit/stall. Already-started operations may finish. No players are created and no timers/currency values are edited. Local settings/permissions are excluded from packages.

448 isolated checks passed. **Live recipient chest/reward delivery remains unverified; no actual game installation, points or chests were changed.**

## 日本語

既存の自分の宝箱設定の下に、独立した**他の人の宝箱を自動で開く**チェックを追加しました。初期値はオフで個別に保存します。**自分のゲーム内ポイントを消費し、相手が宝箱を受け取ります。** 同じ部屋の他のメンバーの準備済み通常・絵文字宝箱が対象です。自分の宝箱設定をオンにする必要はありません。

旧アプリを終了し**0.4.11 EXE**を実行してください。**0.2.4**を内蔵し、必要なら**セットアップしてゲームを再起動**後、入室して新設定をオンにします。停止中の接続は維持します。

表示中の有効なゲームボタン・現在の部屋メンバー・種類ごとの準備状態を確認します。自分、退出した人、非表示/無効/クリック済み/固定された対象とポイント不足は除外します。自分の開封・Steam交換・贈呈演出中は待機します。不確実な同じ要求は、実際の準備解除または明示的なオフ/オンまで繰り返しません。UI再生成も再試行の理由にしません。オフ/終了/停止後6秒以内に新規要求を止め、既に開始した処理は完了できます。部屋変更時は旧許可を使用しません。プレイヤー作成や時間・通貨の書換えは行いません。

448件の隔離テスト合格。**実アカウントでの相手への宝箱・報酬到着は未確認です。実ゲームのインストール・ポイント・宝箱は変更していません。**