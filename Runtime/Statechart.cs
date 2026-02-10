using System;
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
	/// 시맨틱스는 <see cref="http://www.omg.org/spec/UML"/>을 참조하세요.
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
		/// 이미 실행 중인 경우 아무 작업도 수행하지 않습니다.
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
		/// </summary>
		void Reset();
	}

	/// <inheritdoc cref="IStatechart"/>
	public class Statechart : IStatechart
	{
		private bool _isRunning;
		private IStateInternal _currentState;

		private readonly IStateFactoryInternal _stateFactory;

		/// <inheritdoc />
		public bool LogsEnabled { get; set; }

#if UNITY_EDITOR
		public string CurrentState => _currentState.Name;
#endif
		
		private Statechart() {}

		public Statechart(Action<IStateFactory> setup)
		{
			var stateFactory = new StateFactory(0, new StateFactoryData { Statechart = this, StateChartMoveNextCall = MoveNext });

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
		}

		private void MoveNext(IStatechartEvent trigger)
		{
			var nextState = _currentState.Trigger(trigger);
			while (nextState != null)
			{
				_currentState = nextState;
				nextState = _currentState.Trigger(null);
			}
		}
	}
}