using System;
using UnityEngine;

// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine.Internal
{
	/// <inheritdoc cref="IState"/>
	internal interface IStateInternal : IState, IEquatable<IStateInternal>
	{
		/// <summary>
		/// 이 상태를 식별하는 고유 값
		/// </summary>
		uint Id { get; }
		/// <summary>
		/// 이 상태를 식별하는 문자열 표현
		/// </summary>
		string Name { get; }
		/// <summary>
		/// 이 상태가 속한 중첩 설정의 레이어. 루트에 있으면 값은 0입니다.
		/// </summary>
		uint RegionLayer { get; }
		/// <summary>
		/// 이 설정이 생성될 때의 스택 트레이스. 디버깅 목적에 유용합니다.
		/// </summary>
		string CreationStackTrace { get; }

		/// <summary>
		/// 주어진 <paramref name="statechartEvent"/>를 <see cref="IStatechart"/>의 입력으로 트리거하고
		/// 처리된 <see cref="IStateInternal"/>을 출력으로 반환합니다
		/// </summary>
		IStateInternal Trigger(IStatechartEvent statechartEvent);
		/// <summary>
		/// 이 상태가 <see cref="IStatechart"/>에서 새로운 현재 상태로 진입하는 초기 시점을 표시합니다
		/// </summary>
		void Enter();
		/// <summary>
		/// 이 상태가 <see cref="IStatechart"/>에서 현재 상태에서 벗어나는 최종 시점을 표시합니다
		/// </summary>
		void Exit();
		/// <summary>
		/// 이 상태의 잘못된 설정 구성을 검증합니다. 디버깅 목적에 유용합니다.
		/// <see cref="IStatechart"/>가 런타임에서 실행되어야 합니다.
		/// </summary>
		void Validate();
	}

	/// <inheritdoc />
	internal abstract class StateInternal : IStateInternal
	{
		protected readonly IStateFactoryInternal _stateFactory;

		private static uint _idRef;

		/// <inheritdoc />
		public bool LogsEnabled { get; set; }
		/// <inheritdoc />
		public uint Id { get; }
		/// <inheritdoc />
		public string Name { get; }
		/// <inheritdoc />
		public uint RegionLayer => _stateFactory.RegionLayer;
		/// <inheritdoc />
		public string CreationStackTrace { get; }

		protected bool IsStateLogsEnabled => LogsEnabled || _stateFactory.Data.Statechart.LogsEnabled;

		protected StateInternal(string name, IStateFactoryInternal stateFactory)
		{
			Id = ++_idRef;
			Name = name;

			_stateFactory = stateFactory;

#if UNITY_EDITOR || DEBUG
			CreationStackTrace = StatechartUtils.RemoveGarbageFromStackTrace(Environment.StackTrace);
#endif
		}

		/// <inheritdoc />
		public IStateInternal Trigger(IStatechartEvent statechartEvent)
		{
			var transition = OnTrigger(statechartEvent);

			if (transition == null)
			{
				if (IsStateLogsEnabled)
				{
					Debug.Log($"({GetType().UnderlyingSystemType.Name}) - '{statechartEvent?.Name}' : ## STOP ## '{Name}'");
				}
				
				return null;
			}

			var nextState = transition.TargetState;

			if (Equals(nextState))
			{
				if (IsStateLogsEnabled)
				{
					Debug.Log($"({GetType().UnderlyingSystemType.Name}) - '{statechartEvent?.Name}' : " +
					          $"'{Name}' -> '{Name}' because => {GetType().UnderlyingSystemType.Name}");
				}
				
				return nextState;
			}

			if (nextState == null)
			{
				TriggerTransition(transition, statechartEvent?.Name);

				return null;
			}

			TriggerExit();
			TriggerTransition(transition, statechartEvent?.Name);
			TriggerEnter(nextState);

			return nextState;
		}

		/// <inheritdoc />
		public bool Equals(IStateInternal stateInternal)
		{
			return stateInternal != null && Id == stateInternal.Id;
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			return obj is IStateInternal stateBase && Equals(stateBase);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return (int) Id;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return Name;
		}

		/// <inheritdoc />
		public abstract void Enter();
		/// <inheritdoc />
		public abstract void Exit();
		/// <inheritdoc />
		public abstract void Validate();

		protected abstract ITransitionInternal OnTrigger(IStatechartEvent statechartEvent);

		private void TriggerEnter(IStateInternal state)
		{
			try
			{
				if (IsStateLogsEnabled)
				{
					Debug.Log($"({state.GetType().UnderlyingSystemType.Name}) - Entering '{state.Name}'");
				}
				state.Enter();
			}
			catch (Exception e)
			{
				var finalMessage = "";
#if UNITY_EDITOR || DEBUG
				finalMessage = $"\nStackTrace log of '{state.Name}' state creation bellow.\n{CreationStackTrace}";
#endif

				Debug.LogError($"Exception in the state '{state.Name}', OnExit() actions.\n" +
					$"-->> Check the exception log after this one for more details <<-- {finalMessage}");
				Debug.LogException(e);
			}
		}

		private void TriggerExit()
		{
			try
			{
				if(IsStateLogsEnabled)
				{
					Debug.LogFormat($"({GetType().UnderlyingSystemType.Name}) - Exiting '{Name}'");
				}
				Exit();
			}
			catch(Exception e)
			{
				var finalMessage = "";
#if UNITY_EDITOR || DEBUG
				finalMessage = $"\nStackTrace log of '{Name}' state creation bellow.\n{CreationStackTrace}";
#endif

				Debug.LogError($"Exception in the state '{Name}', OnExit() actions.\n" +
					$"-->> Check the exception log after this one for more details <<-- {finalMessage}");
				Debug.LogException(e);
			}
		}

		private void TriggerTransition(ITransitionInternal transition, string eventName)
		{
			try
			{
				if (IsStateLogsEnabled)
				{
					var targetState = transition.TargetState?.Name ?? "only invokes OnTransition()";
					
					Debug.Log($"({GetType().UnderlyingSystemType.Name}) - '{eventName}' : '{Name}' -> '{targetState}'");
				}
				
				transition.TriggerTransition();
			}
			catch (Exception e)
			{
				var finalMessage = "";
#if UNITY_EDITOR || DEBUG
				finalMessage = $"\nStackTrace log of this transition creation bellow.\n{transition.CreationStackTrace}";
#endif

				Debug.LogError($"Exception in the transition '{Name}' -> '{transition.TargetState?.Name}'" +
					$"-->> Check the exception log after this one for more details <<-- {finalMessage}");
				Debug.LogException(e);
			}
		}
	}
}