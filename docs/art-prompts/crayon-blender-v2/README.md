# 크레용사용 검흔 — Blender 시안 v2

2026-11-23. [v1](../crayon-blender-v1/README.md)에 대한 사용자 요청 “날카로운 빛 표현 추가,
잘 보이지 않는 빨간 예고를 선명하게”를 반영한다. 이전 원본·재생본은 v1 경로에 보존한다.
게임 연결·실제 판정·Unity 에셋은 변경하지 않는다. 미술 승인은 대기한다.

## 변경

- 예고 본체 반폭 0.035 → 0.068, 최대 알파 0.26 → 0.82. 길이 4.5는 유지해 공격 그림 길이 7.4보다 작게 둔다.
- 짙은 빨간 외곽(반폭 0.095), 부드러운 붉은 외곽광(0.16), 가는 밝은 빨간 중심선(0.012)을 분리했다.
  밝은 바닥과 어두운 배경에서 예고를 읽기 쉽게 한다. 예고 시간은 그대로 유지한다.
- 교차 섬광의 길이를 보강하고 `08 Needle light rays` Collection을 추가했다.
  검광 위 세 지점에 가는 백색 교차광·바늘형 광선과 좁은 푸른빛 외곽을 구성했다.
- 본체 전체의 발광이나 검은 절단흔 폭을 키우지 않는다. 추가 빛은 프레임 19~26에만 나타나고 27에서 사라진다.
  작은 교차광과 주 교차광은 서로 다른 메시로 수정 가능하다.

## 원본·출력

- 편집 원본: [source/crayon-slash.blend](source/crayon-slash.blend).
- Blender 4.5.9 LTS, 무료 메시·재질·컴포지터 기능만 사용한다. 외부 이미지 텍스처·이미지 생성·유료 애드온은 없다.
- 8개 Collection, -36도 방향, 고정 정사영 카메라, 960×640 RGBA, 40fps·56프레임.
- 대표 프레임 21, 예고 비교 프레임 8. CPU Cycles 64 samples, Standard 색 관리.
- `peak-transparent.png`, `contact-sheet.png`는 이 폴더에 보존한다.
- 전체 프레임·GIF·레이어·전후 비교·검증 로그는 `output/crayon-blender-v2/`에 생성한다.
- 배경·에르핀은 기존 에셋을 고정 배치한 참고 합성이다. Unity 캡처나 실제 런타임 크기 승인 자료가 아니다.
- Blender 파일의 카메라 보기(숫자패드 0), 프레임 8/21, F12로 예고·섬광을 확인한다.
  Collection 01은 예고, 06은 기존 교차광, 08은 추가 광선을 독립 조절한다.

## 재현

저장소 루트에서 실행한다. 생성 명령은 이 버전의 `.blend`를 덮어쓰므로 수동 수정본은 먼저 별도 이름으로 보존한다.

```powershell
$env:BLENDER_RENDER_SEQUENCE = '1'
try {
    python tools/blender/run.py --script docs/art-prompts/crayon-blender-v2/source/build_slash.py --output output/crayon-blender-v2
} finally {
    Remove-Item Env:BLENDER_RENDER_SEQUENCE
}
python tools/blender/run.py --script docs/art-prompts/crayon-blender-v2/source/render_layers.py --output output/crayon-blender-v2/layer-review
python docs/art-prompts/crayon-blender-v2/source/package_preview.py
```

대표 형태만 출력하려면 `BLENDER_RENDER_SEQUENCE`를 설정하지 않고 첫 번째 Python 명령을 실행한다.
패키징은 Pillow로 Blender 출력의 배경 합성·GIF·비교 시트만 만든다.
전후 비교에는 로컬 `output/crayon-blender-v1/`의 예고 프레임 8과 대표 PNG가 필요하다.
전체 프레임이 없으면 v1 제작 명령으로 재생성한다.

## 확인 범위

- PNG 수·크기·알파, 마지막 프레임의 완전 투명, 캔버스 경계 잘림을 검사한다.
- 8개 레이어 단독 출력과 외부 이미지 의존성 없음을 확인한다.
- 예고 프레임의 알파 커버리지 합이 v1의 3배를 초과하는지 검사하고 같은 배경의 전후 비교를 남긴다.
  이 값은 표시량의 수치 근거이며 사용자 가독성·미술 승인을 대체하지 않는다.
- Unity 임포트·실제 게임 표시·공격 판정·벽·짧은 경로는 미확인이다. 28번 계획을 완료 처리하지 않는다.

## 실행 결과 (2026-11-23)

- 전체 56프레임 재렌더 종료 0, `blender.log`의 `CRAYON_BLENDER_SEQUENCE_OK` 확인.
- 8개 Collection 단독 렌더 및 원본 구성 검사 종료 0 (`layer-review/blender.log`).
- 패키징 검사 통과: 56장 RGBA·고정 크기·경계 잘림 없음·대표 알파 0~255·마지막 프레임 완전 투명.
- 예고 프레임 8의 알파 커버리지 합은 v1 대비 약 37.52배. 같은 바닥의 `warning-in-scene.png`와
  `before-after.png`를 확인했다. 넓이·알파·외곽 레이어를 합산한 값이며 가독성의 배율을 의미하지 않는다.
- 추가 빛은 별도 Collection으로 편집할 수 있다. `scene-preview.gif`, `slow-preview.gif`,
  `contact-sheet.png`, `before-after.png`를 출력했다. 사용자 미술 확인은 대기한다.
- Unity 변경이 없어 Unity 배치를 생략했다.

## 게임 연결 (2026-11-23, Boss-Pattern-1)

위 "게임 연결 없음"은 시안 제작 당시 기준이다. 이 원본을 수평으로 렌더해 게임 텍스처 두 장을 만든다. `.blend`는 저장하지 않는다.

```powershell
python tools/blender/run.py --script docs/art-prompts/crayon-blender-v2/source/export_unity.py --output output/crayon-blender-v2/unity
python docs/art-prompts/crayon-blender-v2/source/package_unity.py
```

출력은 `game/Assets/Resources/CombatEffects/crayon-rift-warning.png`, `crayon-rift-strike.png`다. 게임 안 표시는 사용자가 Play로 확인했다(2026-11-23).
