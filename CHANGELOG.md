# Changelog
이 패키지의 모든 주요 변경사항은 이 파일에 기록됩니다.

이 형식은 [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [Semantic Versioning](http://semver.org/spec/v2.0.0.html)을 따릅니다.

## [0.9.3] - 2024-11-13

**New**
- *ITaskWaitState.WaitingFor(Func<UniTask>)* 메서드를 추가하여 *TaskWaitStates*가 *UniTask* 타입 호출을 대기할 수 있도록 지원

**Fixed**
- *Statechart*가 WebGL에서 실행될 수 있도록 패키지에 *UniTask*를 추가

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
