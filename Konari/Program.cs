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

        private Db db;

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

            await client.LoginAsync(TokenType.Bot, File.ReadAllText("Keys/token.txt"));
            StartTime = DateTime.Now;
            await client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task GuildJoin(SocketGuild arg)
        {
            await db.InitGuild(arg.Id);
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            SocketUserMessage msg = arg as SocketUserMessage;
            if (msg == null || arg.Author.IsBot) return;
            int pos = 0;
            if (msg.HasMentionPrefix(client.CurrentUser, ref pos) || msg.HasStringPrefix("k.", ref pos))
            {
                SocketCommandContext context = new SocketCommandContext(client, msg);
                await commands.ExecuteAsync(context, pos);
            }
#pragma warning disable 4014
            Task.Run(async () =>
            {
                await Analyze.SendToServerAsync(await Analyze.CheckText(msg, arg), requestUrlText, arg.Author.Id.ToString());

            });
            Task.Run(async () =>
            {
                await Analyze.SendToServerAsync(await Analyze.CheckImage(msg, arg), requestUrlImage, arg.Author.Id.ToString());
            });
            Task.Run(async () =>
            {
                foreach (Match m in Regex.Matches(msg.Content, "https?:\\/\\/[^ ]+"))
                {
                    if (Utils.IsLinkValid(m.Value))
                        if (await Analyze.SendToServerAsync(await Analyze.CheckImageUrl(m.Value, msg, arg), requestUrlImage, arg.Author.Id.ToString()))
                            break;
                }
            });
#pragma warning restore 4014
        }
    }
}
