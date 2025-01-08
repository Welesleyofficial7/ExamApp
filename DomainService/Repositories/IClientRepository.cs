using ExamApp.Models;

namespace ExamApp.Repositories;

using System.Threading.Tasks;

public interface IClientRepository
{
    Task<IEnumerable<Client>> GetAllAsync();
    Task<Client> GetClientByIdAsync(string id);
    Task CreateClientAsync(Client client);
    Task UpdateClientAsync(Client client);
    Task DeleteClientAsync(string id);
}

