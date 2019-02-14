using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordUtils;
using Google.Cloud.Translation.V2;
using Google.Cloud.Vision.V1;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Konari
{
    class Program
    {
        public static void Main(string[] args)
                 => new Program().MainAsync().GetAwaiter().GetResult();

        public readonly DiscordSocketClient client;
        private readonly CommandService commands = new CommandService();

        public DateTime StartTime { private set; get; }

        public static Program P { private set; get; }

        public Random rand { private set; get; }
        public string perspectiveApi { private set; get; }
        public ImageAnnotatorClient imageClient { private set; get; }
        public TranslationClient translationClient { private set; get; }
        public string requestUrlText { private set; get; }
        public string requestUrlImage { private set; get; }
        public string requestToken { private set; get; }

        private readonly string websiteName;
        private readonly string websiteToken;

        public Db db { private set; get; }

        private Program()
        {
            P = this;
            perspectiveApi = File.ReadAllText("Keys/perspectiveAPI.txt");
            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
            });
            client.Log += Utils.Log;
            commands.Log += Utils.LogError;
            string[] request = File.ReadAllLines("Keys/url.txt");
            requestUrlText = request[0];
            requestUrlImage = request[1];
            requestToken = request[2];
            rand = new Random();
            db = new Db();

            string[] websiteInfos = File.ReadAllLines("Keys/website.txt");
            websiteName = websiteInfos[0];
            websiteToken = websiteInfos[1];

            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "Keys/imageAPI.json");
            imageClient = ImageAnnotatorClient.Create();

            translationClient = TranslationClient.Create();
        }

        private async Task MainAsync()
        {
            await db.InitAsync();

            client.MessageReceived += HandleCommandAsync;
            client.GuildAvailable += GuildJoin;
            client.JoinedGuild += GuildJoin;

            await commands.AddModuleAsync<CommunicationModule>();

            await client.LoginAsync(TokenType.Bot, File.ReadAllText("Keys/token.txt"));
            StartTime = DateTime.Now;
            await client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task GuildJoin(SocketGuild arg)
        {
            await db.InitGuild(arg.Id);
        }

        private async Task<ITextChannel> GetTextChannel(string str, IGuild guild)
        {
            if (str == "O" || str == "X")
                return (null);
            return (await guild.GetTextChannelAsync(ulong.Parse(str)));
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            SocketUserMessage msg = arg as SocketUserMessage;
            if (msg == null || arg.Author.IsBot) return;
            int pos = 0;
            if (msg.HasMentionPrefix(client.CurrentUser, ref pos) || msg.HasStringPrefix("k.", ref pos))
            {
                SocketCommandContext context = new SocketCommandContext(client, msg);
                if ((await commands.ExecuteAsync(context, pos)).IsSuccess)
                    await Utils.WebsiteUpdate("Konari", websiteName, websiteToken, "nbMsgs", "1");
            }
#pragma warning disable 4014
            ITextChannel textChan = arg.Channel as ITextChannel;
            if (textChan == null)
                return;
            ulong guildId = textChan.GuildId;
            string textVal = db.GetText(guildId);
            string imageVal = db.GetImage(guildId);
            string serverVal = db.GetServer(guildId);
            ITextChannel textReport = await GetTextChannel(textVal, textChan.Guild);
            ITextChannel imageReport = await GetTextChannel(imageVal, textChan.Guild);
            if (textVal != "O")
            {
                Task.Run(async () =>
                {
                    var tmp = await Analyze.CheckText(msg, arg, textReport);
                    if (serverVal != "O")
                        await Analyze.SendToServerAsync(tmp, requestUrlText, arg.Author.Id.ToString());
                });
            }
            if (imageVal != "O")
            {
                Task.Run(async () =>
                {
                    var tmp = await Analyze.CheckImage(msg, arg, imageReport);
                    if (serverVal != "O")
                        await Analyze.SendToServerAsync(tmp, requestUrlImage, arg.Author.Id.ToString());
                });
                Task.Run(async () =>
                {
                    foreach (Match m in Regex.Matches(msg.Content, "https?:\\/\\/[^ ]+"))
                    {
                        if (Utils.IsLinkValid(m.Value))
                        {
                            var tmp = await Analyze.CheckImageUrl(m.Value, msg, arg, imageReport);
                            if (serverVal != "O" && await Analyze.SendToServerAsync(tmp, requestUrlImage, arg.Author.Id.ToString()))
                                break;
                        }
                    }
                });
            }
#pragma warning restore 4014
        }
    }
}
