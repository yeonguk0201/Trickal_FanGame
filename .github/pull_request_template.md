<!-- 제목: 커밋과 같은 형식으로 쓴다. 예) feat(item): melune card duplication (Jjangsem-1) -->

## 요약

<!-- 무엇을, 왜 바꿨는지 2~4줄. 계획 문서의 조각 ID(예: Jjangsem-1)를 함께 적는다. -->

## 변경 내용

<!-- 해당하는 영역만 남긴다. -->

- **Unity**:
- **Backend**:
- **Web**:
- **문서**:

## 계약·데이터 영향

<!-- 없으면 "없음". 있으면 영향 범위를 적는다. -->

- [ ] Item / Character / Run ID를 새로 추가했거나 바꿨다 (기존 ID의 의미 변경·재사용 없음)
- [ ] `ItemEffectType` 등 직렬화되는 enum 값을 추가했다 (기존 번호 유지)
- [ ] Prisma 또는 API 계약을 바꿨다 → Backend DTO·서비스, Unity DTO, Web, seed, 문서를 함께 확인했다
- [ ] 로컬 DB seed를 다시 실행해야 한다 (`backend/`에서 `pnpm prisma:seed`)

## 검증

<!-- 실제로 실행한 것만 체크하고, 실행하지 못한 것은 이유를 적는다. -->

- [ ] Unity 검증기:
  <!-- 예) Week22Jjangsem1Verification.SetupAndVerifyBatch, VerifyWithRegressionsBatch — 종료 코드 0 -->
- [ ] Backend: `pnpm test` (필요 시 `pnpm build`)
- [ ] Web: `pnpm lint` (필요 시 `pnpm build`)
- [ ] 실제 화면 수동 확인:
  <!-- 확인한 메뉴 경로·조작과 결과. 확인 전이면 "대기"라고 적는다. -->

## 체크리스트

- [ ] 새 Unity 에셋·스크립트의 `.meta` 파일을 함께 커밋했고, 기존 GUID를 바꾸지 않았다
- [ ] 모든 텍스트 파일이 UTF-8이다
- [ ] 이 PR과 관련 없는 변경이 섞여 있지 않다
- [ ] 계획 문서(`docs/`)의 상태는 자동 검증 또는 수동 확인 증거가 있는 항목만 완료로 표시했다

## 남은 일·참고

<!-- 후속 조각, 임시로 정한 기본값, 리뷰어가 알아야 할 결정. 없으면 지운다. -->
