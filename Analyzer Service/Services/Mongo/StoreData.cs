using System.Collections.Generic;
using System.Threading.Tasks;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Schema;
using MongoDB.Driver;

namespace Analyzer_Service.Services.Mongo
{
    public class StoreData
    {
        private readonly IFlightTelemetryMongoProxy telemetryMongoProxy;

        private readonly Dictionary<int, CachedFlight> flightCache;
        private readonly object cacheLock;

        public StoreData(IFlightTelemetryMongoProxy telemetryMongoProxy)
        {
            this.telemetryMongoProxy = telemetryMongoProxy;
            this.flightCache = new Dictionary<int, CachedFlight>();
            this.cacheLock = new object();
        }

        public async Task<CachedFlight> GetFlightAsync(int masterIndex)
        {
            CachedFlight cachedFlight;

            lock (this.cacheLock)
            {
                if (this.flightCache.TryGetValue(masterIndex, out cachedFlight))
                {
                    return cachedFlight;
                }
            }

            CachedFlight loadedFlight = await this.LoadEntireFlightAsync(masterIndex);

            lock (this.cacheLock)
            {
                if (!this.flightCache.TryGetValue(masterIndex, out cachedFlight))
                {
                    this.flightCache[masterIndex] = loadedFlight;
                    return loadedFlight;
                }

                return cachedFlight;
            }
        }

        public async Task<SignalSeries> GetFlightDataAsync(int masterIndex, string yParameter)
        {
            CachedFlight flight = await this.GetFlightAsync(masterIndex);

            if (!flight.ValuesByParameter.TryGetValue(yParameter, out List<double> values))
            {
                values = new List<double>();
            }

            return new SignalSeries(flight.Time, values);
        }

        private async Task<CachedFlight> LoadEntireFlightAsync(int masterIndex)
        {
            IAsyncCursor<TelemetrySensorFields> cursor =
                await this.telemetryMongoProxy.GetCursorFromFieldsAsync(masterIndex);

            CachedFlight flight = new CachedFlight(masterIndex);

            using (cursor)
            {
                await foreach (TelemetrySensorFields record in cursor.ToAsyncEnumerable())
                {
                    flight.Time.Add(record.Timestep);

                    if (record.Fields == null)
                    {
                        continue;
                    }

                    foreach (KeyValuePair<string, double> kvp in record.Fields)
                    {
                        string parameterName = kvp.Key;
                        double parameterValue = kvp.Value;

                        if (!flight.ValuesByParameter.TryGetValue(parameterName, out List<double> list))
                        {
                            list = new List<double>();
                            flight.ValuesByParameter[parameterName] = list;
                        }

                        list.Add(parameterValue);
                    }
                }
            }

            return flight;
        }
    }
}
