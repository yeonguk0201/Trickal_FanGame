# 황금 기둥 경미한 파손 상태 추가

2026-11-20. 사용자 요청: 살짝만 깨진 상태 하나를 추가하고 3타 파괴로 변경.

## 제작과 적용

- 기존 정상 원화를 참조해 내장 image_gen으로 `gold-pillar-light-damage.png`를 생성했다. 짧은 가장자리 균열과 작은 모서리 깨짐만 추가하고 몸체와 지면의 전체 형태를 유지했다.
- 최종 사용 파일: `game/Assets/Resources/CombatEffects/saemaeum-gold-pillar-light-damage.png`.
- 1254×1254 RGBA, PPU 863, 피벗 (618.5/1254, 12/1254). 정상 원화와 불투명 폭 863px 및 밑동 기준을 맞췄다. Bilinear·Clamp·무압축·mipmap 없음·NPOT 축소 없음·Max Size 2048은 정상 이미지와 같다.
- 1타: 새 경미한 파손 이미지. 2타: 기존 시트의 셀 1(큰 균열). 3타: 기존 셀 2→3→4 파괴 연출 0.3초.
- 보스 프리팹·런타임 기본값·Setup 재실행 값을 모두 3타로 변경했다. 원래의 파괴 시트와 소멸 동작은 그대로 사용한다.
- 생성 결과에서 경미한 균열과 원형 보존을 확인했다. 새 이미지의 실제 Play 미술 검수는 대기 상태다.

## 확인 경로

- 자동 검증: `TrickalFanGame.Editor.SaemaeumGoldPillarArtworkVerification.Verify`.
- Unity 6000.3.22f1 배치 통과(종료 코드 0). 로그 `game/Logs/saemaeum-gold-pillar-three-hit.log`에서 `intact, light damage, heavy cracks, three breakup cells, three-hit removal.` 성공 메시지를 확인했다. 1타·2타 생존과 각각의 표시, 3타 제거·재타격 거부, 파괴 셀 순서와 알파 소멸을 검사했다.
- 수동 확인: Game Scene Play → F1 → `— Boss room —` → `Boss F2`. 소환 기둥의 1타 작은 균열·2타 큰 균열·3타 파괴, 각 상태의 크기와 밑동 정렬을 확인한다.

## 생성 프롬프트

```text
Use case: precise-object-edit.
Edit the supplied golden pillar sprite to create ONE additional LIGHTLY DAMAGED state for the FIRST of three player hits. Keep the exact same intact pillar design, silhouette, proportions, camera, warm golden yellow cel shading, highlights, thick brown outlines and pointed brown earth/gray stone base. Preserve original canvas dimensions, framing, transparent margins, center and ground baseline as closely as possible.
Change ONLY a small amount of damage on the gold: add two or three SHORT thin dark hairline cracks near the upper-left edge and middle-right edge, each extending only a small distance into the front face, plus ONE very small chipped bevel corner. Cracks should occupy less than 10 percent of the golden face. The large center of the pillar stays smooth and uncracked. This is visibly minor damage, much less damaged than a fully cracked pillar: NO large branching crack network, NO cracks traversing the entire face, NO separated chunks, NO missing large sections. Keep the pillar solid, upright and at full height. Keep the dirt and stones unchanged. No floating fragments or dust.
Output a single centered sprite on genuine transparent alpha background. No sheet, extra objects, labels, numbers, text, ground plane or watermark.
```
