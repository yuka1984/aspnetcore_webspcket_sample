using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using WebSocketChatSample.Models.Poco;

namespace WebSocketChatSample.Models
{
    public interface IRoomService
    {
        IEnumerable<Room> GetRooms();
    }

    public class DebugRoomService : IRoomService
    {
        public IEnumerable<Room> GetRooms()
        {
            return Enumerable.Range(1, 10).Select(x => new Room { Id =  x , Name = $"Room{x}"});
        }
    }
}
