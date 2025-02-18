﻿//-----------------------------------------------------------------------
// <copyright file="WriteJournal.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Annotations;

namespace Akka.Persistence.Journal
{
    /// <summary>
    /// TBD
    /// </summary>
    public abstract class WriteJournalBase : ActorBase
    {
        private readonly EventAdapters _eventAdapters;

        /// <summary>
        /// TBD
        /// </summary>
        protected WriteJournalBase()
        {
            var persistence = Persistence.Instance.Apply(Context.System);
            _eventAdapters = persistence.AdaptersFor(Self);
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="resequenceables">TBD</param>
        /// <returns>TBD</returns>
        protected IEnumerable<AtomicWrite> PreparePersistentBatch(IEnumerable<IPersistentEnvelope> resequenceables)
        {
            foreach (var resequenceable in resequenceables)
            {
                if (resequenceable is not AtomicWrite) continue;
                
                var result = ImmutableList.CreateBuilder<IPersistentRepresentation>();

                foreach (var representation in (IEnumerable<IPersistentRepresentation>)resequenceable.Payload)
                {
                    var adapted = AdaptToJournal(representation.Update(representation.SequenceNr, representation.PersistenceId, representation.IsDeleted,
                        ActorRefs.NoSender, representation.WriterGuid));
                    result.Add(adapted);
                }
                yield return new AtomicWrite(result.ToImmutable());
            }
        }

        /// <summary>
        /// INTERNAL API
        /// </summary>
        [InternalApi]
        protected IEnumerable<IPersistentRepresentation> AdaptFromJournal(IPersistentRepresentation representation)
        {
            return _eventAdapters.Get(representation.Payload.GetType())
                .FromJournal(representation.Payload, representation.Manifest)
                .Events
                .Select(representation.WithPayload);
        }

        /// <summary>
        /// INTERNAL API
        /// </summary>
        protected IPersistentRepresentation AdaptToJournal(IPersistentRepresentation representation)
        {
            var payload = representation.Payload;
            var adapter = _eventAdapters.Get(payload.GetType());

            // IdentityEventAdapter returns "" as manifest and normally the incoming IPersistentRepresentation
            // doesn't have an assigned manifest, but when WriteMessages is sent directly to the
            // journal for testing purposes we want to preserve the original manifest instead of
            // letting IdentityEventAdapter clearing it out.
            return (Equals(adapter, IdentityEventAdapter.Instance) || adapter is NoopWriteEventAdapter)
                ? representation
                : representation.WithPayload(adapter.ToJournal(payload)).WithManifest(adapter.Manifest(payload));
        }
    }
}

