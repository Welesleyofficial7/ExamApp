using ExamApp.Models;
using MongoDB.Driver;

namespace ExamApp.Repositories;

public class MongoClientRepository : IClientRepository
{
    private readonly IMongoCollection<Client> _clients;

    public MongoClientRepository(MongoDbContext dbContext)
    {
        _clients = dbContext.GetCollection<Client>("clients");
    }

    public async Task<IEnumerable<Client>> GetAllAsync()
    {
        return await _clients.Find(client => true).ToListAsync();
    }
    
    public async Task<Client> GetClientByIdAsync(string id)
    {
        return await _clients.Find(c => c.Id == id).FirstOrDefaultAsync();
    }

    public async Task CreateClientAsync(Client client)
    {
        await _clients.InsertOneAsync(client);
    }

    public async Task UpdateClientAsync(Client client)
    {
        await _clients.ReplaceOneAsync(c => c.Id == client.Id, client);
    }

    public async Task DeleteClientAsync(string id)
    {
        await _clients.DeleteOneAsync(c => c.Id == id);
    }
}

