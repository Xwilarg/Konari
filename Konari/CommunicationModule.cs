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
        [Command("Status")]
        private async Task Status(params string[] args)
        {
            string[] status = Program.P.db.GetAvailability(Context.Guild.Id);
            EmbedBuilder embed = new EmbedBuilder()
            {
                Title = "Status",
                Color = Color.Blue
            };
            embed.AddField("Text settings", await GetServiceStatus(status[0], Context.Guild));
            embed.AddField("Image settings", await GetServiceStatus(status[1], Context.Guild));
            embed.AddField("Link settings", await GetServiceStatus(status[2], Context.Guild));
            embed.AddField("Send datas", ((status[3] == "O") ? ("Disabled") : ("Enabled")));
            await ReplyAsync("", false, embed.Build());
        }

        [Command("Enable")]
        private async Task Enable(params string[] args)
        {
            if (Context.Guild.OwnerId != Context.User.Id)
            {
                await ReplyAsync("Only the owner can do this command.");
                return;
            }
            string elem = args[0];
            if (elem == "link")
            {
                await ReplyAsync("Link analysis isn't available yet.");
                return;
            }
            string action = string.Join(" ", args.Skip(1));
            if (elem == "data")
            {
                await Program.P.db.SetServer(Context.Guild.Id, "X");
                await ReplyAsync("Sending data to server was enabled.");
                return;
            }
            ITextChannel chan;
            if (action == "delete")
                chan = null;
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
            else if (elem == "link")
            {
                await Program.P.db.SetLink(Context.Guild.Id, str);
                await ReplyAsync("Link analysis " + replyStr);
            }
            else
                await ReplyAsync("Argument must be 'text', 'image', 'link' followed by 'delete' to delete message or by a channel to report them." + Environment.NewLine
                    + "Or 'data' followed by nothing to send datas to the server.");
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
            if (Context.Guild.OwnerId != Context.User.Id)
            {
                await ReplyAsync("Only the owner can do this command.");
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
            else if (elem == "link")
            {
                await Program.P.db.SetLink(Context.Guild.Id, "O");
                await ReplyAsync("Link analysis was disabled.");
            }
            else if (elem == "data")
            {
                await Program.P.db.SetServer(Context.Guild.Id, "O");
                await ReplyAsync("Sending data to server was disabled.");
            }
            else
                await ReplyAsync("Argument must be 'text', 'image', 'link' or 'data'.");
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
