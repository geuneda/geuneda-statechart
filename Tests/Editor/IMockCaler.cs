// ReSharper disable CheckNamespace

namespace GeunedaEditor.StatechartMachine.Tests
{
	/// <summary>
	/// 메서드 호출 수신 여부를 확인하기 위한 모킹 인터페이스
	/// </summary>
	public interface IMockCaller
	{
		void InitialOnExitCall(int id);
		void FinalOnEnterCall(int id);
		void StateOnEnterCall(int id);
		void StateOnExitCall(int id);
		void OnTransitionCall(int id);
	}
}
