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
