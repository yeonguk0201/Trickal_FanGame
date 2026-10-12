# 용사용 검광·내려찍기 VFX 제작 대안

## 프레임 시트 없이 검광의 형태와 움직임을 만드는 방법은?

### Takeaway
검광은 직선 빔용 Line Renderer보다 공격 궤적에 맞춰 만든 곡면 메시와 UV 셰이더 조합이 적합하다. 실제 무기 움직임에 반응하는 궤적에는 Trail Renderer 또는 두 점을 잇는 사용자 메시 트레일이 대안이며, 모든 경우에 같은 기법이 우월하지는 않다.

### Cited Findings
- 작가 Geri의 Unity Venom Slash는 중심을 가리는 알파 마스크를 넣은 고리 모양 메시를 사용한다. 정적인 메시 위에서 두 텍스처를 곱하고 첫 노이즈를 스크롤해 검광의 움직임을 만든다. 색은 Color Ramp와 Glow, 보조 물보라는 4개 도형 아틀라스와 dissolve map으로 처리한다. 즉 완성된 검광 프레임을 순서대로 보여주는 사례가 아니다. — [제작자 직접 분해 설명](https://realtimevfx.com/t/venom-slash-breakdown-of-the-effect-included/18903)
- Gabriel Aguiar는 Sword Slash 작품을 Shader Graph와 VFX Graph로 제작했다고 명시한다. 이 페이지 자체는 노드 세부 구조까지 설명하지 않으므로 그의 모든 검광이 동일 구조라고 추정하면 안 된다. — [작가 작품 페이지](https://gabrielaguiarproductions.artstation.com/projects/G82XxB)
- Unity Trail Renderer는 이동에 따라 점을 추가하며 점 수명, 길이 방향 폭 곡선, 색 그라데이션, 카메라 또는 Transform Z 방향 정렬, Stretch/Tile 텍스처 모드를 제공한다. 폭·색 곡선의 정밀도는 정점 수에 영향을 받는다. — [Unity 6.0 문서](https://docs.unity3d.com/6000.0/Documentation/Manual/class-TrailRenderer.html)

### Inferences
- 프로젝트 권장: 용사용은 위에서 내려오는 쐐기/초승달 메시를 별도 제작하고 UV reveal → 최대 밝기 → 꼬리 erosion을 시간 곡선으로 제어한다. 단순히 이미지 전체를 확대·축소하는 것보다 머리와 꼬리를 다르게 제어하기 쉽다.
- 실제 칼 궤적이 없는 아이템 효과라면 Trail Renderer를 억지로 붙이는 것보다 연출용 곡면 메시가 더 예측 가능하다. 실제 무기 양 끝의 움직임이 있는 공격은 두 점을 샘플링한 리본 메시가 더 정확한 모양을 줄 수 있다. 후자는 본 조사에서 프로젝트 구현까지 검증하지 않았다.
- 셰이더도 재료 텍스처를 쓸 수 있다. 텍스처를 쓴다는 사실과 완성된 정적 이미지를 늘린다는 방식은 다르다. 핵심 차이는 무엇이 시간에 따라 바뀌는가이다.

### Gaps
- 용사용의 현재 화면·타이밍을 이 조사에서 직접 검수하지 않았다. 부자연스러움의 원인을 메시 부재로 확정할 수 없다.
- 사용자 메시 트레일은 구현·정렬 비용이 있다. 프로젝트 실제 카메라·정렬 레이어에서 작은 실험이 필요하다.

## Houdini·Blender 시뮬레이션이나 손그림을 섞는 더 높은 단계도 있나?

### Takeaway
오프라인 시뮬레이션을 구워 메시 애니메이션으로 재생하는 VAT가 존재한다. 그러나 작은 SD 캐릭터의 짧은 검광에는 메시·셰이더·파티클 합성이 먼저이며, 손그림은 실루엣과 고유한 모양을 보완하는 선택지다.

### Cited Findings
- SideFX의 Unity Live Session은 Houdini 절차형 에셋과 Vertex Animation Textures를 이용한 복잡한 효과 애니메이션을 다룬다. 실시간으로 Houdini 시뮬레이션 전체를 계산해야만 하는 방식은 아니다. — [SideFX 공식 Unity 세션](https://www.sidefx.com/tutorials/building-levels-procedurally-vertex-animation-textures-in-unity/)
- Unity VFXToolbox는 DCC용 Vertex Animation Texture 도구와 Unity 샘플을 문서화한다. 이는 Unity에서도 메시 변형을 텍스처 데이터로 굽고 재생하는 경로가 있음을 보여준다. 기존 샘플 문서이므로 현재 URP 17.3 호환은 별도 검증 대상이다. — [Unity 공식 저장소](https://github.com/Unity-Technologies/VFXToolbox/blob/master/Documentation~/DCCTools.md)
- Geri의 검광은 메시 기반 본체와 도형 아틀라스 기반 보조 파티클을 함께 사용한다. 따라서 메시와 이미지 재료를 섞는 하이브리드는 실제 제작 사례가 있다. — [제작자 분해 설명](https://realtimevfx.com/t/venom-slash-breakdown-of-the-effect-included/18903)

### Inferences
- VAT는 특별한 물보라·덩어리 액체·부서지는 오브젝트처럼 메시 변형이 눈에 띄는 대표 효과에 검토할 가치가 있다. 짧고 작은 검광에 먼저 도입하면 제작 파이프라인 비용 대비 차이가 작을 수 있다.
- 손그림 마스크로 날카로운 검광 가장자리와 과장된 impact shape를 만들고, 셰이더로 이를 reveal/왜곡/소멸시키면 SD 캐릭터와 스타일을 맞추기 쉽다. 손그림 프레임을 전부 없애는 것이 목표가 될 필요는 없다.
- Blender는 연출용 곡면 메시와 UV 제작 도구로도 쓸 수 있다. Blender 시뮬레이션의 특정 Unity 내보내기 경로는 이번 조사에서 검증하지 않았다.

### Gaps
- VAT가 이 프로젝트에서 좋은 투자라는 근거는 아직 없다. 플랫폼·메모리·셰이더 호환성·제작 숙련도를 확인해야 한다.
- 특정 판매 에셋을 구매하거나 이식해 품질이 개선된다는 보장은 조사하지 않았다.

## 홍보 화면에서 매력적으로 보이게 만드는 핵심은 기술의 복잡도인가?

### Takeaway
공식 강연과 평가 기준은 기술 이름보다 형태·명암·색·타이밍·전체 흐름을 강조한다. 프로젝트 권장은 먼저 실제 게임 카메라에서 읽히는 대표 효과 하나를 완성하고, 그 제작 규칙을 다른 효과에 확장하는 것이다.

### Cited Findings
- Blizzard의 Hadidjah Chamberlain와 Riot의 Jason Keyser가 진행한 GDC 2017 Artistic Principles of VFX는 장르·아트 스타일에 적용 가능한 예술·게임플레이 원칙을 League of Legends와 Hearthstone 사례로 다룬다. 페이지에서 확인한 것은 강연 개요이며 영상 전 내용을 시청한 것은 아니다. — [GDC 공식 강연 페이지](https://gdcvault.com/play/1023943/Visual-Effects-Bootcamp-Artistic-Principles)
- Riot Creative VFX Contest의 공식 기준은 Story, Satisfaction/Entertainment, Style Match이며 Style Match에 hue range, value, timing, shape language를 포함한다. 확인은 검색 인덱스의 공식 페이지 내용으로 했고 직접 본문 fetch는 시간 초과했다. — [Riot 공식 심사 기준](https://www.riotgames.com/en/news/vfx-contest-league-valorant-2023)

### Inferences
- 용사용은 준비 동작 → 빠른 검광 도착 → 접지 순간의 짧은 강한 밝기 → 뒤늦게 남는 먼지·파편 순서를 분명히 만들 것을 권장한다. 검광과 지면을 하나로 확대하지 않고 독립 레이어로 설계한다.
- 홍보용 첫 개선 목표는 노이즈와 파티클을 늘리는 것이 아니라 작은 화면에서도 보이는 고유한 실루엣, 캐릭터를 가리지 않는 명암 배분, 공격과 정확히 맞는 충돌 순간이다. 이는 위 원칙을 이 프로젝트에 적용한 판단이다.
- 후보 비교는 동일 카메라·배경·캐릭터·크기로 ① 메시 셰이더 ② 트레일 셰이더 ③ 손그림 마스크 하이브리드를 짧게 나란히 재생하고 실제 게임 크기로 평가하는 것이 유용하다.

### Gaps
- 특정 기술로 홍보 전환율·매출이 개선된다는 인과 증거는 확보하지 않았다. 미술적 가독성 기준과 상업적 성과는 구분해야 한다.