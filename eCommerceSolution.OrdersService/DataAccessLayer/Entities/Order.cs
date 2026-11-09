using MongoDB.Bson.Serialization.Attributes;   //importa gli attributi (per dire al driver come serializzare e deserializzare le proprietà della classe) del driver MongoDB per .NET. la conversione avviene solo quando il driver legge o salva il documento.


namespace DataAccessLayer.Entities
{
    public class Order
    {
        [BsonId]  //mongodb vuole un _id per ogni documento
        [BsonRepresentation(MongoDB.Bson.BsonType.String)]   //Guid -> stringa bson
        public Guid _id { get; set; }

        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public Guid OrderID { get; set; }

        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public Guid UserID { get; set; }

        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public DateTime OrderDate { get; set; }

        [BsonRepresentation(MongoDB.Bson.BsonType.Double)]
        public decimal TotalBill { get; set; }

        public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
