using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class when_3_node_cluster_receives_multiple_acks_for_different_positions : with_index_committer_service {
		private CountdownEvent _eventsReplicated = new CountdownEvent(2);

		private Guid _correlationId1 = Guid.NewGuid();
		private Guid _correlationId2 = Guid.NewGuid();
		private Guid _correlationId3 = Guid.NewGuid();

		private long _logPosition1 = 1000;
		private long _logPosition2 = 2000;
		private long _logPosition3 = 3000;

		public override void When() {
			_publisher.Subscribe(new AdHocHandler<StorageMessage.CommitReplicated>(m => _eventsReplicated.Signal()));
			BecomeMaster();
			AddPendingPrepare(_logPosition1);
			AddPendingPrepare(_logPosition2);
			AddPendingPrepare(_logPosition3);
			_commitTracker.Handle(new CommitMessage.LogWrittenTo(_logPosition1));
			_commitTracker.Handle(new CommitMessage.LogWrittenTo( _logPosition2));
			_commitTracker.Handle(new CommitMessage.LogWrittenTo( _logPosition3));

			// Reach quorum for middle commit
			_commitTracker.Handle(new CommitMessage.ReplicaLogWrittenTo( _logPosition2, Guid.NewGuid()));

			if (!_eventsReplicated.Wait(TimeSpan.FromSeconds(_timeoutSeconds))) {
				Assert.Fail("Timed out waiting for commit replicated messages to be published");
			}
		}

		[Test]
		public void replication_checkpoint_should_have_been_updated() {
			Assert.AreEqual(_logPosition2, _replicationCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void commit_replicated_message_should_have_been_published_for_first_two_events() {
			Assert.AreEqual(2, _handledMessages.Count);
			Assert.AreEqual(_logPosition1, _handledMessages[0].TransactionPosition);
			Assert.AreEqual(_logPosition2, _handledMessages[1].TransactionPosition);
		}

		[Test]
		public void index_should_have_been_updated() {
			Assert.AreEqual(2, _indexCommitter.CommittedPrepares.Count);
			Assert.AreEqual(_logPosition1, _indexCommitter.CommittedPrepares[0].LogPosition);
			Assert.AreEqual(_logPosition2, _indexCommitter.CommittedPrepares[1].LogPosition);
		}
	}
}
