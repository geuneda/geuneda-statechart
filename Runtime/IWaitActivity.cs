// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine
{
	/// <summary>
	/// <see cref="IWaitState"/>의 컨트롤러
	/// 이 활동은 <see cref="IWaitState"/>의 실행을 완료하는 동작을 가지며, 제어 중인
	/// <see cref="IWaitState"/>에 대해 더 큰 분할 제어를 위한 추가 활동 계층을 생성할 수도 있습니다.
	/// </summary>
	public interface IWaitActivity
	{
		/// <summary>
		/// 이 대기 가능한 <see cref="IWaitActivity"/>를 정의하는 고유 값 ID
		/// </summary>
		uint Id { get; }
		/// <summary>
		/// 상태의 실행 프로세스를 계속하기 위해 <see cref="IWaitState"/>를 완료로 표시합니다.
		/// 모든 내부 활동도 완료되었으면 true를, 그렇지 않으면 false를 반환합니다.
		/// </summary>
		bool Complete();

		/// <summary>
		/// <see cref="IState"/>를 유지하기 위한 새로운 계층의 내부 활동을 생성하고 반환합니다.
		/// 생성된 새 내부 활동은 이 활동의 자식이 되며, 이 활동의 완료는
		/// 모든 자식도 완료되었을 때만 성공합니다.
		/// </summary>
		IWaitActivity Split();
	}
}