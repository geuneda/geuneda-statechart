// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine.Internal
{
	/// <summary>
	/// 이 데이터 객체는 상태 차트가 관리하는 현재 상태에 대한 기본 정보를 포함합니다
	/// </summary>
	internal class InnerStateData
	{
		public IStateInternal InitialState;
		public IStateInternal CurrenState;
		public IStateFactoryInternal NestedFactory;
		public bool ExecuteExit;
		public bool ExecuteFinal;
	}
}