﻿using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Lemon.Transform
{
    public class MSSQLDataInput : AbstractDataInput, IDisposable
    {
        private SqlConnection _connection;

        private string _connectionString;

        private string _sql;

        private bool _limitSpeed;

        private long _speed;

        private long _count;

        private IDictionary<string, object> _parameters;

        public MSSQLDataInput(DataInputModel model)
        {
            var dictionary = new Dictionary<string, string>();

            var attributes = model.Connection.Split(';');

            foreach (var attribute in attributes)
            {
                var splits = attribute.Split('=');

                var key = splits[0];

                var value = splits[1];

                dictionary.Add(key, value);
            }

            _limitSpeed = dictionary.ContainsKey("Speed");

            if(_limitSpeed)
            {
                dictionary.Remove("Speed");
            }

            _connectionString = string.Join(";", dictionary.Select(m => string.Format("{0}={1}", m.Key, m.Value)));

            _sql = SqlNamedQueryProvider.Instance.Get(model.ObjectName);

            _parameters = Newtonsoft.Json.JsonConvert.DeserializeObject<IDictionary<string, object>>(model.Filter);

            _connection = new SqlConnection(_connectionString);
        }

        private class FieldTypeMapping
        {
            public int Ordinal { get; set; }

            public string FieldName { get; set; }

            public Type DataType { get; set; }
        }

        public void ForEach(Action<BsonDataRow> forEach)
        {
            SqlCommand command = new SqlCommand(_sql, _connection);

            foreach(var parameter in _parameters)
            {
                command.Parameters.Add(new SqlParameter(parameter.Key, parameter.Value));
            }

            _connection.Open();

            SqlDataReader reader = command.ExecuteReader();

            IList<FieldTypeMapping> mappings = new List<FieldTypeMapping>();

            var table = reader.GetSchemaTable();

            foreach(DataRow row in table.Rows)
            {
                var name = row.Field<string>(0);

                var ordinal = row.Field<int>(1);

                var type = row.Field<Type>(12);

                mappings.Add(new FieldTypeMapping { Ordinal = ordinal, FieldName = name, DataType = type });
            }

            while (reader.Read())
            {
                var document = new BsonDocument();

                foreach (FieldTypeMapping column in mappings)
                {
                    if(reader.IsDBNull(column.Ordinal))
                    {
                        document.Add(column.FieldName, BsonNull.Value);
                    }
                    else
                    {
                        var value = reader.GetValue(column.Ordinal);

                        document.Add(column.FieldName, Cast(value, column.DataType));
                    }
                }

                forEach(new BsonDataRow(document));

                _count++;

                if (_limitSpeed && (_count % _speed) == 0)
                {
                    System.Threading.Thread.Sleep(1000);
                }
            }

            reader.Close();
        }

        private static BsonValue Cast(object dotNetValue,Type sourceType)
        {
            if(sourceType == ObjectType.String)
            {
                return new BsonString(dotNetValue as string);
            }
            else if (sourceType == ObjectType.Int32)
            {
                return new BsonInt32((int)dotNetValue);
            }
            else if (sourceType == ObjectType.Int64)
            {
                return new BsonInt64((Int64)dotNetValue);
            }
            else if (sourceType == ObjectType.Boolean)
            {
                return new BsonBoolean((bool)dotNetValue);
            }else if (sourceType == ObjectType.DateTime)
            {
                return new BsonDateTime((DateTime)dotNetValue);
            }

            throw new NotSupportedException(string.Format("Unable to cast type {0} to BsonValue", sourceType.FullName));
        }

        public override void Start()
        {
            _count = 0;

            ForEach(Post);

            Complete();
        }

        public void Dispose()
        {
            _connection.Close();
        }
    }
}
