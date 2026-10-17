using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine.Internal
{
	/// <inheritdoc cref="ITaskWaitState"/>
	internal class TaskWaitState : StateInternal, ITaskWaitState
	{
		private ITransitionInternal _transition;
		private ITransitionInternal _failedTransition;
		private Func<CancellationToken, UniTask> _taskAwaitAction;
		// 로그에 사용자가 넘긴 대리자를 보여 주기 위해 원래 대리자를 보관한다
		private Delegate _taskAwaitDelegate;
		// 실행 중인 태스크의 취소 소스. 태스크가 끝나면 null 로 둔다
		private CancellationTokenSource _cancellation;
		private bool _triggered;
		private bool _isStartingTask;
		private bool _failed;

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
		/// 실행 중인 태스크에 취소를 요청합니다. 태스크가 실제로 끝나기 전까지는 완료로 보지 않습니다
		/// </summary>
		public void CancelTask()
		{
			Cancel(TakeCancellation());
		}

		/// <summary>
		/// 실행 중인 태스크의 취소 소스를 이 상태에서 떼어 내 반환합니다. 실행 중이 아니면 null 입니다.
		/// 취소 콜백에서 상태가 다시 진입해 새 실행이 시작돼도, 떼어 낸 소스만 취소하도록 <see cref="Cancel"/> 과 나눠 씁니다
		/// </summary>
		public CancellationTokenSource TakeCancellation()
		{
			var cancellation = _cancellation;

			_cancellation = null;

			return cancellation;
		}

		/// <summary>
		/// <see cref="TakeCancellation"/> 으로 떼어 낸 실행에 취소를 요청합니다. 취소 콜백의 예외는 로그로 남깁니다
		/// </summary>
		public void Cancel(CancellationTokenSource cancellation)
		{
			if (cancellation == null || cancellation.IsCancellationRequested)
			{
				return;
			}

			try
			{
				cancellation.Cancel();
			}
			catch (Exception e)
			{
				Logger.Error(e, $"Exception in the state '{Name}', when canceling the task wait action " +
					$"'{_taskAwaitDelegate.Target}.{_taskAwaitDelegate.Method.Name}()'.\n" +
					$"-->> Check the exception log after this one for more details <<-- {CreationStackTraceMessage()}");
			}
		}

		/// <inheritdoc />
		public override void Enter()
		{
			_triggered = false;
			_failed = false;
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

			if (_failedTransition != null && _failedTransition.TargetState == null)
			{
				throw new InvalidOperationException($"The state {Name} failed transition is not pointing to any state");
			}

			if (_failedTransition?.TargetState?.Id == Id)
			{
				throw new InvalidOperationException($"The state {Name} is pointing to itself on failed transition");
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

			return SetTaskAwaitAction(_ => taskAwaitAction().AsUniTask(), taskAwaitAction);
		}

		/// <inheritdoc />
		public ITransition WaitingFor(Func<UniTask> taskAwaitAction)
		{
			if (taskAwaitAction == null)
			{
				throw new NullReferenceException($"The state {Name} cannot have a null wait action");
			}

			return SetTaskAwaitAction(_ => taskAwaitAction(), taskAwaitAction);
		}

		/// <inheritdoc />
		public ITransition WaitingFor(Func<CancellationToken, Task> taskAwaitAction)
		{
			if (taskAwaitAction == null)
			{
				throw new NullReferenceException($"The state {Name} cannot have a null wait action");
			}

			return SetTaskAwaitAction(token => taskAwaitAction(token).AsUniTask(), taskAwaitAction);
		}

		/// <inheritdoc />
		public ITransition WaitingFor(Func<CancellationToken, UniTask> taskAwaitAction)
		{
			if (taskAwaitAction == null)
			{
				throw new NullReferenceException($"The state {Name} cannot have a null wait action");
			}

			return SetTaskAwaitAction(taskAwaitAction, taskAwaitAction);
		}

		/// <inheritdoc />
		public ITransition Failed()
		{
			_failedTransition = new Transition();

			return _failedTransition;
		}

		/// <inheritdoc />
		protected override ITransitionInternal OnTrigger(IStatechartEvent statechartEvent)
		{
			if (!_triggered)
			{
				_triggered = true;
				_isStartingTask = true;
				InnerTaskAwait(statechartEvent?.Name).Forget();
				_isStartingTask = false;
			}

			if (!Completed)
			{
				return null;
			}

			return _failed && _failedTransition != null ? _failedTransition : _transition;
		}

		private ITransition SetTaskAwaitAction(Func<CancellationToken, UniTask> taskAwaitAction, Delegate userDelegate)
		{
			_taskAwaitAction = taskAwaitAction;
			_taskAwaitDelegate = userDelegate;
			_transition = new Transition();

			return _transition;
		}

		private async UniTaskVoid InnerTaskAwait(string eventName)
		{
			// 이 실행을 시작할 때의 리셋 세대. 그 뒤 Reset 되면 이 실행의 완료는 버린다(이전 실행이 새 대기를 덮어쓰지 않게)
			var resetVersion = _stateFactory.Data.ResetVersion();
			var cancellation = new CancellationTokenSource();
			var failed = false;

			_cancellation = cancellation;

			try
			{
				if (IsStateLogsEnabled)
				{
					Logger.Info($"TaskWait - '{eventName}' : '{_taskAwaitDelegate.Target}.{_taskAwaitDelegate.Method.Name}()' => '{Name}'");
				}

				//await Task.Yield(); 호출 생략
				await _taskAwaitAction(cancellation.Token);
			}
			catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
			{
				// 부모 상태가 퇴장하려 하거나 Reset 되어 이 상태가 요청한 취소이므로 실패로 보지 않는다
				if (IsStateLogsEnabled)
				{
					Logger.Info($"TaskWait - '{Name}' task canceled");
				}
			}
			catch (Exception e)
			{
				failed = true;

				Logger.Error(e, $"Exception in the state '{Name}', when calling the task wait action" +
					$"'{_taskAwaitDelegate.Target}.{_taskAwaitDelegate.Method.Name}()'.\n" +
					$"-->> Check the exception log after this one for more details <<-- {CreationStackTraceMessage()}");
			}
			finally
			{
				// 취소하려고 떼어 냈거나 Reset 뒤 다시 진입해 새 실행이 시작됐으면 그 소스는 건드리지 않는다.
				// 타이머나 연결 토큰이 없는 소스라 Dispose 하지 않는다. 취소 콜백 안에서 태스크가 바로 끝나는 경우나
				// 태스크가 끝난 뒤 사용자 코드가 토큰을 써도 해제된 소스를 건드리지 않게 하기 위해서다
				if (_cancellation == cancellation)
				{
					_cancellation = null;
				}
			}

			if (resetVersion != _stateFactory.Data.ResetVersion())
			{
				return;
			}

			_failed = failed;
			Completed = true;

			// 태스크가 동기적으로 완료되면 진행 중인 OnTrigger가 이어서 전이하므로 상태 차트를 다시 진행시키지 않습니다.
			// 이벤트 없이 진행을 요청한다. 부모가 퇴장을 미뤘으면 부모가 기억한 이벤트로 퇴장하고,
			// 이미 다음 상태로 넘어갔으면 그 상태에서 이벤트 없는 진행은 아무 일도 하지 않는다
			if (!_isStartingTask)
			{
				_stateFactory.Data.StateChartMoveNextCall(null);
			}
		}

		private string CreationStackTraceMessage()
		{
#if UNITY_EDITOR || DEBUG
			return $"\nStackTrace log of '{Name}' state creation bellow.\n{CreationStackTrace}";
#else
			return "";
#endif
		}
	}
}
