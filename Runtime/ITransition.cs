using System;

// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine
{
	/// <summary>
	/// 두 <see cref="IState"/> 간의 전환을 수행하기 위한 데이터를 가진 전이
	/// 전이는 상태의 입력에 따라 트리거되며, 정의된 경우 출력을 가질 수 있습니다.
	/// </summary>
	public interface ITransition
	{
		/// <summary>
		/// <seealso cref="ITransition"/>이 트리거될 때 호출할 <paramref name="action"/> 출력을 추가합니다.
		/// 체인 명령을 생성하기 위해 이 전이를 반환합니다.
		/// </summary>
		ITransition OnTransition(Action action);

		/// <summary>
		/// 전이의 대상 <paramref name="state"/>를 정의합니다
		/// </summary>
		void Target(IState state);
	}

	/// <summary>
	/// 전이 실행의 유효성을 확인하는 조건 검사를 추가한 <see cref="ITransition"/>의 확장 계약
	/// </summary>
	public interface ITransitionCondition : ITransition
	{
		/// <summary>
		/// 이 전이를 실행할 수 있는지 확인하기 위한 전이 <paramref name="condition"/> 함수를 정의합니다.
		/// 조건 함수가 실패하면 전이가 실행되지 않습니다.
		/// 체인 명령을 생성하기 위해 이 전이를 반환합니다.
		/// </summary>
		ITransition Condition(Func<bool> condition);
	}
}