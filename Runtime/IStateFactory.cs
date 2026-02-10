// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine
{
	/// <summary>
	/// 상태 팩토리는 <see cref="IStatechart"/>의 상태와 전이 표현을 설정하는 데 사용됩니다.
	/// <see cref="IStatechart"/>의 각 영역마다 항상 상태 팩토리가 하나씩 생성됩니다.
	/// 상태 팩토리는 또한 <see cref="IStatechart"/>의 상태와 전이 데이터 컨테이너입니다.
	/// </summary>
	public interface IStateFactory
	{
		/// <inheritdoc cref="IInitialState"/>
		IInitialState Initial(string name);
		/// <inheritdoc cref="IFinalState"/>
		IFinalState Final(string name);
		/// <inheritdoc cref="ISimpleState"/>
		ISimpleState State(string name);
		/// <inheritdoc cref="ITransition"/>
		ITransitionState Transition(string name);
		/// <inheritdoc cref="INestState"/>
		INestState Nest(string name);
		/// <inheritdoc cref="IChoiceState"/>
		IChoiceState Choice(string name);
		/// <inheritdoc cref="IWaitState"/>
		IWaitState Wait(string name);
		/// <inheritdoc cref="ITaskWaitState"/>
		ITaskWaitState TaskWait(string name);
		/// <inheritdoc cref="ISplitState"/>
		ISplitState Split(string name);
		/// <inheritdoc cref="ILeaveState"/>
		ILeaveState Leave(string name);
	}
}