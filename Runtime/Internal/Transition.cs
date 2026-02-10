using System;
using System.Collections.Generic;

// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine.Internal
{
	/// <inheritdoc />
	internal interface ITransitionInternal : ITransitionCondition
	{
		/// <summary>
		/// 이 전이의 현재 대상 상태
		/// </summary>
		IStateInternal TargetState { get; }
		/// <summary>
		/// 이 전이에 확인할 조건이 있으면 true, 그렇지 않으면 false
		/// </summary>
		bool HasCondition { get; }
		/// <summary>
		/// 디버그용 스택 트레이스 문자열
		/// </summary>
		string CreationStackTrace { get; }

		/// <summary>
		/// 정의된 전이 조건을 확인합니다.
		/// 조건이 충족되면 true를, 그렇지 않으면 false를 반환합니다.
		/// </summary>
		/// <returns></returns>
		bool CheckCondition();
		/// <summary>
		/// 정의된 전이를 트리거합니다
		/// </summary>
		void TriggerTransition();
	}

	/// <inheritdoc />
	internal class Transition : ITransitionInternal
	{
		private readonly IList<Action> _onTransition = new List<Action>();
		private readonly IList<Func<bool>> _condition = new List<Func<bool>>();

		/// <inheritdoc />
		public IStateInternal TargetState { get; private set; }
		/// <inheritdoc />
		public string CreationStackTrace { get; private set; }
		/// <inheritdoc />
		public bool HasCondition => _condition.Count > 0;

		/// <inheritdoc />
		public bool CheckCondition()
		{
			var ret = true;

			for(var i = 0; i < _condition.Count; i++)
			{
				ret = ret && _condition[i].Invoke();
			}

			return ret;
		}

		/// <inheritdoc />
		public void TriggerTransition()
		{
			for(var i = 0; i < _onTransition.Count; i++)
			{
				_onTransition[i]?.Invoke();
			}
		}

		/// <inheritdoc />
		public ITransition Condition(Func<bool> condition)
		{
			if(condition == null)
			{
				throw new NullReferenceException("The transition cannot have a null condition");
			}

#if UNITY_EDITOR || DEBUG
			CreationStackTrace = StatechartUtils.RemoveGarbageFromStackTrace(Environment.StackTrace);
#endif

			_condition.Add(condition);

			return this;
		}

		/// <inheritdoc />
		public ITransition OnTransition(Action action)
		{
			if (action == null)
			{
				throw new NullReferenceException("The transition cannot have a null OnTransition action");
			}

#if UNITY_EDITOR || DEBUG
			CreationStackTrace = StatechartUtils.RemoveGarbageFromStackTrace(Environment.StackTrace);
#endif

			_onTransition.Add(action);

			return this;
		}

		/// <inheritdoc />
		public void Target(IState state)
		{
			if (state == null)
			{
				throw new NullReferenceException("The transition cannot have a null target state");
			}

			TargetState = (IStateInternal) state;
		}
	}
}