# ForagerCP — 구조 문서

광물 채집 게임. "채집 → 골드 변환 → 아이템 구매 → 채집력 상승 → 재채집" 한 사이클을 돌리는 것이 1주차 목표였고,
그 위에 광물 종류 분화 / 인벤토리 / 몬스터 / UI 계층까지 올라가 있다.

이 문서는 **무엇을 썼는지(기술 스택)** 와 **왜 그렇게 짰는지(설계에서 신경 쓴 부분)** 를 남긴다.
기능 사양서가 아니라 구조 설명서다. 수치는 전부 인스펙터/에셋에 있으므로 여기 적지 않는다.

---

## 1. 기술 스택

| 영역 | 선택 | 이유 |
|---|---|---|
| 엔진 | Unity **6000.3.18f1**, URP, 3D 탑다운 | 팀 표준 버전. URP blank 템플릿에서 시작 |
| 입력 | **새 Input System** (`activeInputHandler = 1`) | 프로젝트 기본값이 Input System 전용. 액션은 에셋이 아니라 `GameInput.cs`에서 **코드로 정의** — 템플릿 에셋은 Interact가 E라 사양(F)과 어긋나서 |
| 데이터 | **ScriptableObject** (`ItemDefinition` → `MineralDefinition`, `MonsterDefinition`, `MapPalette`) | 콘텐츠 추가가 에셋 작업으로 끝나고 코드를 안 건드리게 |
| UI | uGUI + TextMeshPro + SlimUI *3D Modern Menu UI* 스프라이트 | 프레임/버튼/핸들만 가져다 쓰고, 로직은 전부 자체 구현 |
| 폰트 | TMP **Dynamic** SDF 아틀라스 (한글 6종) | 한글 전체를 정적으로 구우면 아틀라스가 폰트당 수십 MB |
| 메모리 | **자체 오브젝트 풀** (`Core/Pooling`) | 드랍/이펙트/데미지 표시 대비. 첫 실사용처는 몬스터 체력바 |
| 배치 | **그리드(1칸 = 1m) + 씬 오브젝트** | 디자이너가 직접 배치. 런타임 생성 금지 |
| 에디터 확장 | 맵 배치 팔레트 / 폰트 도구 / 콘텐츠 마법사 | 디자이너가 개발자를 거치지 않고 콘텐츠를 넣도록 |
| 버전 관리 | Git (독립 레포, `main`) | `Library/`, `UserSettings/`, MCP 플러그인 DLL 더미는 추적 제외 |

의도적으로 **안 쓴 것**
- **NavMesh** — 맵에 장애물이 드물어 굽는 비용이 더 크다. 직선 추적으로 충분하고, 교체 지점은 `MonsterAI.MoveTowards` 하나뿐이다.
- **DOTween 등 트윈 라이브러리** — 연출이 점멸/펀치/페이드 수준이라 코루틴과 `MoveTowards`로 충분하다.
- **세이브/로드** — 1주차 사양에서 제외. 재실행 시 초기화 허용.

---

## 2. 폴더 구조

```
Assets/
├── _Project/                게임 본체
│   ├── Scripts/
│   │   ├── Core/            인터페이스, 입력, 보상 서비스
│   │   │   ├── Items/       ItemDefinition / MineralDefinition / ItemStack
│   │   │   ├── Grid/        MapGrid / GridPlacement / MapFolder / MapPalette
│   │   │   └── Pooling/     PoolManager / GameObjectPool / PooledInstance / IPooled
│   │   ├── Player/          이동·조준·채집공격·상호작용·체력·카메라
│   │   ├── World/           MineralNode, Monsters/(Monster, MonsterAI, MonsterDefinition)
│   │   ├── Economy/         Inventory, GoldWallet, ConverterStation, ShopStation, ShopItem
│   │   ├── Progression/     PlayerLevel
│   │   ├── UI/              HUD·창·체력바·프롬프트 뷰
│   │   ├── Fx/              HitFlash, Knockback
│   │   └── DebugTools/      DebugCheats (F1~F5)
│   ├── Items/               광물·몬스터 정의 에셋(SO)
│   ├── Prefabs/             Minerals / Stations / Structures / Monsters / UI
│   ├── Materials/, Fonts/   임시 도형 머티리얼, TMP 폰트 에셋
│   └── MapPalette.asset     배치 팔레트 목록
├── Editor/                  디자이너 도구 (게임 빌드에 포함되지 않음)
├── SlimUI/                  구매 UI 에셋 (스프라이트만 사용)
└── Scenes/SampleScene.unity 단일 씬
```

`_Project` 접두사는 외부 에셋(SlimUI, TextMesh Pro)과 **우리 코드를 프로젝트 창에서 즉시 구분**하기 위한 것이다.

---

## 3. 아키텍처 원칙

### 3-1. 의존성은 한 방향, 데이터는 leaf

```
GameInput ──▶ PlayerMotor / PlayerAttacker / PlayerInteractor / UiRouter
                        │                    │
                 IHarvestable          IInteractable        IDamageable
                        │                    │                   │
                  MineralNode       Converter / Shop      Monster / PlayerHealth
                        │ (Harvested)                           │ (Died)
            HarvestRewardService ──┐              CombatRewardService
                                   ▼                            │
              Inventory / PlayerLevel / GoldWallet / HarvestPower
                                   │ (Changed)
                                   ▼
                      HudView / 체력바 / 인벤토리 창
```

**데이터 4종(`Inventory`, `PlayerLevel`, `GoldWallet`, `HarvestPower`)은 아무것도 참조하지 않는다.**
값이 바뀌면 `Changed` 이벤트만 쏘고, 쓰는 쪽이 일방적으로 구독한다.
덕분에 UI를 통째로 바꾸거나 시스템을 추가해도 데이터 레이어는 건드릴 일이 없다.

### 3-2. 문자열 대신 컴포넌트

태그·레이어 문자열로 대상을 판별하지 않는다. `Physics.OverlapSphere` 결과에서
`GetComponentInParent<IHarvestable>()` 같은 **인터페이스 조회**로 거른다.

- 레이어 설정이 깨져도 판정이 죽지 않는다 (과거 프로젝트에서 TagManager 캐시가 깨져 `LayerMask`가 0이 되는 사고를 겪었다)
- 대상이 늘어도 판정 코드가 안 늘어난다

### 3-3. 인터페이스를 용도별로 쪼갠다

| 인터페이스 | 대상 | 목적 |
|---|---|---|
| `IHarvestable` | 광물 | 채집 — 보상 지급으로 이어짐 |
| `IDamageable` | 몬스터, 플레이어 | 전투 — 체력 감소 |
| `IInteractable` | 변환기, 상점 | F키 상호작용 |

좌클릭은 **판정 경로 하나를 공유**하되(전방 부채꼴 최근접 1개), 잡힌 대상이 몬스터면 공격, 광물이면 채집으로 갈린다(`PlayerAttacker`).
보상 처리가 섞이면 안 되기 때문에 인터페이스는 분리했다.

### 3-4. 처리 순서를 한 곳이 소유한다

사양에 "광물 +1 → 경험치 +1 → 레벨 판정 → UI 갱신" 같은 **고정 순서**가 있었다.
이 순서를 `MineralNode` 안에 넣으면 광물이 늘 때마다 순서가 복제된다.

→ 노드는 **"나 캐졌다" 이벤트만** 쏘고, `HarvestRewardService` 한 곳이 순서대로 처리한다.
확장 기능(인벤 꽉 참 → 바닥 드랍)이 들어올 자리도 여기 한 줄로 고정된다.
몬스터 처치 보상도 같은 모양(`CombatRewardService`).

### 3-5. 밸런스 수치는 코드에 없다

모든 수치는 `[SerializeField]` 또는 SO 에셋에 있다. 코드에 하드코딩된 밸런스 값은 없다.
`ConverterStation._priceMultiplier`처럼 **경제 전체를 한 번에 돌릴 손잡이**도 남겨뒀다.

### 3-6. 배치 결과는 항상 씬에 박힌다

맵은 절차적 생성이 아니라 디자이너 배치다. 그래서 배치 도구가 만드는 것은 **씬 오브젝트 + 칸 좌표(`GridPlacement`)** 이고,
게임 실행 중에 생성되는 맵 오브젝트는 하나도 없다. (배치가 런타임 로직에 의존하면 디자이너 작업이 통째로 날아갈 수 있다)

### 3-7. 미확정 기획은 구현하지 않고 자리만 연다

기획이 열려 있는 항목은 **추측으로 채우지 않고**, 나중에 붙일 지점만 표시해뒀다.

| 미확정 항목 | 현재 처리 |
|---|---|
| 죽음 패널티(획득량 감소 디버프) 수치 | `PlayerHealth.Died` 이벤트만 열고, 구독 지점을 주석으로 명시 |
| 인벤 용량 제약 / 셔틀 악용 보완책 | `Inventory._enforceCapacity` **기본 false**. 구조(슬롯+스택)는 완성, 규칙만 비움. `Add()`가 담긴 수량을 반환해 초과분 처리를 한 줄로 얹을 수 있게 |
| 상점 품목 구성 | `ShopPanel`은 목록·구매 흐름만. 판정·차감은 전부 `ShopStation`이 소유 |
| 구역(아일랜드) 경계 | `MonsterAI`의 leash 분기에 주석. 지금은 스폰 지점 거리로 대체 |

### 3-8. 방어적으로 배선한다

팔레트로 찍은 프리팹은 **씬 참조 슬롯이 비어 온다**(프리팹은 씬 오브젝트를 가리킬 수 없다).
그래서 스테이션류는 `Awake`에서 한 번 탐색해 채우고, 그래도 없으면 **`LogError`로 크게 알린다**.
조용히 아무 일도 안 일어나는 것이 제일 나쁘기 때문이다.

---

## 4. 콘텐츠 추가 방법 (코드 수정 없음)

**새 광물**
1. `Tools ▸ ForagerCP ▸ 새 콘텐츠 만들기` 로 정의(SO) + 프리팹 변형 + 팔레트 항목 생성
2. 수치는 SO에서 조정 (체력/채집량/경험치/리스폰/판매가)
3. 배치는 `Ctrl+Shift+M` 팔레트에서 클릭

**새 몬스터** — 같은 마법사에서 생성. 독립 프리팹으로 나오고 팔레트 `Monsters` 카테고리에 등록된다.

광물 프리팹은 기본 `Mineral.prefab`의 **변형(Variant)** 이라 공통 구조를 원본에서 물려받는다.
원본을 고치면 전 종류에 반영된다.

---

## 5. 디자이너 도구 (`Assets/Editor/`)

| 도구 | 메뉴 | 하는 일 |
|---|---|---|
| 맵 배치 팔레트 | `Ctrl+Shift+M` | 씬뷰 클릭 배치 / Ctrl+클릭 삭제 / **R 90° 회전** / 점유 칸 빨강 경고 / 종류에 맞는 그룹 자동 분류 / Undo |
| 새 콘텐츠 만들기 | `Tools ▸ ForagerCP` | 광물·몬스터 정의 + 프리팹 + 팔레트 항목을 한 번에 생성 |
| 폰트 에셋 만들기 | `Tools ▸ ForagerCP` | 폰트 파일(ttf)을 TMP Dynamic 에셋으로 굽기 |
| 폰트 바꾸기 | `Tools ▸ ForagerCP` | 씬 + 게임 프리팹 + TMP 기본 폰트를 한 버튼으로 교체 |

도구 원칙 두 가지:
- **런타임 동작을 대신하는 에디터 스크립트는 만들지 않는다.** 콘텐츠 저작을 돕는 도구만 만든다.
- 도구가 만든 결과는 반드시 씬/에셋에 남는다. 도구가 사라져도 게임은 돈다.

---

## 6. 개발 중 밟은 함정과 해결

실제로 겪고 고친 것들. 같은 구조를 건드릴 때 다시 밟기 쉬운 것 위주로 남긴다.

### 물리 / 라이프사이클

| 증상 | 원인 | 해결 |
|---|---|---|
| 광물이 재생성될 때 **플레이어가 광물 위로 밀려 올라감** | 콜라이더를 켜는 순간 물리가 겹친 몸을 밀어냄 | 재생성 직전 `IsBlocked()`(OverlapBox + `IDamageable` 필터)로 확인하고, 겹쳐 있으면 0.5초 간격 **재시도**. 건너뛰면 그 칸의 광물이 영영 안 돌아오므로 "통과"가 아니라 "보류" |
| 몬스터가 죽은 뒤 **바닥을 뚫고 사라짐**(부활해도 안 보임) | 사망 시 콜라이더만 끄고 **중력은 그대로** → 리스폰 대기 내내 자유낙하 | 죽은 동안 Rigidbody를 **kinematic으로 빼두고**, 부활 시 transform·Rigidbody 양쪽에 위치를 넣고 `Physics.SyncTransforms()` + 속도 0 |
| 넉백이 안 먹힘 | `PlayerMotor`/`MonsterAI`가 매 `FixedUpdate`에 `MovePosition`으로 위치를 덮어써서 `AddForce`가 상쇄됨 | 넉백도 `MovePosition`으로 밀고, **밀리는 동안 이동 컴포넌트가 이동을 양보**(`Knockback.IsActive`) |
| 스폰 위치가 엉뚱함 | `GridPlacement`가 `OnEnable`에서 칸 좌표로 위치를 맞추는데 `Awake`에서 위치를 기억함 | `HomePosition`은 **`Start`에서 캡처** |
| 맵 밖으로 빠지면 복귀 불가 | 물리 밀림으로 바닥 아래로 떨어질 수 있음 | y < -3 구제선 — 몬스터는 `ReturnHome()`(체력 유지), 플레이어는 `Respawn()` |

### 연출

| 증상 | 원인 | 해결 |
|---|---|---|
| 한 광물을 때렸는데 **같은 종류가 전부 하얘짐** | `sharedMaterial`을 직접 수정 (머티리얼은 공유됨) | `MaterialPropertyBlock` 사용 |
| 펀치 크기를 키웠는데 반영 안 됨 | **이미 직렬화된 프리팹/씬 값이 코드 기본값을 이김** | 스크립트로 인스턴스 값까지 일괄 갱신 |
| 크기 펀치 후 원본 크기가 어긋남 | `Awake`에서 잡은 스케일이 `GridPlacement` 보정 전 값 | 원본 스케일은 **첫 타격 때 캡처** |

### UI / 입력

| 증상 | 원인 | 해결 |
|---|---|---|
| 한글이 **□** 로 나옴 | 기본 TMP 폰트(LiberationSans)에 한글 글리프 없음 | 한글 폰트로 TMP Dynamic 에셋 생성 + 전체 교체 도구 |
| 외부 UI 에셋 임포트 후 **프로젝트 전체 컴파일 실패** | 데모 스크립트가 레거시 `Input` 사용 (이 프로젝트는 Input System 전용) | 해당 데모 스크립트 제거. 외부 UI 에셋 임포트 시 매번 확인할 것 |
| 버튼이 눌리지 않음 | EventSystem이 구식 `StandaloneInputModule` | **`InputSystemUIInputModule`** 사용 |
| 폰트 일괄 교체가 남의 에셋까지 건드림 | 프로젝트 전체 프리팹을 훑음 | 탐색 범위를 `Assets/_Project`로 제한 |

### 에디터 스크립팅

| 증상 | 원인 | 해결 |
|---|---|---|
| 에디터에서 컴포넌트를 붙였는데 필드가 null | **편집 모드에서는 `Awake`가 돌지 않음** | 에디터가 부를 수 있는 public 메서드는 필드를 **지연 조회**로 방어 |
| 새로 만든 스크립트 2개만 계속 컴파일에서 누락 | 원인 불명 (재임포트·클린 리빌드 무반응) | **클래스/파일 이름을 바꿔 새로 생성**하면 즉시 해결 |
| 에셋 생성이 Ctrl+Z로 안 되돌려짐 | `AssetDatabase.CreateAsset`은 Undo 대상이 아님 | 도구에 '방금 만든 것 취소' 버튼을 따로 제공 |

### 한글 인코딩

원격 스크립트 실행 경로로 **한글 리터럴을 넘기면 치환문자로 깨진다.**
길이만 같으면 서로 다른 한글이 같은 문자열이 되어, `GameObject.Find(한글)`이 엉뚱한 오브젝트를 집는 사고까지 났다(그룹 두 개가 하나로 합쳐짐).

→ **파일·에셋 이름은 영문 강제**, 표시명만 한글. 오브젝트 식별은 이름이 아니라 **컴포넌트/필드 값**으로.

---

## 7. 검증 방식

에디터 자동화로 **플레이 모드에서 로직을 직접 구동**해 확인했다. 입력(키보드·마우스)만 사람이 확인한다.

확인한 항목 예: 6타 채집 → 광물 1 / 경험치 1, 파괴된 광물 추가 타격 시 무보상, 전량 변환 금액,
골드 부족 구매 무동작, 1회성 아이템 재구매 차단, 경험치 이월·연속 레벨업·상한, 리스폰 타이머,
넉백 방향과 거리, 겹침 보류/해제, 인벤 스택 병합, 풀 재사용·이중 반납 거부.

검증하며 배운 것:
- 몬스터 AI가 플레이어를 쫓아와 때리고 넉백까지 걸어서 **위치 기반 검증이 오염**된다 → AI를 끄고 고정된 방해물로 검증
- 무적 시간 때문에 같은 프레임에 연타한 피해가 전부 무시된다 → 테스트에서는 무적을 해제
- 텔레포트 직후 겹침 판정 전에는 `Physics.SyncTransforms()` 필수
- 에디터가 백그라운드면 플레이 모드가 거의 멈춘다(`Run In Background`를 켜도 느림) → 타이머 검증은 `Time.timeScale`을 올려서

---

## 8. 알려진 제약 / 앞으로

- 경로 탐색이 직선 추적이라 슬라임이 구조물에 낄 수 있다 → 문제가 되면 `MonsterAI.MoveTowards`만 교체
- 아이템 아이콘이 없어 인벤토리가 **색 칩**으로 종류를 구분한다 → 아이콘이 들어오면 자동으로 덮인다
- 상점은 품목 기획 대기 중이라 껍데기다
- 폰트 Dynamic 아틀라스는 새 글자를 굽고 나면 에셋 파일이 바뀐다 → 폰트 에셋에 **의미 없는 diff가 생길 수 있다**
- 한글 무료 폰트가 레포에 포함되어 있다. 재배포 허용 폰트들이지만 **라이선스 고지는 Cafe24 것만 동봉**되어 있어, 외부 배포 전에 나머지 고지를 추가해야 한다
