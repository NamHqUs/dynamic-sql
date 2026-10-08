using Namh.Data.DynamicSql.Internal.Query.Models;
using System.Collections;
using System.Data;
using System.Data.Common;

namespace Namh.Data.DynamicSql.Internal.Query;

class DbReader(
    IDataReader reader,
    IEnumerable<ProjectionItem> projectionItems,
    bool isResultInHierarchy = false,
    Action<DataRecord>? onProcessDataRecord = null,
    Func<string, object, object>? onProcessDataField = null
    ) : IEnumerable<DataRecord>, IEnumerable
{
    #region Inner Classes

    class FieldMap(int dbColumnIndex, string fieldName)
    {
        public readonly int DbColumnIndex = dbColumnIndex;
        public readonly string FieldName = fieldName;
        public Dictionary<string, FieldMap>? Children;
    };

    class Enumerator(IDataReader reader,
            IEnumerable<ProjectionItem> projectionItems,
            bool isResultInHierarchy = false,
            Action<DataRecord>? onProcessDataRecord = null,
            Func<string, object, object>? onProcessDataField = null
            ) : IEnumerator<DataRecord>, IEnumerator, IDisposable
    {
        private readonly Lazy<Dictionary<string, FieldMap>> _dbMapping = new(() => BuildMapping(projectionItems, isResultInHierarchy));
        public DataRecord Current { get; private set; } = default!;

        object IEnumerator.Current => this.Current;

        public bool MoveNext()
        {
            if (!reader.Read())
                return false;

            var record = ReadRecord(reader, _dbMapping.Value);

            onProcessDataRecord?.Invoke(record);

            this.Current = record;
            return true;
        }

        public void Reset() { }

        public void Dispose()
            => reader.Dispose();

        internal static DataRecord ReadRecord(IDataReader reader, Dictionary<string, FieldMap> map,
            Action<DataRecord>? onProcessDataRecord = null,
            Func<string, object, object>? onProcessDataField = null)
        {
            var record = new DataRecord();
            foreach (var e in map)
            {
                var fieldMap = e.Value;
                object val = fieldMap.Children == null 
                    ? reader.GetValue(fieldMap.DbColumnIndex) 
                    : ReadRecord(reader, fieldMap.Children);

                record[e.Key] =
                    val == DBNull.Value 
                        ? null 
                        : val is DataRecord || onProcessDataField == null 
                            ? val! 
                            : onProcessDataField(fieldMap.FieldName, val);
            }
            return record;
        }

        internal static Dictionary<string, FieldMap> BuildMapping(IEnumerable<ProjectionItem> projectionItems, bool isResultInHierarchy)
        {
            var mapRoot = new Dictionary<string, FieldMap>();
            var colIndex = 0;

            foreach (var e in projectionItems)
            {
                if (isResultInHierarchy)
                {
                    var eles = e.Alias.Split('.');

                    FieldMap? fieldMap = null;
                    foreach (var propName in eles)
                    {
                        var current = fieldMap == null ? mapRoot : (fieldMap.Children ??= []);

                        current[propName] = fieldMap = current.TryGetValue(propName, out fieldMap) ? fieldMap : new FieldMap(colIndex, e.Item.ToString());
                    }
                }
                else
                    mapRoot[e.Alias] = new FieldMap(colIndex, e.Item.ToString());

                colIndex++;
            }

            return mapRoot;
        }
    }

    #endregion

    public IEnumerator<DataRecord> GetEnumerator()
        => new Enumerator(reader, projectionItems, isResultInHierarchy, onProcessDataRecord, onProcessDataField);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    internal static async Task<List<DataRecord>> ReadAllAsync(
        DbDataReader reader,
        IEnumerable<ProjectionItem> projectionItems,
        CancellationToken cancellationToken)
    {
        var mapping = Enumerator.BuildMapping(projectionItems, false);
        var records = new List<DataRecord>();

        while (await reader.ReadAsync(cancellationToken))
            records.Add(Enumerator.ReadRecord(reader, mapping));

        return records;
    }
}
