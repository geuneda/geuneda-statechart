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
	public class StatechartNestSplit_IntegrationTest
	{
		private readonly IStatechartEvent _event1 = new StatechartEvent("Event1");
		private readonly IStatechartEvent _event2 = new StatechartEvent("Event2");

		private IMockCaller _caller;
		private IWaitActivity _activity;
		private NestedStateData _nestedStateData;
		private bool _blocker;
		private bool _done;

		[SetUp]
		public void Init()
		{
			_caller = Substitute.For<IMockCaller>();
			_nestedStateData = new NestedStateData(factory => SetupWaitState(factory, waitActivity => _activity = waitActivity));
			_blocker = true;
			_done = false;
		}

		[Test]
		public void NestedState_WaitStateInner_EventTrigger_ForceCompleteSuccess()
		{
			var statechart = new Statechart(SetupNest);

			statechart.Run();
			statechart.Trigger(_event2);

			_caller.Received(2).OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.Received(1).OnTransitionCall(3);
			_caller.DidNotReceive().OnTransitionCall(4);
			_caller.Received(2).InitialOnExitCall(0);
			_caller.Received(1).StateOnEnterCall(0);
			_caller.Received(1).StateOnEnterCall(1);
			_caller.Received(1).StateOnExitCall(0);
			_caller.Received(1).StateOnExitCall(1);
			_caller.Received(2).FinalOnEnterCall(0);
		}

		[Test]
		public async Task NestedState_TaskWaitStateInner_EventTrigger_QueueEvent()
		{
			_nestedStateData.Setup = SetupTaskWaitState;

			var statechart = new Statechart(SetupNest);

			statechart.Run();
			statechart.Trigger(_event2);

			_caller.Received(2).OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.DidNotReceive().OnTransitionCall(3);
			_caller.Received(2).InitialOnExitCall(0);
			_caller.Received(1).StateOnEnterCall(0);
			_caller.Received(1).StateOnEnterCall(1);
			_caller.DidNotReceive().StateOnExitCall(0);
			_caller.DidNotReceive().StateOnExitCall(1);
			_caller.DidNotReceive().FinalOnEnterCall(0);

			_blocker = false;

			await YieldWaitTask();

			_caller.Received(2).OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.Received(1).OnTransitionCall(3);
			_caller.Received(2).InitialOnExitCall(0);
			_caller.Received(1).StateOnEnterCall(0);
			_caller.Received(1).StateOnEnterCall(1);
			_caller.Received(1).StateOnExitCall(0);
			_caller.Received(1).StateOnExitCall(1);
			_caller.Received(2).FinalOnEnterCall(0);
		}

		[Test]
		public void NestedState_TokenTaskWaitInner_EventTrigger_CancelsTaskAndLeaves()
		{
			var token = default(CancellationToken);
			var source = new UniTaskCompletionSource();
			_nestedStateData.Setup = factory => SetupUniTaskWaitState(factory, cancellationToken =>
			{
				token = cancellationToken;
				cancellationToken.Register(() => source.TrySetCanceled());

				return source.Task;
			});

			var statechart = new Statechart(SetupNest);

			statechart.Run();
			statechart.Trigger(_event2);

			Assert.IsTrue(token.IsCancellationRequested);
			_caller.Received(2).OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.Received(1).OnTransitionCall(3);
			_caller.Received(1).StateOnExitCall(0);
			_caller.Received(1).StateOnExitCall(1);
			_caller.Received(2).FinalOnEnterCall(0);
		}

		[Test]
		public void NestedState_TaskWaitInner_TaskThrowsWhileExitPending_Leaves()
		{
			var source = new UniTaskCompletionSource();
			_nestedStateData.Setup = factory => SetupUniTaskWaitState(factory, _ => source.Task);

			var statechart = new Statechart(SetupNest);

			statechart.Run();
			statechart.Trigger(_event2);

			_caller.DidNotReceive().OnTransitionCall(3);

			LogAssert.Expect(LogType.Error, new Regex("Exception in the state 'Task Wait'"));
			LogAssert.Expect(LogType.Exception, new Regex("boom"));
			source.TrySetException(new InvalidOperationException("boom"));

			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.Received(1).OnTransitionCall(3);
			_caller.Received(1).StateOnExitCall(0);
			_caller.Received(1).StateOnExitCall(1);
			_caller.Received(2).FinalOnEnterCall(0);
		}

		[Test]
		public void SplitState_TwoTokenTaskWaitsCanceledSynchronously_ExitEventHandledOnce()
		{
			var tokens = new List<CancellationToken>();
			var menuEnterCount = 0;
			var exitEnterCount = 0;
			var statechart = new Statechart(factory =>
			{
				var initial = factory.Initial("Initial");
				var split = factory.Split("Split");
				var menu = factory.State("Menu");
				var exit = factory.State("Exit");

				initial.Transition().Target(split);
				split.Split(inner => SetupBareTaskWait(inner, CancelableTask(tokens)),
					inner => SetupBareTaskWait(inner, CancelableTask(tokens))).Target(exit);
				split.Event(_event1).Target(menu);
				menu.OnEnter(() => menuEnterCount++);
				menu.Event(_event1).Target(exit);
				exit.OnEnter(() => exitEnterCount++);
			});

			statechart.Run();
			statechart.Trigger(_event1);

			Assert.AreEqual(2, tokens.Count);
			Assert.IsTrue(tokens[0].IsCancellationRequested);
			Assert.IsTrue(tokens[1].IsCancellationRequested);
			Assert.AreEqual(1, menuEnterCount);
			Assert.AreEqual(0, exitEnterCount);
		}

		[Test]
		public void SplitState_TokenTaskWaitsCanceledSynchronously_NextTargetlessNestCompletionRunsOnce()
		{
			var tokens = new List<CancellationToken>();
			var menuCompletedCount = 0;
			var statechart = new Statechart(factory =>
			{
				var initial = factory.Initial("Initial");
				var split = factory.Split("Split");
				var menu = factory.Nest("Menu");
				var final = factory.Final("Final");

				initial.Transition().Target(split);
				split.Split(inner => SetupBareTaskWait(inner, CancelableTask(tokens)),
					inner => SetupBareTaskWait(inner, CancelableTask(tokens))).Target(final);
				split.Event(_event1).Target(menu);
				menu.Nest(inner =>
				{
					var innerInitial = inner.Initial("Initial");
					var innerFinal = inner.Final("Final");

					innerInitial.Transition().Target(innerFinal);
				}).OnTransition(() => menuCompletedCount++);
			});

			statechart.Run();
			statechart.Trigger(_event1);

			Assert.IsTrue(tokens[0].IsCancellationRequested);
			Assert.IsTrue(tokens[1].IsCancellationRequested);
			Assert.AreEqual(1, menuCompletedCount);

			statechart.Run();

			Assert.AreEqual(1, menuCompletedCount);
		}

		[Test]
		public async Task NestedState_TokenTaskOverloadInner_EventTrigger_CancelsTaskAndLeavesOnce()
		{
			_nestedStateData.Setup = factory => SetupTokenTaskWaitState(factory, CancelableDelay);

			var statechart = new Statechart(SetupNest);

			statechart.Run();
			statechart.Trigger(_event2);

			await WaitUntilAsync(() => _done);
			await YieldFrames(5);

			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.Received(1).OnTransitionCall(3);
			_caller.Received(1).StateOnExitCall(0);
			_caller.Received(1).StateOnExitCall(1);
			_caller.Received(2).FinalOnEnterCall(0);
		}

		[Test]
		public async Task NestedState_TokenTaskOverloadInner_PausedWhileCanceling_RunResumesExit()
		{
			_nestedStateData.Setup = factory => SetupTokenTaskWaitState(factory, CancelableDelay);

			var statechart = new Statechart(SetupNest);

			statechart.Run();
			statechart.Trigger(_event2);
			statechart.Pause();

			await WaitUntilAsync(() => _done);
			await YieldFrames(5);

			_caller.DidNotReceive().OnTransitionCall(3);

			statechart.Run();

			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.Received(1).OnTransitionCall(3);
			_caller.Received(1).StateOnExitCall(1);
		}

		[Test]
		public void SplitState_FirstRegionTaskStillRunning_LaterNestedRegionTaskIsCanceled()
		{
			var source = new UniTaskCompletionSource();
			var tokens = new List<CancellationToken>();
			var menuEnterCount = 0;
			var statechart = new Statechart(factory =>
			{
				var initial = factory.Initial("Initial");
				var split = factory.Split("Split");
				var menu = factory.State("Menu");
				var final = factory.Final("Final");

				initial.Transition().Target(split);
				split.Split(inner => SetupBareTaskWait(inner, _ => source.Task), inner =>
				{
					var innerInitial = inner.Initial("Initial");
					var nest = inner.Nest("Nest");
					var innerFinal = inner.Final("Final");

					innerInitial.Transition().Target(nest);
					nest.Nest(nested => SetupBareTaskWait(nested, CancelableTask(tokens))).Target(innerFinal);
				}).Target(final);
				split.Event(_event1).Target(menu);
				menu.OnEnter(() => menuEnterCount++);
			});

			statechart.Run();
			statechart.Trigger(_event1);

			Assert.AreEqual(1, tokens.Count);
			Assert.IsTrue(tokens[0].IsCancellationRequested);
			Assert.AreEqual(0, menuEnterCount);

			source.TrySetResult();

			Assert.AreEqual(1, menuEnterCount);
		}

		[Test]
		public void NestedState_TargetlessEvent_DoesNotCancelInnerTask()
		{
			var source = new UniTaskCompletionSource();
			var tokens = new List<CancellationToken>();
			var statechart = new Statechart(factory =>
			{
				var nest = factory.Nest("Nest");
				var final = SetupSimpleFlow(factory, nest);

				nest.Nest(inner => SetupBareTaskWait(inner, token =>
				{
					tokens.Add(token);

					return source.Task;
				})).OnTransition(() => _caller.OnTransitionCall(2)).Target(final);
				nest.Event(_event2).OnTransition(() => _caller.OnTransitionCall(3));
			});

			statechart.Run();
			statechart.Trigger(_event2);

			_caller.Received(1).OnTransitionCall(3);
			Assert.IsFalse(tokens[0].IsCancellationRequested);
			_caller.DidNotReceive().OnTransitionCall(2);

			source.TrySetResult();

			_caller.Received(1).OnTransitionCall(2);
		}

		[Test]
		public void NestedState_TargetlessEvent_DoesNotForceCompleteInnerWait()
		{
			var statechart = new Statechart(factory =>
			{
				var nest = factory.Nest("Nest");
				var final = SetupSimpleFlow(factory, nest);

				nest.Nest(_nestedStateData).OnTransition(() => _caller.OnTransitionCall(2)).Target(final);
				nest.Event(_event2).OnTransition(() => _caller.OnTransitionCall(3));
			});

			statechart.Run();
			statechart.Trigger(_event2);

			_caller.Received(1).OnTransitionCall(3);
			_caller.DidNotReceive().OnTransitionCall(1);

			_activity.Complete();

			_caller.Received(1).OnTransitionCall(1);
			_caller.Received(1).OnTransitionCall(2);
		}

		[Test]
		public void SplitState_Reset_CancelCallbackRuns_OnlyRunsBeforeResetAreCanceled()
		{
			var tokensA = new List<CancellationToken>();
			var tokensB = new List<CancellationToken>();
			var runFromCallback = true;
			Statechart statechart = null;
			statechart = new Statechart(factory =>
			{
				var initial = factory.Initial("Initial");
				var split = factory.Split("Split");
				var final = factory.Final("Final");

				initial.Transition().Target(split);
				split.Split(inner => SetupBareTaskWait(inner, token =>
				{
					var source = new UniTaskCompletionSource();

					tokensA.Add(token);
					token.Register(() =>
					{
						source.TrySetCanceled();

						if (runFromCallback)
						{
							runFromCallback = false;
							statechart.Run();
						}
					});

					return source.Task;
				}), inner => SetupBareTaskWait(inner, token =>
				{
					tokensB.Add(token);

					return new UniTaskCompletionSource().Task;
				})).Target(final);
			});

			statechart.Run();
			statechart.Reset();

			Assert.AreEqual(2, tokensA.Count);
			Assert.AreEqual(2, tokensB.Count);
			Assert.IsTrue(tokensA[0].IsCancellationRequested);
			Assert.IsTrue(tokensB[0].IsCancellationRequested);
			Assert.IsFalse(tokensA[1].IsCancellationRequested);
			Assert.IsFalse(tokensB[1].IsCancellationRequested);
		}

		[Test]
		public void SplitState_DeferredExitResumes_OtherRegionDoesNotReceiveEventAgain()
		{
			var source = new UniTaskCompletionSource();
			var afterNestEnterCount = 0;
			var thirdStateEnterCount = 0;
			var statechart = new Statechart(factory =>
			{
				var initial = factory.Initial("Initial");
				var split = factory.Split("Split");
				var final = factory.Final("Final");

				initial.Transition().Target(split);
				split.Split(inner =>
				{
					var innerInitial = inner.Initial("Initial");
					var nest = inner.Nest("Nest");
					var afterNest = inner.State("After Nest");

					innerInitial.Transition().Target(nest);
					nest.Nest(nested => SetupBareTaskWait(nested, _ => source.Task)).Target(afterNest);
					nest.Event(_event1).Target(afterNest);
					afterNest.OnEnter(() => afterNestEnterCount++);
				}, inner =>
				{
					var innerInitial = inner.Initial("Initial");
					var first = inner.State("First");
					var second = inner.State("Second");
					var third = inner.State("Third");

					innerInitial.Transition().Target(first);
					first.Event(_event1).Target(second);
					second.Event(_event1).Target(third);
					third.OnEnter(() => thirdStateEnterCount++);
				}).Target(final);
			});

			statechart.Run();
			statechart.Trigger(_event1);

			Assert.AreEqual(0, afterNestEnterCount);

			source.TrySetResult();

			Assert.AreEqual(1, afterNestEnterCount);
			Assert.AreEqual(0, thirdStateEnterCount);
		}

		[Test]
		public void NestedState_TaskCompletesWhilePaused_RunResumesDeferredExit()
		{
			var source = new UniTaskCompletionSource();
			_nestedStateData.Setup = factory => SetupUniTaskWaitState(factory, _ => source.Task);

			var statechart = new Statechart(SetupNest);

			statechart.Run();
			statechart.Trigger(_event2);
			statechart.Pause();
			source.TrySetResult();

			_caller.DidNotReceive().OnTransitionCall(3);

			statechart.Run();

			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.Received(1).OnTransitionCall(3);
			_caller.Received(1).StateOnExitCall(1);
		}

		[Test]
		public void SplitState_LeaveWaitStateInner_EventTrigger_LeaveExitSuccess()
		{
			var statechart = new Statechart(SetupLeaveSplit);

			statechart.Run();
			statechart.Trigger(_event2);

			_caller.Received(3).OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.DidNotReceive().OnTransitionCall(3);
			_caller.DidNotReceive().OnTransitionCall(4);
			_caller.Received(1).OnTransitionCall(5);
			_caller.Received(3).InitialOnExitCall(0);
			_caller.Received(2).StateOnEnterCall(0);
			_caller.Received(1).StateOnEnterCall(1);
			_caller.Received(1).StateOnExitCall(0);
			_caller.Received(1).StateOnExitCall(1);
			_caller.Received(2).FinalOnEnterCall(0);
		}

		[Test]
		public async Task SplitState_LeaveTaskWaitStateInner_EventTrigger_QueueEvent_LeaveExitSuccess()
		{
			_nestedStateData.Setup = SetupTaskWaitState;

			var statechart = new Statechart(SetupLeaveSplit);

			statechart.Run();
			statechart.Trigger(_event2);

			_caller.Received(3).OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.DidNotReceive().OnTransitionCall(3);
			_caller.DidNotReceive().OnTransitionCall(5);
			_caller.Received(3).InitialOnExitCall(0);
			_caller.Received(2).StateOnEnterCall(0);
			_caller.Received(1).StateOnEnterCall(1);
			_caller.DidNotReceive().StateOnExitCall(0);
			_caller.DidNotReceive().StateOnExitCall(1);
			_caller.DidNotReceive().FinalOnEnterCall(0);

			_blocker = false;

			await YieldWaitTask();

			_caller.Received(3).OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.DidNotReceive().OnTransitionCall(3);
			_caller.Received(1).OnTransitionCall(5);
			_caller.Received(3).InitialOnExitCall(0);
			_caller.Received(2).StateOnEnterCall(0);
			_caller.Received(1).StateOnEnterCall(1);
			_caller.Received(1).StateOnExitCall(0);
			_caller.Received(1).StateOnExitCall(1);
			_caller.Received(2).FinalOnEnterCall(0);
		}

		[Test]
		public void SplitState_WaitStateInner_EventTrigger_ForceCompleteSuccess()
		{
			var statechart = new Statechart(SetupSplit);

			statechart.Run();
			statechart.Trigger(_event2);

			_caller.Received(3).OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.Received(1).OnTransitionCall(3);
			_caller.DidNotReceive().OnTransitionCall(4);
			_caller.Received(3).InitialOnExitCall(0);
			_caller.Received(2).StateOnEnterCall(0);
			_caller.Received(1).StateOnEnterCall(1);
			_caller.Received(2).StateOnExitCall(0);
			_caller.Received(1).StateOnExitCall(1);
			_caller.Received(3).FinalOnEnterCall(0);
		}

		[Test]
		public async Task SplitState_TaskWaitStateInner_EventTrigger_QueueEvent_CompleteSuccess()
		{
			_nestedStateData.Setup = SetupTaskWaitState;

			var statechart = new Statechart(SetupSplit);

			statechart.Run();
			statechart.Trigger(_event2);

			_caller.Received(3).OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.DidNotReceive().OnTransitionCall(3);
			_caller.Received(3).InitialOnExitCall(0);
			_caller.Received(2).StateOnEnterCall(0);
			_caller.Received(1).StateOnEnterCall(1);
			_caller.DidNotReceive().StateOnExitCall(0);
			_caller.DidNotReceive().StateOnExitCall(1);
			_caller.DidNotReceive().FinalOnEnterCall(0);

			_blocker = false;

			await YieldWaitTask();

			_caller.Received(3).OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.Received(1).OnTransitionCall(3);
			_caller.Received(3).InitialOnExitCall(0);
			_caller.Received(2).StateOnEnterCall(0);
			_caller.Received(1).StateOnEnterCall(1);
			_caller.Received(2).StateOnExitCall(0);
			_caller.Received(1).StateOnExitCall(1);
			_caller.Received(3).FinalOnEnterCall(0);
		}

		#region 설정

		private async Task TaskWaitAction()
		{
			while (_blocker)
			{
				await Task.Yield();
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

		private IFinalState SetupSimpleFlow(IStateFactory factory, IState state)
		{
			var initial = factory.Initial("Initial");
			var final = factory.Final("final");

			initial.Transition().OnTransition(() => _caller.OnTransitionCall(0)).Target(state);
			initial.OnExit(() => _caller.InitialOnExitCall(0));

			final.OnEnter(() => _caller.FinalOnEnterCall(0));

			return final;
		}

		private void SetupNest(IStateFactory factory)
		{
			var nest = factory.Nest("Nest");
			var final = SetupSimpleFlow(factory, nest);

			nest.OnEnter(() => _caller.StateOnEnterCall(1));
			nest.Nest(_nestedStateData).OnTransition(() => _caller.OnTransitionCall(2)).Target(final);
			nest.Event(_event2).OnTransition(() => _caller.OnTransitionCall(3)).Target(final);
			nest.OnExit(() => _caller.StateOnExitCall(1));
		}

		private void SetupSplit(IStateFactory factory)
		{
			var split = factory.Split("Split");
			var final = SetupSimpleFlow(factory, split);
			var nestedStateData = new NestedStateData(factory =>
			{
				var state = factory.State("State");

				SetupSimpleFlow(factory, state);

				state.OnEnter(() => _caller.StateOnEnterCall(0));
				state.OnExit(() => _caller.StateOnExitCall(0));
			});

			split.OnEnter(() => _caller.StateOnEnterCall(1));
			split.Split(_nestedStateData, nestedStateData).OnTransition(() => _caller.OnTransitionCall(2)).Target(final);
			split.Event(_event2).OnTransition(() => _caller.OnTransitionCall(3)).Target(final);
			split.OnExit(() => _caller.StateOnExitCall(1));
		}

		private void SetupLeaveSplit(IStateFactory factory)
		{
			var split = factory.Split("Split");
			var final = SetupSimpleFlow(factory, split);
			var nestedStateData = new NestedStateData(factory => SetupLeave(factory, final));

			split.OnEnter(() => _caller.StateOnEnterCall(1));
			split.Split(_nestedStateData, nestedStateData).OnTransition(() => _caller.OnTransitionCall(2)).Target(final);
			split.Event(_event2).OnTransition(() => _caller.OnTransitionCall(3)).Target(final);
			split.OnExit(() => _caller.StateOnExitCall(1));
		}

		private ILeaveState SetupLeave(IStateFactory factory, IState leaveTarget)
		{
			var leave = factory.Leave("Leave");
			var final = SetupSimpleFlow(factory, leave);

			leave.OnEnter(() => _caller.StateOnEnterCall(0));
			leave.Transition().OnTransition(() => _caller.OnTransitionCall(5)).Target(leaveTarget);

			return leave;
		}

		private void SetupWaitState(IStateFactory factory, Action<IWaitActivity> waitAction)
		{
			var waiting = factory.Wait("Wait");
			var final = SetupSimpleFlow(factory, waiting);

			waiting.OnEnter(() => _caller.StateOnEnterCall(0));
			waiting.WaitingFor(waitAction).OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
			waiting.Event(_event1).OnTransition(() => _caller.OnTransitionCall(4)).Target(final);
			waiting.OnExit(() => _caller.StateOnExitCall(0));
		}

		private void SetupTaskWaitState(IStateFactory factory)
		{
			var waiting = factory.TaskWait("Task Wait");
			var final = SetupSimpleFlow(factory, waiting);

			waiting.OnEnter(() => _caller.StateOnEnterCall(0));
			waiting.WaitingFor(TaskWaitAction).OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
			waiting.OnExit(() => _caller.StateOnExitCall(0));
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

		private static async Task WaitUntilAsync(Func<bool> condition)
		{
			// 조건이 끝내 충족되지 않아도 테스트가 무한 대기하지 않도록 횟수를 제한한다
			for (var i = 0; i < 300 && !condition(); i++)
			{
				await Task.Yield();
			}

			Assert.IsTrue(condition(), "Condition was not met in time");
		}

		private static async Task YieldFrames(int count)
		{
			for (var i = 0; i < count; i++)
			{
				await Task.Yield();
			}
		}

		private void SetupTokenTaskWaitState(IStateFactory factory, Func<CancellationToken, Task> taskAwaitAction)
		{
			var waiting = factory.TaskWait("Task Wait");
			var final = SetupSimpleFlow(factory, waiting);

			waiting.OnEnter(() => _caller.StateOnEnterCall(0));
			waiting.WaitingFor(taskAwaitAction).OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
			waiting.OnExit(() => _caller.StateOnExitCall(0));
		}

		private static Func<CancellationToken, UniTask> CancelableTask(List<CancellationToken> tokens)
		{
			return token =>
			{
				var source = new UniTaskCompletionSource();

				tokens.Add(token);
				token.Register(() => source.TrySetCanceled());

				return source.Task;
			};
		}

		private static void SetupBareTaskWait(IStateFactory factory, Func<CancellationToken, UniTask> taskAwaitAction)
		{
			var initial = factory.Initial("Initial");
			var waiting = factory.TaskWait("Task Wait");
			var final = factory.Final("Final");

			initial.Transition().Target(waiting);
			waiting.WaitingFor(taskAwaitAction).Target(final);
		}

		private void SetupUniTaskWaitState(IStateFactory factory, Func<CancellationToken, UniTask> taskAwaitAction)
		{
			var waiting = factory.TaskWait("Task Wait");
			var final = SetupSimpleFlow(factory, waiting);

			waiting.OnEnter(() => _caller.StateOnEnterCall(0));
			waiting.WaitingFor(taskAwaitAction).OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
			waiting.OnExit(() => _caller.StateOnExitCall(0));
		}

		#endregion
	}
}
