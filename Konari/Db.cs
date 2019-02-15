using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using System.Collections.Generic;
using System.Linq;
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
            availability = new Dictionary<ulong, string[]>();
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
            List<string> curr = ((string)(await R.Db(dbName).Table("Guilds").Get(guildId.ToString()).RunAsync(conn)).Availability).Split('|').ToList();
            for (int i = curr.Count; i < defaultAvailability.Split('|').Length; i++)
                curr.Add("O");
            availability.Add(guildId, curr.ToArray());
        }

        public string[] GetAvailability(ulong guildId)
            => availability[guildId];

        public string GetText(ulong guildId)
            => availability[guildId][0];

        public string GetImage(ulong guildId)
            => availability[guildId][1];

        public string GetLink(ulong guildId)
            => availability[guildId][2];

        public string GetServer(ulong guildId)
            => availability[guildId][3];

        public string GetNsfw(ulong guildId)
            => availability[guildId][4];

        public string GetTranslation(ulong guildId)
            => availability[guildId][5];

        private async Task UpdateAvailability(ulong guildId)
        {
            await R.Db(dbName).Table("Guilds").Update(R.HashMap("id", guildId.ToString())
                .With("Availability", string.Join("|", availability[guildId]))
                ).RunAsync(conn);
        }

        private async Task SetElement(ulong guildId, string content, int id)
        {
            string[] arr = availability[guildId];
            arr[id] = content;
            availability[guildId] = arr;
            await UpdateAvailability(guildId);
        }

        public async Task SetText(ulong guildId, string content)
        {
            await SetElement(guildId, content, 0);
        }

        public async Task SetImage(ulong guildId, string content)
        {
            await SetElement(guildId, content, 1);
        }

        public async Task SetLink(ulong guildId, string content)
        {
            await SetElement(guildId, content, 2);
        }

        public async Task SetServer(ulong guildId, string content)
        {
            await SetElement(guildId, content, 3);
        }

        public async Task SetNsfw(ulong guildId, string content)
        {
            await SetElement(guildId, content, 4);
        }

        public async Task SetTranslation(ulong guildId, string content)
        {
            await SetElement(guildId, content, 5);
        }

        private RethinkDB R;
        private Connection conn;
        private string dbName;
        // Text settings, image settings, links settings, api settings
        // Text/Image/Links: O (capital o) disable, X delete message, [id] chanel to report, check NSFW chans, use translation instead of native language
        // API: O disabled, X enabled
        private const string defaultAvailability = "O|O|O|O|O|O";
        private Dictionary<ulong, string[]> availability;
    }
}
