using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Geuneda.StatechartMachine;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable CheckNamespace

namespace GeunedaEditor.StatechartMachine.Tests
{
	[TestFixture]
	public class StatechartTaskWaitTest
	{
		private readonly IStatechartEvent _event = new StatechartEvent("Event");

		private IMockCaller _caller;
		private bool _blocker;
		private bool _done;
		
		[SetUp]
		public void Init()
		{
			_caller = Substitute.For<IMockCaller>();
			_blocker = true;
			_done = false;
		}

		[Test]
		public async Task SimpleTest()
		{
			var statechart = new Statechart(SetupTaskWaitState);

			statechart.Run();

			_caller.Received().OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.Received().InitialOnExitCall(0);
			_caller.Received().StateOnEnterCall(0);
			_caller.DidNotReceive().StateOnExitCall(0);
			_caller.DidNotReceive().FinalOnEnterCall(0);

			_blocker = false;

			await YieldWaitTask();

			_caller.Received().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.Received().StateOnExitCall(0);
			_caller.Received().FinalOnEnterCall(0);
		}

		[Test]
		public async Task TaskWait_EventTrigger_DoesNothing()
		{
			var statechart = new Statechart(SetupTaskWaitState);

			statechart.Run();

			_caller.Received().OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.Received().InitialOnExitCall(0);
			_caller.Received().StateOnEnterCall(0);
			_caller.DidNotReceive().StateOnExitCall(0);
			_caller.DidNotReceive().FinalOnEnterCall(0);

			statechart.Trigger(_event);

			_blocker = false;

			await YieldWaitTask();

			_caller.Received().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.Received().StateOnExitCall(0);
			_caller.Received().FinalOnEnterCall(0);
		}

		[Test]
		public async Task UniTaskWait_EventTrigger_DoesNothing()
		{
			var statechart = new Statechart(SetupUniTaskWaitState);

			statechart.Run();

			_caller.Received().OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.Received().InitialOnExitCall(0);
			_caller.Received().StateOnEnterCall(0);
			_caller.DidNotReceive().StateOnExitCall(0);
			_caller.DidNotReceive().FinalOnEnterCall(0);

			statechart.Trigger(_event);

			_blocker = false;

			await YieldWaitUniTask();

			_caller.Received().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.Received().StateOnExitCall(0);
			_caller.Received().FinalOnEnterCall(0);
		}

		[Test]
		public void UniTaskWait_CompleteSynchronously_TransitionsOnce()
		{
			var statechart = new Statechart(factory =>
			{
				var waiting = factory.TaskWait("Task Wait");
				var final = SetupSimpleFlow(factory, waiting);

				waiting.OnEnter(() => _caller.StateOnEnterCall(0));
				waiting.WaitingFor(() => UniTask.CompletedTask).OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
				waiting.OnExit(() => _caller.StateOnExitCall(0));
			});

			statechart.Run();

			_caller.Received(1).OnTransitionCall(0);
			_caller.Received(1).InitialOnExitCall(0);
			_caller.Received(1).StateOnEnterCall(0);
			_caller.Received(1).OnTransitionCall(1);
			_caller.Received(1).StateOnExitCall(0);
			_caller.Received(1).FinalOnEnterCall(0);
		}

		[Test]
		public void TaskWait_MissingConfiguration_ThrowsException()
		{
			Assert.Throws<InvalidOperationException>(() => new Statechart(factory =>
			{
				var waiting = factory.TaskWait("Task Wait");
				var final = SetupSimpleFlow(factory, waiting);
			}));
		}

		[Test]
		public void TaskWait_MissingTarget_ThrowsException()
		{
			Assert.Throws<InvalidOperationException>(() => new Statechart(factory =>
			{
				var waiting = factory.TaskWait("Task Wait");
				var final = SetupSimpleFlow(factory, waiting);

				waiting.WaitingFor(TaskWaitAction).OnTransition(() => _caller.OnTransitionCall(1));
			}));
		}

		[Test]
		public void TaskWait_TransitionsLoop_ThrowsException()
		{
			Assert.Throws<InvalidOperationException>(() => new Statechart(factory =>
			{
				var waiting = factory.TaskWait("Task Wait");
				var final = SetupSimpleFlow(factory, waiting);

				waiting.WaitingFor(TaskWaitAction).OnTransition(() => _caller.OnTransitionCall(1)).Target(waiting);
			}));
		}

		[Test]
		public void UniTaskWait_TaskThrows_WithoutFailedTransition_TakesWaitingForTransition()
		{
			var source = new UniTaskCompletionSource();
			var statechart = new Statechart(factory =>
			{
				var waiting = factory.TaskWait("Task Wait");
				var final = SetupSimpleFlow(factory, waiting);

				waiting.WaitingFor(() => source.Task).OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
			});

			statechart.Run();

			_caller.DidNotReceive().OnTransitionCall(1);

			ExpectTaskWaitExceptionLog();
			source.TrySetException(new InvalidOperationException("boom"));

			_caller.Received(1).OnTransitionCall(1);
			_caller.Received(1).FinalOnEnterCall(0);
		}

		[Test]
		public void UniTaskWait_TaskThrows_WithFailedTransition_TakesFailedTransition()
		{
			var source = new UniTaskCompletionSource();
			var statechart = new Statechart(factory => SetupFailedFlow(factory, waiting => waiting.WaitingFor(() => source.Task)));

			statechart.Run();

			ExpectTaskWaitExceptionLog();
			source.TrySetException(new InvalidOperationException("boom"));

			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.Received(1).OnTransitionCall(2);
			_caller.Received(1).StateOnExitCall(0);
			_caller.Received(1).StateOnEnterCall(1);
			_caller.DidNotReceive().FinalOnEnterCall(0);
		}

		[Test]
		public void UniTaskWait_ThrowsSynchronously_WithFailedTransition_TransitionsOnce()
		{
			var statechart = new Statechart(factory => SetupFailedFlow(factory,
				waiting => waiting.WaitingFor(() => UniTask.FromException(new InvalidOperationException("boom")))));

			ExpectTaskWaitExceptionLog();
			statechart.Run();

			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.Received(1).OnTransitionCall(2);
			_caller.Received(1).StateOnExitCall(0);
			_caller.Received(1).StateOnEnterCall(1);
		}

		[Test]
		public async Task TaskWait_TaskThrows_WithFailedTransition_TakesFailedTransition()
		{
			var statechart = new Statechart(factory => SetupFailedFlow(factory, waiting => waiting.WaitingFor(ThrowingTaskWaitAction)));

			statechart.Run();

			ExpectTaskWaitExceptionLog();
			_blocker = false;

			await YieldWaitTask();

			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.Received(1).OnTransitionCall(2);
			_caller.Received(1).StateOnEnterCall(1);
		}

		[Test]
		public void TaskWait_FailedMissingTarget_ThrowsException()
		{
			Assert.Throws<InvalidOperationException>(() => new Statechart(factory =>
			{
				var waiting = factory.TaskWait("Task Wait");
				var final = SetupSimpleFlow(factory, waiting);

				waiting.WaitingFor(TaskWaitAction).Target(final);
				waiting.Failed().OnTransition(() => _caller.OnTransitionCall(2));
			}));
		}

		[Test]
		public void TaskWait_FailedTransitionLoop_ThrowsException()
		{
			Assert.Throws<InvalidOperationException>(() => new Statechart(factory =>
			{
				var waiting = factory.TaskWait("Task Wait");
				var final = SetupSimpleFlow(factory, waiting);

				waiting.WaitingFor(TaskWaitAction).Target(final);
				waiting.Failed().Target(waiting);
			}));
		}

		[Test]
		public void UniTaskWait_Reset_CancelsTokenAndIgnoresCompletion()
		{
			var tokens = new List<CancellationToken>();
			var source = new UniTaskCompletionSource();
			var statechart = new Statechart(factory =>
			{
				var waiting = factory.TaskWait("Task Wait");
				var final = SetupSimpleFlow(factory, waiting);

				waiting.WaitingFor(token =>
				{
					tokens.Add(token);
					token.Register(() => source.TrySetCanceled());

					return source.Task;
				}).OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
			});

			statechart.Run();
			statechart.Reset();

			Assert.AreEqual(1, tokens.Count);
			Assert.IsTrue(tokens[0].IsCancellationRequested);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().FinalOnEnterCall(0);

			source = new UniTaskCompletionSource();
			statechart.Run();

			Assert.AreEqual(2, tokens.Count);
			Assert.IsFalse(tokens[1].IsCancellationRequested);

			source.TrySetResult();

			_caller.Received(1).OnTransitionCall(1);
			_caller.Received(1).FinalOnEnterCall(0);
		}

		[Test]
		public async Task TaskWait_TokenTaskOverload_ResetThenRun_LateCancellationDoesNotAdvanceNewRun()
		{
			var tokens = new List<CancellationToken>();
			var statechart = new Statechart(factory =>
			{
				var waiting = factory.TaskWait("Task Wait");
				var final = SetupSimpleFlow(factory, waiting);

				waiting.WaitingFor(token =>
				{
					tokens.Add(token);

					return CancelableDelay(token);
				}).OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
			});

			statechart.Run();
			statechart.Reset();
			statechart.Run();

			// 리셋 전 실행의 취소 완료는 동기화 컨텍스트를 거쳐 늦게 도착한다
			for (var i = 0; i < 300 && !_done; i++)
			{
				await Task.Yield();
			}

			for (var i = 0; i < 5; i++)
			{
				await Task.Yield();
			}

			Assert.IsTrue(_done);
			Assert.AreEqual(2, tokens.Count);
			Assert.IsTrue(tokens[0].IsCancellationRequested);
			Assert.IsFalse(tokens[1].IsCancellationRequested);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().FinalOnEnterCall(0);

			// 남은 실행이 테스트 뒤에도 대기하지 않도록 취소한다
			statechart.Reset();
		}

		private async Task CancelableDelay(CancellationToken token)
		{
			try
			{
				await Task.Delay(Timeout.Infinite, token);
			}
			finally
			{
				_done = true;
			}
		}

		private static void ExpectTaskWaitExceptionLog()
		{
			LogAssert.Expect(LogType.Error, new Regex("Exception in the state 'Task Wait'"));
			LogAssert.Expect(LogType.Exception, new Regex("boom"));
		}

		private void SetupFailedFlow(IStateFactory factory, Func<ITaskWaitState, ITransition> setWaitingFor)
		{
			var waiting = factory.TaskWait("Task Wait");
			var failedState = factory.State("Failed State");
			var final = SetupSimpleFlow(factory, waiting);

			setWaitingFor(waiting).OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
			waiting.Failed().OnTransition(() => _caller.OnTransitionCall(2)).Target(failedState);
			waiting.OnExit(() => _caller.StateOnExitCall(0));
			failedState.OnEnter(() => _caller.StateOnEnterCall(1));
		}

		private async Task ThrowingTaskWaitAction()
		{
			while (_blocker)
			{
				await Task.Yield();
			}

			_done = true;

			throw new InvalidOperationException("boom");
		}

		private async Task TaskWaitAction()
		{
			while (_blocker)
			{
				await Task.Yield();
			}

			_done = true;
		}

		private async UniTask UniTaskWaitAction()
		{
			while (_blocker)
			{
				await UniTask.Yield();
			}

			_done = true;
		}

		private async Task YieldWaitTask()
		{
			while (!_done)
			{
				await Task.Yield();
			}

			await Task.Yield();
		}

		private async UniTask YieldWaitUniTask()
		{
			while (!_done)
			{
				await UniTask.Yield();
			}

			await UniTask.Yield();
		}

		private IFinalState SetupSimpleFlow(IStateFactory factory, IState state)
		{
			var initial = factory.Initial("Initial");
			var final = factory.Final("final");

			initial.Transition().OnTransition(() => _caller.OnTransitionCall(0)).Target(state);
			initial.OnExit(() => _caller.InitialOnExitCall(0));

			final.OnEnter(() => _caller.FinalOnEnterCall(0));

			return final;
		}

		private void SetupTaskWaitState(IStateFactory factory)
		{
			var waiting = factory.TaskWait("Task Wait");
			var final = SetupSimpleFlow(factory, waiting);

			waiting.OnEnter(() => _caller.StateOnEnterCall(0));
			waiting.WaitingFor(TaskWaitAction).OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
			waiting.OnExit(() => _caller.StateOnExitCall(0));
		}

		private void SetupUniTaskWaitState(IStateFactory factory)
		{
			var waiting = factory.TaskWait("Task Wait");
			var final = SetupSimpleFlow(factory, waiting);

			waiting.OnEnter(() => _caller.StateOnEnterCall(0));
			waiting.WaitingFor(UniTaskWaitAction).OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
			waiting.OnExit(() => _caller.StateOnExitCall(0));
		}
	}
}