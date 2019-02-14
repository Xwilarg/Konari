﻿using Newtonsoft.Json;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using System.Collections.Generic;
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
            availability.Add(guildId, ((string)(await R.Db(dbName).Table("Guilds").Get(guildId.ToString()).RunAsync(conn)).Availability).Split('|'));
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

        private RethinkDB R;
        private Connection conn;
        private string dbName;
        // Text settings, image settings, links settings, api settings
        // Text/Image/Links: O (capital o) disable, X delete message, [id] chanel to report
        // API: O disabled, X enabled
        private const string defaultAvailability = "O|O|O|O";
        private Dictionary<ulong, string[]> availability;
    }
}
