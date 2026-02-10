using System;
using System.Collections.Generic;

// ReSharper disable CheckNamespace

namespace Geuneda.StatechartMachine.Internal
{
	/// <inheritdoc />
	internal interface IWaitActivityInternal : IWaitActivity
	{
		/// <summary>
		/// 이 활동이 완료로 표시되었으면 true를, 그렇지 않으면 false를 반환합니다
		/// </summary>
		bool IsCompleted { get; }

		/// <summary>
		/// 이 활동과 이전에 분리된 모든 자식 활동을 강제 완료합니다
		/// </summary>
		void ForceComplete();
	}

	/// <inheritdoc />
	internal class WaitActivity : IWaitActivityInternal
	{
		private static uint _idRef;

		private readonly Action<uint> _onComplete;
		private readonly List<IWaitActivityInternal> _innerActivities = new List<IWaitActivityInternal>();

		private bool _completed;

		/// <inheritdoc />
		public uint Id { get; private set; }
		/// <inheritdoc />
		public bool IsCompleted => _completed && AreInnerCompleted();

		private WaitActivity()
		{
			Id = ++_idRef;
		}

		/// <summary>
		/// 이 생성자는 <see cref="WaitState"/>에서 외부적으로 호출됩니다
		/// </summary>
		public WaitActivity(Action<uint> onComplete) : this()
		{
			_onComplete = onComplete;
		}

		/// <inheritdoc />
		public bool Complete()
		{
			var innerCompleted = AreInnerCompleted();

			if (_completed && innerCompleted)
			{
				return true;
			}

			_completed = true;

			if (innerCompleted)
			{
				InvokeCompleted();
			}

			return _completed && innerCompleted;
		}

		/// <inheritdoc />
		public void ForceComplete()
		{
			_completed = true;

			foreach (var activity in _innerActivities)
			{
				activity.ForceComplete();
			}

			_innerActivities.Clear();
		}

		/// <inheritdoc />
		public IWaitActivity Split()
		{
			var activity = new WaitActivity(ProcessComplete);

			_innerActivities.Add(activity);

			return activity;
		}

		private void ProcessComplete(uint id)
		{
			if(!IsCompleted)
			{
				return;
			}

			InvokeCompleted();
		}

		private void InvokeCompleted()
		{
			_innerActivities.Clear();
			_onComplete(Id);
		}

		private bool AreInnerCompleted()
		{
			foreach (var activity in _innerActivities)
			{
				if (!activity.IsCompleted)
				{
					return false;
				}
			}

			return true;
		}
	}
}
