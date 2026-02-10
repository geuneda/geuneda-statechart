using System;
using System.Collections.Generic;

// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine.Internal
{
	/// <summary>
	/// 상태 생성 패턴을 관리하기 위해 <see cref="IStateFactory"/>에서 사용할 데이터 참조
	/// </summary>
	internal struct StateFactoryData
	{
		public Action<IStatechartEvent> StateChartMoveNextCall;
		public IStatechart Statechart;
	}

	/// <inheritdoc />
	internal interface IStateFactoryInternal : IStateFactory
	{
		/// <summary>
		/// 이 <see cref="IStateFactory"/>가 구축하는 레이어
		/// </summary>
		uint RegionLayer { get; }
		/// <summary>
		/// 이 <see cref="IStateFactory"/>가 구축하는 현재 <see cref="InitialState"/>
		/// </summary>
		InitialState InitialState { get; }
		/// <summary>
		/// 이 <see cref="IStateFactory"/>가 구축하는 현재 <see cref="FinalState"/>
		/// </summary>
		FinalState FinalState { get; }
		/// <summary>
		/// 이 <see cref="IStateFactory"/>가 구축하는 현재 상태 목록
		/// </summary>
		IList<IStateInternal> States { get; }
		/// <summary>
		/// 이 <see cref="IStateFactory"/>가 구축하는 <see cref="StateFactoryData"/>
		/// </summary>
		StateFactoryData Data { get; }

		/// <summary>
		/// 주어진 <paramref name="states"/> 목록을 이 <see cref="IStateFactory"/>에 추가하여 구축합니다
		/// </summary>
		/// <param name="states"></param>
		void Add(IList<IStateInternal> states);
	}

	/// <inheritdoc />
	internal class StateFactory : IStateFactoryInternal
	{
		/// <inheritdoc />
		public uint RegionLayer { get; private set; }
		/// <inheritdoc />
		public InitialState InitialState { get; private set; }
		/// <inheritdoc />
		public FinalState FinalState { get; private set; }
		/// <inheritdoc />
		public IList<IStateInternal> States { get; private set; }
		/// <inheritdoc />
		public StateFactoryData Data { get; private set; }

		private StateFactory()
		{
		}

		public StateFactory(uint layer, StateFactoryData data)
		{
			States = new List<IStateInternal>();

			Data = data;
			RegionLayer = layer;
		}

		/// <inheritdoc />
		public void Add(IList<IStateInternal> states)
		{
			for (var i = 0; i < states.Count; i++)
			{
				States.Add(states[i]);
			}
		}

		/// <inheritdoc />
		public IInitialState Initial(string name)
		{
			if (InitialState != null)
			{
				throw new InvalidOperationException($"Initial state already set to {InitialState.Name}");
			}

			var initial = new InitialState(name, this);

			InitialState = initial;

			States.Add(initial);

			return initial;
		}

		/// <inheritdoc />
		public IFinalState Final(string name)
		{
			if (FinalState != null)
			{
				throw new InvalidOperationException($"Final state already set to {FinalState.Name}");
			}

			var state = new FinalState(name, this);

			FinalState = state;

			States.Add(state);

			return state;
		}

		/// <inheritdoc />
		public ISimpleState State(string name)
		{
			var state = new SimpleState(name, this);

			States.Add(state);

			return state;
		}

		/// <inheritdoc />
		public ITransitionState Transition(string name)
		{
			var state = new TransitionState(name, this);

			States.Add(state);

			return state;
		}

		/// <inheritdoc />
		public INestState Nest(string name)
		{
			var state = new NestState(name, this);

			States.Add(state);

			return state;
		}

		/// <inheritdoc />
		public IWaitState Wait(string name)
		{
			var state = new WaitState(name, this);

			States.Add(state);

			return state;
		}

		/// <inheritdoc />
		public ITaskWaitState TaskWait(string name)
		{
			var state = new TaskWaitState(name, this);

			States.Add(state);

			return state;
		}

		/// <inheritdoc />
		public IChoiceState Choice(string name)
		{
			var state = new ChoiceState(name, this);

			States.Add(state);

			return state;
		}

		/// <inheritdoc />
		public ISplitState Split(string name)
		{
			var state = new SplitState(name, this);

			States.Add(state);

			return state;
		}

		/// <inheritdoc />
		public ILeaveState Leave(string name)
		{
			var state = new LeaveState(name, this);

			States.Add(state);

			return state;
		}
	}
}