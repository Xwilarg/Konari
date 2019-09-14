using Discord;
using Discord.Commands;
using DiscordUtils;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Konari
{
    public class CommunicationModule : ModuleBase
    {
        [Command("Help")]
        private async Task Help(params string[] _)
        {
            await ReplyAsync("", false, new EmbedBuilder
            {
                Description =
                    "**Status**: Display current bot status" + Environment.NewLine +
                    "**Enable/Disable**: You can enable the following things:" + Environment.NewLine +
                    " - data: Send message report to a predefined server" + Environment.NewLine +
                    " - nsfw: Analyse messages in NSFW channels" + Environment.NewLine +
                    " - native: Use native language for analysis when available. If not enabled, will use Google Translate to english" + Environment.NewLine +
                    " - text: Analyse text messages" + Environment.NewLine +
                    " - image: Analyse images" + Environment.NewLine +
                    "Also when using the enable command, you must either say 'delete' for a bad message to be deleted, or give a channel id to automatically create a report inside",
                Color = Color.Blue
            }.Build());
        }

        [Command("Status")]
        private async Task Status(params string[] _)
        {
            string[] status = Program.P.db.GetAvailability(Context.Guild.Id);
            EmbedBuilder embed = new EmbedBuilder()
            {
                Title = "Status",
                Color = Color.Blue
            };
            embed.AddField("Text settings", await GetServiceStatus(status[0], Context.Guild));
            embed.AddField("Image settings", await GetServiceStatus(status[1], Context.Guild));
            embed.AddField("Send datas", ((status[2] == "O") ? ("Disabled") : ("Enabled")));
            embed.AddField("Check NSFW channels", ((status[3] == "O") ? ("Disabled") : ("Enabled")));
            embed.AddField("Use native language for analysis when available (experimental)", ((status[4] == "O") ? ("Disabled") : ("Enabled")));
            await ReplyAsync("", false, embed.Build());
        }

        [Command("Enable")]
        private async Task Enable(params string[] args)
        {
            IGuildUser guildUser = Context.User as IGuildUser;
            if (guildUser == null)
            {
                await ReplyAsync("This command can't be done in private message.");
                return;
            }
            if (!guildUser.GuildPermissions.Administrator)
            {
                await ReplyAsync("Only someone with Administrator permission can do this command.");
                return;
            }
            string elem = args[0];
            string action = string.Join(" ", args.Skip(1));
            if (elem == "data")
            {
                await Program.P.db.SetServer(Context.Guild.Id, "X");
                await ReplyAsync("Sending data to server was enabled.");
                return;
            }
            if (elem == "nsfw")
            {
                await Program.P.db.SetNsfw(Context.Guild.Id, "X");
                await ReplyAsync("Checking NSFW channel was enabled.");
                return;
            }
            if (elem == "native")
            {
                await Program.P.db.SetTranslation(Context.Guild.Id, "X");
                await ReplyAsync("Using native language when available was enabled.");
                return;
            }
            if (elem != "text" && elem != "image")
            {
                await ReplyAsync("Argument must be 'text' or 'image' followed by 'delete' to delete message or by a channel to report them." + Environment.NewLine
                    + "Or 'data' followed by nothing to send datas to the server." + Environment.NewLine
                    + "Or 'nsfw' followed by nothing to enable check in NSFW channels." + Environment.NewLine
                    + "Or 'native' followed by nothing to enable use of native language over translation when available.");
                return;
            }
            ITextChannel chan;
            if (action == "delete")
            {
                if (!((IGuildUser)(await Context.Channel.GetUserAsync(Program.P.client.CurrentUser.Id))).GuildPermissions.ManageMessages)
                {
                    await ReplyAsync("I need to have the ability to manage message for this mode.");
                    return;
                }
                chan = null;
            }
            else
            {
                chan = await Utils.GetTextChannel(action, Context.Guild);
                if (chan == null)
                {
                    await ReplyAsync("You must precise 'delete' to delete message or a channel to report them.");
                    return;
                }
            }
            string str = GetEnableString(chan);
            string replyStr = "was enabled ";
            if (chan == null)
                replyStr += "with delete action.";
            else
                replyStr += "with report in " + chan.Mention;
            if (elem == "text")
            {
                await Program.P.db.SetText(Context.Guild.Id, str);
                await ReplyAsync("Text analysis " + replyStr);
            }
            else if (elem == "image")
            {
                await Program.P.db.SetImage(Context.Guild.Id, str);
                await ReplyAsync("Image analysis " + replyStr);
            }
        }

        private string GetEnableString(ITextChannel chan)
        {
            if (chan == null)
                return ("X");
            return (chan.Id.ToString());
        }

        [Command("Disable")]
        private async Task Disable(params string[] args)
        {
            IGuildUser guildUser = Context.User as IGuildUser;
            if (guildUser == null)
            {
                await ReplyAsync("This command can't be done in private message.");
                return;
            }
            if (!guildUser.GuildPermissions.Administrator)
            {
                await ReplyAsync("Only someone with Administrator permission can do this command.");
                return;
            }
            string elem = string.Join(" ", args);
            if (elem == "text")
            {
                await Program.P.db.SetText(Context.Guild.Id, "O");
                await ReplyAsync("Text analysis was disabled.");
            }
            else if (elem == "image")
            {
                await Program.P.db.SetImage(Context.Guild.Id, "O");
                await ReplyAsync("Image analysis was disabled.");
            }
            else if (elem == "data")
            {
                await Program.P.db.SetServer(Context.Guild.Id, "O");
                await ReplyAsync("Sending data to server was disabled.");
            }
            else if (elem == "nsfw")
            {
                await Program.P.db.SetNsfw(Context.Guild.Id, "O");
                await ReplyAsync("Checking NSFW channel was disabled.");
            }
            else if (elem == "native")
            {
                await Program.P.db.SetTranslation(Context.Guild.Id, "O");
                await ReplyAsync("Using native language when available was disabled.");
            }
            else
                await ReplyAsync("Argument must be 'text', 'image', 'data', 'nsfw' or 'native'.");
        }

        private async Task<string> GetServiceStatus(string current, IGuild guild)
        {
            if (current == "O")
                return ("Disabled");
            if (current == "X")
                return ("Enabled (Message are deleted)");
            ITextChannel chan = await guild.GetTextChannelAsync(ulong.Parse(current));
            if (chan == null)
                return ("Disabled (Deleted channel)");
            return ("Enabled (report in " + chan.Mention + ")");
        }
    }
}
