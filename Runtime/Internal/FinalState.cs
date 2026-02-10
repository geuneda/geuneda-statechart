using System;
using System.Collections.Generic;

// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine.Internal
{
	/// <inheritdoc cref="IFinalState"/>
	internal class FinalState : StateInternal, IFinalState
	{
		private readonly IList<Action> _onEnter = new List<Action>();

		public FinalState(string name, IStateFactoryInternal factory) : base(name, factory)
		{
		}

		/// <inheritdoc />
		public override void Enter()
		{
			for(var i = 0; i < _onEnter.Count; i++)
			{
				_onEnter[i]?.Invoke();
			}
		}

		/// <inheritdoc />
		public override void Exit()
		{
			// 최종 상태에서는 아무 작업도 수행하지 않음
		}

		/// <inheritdoc />
		public override void Validate()
		{
			// 최종 상태에서는 아무 작업도 수행하지 않음
		}

		/// <inheritdoc />
		protected override ITransitionInternal OnTrigger(IStatechartEvent statechartEvent)
		{
			return null;
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
	}
}