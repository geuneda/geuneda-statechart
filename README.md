# Geuneda Statechart

Unity에서 Statechart(계층적 상태 머신, HFSM)를 코드로 구성하고 실행하는 패키지입니다. 상태는 하위 영역을 가질 수 있고(Nest), 병렬 영역(Split), 조건 분기(Choice), 비동기 대기(Wait, TaskWait)를 지원합니다.

Statechart 개념: https://statecharts.github.io/what-is-a-statechart.html

## 설치

이 패키지는 `com.cysharp.unitask` 에 의존합니다. UniTask 는 Unity 공식 레지스트리에 없으므로 함께 추가해야 합니다. 프로젝트의 `Packages/manifest.json` 에 다음을 추가합니다.

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.geuneda.statechart": "https://github.com/geuneda/geuneda-statechart.git"
  }
}
```

Package Manager 창에서 `+` > `Add package from git URL...` 로 같은 URL 을 입력해도 됩니다. 특정 버전에 고정하려면 URL 끝에 `#<태그>` 를 붙입니다.

## 빠른 시작

```csharp
using Geuneda.StatechartMachine;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    private readonly IStatechartEvent _seePlayer = new StatechartEvent("SeePlayer");
    private readonly IStatechartEvent _lostPlayer = new StatechartEvent("LostPlayer");

    private IStatechart _statechart;

    private void Awake()
    {
        // 상태와 전이는 생성자의 setup 콜백에서만 정의할 수 있습니다
        _statechart = new Statechart(factory =>
        {
            var initial = factory.Initial("Initial");
            var patrol = factory.State("Patrol");
            var chase = factory.State("Chase");

            initial.Transition().Target(patrol);

            patrol.OnEnter(() => Debug.Log("순찰 시작"));
            patrol.Event(_seePlayer).Target(chase);

            chase.OnEnter(() => Debug.Log("추격 시작"));
            chase.Event(_lostPlayer).OnTransition(() => Debug.Log("놓침")).Target(patrol);
        });
    }

    private void Start() => _statechart.Run();

    public void OnSeePlayer() => _statechart.Trigger(_seePlayer);
    public void OnLostPlayer() => _statechart.Trigger(_lostPlayer);
}
```

## 핵심 개념

### 구성 흐름

1. `new Statechart(setup)`: `setup` 에 전달된 `IStateFactory` 로 상태를 만들고 전이를 연결합니다. 이후에는 구성을 바꿀 수 없습니다.
2. `Run()`: Initial 상태부터 실행을 시작하거나, 일시정지된 위치에서 재개합니다.
3. `Trigger(event)`: 현재 상태에 정의된 이벤트 전이를 run-to-completion 방식으로 처리합니다. 일시정지 중에 보낸 이벤트는 버려집니다.
4. `Pause()`: 실행을 멈춥니다. `Run()` 으로 재개합니다.
5. `Reset()`: 현재 위치를 Initial 상태로 되돌립니다. 실행/정지 여부는 바꾸지 않고 현재 상태의 `OnExit` 도 호출하지 않습니다.

### 상태 종류

| 팩토리 메서드 | 반환 타입 | 설명 |
|---|---|---|
| `Initial(name)` | `IInitialState` | 영역의 시작점. 전이 하나(`Transition()`)와 `OnExit` 만 가짐. 영역마다 하나 |
| `Final(name)` | `IFinalState` | 영역의 완료 지점. `OnEnter` 만 가짐. 영역마다 최대 하나 |
| `State(name)` | `ISimpleState` | 이벤트를 기다리며 멈추는 상태 |
| `Transition(name)` | `ITransitionState` | 멈추지 않고 바로 다음 상태로 넘어가는 통과 상태 |
| `Choice(name)` | `IChoiceState` | 조건으로 다음 상태를 고르는 분기 |
| `Nest(name)` | `INestState` | 하위 영역 하나를 실행. 하위 영역이 Final 에 도달하면 전이 |
| `Split(name)` | `ISplitState` | 2개 이상의 병렬 영역을 실행. 모든 영역이 Final 에 도달하면 전이 |
| `Wait(name)` | `IWaitState` | `IWaitActivity.Complete()` 가 호출될 때까지 대기. 이벤트로 중단 가능 |
| `TaskWait(name)` | `ITaskWaitState` | `Task`/`UniTask` 완료까지 대기. 이벤트를 받지 않음 |
| `Leave(name)` | `ILeaveState` | Nest/Split 내부에서 바로 위 영역의 상태로 빠져나감 |

### 전이

전이는 `ITransition` 을 체이닝해 구성합니다.

```csharp
state.Event(someEvent)              // 이벤트 전이 (State, Nest, Split, Wait)
    .OnTransition(() => PlaySound()) // 전이 중 실행할 액션. 여러 번 추가 가능
    .Target(nextState);              // 대상 상태
```

- `Transition()`: Initial, Transition, Leave 상태의 자동 전이. 상태마다 하나만 만들 수 있습니다.
- `Event(e)` 에서 `Target` 을 생략하면 상태는 그대로 두고 `OnTransition` 액션만 실행합니다.
- Choice 상태는 `Transition().Condition(() => ...)` 로 조건 전이를 만들고, 조건 없는 기본 전이를 정확히 하나 둡니다.

### 중첩과 병렬

```csharp
var combat = factory.Nest("Combat");
combat.Nest(inner =>
{
    var init = inner.Initial("CombatInit");
    var fight = inner.State("Fight");
    var end = inner.Final("CombatEnd");

    init.Transition().Target(fight);
    fight.Event(enemyDefeated).Target(end);
}).Target(victory);                 // 하위 영역이 Final 에 도달하면 실행
combat.Event(flee).Target(escape);  // 바깥 이벤트로 하위 영역을 강제 종료
```

`Nest`/`Split` 에 `Action<IStateFactory>` 대신 `NestedStateData` 를 넘기면, 상태를 떠날 때 내부 현재 상태의 `OnExit`(`ExecuteExit`)와 내부 Final 상태의 `OnEnter`(`ExecuteFinal`)를 실행할지 정할 수 있습니다. 람다만 넘기면 둘 다 `true` 입니다.

### 대기

```csharp
var loading = factory.Wait("Loading");
loading.WaitingFor(activity => StartCoroutine(Load(activity))).Target(next);

var download = factory.TaskWait("Download");
download.WaitingFor(DownloadAsync).Target(next); // Func<Task> 또는 Func<UniTask>
```

`IWaitActivity.Split()` 으로 자식 활동을 만들면 모든 자식이 완료되어야 Wait 상태가 끝납니다.

### 검증과 디버깅

- 에디터 또는 `DEBUG` 빌드에서는 생성자가 모든 상태의 구성을 검증합니다. 자기 자신으로의 전이, 대상 없는 자동 전이, Choice 의 기본 전이 누락, Nest/Split 하위 영역의 Initial 누락 등은 예외로 알려 줍니다. 최상위 영역에 Initial 상태가 없으면 빌드 종류와 관계없이 `MissingMemberException` 이 발생합니다.
- `OnEnter`/`OnExit`/`OnTransition`/대기 액션에서 발생한 예외는 잡혀서 `Debug.LogError` 와 `Debug.LogException` 으로 출력되고, 상태 흐름은 계속됩니다.
- `Statechart.LogsEnabled` 또는 상태별 `IState.LogsEnabled` 를 켜면 전이 로그를 콘솔에 출력합니다. 에디터에서는 `Statechart.CurrentState` 로 최상위 영역의 현재 상태 이름을 볼 수 있습니다.

## 요구 사항

- Unity 2022.3 이상
- `com.cysharp.unitask` 2.5.10
- 네임스페이스: `Geuneda.StatechartMachine` (어셈블리 `Geuneda.Statechart`)

## 라이선스

MIT. 원저작자 Miguel Tomás. 자세한 내용은 [LICENSE.md](LICENSE.md) 를 참고하세요. 변경 내역은 [CHANGELOG.md](CHANGELOG.md) 에 있습니다.
