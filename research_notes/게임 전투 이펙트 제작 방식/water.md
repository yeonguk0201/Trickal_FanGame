# 샤샤 물줄기: Line Renderer UV 스크롤 이후의 선택지

## 1. 단순 UV 스크롤보다 유기적인 실시간 표현은 무엇인가?

### Takeaway
추천은 길이와 폭을 제어하는 스트립 메시, 물결을 변형하는 셰이더, 독립적인 물방울 입자의 합성이다. 이는 단순 스크롤의 확장이며, Line Renderer 자체를 버려야만 고품질이 되는 것은 아니다.

### Cited Findings
- Unity Shader Graph는 Vertex Position을 변경할 수 있다. 표면 무늬만 이동하는 것과 달리 실제 메시 형태를 변형할 수 있는 기반이다. — [Unity ShaderGraph Vertex Position](https://learn.unity.com/tutorial/shadergraph-vertex-position)
- Flowmap은 RG 채널에 흐름 방향을 저장한다. 제작자의 Unity 사례는 2D 유체 계산으로 flowmap을 생성해 표면의 흐름 방향을 제어한다. — [Omid Saadat: Open Flowmap](https://www.omid-saadat.com/writing/open-flowmap/)
- Unity VFX Graph의 strip output은 독립된 입자 대신 연결된 연속 표면을 렌더링하고, custom Shader Graph strip도 지원한다. — [Unity Output Strip Contexts](https://docs.unity.cn/Packages/com.unity.visualeffectgraph@17.6/manual/Context-OutputStrip-LandingPage.html)
- Gabriel Aguiar의 Tsunami 예제는 stylized water shader와 texture를 함께 제작하는 예제이며 실제 Riot 게임의 구현을 공개한 자료는 아니다. — [Artist tutorial](https://gabrielaguiarproductions.artstation.com/projects/Ze8Y31)

### Inferences
- 샤샤에는 시작부를 고정한 메시의 폭·가장자리에 낮은 진폭의 변형을 주고, 몸통 내부는 다른 속도로 흐르게 하는 방식이 적합하다. 화면상 한 물질이 움직이는 인상을 얻으려면 외곽과 내부 변화가 필요하다.
- Flowmap은 흐름 방향이 휘거나 합쳐지는 경우에 의미가 크다. 곧은 짧은 물줄기라면 다층 노이즈와 UV 왜곡만으로 충분할 수 있어 flowmap을 무조건 추가하지 않는다.
- 리본은 이미 방출한 물이 월드에 남고 새 물이 새 발사점에서 나오는 궤적에 유리하다. 그러나 즉시 직선 판정인 스킬에서 본체를 크게 휘게 만들면 판정과 시각이 어긋날 수 있으므로 본체 곡률은 제한하고 잔물방울에 관성을 주는 편이 낫다.

### Gaps
- 프로젝트의 실제 Renderer Data, 색 공간, 대상 기기 성능은 이번 웹 조사에서 확인하지 않았다. 17.6 strip 문서는 개념 근거이며 프로젝트 17.3에서 해당 output 제공 여부는 도입 전에 확인해야 한다.

## 2. 파티클만으로 만들거나 유체를 실제 시뮬레이션할 수 있는가?

### Takeaway
파티클 중심 분출과 실제 유체 시뮬레이션 모두 가능하다. 전투 가독성이 필요한 짧은 물줄기는 제어 가능한 본체를 유지하고 파티클을 합성하는 쪽을 우선 권한다.

### Cited Findings
- Unity Particle System은 속도에 따라 늘어나는 Stretched Billboard와 메시 입자 렌더링을 지원한다. — [Unity Particle Renderer module](https://docs.unity3d.com/6000.0/Documentation/Manual/PartSysRendererModule.html)
- Unity의 Noise module은 Curl Noise 기반이며 노이즈 품질과 성능의 교환 관계를 설명한다. — [Unity Particle movement patterns](https://docs.unity.com/en-us/engine/6000.3/manual/particle-movement-patterns)
- Zibra의 개발사 설명은 실시간 3D 액체, 점도·밀도·표면장력, 충돌과 거품을 지원한다고 밝힌다. 동시에 실시간 유체 시뮬레이션의 자원 비용과 baked 방식의 상호작용 제약을 설명한다. 제품사 자료이며 독립 성능 검증은 아니다. — [Zibra Effects technical overview](https://www.zibra.ai/blog-posts/setting-new-standards-how-zibra-effects-advanced-real-time-simulations-bring-liquid-smoke-and-fire-to-life-in-gaming)
- VFX Graph 17.3은 URP와 호환되나 Compute Shader 및 SSBO 하드웨어가 필요하고 OpenGL ES를 지원하지 않으며 URP gamma 색 공간을 지원하지 않는다. — [Unity VFX Graph 17.3 requirements](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.3/manual/System-Requirements.html)

### Inferences
- 파티클만 쓰면 이동 관성·분출·떨어지는 방울은 자연스럽게 만들기 쉽지만 밀도가 낮으면 끊어진 스프레이로 보일 수 있다. 밀도를 높이면 투명 레이어가 중첩되므로 화면 해상도와 다중 발사 상황에서 측정해야 한다.
- 샤샤가 소방호스처럼 연속적인 굵은 줄기라면 메시 본체+입자, 산탄처럼 흩어지는 물 분출이라면 입자 중심 방식이 더 적합하다.
- 유체 시뮬레이션은 물이 쌓이고 장애물을 돌아 흐르는 게임플레이에는 가치가 있다. 짧은 직선 전투 스킬 하나의 홍보 품질을 높이려는 현재 목적에는 비용과 제어 복잡도가 큰 선택이다. 고급 기술이라는 이유만으로 우선하지 않는다.
- VFX Graph는 도구 이름이지 자동적인 품질 업그레이드가 아니다. 소규모 분출이라면 일반 Particle System으로도 필요한 입자 움직임을 구성할 수 있다.

### Gaps
- 각 방법의 정확한 FPS 비용은 대상 기기, 해상도, 입자 수, 셰이더에 따라 달라 웹 자료로 추정할 수 없다.

## 3. 프레임 시트 없이 복잡한 물 형태를 미리 제작할 수 있는가?

### Takeaway
Houdini 등의 유체·변형 시뮬레이션을 Vertex Animation Textures(VAT)로 구워 메시로 재생할 수 있다. 2D 프레임 시트와 달리 기하 형태가 움직이지만, 이미 구운 움직임이라는 제약은 남는다.

### Cited Findings
- SideFX VAT는 메시와 텍스처를 내보내 셰이더로 soft-body, rigid-body, fluid dynamic remeshing, particle sprite 효과를 재생한다. GPU에서 텍스처·셰이더로 재생하며, 문서는 텍스처 메모리·애니메이션 충돌 부재·제한적인 실시간 상호작용을 한계로 명시한다. Unity URP_VAT3 패키지도 안내한다. — [SideFX Labs VAT 3.1](https://www.sidefx.com/docs/houdini/nodes/out/labs--vertex_animation_textures-3.1.html)
- SideFX는 Unity용 복잡한 효과 애니메이션을 VAT로 만드는 발표를 제공한다. — [SideFX Unity live-session presentation](https://www.sidefx.com/tutorials/building-levels-procedurally-vertex-animation-textures-in-unity/)

### Inferences
- VAT는 일정 모양의 큰 파도·폭발하는 물덩어리·고정 충돌 물보라에 유리하다. 실시간으로 길이가 바뀌는 몸통 전체를 VAT로 처리하면 늘어남과 잘림이 부자연스러울 수 있다.
- 현재 추천 순서는 ① 셰이더 메시 본체+파티클 ② 실제 게임 이동/벽 접촉에서 비교 ③ 필요한 큰 물보라에만 VAT 검토다. 홍보 영상에서도 이펙트 단독 확대샷보다 실제 게임 카메라에서 캐릭터와 적이 읽히는지가 품질 판단의 기준이어야 한다.

### Gaps
- VAT 3.1 문서에 Unity URP 경로는 존재하지만 현 프로젝트 Unity 버전과 패키지의 무수정 호환성은 확인하지 않았다. 실제 도입에는 짧은 호환성 테스트가 필요하다.
