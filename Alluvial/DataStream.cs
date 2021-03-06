using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Alluvial
{
    public static class DataStream
    {
        public static IDataStream<TData> AsDataStream<TData>(
            this IEnumerable<TData> source)
            where TData : IComparable<TData>
        {
            return Create<TData>(Guid.NewGuid().ToString(),
                                 query => source.SkipWhile(x => query.Cursor.HasReached(x))
                                                .Take(query.BatchCount ?? int.MaxValue));
        }

        public static IDataStream<TData> Create<TData>(
            Func<IStreamQuery<TData>, Task<IEnumerable<TData>>> query,
            Action<IStreamQuery<TData>, IStreamQueryBatch<TData>> advanceCursor = null)
        {
            return Create(Guid.NewGuid().ToString(), query, advanceCursor);
        }

        public static IDataStream<TData> Create<TData>(
            string id,
            Func<IStreamQuery<TData>, Task<IEnumerable<TData>>> query,
            Action<IStreamQuery<TData>, IStreamQueryBatch<TData>> advanceCursor = null)
        {
            return new AnonymousDataStream<TData>(
                id,
                async q => StreamQueryBatch.Create(await query(q), q.Cursor),
                advanceCursor);
        }

        public static IDataStream<TData> Create<TData>(
            Func<IStreamQuery<TData>, IEnumerable<TData>> query,
            Action<IStreamQuery<TData>, IStreamQueryBatch<TData>> advanceCursor = null)
        {
            return Create(Guid.NewGuid().ToString(), query, advanceCursor);
        }

        public static IDataStream<TData> Create<TData>(
            string id,
            Func<IStreamQuery<TData>, IEnumerable<TData>> query,
            Action<IStreamQuery<TData>, IStreamQueryBatch<TData>> advanceCursor = null)
        {
            return new AnonymousDataStream<TData>(
                id,
                async q => StreamQueryBatch.Create(query(q), q.Cursor),
                advanceCursor);
        }

        public static ICursor CreateCursor<TData>(this IDataStream<TData> stream)
        {
            return Cursor.New();
        }

        public static IDataStream<TTo> Map<TFrom, TTo>(
            this IDataStream<TFrom> sourceStream,
            Func<IEnumerable<TFrom>, IEnumerable<TTo>> map,
            string id = null)
        {
            return Create<TTo>(
                id: id ?? sourceStream.Id,
                query: async q =>
                {
                    var sourceBatch = await sourceStream.Fetch(
                        sourceStream.CreateQuery(q.Cursor, q.BatchCount));

                    var mappedBatch = map(sourceBatch);

                    return StreamQueryBatch.Create(mappedBatch, q.Cursor);
                },
                advanceCursor: async (query, batch) =>
                {
                    // don't advance the cursor in the map operation, since sourceStream.Fetch will already have done it
                });
        }

        public static IDataStream<IDataStream<TDownstream>> Requery<TUpstream, TDownstream>(
            this IDataStream<TUpstream> upstream,
            Func<TUpstream, IDataStream<TDownstream>> queryDownstream)
        {
            return Create<IDataStream<TDownstream>>(
                query: async upstreamQuery =>
                {
                    var cursor = upstreamQuery.Cursor;

                    var upstreamBatch = await upstream.Fetch(
                        upstream.CreateQuery(cursor, upstreamQuery.BatchCount));

                    return upstreamBatch.Select(queryDownstream);
                },
                advanceCursor: (query, batch) =>
                {
                    // we're passing the cursor through to the upstream query, so we don't want downstream queries to overwrite it
                });
        }

        public static async Task<TProjection> ProjectWith<TProjection, TData>(
            this IDataStream<TData> dataStream,
            IDataStreamAggregator<TProjection, TData> projector,
            TProjection projection = null)
            where TProjection : class
        {
            // QUESTION: (ProjectWith) better name? this can also be used for side effects, where TProjection is used to track the state of the work

            var cursor = (projection as ICursor) ??
                         Cursor.New();

            var query = dataStream.CreateQuery(cursor);

            var data = await query.NextBatch();

            if (data.Any())
            {
                projection = projector.Aggregate(projection, data);
            }

            return projection;
        }
    }
}