using DataAccessLayer.Entities;
using DataAccessLayer.RepositoryContracts;
using MongoDB.Driver;

namespace DataAccessLayer.Repositories
{
    public class OrdersRepository : IOrdersRepository
    {
        private readonly IMongoCollection<Order> _orders;  //collection MongoDB i cui documenti vengono mappati sulla classe C# Order
        private readonly string collectionName = "orders";

        public OrdersRepository(IMongoDatabase mongoDatabase)  //quando il framework crea un'istanza di OrdersRepository, il costruttore riceve un oggetto IMongoDatabase
        {
            _orders = mongoDatabase.GetCollection<Order>(collectionName);  //chiedi al db un riferimento alla collection chiamata orders, mappata sul tipo C# Order
        }


        public async Task<Order?> AddOrder(Order order)
        {
            order.OrderID = Guid.NewGuid();
            order._id = order.OrderID;  //override, di solito non si fa cmnq stai ancora imparando gli mss
            foreach (OrderItem orderItem in order.OrderItems)
            {
                orderItem._id = Guid.NewGuid();  //update
            }
            await _orders.InsertOneAsync(order);  //save on mongodb
            return order;
        }


        public async Task<bool> DeleteOrder(Guid orderID)
        {
            FilterDefinition<Order> filter = Builders<Order>.Filter.Eq(temp => temp.OrderID, orderID);  //Builders<Order>.Filter è un insieme di strumenti del driver per costruire filtri MongoDB
            Order? existingOrder = (await _orders.FindAsync(filter)).FirstOrDefault(); //applica il filter e prendi primo result / null
            if (existingOrder == null)
            {
                return false;
            }
            DeleteResult deleteResult = await _orders.DeleteOneAsync(filter);
            return deleteResult.DeletedCount > 0;
        }


        public async Task<Order?> GetOrderByCondition(FilterDefinition<Order> filter)
        {
            return (await _orders.FindAsync(filter)).FirstOrDefault();
        }


        public async Task<IEnumerable<Order>> GetOrders()
        {
            return (await _orders.FindAsync(Builders<Order>.Filter.Empty)).ToList();  //Filter.empty non applica nessun filtro, quindi return all
        }


        public async Task<IEnumerable<Order?>> GetOrdersByCondition(FilterDefinition<Order> filter)
        {
            return (await _orders.FindAsync(filter)).ToList();
        }


        public async Task<Order?> UpdateOrder(Order order)
        {
            FilterDefinition<Order> filter = Builders<Order>.Filter.Eq(temp => temp.OrderID, order.OrderID);

            Order? existingOrder = (await _orders.FindAsync(filter)).FirstOrDefault();

            if (existingOrder == null)
            {
                return null;
            }
            order._id = existingOrder._id;

            ReplaceOneResult replaceOneResult = await _orders.ReplaceOneAsync(filter, order);  //ReplaceOneAsync sostituisce l'intero documento corrispondente al filtro con la rappresentazione BSON dell'oggetto passato

            return order;
        }
    }


}
