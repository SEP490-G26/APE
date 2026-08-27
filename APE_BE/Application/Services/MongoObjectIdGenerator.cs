using Application.Interfaces;
using MongoDB.Bson;

namespace Application.Services
{
    public sealed class MongoObjectIdGenerator : IIdGenerator
    {
        public string NewId()
        {
            return ObjectId.GenerateNewId().ToString();
        }
    }
}
