using System;
using Geuneda.Core;

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
		protected static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Statechart");

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
			// 사용자 코드(대기 액션, 선택 조건, 중첩 영역의 전이)가 실행되기 전의 리셋 세대를 기억한다
			var resetVersion = _stateFactory.Data.ResetVersion();
			var transition = OnTrigger(statechartEvent);

			// 그 안에서 Reset 되었으면 이 전이를 버린다
			if (resetVersion != _stateFactory.Data.ResetVersion())
			{
				return null;
			}

			if (transition == null)
			{
				if (IsStateLogsEnabled)
				{
					Logger.Info($"({GetType().UnderlyingSystemType.Name}) - '{statechartEvent?.Name}' : ## STOP ## '{Name}'");
				}
				
				return null;
			}

			var nextState = transition.TargetState;

			if (Equals(nextState))
			{
				if (IsStateLogsEnabled)
				{
					Logger.Info($"({GetType().UnderlyingSystemType.Name}) - '{statechartEvent?.Name}' : " +
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

			// 퇴장/전이 액션 안에서 Reset 되면 버려진 전이이므로 남은 전이 액션과 목적 상태 진입을 하지 않는다
			if (resetVersion != _stateFactory.Data.ResetVersion())
			{
				return null;
			}

			TriggerTransition(transition, statechartEvent?.Name);

			if (resetVersion != _stateFactory.Data.ResetVersion())
			{
				return null;
			}

			TriggerEnter(nextState);

			// 진입 액션 안에서 Reset 되면 진입한 상태를 다음 상태로 넘기지 않는다(호출한 쪽이 연쇄 전이를 멈춘다)
			if (resetVersion != _stateFactory.Data.ResetVersion())
			{
				return null;
			}

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
					Logger.Info($"({state.GetType().UnderlyingSystemType.Name}) - Entering '{state.Name}'");
				}
				state.Enter();
			}
			catch (Exception e)
			{
				var finalMessage = "";
#if UNITY_EDITOR || DEBUG
				finalMessage = $"\nStackTrace log of '{state.Name}' state creation bellow.\n{state.CreationStackTrace}";
#endif

				Logger.Error(e, $"Exception in the state '{state.Name}', OnEnter() actions.\n" +
					$"-->> Check the exception log after this one for more details <<-- {finalMessage}");
			}
		}

		private void TriggerExit()
		{
			try
			{
				if(IsStateLogsEnabled)
				{
					Logger.Info($"({GetType().UnderlyingSystemType.Name}) - Exiting '{Name}'");
				}
				Exit();
			}
			catch(Exception e)
			{
				var finalMessage = "";
#if UNITY_EDITOR || DEBUG
				finalMessage = $"\nStackTrace log of '{Name}' state creation bellow.\n{CreationStackTrace}";
#endif

				Logger.Error(e, $"Exception in the state '{Name}', OnExit() actions.\n" +
					$"-->> Check the exception log after this one for more details <<-- {finalMessage}");
			}
		}

		private void TriggerTransition(ITransitionInternal transition, string eventName)
		{
			try
			{
				if (IsStateLogsEnabled)
				{
					var targetState = transition.TargetState?.Name ?? "only invokes OnTransition()";
					
					Logger.Info($"({GetType().UnderlyingSystemType.Name}) - '{eventName}' : '{Name}' -> '{targetState}'");
				}
				
				transition.TriggerTransition();
			}
			catch (Exception e)
			{
				var finalMessage = "";
#if UNITY_EDITOR || DEBUG
				finalMessage = $"\nStackTrace log of this transition creation bellow.\n{transition.CreationStackTrace}";
#endif

				Logger.Error(e, $"Exception in the transition '{Name}' -> '{transition.TargetState?.Name}'" +
					$"-->> Check the exception log after this one for more details <<-- {finalMessage}");
			}
		}
	}
}