using System;
using System.Collections.Generic;

// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine.Internal
{
	/// <inheritdoc cref="ISplitState"/>
	internal class SplitState : StateInternal, ISplitState
	{
		protected readonly IList<Action> _onEnter = new List<Action>();
		protected readonly IList<Action> _onExit = new List<Action>();
		protected readonly IDictionary<IStatechartEvent, ITransitionInternal> _events = new Dictionary<IStatechartEvent, ITransitionInternal>();
		protected readonly IList<InnerStateData> _innerStatesData = new List<InnerStateData>();

		private bool _isPaused;
		// 퇴장을 미룬 이벤트. Leave 로 퇴장을 미뤘으면 null 이다
		private IStatechartEvent _pausedEvent;
		// 대상 없는 완료 전이를 이번 진입에서 이미 실행했는지. 대상이 없으면 이 상태에 머무르므로 이후 이벤트 없는 진행에서 다시 실행하지 않게 한다
		private bool _isTargetlessCompletionDone;
		private ITransitionInternal _transition;

		public SplitState(string name, IStateFactoryInternal factory) : base(name, factory)
		{
		}

		/// <inheritdoc />
		public override void Enter()
		{
			_isPaused = false;
			_pausedEvent = null;
			_isTargetlessCompletionDone = false;

			for (var i = 0; i < _innerStatesData.Count; i++)
			{
				var innerState = _innerStatesData[i];

				innerState.CurrenState = innerState.InitialState;

				_innerStatesData[i] = innerState;
			}

			foreach (var action in _onEnter)
			{
				action();
			}
		}

		/// <inheritdoc />
		public override void Exit()
		{
			_isPaused = false;

			// 영역을 종료하는 도중 상태 차트가 Reset 되면 남은 영역의 종료와 Final 진입, 이 상태의 퇴장 액션을 실행하지 않는다
			var resetVersion = _stateFactory.Data.ResetVersion();

			for (var i = 0; i < _innerStatesData.Count; i++)
			{
				var innerState = _innerStatesData[i];

				if (innerState.ExecuteExit)
				{
					innerState.CurrenState.Exit();

					if (resetVersion != _stateFactory.Data.ResetVersion())
					{
						return;
					}
				}

				if (innerState.ExecuteFinal && !(innerState.CurrenState is FinalState) && !(innerState.CurrenState is LeaveState))
				{
					innerState.NestedFactory.FinalState?.Enter();

					if (resetVersion != _stateFactory.Data.ResetVersion())
					{
						return;
					}
				}
			}
			
			foreach (var action in _onExit)
			{
				action();
			}
		}

		/// <inheritdoc />
		public override void Validate()
		{
#if UNITY_EDITOR || DEBUG
			if (_innerStatesData.Count < 2)
			{
				throw new InvalidOperationException($"Split state {Name} doesn't have enough nested setup defined." +
				                                 $"It needs min 2 nested states to be a proper {nameof(ISplitState)}");
			}
#endif
			OnValidate();
		}

		protected void OnValidate()
		{
#if UNITY_EDITOR || DEBUG
			if (_innerStatesData.Count == 0)
			{
				throw new InvalidOperationException($"This state {Name} doesn't have the nested setup defined correctly");
			}
			
			for (var i = 0; i < _innerStatesData.Count; i++)
			{
				if (_innerStatesData[i].InitialState == null)
				{
					throw new InvalidOperationException($"The state {Name} has a nested setup without an initial state");
				}

				foreach(var state in _innerStatesData[i].NestedFactory.States)
				{
					state.Validate();
				}
			}

			if (_transition.TargetState?.Id == Id)
			{
				throw new InvalidOperationException($"The state {Name} is pointing to itself on transition");
			}

			foreach (var eventTransition in _events)
			{
				if (eventTransition.Value.TargetState?.Id == Id)
				{
					throw new InvalidOperationException($"The state {Name} with the event {eventTransition.Key.Name} is pointing to itself on transition");
				}
			}
#endif
		}

		/// <inheritdoc />
		public void OnEnter(Action action)
		{
			if (action == null)
			{
				throw new NullReferenceException($"The state {Name} cannot have a null OnEnter action");
			}

			_onEnter.Add(action);
		}

		/// <inheritdoc />
		public void OnExit(Action action)
		{
			if (action == null)
			{
				throw new NullReferenceException($"The state {Name} cannot have a null OnExit action");
			}

			_onExit.Add(action);
		}

		/// <inheritdoc />
		public ITransition Event(IStatechartEvent statechartEvent)
		{
			if (statechartEvent == null)
			{
				throw new NullReferenceException($"The state {Name} cannot have a null event");
			}

			var transition = new Transition();

			_events.Add(statechartEvent, transition);

			return transition;
		}

		/// <inheritdoc />
		public ITransition Split(params Action<IStateFactory>[] data)
		{
			var array = new NestedStateData[data.Length];

			for (var i = 0; i < array.Length; i++)
			{
				array[i] = data[i];
			}

			return Split(array);
		}

		/// <inheritdoc />
		public ITransition Split(params NestedStateData[] data)
		{
			if (_transition != null)
			{
				throw new InvalidOperationException($"State {Name} is nesting multiple times");
			}

			foreach (var stateData in data)
			{
				var factory = new StateFactory(_stateFactory.RegionLayer + 1, _stateFactory.Data);
				
				stateData.Setup(factory);
				
				_stateFactory.Add(factory.States);
				_innerStatesData.Add(new InnerStateData
				{
					InitialState = factory.InitialState,
					CurrenState = null,
					NestedFactory = factory,
					ExecuteExit = stateData.ExecuteExit,
					ExecuteFinal = stateData.ExecuteFinal
				});
			}
			
			_transition = new Transition();

			return _transition;
		}

		/// <inheritdoc />
		protected override ITransitionInternal OnTrigger(IStatechartEvent statechartEvent)
		{
			if (_isPaused)
			{
				if (!IsAllCompleted())
				{
					return null;
				}

				// 내부 태스크가 모두 끝났으므로 미뤄 둔 퇴장을 이어 간다. 이 요청의 이벤트가 아니라 퇴장을 미룬 이벤트로 처리하므로
				// 태스크 완료 요청이 여러 번 오거나 일시 정지로 요청이 버려진 뒤 Run 해도 퇴장은 한 번만, 원래 이벤트로 일어난다
				_isPaused = false;

				return _pausedEvent != null && _events.TryGetValue(_pausedEvent, out var pausedTransition)
					? pausedTransition
					: ProcessInnerStates(null, true);
			}

			if (statechartEvent != null && _events.TryGetValue(statechartEvent, out var transition))
			{
				// 이 상태를 떠나는 이벤트면 실행 중인 태스크가 끝날 때까지 퇴장을 미룬다.
				// 대상 없는 이벤트는 상태를 떠나지 않으므로 내부 대기를 건드리지 않고 바로 처리한다
				if (transition.TargetState != null && DelayForceComplete())
				{
					_pausedEvent = statechartEvent;

					return null;
				}

				return transition;
			}

			return ProcessInnerStates(statechartEvent, false);
		}

		/// <summary>
		/// 이 상태가 떠나기 전에 하위 영역의 대기 작업을 정리하고, 아직 끝나지 않은 태스크가 있으면 퇴장을 미루도록 일시 정지합니다.
		/// 내부 태스크가 실행 중이라 일시 정지한 경우 true를 반환합니다.
		/// </summary>
		private bool DelayForceComplete()
		{
			_isPaused = StopInnerWaits();

			return _isPaused;
		}

		/// <summary>
		/// 모든 하위 영역(중첩 영역 포함)의 현재 상태가 <see cref="WaitState"/>면 강제 완료하고,
		/// 실행 중인 <see cref="TaskWaitState"/>면 태스크에 취소를 요청합니다.
		/// 취소 요청 뒤에도 끝나지 않은 태스크가 있으면 true를 반환합니다.
		/// </summary>
		private bool StopInnerWaits()
		{
			var hasRunningTask = false;

			for (var i = 0; i < _innerStatesData.Count; i++)
			{
				var currenState = _innerStatesData[i].CurrenState;

				if(currenState is WaitState waitState)
				{
					waitState.ForceComplete();
				}
				else if(currenState is TaskWaitState taskState && !taskState.Completed)
				{
					// 취소 콜백 안에서 태스크가 바로 끝날 수 있으므로 취소 뒤에 완료 여부를 다시 본다
					taskState.CancelTask();
					hasRunningTask = hasRunningTask || !taskState.Completed;
				}
				else if(currenState is SplitState splitState)
				{
					// 앞 영역의 결과와 관계없이 모든 중첩 영역을 방문해야 뒤쪽 태스크에도 취소가 전달된다
					var innerHasRunningTask = splitState.StopInnerWaits();
					hasRunningTask = hasRunningTask || innerHasRunningTask;
				}
			}

			return hasRunningTask;
		}
		
		/// <summary>
		/// 대기 중인 모든 내부 상태가 이미 완료되었는지 확인합니다
		/// </summary>
		internal bool IsAllCompleted()
		{
			for (var i = 0; i < _innerStatesData.Count; i++)
			{
				var currenState = _innerStatesData[i].CurrenState;

				if (currenState is TaskWaitState taskState && !taskState.Completed)
				{
					return false;
				}
				else if (currenState is SplitState splitState && !splitState.IsAllCompleted())
				{
					return false;
				}
			}

			return true;
		}

		private ITransitionInternal ProcessInnerStates(IStatechartEvent statechartEvent, bool isUnpausing)
		{
			var nextTransition = _transition;
			var leaveState = (LeaveState) null;
			var resetVersion = _stateFactory.Data.ResetVersion();

			for (var i = 0; i < _innerStatesData.Count; i++)
			{
				var innerState = _innerStatesData[i];

				if(!isUnpausing)
				{
					var nextState = innerState.CurrenState.Trigger(statechartEvent);

					while (nextState != null)
					{
						innerState.CurrenState = nextState;
						nextState = innerState.CurrenState.Trigger(null);
					}

					// 영역 처리 중 상태 차트가 Reset 되면 다른 영역과 이 상태의 완료 전이를 진행하지 않는다
					if (resetVersion != _stateFactory.Data.ResetVersion())
					{
						return null;
					}
				}

				if (innerState.CurrenState is LeaveState state)
				{
					leaveState = state;
				}
				else if (innerState.CurrenState is not FinalState)
				{
					nextTransition = null;
				}

				_innerStatesData[i] = innerState;
			}

			if(leaveState != null)
			{
				// 일부 작업이 실행 중인 동안 이 상태의 완료를 지연시킴
				if (!isUnpausing && DelayForceComplete())
				{
					_pausedEvent = null;
					nextTransition = null;
				}
				else
				{
					nextTransition = leaveState.LeaveTransition;
				}
			}
			else if (nextTransition != null && nextTransition.TargetState == null)
			{
				if (_isTargetlessCompletionDone)
				{
					return null;
				}

				_isTargetlessCompletionDone = true;
			}

			return nextTransition;
		}
	}
}