# 요정마을 연결형 에셋 적용

## 현재 구성 (2026-10-02)

사용자가 제공한 `트릭컬 요정마을 맵 전경.png`의 평평하고 밝은 카툰 그림체를 기준으로 새 에셋을 생성했다. 이전 조합에서 지적된 문·벽·모서리·바닥의 분리감과 반복 무늬를 해결하기 위해, 하나의 공통 전경에서 연결 구간을 분리하는 방식으로 교체한다.

`game/Assets/Rooms/Artwork/FairyVillage/`의 현재 사용 에셋:

- `room-open-v3.png`: 네 방향 열린 문과 전체 연결 벽·모서리·잔디의 공통 그림.
- `room-closed-v3.png`: 같은 위치와 구도의 닫힌 문 상태.
- `room-locked-v3.png`: 금색 사슬과 클로버 자물쇠가 있는 열쇠 잠금 상태.
- `room-sealed-v3.png`: 문 없는 면의 연결 벽 상태.
- `ConnectedRoom.shader`, `connected-room.mat`: 원색을 유지하면서 생성 이미지 외곽의 낮은 alpha와 극단적인 녹색·빨강 마스킹 흔적을 렌더에서 제거한다.

원본 PNG와 이전 실험 에셋은 보존한다. 새 에셋은 프로젝트 안에 복사되어 외부 생성 이미지 폴더에 의존하지 않는다. 텍스처마다 25개 격자 영역과 방향별 문 영역을 Unity Sprite로 분리한다. 바닥·벽·모서리 21개 구간과 문/봉인 구간 4개가 전체 방을 채운다. 기본 방은 전체 공통 그림을 그대로 재구성한다. 큰 방에서 늘어난 구간은 원래 픽셀 크기로 반사 샘플링하고 경계에서 원본 색을 혼합하여 잎·꽃이 찌그러지는 것을 줄인다. 이전처럼 작은 잔디 타일을 바둑판 형태로 배치하지 않는다.

방이 커져도 문과 모서리의 가로·세로 배율은 동일하게 고정하며, 긴 벽과 잔디 구간의 표시 영역만 함께 늘리고 `ConnectedRoomPatch`가 원래 텍스처 크기와 접합부의 색을 유지한다. Small 방에서는 문과 모서리를 같은 비율로 축소한다. 장식 꼭대기와 아래 돌 부분은 원본 비율 보존을 위해 충돌 사각형 밖으로 약 0.29 유닛 이내 더 그려질 수 있으며 기존 외곽 Collider는 보존하고, 그림 속 벽 안쪽에는 별도 충돌 경계를 둔다. 모든 영역은 공통 그림의 같은 좌표에서 잘리므로 경계의 색·선·바닥을 공유한다. 바닥이 함께 포함된 문 영역은 색을 따로 물들이지 않는다. 좌·우문은 각각 생성된 방향별 그림을 사용하며 반전하지 않는다.

`FairyVillageDoorArtwork`는 기존 `DoorController` 상태를 읽어 열린 문·닫힌 문·열쇠 잠금 그림만 갱신한다. 문 없는 면에는 봉인 벽 그림을 표시한다. 기존 도형 렌더러의 타입별 색 값, Collider, 문 슬롯·트리거·진입점, Room Profile, Template, Prefab GUID와 API 계약은 유지한다. 새 그림의 정렬 순서는 -100으로 게임 오브젝트 뒤에 둔다. 보스문의 기존 타입 값은 유지하지만 연결 그림 전체에 보라색을 덧씌우지는 않는다.

## 실행 및 확인

1. 적용: `Trickal Fan Game > Artwork > Apply Fairy Village Tiles and Walls`.
2. 자동 검증: `Trickal Fan Game > Artwork > Verify Fairy Village Tiles and Walls`.
3. 새 Run을 시작하여 갱신된 프리팹을 사용한다. 기존 방 인스턴스는 자동 교체하지 않는다.
4. 작은·기본·넓은·세로·큰 방에서 문·모서리·벽·잔디 접합부와 플레이어의 벽/문 접근 모습을 확인한다.
5. 전투 전후 문 그림 변경, 열쇠 잠금 해제, 문 없는 면의 봉인 그림과 좌우 방 이동을 확인한다.

배치 적용·검증: `TrickalFanGame.Editor.FairyVillageArtworkVerification.SetupAndVerifyBatch`.
두 번 적용하여 중복 오브젝트·Sprite 참조 변동과 Collider 위치·크기·활성 상태·Prefab GUID 보존을 비교하고, 문 방향·비율·상태와 포탈 장벽을 검사한다. 실패는 예외로 보고한다.

전체 검증·화면 생성: `TrickalFanGame.Editor.FairyVillageArtworkVerification.ApplyVerifyAndRenderBatch`.
기존 Room-0 및 Fixed Room Graph 검증을 포함하며, 그래픽 장치가 필요하므로 `-nographics`를 사용하지 않는다.

렌더 결과는 `output/`에 저장한다:

- `fairy-village-room-preview.png`: 기본 방, 네 문 모두 열림.
- `fairy-village-door-states.png`: 위 닫힘, 오른쪽 열쇠 잠금, 왼쪽 봉인, 아래 열림.
- `fairy-village-small-preview.png`, `fairy-village-wide-preview.png`, `fairy-village-tall-preview.png`, `fairy-village-large-preview.png`: 방 크기별 조합.

과거 `Setup Temporary Basic Room Artwork`는 이전 한 장짜리 배경을 적용한다. 다른 방 생성 Setup을 재실행한 후에는 이 Artwork 적용 메뉴를 마지막에 실행한다. 이번 구성에는 과거 배경의 활성화를 요구하는 `Verify Temporary Basic Room Artwork`를 사용하지 않는다.

## 검증 기록

이전 타일 조합은 2026-10-01에 기능 검증을 통과했지만 사용자가 시각 품질을 거부하여 현재 연결형 구성으로 대체했다. 그 결과를 현재 구성의 시각 품질 근거로 사용하지 않는다.

현재 구성의 자동 검증 로그는 `output/artwork-connected-v3.log`, 초기 적용 로그는 `output/artwork-connected-setup.log`다. 전체 Run의 실제 Play 확인과 사용자 미감 평가는 별도로 남아 있으며, 기존 주차 계획의 완료 상태는 변경하지 않는다.

2026-10-02 Unity 6000.3.22f1의 최종 `ApplyVerifyAndRenderBatch`가 종료 코드 0으로 통과했다.
16개 템플릿의 반복 적용·GUID·Collider 보존·방향별 문 비율·열림/닫힘/열쇠 잠금 상태와 포탈 장벽을 확인했다.
기존 Room-0와 Fixed Room Graph 검증도 통과했다. 기본·Small·Wide·Tall·Large 및 문 상태 조합을 실제 Unity에서
렌더하여 확인했다. 큰 방의 꽃·잎 늘어짐과 UV 혼합 과정의 가는 경계 흔적을 발견해 색 혼합 방식으로 수정했다.
큰 방 바닥에는 반사 샘플링에 따른 일부 반복·대칭 모티프가 남는다. 원본과 동일한 그림이라고 단정하지 않으며
실제 캐릭터가 벽과 문에 접근하는 모습과 사용자 미감 평가는 이후 Play 확인으로 남긴다.

## 벽 접근 및 문 진입 수정 (2026-10-02)

사용자가 실제 Play 사진으로 연결형 에셋의 자연스러움을 확인했다. 추가로 좌측·하단 벽 위까지 캐릭터가 이동하고, 위쪽 벽을 따라 이동할 때 문이 자동으로 반응하는 문제를 보고했다.

- 에셋과 문 그림·트리거·진입점 위치는 유지한다. 16개 템플릿에 `Fairy Village Wall Boundaries`의 8개 BoxCollider2D를 추가해 그림 속 벽 안쪽에서 이동을 막는다. 네 문 통로는 기존 2.4 유닛 폭을 유지한다. 경계는 기존 벽과 같은 물리 레이어로 플레이어·적·투사체에 함께 적용된다.
- `PlayerMovement.MovementIntent`와 `RoomDoorway.IsMovingIntoPassage`로 실제 이동 의도를 확인한다. 문 방향의 입력이 있는 경우만 접촉 진입을 허용하며, 옆으로 스치기·정지·방 안쪽 이동은 진입이나 열쇠 소비를 유발하지 않는다. 대각선으로 문에 접근하는 것은 허용한다.
- 기존 외곽 벽·포탈 장벽과 GUID 보존 검증은 유지한다. 추가 경계는 별도로 8개 구성·레이어·비트리거·안쪽 바닥 침범 여부를 검사한다.
- `output/artwork-wall-boundaries.log`: Unity 배치 적용 및 반복 적용, 16개 템플릿, Door-1 접촉 진입 회귀, Room-0 및 Fixed Room Graph 검증이 종료 코드 0으로 통과했다.

수동 확인: 새 Run에서 좌우·하단 벽에 붙어 걸을 때 캐릭터가 잔디 안쪽에서 멈추는지, 위쪽 문 앞을 좌우로 지나갈 때 방이 바뀌지 않는지, 위쪽 문을 향해 W를 누르면 이동하는지 확인한다. 아래쪽 문의 시각적 진입 깊이는 기존과 동일해야 한다. 추가 경계 적용 후의 실제 조작 감각은 아직 사용자 확인 전이다.
## 중앙 봉인 경계와 하단 입체감 조정 (2026-10-02)

사용자 Play 사진에서 문이 없는 면도 기존 문 통로의 중앙 빈틈에 들어갈 수 있음을 확인했다. 각 Seal 아래에 `Fairy Village Seal Boundary`를 구성하여 문 없는 면 및 봉인된 숨겨진 문에서만 중앙 경계를 활성화한다. 연결된 통로는 Seal 비활성화와 함께 추가 경계도 해제된다. 기존 8개 벽 경계와 끊김 없이 이어지며, 문 전환·열쇠 처리 계약은 유지한다.

추가 경계 두께는 이전 값 대비 위쪽 50%, 아래쪽 70%, 좌우 90%로 조정했다. 이는 방 전체 크기가 아닌 벽 안쪽에 추가했던 경계 폭의 비율이다. 기본 방의 위쪽 추가 폭은 약 0.636, 아래쪽은 약 0.784, 좌우는 약 1.145 유닛이다.

하단 벽의 네 구간과 하단 Seal에 정렬 순서 20의 전경을 구성했다. 동일한 그림을 사용하되 원본 Y=820 이하의 벽 부분만 표시하여 바닥 전체가 플레이어나 몬스터를 덮지 않도록 한다. 열린 아래쪽 문의 통로에는 전경을 추가하지 않는다. `ConnectedRoomPatch.foregroundCutoff`와 Shader의 `_ForegroundCutoff`로 이 범위를 제한한다.

검증 항목: 16개 템플릿의 봉인/미연결/연결 상태별 중앙 Collider 활성화, 전경 구간 수·정렬·마스크, 기존 외곽 Collider 및 GUID 보존, 문 이동 의도와 Room-0 회귀. 실제 조작감과 가림 정도는 새 Run에서 추가 확인한다.

최종 검증: 열린 원본 Unity의 프로젝트 잠금을 피해 복사본에서 output/artwork-wall-depth-final.log에 해당하는 배치 적용·회귀 검증·화면 생성을 완료했다(종료 코드 0). 검증된 16개 프리팹을 GUID 보존 확인 후 원본에 적용하고 파일 해시 일치를 확인했다. output/fairy-village-wall-depth-preview.png에서 플레이어·몬스터의 하단 가림을 살펴본 후, 과한 가림을 줄이기 위해 전경 시작점을 Y=795에서 Y=820으로 낮췄다. 실제 Play에서의 체감 조정은 추가 확인이 필요하다.

## 벽 에셋의 가림 시작점 수정 (2026-10-02)

사용자가 경계 두께는 승인했으나, 캐릭터가 여전히 벽 위에 서 있는 듯 보인다고 정정했다. 이전에 Y=820까지 낮춘 전경 시작점 때문에 벽 상단에 캐릭터가 겹쳐 보였다. 이동·충돌 경계와 문 진입 조건을 유지하고, 아래쪽 전경 시작점을 벽 상단에 가까운 Y=795로 올렸다. 벽 그림이 플레이어와 몬스터 앞에 겹쳐지도록 하며 열린 문의 중앙 통로는 그대로 유지한다. 가림 마스크의 정확한 값(146/221)을 검증에 추가했다.

`output/artwork-wall-occlusion.log`: 원본 프로젝트에서 16개 템플릿 적용 및 반복 적용·문 방향 진입·중앙 봉인 경계·Room-0·Fixed Room Graph 검증, 화면 생성을 종료 코드 0으로 완료했다. `output/fairy-village-wall-depth-preview.png`에서 캐릭터와 몬스터가 하단 벽 뒤로 가려지는 모습을 확인했다. 실제 Play의 체감 확인은 새 Run에서 수행한다.
## 윤곽을 따른 투명 전경 (2026-10-02)

사용자 요청에 따라 수평 가림을 투명 전경의 잎 윤곽으로 대체했다. 내장 imagegen으로 `room-sealed-v3.png`에서 중앙 잔디 바닥과 외부 배경을 투명하게 제거한 `wall-foreground-cutout-v1.png`를 만들었다. 전체 캔버스는 원본과 같은 1672×941이며, PNG 생성 요청문은 `output/wall-foreground-prompt.txt`에 보존했다.

이미지 생성 과정에서 색이나 세부 그림이 변할 수 있으므로 생성 이미지의 RGB는 게임에 표시하지 않는다. 원본 벽 RGB를 그대로 표시하며 새 이미지의 alpha만 윤곽 가림으로 사용한다. `ConnectedRoomPatch.foregroundMask`를 전경에 바인딩하고 `ConnectedRoom.shader`에서 원본/반사 샘플의 alpha를 같은 UV로 적용한다. 기존 수평 cutoff는 1로 해제했다. 하단 네 구간과 봉인된 하단 면만 정렬 순서 20으로 앞에 그리며, 열린 문 중앙은 유지한다. 승인된 경계 두께 및 문 진입 조건도 유지한다.

PNG 확인 결과 중앙 alpha는 0, 하단 벽 alpha는 253/255다. 하단 안쪽 윤곽의 높이는 Y=780~787로 변화하며, 원본 검은 윤곽과의 높이 차이는 표본의 중앙값 1픽셀이다. 완전히 동일한 픽셀 단위 추출이라고 단정하지 않는다. 자동 검증은 캔버스 크기·바닥 투명도·벽 불투명도·잎 윤곽의 높이 변화를 검사한다.

`output/artwork-wall-silhouette.log`: 16개 템플릿 적용 및 반복 적용, 중앙 봉인 경계, 문 이동 의도, Room-0 및 Fixed Room Graph 검증과 방 크기별 화면 생성을 종료 코드 0으로 완료했다. `output/fairy-village-wall-depth-preview.png`에서 수평 가림 대신 벽 윤곽이 플레이어·몬스터 앞에 겹쳐지는 모습을 확인했다. 승인된 경계가 가까워 벽에 붙으면 몸 상당 부분이 가려질 수 있다. 실제 조작 화면의 사용자 평가는 새 Run에서 추가 확인한다.
## 하단 경계 50% 확대 (2026-10-02)

투명 전경에 대한 사용자 승인 후 몸이 너무 많이 가려진다는 피드백에 따라, 하단 경계 두께를 직전 값의 1.5배로 늘렸다. 하단 계수는 0.7에서 1.05로 변경했으며 기본 방에서는 약 0.784에서 1.176 유닛으로 증가한다. 캐릭터와 몬스터가 일반 하단 벽에서 약 0.392 유닛 더 안쪽에서 멈춘다. 위·좌·우 경계와 벽 윤곽의 투명 전경, 열린 문 통로 및 문 진입 조건은 유지한다. 하단 봉인 면에도 같은 깊이를 적용한다.

`output/artwork-bottom-boundary-expanded.log`: 16개 템플릿 반복 적용, 전경 투명도·중앙 봉인 경계·문 진입·Room-0·Fixed Room Graph 검증과 화면 생성을 종료 코드 0으로 완료했다. `output/fairy-village-wall-depth-preview.png`에서 벽이 몸을 가리는 양이 줄어든 것을 확인했다. 새 Run에서 실제 조작감을 추가 확인한다.
## 하단 문 전경 및 닫힌 문 중앙 경계 (2026-10-02)

사용자 요청에 따라 하단 경계를 직전 두께의 40%로 줄였다(계수 1.05 → 0.42, 기본 방 약 1.176 → 0.470 유닛). 상단·좌우 두께와 기존 문 트리거·진입점은 유지한다.

하단 문에만 정렬 순서 20의 전경을 추가했다. 내장 imagegen으로 `door-open-foreground-cutout-v1.png`와 `door-closed-foreground-cutout-v1.png`를 생성했다. 생성 요청문은 `output/door-foreground-prompts.txt`에 보존한다. 원본 RGB와 생성된 alpha를 결합하며, 열림 상태에서는 문틀만 앞에 그리고 통로 내부는 투명하게 유지한다. 닫힘과 열쇠 잠금에는 목재까지 불투명한 윤곽을 적용한다. 상태 전환 시 `FairyVillageDoorArtwork`가 전경 Sprite와 mask를 함께 갱신한다. 다른 방향의 문은 전경을 추가하지 않는다.

모든 방향의 문에 `Fairy Village Closed Door Boundary`를 추가했다. `DoorController`가 전투 잠금 또는 열쇠 잠금일 때 내부 중앙 Collider를 켜고, 두 잠금이 모두 풀리면 즉시 해제한다. 문 없는 면 및 봉인 면의 경계는 별도로 유지한다. 새 내부 경계로 기존 열쇠 진입 트리거에 접근할 수 없게 되는 것을 방지하려고 `ClosedDoorBoundary`에서 의도적으로 문을 향해 움직이는 접촉에 한해서 열쇠 잠금을 해제한다. 이 접촉은 방 전환을 하지 않으며 이후 기존 트리거에서 진입한다. 옆 이동·정지·열쇠 없음·반복 접촉 시 소비하지 않고, 전투 중에는 해제하지 않는다.

`output/artwork-lower-door-boundaries.log`: Unity 배치 적용 및 반복 적용, 16개 템플릿의 전경 방향·닫힘/열림/키 잠금의 중앙 경계, 네 방향 열쇠 접촉 검증, Room-0·Fixed Room Graph 검증과 화면 생성을 종료 코드 0으로 완료했다. 화면 `fairy-village-lower-door-open-preview.png`, `fairy-village-lower-door-closed-preview.png`, `fairy-village-lower-door-locked-preview.png`를 확인했다. 문틀에 바짝 붙으면 문 전경이 몸을 많이 가릴 수 있으며, 실제 조작감은 새 Run에서 추가 확인한다.
## 하단 경계 47% 조정 (2026-10-02)

사용자 요청에 따라 동일 기준에서 하단 경계를 40%에서 47%로 높였다. 계수는 0.42에서 0.4935(1.05×0.47)로 변경했으며 기본 방의 두께는 약 0.470에서 0.553 유닛으로 증가했다. 벽·하단 문의 전경, 다른 방향의 경계 및 잠금/열림 조건은 유지한다. `output/artwork-bottom-boundary-47-percent.log`의 16개 템플릿 적용·반복 적용·중앙 경계·열쇠 접촉·Room-0·Fixed Room Graph 검증과 화면 생성은 종료 코드 0으로 통과했다. 새 Run에서 체감을 확인한다.
## 방 크기별 하단 가림 깊이 통일 (2026-10-02)

사용자가 Small은 덜 가려지고 다른 방은 많이 가려진다는 Play 사진을 제공했다. Small의 벽 그림은 0.75배지만 캐릭터와 Collider 반지름은 일정하다. 이전처럼 외곽에서 벽 배율에 비례한 두께만 적용하면 캐릭터 중심과 그림 속 벽 윗윤곽의 거리가 방마다 달라졌다.

Small의 승인된 47% 설정은 그대로 보존한다. 다른 크기는 등록된 하단 잎 윤곽(Y≈783) 위치를 기준으로 Small과 같은 월드 단위 침투 깊이를 적용한다. 기본·넓은·세로·큰 방에서는 하단 경계를 약 0.192 유닛 더 안쪽으로 맞추며, Small은 기존 경계를 유지한다. 봉인 및 닫힌 문의 하단 경계도 같은 계산을 사용한다. 다른 방향의 두께, 전경 그림과 문 진입 조건은 유지한다.

검증은 인스턴스의 실제 하단 Collider bounds와 전경 Sprite bounds를 비교하여 16개 템플릿에서 동일한 반지름 0.5의 캐릭터 중심과 벽 윤곽 사이 거리의 차이가 0.01 유닛 미만인지 확인한다. `output/artwork-room-size-occlusion.log`: 반복 적용·방 크기별 가림 깊이·전경 투명도·잠금 경계·열쇠 접촉·Room-0·Fixed Room Graph 검증과 화면 생성을 종료 코드 0으로 완료했다. Small 및 Large의 `fairy-village-small-wall-depth-preview.png`, `fairy-village-large-wall-depth-preview.png`를 비교 확인했다. 실제 Play에서의 체감은 새 Run에서 확인한다.
## 작은 방 기준 52% 조정 (2026-10-02)

방 크기별 가림 깊이가 같아졌다는 사용자 Play 확인 후, Small 기준을 47%에서 52%로 5퍼센트포인트 높였다. 기준 계수는 0.4935에서 0.546(1.05×0.52)으로 변경했다. 모든 방의 하단 경계가 약 0.044 유닛 안쪽으로 이동하며, 크기별 동일한 가림 깊이와 전경·문 상태 동작을 유지한다. `output/artwork-bottom-boundary-52-percent.log`에 16개 템플릿 적용·반복 적용·가림 깊이·잠금 경계·열쇠 접촉·Room-0·Fixed Room Graph 검증 및 화면 생성 결과를 남겼다. 새 Run에서 체감을 확인한다.
## 작은 방 기준 60% 조정 (2026-10-02)

사용자 요청에 따라 Small 기준을 52%에서 60%로 높였다. 기준 계수는 0.546에서 0.63(1.05×0.60)으로 변경했다. 모든 방의 하단 경계가 직전 설정보다 약 0.071 유닛 안쪽으로 이동하며, 방 크기별 동일한 가림 깊이와 전경·문 상태 동작을 유지한다. `output/artwork-bottom-boundary-60-percent.log`에 16개 템플릿 적용·반복 적용·가림 깊이·잠금 경계·열쇠 접촉·Room-0·Fixed Room Graph 검증 및 화면 생성 결과를 남겼다. 새 Run에서 체감을 확인한다.
## 작은 방 기준 80% 조정 (2026-10-02)

사용자 요청에 따라 Small 기준을 60%에서 80%로 높였다. 기준 계수는 0.63에서 0.84(1.05×0.80)로 변경했다. 모든 방의 하단 경계가 직전 설정보다 약 0.176 유닛 안쪽으로 이동하며, 크기별 동일한 가림 깊이와 전경·문 상태 동작을 유지한다. `output/artwork-bottom-boundary-80-percent.log`의 16개 템플릿 적용·반복 적용·가림 깊이·잠금 경계·열쇠 접촉·Room-0·Fixed Room Graph 검증 및 화면 생성은 종료 코드 0으로 완료했다. 새 Run에서 체감을 확인한다.

## 특수 방 출입구 (2026-10-02)

기존 연결 벽·잔디를 유지하고 내장 imagegen으로 네 종류의 출입구를 생성했다. `Assets/Rooms/Artwork/FairyVillage/room-{shop,treasure,boss,secret}-open-v1.png`에 네 방향을 등록했으며, 상점·보물방·보스방은 `room-*-closed-v1.png`도 추가했다. 생성 요청문은 `output/special-door-prompts.txt`에 보존한다.

- 상점: 사용자 금화 참고의 잎 무늬 금화, 기존 목재와 돌기둥, 닫힌 목재 문짝.
- 보물방: 흰 대리석 기둥, 금색 구조물과 왕관, 잠긴 금색 문짝·사슬·자물쇠.
- 보스방: 자주색 구조물, 철제 보강과 뿔 장식, 무거운 닫힌 문짝.
- 비밀방: 부서진 돌과 찢긴 수풀로 둘러싸인 구멍. 발견 전에는 기존 봉인 벽을 유지한다.

각 상태의 `door-*-cutout-v1.png`는 별도로 생성한 투명 전경 윤곽이다. 아래 문만 정렬 순서 20으로 캐릭터·몬스터 앞에 그리며, 열린 구멍은 투명하고 닫힌 문짝은 불투명하다. 원본 색과 전경 alpha를 결합하는 기존 방식과 Small 기준 하단 경계 80%는 유지한다. 생성 캔버스의 가로 1픽셀 차이는 정규화한 Sprite 슬라이스 좌표로 등록한다.

`DoorVisualKind` 기존 숫자는 유지하고 끝에 Shop을 추가했다. 방 역할로 양방향 출입구의 종류를 결정하므로 열쇠 잠금을 해제해도 상점·보물방 외형을 유지한다. 잠금 소비, 충돌, 폭탄 발견·진입 규칙은 기존 런타임을 사용한다. Backend·Web 데이터 계약은 변경하지 않는다.

수동 확인: 새 Run을 시작하여 상점 금화, 보물방 왕관·대리석, 보스 장식을 네 방향에서 확인한다. 비밀벽은 폭탄으로 발견하기 전에는 평범한 벽, 발견 후에는 뚫린 구멍이어야 한다. 아래쪽 각 출입구에 접근하면 장식 윤곽만 캐릭터 앞에 겹치고 열린 통로에는 캐릭터가 보여야 한다. 적용 메뉴는 `Trickal Fan Game/Artwork/Apply Fairy Village Tiles and Walls`, 검증 메뉴는 `Trickal Fan Game/Artwork/Verify Fairy Village Tiles and Walls`이다.

검증 근거: `output/artwork-special-doors.log`에서 16개 템플릿·문 진입·Room-0·보물방 열쇠 소비와 비밀벽 폭탄 발견/재방문 검증을 통과했다. 이어 실행한 상점 검증은 편집 모드에서 직전 비밀방 이동의 쿨다운이 남아 실패했으므로 검증 사이에 Game Scene을 다시 여는 방식으로 격리했다. `output/artwork-special-doors-shop-render.log`에서 상점 열쇠·구매·재방문 검증과 전체 문 렌더링을 종료 코드 0으로 완료했다. 확대 렌더링에서 왕관 보석의 붉은색이 기존 크로마 필터에 제거되는 현상을 확인해 near-pure red만 제외하도록 조정했고, Sprite 교체 시 atlas 좌표도 갱신한다. `output/artwork-special-doors-final-render.log`의 최종 전경 검증·렌더링 및 `output/artwork-special-doors-idempotence.log`의 반복 적용·문 경계 검증은 모두 종료 코드 0이다. 기존 방 master의 Sprite 식별자와 프리팹 GUID를 보존했다.

조합 화면 `output/fairy-village-special-doors-gallery.png`는 위 보스·아래 보물·왼쪽 상점·오른쪽 비밀방을 한 방에 배치한 비교용 렌더링이다. `output/fairy-village-{shop,treasure,boss,secret}-depth-preview.png`와 닫힌 문 화면에서 벽 연결 및 전경 윤곽 가림을 확인했다. 실제 게임은 생성된 연결 방의 역할을 따라 각각의 출입구를 선택한다.

## 특수 세로 문 구조 수정 (2026-10-02)

후속 수정: 과도한 입체 구조 생성은 사용자 중단 요청으로 채택하지 않았다. 이후 기존 v2를 기준으로 보물방 측면의 대리석 기둥과 잎 경계 기울기를 완만하게 조정하고 좁은 음영만 보강했다. 보스방은 측면 해골과 두 뿔을 문틀의 기둥 사이 중앙 쪽으로 옮겼다. 열림·닫힘 master `room-{treasure,boss}-{open,closed}-v3.png`의 좌우 슬라이스만 적용하고 위·아래, 상점, 비밀방은 이전 참조를 유지한다. 내장 imagegen 요청문은 `output/side-door-v3-subtle-prompts.txt`에 보존한다. `output/artwork-side-doors-v3-subtle.log`에서 16개 템플릿 반복 적용·충돌/GUID 보존·가림·Door-1·Room-0·Fixed Room Graph 검증 및 렌더링이 종료 코드 0으로 완료되었다. 좌우 열림·닫힘 8개 `fairy-village-{treasure,boss}-{left,right}-{open,closed}-v3-preview.png`를 확인했다. 실제 조작과 미감 평가는 새 Run에서 확인한다.

사용자가 직접 기울인 참고 그림을 기준으로 내장 imagegen에서 보물·보스·상점의 좌우 문을 다시 생성했다. 보물방은 왕관을 바깥쪽 금색 아치의 중앙에 배치하고 위·아래 대리석 기둥과 함께 기울인다. 보스방은 두 뿔과 해골 장식이 있는 자주색 문틀을 열림·닫힘 상태에서 같은 구조로 사용한다. 상점의 닫힌 세로 문은 벽 길이 방향으로 두 문짝을 나누고 중앙의 비스듬한 가로 틈 양쪽에 문고리를 하나씩 배치한다.

상점 메달은 새 참고의 두꺼운 금색 테두리, 황갈색 안쪽 원판, 입체적인 잎을 반영하여 네 방향에 적용했다. 보물·보스의 위·아래 문은 v1을 유지하고 좌우에만 v2를 사용한다. 비밀방은 아래 구멍의 상단에 연결된 잎과 깨진 돌 테두리를 추가한 v2를 사용하며, 다른 방향은 v1을 유지한다. 상점과 비밀방의 아래쪽 전경 alpha도 새 master에 맞추어 생성했다. 요청문은 `output/side-door-v2-prompts.txt`에 저장했다.

생성 캔버스의 1~2픽셀 차이를 정규화된 슬라이스와 원래 문 패치의 월드 크기 보존으로 처리한다. 특수 문 패치의 가장자리에는 기존 문 주변 색을 섞어 사각형 잔디 색 이음새를 완화한다. 외곽의 왕관·뿔이 흐려지지 않도록 외측 혼합 폭은 2픽셀로 제한한다. 이동 경계는 승인된 Small 기준 80%를 유지하며 런타임 잠금·열쇠·폭탄 규칙과 Backend·Web 계약은 변경하지 않았다.

`output/artwork-side-doors-v2.log` 및 `output/artwork-side-doors-v2-final.log`: Unity 컴파일, 16개 템플릿 반복 적용과 기존 충돌·GUID 보존, 방 크기별 가림 깊이, 열린/닫힌 문 전경 투명도, 비밀문 상단 alpha, Door-1, Room-0 및 Fixed Room Graph 검증을 종료 코드 0으로 완료했다. `output/fairy-village-{shop,treasure,boss}-{left,right}-{open,closed}-v2-preview.png`의 12개 확대 렌더링과 비밀방 하단 전경 화면을 확인했다. 실제 조작 확인은 새 Run에서 특수 방 연결 문과 폭탄 발견 후 아래 비밀문을 확인한다.

## 벽 경계를 방 격자에 맞춤 (2026-10-11)

사용자 결정으로 벽 안쪽 경계를 그림 비율이 아니라 방 격자에 맞췄다. `FairyVillageArtworkSetup.WalkableFloor`는 그림에서 계산한 바닥(`PaintedFloor`, 이전 값)에 들어가는 가장 큰 정수 크기 사각형을 방 중심에 두어 돌려준다. Small 10×5, Basic 13×7, Wide 21×7, Tall 13×11, Large 21×11이며 방 Layout 임포터(T7)의 격자와 같다.

- Basic 기준 경계가 좌우 0.36, 위 0.36 안쪽으로 들어왔고 아래는 0.04 내려갔다. 좌우는 그림 속 풀밭이 경계 밖으로 0.18 남는다.
- 방 높이가 정수가 아니어서 아래 덤불 뒤로 가려지는 깊이가 프로필마다 최대 0.25 다르다. 검증기의 "모든 방이 같은 깊이" 조건은 이 범위와 "몸 중심은 덤불 위"로 바꿨다.
- 검증: Unity 6000.3.22f1 배치에서 `FairyVillageArtworkVerification.SetupAndVerifyBatch`(30개 템플릿, Door-1 회귀 포함)와 `RoomLayoutImporterVerification.ImportAndVerifyBatch`가 종료 코드 0으로 통과했다. 사용자가 2026-10-11 Play 화면에서 벽 위치와 벽 옆 장애물을 확인했다.

수동 확인: 새 Run에서 네 벽에 붙어 걸을 때 멈추는 위치, 벽 옆 칸의 장애물이 벽에 빈틈없이 붙는지, Small·Tall·Large에서 아래 덤불에 가려지는 정도가 어색하지 않은지 확인한다.
