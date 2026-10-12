# Unity URP 2D 전투 VFX 제작 도구와 제작 흐름

## Particle System, VFX Graph 중 무엇이 적합한가?

### Takeaway
샤샤의 짧은 물 분출과 용사용 검광은 Particle System + 전용 셰이더 + 메시를 우선 검토할 만하다. VFX Graph는 추가 선택지이며 설치 자체가 미술 품질 상승을 보장하지 않는다(프로젝트 적합성에 대한 판단).

### Cited Findings
- Unity Built-in Particle System은 URP와 호환되고 C#에서 입자를 읽고 쓰며 충돌 이벤트에 대응할 수 있다. VFX Graph는 GPU 시뮬레이션과 노드 기반 저작을 제공한다. — [Unity 공식 비교](https://docs.unity3d.com/6000.0/Documentation/Manual/ChoosingYourParticleSystem.html)
- VFX Graph 17.3 공식 요구 사항은 compute shader와 SSBO 지원을 요구한다. OpenGL ES를 지원하지 않고 URP에서 Gamma 색 공간을 지원하지 않는다. 문서는 URP와 모바일 지원에 여전히 preview 범위 제약을 명시한다. — [17.3 Requirements](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.3/manual/System-Requirements.html)
- VFX Graph가 2D Renderer에서 절대 불가능하다는 설명은 틀리다. Unity는 VFXSpriteLit / VFXSpriteUnlit Shader Graph를 선택해 2D Renderer 및 2D 조명과 연결하는 절차를 제공한다. 다만 이 확인 문서는 URP 17.0 버전이다. — [2D Renderer VFX Graph](https://docs.unity.cn/Packages/com.unity.render-pipelines.universal@17.0/manual/2d-visual-effect-graph-compatibility.html)
- 프로젝트 파일 직접 확인: game/Packages/manifest.json에서 URP 17.3.0, Particle System 모듈 확인; Visual Effect Graph 직접 의존성은 없음. game/ProjectSettings/ProjectVersion.txt는 6000.3.22f1. ProjectSettings.asset의 m_ActiveColorSpace: 1과 Assets/Settings/Renderer2D.asset도 확인했다. — 로컬 파일 근거, 2026-10-12 조사.

### Inferences
- 한 번에 엄청난 입자가 필요한 것이 아닌 2D 전투 효과라면 이미 사용 가능한 Particle System으로 물방울, 먼지, 파편을 만들고 셰이더를 붙이는 것이 도입 부담이 적다.
- VFX Graph는 대규모 입자, 복잡한 데이터 흐름, 전장 전체 마법 폭풍 등 명확한 요구가 생겼을 때 시제품을 비교할 후보다. 지금 HDRP로 변경하거나 Unity를 업그레이드할 이유로 삼을 필요는 없다.
- 실제 프로젝트의 linear 색 공간은 알려진 gamma 제한에는 걸리지 않지만 타깃 그래픽 API, 정렬, build 동작은 별도 확인해야 한다.

### Gaps
- 타깃 플랫폼과 그래픽 API, 동시 효과 수가 확정되지 않아 두 시스템의 실제 성능 우열은 판단하지 않았다.
- URP 17.3 2D 호환성 페이지 직접 조회가 실패하여 17.0 문서와 17.3 시스템 요구 사항을 구분해서 사용했다.

## UV 스크롤보다 표현력을 높이는 실시간 방식은?

### Takeaway
Line Renderer는 효과의 뼈대일 뿐이다. 이를 유지해도 셰이더로 외곽 형태와 내부 흐름을 만들고 입자를 합성하면 결과가 달라진다. 메시가 필요한 것은 선 이상의 실루엣과 변형을 통제하려는 경우다.

### Cited Findings
- URP Sprite Unlit Shader Graph는 vertex position, fragment 색과 alpha, alpha clipping 및 Alpha/Premultiply/Additive/Multiply 혼합을 제공한다. — [Sprite Unlit Shader Graph](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/built-in/shaders-in-universalrp/prebuilt-shader-graphs-urp/sprite-unlit)
- SideFX의 Flowmap Shader는 텍스처 또는 vertex color의 방향 데이터를 사용해 UV를 왜곡하고 시간·속도·왜곡 강도를 조절한다. 이는 흐름장을 단순한 동일 방향 스크롤과 구분하는 근거다. — [Flowmap Shader](https://www.sidefx.com/docs/houdini/nodes/shop/labs--flowmap_shader.html)

### Inferences
- 샤샤: 길이와 폭을 실시간 갱신하는 메시 또는 Line Renderer + 두 노이즈 레이어의 UV 왜곡 + threshold로 셀풍 실루엣을 만드는 셰이더 + 월드 공간 물방울 + 충돌부 파티클을 합성하는 구성이 적합하다.
- Flowmap은 강·폭포처럼 위치별로 다른 흐름이 필요할 때 유용하다. 직선 짧은 분출에서는 우선 두 레이어 노이즈 왜곡만으로 충분한지 비교하는 것이 현실적이다.
- 용사용: 검광의 늘어남/좁아짐을 메시나 셰이더로 만들고 지면 충격, 바위, 먼지를 별도로 재생한다. 시작·타격·잔상 시간 곡선은 그래픽 도구와 독립적인 품질 요소다.
- 불투명에 가까운 셀풍 코어에 제한된 Additive 하이라이트를 겹치는 방향을 우선 비교할 만하다. 모든 레이어를 광택·Bloom으로 처리하면 SD 캐릭터와 색의 위계가 맞지 않을 수 있다.

### Gaps
- 현재 플레이 동영상의 프레임별 검수 없이 부자연스러움의 원인을 특정하지 않는다. 발사점 추적, 월드/로컬 공간, 벽 끝점, 레이어 정렬 문제는 셰이더 변경과 별도로 남을 수 있다.

## 외부 시뮬레이션 도구를 쓰면 더 좋아지는가?

### Takeaway
외부 도구는 자연스러운 움직임을 만드는 재료 제작에 유용하다. 그러나 게임에서 완전 실시간 유체를 돌리는 것과 외부에서 만든 움직임을 베이크해 재생하는 것은 다르다. 이번 요구에는 절차적 실시간 코어를 우선하고 고급 베이크는 보조 효과 후보로 보는 것이 적합하다.

### Cited Findings
- Houdini Labs Flipbook Textures는 실시간 엔진용 텍스처를 렌더·합성·내보낸다. motion vector pass는 프레임 사이 부드러운 보간을 지원하며 normal/depth/relighting용 데이터도 생성한다. 단순히 정지 그림을 교체하는 것보다 고급 재생이 가능하다는 근거다. — [Houdini Labs Flipbook Textures](https://www.sidefx.com/docs/houdini/nodes/out/labs--flipbook_textures-1.0.html)
- JangaFX 공식 안내: EmberGen은 불·연기·폭발 등의 volumetric fluid를 만들고 flipbook/image sequence/VDB를 출력한다. LiquiGen은 액체, foam/spray/bubbles와 flipbook/image sequence/alembic 출력에 초점을 둔다. IlluGen은 타일 노이즈, flowmap, FX mesh, mask와 distortion 등 재료 생성 도구다. — [JangaFX 공식 도구 문서](https://docs.jangafx.com/)

### Inferences
- EmberGen을 물줄기의 대표 해결책으로 추천하지 않는다. 물에는 LiquiGen 쪽이 범위상 직접적이지만 새 도구 학습과 출력물을 우리 그림체로 조정할 비용이 있다.
- 고급 flipbook은 사용자가 묻는 실시간 방식의 대체재 전체가 아니다. 물줄기 코어를 길게 늘이는 데 쓰기보다 충돌 물보라, 먼지, 폭발 등 한 지점의 복잡한 순간 효과에 선택적으로 적합하다.
- Houdini나 LiquiGen을 새로 구매하기 전에 Unity만으로 샤샤 한 개의 대표 효과를 시제품 제작해 동영상으로 비교하는 편이 좋다. 실루엣, 색, 시작-타격-잔상, 캐릭터 가독성이 만족되는지 평가하고 부족한 재료만 외부 제작한다.
- 수작업/전문 VFX 제작 의뢰도 유효한 생산 방식이다. 도구 선택보다 레퍼런스와 그림체의 일관성을 유지하는 작업이 실제 홍보 화면의 품질을 좌우할 가능성이 높다(미술적 판단이며 정량 근거는 없음).

### Gaps
- 제품 가격, 라이선스, 최신 Unity importer의 호환성은 이번 조사에서 검증하지 않았으므로 구매 권고를 하지 않는다.
- Blender는 이 조사에서 구체적인 VFX bake 워크플로를 직접 검증하지 않았다. 단순히 무료라는 이유만으로 권장 도구 목록에 추가하지 않는다.
