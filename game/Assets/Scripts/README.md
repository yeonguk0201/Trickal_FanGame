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
2. 출구 오브젝트에 `BoxCollider2D`와 `DoorController`를 추가한 뒤 Room의 `Doors` 목록에 넣는다. 전투가 시작되면 Collider가 켜지고 전멸하면 꺼진다.
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

Play Mode를 종료한 뒤 Unity 메뉴에서 **Trickal Fan Game > Setup Week 5 Character Selection**을 한 번 실행한다. Play Mode가 시작되면 게임 시간이 멈추고 Character A 선택 화면이 나타난다. 선택하기 전에는 플레이어 이동과 공격이 비활성화되며, 선택한 뒤부터 Run 시간 측정과 플레이가 시작된다.

캐릭터 정보는 `Assets/Characters/character-a.asset`의 `CharacterDefinition`으로 관리한다. 이 에셋의 `characterId`는 Backend seed의 `Character.id`와 동일한 `character-a`다. 캐릭터를 추가할 때는 같은 형식의 에셋을 만들고 `CharacterSelectionUI` 목록에 연결하면 된다. 선택된 ID는 `RunSession`의 `CreateRunRequest.characterId`에 기록된다.
