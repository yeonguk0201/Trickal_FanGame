# 크레용사용 검흔 — Blender 시안 v1

후속 수정: [v2 — 날카로운 광선·빨간 예고 선명도 보강](../crayon-blender-v2/README.md).
이 폴더의 v1 원본은 전후 비교용으로 보존한다.

제작일: 2026-11-23. 사용자 첨부 8프레임 시안의 형태와 움직임을 Blender로 제작하는 독립 미리보기다.
게임 공격·Prefab·피해 판정에는 연결하지 않는다. 미술 승인 및 Unity 적용은 별도다.

## 목표와 참고

- 작은 반투명 빨간 예고 → 좌상단에서 우하단으로 빠르게 긋는 절단 → 은백색 교차 섬광 → 잔광·파편 소멸.
- 검은 본체는 가는 절단흔이다. 중앙이 넓어지는 구멍·소용돌이·회전하는 입자는 만들지 않는다.
- 참고: 사용자 첨부 `ChatGPT 이미지 2026년 11월 23일 오후 04_24_55.png`.
  저장소의 `game/Assets/Editor/CrayonSlashConcept-v1.png`와 SHA256이 같다
  (`57b0753bcdaa0534bce1e81d675267a6c8aa226e19b53f06dabd39569b26f652`).
- 앞선 [Unity 시안 v3 기록](../crayon-rift-prototype.md)의 피드백을 따른다.
  이전 시안의 한 프레임을 텍스처로 늘리는 대신 Blender 메시·절차 재질로 새로 구성한다.
- Blender 4.5.9 LTS·Python, 무료 기본 기능만 사용한다. 외부 AI API·유료 애드온·이미지 생성은 사용하지 않는다.

## 편집 가능한 구성

원본: [source/crayon-slash.blend](source/crayon-slash.blend).

| Collection | 역할 | 독립 조절 |
|---|---|---|
| 01 Small red warning | 작은 반투명 붉은 선 | 공격보다 짧은 길이(4.5 대 7.4), 폭·알파·표시 시간 |
| 02 Silver atmosphere | 옅은 은회색 외곽 | 두께·부드러운 알파·밝기 |
| 03 Torn silver blades | 찢어진 은색 조각 | 개별 메시·길이·폭·노이즈 침식 |
| 04 Black incision | 가는 검은 절단흔 | 폭·길이·수명, 양옆으로 벌어지지 않음 |
| 05 White cutting edges | 양쪽 백색 검광·가는 선 | 밝기·폭·곡률·위치 |
| 06 Impact glint | 짧은 교차 섬광 | 방향·크기·발생과 소멸 |
| 07 Flying splinters | 18개 직선 파편 | 위치·이동 키프레임·길이·수명 |

- `Slash direction - editable angle` 루트의 Z 회전: -36도. 정사영 카메라는 고정, Ortho Scale 10.8.
- 본체 메시의 `cut_position`·`cross_position` 속성과 재질의 절단 전선·끝 좁힘 마스크가 끝에서 끝으로 진행한다.
  효과 전체를 중심에서 확대하는 방식으로 발생시키지 않는다.
- 메시 `coverage` 속성, 재질의 수명 키프레임·절차 노이즈 임계값으로 감쇠와 부분 침식을 구성한다.
- 재질 노드의 Value 키프레임과 파편의 위치 키프레임으로 결정적으로 재생한다. 시뮬레이션 누적 상태는 없다.
- Blender에서 파일을 열고 카메라 보기(숫자패드 0), 타임라인 1~56, Render Image(F12)로 확인한다.
  재질·컴포지터를 반영한 외형은 렌더에서 확인한다. Collection 표시와 렌더 표시를 각각 조절할 수 있다.

## 출력 규격과 시간

- 960×640 RGBA PNG, 고정 캔버스·카메라·중심 기준. 프레임별 잘라내기를 하지 않는다.
- 40fps, 56프레임. 최대 형태 기준 프레임 21. Standard 색 관리, CPU Cycles 64 samples.
- 기준 시간: 프레임 1은 0초. 작은 예고 약 0~0.375초, 절단 진행 0.375~0.45초,
  최대 섬광 약 0.475~0.55초, 이후 약 1.05초까지 침식·감쇠하고 나머지는 빈 프레임이다.
- 컴포지터 Fog Glow와 발광 범위의 알파를 함께 출력한다. 검은 본체는 불투명 영역을 보존한다.
  이후 Unity 전달에서는 검은 본체를 포함한 전체 이미지를 Additive로 처리하지 않는다.
- GIF는 검수용 배경 합성본이다. 원본 투명 PNG와 색·알파 정밀도가 다르다.

## 재현 명령

저장소 루트에서 대표 형태를 생성한다. 같은 명령은 이 시안의 `.blend` 원본을 다시 생성하므로,
Blender에서 직접 수정했다면 먼저 다른 이름으로 보존하거나 변경을 제작 스크립트에 반영한다.

```powershell
python tools/blender/run.py --script docs/art-prompts/crayon-blender-v1/source/build_slash.py --output output/crayon-blender-v1
```

대표 형태 확인 후 전체 프레임을 출력한다.

```powershell
$env:BLENDER_RENDER_SEQUENCE = '1'
try {
    python tools/blender/run.py --script docs/art-prompts/crayon-blender-v1/source/build_slash.py --output output/crayon-blender-v1
} finally {
    Remove-Item Env:BLENDER_RENDER_SEQUENCE
}
python tools/blender/run.py --script docs/art-prompts/crayon-blender-v1/source/render_layers.py --output output/crayon-blender-v1/layer-review
python docs/art-prompts/crayon-blender-v1/source/package_preview.py
```

패키징에는 일반 Python의 Pillow가 필요하다. Pillow는 Blender 렌더의 배경 합성·격자 배치·GIF 묶기에만 사용하며
검광을 새로 그리거나 참고 이미지를 변형하지 않는다.

## 검수 자료와 상태

- `peak-transparent.png`: 보존용 대표 투명 렌더.
- `contact-sheet.png`: 중요한 8시점을 뽑은 비교 자료. 시점 간격은 동일하지 않다.
- `output/crayon-blender-v1/sample-sheet-rgba.png`: 4×2·3840×1280 투명 비교 시트.
  8개 시점은 불균일하므로 고정 FPS 최종 런타임 시트로 사용하지 않는다. 대응 시각은 `sample-sheet-timing.json`.
- `output/crayon-blender-v1/`: 전체 PNG 프레임, 효과 단독·배경 합성·느린 GIF, 명세·검증 결과·실행 로그.
- `output/crayon-blender-v1/layer-review/layers/`: 레이어별 단독 렌더·Collection 대응표.
- 배경 합성은 기존 바닥 타일의 일부와 에르핀 첫 걷기 프레임을 고정 크기로 배치한 참고 자료다.
  Unity 캡처가 아니며 실제 런타임 크기·정렬·카메라·블룸 일치의 증거로 사용하지 않는다.
- 사용자 미술 검수는 대기한다.
- 미확인: 원화의 붓 질감과 같은 수준의 재현, Unity 전달 포맷·재질·알파 경계·좌우 방향,
  짧은 경로·벽·연속 대쉬·게임 이벤트·실제 피해 범위. Blender 노드는 Unity로 직접 이식되지 않는다.

이 시안은 Blender 제작 환경을 실제 효과에 적용한 첫 비교본이다. 원화와의 차이 및 사용자 피드백을 받은 뒤
채택 여부와 다음 수정 범위를 정한다. 28번 계획의 게임 이펙트 완료 항목은 체크하지 않는다.

## 실행 확인 (2026-11-23)

- 전체 56프레임 Blender 렌더 종료 0. `output/crayon-blender-v1/blender.log`의 `CRAYON_BLENDER_SEQUENCE_OK` 확인.
- 진행 중인 절단 끝의 좁힘과 이동 섬광을 보강하고 프레임 17·18을 다시 출력했다.
  대표 프레임도 갱신했으며 `retake/blender.log`의 `CRAYON_BLENDER_RETAKES_OK` 확인.
  최종 제작 스크립트로 전체 재출력하면 같은 구성으로 생성된다.
- `render_layers.py`: 7개 Collection 단독 출력, 고정 카메라·40fps·투명 출력·외부 이미지 의존성 없음 검사 통과.
- `package_preview.py`: 56장 RGBA·고정 크기, 캔버스 경계 잘림 없음, 대표 알파 범위 0~255,
  마지막 프레임 완전 투명, 7개 단독 레이어가 비어 있지 않음을 검사한다. 결과는 `verification.json`.
- 자동 검사는 파일·구성·출력 불변조건의 근거다. 외형 승인·Unity 표시·공격 판정의 근거로 사용하지 않는다.
- Unity 코드·Scene·Prefab·Resources 변경이 없어 Unity 배치는 실행하지 않는다.
