# 개봉노트 검증 기록

2026-09-11 · Unity 6000.3.23f1 · Windows 11 호스트 · Android SDK 36 · ARM64 IL2CPP

## 완료한 검사

| 범위 | 결과와 근거 |
| --- | --- |
| Unity C# 컴파일 | 성공, 최종 Android 내보내기 오류 0건 |
| 저장·날짜·상태 | `OpenedChecks` 52개 통과, `BuildArtifacts/QA/data-checks.txt` |
| 네이티브 알림 날짜 규칙 | `ReminderRulesTest` 15개 통과, `native-checks.txt` |
| 실제 Unity UI 조작 | 390×844, 360×640, 1080×1920, 844×390 각각 28개 확인 통과 |
| 화면 검증 | 빈 목록, 홈, 제품 상세, 편집, 달력, 보관함, 설정, 큰 글자 편집·상세, 개인정보 화면 캡처 검토 |
| Android 네이티브 코드 | 사진 선택기/변환, 예약·수신·재부팅 복원 Java 코드 API 36 컴파일 및 Gradle 빌드 성공 |
| Android 산출물 | 서명 전 AAB·APK, 네이티브 심볼 ZIP 생성 |
| 번들 구조 | bundletool 1.17.2 validate 통과 |
| APK 정렬 | Build Tools 36 zipalign `-c -P 16 -v 4` 통과 |
| 16KB 호환 구성 | 번들 PAGE_ALIGNMENT_16K, ARM64 라이브러리 4개의 모든 LOAD 정렬 ≥16384 |
| 패키지 및 SDK | com.secondwindgames.openednote / 1.0.0 (1) / min 26 / target 36 |
| 최종 권한 | POST_NOTIFICATIONS, RECEIVE_BOOT_COMPLETED만 포함 |
| 데이터 전송 및 백업 | INTERNET 없음, allowBackup=false, fullBackupContent=false, cloud/device-transfer 제외 규칙 포함 |
| 서명 | AAB 서명 없음, APK v1/v2/v3 서명 없음. 사용자 키스토어·비밀번호 사용 없음 |
| 등록자료 | 한국어/영어 문구 길이, 아이콘·그래픽·스크린샷 규격 및 릴리스 구조 46개 자동 검사 |

데이터 검사는 공백/40자 경계/잘못된 날짜·시각, 미래 개봉일, 알림일 순서, 같은 이름, 이중 저장, 디스크 쓰기 실패, JSON 재실행, 손상 복구, 최신 스키마 보호, 보관·복구와 15개 한도, 정렬, 원자적 파일 교체 및 전체 삭제를 다룬다.

네이티브 규칙 검사는 선택 현지 시각, 지난 시각 미예약, 미리 발송 방지, 당일 OS 지연 허용, 자정 이후 소급 발송 금지, 수정된 예약·이미 발송된 예약의 중복 방지, 윤년, 시간대 이동 및 서머타임의 없는 시각/중복 시각을 다룬다. 이는 Java 날짜 판단 검증이며 Android OS가 실제로 알림을 전달했다는 증거는 아니다.

화면 검사는 실제 UI Toolkit 버튼 콜백과 TextField/Toggle을 실행했다. 등록, 날짜 선택, 저장 오류, 알림 설명 거절, 수정, 보관, 검색, 복구, 큰 글자, 편집 취소, 삭제 취소/확정, 개인정보 안내, 보관함 페이지 이동, 전체 삭제를 확인했다. 핵심 저장·완료 버튼의 화면 내 배치와 40px 이상 터치 높이도 검사했다. 보관함은 한 번에 20개를 보여주고 검색/페이지 이동 때 이전 사진 텍스처를 해제한다.

모든 UI 예시 데이터는 `BuildArtifacts/QA/data/`에 격리했다. 테스트 컴포넌트는 UNITY_EDITOR 조건부이며 Android 플레이어에 포함하지 않는다. Play 모드 종료 후 QA 플래그를 정리했다. 스토어 화면은 이 실제 런타임 UI의 1080×1920 렌더링 결과이며 Android 기기 화면이라고 표기하지 않는다.

## 남은 외부 확인

- 연결된 물리 Android 기기가 없어 설치·실행 및 OS 통합을 실기기에서 확인하지 못했다. 서명 없는 APK는 설치할 수 없다.
- 실기기 사진 선택/취소/회전·IME·시스템 뒤로가기·OS 글자 크기·노치/시스템 바·알림 권한 변경·실제 예약 알림·재부팅/강제 종료 검사는 `DEVICE-QA.md` 절차로 실행해야 한다.
- 실제 16KB 페이지 크기로 부팅한 Android 환경에서 실행한 검사는 아니다. 현재 검증은 빌드 구성 및 ELF/ZIP 정렬이다.
- TalkBack 화면 읽기 호환성을 확인하지 않았다. 큰 글자 옵션은 앱 자체 설정이다.
- 운영자 고객지원 이메일과 공개 개인정보처리방침 URL이 제공되지 않았다. 등록정보에는 null로 남겼고 공개 게시나 계정 정보를 임의로 만들지 않았다.
- Play Console 계정의 개발자 인증·테스트 요구, IARC 등급, 사전 출시 보고서, 제출 및 심사는 수행하지 않았다.
- PPT의 10명 베타와 14일 재사용 지표는 실제 사용자 실험이 필요하다. 테스트 결과를 만들어 채우지 않았다.

## 재현

1. Unity에서 `OpenedNote > 2. Run Data Checks`.
2. Unity를 연 상태에서 `Tools/Run-EditorQA.ps1`.
3. `Tools/native-tests/ReminderRulesTest.java`와 앱의 `ReminderRules.java`를 JDK 11 이상으로 함께 컴파일해 실행.
4. `OpenedNote > 3. Export Android (No Upload Signing)` 후 `Tools/Build-UnsignedAndroid.ps1`.
5. `Tools/Verify-Android.ps1` 및 Python `Tools/verify-release.py`.

원본 증거는 `BuildArtifacts/QA/`, 최종 파일 해시는 `Builds/Android/SHA256SUMS.txt`에 있다. Unity/Gradle의 일부 기존 API deprecation 안내는 빌드 실패가 아니며 Gradle 10으로 임의 업그레이드하지 않았다.
