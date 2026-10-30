# 0.4.8 — 한글 조합 중 Enter 전송

0.4.7에서는 한글 조합 중 `PreviewKeyDown`의 Enter 처리 조건이 성립하지 않아, `AcceptsReturn=True`인 입력창의 기본 줄바꿈 동작으로 넘어갈 수 있었습니다. 또한 IME가 처리한 키(`Key.ImeProcessed`)를 Enter로 판별하지 않았습니다.

- Shift 없는 Enter는 입력창의 기본 줄바꿈으로 넘기지 않습니다.
- 조합 중 Enter는 활성 조합의 `Complete()`를 요청하고, 입력창이 확정 문자를 삽입한 뒤 전송합니다. 마지막 글자를 확정하기 전에 초안을 지우지 않습니다.
- 이미 조합 완료 콜백을 기다리는 중인 Enter도 같은 대기 전송으로 처리합니다.
- Shift+Enter는 기존 줄바꿈 동작을 유지합니다.
- 반복 키와 중복 완료 이벤트는 전송을 중복시키지 않습니다. 새 조합이나 포커스 이탈은 대기 전송을 취소하고, 연결이 끊겼거나 다른 전송이 진행 중이면 초안을 보존합니다.

새 `ComposerEnterTests`는 합성 WPF 키·조합 이벤트와 격리된 가상 게임 파일을 사용합니다. 실제 한국어 IME 및 실제 방으로의 전송을 재현한 검사는 아니므로 사용자 PC에서 다음을 확인해야 합니다.

1. 마지막 글자가 조합 중인 한글을 입력하고 Enter: 마지막 글자가 포함된 메시지를 한 번 전송하고 줄바꿈하지 않는지 확인합니다.
2. 한글 확정 직후 빠르게 Enter, 영어 Enter, 빠른 반복 Enter를 확인합니다.
3. Shift+Enter는 줄바꿈하고 전송하지 않는지 확인합니다.

실행 파일의 게임 브리지와 자동 연결 설치 구성은 0.4.7과 같습니다. 이 수정은 입력 처리만 변경하며 README 다운로드 링크를 추가하지 않습니다.

API: [Microsoft TextComposition.Complete](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.textcomposition.complete).
