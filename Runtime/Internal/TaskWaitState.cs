using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine.Internal
{
	/// <inheritdoc cref="ITaskWaitState"/>
	internal class TaskWaitState : StateInternal, ITaskWaitState
	{
		private ITransitionInternal _transition;
		private Func<UniTask> _taskAwaitAction;
		private IStatechartEvent _eventQueued;
		private bool _triggered;
		private bool _isStartingTask;

		private readonly IList<Action> _onEnter = new List<Action>();
		private readonly IList<Action> _onExit = new List<Action>();

		/// <summary>
		/// 이 태스크 대기 상태의 완료 상태를 요청합니다
		/// </summary>
		public bool Completed { get; private set; }

		public TaskWaitState(string name, IStateFactoryInternal factory) : base(name, factory)
		{
			_triggered = false;
			Completed = false;
		}

		/// <summary>
		/// 이 태스크가 아직 실행 중인 경우, 태스크 완료 후 실행할 첫 번째 명령을 큐에 넣습니다
		/// </summary>
		public void EnqueuEvent(IStatechartEvent statechartEvent)
		{
			_eventQueued ??= statechartEvent;
		}

		/// <inheritdoc />
		public override void Enter()
		{
			_eventQueued = null;
			_triggered = false;
			Completed = false;

			for (int i = 0; i < _onEnter.Count; i++)
			{
				_onEnter[i]?.Invoke();
			}
		}

		/// <inheritdoc />
		public override void Exit()
		{
			Completed = true;
			
			for(int i = 0; i < _onExit.Count; i++)
			{
				_onExit[i]?.Invoke();
			}
		}

		/// <inheritdoc />
		public override void Validate()
		{
#if UNITY_EDITOR || DEBUG
			if (_taskAwaitAction == null)
			{
				throw new InvalidOperationException($"The state {Name} doesn't have a task await action");
			}

			if (_transition?.TargetState == null)
			{
				throw new InvalidOperationException($"The state {Name} is not pointing to any state");
			}

			if (_transition.TargetState?.Id == Id)
			{
				throw new InvalidOperationException($"The state {Name} is pointing to itself on transition");
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
		public ITransition WaitingFor(Func<Task> taskAwaitAction)
		{
			if (taskAwaitAction == null)
			{
				throw new NullReferenceException($"The state {Name} cannot have a null wait action");
			}

			_taskAwaitAction = () => taskAwaitAction().AsUniTask();
			_transition = new Transition();

			return _transition;
		}

		/// <inheritdoc />
		public ITransition WaitingFor(Func<UniTask> taskAwaitAction)
		{
			_taskAwaitAction = taskAwaitAction ?? throw new NullReferenceException($"The state {Name} cannot have a null wait action");
			_transition = new Transition();

			return _transition;
		}

		/// <inheritdoc />
		protected override ITransitionInternal OnTrigger(IStatechartEvent statechartEvent)
		{
			if (!_triggered)
			{
				_triggered = true;
				_isStartingTask = true;
				_ = InnerTaskAwait(statechartEvent?.Name);
				_isStartingTask = false;
			}

			return Completed ? _transition : null;
		}

		private async UniTask InnerTaskAwait(string eventName)
		{
			// 이 실행을 시작할 때의 리셋 세대. 그 뒤 Reset 되면 이 실행의 완료는 버린다(이전 실행이 새 대기를 덮어쓰지 않게)
			var resetVersion = _stateFactory.Data.ResetVersion();

			try
			{
				if (IsStateLogsEnabled)
				{
					Debug.Log($"TaskWait - '{eventName}' : '{_taskAwaitAction.Target}.{_taskAwaitAction.Method.Name}()' => '{Name}'");
				}

				//await Task.Yield(); 호출 생략
				await _taskAwaitAction();

				if (resetVersion != _stateFactory.Data.ResetVersion())
				{
					return;
				}

				Completed = true;

				// 태스크가 동기적으로 완료되면 진행 중인 OnTrigger가 이어서 전이하므로 상태 차트를 다시 진행시키지 않습니다
				if (!_isStartingTask)
				{
					_stateFactory.Data.StateChartMoveNextCall(_eventQueued);
				}
			}
			catch (Exception e)
			{
				var finalMessage = "";
#if UNITY_EDITOR || DEBUG
				finalMessage = $"\nStackTrace log of '{Name}' state creation bellow.\n{CreationStackTrace}";
#endif

				Debug.LogError($"Exception in the state '{Name}', when calling the task wait action" +
					$"'{_taskAwaitAction.Target}.{_taskAwaitAction.Method.Name}()'.\n" +
					$"-->> Check the exception log after this one for more details <<-- {finalMessage}");
				Debug.LogException(e);
			}
		}
	}
}
