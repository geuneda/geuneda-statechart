using Cysharp.Threading.Tasks;
using System;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine
{
	/// <summary>
	/// <see cref="IStatechart"/>에서의 상태 표현
	/// </summary>
	public interface IState
	{
		/// <summary>
		/// 이 특정 상태에서 발생할 수 있는 오류를 디버깅하기 위해 콘솔에 로그를 표시할 수 있도록 합니다
		/// </summary>
		bool LogsEnabled { get; set; }
	}

	#region 구성 요소

	/// <summary>
	/// <see cref="IState"/>에 다른 <see cref="IState"/>로의 전이 기능을 부여합니다
	/// </summary>
	public interface IStateTransition : IState
	{
		/// <summary>
		/// 두 상태 간의 전이를 설정하며 현재 상태에 의해 트리거될 때 처리됩니다.
		/// 상태 차트에서 이벤트가 처리될 때 트리거될 생성된 <see cref="ITransition"/>을 반환합니다.
		/// </summary>
		ITransition Transition();
	}

	/// <summary>
	/// <see cref="IState"/>에 활성화 시 진입 실행 기능을 부여합니다
	/// </summary>
	public interface IStateEnter : IState
	{
		/// <summary>
		/// 상태가 활성화될 때 호출할 <paramref name="action"/>을 추가합니다
		/// </summary>
		void OnEnter(Action action);
	}

	/// <summary>
	/// <see cref="IState"/>에 비활성화 시 퇴장 실행 기능을 부여합니다
	/// </summary>
	public interface IStateExit : IState
	{
		/// <summary>
		/// 상태가 비활성화될 때 호출할 <paramref name="action"/>을 추가합니다
		/// </summary>
		void OnExit(Action action);
	}

	/// <summary>
	/// <see cref="IState"/>에 상태 전이를 트리거하는 기능을 부여합니다
	/// </summary>
	public interface IStateEvent : IState
	{
		/// <summary>
		/// 상태 차트에서 처리될 때 전이를 트리거할 <paramref name="statechartEvent"/>를 상태에 추가합니다.
		/// 상태 차트에서 이벤트가 처리될 때 트리거될 생성된 <see cref="ITransition"/>을 반환합니다.
		/// </summary>
		ITransition Event(IStatechartEvent statechartEvent);
	}

	#endregion

	#region 상태

	/// <summary>
	/// 초기 의사 상태는 영역의 시작점을 나타냅니다. 즉, 영역에 진입했을 때
	/// 실행이 시작되는 지점입니다.
	/// <see cref="IStatechart"/>는 단일 영역에서 초기 상태를 하나만 가질 수 있지만, 중첩 영역에서는 더 많은 초기 상태를 가질 수 있습니다.
	/// </summary>
	public interface IInitialState : IStateExit, IStateTransition
	{
	}

	/// <summary>
	/// 최종 상태는 포함하는 영역이 완료되었음을 표시합니다.
	/// 최종 상태로의 전이는 해당 최종 상태를 포함하는 영역의 완료를 나타냅니다.
	/// <see cref="IStatechart"/>는 단일 영역에서 최종 상태를 하나만 가질 수 있지만, 중첩 영역에서는 더 많은 최종 상태를 가질 수 있습니다.
	/// </summary>
	public interface IFinalState : IStateEnter
	{
	}

	/// <summary>
	/// 전이 상태는 <see cref="IStatechart"/> 동작 실행 중 비차단 상황을 모델링합니다.
	/// 상태 차트 실행에서 자동으로 대상 상태로 계속 진행하는 비차단 지점을 나타냅니다.
	/// </summary>
	public interface ITransitionState : IStateEnter, IStateExit, IStateTransition
	{
	}

	/// <summary>
	/// 단순 상태는 <see cref="IStatechart"/> 동작 실행 중 상황을 모델링합니다.
	/// 이벤트 트리거를 기다리며 상태 차트 실행을 차단하는 지점을 나타냅니다.
	/// </summary>
	public interface ISimpleState : IStateEnter, IStateExit, IStateEvent
	{
	}

	/// <summary>
	/// 중첩 상태는 <see cref="IStatechart"/>에서 새로운 중첩 영역을 생성할 수 있게 합니다.
	/// 코드의 비대함을 줄이고 가독성을 높이는 데 매우 유용합니다.
	/// </summary>
	public interface INestState : IStateEnter, IStateExit, IStateEvent
	{
		/// <inheritdoc cref="Nest(NestedStateData)"/>
		/// <remarks>
		/// <see cref="NestedStateData"/>의 실행 옵션이 true로 설정된 중첩 상태
		/// </remarks>
		ITransition Nest(Action<IStateFactory> data);

		/// <summary>
		/// <paramref name="data"/>에 정의된 새로운 중첩 영역을 생성합니다.
		/// 중첩 영역이 완료되는 즉시 트리거될 생성된 <see cref="ITransition"/>을 반환합니다.
		/// </summary>
		/// <remarks>
		/// 이 <see cref="INestState"/>가 완료될 때 현재 활성 상태의
		/// <see cref="IStateExit.OnExit"/>를 실행합니다.
		/// 이 <see cref="INestState"/>가 완료될 때 중첩 상태의
		/// <see cref="IFinalState"/>는 실행하지 않습니다.
		/// </remarks>
		ITransition Nest(NestedStateData data);
	}

	/// <summary>
	/// 대기 상태는 <see cref="IStatechart"/> 동작을 차단하는 상태입니다.
	/// 정의된 활동의 완료를 기다리거나 이벤트가 트리거되어 상태 차트 실행을 재개할 때까지 대기합니다.
	/// 동시성의 경우 이 상태의 <see cref="IStateEvent.Event(IStatechartEvent)"/> 동작을 확인하기 전에
	/// <see cref="WaitingFor(Action{IWaitActivity})"/> 호출을 먼저 실행합니다.
	/// 부모 <see cref="INestState"/> 또는 <see cref="ISplitState"/>에서 퇴장할 때 이 상태가 현재 활성 상태인 경우,
	/// 동시성 병목을 피하기 위해 실행과 모든 내부 활동을 강제 완료합니다.
	/// </summary>
	public interface IWaitState : IStateEnter, IStateExit, IStateEvent
	{
		/// <summary>
		/// 주어진 <paramref name="waitAction"/>과 가능한 자식 활동이 완료될 때까지
		/// 상태 동작을 차단합니다.
		/// 상태가 차단 해제되는 즉시 트리거될 생성된 <see cref="ITransition"/>을 반환합니다.
		/// 동시성의 경우 이 상태의 <see cref="IStateEvent.Event(IStatechartEvent)"/> 동작을
		/// 확인하기 전에 이 호출을 먼저 수행합니다.
		/// </summary>
		ITransition WaitingFor(Action<IWaitActivity> waitAction);
	}

	/// <summary>
	/// 태스크 대기 상태는 <see cref="IStatechart"/> 동작을 차단하는 상태입니다.
	/// 비동기로 정의된 <see cref="Task"/>의 완료를 기다린 후 상태 차트 실행을 재개합니다.
	/// 스레드 태스크 중 동시성을 방지하기 위해 이 상태는 <see cref="IStateEvent.Event(IStatechartEvent)"/>를 실행할 수 없습니다.
	/// 부모 <see cref="INestState"/> 또는 <see cref="ISplitState"/>에서 퇴장할 때 이 상태가 현재 활성 상태인 경우,
	/// 동시성 병목을 피하기 위해 퇴장을 일시 정지하고 대기 과정 중 보류된 모든 이벤트를 큐에 넣습니다.
	/// </summary>
	public interface ITaskWaitState : IStateEnter, IStateExit
	{
		/// <summary>
		/// 주어진 비동기 <paramref name="taskAwaitAction"/>이 완료될 때까지 상태 동작을 차단합니다.
		/// 상태가 차단 해제되는 즉시 트리거될 생성된 <see cref="ITransition"/>을 반환합니다.
		/// </summary>
		ITransition WaitingFor(Func<Task> taskAwaitAction);

		/// <inheritdoc cref="WaitingFor(Func<Task>)"/>
		ITransition WaitingFor(Func<UniTask> taskAwaitAction);
	}

	/// <summary>
	/// 선택 상태는 명시적 조건이 <see cref="IStatechart"/> 동작을 제어하는 실행 중 상황을 모델링합니다.
	/// 항상 유효한 전이가 정의되어 있으므로 이 상태는 상태 차트 실행을 차단하지 않습니다.
	/// </summary>
	public interface IChoiceState : IStateEnter, IStateExit
	{
		/// <summary>
		/// 상태에 조건 동작을 가진 <see cref="ITransitionCondition"/>을 생성합니다.
		/// 상태 차트에서 조건이 처리될 때 트리거될 생성된 <see cref="ITransitionCondition"/>을 반환합니다.
		/// </summary>
		ITransitionCondition Transition();
	}

	/// <summary>
	/// 분할 상태는 <see cref="IStatechart"/>에서 두 개의 새로운 중첩 병렬 영역을 생성할 수 있게 합니다.
	/// 두 개의 새로운 중첩 영역은 병렬로 실행되고 처리됩니다.
	/// </summary>
	public interface ISplitState : IStateEnter, IStateExit, IStateEvent
	{
		/// <inheritdoc cref="Split(NestedStateData[])"/>
		/// <remarks>
		/// <see cref="NestedStateData"/>의 실행 옵션이 true로 설정된 분할 상태
		/// </remarks>
		ITransition Split(params Action<IStateFactory>[] data);

		/// <summary>
		/// 상태를 동시에 활성화되는 두 개의 새로운 중첩 병렬 영역으로 분할합니다.
		/// <paramref name="data"/>에 정의된 모든 중첩 영역을 설정합니다.
		/// 두 중첩 영역이 각각의 <see cref="IFinalState"/>에 도달하여 실행을 완료하면
		/// 트리거될 생성된 <see cref="ITransition"/>을 반환합니다.
		/// </summary>
		/// <remarks>
		/// 이 <see cref="ISplitState"/>가 완료될 때 현재 활성 상태의
		/// <see cref="IStateExit.OnExit"/>를 실행합니다.
		/// 이 <see cref="ISplitState"/>가 완료될 때 중첩 상태의
		/// <see cref="IFinalState"/>는 실행하지 않습니다.
		/// </remarks>
		ITransition Split(params NestedStateData[] data);
	}

	/// <summary>
	/// 이탈 상태는 <see cref="IFinalState"/>와 매우 유사하며, 포함하는 영역이 완료되었음을 표시합니다.
	/// 핵심 차이점은 이탈 상태의 전이가 다른 영역의 상태를 대상으로 하여
	/// <see cref="INestState"/> 또는 <see cref="ISplitState"/>의 전이 대상을 우회한다는 것입니다.
	/// 이 상태는 <see cref="INestState"/> 또는 <see cref="ISplitState"/> 내부에 있어야 하며 한 영역 레이어만 점프할 수 있습니다.
	/// </summary>
	public interface ILeaveState : IStateEnter, IStateTransition
	{
	}

	/// <summary>
	/// <see cref="ISplitState"/> 및 <see cref="INestState"/>를 위한 중첩 상태 설정 데이터 구성
	/// </summary>
	public struct NestedStateData
	{
		/// <summary>
		/// 이 중첩 상태 정의의 설정
		/// </summary>
		public Action<IStateFactory> Setup;
		/// <summary>
		/// true인 경우 이 <see cref="ISplitState"/>의 완료로 중첩 상태를 떠날 때
		/// 내부 현재 활성 <see cref="IStateExit.OnExit"/>가 실행됩니다.
		/// </summary>
		public bool ExecuteExit;
		/// <summary>
		/// true인 경우 이벤트 또는 내부 상태 중 하나의 <see cref="ILeaveState"/>로 인해
		/// 중첩 상태를 떠날 때 내부 <see cref="IFinalState"/>가 실행됩니다.
		/// </summary>
		public bool ExecuteFinal;

		public NestedStateData(Action<IStateFactory> setup, bool executeExit, bool executeFinal)
		{
			Setup = setup;
			ExecuteExit = executeExit;
			ExecuteFinal = executeFinal;
		}

		public NestedStateData(Action<IStateFactory> setup)
		{
			Setup = setup;
			ExecuteExit = true;
			ExecuteFinal = true;
		}

		public static implicit operator NestedStateData(Action<IStateFactory> setup)
		{
			return new NestedStateData(setup);
		}
	}

	#endregion
}