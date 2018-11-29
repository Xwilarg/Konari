using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Konari
{
    class Program
    {
        public static void Main(string[] args)
                 => new Program().MainAsync().GetAwaiter().GetResult();

        public readonly DiscordSocketClient client;
        private readonly CommandService commands = new CommandService();

        private readonly string perspectiveApi;

        public DateTime StartTime { private set; get; }

        public static Program P { private set; get; }

        private Program()
        {
            P = this;
            perspectiveApi = File.ReadAllText("Keys/perspectiveAPI.txt");
            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
            });
            client.Log += DiscordUtils.Utils.Log;
            commands.Log += DiscordUtils.Utils.LogError;
        }

        private async Task MainAsync()
        {
            client.MessageReceived += HandleCommandAsync;

            await client.LoginAsync(TokenType.Bot, File.ReadAllText("Keys/token.txt"));
            StartTime = DateTime.Now;
            await client.StartAsync();

            await Task.Delay(-1);
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
            using (HttpClient hc = new HttpClient())
            {
                HttpResponseMessage post = await hc.PostAsync("https://commentanalyzer.googleapis.com/v1alpha1/comments:analyze?key=" + perspectiveApi, new StringContent(
                        JsonConvert.DeserializeObject("{comment: {text: \"" + DiscordUtils.Utils.EscapeString(msg.Content) + "\"},"
                                                    + "languages: [\"en\"],"
                                                    + "requestedAttributes: {TOXICITY:{}} }").ToString(), Encoding.UTF8, "application/json"));
                dynamic json = JsonConvert.DeserializeObject(await post.Content.ReadAsStringAsync());
                double value = json.attributeScores.TOXICITY.summaryScore.value;
                if (value > .80)
                    await msg.AddReactionAsync(new Emoji("🇪"));
                else if (value > .60)
                    await msg.AddReactionAsync(new Emoji("🇩"));
                else if (value > .40)
                    await msg.AddReactionAsync(new Emoji("🇨"));
                else if (value > .20)
                    await msg.AddReactionAsync(new Emoji("🇧"));
                else
                    await msg.AddReactionAsync(new Emoji("🇦"));
            }
        }
    }
}
