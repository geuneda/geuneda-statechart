using System;

// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine
{
	/// <summary>
	/// 이벤트는 설정에서 정의된 경우 상태 차트를 전진시키기 위한 고유 입력입니다.
	/// 상태 차트를 전진시키려면 <see cref="IStatechart.Trigger(IStatechartEvent)"/>를 호출해야 합니다.
	/// </summary>
	public interface IStatechartEvent : IEquatable<IStatechartEvent>
	{
		/// <summary>
		/// 이벤트의 고유 ID.
		/// 생성 시 자동으로 생성됩니다.
		/// </summary>
		uint Id { get; }

		/// <summary>
		/// 이벤트에 정의된 이름.
		/// 디버깅 목적에 유용합니다.
		/// </summary>
		string Name { get; }
	}

	/// <inheritdoc cref="IStatechartEvent"/>
	public class StatechartEvent : IStatechartEvent
	{
		private static uint _idRef;

		/// <inheritdoc />
		public uint Id { get; }
		/// <inheritdoc />
		public string Name { get; }

		private StatechartEvent() {}

		public StatechartEvent(string name)
		{
			Id = ++_idRef;
			Name = name;
		}

		public bool Equals(IStatechartEvent statechartEvent)
		{
			return statechartEvent != null && Id == statechartEvent.Id;
		}

		public override bool Equals(object obj)
		{
			return obj is IStatechartEvent chartEvent && Equals(chartEvent);
		}

		public override int GetHashCode()
		{
			return (int) Id;
		}

		public override string ToString()
		{
			return Name;
		}
	}
}