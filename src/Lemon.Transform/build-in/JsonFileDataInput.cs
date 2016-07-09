﻿using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.IO;

namespace Lemon.Transform
{
    public class JsonFileDataInput : AbstractDataInput
    {
        private string _primaryKey;

        private string _filePath;

        private long _count;

        private bool _limitSpeed;

        private long _speed; 

        public string PrimaryKey
        {
            get
            {
                return _primaryKey;
            }
        }

        public JsonFileDataInput(DataInputModel model)
        {
            var dictionary = new Dictionary<string, string>();

            var attributes = model.Connection.Split(';');

            foreach (var attribute in attributes)
            {
                var temp = attribute.Split('=');

                var key = temp[0];

                var value = temp[1];

                dictionary.Add(key, value);
            }

            _filePath = dictionary["FilePath"];

            _limitSpeed = dictionary.ContainsKey("Speed");

            if(_limitSpeed)
            {
                _speed = long.Parse(dictionary["Speed"]);
            }

            _primaryKey = model.PrimaryKey;
        }

        public void ForEach(Action<BsonDataRow> forEach)
        {
            using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read))
            using (var sr = new StreamReader(fs))
            {
                var documents = new List<BsonDocument>();

                while (!sr.EndOfStream)
                {
                    try
                    {
                        var text = sr.ReadLine();

                        var document = BsonDocument.Parse(text);

                        forEach(new BsonDataRow(document));

                        _count++;

                        if(_limitSpeed && (_count % _speed) == 0)
                        {
                            System.Threading.Thread.Sleep(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.Default.Error("Json Data Input failed", ex);
                    }
                }
            }
        }

        public override void Start()
        {
            _count = 0;

            ForEach(Post);

            Complete();
        }
    }
}
