using System;
using System.Collections.Generic;
using System.Threading;
using Geuneda.StatechartMachine.Internal;

// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine
{
	/// <summary>
	 /// 상태 차트 디버깅을 돕기 위한 인터페이스
	 /// </summary>
	public interface IStateMachineDebug
	{
		/// <summary>
		/// 상태 및 전이에서 발생할 수 있는 오류를 디버깅하기 위해 콘솔에 로그를 표시할 수 있도록 합니다
		/// </summary>
		bool LogsEnabled { get; set; }
	}

	/// <summary>
	/// 상태 차트를 나타내고 이를 구동하는 메인 객체입니다.
	/// 상태 차트 구성은 생성자의 설정 액션에서 정의되며 런타임 중에는 수정할 수 없습니다.
	/// 시맨틱스는 <see href="http://www.omg.org/spec/UML"/>을 참조하세요.
	/// </summary>
	public interface IStatechart : IStateMachineDebug
	{
		/// <summary>
		/// 실행 완료(run-to-completion) 패러다임으로 상태 차트에 이벤트 <param name="trigger"></param>를 처리합니다.
		/// 트리거는 상태 차트가 일시 정지 상태가 아닐 때만 동작합니다.
		/// </summary>
		void Trigger(IStatechartEvent trigger);

		/// <summary>
		/// 현재 앵커된 위치에서 상태 차트의 제어를 시작/재개합니다.
		/// 이미 실행 중이어도 무시하지 않고 항상 현재 상태를 이벤트 없이 다시 처리합니다.
		/// 따라서 일시 정지 동안 완료된 대기 상태나 <see cref="Reset"/> 이후의 초기 상태는 이 호출에서 다음 상태로 전이됩니다.
		/// </summary>
		void Run();

		/// <summary>
		/// 상태 차트의 제어를 일시 정지합니다.
		/// 재개하려면 <see cref="Run"/>을 호출하세요.
		/// </summary>
		void Pause();

		/// <summary>
		/// 상태 차트를 초기 시작 지점으로 리셋합니다.
		/// 이 호출은 상태 차트의 제어를 일시 정지하거나 재개하지 않습니다. 상태 차트가 이벤트 대기 모드이거나
		/// 일시 정지 상태인 경우, 재개하려면 <see cref="Run"/>을 호출해야 합니다.
		/// 실행 중인 <see cref="ITaskWaitState"/> 태스크에는 취소를 요청하고, 그 태스크의 완료는 무시합니다.
		/// </summary>
		void Reset();
	}

	/// <inheritdoc cref="IStatechart"/>
	public class Statechart : IStatechart
	{
		private bool _isRunning;
		private bool _isMovingNext;
		// Reset 할 때마다 바뀐다. 처리 도중 리셋되면 진행 중인 전이 결과를 버리는 데 쓴다
		private int _resetVersion;
		private IStateInternal _currentState;

		// 상태 처리 도중에 들어온 이벤트. 현재 처리가 끝난 뒤 순서대로 처리한다
		private readonly Queue<IStatechartEvent> _pendingEvents = new Queue<IStatechartEvent>();

		private readonly IStateFactoryInternal _stateFactory;

		/// <inheritdoc />
		public bool LogsEnabled { get; set; }

#if UNITY_EDITOR
		public string CurrentState => _currentState.Name;
#endif

		private Statechart() {}

		public Statechart(Action<IStateFactory> setup)
		{
			var stateFactory = new StateFactory(0, new StateFactoryData { Statechart = this, StateChartMoveNextCall = MoveNext, ResetVersion = GetResetVersion });

			setup(stateFactory);

			_stateFactory = stateFactory;

			if (_stateFactory.InitialState == null)
			{
				throw new MissingMemberException("State chart doesn't have initial state");
			}

			_currentState = _stateFactory.InitialState;

#if UNITY_EDITOR || DEBUG
			for(int i = 0; i < _stateFactory.States.Count; i++)
			{
				_stateFactory.States[i].Validate();
			}
#endif
		}

		/// <inheritdoc />
		public void Trigger(IStatechartEvent trigger)
		{
			if (!_isRunning)
			{
				return;
			}

			MoveNext(trigger);
		}

		/// <inheritdoc />
		public void Run()
		{
			_isRunning = true;

			MoveNext(null);
		}

		/// <inheritdoc />
		public void Pause()
		{
			_isRunning = false;
		}

		/// <inheritdoc />
		public void Reset()
		{
			_currentState = _stateFactory.InitialState;
			// 상태 액션 안에서 리셋되면 리셋 전에 쌓인 요청과 진행 중인 전이 결과가 리셋 뒤에 섞이지 않게 한다
			_resetVersion++;
			_pendingEvents.Clear();

			// 리셋 전에 시작한 태스크의 결과는 버리므로 실행 중인 태스크에 취소를 요청한다.
			// 취소 콜백에서 Run 하면 상태가 다시 진입해 새 실행이 시작될 수 있으므로, 리셋 전 실행을 먼저 모두 떼어 낸 뒤 취소한다
			List<(TaskWaitState State, CancellationTokenSource Cancellation)> runningTasks = null;

			for (var i = 0; i < _stateFactory.States.Count; i++)
			{
				if (_stateFactory.States[i] is TaskWaitState taskWaitState &&
				    taskWaitState.TakeCancellation() is { } cancellation)
				{
					runningTasks ??= new List<(TaskWaitState, CancellationTokenSource)>();
					runningTasks.Add((taskWaitState, cancellation));
				}
			}

			if (runningTasks == null)
			{
				return;
			}

			for (var i = 0; i < runningTasks.Count; i++)
			{
				runningTasks[i].State.Cancel(runningTasks[i].Cancellation);
			}
		}

		private void MoveNext(IStatechartEvent trigger)
		{
			if (!_isRunning)
			{
				return;
			}

			// 진입/퇴장/전이 액션 안에서 다시 들어온 요청은 현재 처리가 끝난 뒤 순서대로 처리한다(run-to-completion).
			// 바로 처리하면 _currentState 가 아직 이전 상태라 이미 빠져나온 상태로 이벤트를 처리하게 된다.
			if (_isMovingNext)
			{
				_pendingEvents.Enqueue(trigger);
				return;
			}

			_isMovingNext = true;

			try
			{
				ProcessEvent(trigger);

				while (_isRunning && _pendingEvents.Count > 0)
				{
					ProcessEvent(_pendingEvents.Dequeue());
				}
			}
			finally
			{
				_isMovingNext = false;
				// 일시 정지나 예외로 처리하지 못한 요청은 버린다(일시 정지 중 Trigger 를 무시하는 것과 같다)
				_pendingEvents.Clear();
			}
		}

		private int GetResetVersion()
		{
			return _resetVersion;
		}

		private void ProcessEvent(IStatechartEvent trigger)
		{
			var resetVersion = _resetVersion;
			var nextState = _currentState.Trigger(trigger);

			// 처리 도중 Reset 되었으면 리셋 전 전이의 결과로 현재 상태를 덮어쓰지 않는다
			while (nextState != null && resetVersion == _resetVersion)
			{
				_currentState = nextState;

				// 연쇄 전이 도중 일시 정지되면 방금 진입한 상태에서 멈춘다. 다시 Run 하면 이 상태부터 이어서 진행한다
				if (!_isRunning)
				{
					break;
				}

				nextState = _currentState.Trigger(null);
			}
		}
	}
}