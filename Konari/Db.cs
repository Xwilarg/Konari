using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using System.Threading.Tasks;

namespace Konari
{
    public class Db
    {
        public Db()
        {
            R = RethinkDB.R;
        }

        public async Task InitAsync(string dbName = "Konari")
        {
            this.dbName = dbName;
            conn = await R.Connection().ConnectAsync();
            if (!await R.DbList().Contains(dbName).RunAsync<bool>(conn))
                R.DbCreate(dbName).Run(conn);
            if (!await R.Db(dbName).TableList().Contains("Guilds").RunAsync<bool>(conn))
                R.Db(dbName).TableCreate("Guilds").Run(conn);
        }

        public async Task InitGuild(ulong guildId)
        {
            string guildIdStr = guildId.ToString();
            if (await R.Db(dbName).Table("Guilds").GetAll(guildIdStr).Count().Eq(0).RunAsync<bool>(conn))
            {
                await R.Db(dbName).Table("Guilds").Insert(R.HashMap("id", guildIdStr)
                    .With("Availability", defaultAvailability)
                    ).RunAsync(conn);
            }
        }

        private RethinkDB R;
        private Connection conn;
        private string dbName;
        // Text settings, image settings, links settings, api settings
        // Text/Image/Links: O (capital o) disable, X delete message, [id] chanel to report
        // API: O disabled, X enabled
        private const string defaultAvailability = "O|O|O|O";
    }
}
