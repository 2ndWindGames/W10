# Google Play 등록 패키지

**개봉노트 - 개봉일과 날짜 알림** · `com.secondwindgames.openednote`  
SecondWindGames · 1.0.0 (1) · Android 8.0 이상 · ARM64 · target API 36

실제 Play Console 제출 및 공개 배포는 수행하지 않았다. 사용자 요청에 따라 업로드 키 생성·설정·서명을 제외한다.

## 등록정보

| 항목 | 값 / 파일 |
| --- | --- |
| 기본 언어 | 한국어 ko-KR |
| 이름, 간단한 설명, 자세한 설명 | `ko-KR/title.txt`, `short-description.txt`, `full-description.txt` |
| 영어 보조 등록정보 | `en-US/` — 앱 UI가 한국어라는 점을 명시 |
| 앱 유형 / 카테고리 | 앱 / 생산성 |
| 가격 / 광고 / 인앱 구매 | 무료 / 없음 / 없음 |
| 아이콘 | `Graphics/icon-512.png` |
| 그래픽 이미지 | `Graphics/feature-1024x500.png` |
| 휴대전화 스크린샷 | `Graphics/phone-01-home.png`~`phone-04-archive.png` |
| 업데이트 내용 | `ko-KR/release-notes.txt` |
| 고객지원 이메일 | 운영자 이메일 필요. `listing.json`의 `supportEmail`에 반영 |
| 개인정보처리방침 | `privacy-policy.html`의 문의 연락처 확인 후 공개 HTTPS 주소 게시 필요 |

스크린샷은 실제 Unity 런타임 화면을 1080×1920으로 렌더링한 예시다. Android 기기의 화면 캡처 또는 OS 바를 합성한 이미지라고 주장하지 않는다. 무료 사용 중 15개 한도, 알림 전달의 OS 제한, 자동 백업 없음은 상세 설명과 앱 안에 안내한다.

## 앱 콘텐츠 응답 초안

- 앱 액세스: 제한 없음. 로그인이나 심사용 계정이 필요하지 않음.
- 광고 포함: 아니요.
- 데이터 보안: 앱은 사용자 데이터를 기기 밖으로 수집하거나 공유하지 않음. 제품 기록과 선택 사진은 앱 내부에서만 처리. 외부 분석/광고 SDK 없음.
- 데이터 유형의 사진 접근과 외부 수집을 혼동하지 않음. 시스템 선택기로 고른 사진을 기기 내에서만 사용하는 실제 구현을 기준으로 답변.
- 계정 생성: 없음. 계정 삭제 기능이 필요한 계정 기반 서비스가 아님. 로컬 기록 삭제는 앱 설정에 제공.
- 권한: POST_NOTIFICATIONS(선택), RECEIVE_BOOT_COMPLETED(미래 예약 복원). 전체 미디어, 저장소, 카메라, 위치, 연락처, 광고 ID 및 정확한 알람 권한 없음.
- 금융 기능, 정부 앱, 뉴스, 건강 서비스: 해당 기능 없음. 제품의 섭취 안전이나 사용 적합성에 관한 판단·권고 기능을 제공하지 않음.
- 콘텐츠 등급: 폭력, 성적 콘텐츠, 도박, 약물, 사용자 간 교류, 공개 UGC, 위치 공유, 구매 기능 없음. 개인 메모와 사진은 공개되지 않음. 등급은 IARC 설문 결과에 따라 결정되므로 사전에 인증 등급을 표기하지 않음.
- 대상 연령 제안: 13세 이상(13~15, 16~17, 18세 이상). 어린이 대상 앱으로 홍보하지 않는 일반 생활 기록 도구. 최종 선택은 실제 출시 전략과 일치하도록 운영자가 확정.
- 지원 지역: 한국 우선 제안. 추가 지역은 현지 언어/문의 지원 계획에 맞게 결정.
- 접근성: 큰 글자와 스크롤 및 터치 영역 대응. 실기기 TalkBack 지원 검증 완료라고 선언하지 않음.

## 심사용 확인 순서

1. 첫 실행에서 ‘새 제품 기록하기’를 누릅니다. 로그인은 없습니다.
2. 이름을 ‘우유’, 개봉일을 이틀 전으로 입력합니다. 사진은 선택 사항입니다.
3. 날짜 알림을 켜고 원하는 날짜·시각을 정합니다. 저장 후 알림 안내에서 ‘나중에’를 선택해도 기록을 볼 수 있습니다.
4. 상세 화면에서 개봉 후 경과일, 개봉일과 직접 정한 알림일을 확인합니다.
5. ‘기록 수정’에서 이름·날짜·메모·사진을 변경합니다. 사진은 Android 시스템 선택기를 사용합니다.
6. ‘다 썼어요’를 누른 뒤 보관함에서 확인합니다. 사용 중으로 되돌릴 수 있습니다.
7. 설정에서 큰 글자, 알림 상태, 개인정보처리방침, 모든 기록과 사진 삭제를 확인합니다.

## 서명 외에 외부 확인이 필요한 항목

- 실제 고객지원 이메일 및 공개 개인정보처리방침 URL: 계정 정보가 제공되지 않아 임의로 만들지 않았다.
- Play Console에서 패키지명 사용 가능 여부, 개발자 인증, 대상 지역·연령, IARC 설문 및 계정별 테스트 요건 확정.
- 물리 Android 기기에서 사진 선택/취소, 한국어 IME, 알림 권한 허용·거절·재차 차단, 실제 알림 전달, 재부팅·시간대 변경, 강제 종료 후 재실행 확인.
- 내부 테스트와 Play Console 사전 출시 보고서, 실제 심사·출시 승인은 외부 절차다.

서명 전 AAB는 그대로 Play Console에 업로드할 수 없다. 최종 서명과 업로드에는 같은 버전의 네이티브 심볼 ZIP을 함께 사용한다. 키스토어·비밀번호는 이 저장소에 두지 않는다.

## 공식 자료

2026-09-11 확인. 제출할 때 Console의 최신 안내를 우선한다.

- [목표 API](https://developer.android.com/google/play/requirements/target-sdk): 신규 일반 앱은 API 36 이상을 대상으로 구성.
- [미리보기 그래픽 규격](https://support.google.com/googleplay/android-developer/answer/9866151): 512×512 아이콘, 1024×500 그래픽 이미지, 실제 기능을 보여주는 스크린샷.
- [데이터 보안](https://support.google.com/googleplay/android-developer/answer/10787469): 기기 내 처리와 외부 수집의 구분, 신고 범위.
- [사용자 데이터 정책](https://support.google.com/googleplay/android-developer/answer/10144311): 공개 개인정보처리방침과 문의 정보.
- [Android 사진 선택기](https://developer.android.com/training/data-storage/shared/photo-picker): 선택한 사진 접근.
- [Android 알람](https://developer.android.com/develop/background-work/services/alarms): 부정확 알람과 절전 정책.
- [16KB 페이지 크기](https://developer.android.com/guide/practices/page-sizes): 네이티브 라이브러리·패키징 검증.
