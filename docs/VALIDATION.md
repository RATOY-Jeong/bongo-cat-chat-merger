# 검사 결과

- 앱·플러그인 C# 빌드 성공
- WPF 화면을 PNG로 렌더링해 말풍선, 한글, 검색창, 상태 및 도구 배치 확인
- 한글·이모지·줄바꿈·마크업 모양의 텍스트 보존
- 최근 500개, 중복 ID, 작성 중인 마지막 기록 처리
- 큰 기록 파일의 마지막 2MB 읽기
- 방 ID 경로 검증
- 정상 연결 상태와 방 이름 표시
- 방 전환 시 기존 방 메시지 혼입 방지
- 오래된 상태 신호의 연결 끊김 표시
- 실행 중이지만 방에 없는 상태 표시
- 격리한 설치 폴더에서 설치·제거·재설치·수정된 파일 보존 검사
- 설치된 실제 게임을 headless로 실행해 BepInEx 로드와 Harmony 수신 메서드 연결, `state.json`의 `ready=true`, 빈 `error` 확인

Steam에 로그인한 실제 방에서 내 메시지와 상대 메시지가 원본 기록 파일에 저장되는 것을 확인했습니다. 기록 검사 시 222개 메시지가 저장되어 있었으며, 실제 메시지 내용은 배포 파일에 포함하지 않았습니다. 설치 스크립트의 Steam 라이브러리 자동 탐색과 게임 폴더 지정 방식도 검사했습니다.

`preview.png`의 대화는 화면 검사에 사용하는 예시 데이터입니다.

## 0.1.1 시작 문제 수정

- Bongo Cat 빌드 25902419의 어셈블리로 앱·플러그인 빌드 성공
- 실제 플러그인의 시작 처리 함수를 호출해 현재 프로세스의 DOORSTOP_INITIALIZED 제거 및 자식 프로세스에 상속되지 않음 확인
- Steam 완전 종료 후 Steam에서 게임을 실행하면 0.1.0 연결 복구 확인
- 수정본 설치 시 플러그인 파일 해시와 제거용 설치 목록을 함께 갱신

수정본 0.1.1 설치 후 Steam에서 게임을 다시 실행하여 최신 상태 신호, ready=true, 빈 error 및 현재 실행 세션의 실제 방 메시지 2개(내 메시지 1개 포함) 기록을 확인했습니다.

전체 Windows 재부팅 후 자동 시작 경로 검사는 아직 수행하지 않았습니다.

## 말풍선 고정 오류 수정

실제 게임 로그의 `Invalid header data!!!`, `SelectedLocale is null`, `Vfx.CatSpeech.DoShowSpeech` 예외를 확인했습니다. 게임 원본의 `String.GetHashCode`는 legacy 해시를 사용하지만 추가 Mono 라이브러리는 randomized 해시를 사용하여 Addressables의 catalog marker가 달라졌습니다. 야옹 문구가 null이 되고 말풍선 종료 코루틴이 중단된 원인입니다.

플러그인 시작 시 Addressables의 `kMagic`만 원본 게임의 legacy 값으로 복원합니다. 게임 원본 DLL·데이터 파일 및 전체 문자열 해시 동작은 변경하지 않습니다. 원본 catalog.bin의 marker와 버전을 먼저 검증하며, 지원되지 않는 형식은 거부합니다.

- 원본 catalog.bin의 marker와 legacy 해시 계산 결과 일치 확인
- 잘못된 버전·잘린 파일을 거부하고 marker를 변경하지 않음 확인
- 수정본 실행 로그에서 catalog compatibility ready, 빈 error 및 최신 heartbeat 확인
- 새 실행 로그에서 기존 catalog·localization·CatSpeech 예외가 발생하지 않음 확인
- 사용자가 봉고챗 실행 상태에서 캐릭터 클릭, 말풍선 자동 종료, 이후 채팅 입력·전송 정상 동작 확인