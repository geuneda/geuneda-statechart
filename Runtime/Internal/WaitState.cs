using System;
using System.Collections.Generic;

// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine.Internal
{
	/// <inheritdoc cref="IWaitState"/>
	internal class WaitState : StateInternal, IWaitState
	{
		private ITransitionInternal _transition;
		private IWaitActivityInternal _waitingActivity;
		private Action<IWaitActivity> _waitAction;
		private bool _triggered;
		private bool _isInvokingWaitAction;
		// 대기 액션을 시작할 때의 리셋 세대. 그 뒤 Reset 되면 늦게 오는 완료는 버려진 실행의 것이므로 무시한다
		private int _waitResetVersion;

		private readonly IList<Action> _onEnter = new List<Action>();
		private readonly IList<Action> _onExit = new List<Action>();
		private readonly Dictionary<IStatechartEvent, ITransitionInternal> _events = new Dictionary<IStatechartEvent, ITransitionInternal>();

		public WaitState(string name, IStateFactoryInternal factory) : base(name, factory)
		{
			_triggered = false;
		}

		/// <summary>
		/// 정상적인 실행 흐름을 우회하여 이 상태를 강제 완료합니다.
		/// 부모 중첩 상태가 이 상태를 제어하는 경우에 유용합니다.
		/// </summary>
		public void ForceComplete()
		{
			_waitingActivity.ForceComplete();
		}

		/// <inheritdoc />
		public override void Enter()
		{
			// 이전 활동이 새 활동을 덮어쓰지 않도록 매번 새 활동을 생성해야 합니다
			_waitingActivity = new WaitActivity(OnActivityComplete);
			_triggered = false;

			for (int i = 0; i < _onEnter.Count; i++)
			{
				_onEnter[i]?.Invoke();
			}
		}

		/// <inheritdoc />
		public override void Exit()
		{
			for (int i = 0; i < _onExit.Count; i++)
			{
				_onExit[i]?.Invoke();
			}
		}

		/// <inheritdoc />
		public override void Validate()
		{
#if UNITY_EDITOR || DEBUG
			if (_waitAction == null)
			{
				throw new InvalidOperationException($"The state {Name} doesn't have a waiting activity");
			}

			if (_transition?.TargetState == null)
			{
				throw new InvalidOperationException($"The state {Name} is not pointing to any state");
			}

			if (_transition.TargetState?.Id == Id)
			{
				throw new InvalidOperationException($"The state {Name} is pointing to itself on transition");
			}

			foreach (var eventTransition in _events)
			{
				if (eventTransition.Value.TargetState?.Id == Id)
				{
					throw new InvalidOperationException(
						$"The state {Name} with the event {eventTransition.Key.Name} is pointing to itself on transition");
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
		public ITransition WaitingFor(Action<IWaitActivity> waitAction)
		{
			_waitAction = waitAction ?? throw new NullReferenceException($"The state {Name} cannot have a null wait action");
			_transition = new Transition();

			return _transition;
		}

		private void OnActivityComplete(uint id)
		{
			// 대기 액션 안에서 동기적으로 완료되면 진행 중인 OnTrigger가 이어서 전이하므로 상태 차트를 다시 진행시키지 않습니다
			if (id == _waitingActivity.Id && !_isInvokingWaitAction &&
			    _waitResetVersion == _stateFactory.Data.ResetVersion())
			{
				_stateFactory.Data.StateChartMoveNextCall(null);
			}
		}

		/// <inheritdoc />
		protected override ITransitionInternal OnTrigger(IStatechartEvent statechartEvent)
		{
			if (statechartEvent != null && _events.TryGetValue(statechartEvent, out var transition))
			{
				if (transition.TargetState != null)
				{
					_waitingActivity.ForceComplete();
				}

				return transition;
			}

			if (!_triggered)
			{
				_triggered = true;
				_waitResetVersion = _stateFactory.Data.ResetVersion();
				_isInvokingWaitAction = true;
				InnerWait(statechartEvent?.Name);
				_isInvokingWaitAction = false;
			}

			return _waitingActivity.IsCompleted ? _transition : null;
		}

		private void InnerWait(string eventName)
		{
			try
			{
				if (IsStateLogsEnabled)
				{
					Logger.Info($"'{eventName}' event triggers the wait method '{_waitAction.Method.Name}'" +
							  $"from the object {_waitAction.Target} in the state {Name}");
				}
				_waitAction(_waitingActivity);
			}
			catch (Exception e)
			{
				var finalMessage = "";
#if UNITY_EDITOR || DEBUG
				finalMessage = $"\nStackTrace log of '{Name}' state creation bellow.\n{CreationStackTrace}";
#endif

				Logger.Error(e, $"Exception in the state '{Name}', when calling the wait action {_waitAction.Method.Name}" +
					$" from the object {_waitAction.Target}.\n" +
					$"-->> Check the exception log after this one for more details <<-- {finalMessage}");
			}
		}
	}
}
