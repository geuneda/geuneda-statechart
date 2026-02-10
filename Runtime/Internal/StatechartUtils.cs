// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine.Internal
{
	/// <summary>
	/// 상태 차트가 올바르게 동작하도록 돕는 헬퍼 클래스
	/// </summary>
	internal static class StatechartUtils
	{
		/// <summary>
		/// 주어진 <paramref name="stackTrace"/>에서 불필요한 문자열 데이터를 정리합니다
		/// </summary>
		public static string RemoveGarbageFromStackTrace(string stackTrace)
		{
			var lineIdx = stackTrace.IndexOf('\n') + 1;
			do
			{
				stackTrace = stackTrace.Remove(0, lineIdx);
				lineIdx = stackTrace.IndexOf('\n') + 1;
			}
			while (stackTrace.Substring(0, lineIdx).Contains("StateMachine.Internal"));

			return stackTrace + "\n";
		}
	}
}
