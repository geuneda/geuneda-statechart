# Changelog
이 패키지의 모든 주요 변경사항은 이 파일에 기록됩니다.

이 형식은 [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [Semantic Versioning](http://semver.org/spec/v2.0.0.html)을 따릅니다.

## [Unreleased]

**New**
- *ITaskWaitState.Failed()* 추가. 태스크가 예외로 끝났을 때 실행할 전이를 설정합니다
- *ITaskWaitState.WaitingFor(Func<CancellationToken, Task>)*, *WaitingFor(Func<CancellationToken, UniTask>)* 추가. 부모 *INestState*/*ISplitState*가 이벤트나 *ILeaveState*로 퇴장하려 하거나 *Statechart.Reset*되면 토큰에 취소를 요청합니다. 이 취소로 생긴 *OperationCanceledException*은 실패로 보지 않습니다

**Fixed**
- *ITaskWaitState*의 태스크가 예외로 끝나면 상태가 완료되지 않아 상태 차트가 그 자리에 멈추고, 부모 *INestState*/*ISplitState*도 미뤄 둔 퇴장을 끝내지 못하던 문제를 수정. 예외를 로그로 남긴 뒤 *Failed()* 전이(없으면 *WaitingFor* 전이)를 실행하고, 퇴장이 미뤄진 경우에는 미뤄 둔 이벤트로 부모가 퇴장합니다
- *ISplitState*/*INestState*에 대상 없는 이벤트(*OnTransition*만 등록)가 들어와도 퇴장할 때처럼 내부 *IWaitState*를 강제 완료하고 실행 중인 태스크를 기다리던 문제를 수정. 대상 없는 이벤트는 내부 대기를 건드리지 않고 바로 실행합니다
- 퇴장을 미룬 *ISplitState*/*INestState*가 한 영역에서 태스크가 실행 중이면 뒤쪽 중첩 영역의 *IWaitState*를 강제 완료하지 않던 문제를 수정. 모든 하위 영역을 정리합니다
- 미뤄 둔 퇴장이 이어질 때 태스크 완료가 퇴장 이벤트를 다시 보내, 같은 *ISplitState*의 다른 영역이나 퇴장 뒤 들어간 상태가 그 이벤트를 한 번 더 처리하던 문제를 수정. 퇴장을 미룬 상태가 이벤트를 기억했다가 한 번만 씁니다
- 대상 없는 *INestState*/*ISplitState* 완료 전이(*OnTransition*만 설정)가 하위 영역이 모두 Final인 동안 *Run*이나 대기 완료로 진행 요청이 올 때마다 다시 실행되던 문제를 수정. 진입마다 한 번만 실행합니다
- 퇴장이 미뤄진 동안 상태 차트를 *Pause*한 사이 태스크가 끝나면, 다시 *Run*해도 미뤄 둔 퇴장이 사라지고 그 자리에 머물던 문제를 수정
- 대기 액션 예외에 대한 README와 문서 주석의 설명을 실제 동작에 맞게 수정. *IWaitState*의 대기 액션이 예외를 던지면 활동이 완료되거나 이벤트가 올 때까지 기다립니다

**Changed**
- *Statechart.Reset*이 실행 중인 *ITaskWaitState* 태스크에 취소를 요청합니다(토큰을 받는 *WaitingFor*에서만 효과가 있음)
- *ITaskWaitState*의 로그가 *Func<Task>*를 넘긴 경우에도 내부 람다 대신 사용자가 넘긴 메서드 이름을 보여 줍니다

## [0.9.6] - 2026-10-12

**Fixed**
- *Reset* 전에 시작한 *IWaitState* 활동이나 *ITaskWaitState* 태스크가 리셋 뒤에 완료되면 추가 *Run* 없이 상태 차트가 초기 상태부터 다시 진행되던 문제를 수정. 버려진 실행의 완료는 무시합니다
- *ISplitState*/*INestState* 를 빠져나가며 영역을 종료하는 도중 *Reset*하면 남은 영역의 퇴장 액션과 Final 진입, 이 상태의 퇴장 액션이 계속 실행되던 문제를 수정

## [0.9.5] - 2026-10-12

**Changed**
- *Statechart*가 상태의 진입/퇴장/전이 액션 안에서 들어온 *Trigger*와 대기 완료 요청을 바로 처리하지 않고 큐에 넣었다가, 진행 중인 처리가 끝난 뒤 순서대로 처리하도록 변경(run-to-completion). 이전에는 아직 바뀌지 않은 이전 상태로 이벤트를 처리해 이벤트가 사라지거나 이미 떠난 상태의 전이가 실행될 수 있었습니다. 처리 도중 *Pause*되거나 예외가 *Trigger*/*Run* 밖으로 전파되면 남은 요청은 버립니다(상태 액션의 예외는 지금처럼 로그로 남고 처리는 계속됩니다)

**Fixed**
- 상태 액션 안에서 *Reset*하면 리셋 전에 쌓인 요청이 리셋 뒤에 처리되거나, 진행 중이던 전이 결과가 리셋된 초기 상태를 덮어쓰던 문제를 수정. 퇴장/전이 액션 안에서 리셋하면 그 전이의 남은 단계(전이 액션, 목적 상태 진입)를 실행하지 않고, 대기 액션·선택 조건·중첩/분할 영역 안에서 리셋해도 그 상태의 전이와 다른 영역의 진행을 멈춥니다
- 연쇄 전이(진입 후 이벤트 없이 이어지는 전이) 도중 *Pause*해도 끝까지 진행하던 문제를 수정. 방금 진입한 상태에서 멈추고 *Run*하면 이어서 진행합니다. 중첩/분할 영역 안의 연쇄 전이는 각 영역의 자체 일시정지 처리를 따르며 이번 변경에 포함되지 않습니다

## [0.9.4] - 2026-10-12

**Fixed**
- *IWaitState*의 대기 액션 안에서 활동을 동기적으로 완료하거나 *ITaskWaitState*에 이미 완료된 *UniTask*를 넘기면 완료 콜백이 진행 중인 처리 안으로 재진입해 *OnExit*, 전이, 다음 상태의 *OnEnter*가 두 번 실행되던 문제를 수정
- *INestState*/*ISplitState*의 하위 영역에 *IInitialState*가 없으면 실행 시점에 *NullReferenceException*이 나던 문제를, 생성자 검증에서 *InvalidOperationException*을 던지도록 수정
- 상태 진입 중 예외 로그가 "OnExit() actions"로 표시되고 퇴장하는 상태의 생성 스택 트레이스를 보여 주던 문제를 수정
- *IStatechart.Run*, *ISplitState*, *INestState*, *NestedStateData.ExecuteExit*, *IStateFactory.Transition* 문서 주석을 실제 동작에 맞게 수정

## [0.9.3] - 2026-06-26

**New**
- *ITaskWaitState.WaitingFor(Func<UniTask>)* 메서드를 추가하여 *TaskWaitStates*가 *UniTask* 타입 호출을 대기할 수 있도록 지원

**Fixed**
- *Statechart*가 WebGL에서 실행될 수 있도록 패키지에 *UniTask*를 추가
- *Statechart.MoveNext*에 가드를 추가하여 상태 차트가 일시 정지된 동안 연쇄 상태 전이를 중단하도록 수정

## [0.9.2] - 2024-10-25

**Fixed**
- try/catch 예외 처리를 Debug.LogException과 Debug.LogError로 변경하여 Unity 콘솔에 문제가 올바르게 표시되도록 개선

## [0.9.1] - 2023-09-04

**Fixed**
- *InnerStateData*와 *StateFactoryData* 클래스의 목적을 설명하는 누락된 문서를 추가

## [0.9.0] - 2023-09-02

**New**
- 서로 다른 활동을 구분하기 위해 *IWaitActivity* 인스턴스에 고유 식별자를 도입
- *TaskWaitState* 클래스에 태스크 완료 후 이벤트를 처리하는 새로운 이벤트 큐잉 메커니즘을 추가
- *SplitState* 클래스에서 내부 상태 전이의 일시정지/재개를 허용하도록 개선

**Changed**
- 다양한 클래스의 예외 처리를 업데이트하여, 보다 적절한 오류 표현을 위해 *MissingMemberException*을 *InvalidOperationException*으로 교체

## [0.8.0] - 2023-08-03

**Changed**
- 메인 클래스 이름을 *Statemachine*에서 *Statechart*로 변경하고, 충돌 방지를 위해 네임스페이스를 StatechartMachine으로 변경
- *ISplitState*와 *INestedState*가 단일 파라미터화된 *NestedStateData*를 받도록 변경하여 호출을 간결하게 하고 향후 확장성에 대비

## [0.7.0] - 2021-05-04

**New**
- State 문서를 개선
- State 디버그 로그를 개선

**Changed**
- *ISplitState*에서 중복되는 *SplitFinal* 및 *SplitExitFinal* 메서드를 제거하고, *Split* 메서드가 모든 가능한 케이스를 포함하도록 변경하여 구현을 단순화

**Fixed**
- *WaitState*의 참조 검사를 수정
- *StatechartTaskWaitTest*를 여러 테스트가 병렬로 실행될 때도 동작하도록 수정
- *TaskWaitState*에서 체인된 상태의 경쟁 조건 버그를 수정
- *WaitState*가 즉시 완료될 때 상태 전이가 여러 번 실행되는 버그를 수정

## [0.6.0] - 2020-09-29

**Changed**
- 네임스페이스 충돌 방지를 위해 *Statechart*를 *StateMachine*으로 이름 변경

**Fixed**
- *ITaskWaitState*의 잘못된 테스트를 수정

## [0.5.2] - 2020-09-27

**New**
- 로그에 *IStatechartEvent* 데이터를 추가
- 모든 가능한 트리거 케이스에 로그를 추가
- *ITaskWaitState*와 *IWaitState*의 대기 호출 메서드에 로그를 추가
- *ITaskWaitState*와 *IWaitState*의 대기 호출 메서드에 예외 캐치를 추가
- 각 *IState*에 개별적으로 로그를 활성화할 수 있는 기능을 추가

## [0.5.1] - 2020-09-24

**Fixed**
- *ITaskWaitState* 실행 크래시를 수정

## [0.5.0] - 2020-09-24

**New**
- 두 개의 서로 다른 상태 사이에서 비차단 상태로 작동하는 새로운 *ITransitionState*를 추가

**Changed**
- 이벤트 호출을 허용하기 위해 *ITaskWaitState*에 *IStateEvent*를 추가

**Fixed**
- *INestState* 또는 *ISplitState*가 외부 종료 이벤트를 트리거할 때 *ITaskWaitState*와 *WaitState*가 플로우를 여러 번 실행할 수 있는 잠재적 문제를 수정

## [0.4.0] - 2020-09-22

**New**
- *IStateNest* 또는 *IStateSplit*을 떠날 때 현재 활성 상태에서 *IStateExit.OnExit*를 실행하지 않는 옵션을 추가
- Task 비동기 메서드를 위한 대기자인 새로운 *ITaskWaitState*를 추가. 이 상태는 이벤트 트리거를 지원하지 않으며, 이벤트가 필요한 경우 *IWaitState*를 사용

**Changed**
- *IStateSplit.Split*을 새로운 메서드 *IStateSplit.SplitFinal*로 분리

## [0.3.0] - 2020-09-06

**New**
- 대상 상태 없이 이벤트를 트리거할 수 있는 기능을 추가. *InitialState*, *ChoiceState*, *LeaveState*는 동작 특성상 이 기능을 허용하지 않음

## [0.2.0] - 2020-08-27

**New**
- *NestState*와 *SplitState*의 *FinalState*를 항상 실행할 수 있는 기능을 추가

**Changed**
- 조직화와 가독성을 위해 유닛 테스트를 다른 파일로 분리. 이제 각 상태가 자체 테스트 세트만 포함하는 고유 파일을 가짐

**Fixed**
- *NestState*와 *SplitState*가 중첩 상태 체인으로 설정되었을 때 내부 *OnExit* 호출을 올바르게 실행하도록 수정

## [0.1.3] - 2020-01-06

**Fixed**
- 패키지 의존성을 제거

## [0.1.2] - 2020-01-06

**New**
- 유닛 테스트를 위한 NSubstitute 의존성을 추가

**Fixed**
- 패키지 버전에서 Preview 라벨을 제거

## [0.1.0] - 2020-01-05

- 패키지 배포를 위한 최초 제출
