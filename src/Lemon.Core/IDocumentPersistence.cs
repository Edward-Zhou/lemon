﻿using MongoDB.Bson;
using System.Collections.Generic;

namespace Lemon.Core
{
    public interface IDocumentPersistence
    {
        void Persist(BsonDocument document);
    }
}
