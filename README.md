# 개봉노트 · Android

공통 모바일 앱 기획 PPT의 **네 번째 앱**(28~34쪽)을 구현한 Unity 프로젝트입니다. 개봉일, 선택 사진, 직접 정한 알림일, 사용 중/보관함 관리를 제공합니다.

Unity **6000.3.23f1**에서 `Assets/OpenedNote/Scenes/OpenedNote.unity`를 열고 Play를 실행합니다. 설정을 다시 적용하려면 **OpenedNote > 1. Configure Android**를 선택합니다.

## 구현 기능

- 제품별 개봉일과 메모, 동일 이름 별도 등록, 분류와 정렬
- 시스템 사진 선택기로 한 장 가져오기, 기기 내부 복사·축소·회전·메타데이터 제거
- 날짜 달력·직접 입력, 미래 개봉일 및 잘못된 알림 날짜/시각 검증
- Android 알림 허용/거절 처리, 제품별 한 번 알림, 재부팅·시간대 변경 복원
- 다 썼어요·보관함·되돌리기·완전삭제, 보관함 검색
- 원자적 저장, 직전 저장본 복구, 손상·미지원 버전 덮어쓰기 방지
- 큰 글자, 작은 화면/가로 창 대응, 모든 기록·사진·알림 삭제

첫 실행은 빈 목록입니다. 사용 중인 제품 최대 15개를 제공하는 무료 버전이며 광고·결제·로그인·클라우드 동기화가 없습니다. PPT의 유료 확장 가격은 별도 단계의 검증 가설로 남겼습니다.

## 빌드

식별자 `com.secondwindgames.openednote` · 버전 1.0.0 (1) · Android API 26 이상 / target API 36 · ARM64 IL2CPP.

1. Unity 메뉴 **OpenedNote > 3. Export Android (No Upload Signing)**.
2. PowerShell에서 `Tools/Build-UnsignedAndroid.ps1` 실행.
3. `Tools/Verify-Android.ps1` 후 Python으로 `Tools/verify-release.py` 실행.

산출물은 `Builds/Android/OpenedNote-1.0.0-unsigned.aab`, `OpenedNote-1.0.0-unsigned.apk`, `OpenedNote-1.0.0-native-symbols.zip`에 생성합니다. 요청에 따라 사용자 키스토어·업로드 서명은 사용하지 않습니다. 서명 전 파일이므로 설치/스토어 업로드 전 서명이 필요합니다.

## 검증 및 등록자료

- 데이터 검사: Unity **OpenedNote > 2. Run Data Checks**.
- 화면 검사: Unity를 연 상태에서 `Tools/Run-EditorQA.ps1`. 실제 버튼을 실행하고 격리된 `BuildArtifacts/QA/data/`만 사용합니다. 테스트 코드는 Android 플레이어에서 제외됩니다.
- 네이티브 알림 규칙: `Tools/native-tests/ReminderRulesTest.java`와 실제 `ReminderRules.java`를 JDK로 컴파일해 실행합니다.
- [구현 명세](Documentation/SPEC.md), [검증 결과](Documentation/VALIDATION.md), [리소스 출처](Documentation/ASSETS.md).
- [Play Console 등록 안내](Store/PLAY-CONSOLE.md), [개인정보처리방침](Store/privacy-policy.html), `Store/Graphics/`의 등록 이미지 및 `Store/ko-KR/`의 문구.

고객지원 이메일과 공개 정책 URL을 받으면 `Tools/Set-StoreContact.ps1 -SupportEmail 실제주소 -PrivacyPolicyUrl https://실제정책주소`로 등록정보·정책·앱 내 연락처를 반영한 뒤 다시 빌드할 수 있습니다. 이 명령은 웹 게시를 수행하지 않습니다.

실기기 권한·사진·알림 전달과 Play Console 계정/심사는 로컬 자동 검사와 별도로 확인해야 합니다. 현재 확인 범위와 남은 항목은 검증 문서에 구분합니다.
