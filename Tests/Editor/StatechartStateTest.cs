using System;
using Cysharp.Threading.Tasks;
using Geuneda.StatechartMachine;
using NSubstitute;
using NUnit.Framework;

// ReSharper disable CheckNamespace

namespace GeunedaEditor.StatechartMachine.Tests
{
	[TestFixture]
	public class StatechartStateTest
	{
		private IMockCaller _caller;
		private readonly IStatechartEvent _event = new StatechartEvent("Event");
		
		[SetUp]
		public void Init()
		{
			_caller = Substitute.For<IMockCaller>();
		}

		[Test]
		public void SimpleTest()
		{
			var statechart = new Statechart(SetupStateFlow);

			statechart.Run();

			_caller.Received().OnTransitionCall(0);
			_caller.Received().InitialOnExitCall(0);
			_caller.Received().StateOnEnterCall(1);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().StateOnExitCall(1);
			_caller.DidNotReceive().FinalOnEnterCall(0);

			statechart.Trigger(_event);

			_caller.Received().OnTransitionCall(1);
			_caller.Received().StateOnExitCall(1);
			_caller.Received().FinalOnEnterCall(0);
		}

		[Test]
		public void State_TransitionWithoutTarget_Succeeds()
		{
			var statechart = new Statechart(factory =>
			{
				var state = factory.State("State");
				var final = SetupSimpleFlow(factory, state);

				state.OnEnter(() => _caller.StateOnEnterCall(1));
				state.Event(_event).OnTransition(() => _caller.OnTransitionCall(1));
				state.OnExit(() => _caller.StateOnExitCall(1));
			});

			statechart.Run();

			_caller.Received().OnTransitionCall(0);
			_caller.Received().InitialOnExitCall(0);
			_caller.Received().StateOnEnterCall(1);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().StateOnExitCall(1);
			_caller.DidNotReceive().FinalOnEnterCall(0);

			statechart.Trigger(_event);

			_caller.Received().OnTransitionCall(1);
			_caller.DidNotReceive().StateOnExitCall(1);
			_caller.DidNotReceive().FinalOnEnterCall(0);
		}

		[Test]
		public void State_TriggerNotConfiguredEvent_NoEffect()
		{
			var statechart = new Statechart(SetupStateFlow);
			var event2 = new StatechartEvent("Event2");

			statechart.Run();
			statechart.Trigger(event2);

			_caller.Received().OnTransitionCall(0);
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().OnTransitionCall(2);
			_caller.Received().InitialOnExitCall(0);
			_caller.Received().StateOnEnterCall(1);
			_caller.DidNotReceive().StateOnEnterCall(2);
			_caller.DidNotReceive().StateOnExitCall(1);
			_caller.DidNotReceive().StateOnExitCall(2);
			_caller.DidNotReceive().FinalOnEnterCall(0);
		}

		[Test]
		public void State_PauseRunStatechart_Success()
		{
			var statechart = new Statechart(SetupStateFlow);

			statechart.Run();
			statechart.Pause();
			statechart.Run();
			statechart.Trigger(_event);

			_caller.Received().OnTransitionCall(0);
			_caller.Received().OnTransitionCall(1);
			_caller.Received().InitialOnExitCall(0);
			_caller.Received().StateOnEnterCall(1);
			_caller.Received().StateOnExitCall(1);
			_caller.Received().FinalOnEnterCall(0);
		}

		[Test]
		public void State_ResetRunStatechart_Success()
		{
			var statechart = new Statechart(SetupStateFlow);

			statechart.Run();
			statechart.Reset();
			statechart.Run();
			statechart.Trigger(_event);

			_caller.Received(2).OnTransitionCall(0);
			_caller.Received(1).OnTransitionCall(1);
			_caller.Received(2).InitialOnExitCall(0);
			_caller.Received(2).StateOnEnterCall(1);
			_caller.Received(1).StateOnExitCall(1);
			_caller.Received(1).FinalOnEnterCall(0);
		}

		[Test]
		public void StateTransitionsLoop_ThrowsException()
		{
			Assert.Throws<InvalidOperationException>(() => new Statechart(factory =>
			{
				var state = factory.State("State");
				
				SetupSimpleFlow(factory, state);

				state.OnEnter(() => _caller.StateOnEnterCall(1));
				state.Event(_event).OnTransition(() => _caller.OnTransitionCall(1)).Target(state);
				state.OnExit(() => _caller.StateOnExitCall(1));
			}));
		}

		[Test]
		public void State_TriggerFromOnEnter_ProcessedAfterEnteringState()
		{
			var event2 = new StatechartEvent("Event2");
			Statechart statechart = null;
			statechart = new Statechart(factory =>
			{
				var state1 = factory.State("State1");
				var state2 = factory.State("State2");
				var final = SetupSimpleFlow(factory, state1);

				state1.Event(_event).Target(state2);
				// State2 에 들어가는 도중에 다음 이벤트를 보낸다. State2 진입이 끝난 뒤 State2 에서 처리되어야 한다
				state2.OnEnter(() => statechart.Trigger(event2));
				state2.Event(event2).OnTransition(() => _caller.OnTransitionCall(2)).Target(final);
			});

			statechart.Run();
			statechart.Trigger(_event);

			_caller.Received(1).OnTransitionCall(2);
			_caller.Received(1).FinalOnEnterCall(0);
		}

		[Test]
		public void State_TriggerFromOnTransition_ProcessedAfterTransitionCompletes()
		{
			var event2 = new StatechartEvent("Event2");
			Statechart statechart = null;
			statechart = new Statechart(factory =>
			{
				var state1 = factory.State("State1");
				var state2 = factory.State("State2");
				var final = SetupSimpleFlow(factory, state1);

				state1.Event(_event).OnTransition(() => statechart.Trigger(event2)).Target(state2);
				state1.Event(event2).OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
				state2.OnEnter(() => _caller.StateOnEnterCall(2));
				state2.Event(event2).OnTransition(() => _caller.OnTransitionCall(2)).Target(final);
			});

			statechart.Run();
			statechart.Trigger(_event);

			// 전이 액션 중에 보낸 이벤트는 이미 떠난 State1 이 아니라 새로 들어간 State2 에서 처리된다
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.Received(1).StateOnEnterCall(2);
			_caller.Received(1).OnTransitionCall(2);
			_caller.Received(1).FinalOnEnterCall(0);
		}

		[Test]
		public void State_ResetInsideAction_DropsEventsQueuedBeforeReset()
		{
			var oldEvent = new StatechartEvent("OldEvent");
			Statechart statechart = null;
			statechart = new Statechart(factory =>
			{
				var state = factory.State("State");
				var final = SetupSimpleFlow(factory, state);

				// 대상 없는 전이 액션에서 다음 이벤트를 보낸 뒤 리셋한다
				state.Event(_event).OnTransition(() =>
				{
					statechart.Trigger(oldEvent);
					statechart.Reset();
				});
				state.Event(oldEvent).OnTransition(() => _caller.OnTransitionCall(5)).Target(final);
			});

			statechart.Run();
			statechart.Trigger(_event);

			// 리셋 전에 쌓인 이벤트는 리셋 뒤에 처리되지 않고, 다시 Run 하기 전까지 초기 상태에 머문다
			_caller.DidNotReceive().OnTransitionCall(5);
			Assert.AreEqual("Initial", statechart.CurrentState);
		}

		[Test]
		public void State_ResetInsideOnEnter_IsNotOverwrittenByTransition()
		{
			Statechart statechart = null;
			statechart = new Statechart(factory =>
			{
				var state1 = factory.State("State1");
				var state2 = factory.State("State2");
				var final = SetupSimpleFlow(factory, state1);

				state1.Event(_event).Target(state2);
				state2.OnEnter(() => statechart.Reset());
				state2.Event(_event).Target(final);
			});

			statechart.Run();
			statechart.Trigger(_event);

			// 진행 중이던 전이의 결과(State2)가 리셋된 초기 상태를 덮어쓰지 않아야 한다
			Assert.AreEqual("Initial", statechart.CurrentState);

			statechart.Run();

			Assert.AreEqual("State1", statechart.CurrentState);
		}

		[Test]
		public void PauseInsideChainedTransition_StopsAtEnteredState_RunResumes()
		{
			Statechart statechart = null;
			statechart = new Statechart(factory =>
			{
				var initial = factory.Initial("Initial");
				var transition = factory.Transition("Transition");
				var final = factory.Final("final");

				initial.Transition().Target(transition);
				transition.OnEnter(() => statechart.Pause());
				transition.Transition().OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
				final.OnEnter(() => _caller.FinalOnEnterCall(0));
			});

			statechart.Run();

			// 진입 액션에서 일시 정지하면 연쇄 전이가 그 상태에서 멈춘다
			_caller.DidNotReceive().OnTransitionCall(1);
			_caller.DidNotReceive().FinalOnEnterCall(0);
			Assert.AreEqual("Transition", statechart.CurrentState);

			statechart.Run();

			_caller.Received(1).OnTransitionCall(1);
			_caller.Received(1).FinalOnEnterCall(0);
			Assert.AreEqual("final", statechart.CurrentState);
		}

		[Test]
		public void State_ResetInsideTargetedOnTransition_DoesNotEnterTarget()
		{
			var other = new StatechartEvent("Other");
			var enteredB = 0;
			Statechart statechart = null;
			statechart = new Statechart(factory =>
			{
				var a = factory.State("A");
				var b = factory.State("B");
				var final = SetupSimpleFlow(factory, a);

				a.Event(_event).OnTransition(() => statechart.Reset()).Target(b);
				b.OnEnter(() =>
				{
					enteredB++;
					statechart.Trigger(other);
				});
				b.Event(other).Target(final);
			});

			statechart.Run();
			statechart.Trigger(_event);

			// 리셋된 전이의 목적 상태에 들어가지 않고, 다시 Run 하기 전까지 초기 상태에 머문다
			Assert.AreEqual(0, enteredB);
			Assert.AreEqual("Initial", statechart.CurrentState);
			_caller.Received(1).OnTransitionCall(0);
			_caller.DidNotReceive().FinalOnEnterCall(0);
		}

		[Test]
		public void State_ResetInsideOnExit_SkipsTransitionAndTargetEnter()
		{
			var enteredB = 0;
			Statechart statechart = null;
			statechart = new Statechart(factory =>
			{
				var a = factory.State("A");
				var b = factory.State("B");
				var final = SetupSimpleFlow(factory, a);

				a.OnExit(() => statechart.Reset());
				a.Event(_event).OnTransition(() => _caller.OnTransitionCall(7)).Target(b);
				b.OnEnter(() => enteredB++);
				b.Event(_event).Target(final);
			});

			statechart.Run();
			statechart.Trigger(_event);

			_caller.DidNotReceive().OnTransitionCall(7);
			Assert.AreEqual(0, enteredB);
			Assert.AreEqual("Initial", statechart.CurrentState);
		}

		[Test]
		public void State_ResetInsideNestedRegionOnEnter_StopsNestCompletion()
		{
			var enteredAfterNest = 0;
			Statechart statechart = null;
			statechart = new Statechart(factory =>
			{
				var nest = factory.Nest("Nest");
				var after = factory.State("After");
				var final = SetupSimpleFlow(factory, nest);

				nest.Nest(inner =>
				{
					var innerInitial = inner.Initial("InnerInitial");
					var innerTransition = inner.Transition("InnerTransition");
					var innerFinal = inner.Final("InnerFinal");

					innerInitial.Transition().Target(innerTransition);
					innerTransition.OnEnter(() => statechart.Reset());
					innerTransition.Transition().Target(innerFinal);
				}).Target(after);

				after.OnEnter(() => enteredAfterNest++);
				after.Event(_event).Target(final);
			});

			statechart.Run();

			// 중첩 영역 안에서 리셋되면 영역의 남은 연쇄 전이와 중첩 상태의 완료 전이가 진행되지 않는다
			Assert.AreEqual(0, enteredAfterNest);
			Assert.AreEqual("Initial", statechart.CurrentState);
		}

		[Test]
		public void State_ResetInsideWaitAction_DropsWaitTransition()
		{
			var enteredB = 0;
			Statechart statechart = null;
			statechart = new Statechart(factory =>
			{
				var initial = factory.Initial("Initial");
				var waiting = factory.Wait("Waiting");
				var b = factory.State("B");
				var final = factory.Final("final");

				initial.Transition().Target(waiting);
				waiting.WaitingFor(activity =>
				{
					statechart.Reset();
					activity.Complete();
				}).Target(b);
				b.OnEnter(() => enteredB++);
				b.Event(_event).Target(final);
			});

			statechart.Run();

			// 대기 액션 안에서 리셋되면 대기 상태의 전이와 목적 상태 진입을 하지 않는다
			Assert.AreEqual(0, enteredB);
			Assert.AreEqual("Initial", statechart.CurrentState);
		}

		[Test]
		public void TaskWait_CompletesAfterReset_DoesNotRestartChart()
		{
			var completion = new UniTaskCompletionSource();
			var enteredAfter = 0;
			var statechart = new Statechart(factory =>
			{
				var initial = factory.Initial("Initial");
				var waiting = factory.TaskWait("TaskWait");
				var after = factory.State("After");
				var final = factory.Final("final");

				initial.Transition().Target(waiting);
				waiting.WaitingFor(() => completion.Task).Target(after);
				after.OnEnter(() => enteredAfter++);
				after.Event(_event).Target(final);
			});

			statechart.Run();
			statechart.Reset();
			completion.TrySetResult();

			// 리셋 전에 시작한 태스크의 완료는 버려진 실행의 것이므로 상태 차트를 다시 진행시키지 않는다
			Assert.AreEqual(0, enteredAfter);
			Assert.AreEqual("Initial", statechart.CurrentState);
		}

		[Test]
		public void Wait_CompletesAfterReset_DoesNotRestartChart()
		{
			IWaitActivity activity = null;
			var statechart = new Statechart(factory =>
			{
				var initial = factory.Initial("Initial");
				var waiting = factory.Wait("Waiting");
				var after = factory.State("After");
				var final = factory.Final("final");

				initial.Transition().Target(waiting);
				waiting.WaitingFor(waitActivity => activity = waitActivity).Target(after);
				after.Event(_event).Target(final);
			});

			statechart.Run();
			statechart.Reset();
			activity.Complete();

			Assert.AreEqual("Initial", statechart.CurrentState);
		}

		[Test]
		public void Split_ResetInsideRegionExit_StopsRemainingExitAndFinals()
		{
			var innerEvent = new StatechartEvent("InnerEvent");
			var region2Exited = 0;
			var region2FinalEntered = 0;
			var enteredAfter = 0;
			Statechart statechart = null;
			statechart = new Statechart(factory =>
			{
				var split = factory.Split("Split");
				var after = factory.State("After");
				var final = SetupSimpleFlow(factory, split);

				split.Split(
					new NestedStateData(region =>
					{
						var regionInitial = region.Initial("Region1Initial");
						var regionState = region.State("Region1State");
						var regionFinal = region.Final("Region1Final");

						regionInitial.Transition().Target(regionState);
						regionState.OnExit(() => statechart.Reset());
						regionState.Event(innerEvent).Target(regionFinal);
					}),
					new NestedStateData(region =>
					{
						var regionInitial = region.Initial("Region2Initial");
						var regionState = region.State("Region2State");
						var regionFinal = region.Final("Region2Final");

						regionInitial.Transition().Target(regionState);
						regionState.OnExit(() => region2Exited++);
						regionState.Event(innerEvent).Target(regionFinal);
						regionFinal.OnEnter(() => region2FinalEntered++);
					})).Target(final);
				split.Event(_event).Target(after);
				after.OnEnter(() => enteredAfter++);
				after.Event(_event).Target(final);
			});

			statechart.Run();
			statechart.Trigger(_event);

			// 첫 영역의 종료 중 리셋되면 다른 영역의 종료와 Final 진입, 외부 목적 상태 진입을 하지 않는다
			Assert.AreEqual(0, region2Exited);
			Assert.AreEqual(0, region2FinalEntered);
			Assert.AreEqual(0, enteredAfter);
			Assert.AreEqual("Initial", statechart.CurrentState);
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

		private void SetupStateFlow(IStateFactory factory)
		{
			var state = factory.State("State");
			var final =	SetupSimpleFlow(factory, state);

			state.OnEnter(() => _caller.StateOnEnterCall(1));
			state.Event(_event).OnTransition(() => _caller.OnTransitionCall(1)).Target(final);
			state.OnExit(() => _caller.StateOnExitCall(1));
		}
	}
}