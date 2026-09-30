# audio-sources.md

Trickal Fan Game에 실제로 적용하기로 확정한 음원의 출처와 라이선스 정리.

| 용도 | 제목 | 제작자 | 라이선스 | 크레딧 표기 | 상업적 이용 | 출처 링크 |
|---|---|---|---|---|---|---|
| 홈 화면 BGM | Flowerbed Fields [Loop] | Zane Little Music | CC0 (Public Domain) | 불필요 (선택) | 가능 | https://opengameart.org/content/flowerbed-fields-loop |
| 전투 BGM | 探検 Exploration | nojisuma | Pixabay Content License | 불필요 | 가능 (제작자가 게임 BGM 상업적 이용 명시적으로 허용) | https://pixabay.com/music/instrumental-探検-exploration-565678/ |
| UI 클릭 효과음 | Plop! | Breviceps | CC0 (Public Domain) | 불필요 (선택) | 가능 | https://freesound.org/people/Breviceps/sounds/447910/ |

## 비고

- 세 음원 모두 크레딧 표기가 필수는 아니지만, CC0 음원(Flowerbed Fields, Plop!)은 제작자가 링크나 후원을 반가워한다고 밝혔으므로 게임 크레딧 페이지에 가볍게 언급하는 걸 권장.
- Pixabay 음원(探検 Exploration)은 Pixabay Content License 적용이라 Pixabay 자체가 서비스 종료되거나 라이선스 정책이 바뀔 가능성에 대비해, 다운로드한 원본 파일을 프로젝트 저장소에 백업해두는 걸 권장(Pixabay는 사후에 트랙이 내려가도 이미 받은 파일의 라이선스는 유지되는 방식).
- 파일 포맷: 원본은 Flowerbed Fields(확인 필요, 보통 wav/ogg), 探検 Exploration(mp3) — 게임 리소스 규칙(BGM=ogg, SFX=wav)에 맞춰 변환 후 사용.
- 확정 목록이 늘어나면 이 표에 행만 추가하면 됨 (프로젝트 문서 `bgm-sfx-후보-리스트.md`의 "후보" 성격과 구분해서, 이 파일은 "실제 적용 확정본" 용도로 유지).

## Unity 적용 경로

| 용도 | 안정 경로 |
|---|---|
| 홈 화면 BGM | `Assets/Audio/BGM/BGM_Home.ogg` |
| 전투 BGM | `Assets/Audio/BGM/BGM_Combat.ogg` |
| UI 클릭 효과음 | `Assets/Audio/SFX/SFX_UI_Click.wav` |

- 음원만 교체할 때는 위 파일명을 유지하면 Scene 참조와 런타임 코드를 바꿀 필요가 없다.
- 경로나 용도를 바꿀 때는 이 문서와 `Week13Setting1ASetup`의 경로를 함께 갱신한 뒤 Setup·검증을 다시 실행한다.
