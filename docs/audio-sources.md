# audio-sources.md

Trickal Fan Game에 실제로 적용하기로 확정한 음원의 출처와 라이선스 정리.

| 용도 | 제목 | 제작자 | 라이선스 | 크레딧 표기 | 상업적 이용 | 출처 링크 |
|---|---|---|---|---|---|---|
| 홈 화면 BGM | Flowerbed Fields [Loop] | Zane Little Music | CC0 (Public Domain) | 불필요 (선택) | 가능 | https://opengameart.org/content/flowerbed-fields-loop |
| 전투 BGM (교체됨) | 探検 Exploration | nojisuma | Pixabay Content License | 불필요 | 가능 (제작자가 게임 BGM 상업적 이용 명시적으로 허용) | https://pixabay.com/music/instrumental-探検-exploration-565678/ |
| UI 클릭 효과음 | Plop! | Breviceps | CC0 (Public Domain) | 불필요 (선택) | 가능 | https://freesound.org/people/Breviceps/sounds/447910/ |
| 전투 효과음 (일부) | 자체 합성 (Sound-0) | 프로젝트 자체 제작 | 자체 보유 | 불필요 | 가능 | `tools/sfx-synth` |

> 전투 BGM은 Sound-0(2026-10-20)에서 `openmindaudio - Cartoon Quirky Christmas Gift Unwrapping`(Pixabay #620480)으로
> 교체됐다. 아래 "외부 음원 라이선스" 표와 게임 내 크레딧 화면은 교체 후 상태를 따른다. 探検 Exploration은 현재
> 게임에 들어 있지 않으므로 크레딧 화면에도 없다.

## 외부 음원 라이선스 (2026-10-20, 사용자 확인)

외부 음원 16개를 적용했다. 라이선스는 사용자가 항목별로 확인했다(2026-10-20).

| 파일 | 출처 | 제작자 | 라이선스 | 크레딧 |
|---|---|---|---|---|
| `541478__eminyildirim__magic-fire-spell-impact-punch.wav` | Freesound #541478 | EminYILDIRIM | **CC BY 4.0** | **필수** |
| `396500__alonsotm__firespell3.wav` | Freesound #396500 | alonsotm | **CC BY 4.0** | **필수** |
| `632336__igroglaz__magic-spell-06.wav` | Freesound #632336 | igroglaz | CC0 | 불필요 |
| `659762__lilmati__magic-spell-05-remastered.wav` | Freesound #659762 | lilmati | CC0 | 불필요 |
| `249817__spookymodem__magic-missiles.wav` | Freesound #249817 | spookymodem | CC0 | 불필요 |
| `563542__colorscrimsontears__teleport-rpg.wav` | Freesound #563542 | colorscrimsontears | CC0 | 불필요 |
| `563536__colorscrimsontears__fire-3-rpg.wav` | Freesound #563536 | colorscrimsontears | CC0 | 불필요 |
| `sfx_exp_short_soft1.wav` | [OpenGameArt 512 Sound Effects (8-bit style)](https://opengameart.org/content/512-sound-effects-8-bit-style) | 해당 팩 제작자 | CC0 | 불필요 |
| `sfx_exp_short_hard9.wav` | 같은 팩 | 해당 팩 제작자 | CC0 | 불필요 |
| `daviddumaisaudio-magical-spell-cast-190272.mp3` | Pixabay #190272 | daviddumaisaudio | Pixabay Content License | 불필요 |
| `dragon-studio-distant-bang-472364.mp3` | Pixabay #472364 | dragon-studio | Pixabay Content License | 불필요 |
| `dragon-studio-boom-copyright-free-487662.mp3` | Pixabay #487662 | dragon-studio | Pixabay Content License | 불필요 |
| `yodguard-healing-magic-2-378663.mp3` | Pixabay #378663 | yodguard | Pixabay Content License | 불필요 |
| `yodguard-healing-magic-1-378665.mp3` | Pixabay #378665 | yodguard | Pixabay Content License | 불필요 |
| `yodguard-locked-door-4-540180.mp3` | Pixabay #540180 | yodguard | Pixabay Content License | 불필요 |
| `universfield-wine-cork-pop-352295.mp3` | Pixabay #352295 | universfield | Pixabay Content License | 불필요 |
| `openmindaudio-cartoon-quirky-christmas-gift-unwrapping-620480.mp3` | Pixabay #620480 | openmindaudio | Pixabay Content License | 불필요 |
| `openmindaudio-thriller-cinematic-background-high-tension-score-469144.mp3` | Pixabay #469144 | openmindaudio | Pixabay Content License | 불필요 |

### 크레딧 표기 의무 — 배포 전 필수

CC BY 4.0 음원이 들어오면서 크레딧 표기가 **권장에서 의무로 바뀌었다.** 기존 CC0·Pixabay 음원만 쓸 때는
크레딧이 선택이었지만, 아래 두 줄은 게임에 표시하지 않으면 라이선스 위반이다.

표기 의무가 있는 음원은 **2개**다(위 표에서 `필수`로 표시된 두 줄). 스펠 쪽에 쓰려던 세 번째 CC BY 음원은
CC0 음원으로 교체되어 의무가 없어졌다.

**어디에 적는가**: 게임 내 크레딧 화면이면 충분하다. CC BY 4.0은 "매체에 맞는 합리적인 방식"을 요구하므로,
소리가 나는 순간에 띄울 필요는 없고 크레딧 화면 하나에 모아두면 된다. 다만 플레이어가 찾아갈 수 있어야 하므로
타이틀 화면에서 들어갈 수 있는 경로를 둔다. 배포 페이지가 따로 있으면 거기에도 같이 적는 편이 안전하다.

**무엇을 적는가**: 제목, 제작자, 원본 링크, 라이선스 이름과 링크, 그리고 **수정했다는 사실**이다. 마지막 항목을
빼먹기 쉽다. 이 프로젝트는 두 음원 모두 리샘플링·무음 잘라내기·레벨 조정을 했고 기본 공격은 두 음원을 겹쳤으므로,
둘 다 수정에 해당한다. Freesound가 제공하는 기본 문구에는 수정 고지가 없어서 그대로 쓰면 불완전하다.

Freesound가 제공하는 문구에 수정 고지를 덧붙인 형태다. 두 줄 모두 확정됐다(2026-10-20 사용자 확인).
둘 다 기본 공격 발사음에 쓰였다.

```
Magic Fire Spell Impact & Punch by EminYILDIRIM -- https://freesound.org/s/541478/ -- License: Attribution 4.0 (modified)
FireSpell3.wav by alonsotm -- https://freesound.org/s/396500/ -- License: Attribution 4.0 (modified)
Pop-Up by IENBA -- https://freesound.org/s/545477/ -- License: Attribution 4.0
```

- 2026-11-19 Sound-2: 뒤로가기·취소음(`SFX_UI_Cancel.wav`)으로 Pop-Up(IENBA, CC BY 4.0)이 들어와 표기 의무 음원이
  **3개**가 됐다. 원본 그대로 쓰므로 수정 고지는 붙이지 않는다. `CreditsScreenVerification`이 세 줄을 모두 검사한다.

- 수정 내용을 자세히 적을 필요는 없다. "modified" 또는 "수정됨" 표시로 충족된다.
- 제작자가 이 게임을 후원하거나 보증한다는 뜻으로 읽힐 표현은 쓰지 않는다(CC BY 4.0이 금지한다).
- 배포하는 동안 계속 표기해야 한다. 음원을 빼면 해당 줄도 지운다.
- 해당 음원이 쓰인 곳: 둘 다 기본 공격 발사(두 음원을 겹쳤다). 스펠 쪽은 CC0 음원으로 교체되어 의무가 없다.
- 크레딧을 넣고 싶지 않다면 그 두 음원을 CC0나 Pixabay 음원으로 교체해야 한다. 교체는
  `tools/sfx-synth/import_sources.py`의 `MANIFEST`를 고치고 다시 실행하면 된다.
- 원본 파일은 `MANIFEST`가 상대 경로로 기록하고 있어 어떤 원본이 어떤 역할에 쓰였는지 추적할 수 있다.
  원본 백업을 저장소 밖에 보관하는 것을 권장한다.
- **크레딧 화면은 구현되어 있다(Credits-0, 2026-10-20).** 타이틀 화면의 `크레딧` 버튼과 홈 메뉴의 `크레딧`
  버튼에서 들어간다. 표시 내용은 `game/Assets/Resources/Credits.json`이 가지며 화면 코드에 들어 있지 않다.
  `TrickalFanGame.Editor.CreditsScreenVerification`이 위 두 줄의 제작자·원본 링크·라이선스 이름·라이선스 링크·
  `modified` 고지가 실제 화면 줄에 모두 들어 있는지 검사하고, 빠지면 실패한다. 실행 방법은
  [05번 "크레딧 화면 Credits-0"](./05-development-setup.md)에 있다.
- 음원을 바꾸면 `Credits.json`의 해당 항목도 같이 고친다. CC BY 음원을 더하거나 빼면
  `CreditsScreenVerification.RequiredAttribution` 표도 같이 고친다.

## 전투 효과음 (Sound-0, 2026-10-19)

전투 효과음은 외부 음원을 쓰지 않고 `tools/sfx-synth`에서 파라미터로 합성한다. 저작권·출처 관리 대상이 아니며,
수정이 필요하면 음원을 다시 구하지 않고 프리셋 수치를 고쳐 다시 렌더한다.

| 울리는 순간 | 큐 | 안정 경로 | 음원 |
|---|---|---|---|
| 기본 공격 발사 (1회/연사) | `BasicAttack` | `SFX/SFX_Attack_Basic.wav` | 외부: firespell3 + magic-fire-spell-impact-punch (겹침) |
| 기본 공격이 적에 명중 | `HitEnemy` | `SFX/SFX_Hit_Enemy.wav` | 외부: sfx_exp_short_soft1 |
| 기본 공격 치명타 | `HitCritical` | `SFX/SFX_Hit_Critical.wav` | 외부: sfx_exp_short_hard9 |
| 적이 틱 데미지를 받음 | `EnemyTickDamage` | `SFX/SFX_Enemy_Tick.wav` | 자체 합성: 퍅 (E) |
| 플레이어 피격 | `PlayerHurt` | `SFX/SFX_Player_Hurt.wav` | 자체 합성: 뭉 (B) |
| 저학년 스킬 시전 (Space) | `SkillLowerCast` | `SFX/SFX_Skill_Lower_Cast.wav` | 외부: magical-spell-cast + magic-missiles (겹침) |
| 저학년 유도탄 발사 (4회) | `SkillLowerShot` | `SFX/SFX_Skill_Lower_Shot.wav` | 자체 합성: 퓨 (A) |
| 저학년 유도탄 폭발 | `SkillLowerBurst` | `SFX/SFX_Skill_Lower_Burst.wav` | 자체 합성: 부드럽게 (A) |
| 고학년 돌진 시작 (Q) | `SkillUpperDash` | `SFX/SFX_Skill_Upper_Dash.wav` | 자체 합성: 바람 (A, 출발음만) |
| 고학년 돌진 중 왼발 | `SkillUpperStepLeft` | `SFX/SFX_Skill_Upper_Step_Left.wav` | 자체 합성: 쿵쿵 왼발 |
| 고학년 돌진 중 오른발 | `SkillUpperStepRight` | `SFX/SFX_Skill_Upper_Step_Right.wav` | 자체 합성: 쿵쿵 오른발 |
| 고학년 돌진이 적과 충돌 | `SkillUpperImpact` | `SFX/SFX_Skill_Upper_Impact.wav` | 자체 합성: 슬램 (B) |
| 다음 층 포탈 진입 | `BossPortal` | `SFX/SFX_Boss_Portal.wav` | 외부: teleport-rpg |
| 폭탄·폭탄 상자 폭발 | `BombExplosion` | `SFX/SFX_Bomb_Explosion.wav` | 외부: distant-bang + fire-3-rpg (겹침) |
| 폭발 머핀 폭발 | `MuffinExplosion` | `SFX/SFX_Muffin_Explosion.wav` | 외부: dragon-studio-boom |
| 스펠 사용 | `SpellCast` | `SFX/SFX_Spell_Cast.wav` | 외부: igroglaz magic-spell-06 (CC0) |
| 짱셈 스펠 사용 | `JjangsemSpellCast` | `SFX/SFX_Jjangsem_Spell_Cast.wav` | 외부: lilmati magic-spell-05-remastered (CC0) |
| SP 획득 | `SpGain` | `SFX/SFX_SP_Gain.wav` | 외부: yodguard-healing-magic-2 |
| 체력 회복 | `Heal` | `SFX/SFX_Heal.wav` | 외부: yodguard-healing-magic-1 |
| 잠긴 문 열림 | `DoorOpen` | `SFX/SFX_Door_Open.wav` | 외부: yodguard-locked-door-4 |
| 아이템 바닥 드롭 | `ItemDrop` | `SFX/SFX_Item_Drop.wav` | 외부: wine-cork-pop |
| 전투 BGM | — (`GameAudioController`) | `BGM/BGM_Combat.ogg` | 외부: cartoon-quirky-christmas-gift-unwrapping |
| 보스방 BGM (VS 효과음 뒤 시작) | — (`GameAudioController.BossBgm`) | `BGM/BGM_Boss.mp3` | 외부: thriller-cinematic-background-high-tension-score |

### 외부 음원 반입 (2026-10-20)

외부 음원은 받은 파일을 그대로 넣지 않고 `tools/sfx-synth/import_sources.py`를 통과시킨다. 그 이유는 다음과 같다.

- **리샘플링**: 받은 파일이 44.1k·48k·96k로 섞여 있고, 두 파일을 겹치려면 샘플레이트가 같아야 한다.
- **무음 잘라내기**: 발사음 원본이 0.22초 늦게 시작해 그대로 쓰면 입력이 밀린 것처럼 들렸고, SP·회복 음원은
  소리가 끝난 뒤 6.4초의 잔향 바닥이 붙어 있었다(8.04초 → 1.58초). 잘라내기 기준은 피크 대비 -40dB이며,
  그 두 파일의 잔향 바닥이 정확히 -50dB에 걸려 있어 더 낮게 두면 전부 남는다.
- **레벨 정리**: mp3 디코딩 결과 피크가 1.0을 넘는 파일이 네 개 있어 그대로 쓰면 클리핑한다. 모두 -1dBFS로 맞춘다.
- **겹치기**: 한 순간을 두 음원으로 만드는 경우(기본 공격, 저학년 시전, 폭탄)를 오프라인에서 하나로 합친다.
- BGM은 루프라서 앞뒤를 자르지 않는다. 자르면 루프 이음새에 틱이 생긴다. ogg로는 블록 단위로 쓴다
  (libsndfile이 긴 Vorbis를 한 번에 쓰면 스택 오버플로로 죽는다).

- 큐 이름은 "울리는 순간"을 뜻하고 채택 후보와 분리되어 있다. 소리만 바꿀 때는 같은 경로에 WAV를 덮어쓰고
  `Trickal Fan Game/Audio/Setup Combat SFX`를 다시 실행한다. 코드와 Scene은 건드리지 않는다.
- 음량과 최소 재생 간격은 `CombatSfxSetup.Cues` 표가 가진다. 저학년 발사·폭발의 간격은 실제 발사 간격(0.08초)보다
  짧게 유지해야 하며, 검증기가 이 조건을 확인한다.
- 채택 후보를 바꿀 때는 `tools/sfx-synth/render_sfx.py`의 `CHOSEN` 표를 고치고 `--install`로 다시 설치한다.

### 명중은 폭발, 틱 데미지는 따로 (2026-10-19 수정)

에르핀의 기본 공격은 작은 폭발로 터지므로 명중음도 두드리는 소리가 아니라 폭발이다. 두드리는 소리는
틱 데미지 쪽으로 옮겼다.

- `HitEnemy`는 `PlayerCombatEvents.BasicAttackHit`에서 울린다. 매 명중마다 나오므로 폭발로 들리면서도
  0.35초 간격에서 뭉개지지 않을 만큼 짧아야 하고, `SkillLowerBurst`보다 작아야 둘이 헷갈리지 않는다.
- `EnemyTickDamage`는 `Health.AnyDamageResolved`에서 `DeliveryType == Periodic`인 플레이어 피해만 받는다.
  독·화상·피해 오라·스펠 맥박이 모두 여기로 들어온다. 소스 타입을 하나씩 나열하지 않는 이유는 `Periodic`이
  "틱"의 정의 그 자체이기 때문이다.
- 틱은 한 프레임에 여러 마리가 동시에 들어오므로 최소 간격(0.12초)이 가장 길고, 음량은 조준해서 때린 명중음보다
  낮다. 검증기가 두 조건을 모두 확인한다.
- 플레이어가 받는 틱(장판, 독가스)은 `PlayerHurt`로 울린다. `EnemyTickDamage`는 적 전용이다.

### 돌진 발소리는 반복 재생이다 (2026-10-19 수정, 2026-11-21 대체)

> 2026-11-21 사운드 교체에서 이 구조는 달리기 음원 루프로 바뀌었다. 아래는 당시 기록이다. 현재 구조는
> 맨 아래 "2026-11-21 사운드 교체" 항목을 본다.

고학년 돌진은 적에 닿을 때까지 이어지고 최대 10초다. 고정 길이 음원 하나로는 덮을 수 없어서 구조를 바꿨다.

- `SkillUpperDash`는 출발 순간의 바람 소리만 담는다. 발소리가 들어 있던 예전 음원을 그대로 쓰면 반복 발소리와
  겹치므로 쓰지 않는다.
- 달리는 효과는 `SkillUpperStepLeft`·`SkillUpperStepRight`를 `CombatSfxSetup.DashStepInterval`(0.17초) 간격으로
  번갈아 재생해서 만든다. 돌진이 끝나면 멈춘다. 왼발·오른발을 따로 둔 이유는 연속 두 걸음이 같은 음원이면
  기계적으로 들리기 때문이다.
- 간격은 Setup이 `PlayerCombatAudio.ConfigureDashSteps`로 넣는다. 한 발의 최소 재생 간격은 같은 발이 다시
  닿는 0.34초보다 짧아야 하며, 검증기가 6초 돌진을 모사해 걸음 수와 좌우 교대를 확인한다.

## 비고

- **크레딧 화면은 이제 선택이 아니다.** 전투 효과음에 CC BY 4.0 음원 2개가 들어왔다. 화면은 Credits-0에서 구현했다. 위 "크레딧 표기 의무" 절을 본다.
- 아래 BGM·UI 클릭 세 음원은 크레딧 표기가 필수는 아니지만, CC0 음원(Flowerbed Fields, Plop!)은 제작자가 링크나 후원을 반가워한다고 밝혔으므로 게임 크레딧 페이지에 가볍게 언급하는 걸 권장.
- Pixabay 음원(探検 Exploration)은 Pixabay Content License 적용이라 Pixabay 자체가 서비스 종료되거나 라이선스 정책이 바뀔 가능성에 대비해, 다운로드한 원본 파일을 프로젝트 저장소에 백업해두는 걸 권장(Pixabay는 사후에 트랙이 내려가도 이미 받은 파일의 라이선스는 유지되는 방식).
- 파일 포맷: 원본은 Flowerbed Fields(확인 필요, 보통 wav/ogg), 探検 Exploration(mp3) — 게임 리소스 규칙(BGM=ogg, SFX=wav)에 맞춰 변환 후 사용.
- 확정 목록이 늘어나면 이 표에 행만 추가하면 됨 (프로젝트 문서 `bgm-sfx-후보-리스트.md`의 "후보" 성격과 구분해서, 이 파일은 "실제 적용 확정본" 용도로 유지).

## Unity 적용 경로

| 용도 | 안정 경로 |
|---|---|
| 홈 화면 BGM | `Assets/Audio/BGM/BGM_Home.ogg` |
| 전투 BGM | `Assets/Audio/BGM/BGM_Combat.ogg` |
| 보스방 BGM | `Assets/Audio/BGM/BGM_Boss.mp3` |
| UI 클릭 효과음 | `Assets/Audio/SFX/SFX_UI_Click.wav` |

- 음원만 교체할 때는 위 파일명을 유지하면 Scene 참조와 런타임 코드를 바꿀 필요가 없다.
- 경로나 용도를 바꿀 때는 이 문서와 `Week13Setting1ASetup`의 경로를 함께 갱신한 뒤 Setup·검증을 다시 실행한다.
- 전투 효과음 8종의 경로는 위 "전투 효과음" 절에 있고, 담당 Setup은 `CombatSfxSetup`이다. BGM·UI 클릭과 전투 효과음은
  서로 다른 필드를 쓰므로 두 Setup은 서로를 덮어쓰지 않는다.
- 2026-11-19 Sound-1·Sound-2: 크레용사 칼, 장애물·적 공격·처치, 권능·스펠, 보스 VS, 새마음금고(기둥·점프·메테오), 부스러기 장애물
  착지, 포탈 등장, 레벨업, 순간이동 스펠, 뒤로가기·취소, 적 등장 효과음이 추가됐다. 기본 타격·치명타·아이템 드롭은 새 음원으로
  교체됐고(`SFX_Hit_Punch.mp3`, `SFX_Hit_Critical_Slap.mp3`, `SFX_Item_Drop_Cork.mp3`), UI 클릭은 같은 경로
  `SFX_UI_Click.wav`에 POP SFX Neutral(ethernel0, CC0)을 덮어썼다. 적 등장음 `SFX_Enemy_Appear.wav`는 OpenGameArt Pop Sounds의 pop8(EZduzziteh, CC0)이다. 큐와 파일 경로의 기준은 `CombatSfxSetup.Cues`,
  출처는 `Resources/Credits.json`이다.
- 2026-11-20 Sound-3: 상점 포커스 이동음 `SFX_UI_Focus_Move.wav`(큐 `UiFocusMove`)는 같은 Pop Sounds 팩의 pop2(EZduzziteh,
  CC0, 무수정)다. 상점은 오퍼 칸·투자 행·투자/닫기 버튼·구매 팝업 선택이, 보상 선택 창은 카드·확인·취소 사이 선택이 실제로
  바뀔 때만 울린다(창이 열리며 첫 카드를 잡을 때와 카드 클릭으로 확인 버튼에 넘어갈 때는 울리지 않는다). 자폭형과 부스러기 쫄따구의 사망
  폭발(`EnemyDeathBlast`)은 새 음원 없이 폭탄 큐 `BombExplosion`을 재사용한다.
- 2026-11-20 Sound-3: 아이템 줍기음 `SFX_Item_Pickup.mp3`(큐 `ItemPickup`)는 Pixabay #145251 Coin Collect Retro 8-bit
  Sound Effect(lesiakower, Pixabay Content License, 무수정)다. 아티팩트·스펠·권능·열쇠·폭탄·엘리프를 주울 때 울린다. 골드는
  별도 동전 소리를 넣을 예정이라 제외했고, SP·하트는 기존 `SpGain`·`Heal`을 그대로 쓴다.
- 2026-11-20 Sound-3 추가분(발소리 외 무수정): 골드 줍기 `SFX_Gold_Pickup.wav`(Mixkit #1939 Coins handling), 문으로 방 이동
  `SFX_Room_Transition.wav`(Mixkit #192 Cabinet door closing, 구덩이·순간이동 스펠은 제외), 상자 열기 `SFX_Chest_Open.mp3`
  (Freesound 771164 steprock, CC0), 플레이어 발소리 `SFX_Player_Step.wav`(Freesound 535222 spycrah, **CC BY 4.0 — 크레딧
  표기 필수**, 원본이 다른 효과음보다 약 30dB 작아 피크 -3dBFS로 +29.8dB 증폭(그 외 무가공), 크레딧 수정 표기, 걷기 프레임
  1·3에서 0.25초 간격), 새마음금고 먹기 `SFX_Vault_Eat.mp3`(Freesound 244370 philip1789, CC0, 회복
  패턴 동안 voice로 재생하고 패턴이 끝나면 끊음), 젤리 던지기 `SFX_Vault_Slime_Throw.wav`(Freesound 346373 denao270, CC0,
  젤리 덩어리가 실제로 날아갈 때마다 1회, 0.08초 간격), 이동 `SFX_Vault_Move.wav`(Freesound 353250 Zuzek06, CC0, 걷기
  애니메이션의 깡충 이륙 지점(보폭 18%)마다 1회).
  Mixkit 음원은 Mixkit Sound Effects Free License로 표기 의무가 없다.
- 2026-11-20 사운드 믹서(`tools/sound-mixer`)로 33개 cue 볼륨을 조정했다. 값은 `CombatSfxSetup.Cues`가 기준이다.
- 2026-11-21 사운드 교체: 사운드 믹서의 "새 후보 비교"로 들어 보고 사용자가 고른 값(볼륨, 내려치기 겹 타이밍)을 반영했다.
  파일은 모두 무수정이고, 출처는 `Resources/Credits.json`에 있다.
  - 교체: 고학년 충돌 `SFX_Skill_Upper_Impact_Thud.mp3`(Pixabay #515257), 저학년 발사 `SFX_Skill_Lower_Shot_Heavy.wav`
    (OpenGameArt 150 Combat Sound Effects의 HeavyShot_01, CC0), 저학년 폭발 `SFX_Skill_Lower_Burst_8Bit.mp3`(Pixabay #340462),
    적 틱 `SFX_Enemy_Tick_Hitmarker.mp3`(Freesound 322640 aruscio28, CC0), 스펠·짱셈 스펠·교주의 권능은 각자 다른 음원
    `SFX_Spell_Impact.wav`·`SFX_Jjangsem_Spell_Impact.wav`·`SFX_Power_Impact.wav`(같은 팩의 Healing Spell Impact 15·14·4,
    CC0), 새마음금고 황금 기둥 `SFX_Vault_Pillar_Earth.wav`(같은 팩 Earth Spell Impact 2, CC0).
  - 고학년 돌진: 왼발·오른발 0.17초 반복을 없애고 `SkillUpperRun`(`SFX_Skill_Upper_Run.mp3`, Pixabay #359909)을 돌진 동안
    루프 voice로 재생하다(원본 발소리가 돌진 속도보다 느려 `PlayerCombatAudio.DashRunPitch` 1.55배속, 믹서도 같은 배속으로
    재생하고 배속 칸에서 조절) 돌진이 끝나면(충돌·시간 초과·취소 모두) 0.1초 페이드로 끊는다. `SkillUpperStepLeft`·`Right`는
    씬이 큐를 번호로 저장하므로 enum에 남기고 `CombatSfxSetup.RetiredCues`로 표에서 뺐다.
  - 새 큐: 상점 구매 `ShopPurchase`(Pixabay #567194, 오퍼 구매와 투자가 성공할 때), 시스트의 가짜 날개 비행 날갯짓
    `PlayerWingFlap`(Pixabay #80054, 날아서 움직이는 동안 걷기 주기마다 1회 = 0.5초), 크레용사 쫄병 소환 `BossCrayonSummon`
    (効果音ラボ 剣を抜く, 이 보스의 소환에서는 일반 적 등장음 대신 울림), 검 차징 `BossCrayonCharge`(Pixabay #102051, 맵 베기
    예고부터 voice로 재생하고 내려칠 때 끊음), 변신 `BossCrayonTransform`(効果音ラボ オーラ1, 페이즈 전환에서 3초 재생·마지막 1.5초
    페이드).
  - 크레용사 기모아 내려치기는 3겹이다: `BossSwordSlam` ① 휘두름(Pixabay #185432) 즉시, `BossSwordSlamMagic` ② 마법 충격
    +0.27초, `BossSwordSlamClash` ③ 칼 부딪힘(効果音ラボ 剣で打ち合う3) +0.39초. 지연은
    `CrayonHeroBossPatternRuntime.SlamMagicDelay`·`SlamClashDelay`, 재생은 `CombatSfx.PlayAfter`가 맡는다.
  - **② 마법 충격(Freesound 749799 RescopicSound)은 CC BY-NC 4.0이다.** 크레딧 표기가 필수이고 상업적 이용이 금지된다.
    `Credits.json` 표기 의무 절에 넣었고 `CreditsScreenVerification.RequiredAttribution`이 검사한다. 게임을 유료로 배포하면
    이 음원을 교체해야 한다.
  - 効果音ラボ 음원은 상업적 이용 가능·표기 불필요지만 음원 파일 자체의 재배포는 금지된다(게임에 포함해 재생하는 것은 허용).
  - 더 이상 쓰지 않는 예전 파일(`SFX_Skill_Upper_Impact.wav`, `SFX_Skill_Upper_Step_*.wav`, `SFX_Skill_Lower_Shot.wav`,
    `SFX_Skill_Lower_Burst.wav`, `SFX_Enemy_Tick.wav`, `SFX_Power_Spell_Impact.mp3`, `SFX_Boss_Sword_Slam.mp3`,
    `SFX_Vault_Pillar_Emerge.mp3`)은 이전 교체 때처럼 지우지 않고 남겨 두었다. 크레딧에서는 뺐다.
