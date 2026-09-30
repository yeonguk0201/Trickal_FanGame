# 전투 코어 씬 구성

`SampleScene`에 아래 임시 오브젝트를 만든 뒤 Play Mode에서 확인한다.

1. **Player**: `SpriteRenderer`, `Rigidbody2D`(Dynamic, Gravity Scale 0, Freeze Rotation Z), `CircleCollider2D`, `Health`, `PlayerMovement`, `PlayerAttack`를 붙인다.
2. **TestEnemy**: `SpriteRenderer`, `Rigidbody2D`(Kinematic, Gravity Scale 0), `CircleCollider2D`, `Health`, `TestEnemy`, `EnemyChase`, `ContactDamage`를 붙인다. `Health.maxHealth`는 3 정도로 설정한다. `EnemyChase`는 Player를 자동으로 찾아 6 유닛 안에서 추적하며, 0.8 유닛 거리에서 멈춘다.
3. Player와 TestEnemy의 Collider2D가 충돌하도록 Physics 2D 레이어 충돌 설정을 확인한다.

조작은 WASD 이동, 방향키 공격이다. 방향키를 누르고 있으면 `attackCooldown` 간격으로 연속 공격한다. 이동과 공격 방향은 독립적이므로, 적에게서 멀어지며 반대 방향으로 공격할 수 있다. 적과 닿으면 플레이어가 1초마다 피해를 받으며, Console에서 적의 HP 변화를 확인할 수 있다. 플레이어 Health의 `maxHealth`를 낮춰 사망 후 이동·공격이 차단되는지도 확인한다. Scene 뷰에서 TestEnemy를 선택하면 노란 원(감지 거리)과 빨간 원(정지 거리)으로 추적 범위를 확인할 수 있다.

이 구성은 Input System 패키지를 사용한다. Project Settings > Player > Active Input Handling은 `Input System Package (New)` 또는 `Both`여야 한다.

## 기본 원거리 투사체

근접형 캐릭터는 `PlayerAttack`을, 기본 원거리 캐릭터는 `PlayerProjectileAttack`을 사용한다. 같은 Player에는 둘 중 하나만 활성화한다.

1. Hierarchy에서 `2D Object > Sprites > Circle`을 만들고 이름을 `PlayerProjectile`로 변경한다.
2. `Rigidbody2D`(Dynamic, Gravity Scale 0, Collision Detection: Continuous), `CircleCollider2D`, `Projectile`을 추가한다. Collider는 Trigger가 아니어도 된다.
3. Project 창의 `Assets` 폴더에 `Prefabs` 폴더를 만들고, Hierarchy의 `PlayerProjectile`을 끌어 넣어 Prefab으로 만든 뒤 Hierarchy의 원본은 삭제한다.
4. Player에서 `PlayerAttack`을 비활성화하고 `PlayerProjectileAttack`을 추가한다. 생성한 `PlayerProjectile` Prefab을 `Projectile Prefab` 슬롯에 끌어 놓는다.

방향키를 누르고 있는 동안 투사체가 발사된다. 초기 속도는 `발사 방향 × baseProjectileSpeed + 플레이어 현재 속도 × inheritedVelocityFactor`다. 기본값은 8과 0.25이며, 이동 방향으로 발사하면 조금 빨라지고 반대 방향 발사는 조금 느려진다.

## 방 진행 루프

`RoomController`가 방의 상태와 적 생명주기를 소유한다. 상태는 `Waiting → Combat → Cleared` 순서로 한 번만 진행하며, 이미 시작한 방에 재진입해도 적을 다시 만들지 않는다.

1. 빈 오브젝트에 `BoxCollider2D`와 `RoomController`를 추가하고 Collider 크기를 방 내부에 맞춘다. Collider는 실행 시 자동으로 Trigger가 된다.
2. 출구 오브젝트에 `BoxCollider2D`와 `DoorController`를 추가하고 `ConfigurePortalBarrier(true)`로 초기화한 뒤 Room의 `Doors` 목록에 넣는다. 연결문 blocker는 열린 뒤에도 포탈형 물리 장벽으로 유지한다. 방 전환용 `RoomDoorway` Trigger는 별도 오브젝트로 통로 중앙에 둔다.
3. 적 프리팹과 빈 오브젝트로 만든 스폰 지점들을 Room의 `Enemy Prefab`, `Spawn Points`에 연결한다. 적 프리팹에는 반드시 `Health`가 있어야 한다.
4. 씬에 미리 둔 적을 사용할 때는 `Preplaced Enemies`에 넣는다. 이 적들은 입장 전에는 비활성화되고 전투 시작 시 등록·활성화된다.
5. 씬에 `RunProgress`를 하나 두고 각 Room에 같은 인스턴스를 연결한다. Room의 `Floor Number`, `Room Number`가 입장 시 현재 진행 위치로 기록된다.

플레이어가 방 Trigger에 처음 들어오면 문 잠금, 적 스폰, 사망 이벤트 등록이 순서대로 실행된다. 마지막 적의 `Health.Died`가 발생하면 방이 클리어되고 문이 열린다. 플레이어가 먼저 죽으면 진행 상태가 멈추며 문은 잠긴 상태로 유지된다.

첫 달 범위에서는 두 개의 Room을 고정된 순서로 배치하고 첫 Room의 열린 출구가 두 번째 Room의 Trigger로 이어지게 구성한다. 랜덤 생성이나 방 선택은 이후로 미룬다.

## 4주차 최소 Run 수직 슬라이스

Unity 메뉴에서 **Trickal Fan Game > Setup Week 4 Vertical Slice**를 한 번 실행한다. 3주차 맵의 끝에 보스방과 `RunSession`이 추가된다. 보스는 플레이어를 향해 1.2초마다 피할 수 있는 투사체를 발사하며, 처치하면 클리어 Run을, 플레이어가 적 또는 보스에게 죽으면 사망 Run을 생성한다.

`RunSession`은 시작·종료 시각, 실제 경과 초, 현재 층, 처치 수를 스냅샷으로 만들고 한 번만 전송한다. 화면 좌측 상단에는 저장 성공 또는 실패가 표시된다. API가 꺼져 있거나 요청에 실패해도 게임 진행은 멈추지 않는다. 이전의 `TestRunSender`는 파이프라인 점검용으로 남아 있지만 실제 플레이 결과에는 사용하지 않는다.

## 5주차 아이템 수직 슬라이스

Unity 메뉴에서 **Trickal Fan Game > Setup Week 5 Item Slice**를 한 번 실행한다. Player에 `PlayerInventory`가 추가되고 시작 방에 공격력, 최대 HP, 이동 속도 아이템과 공격력 중복 아이템이 배치된다. 메뉴는 `Assets/Items`에 DB seed와 같은 `item-01`~`item-03` ID를 가진 `ItemDefinition` 에셋도 생성한다.

Play Mode에서 아이템과 접촉하면 Console에 아이템 이름과 현재 중첩 수가 출력된다. 공격력 아이템은 새 투사체의 피해량, 최대 HP 아이템은 최대·현재 HP, 이동 속도 아이템은 실제 이동 속도에 즉시 반영된다. 기본 중첩은 가산이며 `maxStacks`가 0이면 무제한, 양수면 해당 수가 상한이다.

획득 기록은 중복을 포함해 1부터 순서대로 `PlayerInventory`에 남는다. Run 종료 시 `RunSession`이 실제 경과 시간을 UTC 시각으로 변환해 `itemId`, 획득 층, 순서, `acquiredAt`을 Backend에 전송한다.

### 보상방과 보스 드롭

Unity 메뉴에서 **Trickal Fan Game > Setup Week 5 Reward Drops**를 한 번 실행한다. 1층 두 번째 전투방 출구에 보상방 표식이 추가된다. 두 번째 전투방을 클리어한 뒤 표식에 진입하면 `item-01`~`item-03` 중 하나가 확정 드롭되며, 같은 표식에 다시 진입해도 보상은 한 번만 생성된다. 표식 안에서 마지막 적을 처치한 경우에도 클리어 이벤트를 받아 즉시 드롭된다.

현재 보스는 1층 보스로 취급하여 처치 시 같은 풀에서 아이템 하나를 확정 드롭한다. 이 단계에서는 보스 처치가 Run 클리어를 발생시키지 않는다. 이후 3개 층을 연결할 때 1~2층 보스에는 드롭을 유지하고, 3층 최종 보스만 `isFinalBoss`로 설정해 아이템 없이 Run을 클리어한다.

`RewardRoom`과 `BossItemDrop`은 공통 `ItemDropSource`를 사용한다. 드롭 풀에는 중복 획득 가능한 `ItemDefinition`을 넣으며, 한 보상 소스는 한 번만 드롭한다.

### 3개 층 이동 뼈대

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Setup Week 5 Three Floor Skeleton**을 한 번 실행한다. 기존 맵을 1층으로 사용하고 아래쪽에 고정 구조의 2층과 3층을 추가한다. 각 층은 일반 전투방 2개, 보상방 1개, 보스방 1개로 구성된다.

1~2층 보스를 처치하면 아이템이 하나 드롭되고 청록색 층 출구가 활성화된다. 출구에 들어가면 같은 Player 오브젝트가 다음 층 시작점으로 이동하므로 현재 HP, 인벤토리, 아이템 효과가 유지된다. 3층 최종 보스는 아이템을 드롭하지 않고 `RunSession`의 클리어 처리를 실행한다.

현재 방 배치는 층 이동과 상태 유지 검증을 위한 고정형이다. 랜덤 방 생성 단계에서는 전투방·보상방·보스방을 프리팹 또는 방 정의 데이터로 분리하고, 생성된 연결 그래프에 현재의 `RoomController`, `RewardRoom`, `FloorExit`를 배치한다.

### 캐릭터 선택과 Run ID

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Setup Week 5 Character Selection**을 한 번 실행한다. Play Mode가 시작되면 게임 시간이 멈추고 에르핀 선택 화면이 나타난다. 선택하기 전에는 플레이어 이동과 공격이 비활성화되며, 선택한 뒤부터 Run 시간 측정과 플레이가 시작된다.

캐릭터 정보는 `Assets/Characters/erpin.asset`의 `CharacterDefinition`으로 관리한다. 이 에셋의 `characterId`는 Backend seed의 `Character.id`와 동일한 `erpin`이다. 캐릭터를 추가할 때는 같은 형식의 에셋을 만들고 `CharacterSelectionUI` 목록에 연결하면 된다. 선택된 ID는 `RunSession`의 `CreateRunRequest.characterId`에 기록된다.

## 6주차 아이템 확장과 시너지

Unity 메뉴에서 **Trickal Fan Game > Setup Week 6 Items and Synergy**를 실행한다. 기존 3종에 `item-06` 다중 투사체, `item-11` 관통 투사체, `item-08` 처치 회복이 추가되고 모든 보상방과 1~2층 보스의 드롭 풀이 6종으로 갱신된다.

- 다중 투사체는 획득당 한 발을 추가하며 최대 2회 중첩된다.
- 관통 투사체는 획득당 한 명의 적을 추가로 맞히며 최대 2회 중첩된다.
- 처치 회복은 플레이어의 투사체로 적을 처치할 때마다 HP를 1 회복하며 최대 3회 중첩된다.
- `MULTI_SHOT` + `PIERCE`는 획득 순서와 무관하게 한 번만 활성화된다. 활성화 로그가 Console에 출력되고, 이후 생성되는 모든 다중 투사체에 관통 횟수가 적용된다.

스택 상한은 극단적인 조합에서 투사체 수와 회복량이 무한히 커지지 않도록 정한 밸런스 장치다. 층 이동은 같은 Player와 `PlayerInventory`를 유지하므로 아이템 스택과 시너지 활성 상태도 그대로 유지된다. Run 종료 기록에는 시너지를 별도 아이템으로 추가하지 않고, 두 원본 아이템 ID와 각각의 획득 순서를 기존 방식대로 남긴다.

## 7주차 전투 기반

Player의 최대 HP, 공격력, 이동 속도, 공격 속도와 투사체 관련 아이템 효과는 `PlayerStats`가 소유한다. `PlayerInventory`는 획득 효과를 `PlayerStats`에 누적하고, `PlayerMovement`와 `PlayerProjectileAttack`은 현재 값을 읽어 동작한다.

기본 투사체는 발사 시점의 공격력, 배율, 공격 주체와 `PlayerProjectile` 출처 종류를 `DamageContext`에 기록한다. `Projectile`은 관통하는 동안 같은 컨텍스트를 각 대상의 `Health`에 전달하며, `DamageCalculator`가 최종 피해를 계산한다. 기존 `Health.Damaged`와 `Health.Died` 이벤트는 유지되고, 출처가 필요한 소비자는 `Health.DamageApplied`에서 컨텍스트를 확인할 수 있다.

Unity 메뉴에서 **Trickal Fan Game > Verify PlayerStats and Item Effects**와 **Trickal Fan Game > Verify Damage Context and Projectile**을 차례로 실행한다. 두 번째 검증은 현재 공격력과 출처, 중복 타격 방지, 관통, 일반 적 사망, 보스 피격·사망 이벤트가 유지되는지 확인한다.

플레이어 피해로 `Health`가 사망 상태로 전환되면 Player의 `PlayerCombatEvents.EnemyKilled`가 대상과 마지막 `DamageContext`를 한 번 전달한다. 고정 수치와 현재 최대 HP 비례 처치 회복은 `PlayerStats`에서 합산되고, `PlayerProjectileAttack`이 활성화될 때 이 이벤트를 구독해 한 번 회복한 뒤 비활성화 시 구독을 해제한다. 이후 SP 드롭과 처치 시 연쇄 폭발은 기존 처치 판정을 복제하지 않고 같은 이벤트에 별도 구독자로 연결한다.

Unity 메뉴에서 **Trickal Fan Game > Verify Player Combat Events**를 실행하면 플레이어 원인이 아닌 사망 제외, 중복 보고 차단, 연속 처치, 처치 회복과 구독 해제를 확인한다.

## 7주차 고정 방 그래프

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Setup Week 7 Fixed Room Graph**를 실행한다. 기존 연속형 임시 맵 루트는 비활성화되고, 안정적인 `roomId`를 가진 세 개의 `RoomNode`가 양방향 고정 그래프로 구성된다. 각 노드는 방 콘텐츠, 카메라 중심, 기본 입구와 출입구 연결을 소유한다.

Play Mode에서는 현재 노드의 콘텐츠만 활성화된다. 연결문의 blocker 콜라이더는 열린 뒤에도 포탈형 물리 장벽으로 남아 플레이어와 기본·보스 투사체가 문 시각 오브젝트를 통과하거나 방 밖으로 나가지 않게 한다. 별도의 출입구 Trigger는 통로 중앙에 유지되며 플레이어가 장벽에 닿기 전에 열린 문 Trigger로 들어가면 목적지의 방 안쪽 안전 진입점으로 순간이동한다. 저학년 유도탄은 발사할 때 타깃이 배정된 탄만 벽과 문을 관통하고, 타깃 없이 발사된 탄은 벽과 문에서 폭발 없이 사라진다. 전환 직전에는 타깃 배정 여부와 관계없이 남아 있는 투사체를 모두 정리하여 다음 방 화면에 나타나지 않게 한다. 카메라는 목적지 중심으로 전환되고 이전 방 콘텐츠는 비활성화되며, 재방문해도 `RoomController.State`와 보상 획득 여부는 초기화되지 않는다.

일반 연결문은 초록, 보스방과 연결된 양쪽 문은 보라, 다음 층 출구는 청록으로 표시한다. 연결문은 잠기면 같은 계열의 어두운 색, 열리면 밝은 색이 되어 문 종류와 잠금 상태를 함께 구분한다. 층 출구도 보스방 클리어 전에는 어두운 청록, 해제 후에는 밝은 청록으로 바뀐다.

Unity 메뉴에서 **Trickal Fan Game > Verify Fixed Room Graph**를 실행한다. 검증기는 중복 방 ID, 누락·단방향 연결, 한 화면에 여러 방이 활성화되는 상태를 실패로 처리하고, 잠금·개방 상태의 포탈형 물리 장벽, 별도 전환 Trigger 유지, 기본·보스 및 무타깃 저학년 탄의 방 밖 이탈 차단, 타깃 배정 저학년 탄 통과, 전환 시 전체 투사체 정리, 플레이어와 카메라 전환, 양방향 재방문, 클리어·보상 상태 보존을 확인한다.

## 7주차 저학년 스킬

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Setup Phase C Lower Grade Skill**을 실행한다. Player에 `PlayerSP`, `PlayerSPDropper`, `PlayerSkill`이 추가되고 SP 픽업과 유도탄 프리팹이 생성·연결된다. Setup은 다시 실행해도 같은 프리팹과 Player 구성을 갱신한다.

플레이어 피해로 적을 처치하면 25% 확률로 청록색 SP 픽업이 처치 위치에 생성된다. 접촉하면 SP가 최대 3까지 증가하며, 이미 최대여도 픽업은 사라진다. SP가 있을 때 Space를 누르면 1을 소비해 마지막 이동 방향 중심 36° 부채꼴의 슬롯을 왼쪽부터 1~4로 보아 `1→3→2→4` 순서로 첫 탄을 즉시, 나머지 세 탄을 0.08초 간격으로 발사한다. 네 탄은 부채꼴 방향으로 먼저 나간 뒤 각 타깃으로 유도되며, 적이 여러 명이면 거리순 라운드로빈으로 배분된다. 연사가 끝나기 전의 추가 Space는 SP를 소비하지 않는다. 각 탄의 폭발은 발사 시점 현재 공격력의 100%를 `PlayerSkillExplosion` 피해로 전달하고, 네 폭발에 같은 적이 들어오면 각각 중첩된다. 대상은 `Enemy` 레이어에서만 찾는다. 발사 시 타깃이 배정된 탄은 벽과 문을 관통한다. 대상이 없으면 같은 부채꼴을 유지해 나가되 벽이나 문에 닿거나 수명이 끝날 때 폭발 없이 사라진다.

Unity 메뉴에서 **Trickal Fan Game > Verify Phase C Lower Grade Skill**을 실행한다. 검증기는 SP 상한·소비, 최대 SP 픽업 소멸, 처치 드롭, 36° 부채꼴과 `1→3→2→4` 발사 순서, 순차 네 발과 다수 적 배분, 400% 중첩 피해, 타깃 배정 탄의 벽 관통, 무타깃 탄의 벽 소멸, SP 부족 시 발사 거부를 확인한다.

## 7주차 고학년 스킬

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Setup Phase D High Grade Skill**을 실행한다. Player에 `PlayerActionState`와 `PlayerUltimate`가 추가되고 테스트 적 프리팹에 `KnockbackReceiver`가 연결된다. Setup은 다시 실행해도 같은 컴포넌트를 중복 생성하지 않는다.

Q를 누르면 현재 이동 속도의 2배로 최대 10초 동안 무적 돌진한다. 공격용 화살표 키와 분리된 WASD로 조향하며 입력이 없으면 마지막 이동 방향을 유지한다. 벽 접촉은 돌진을 끝내지 않고 첫 적 접촉이나 제한 시간 도달이 돌진을 종료한다. 첫 접촉은 현재 공격력의 200% 범위 피해와 0.2초 넉백을 주며, 보스 넉백은 일반 적의 25%다. 넉백이 끝난 뒤 일반 적은 0.4초, 보스는 0.15초 동안 경직된다. 적 행동 스크립트는 `KnockbackReceiver.IsActive`를 확인해야 하며, 현재 추격과 보스 발사는 넉백·경직 동안 모두 멈춘다.

적 충돌로 돌진이 끝나면 플레이어는 충돌 지점에 즉시 멈추고 0.4초 동안 무적 경직된다. 적 없이 제한 시간이 끝나면 무적 없이 0.25초 동안 돌진 방향으로 감속한다. 돌진과 두 회복 상태에서는 이동, 기본 공격, Space, Q, 방·층 전환이 모두 차단되고 회복이 끝난 뒤 함께 허용된다. 30초 쿨타임은 돌진 시작이 아니라 종료 시점부터 계산한다. `PlayerUltimate.DashEnded`는 `Impact` 또는 `Timeout` 종료 원인을 전달하므로 이후 넘어짐·제동 애니메이션을 분리해 연결할 수 있다.

Unity 메뉴에서 **Trickal Fan Game > Verify Phase D High Grade Skill**을 실행한다. 검증기는 상태 재진입, Q 쿨타임, 조향·무적, 행동 게이트, 200% 범위 피해, 일반/보스 넉백과 경직, 벽 처리, 충돌 후 무적 경직, 시간 만료 감속과 사망 정리를 확인한다.

## Phase E-1 추적형 몬스터

기존 `TestEnemy`를 추적형 몬스터의 기준 프리팹으로 사용한다. `EnemyChase`는 `Rigidbody2D`, `Health`, `KnockbackReceiver`를 필수 계약으로 가지며 감지 거리 안의 살아 있는 플레이어를 추적하고 접촉 거리에서 멈춘다. 돌진 넉백 속도는 추적이 덮어쓰지 않고, 넉백 이후 경직 중에는 정지하며 경직이 끝나면 추적을 재개한다. 방 콘텐츠가 비활성화될 때는 잔존 속도만 지우고 HP와 방 진행 상태는 초기화하지 않는다.

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Verify Phase E-1 Chaser Monster**를 실행한다. 이 통합 검증은 추적·정지·넉백·경직·사망·비활성화 동작과 기준 프리팹의 물리 구성을 확인한 뒤, Phase B~D 검증을 재사용해 돌진 범위 피해, 플레이어 처치/SP 드롭, 방 재방문 상태 보존, 벽과 초록 문 경계를 함께 회귀 검증한다.

## Phase E-2 원거리 몬스터

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Setup Phase E-2 Ranged Enemy**를 실행한다. Setup은 `TestEnemy`의 체력·사망·넉백·물리 구성을 재사용해 파란색 `RangedEnemy.prefab`을 생성하거나 갱신하고, 고정 방 그래프가 있으면 3번 방의 테스트 스폰 프리팹으로 연결한다. 다시 실행해도 같은 프리팹과 방 참조를 갱신한다.

원거리형은 HP 3·이동 속도 1.5이며 플레이어를 8 유닛 안에서 감지한다. 6 유닛 밖에서는 접근, 3 유닛 안에서는 후퇴하고, 3~6 유닛 구간에서는 멈춰 1.5초마다 피해 2의 투사체를 발사한다. 넉백과 이후 경직 중에는 이동과 발사를 중단하며 경직 종료 후 재개한다. 적 투사체는 플레이어에게 `EnemyProjectile` 피해와 `ENEMY` 사망 원인을 전달하고, 벽·포탈형 연결문 장벽·수명 만료·방 전환 시 제거된다.

Setup 후 Unity 메뉴에서 **Trickal Fan Game > Verify Phase E-2 Ranged Enemy**를 실행한다. 검증기는 프리팹 계약, 접근·후퇴·사거리 유지, 첫 발과 1.5초 쿨타임, 피해 출처, 넉백·경직 행동 중단, 공통 처치 이벤트, 벽·포탈형 연결문 장벽 충돌과 방 전환 투사체 정리를 확인한다.

## Phase E-3 돌진형 몬스터

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Setup Phase E-3 Charging Enemy**를 실행한다. Setup은 `TestEnemy`의 체력·사망·넉백·물리 구성을 재사용해 주황색 `ChargingEnemy.prefab`을 생성하거나 갱신하고, 고정 방 그래프가 있으면 2번 방의 테스트 스폰 프리팹으로 연결한다. 다시 실행해도 같은 프리팹과 방 참조를 갱신한다. 이 구성으로 1번 방 추적형, 2번 방 돌진형, 3번 방 원거리형을 연속 비교할 수 있다.

돌진형은 HP 7이며 플레이어를 7 유닛 안에서 감지하면 노란색으로 변해 0.65초 동안 정지한다. 예고 시작 시점에 고정한 방향으로 초당 8 유닛 속도로 최대 0.8초 돌진한다. 돌진 중 플레이어나 벽·포탈형 연결문 장벽과 충돌하면 즉시 멈춰 0.6초 회복하며, 한 번의 돌진은 플레이어에게 피해 3을 한 번만 준다. 넉백과 이후 경직은 진행 중인 예고·돌진을 취소하고, 사망이나 방 비활성화는 상태와 잔존 속도를 정리한다.

Setup 후 Unity 메뉴에서 **Trickal Fan Game > Verify Phase E-3 Charging Enemy**를 실행한다. 검증기는 프리팹 계약, 방향 고정, 예고·돌진·회복·쿨타임 전이, 단일 충돌 피해, 벽·포탈형 연결문 장벽 중단, 넉백·경직 취소, 공통 처치 이벤트, 사망과 방 비활성화 정리를 확인한다. Play Mode에서는 2번 방에서 노란색 예고 중 옆으로 피했을 때 돌진이 플레이어를 따라 꺾이지 않는지 함께 확인한다.

## Phase E-4 몬스터 전투 프로필

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Setup Phase E-4 Enemy Balance**를 두 번 실행한다. Setup은 기존 프리팹을 새로 만들지 않고 추적형을 HP 5·접촉 피해 1·이동 속도 2.5, 원거리형을 HP 3·투사체 피해 2·이동 속도 1.5, 돌진형을 HP 7·돌진 피해 3·돌진 속도 8로 갱신한다. E-2나 E-3 Setup을 나중에 다시 실행해도 원거리형과 돌진형은 같은 최종 수치를 유지한다.

Setup 후 Unity 메뉴에서 **Trickal Fan Game > Verify Phase E-4 Enemy Balance**를 실행한다. 검증기는 세 프리팹의 HP·피해·이동 속도가 정확한지, 각 축의 값이 모두 서로 다른지, 저체력 원거리형·균형 추적형·고체력 돌진형의 역할 순서가 유지되는지 확인한다. Play Mode에서는 1~3번 방을 돌며 처치에 필요한 기본 공격 횟수와 피격 피해가 실제로 구분되는지 확인한다.

## Phase E-5 3층 고정 그래프

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Setup Phase E-5 Three Floor Graph**를 두 번 실행한다. Setup은 기존 `Week7 Fixed Room Graph`를 Undo 가능한 방식으로 다시 구성하면서 그 아래의 `RunProgress`를 보존한다. 결과는 `Floor 1`~`Floor 3` 아래에 각각 3개 방, 총 9개 `RoomNode`이며 ID는 `floor-01-room-01`부터 `floor-03-room-03`까지다. 각 층은 내부에서만 `1 ↔ 2 ↔ 3`으로 연결되고 층간 연결은 E-7까지 만들지 않는다. 1층의 추적형·돌진형·원거리형 배치는 재구성 후에도 보존되며 2~3층의 구체적인 몬스터 조합과 보스는 E-6에서 적용한다.

Setup 후 Unity 메뉴에서 **Trickal Fan Game > Verify Phase E-5 Three Floor Graph**를 실행한다. 검증기는 단일 그래프 루트, 3층×3방 계층, 고유한 층·방 좌표와 안정 ID, `RoomNode`/`RoomController` 좌표 일치, 각 층의 양방향 내부 연결을 확인한다. 이어서 기존 고정 그래프 회귀 검증을 실행해 한 화면 한 방, 플레이어·카메라 전환, 투사체 정리, 재방문 상태 보존도 확인한다. E-7 이후 추가되는 층간 출구는 내부 연결 회귀 검사와 별도로 E-7 검증기가 확인한다.

## Phase E-6 층별 전투 구성과 보스

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Setup Phase E-6 Floor Encounters**를 두 번 실행한다. Setup은 기존 9개 방을 재생성하지 않고 스폰 지점별 프리팹 목록을 구성한다. 1층은 `[추적형]`, `[돌진형+원거리형]`, 보스, 2층은 `[원거리형]`, `[추적형+돌진형]`, 보스, 3층은 `[돌진형]`, `[추적형+원거리형]`, 최종 보스 순서다. 일반 방은 프리팹 수와 스폰 지점 수가 반드시 같아야 하며, 각 프리팹은 `Health`를 가져야 한다.

각 층 3번 방의 보스는 `TestBoss.prefab`을 재사용한다. 1~2층 보스는 성장 아이템을 한 번 드롭하고 3층 보스는 드롭 없이 `RunSession`에 연결되어 최종 Run 클리어를 담당한다. 기존 비활성 Week 4 루트 아래의 `RunSession`은 씬 루트로 옮겨 활성 상태를 유지하므로 E-5 그래프를 다시 구성해도 함께 삭제되지 않는다.

Setup 후 Unity 메뉴에서 **Trickal Fan Game > Verify Phase E-6 Floor Encounters**를 실행한다. 검증기는 E-5 그래프 회귀 검사 후 6개 일반 전투방의 프리팹 순서, 세 보스방의 단일 사전 배치 보스, 1~2층/최종 보스 드롭 역할, `RunSession`의 3층 보스 참조를 확인한다. 별도 런타임 픽스처로 서로 다른 두 프리팹의 혼합 생성·처치·방 클리어와 프리팹/스폰 수 불일치·`Health` 누락 실패도 검증한다.

## Phase E-7 층 클리어와 다음 층 이동

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Setup Phase E-7 Floor Transitions**를 두 번 실행한다. Setup은 기존 9개 방과 E-6 전투 구성을 재생성하지 않고, 1층 3번 보스방에서 2층 1번 방으로, 2층 3번 보스방에서 3층 1번 방으로 향하는 단방향 출구를 구성한다. 같은 Setup을 다시 실행하면 기존 층 출구를 교체하므로 중복 링크나 트리거가 생기지 않는다. 출구는 보스방이 클리어된 뒤 초록색 오른쪽 문 안쪽 트리거로 진입할 때만 작동한다.

층 출구는 일반 방과 같은 `RoomGraphController` 전환 경로를 사용한다. 따라서 동일한 Player 오브젝트의 HP·인벤토리·아이템 효과를 유지하면서 발사 완료 투사체를 정리하고, 다음 층 1번 방만 표시하며 플레이어·카메라·`RunProgress`를 함께 이동한다. 고학년 돌진과 회복 중에는 기존 `PlayerActionState.CanTransition` 규칙에 따라 차단된다. 3층 최종 보스방에는 출구를 만들지 않으며 기존 `RunSession` 클리어가 Run을 종료한다.

Setup 후 Unity 메뉴에서 **Trickal Fan Game > Verify Phase E-7 Floor Transitions**를 실행한다. 검증기는 E-5·E-6 회귀 검사, 정확히 두 개인 단방향 층 링크, 보스방 클리어 게이트, 다음 층 1번 방 목적지, 3층 출구 부재를 확인한다. 별도 런타임 픽스처로 클리어 전 진입 거부, 클리어 후 플레이어 이동·단일 방 표시·`RunProgress` 갱신, 떠난 출구의 재진입 거부도 검증한다. Play Mode에서는 1층과 2층 보스 처치 후 오른쪽 출구로 각각 다음 층에 진입하고, 3층 최종 보스 처치 시 층 이동 없이 Run이 종료되는지 확인한다.

## Phase F-5 seed 기반 격자형 층

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Setup Phase F-5 Seeded Grid Floors**를 두 번 실행한다. Setup은 6~8방·보스 최소 거리 3·재시도 32회 설정을 적용하고 `Assets/Rooms/Prefabs/room-grid-base.prefab`을 생성하거나 같은 GUID로 갱신한다. 프리팹은 좌·우·상·하 문 슬롯마다 전환 트리거, 반대편 진입점, 전투 차단문과 미연결 봉인 벽을 가진다. 기본 방은 공통 `RoomLayout`의 `16 × 9`, 방 간격 `20 × 13`, 카메라 크기 `5.25`를 사용한다. 문과 봉인 벽은 벽 중심선에 두고 전환 트리거는 폭 `0.3`, 벽 안쪽 `0.2` 위치로 좁혀 플레이어가 문에 가까이 접근했을 때 전환한다. 안전 진입점은 벽에서 `1.9` 안쪽의 기존 위치를 유지한다. 실행 중에는 Run seed에서 세 층의 Topology와 콘텐츠 seed를 독립 파생하고 현재 층의 프리팹만 조립한다.

Setup 후 **Trickal Fan Game > Verify Phase F-5 Seeded Grid Floors**를 실행한다. 검증기는 같은 seed 결정성, 여러 seed의 실제 변화, 6~8방 연결성과 필수 방, 안정 ID, 보스 최소 거리, 좌상·우하·좌우·상하 문 조합, 공통 방 크기·벽·문 중심선·전환 트리거·안전 진입점·Encounter·스폰·방 간격·카메라 범위, 미연결 봉인, 반복 Encounter 정책, 상태 복원, 제한 재시도 실패, 8~12방 설정 확장과 Setup 중복 방지를 확인한다.

Play Mode에서는 새 Run을 시작해 각 층의 보스방까지 이동한다. 1~2층 보스를 처치하고 방 중앙 위쪽 출구로 다음 층 시작방에 들어간 뒤 HP·아티팩트·seed가 유지되는지 확인한다. 보물방을 방문한 뒤 인접 방을 왕복해 아티팩트가 다시 지급되지 않는지, 클리어한 전투방의 적이 다시 생성되지 않는지 확인한다. 마지막으로 3층 보스 처치 시 다음 층 출구 없이 기존 Run 클리어가 실행되는지 확인한다.

## Phase G-7 보상 추첨과 전체 Run

`ItemDropSource`는 활성 상태이고 최대 스택에 도달하지 않은 아티팩트만 후보로 만든다. 후보마다 일반 60, 고급 25, 희귀 12, 전설 3의 등급 가중치를 적용하며 `RunProgress.RunSeed`, 안정적인 `floor-XX-room-YY:treasure|boss` 보상 ID, 현재 후보와 스택 상태를 함께 사용해 결정적으로 추첨한다. 따라서 같은 seed와 같은 보상 상태는 같은 결과를 만들고, 아이템 풀의 직렬화 순서가 달라져도 결과가 바뀌지 않는다.

모든 활성 아티팩트가 최대 스택이면 아이템 대신 최대 HP의 25%를 회복한다. 플레이어가 이미 사망했다면 대체 회복을 지급하거나 해당 보상을 소비하지 않는다. `RewardRoom`은 보상을 생성한 즉시 방별 `RoomRunState.HasClaimedArtifact`를 기록하며 재방문과 같은 seed 재조립에서 다시 추첨하지 않는다. 1~2층 보스도 방 ID 기반의 별도 보상 ID를 사용하고 3층 최종 보스는 기존처럼 드롭하지 않는다.

Unity 메뉴에서 **Trickal Fan Game > Verify Phase G-7 Rewards and Full Run**을 Edit Mode와 Play Mode에서 각각 실행한다. 검증기는 활성 10종, 등급 가중치 분포, 최대 스택·비활성 후보 제외, 같은 seed·상태 결정성, 재방문 무재추첨, 방별 수령 상태, 전부 최대 스택일 때의 생존/사망 대체 회복을 확인한다. 이어서 10종을 최대 스택까지 획득한 한 Player가 1→2→3층 좌표를 진행해도 스택, 복합 효과, 방어막, 피해 오라와 최대 SP가 유지되는지 확인한다. 두 모드 모두 Console에 `passed`가 출력되고 오류가 없어야 Phase G를 완료 처리한다.

## Phase H-5 Run 전송과 메타 진행

Backend와 `test-player` seed가 실행 중인 상태에서 Play Mode를 종료하고 Unity 메뉴의 **Trickal Fan Game > Setup Phase H-5 Meta Progression**을 두 번 실행한다. Setup은 기존 `RunSession`에 `PlayerProgressClient`를 하나만 연결하며 저학년·고학년 스킬 참조를 갱신한다. 새 Run은 캐릭터 선택 직후 `GET /api/users/:nickname`으로 진행을 조회한 뒤 시작한다. 조회 실패 시 캐릭터별 마지막 성공 캐시를 사용하고, 캐시도 없으면 Lv.1을 적용하면서 화면에 오프라인 기본값 상태를 표시한다.

Run 시작 시 `clientRunId`를 한 번 생성한다. 사망 또는 3층 클리어 시 실제 캐릭터, 획득 순서의 아티팩트, 도달 층, 처치 수, 플레이 시간과 종료 원인을 하나의 요청으로 고정해 전송한다. 중복 종료 이벤트는 무시하며 실패 후 화면의 **Retry save with the same Run ID** 버튼은 저장된 요청 객체와 ID를 그대로 다시 보낸다. 성공 결과에는 획득 XP, 현재 레벨·경험치/다음 필요 경험치, 미사용 포인트와 저학년/고학년 스킬 레벨이 표시된다.

Unity 메뉴에서 **Trickal Fan Game > Verify Phase H-5 Meta Progression**을 Edit Mode와 Play Mode에서 각각 실행한다. 검증기는 Lv.1/Lv.10 피해·투사체·쿨타임 경계, 새 진행 스냅샷의 이전 값 덮어쓰기, 온라인·캐시·기본값 폴백, 클리어·사망 DTO, 중복 종료, 동일 요청 재시도, 성공·네트워크 실패·멱등성 충돌 표시를 확인한다. 두 모드 모두 Console에 `Phase H-5 verification passed`가 출력되고 오류가 없어야 H-5를 완료 처리한다.

## 아이템 전투 테스트 방

Unity 메뉴에서 **Trickal Fan Game > Debug > Open or Create Item Test Room**을 실행하면 `Assets/Scenes/ItemTestScene.unity`가 생성되거나 열린다. `Item Test Room` 오브젝트의 Inspector에서 아이템별 시작 스택과 적 Prefab·활성 여부·로컬 좌표·회전을 설정하고 Play Mode를 시작한다. 이 씬은 랜덤 층 생성과 Backend Run 저장을 사용하지 않는다.

Play Mode의 오른쪽 패널에서 아이템을 `+1`씩 즉시 획득하고 현재 전투 수치를 확인할 수 있다. `Heal / Reset HP`와 `Respawn Enemies`로 같은 배치를 반복 테스트하며, 스택을 줄이거나 초기화하려면 Play Mode를 다시 시작한다. **Trickal Fan Game > Debug > Verify Item Test Room**을 Edit Mode와 Play Mode에서 각각 실행해 `passed`와 Console 오류 0개를 확인한다.
