﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;

using CodeJam.Extensibility;
using CodeJam.Threading;

using JetBrains.Annotations;

using LinqToDB;

namespace Rsdn.Janus
{
	[Service(typeof(IMessageMarkService))]
	internal class MessageMarkService : IMessageMarkService, IDisposable
	{
		private readonly IServiceProvider _provider;
		private readonly AsyncOperation _uiAsyncOperation;
		private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
		private readonly AutoResetEvent _syncEvent = new AutoResetEvent(false);
		private readonly List<MarkRequest> _requests = new List<MarkRequest>();
		private bool _threadCreated;

		public MessageMarkService([NotNull] IServiceProvider provider)
		{
			if (provider == null)
				throw new ArgumentNullException(nameof(provider));

			_provider = provider;
			_uiAsyncOperation = _provider.GetRequiredService<IUIShell>().CreateUIAsyncOperation();
		}

		#region Implementation of IMessageMarkService

		public void QueueMessageMark(
			[NotNull] IEnumerable<ForumEntryIds> msgIds, bool isRead, Action markFinished)
		{
			if (msgIds == null)
				throw new ArgumentNullException(nameof(msgIds));

			var msgIdsArray = msgIds.ToArray();
			using (_rwLock.GetWriteLock())
			{
				AsyncOperation asyncOp = null;
				if (markFinished != null)
					asyncOp = AsyncHelper.CreateOperation();
				_requests.Add(
					new MarkRequest(
						msgIdsArray,
						isRead,
						() =>
						{
							if (markFinished != null)
								// ReSharper disable AssignNullToNotNullAttribute
								asyncOp.PostOperationCompleted(markFinished);
							// ReSharper restore AssignNullToNotNullAttribute
						}));
				_syncEvent.Set();
				EnsureThread();
			}
		}

		#endregion

		#region Implementation of IDisposable

		public void Dispose()
		{
			_uiAsyncOperation.OperationCompleted();
		}

		#endregion

		#region Private Members

		private void EnsureThread()
		{
			if (_threadCreated)
				return;
			_threadCreated = true;
			new Thread(WorkProc) { IsBackground = true }.Start();
		}

		private void WorkProc()
		{
			while (true)
			{
				_syncEvent.WaitOne();
				try
				{
					MarkMessages();
				}
				catch (Exception ex)
				{
					_uiAsyncOperation.Post(() => { throw ex; });
				}
			}
			// ReSharper disable FunctionNeverReturns
		}
		// ReSharper restore FunctionNeverReturns

		private static void MarkMsgsRead(
			IServiceProvider provider,
			IEnumerable<ForumEntryIds> msgIds,
			bool isRead)
		{
			var msgIdsArray = msgIds.ToArray();
			if (msgIdsArray.Any(ids => ids.GetEntryType() != ForumEntryType.Message))
				throw new ArgumentException(@"Элемент последовательности не является сообщением.", nameof(msgIds));

			using (var db = provider.CreateDBContext())
			using (var tx = db.BeginTransaction())
			{
				foreach (var series in
					msgIds
						.Select(ids => ids.MessageId)
						.SplitForInClause(provider))
				{
					var locSeries = series;
					db
						.Messages(m => locSeries.Contains(m.ID))
						.Set(_ => _.IsRead, isRead)
						.Update();
				}

				DatabaseManager.UpdateTopicInfoRange(
					provider,
					db,
					msgIdsArray
						.Select(ids => ids.TopicId.GetValueOrDefault())
						.Distinct());

				tx.Commit();
			}
		}

		private void MarkMessages()
		{
			HashSet<MarkRequest> hash;
			using (_rwLock.GetReadLock())
				hash = new HashSet<MarkRequest>(_requests);

			foreach (var group in hash
					.GroupBy(rq => rq.IsRead)
					.Select(grp => new { IsRead = grp.Key, Ids = grp.SelectMany(rq => rq.MsgIds) }))
				MarkMsgsRead(
					_provider,
					group.Ids,
					group.IsRead);

			using (_rwLock.GetWriteLock())
				_requests.RemoveAll(hash.Contains);

			foreach (var notificator in hash.Select(rq => rq.MarkFinished))
				notificator();
		}

		#endregion

		#region MarkRequest class
		private class MarkRequest
		{
			public MarkRequest(ForumEntryIds[] msgIds, bool isRead, Action markFinished)
			{
				MsgIds = msgIds;
				IsRead = isRead;
				MarkFinished = markFinished;
			}

			public ForumEntryIds[] MsgIds { get; }

			public bool IsRead { get; }

			public Action MarkFinished { get; }
		}
		#endregion
	}
}