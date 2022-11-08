using Discord.Commands;
using Discord.WebSocket;
using Discord;
using FAQBot.Models;
using Microsoft.Extensions.Configuration;
using System.Text;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Drawing;
using Color = Discord.Color;
using static System.Net.Mime.MediaTypeNames;
using System.Reactive.Disposables;
using System.Reflection.Metadata.Ecma335;
using System;

namespace FAQBot
{
    internal class FAQBot
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandHandler _commands;
        private readonly FAQDB _db;
        private readonly IConfiguration _config;
        private const ulong ROLE_TRUE_PIRATE_ID = 968880274517655612;
        private const ulong ROLE_LACKEY_ID = 1038517985503100948;
        private const ulong ROLE_SCALLYWAG_ID = 940073182042411068;
        private const ulong PIRATES_GUILD_ID = 940071768679395369;
        private const ulong GENERAL_CHANNEL_ID = 940071768679395371;
        private List<FAQEntry> _faq;

        public FAQBot(IConfiguration config, FAQDB db)
        {
            _db = db;
            _config = config;
            _faq = new();
            _client = new DiscordSocketClient(new DiscordSocketConfig { MessageCacheSize = 100, GatewayIntents = GatewayIntents.All });
            _commands = new CommandHandler(_client, new CommandService(new CommandServiceConfig() { DefaultRunMode = RunMode.Async }));
        }

        public async Task MainAsync(string[] args)
        {
            Console.WriteLine("Fetching FAQ DB!");
            _faq = await _db.FAQs
                .Include(faq => faq.Tags)
                .ToListAsync();
            Console.WriteLine($"Fetched {_faq.Count} rows.");
            _client.Log += Log;
            var token = _config.GetValue<string>("DiscordToken");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await _commands.InstallCommandsAsync();
            _client.MessageReceived += MessageReceived;
            _client.Ready += InitiateSlashCommands;
            _client.SlashCommandExecuted += SlashCommandExecuted;
            _client.UserJoined += UserJoined;
            await Task.Delay(-1);
        }

        private async Task UserJoined(SocketGuildUser arg)
        {
            await arg.AddRoleAsync(ROLE_SCALLYWAG_ID);
            var generalChannel = _client.GetGuild(PIRATES_GUILD_ID).GetChannel(GENERAL_CHANNEL_ID);
            
            await arg.Guild.DefaultChannel.SendMessageAsync(embed: new EmbedBuilder()
            {
                Title = $"YARGH SCALLYWAG!",
                Description = $"Welcome to the open sea <@${arg.Id}>!\nKeep your limbs inside the boat or you might lose one, ahaha!\nBide your time and gather favour of the crew and you might even become Lackey one day, ahahah!\nBut i doubt it. To work SCALLYWAG!",
                Color = Color.Red
            }.Build());
        }

        private async Task MessageReceived(SocketMessage arg)
        {
            var msg = arg as SocketUserMessage;
            if (msg is null || msg.Author.IsBot)
            {
                return;
            }

            if (msg.Content.Contains(_client.CurrentUser.Mention.Replace("!", "")))
            {
                await msg.ReplyAsync("Botswain takes commands from noone!\nUnless you use the right ones, try /botswainhelp");
            }
        }

        private async Task SlashCommandExecuted(SocketSlashCommand command)
        {
            try
            {
                switch (command.CommandName)
                {
                    case "botswainhelp":
                        await FAQHelpCommand(command);
                        break;
                    case "faq":
                        await FAQCommand(command);
                        break;
                    case "faqlist":
                        await FAQListCommand(command);
                        break;
                    case "faqadd":
                        await FAQAddCommand(command);
                        break;
                    case "faqedit":
                        await FAQEditCommand(command);
                        break;
                    case "faqaddtag":
                        await FAQAddTagCommand(command);
                        break;
                    case "faqedittag":
                        await FAQEditTagCommand(command);
                        break;
                    case "lackeyjoin":
                        await LackeyJoinCommand(command);
                        break;
                    case "lackeyapprove":
                        await LackeyApproveCommand(command);
                        break;
                    case "lackeylist":
                        await LackeyListCommand(command);
                        break;
                    default:
                        await command.RespondAsync($"{command.CommandName} is not implemented yet.", ephemeral: true);
                        break;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private async Task LackeyListCommand(SocketSlashCommand command)
        {
            var embeds = new List<Embed>();
            embeds.Add(new EmbedBuilder()
            {
                Title = $"Lackey applicants",
                Description = $"Scallywags that applied to be approved for the role of Lackey.\nApplications have 24 hours to get approved by True Pirates.",
                Color = Color.Blue
            }.Build());
            var applications = await _db.LackeyApplications
                .Include(app => app.Approvals)
                .ToListAsync();
            bool old = false;
            if (command.Data.Options.FirstOrDefault(op => op.Name == "old")?.Value is not null)
                old = bool.Parse(command.Data.Options.FirstOrDefault(op => op.Name == "old")?.Value.ToString());
            foreach (var app in applications.Where(a => (old is false && IsLackeyApplicationPending(a)) || old))
            {
                StringBuilder approvals = new();
                foreach (var approval in app.Approvals)
                {
                    approvals.Append($"<@{approval.ApproverId}>, ");
                }
                embeds.Add(new EmbedBuilder()
                {
                    Title = $"#{app.Id} {(IsLackeyApplicationApproved(app) ? "Approved!" : IsLackeyApplicationPending(app) ? $"Pending approval, closing <t:{(int)app.ApplicationTime.AddDays(1).Subtract(new DateTime(1970, 1, 1)).TotalSeconds}:R>!" : "Unapproved!")}",
                    Description = $"Applicant: <@{app.ApplicantId}>\nApprovals: {app.Approvals.Count}/3\nApproved by {approvals}",
                    Color = (IsLackeyApplicationApproved(app) ? Color.Green : IsLackeyApplicationPending(app) ? Color.Blue : Color.Red)
                }.Build());
            }

            await command.RespondAsync(embeds: embeds.ToArray(), ephemeral: IsCommandEphemeral(command.Data.Options));
        }

        private static bool IsLackeyApplicationPending(LackeyApplication app)
        {
            return app.ApplicationTime >= DateTime.UtcNow.AddDays(-1);
        }

        private static bool IsLackeyApplicationApproved(LackeyApplication app)
        {
            return app.Approvals.Where(a => a.TimeStamp >= app.ApplicationTime.AddDays(-1)).Count() > 3;
        }

        private async Task LackeyApproveCommand(SocketSlashCommand command)
        {
            if (await IsTruePirateAsync(command) is false)
                return;
            if (int.TryParse(command.Data.Options.FirstOrDefault(op => op.Name == "id")?.Value.ToString(), out int applicationId))
            {
                var application = await _db.LackeyApplications
                    .Where(app => app.Id == applicationId)
                    .Include(app => app.Approvals)
                    .FirstOrDefaultAsync();
                if(application is null)
                {
                    await command.RespondAsync($"Application doesn't exist.", ephemeral: true);
                    return;
                }
                if(application.Approvals.Any(app => app.ApproverId == command.User.Id))
                {
                    await command.RespondAsync($"**No cheating!**\nYou've already approved this application.", ephemeral: IsCommandEphemeral(command.Data.Options));
                    return;
                }
                if (application.ApplicationTime <= DateTime.UtcNow.AddDays(-1))
                {
                    await command.RespondAsync($"You're out of thyme.");
                    return;
                }
                application.Approvals.Add(new()
                {
                    ApproverId = command.User.Id,
                    TimeStamp = DateTime.UtcNow,
                });
                List<Embed> embeds = new()
                {
                    new EmbedBuilder()
                    {
                        Title = $"Approved!",
                        Description = $"<@{command.User.Id}> approved of <@{application.ApplicantId}>!",
                        Color = Color.Green
                    }.Build()
                    
                };
                if(IsLackeyApplicationApproved(application))
                {
                    var guildApplicant = (await _client.GetUserAsync(application.ApplicantId) as IGuildUser);
                    await guildApplicant.AddRoleAsync(ROLE_LACKEY_ID);
                    embeds.Add(new EmbedBuilder()
                    {
                        Title = $"Welcome to the Lackeys <@{application.ApplicantId}>",
                        Description = $"You're now one step closer to becoming a TRUE pirate.\nKeep going!",
                        ImageUrl = $"https://media.discordapp.net/attachments/1018925222449135718/1038813955780530218/unknown.png?width=1183&height=676"
                    }.Build());
                }
                else
                {
                    embeds.Add(new EmbedBuilder()
                    {
                        Title = $"#{application.Id} {(IsLackeyApplicationApproved(application) ? "Approved!" : IsLackeyApplicationPending(application) ? $"Pending approval, closing <t:{(int)application.ApplicationTime.AddDays(1).Subtract(new DateTime(1970, 1, 1)).TotalSeconds}:R>!" : "Unapproved!")}",
                        Description = $"Applicant: <@{application.ApplicantId}>\nApprovals: {application.Approvals.Count}/3\nApproved by {application.Approvals}",
                        Color = (IsLackeyApplicationApproved(application) ? Color.Green : IsLackeyApplicationPending(application) ? Color.Blue : Color.Red)
                    }.Build());
                }
                await command.RespondAsync(embeds: embeds.ToArray(), ephemeral: IsCommandEphemeral(command.Data.Options));
                await _db.SaveChangesAsync();
            }
            else
            {
                await command.RespondAsync($"Couldn't approve application.", ephemeral: true);
            }
        }

        private async Task LackeyJoinCommand(SocketSlashCommand command)
        {
            var prevApp = await _db.LackeyApplications.Where(app => app.ApplicantId == command.User.Id).OrderBy(app => app.Id).LastOrDefaultAsync();
            if (prevApp is not null && prevApp.ApplicationTime <= DateTime.UtcNow.AddDays(-7))
            {
                await command.RespondAsync($"You've already applied to be a lackey in the past week.");
                return;
            }
            if (await IsPirateLakceyOrTruePirateAsync(command))
            {
                await command.RespondAsync($"You're already in, dummy!");
                return;
            }
            await _db.LackeyApplications.AddAsync(new()
            {
                ApplicantId = command.User.Id,
                ApplicationTime = DateTime.UtcNow
            });
            Embed embed = new EmbedBuilder()
            {
                Timestamp = DateTime.UtcNow,
                Title = $"Your application is in!",
                Description = $"You've got 24 hours to get approvals from True Pirates.\n3 approvals are required to be a lackey.\nBeing a lackey does not mean you're in the guild, to be in the guild you need to become a True Pirate!",
                ImageUrl = $"https://cdn.discordapp.com/attachments/1018925222449135718/1038818925317730325/unknown.png"
            }.Build();
            await _db.SaveChangesAsync();
            await command.RespondAsync(embed: embed);

        }

        private async Task FAQEditTagCommand(SocketSlashCommand command)
        {
            if (await IsTruePirateAsync(command) is false)
                return;
            if (int.TryParse(command.Data.Options.FirstOrDefault(op => op.Name == "id")?.Value.ToString(), out int tagId))
            {
                string tag = command.Data.Options.FirstOrDefault(op => op.Name == "tag")?.Value.ToString();
                var oldTag = await _db.FAQsTags
                    .Where(f => f.Id == tagId)
                    .FirstOrDefaultAsync();
                string oldTagTag = oldTag.Tag;
                oldTag.Tag = tag;
                await _db.SaveChangesAsync();
                await command.RespondAsync($"Changed Tag #{tagId} from {oldTagTag} to {tag}.", ephemeral: IsCommandEphemeral(command.Data.Options));
            }
            else
            {
                await command.RespondAsync($"Couldn't find FAQ.", ephemeral: true);
            }
        }

        private async Task FAQAddTagCommand(SocketSlashCommand command)
        {
            if (await IsTruePirateAsync(command) is false)
                return;
            if(int.TryParse(command.Data.Options.FirstOrDefault(op => op.Name == "id")?.Value.ToString(), out int faqId))
            {
                string tag = command.Data.Options.FirstOrDefault(op => op.Name == "tag")?.Value.ToString();
                var faq = await _db.FAQs
                    .Where(f => f.Id == faqId)
                    .Include(f => f.Tags)
                    .FirstOrDefaultAsync();
                faq.Tags.Add(new FAQTag() { Tag = tag });
                await _db.SaveChangesAsync();
                await command.RespondAsync($"Added Tag {tag} to FAQ Entry #{faq.Id} {faq.Name}.", ephemeral: IsCommandEphemeral(command.Data.Options));
            }
            else
            {
                await command.RespondAsync($"Couldn't find FAQ.", ephemeral: true);
            }
        }

        private async Task FAQHelpCommand(SocketSlashCommand command)
        {
            var embed = new EmbedBuilder
            {
                Title = $"Botswain",
                Description = $"Botswain can list FAQ entries and handle lackey approval requests.\n" +
                $"FAQ entries get served to users through the /faq command.\n" +
                $"To apply to be a Perilous Pirates Lackey use the /lackeyjoin command."
            };
            embed.AddField($"1", $"faq", true);
            embed.AddField($"2", $"faqhelp", true);
            embed.AddField($"3", $"faqhelp", true);
            embed.AddField($"4", $"lackeyjoin", true);
            embed.AddField($"5", $"lackeylist", true);
            embed.AddField($"10 (TRUE PIRATE)", $"faqadd", true);
            embed.AddField($"11 (TRUE PIRATE)", $"faqedit", true);
            embed.AddField($"12 (TRUE PIRATE)", $"faqaddtag", true);
            embed.AddField($"13 (TRUE PIRATE)", $"faqedittag", true);
            embed.AddField($"15 (TRUE PIRATE)", $"lackeyapprove", true);

            await command.RespondAsync(embed: embed.Build(), ephemeral: IsCommandEphemeral(command.Data.Options));
        }

        private static bool IsCommandEphemeral(IReadOnlyCollection<SocketSlashCommandDataOption> options)
        {
            string ephemeral = options.FirstOrDefault(op => op.Name == "hidden")?.Value.ToString();
            if (ephemeral is null)
                return true;
            if (bool.TryParse(ephemeral, out bool result))
            {
                return result;
            }
            else
            {
                return true;
            }
        }

        private async Task<bool> IsPirateLakceyOrTruePirateAsync(SocketSlashCommand command)
        {
            var piratesGuild = _client.GetGuild(PIRATES_GUILD_ID);
            var piratesUser = piratesGuild.Users.FirstOrDefault(user => user.Id == command.User.Id);
            if (piratesUser == null)
            {
                await command.RespondAsync($"Couldn't find user.", ephemeral: true);
                return false;
            };
            return piratesUser.Roles.Any(role => role.Id == ROLE_TRUE_PIRATE_ID || role.Id == ROLE_LACKEY_ID);
        }

        private async Task<bool> IsTruePirateAsync(SocketSlashCommand command)
        {
            var piratesGuild = _client.GetGuild(PIRATES_GUILD_ID);
            var piratesUser = piratesGuild.Users.FirstOrDefault(user => user.Id == command.User.Id);
            if (piratesUser == null)
            {
                await command.RespondAsync($"Couldn't find user.", ephemeral: true);
                return false;
            };
            bool allowed = piratesUser.Roles.Any(role => role.Id == ROLE_TRUE_PIRATE_ID);

            if (allowed is false)
            {
                await command.RespondAsync($"You're not a TRUE pirate.", ephemeral: true);
            };
            return allowed;
        }

        private async Task FAQEditCommand(SocketSlashCommand command)
        {
            if (await IsTruePirateAsync(command) is false)
                return;
            try
            {
                var subCommand = command.Data.Options.First();
                string id = subCommand.Options.FirstOrDefault(op => op.Name == "id")?.Value.ToString();

                var oldFaq = await _db.FAQs
                    .Include(faq => faq.Tags)
                    .FirstOrDefaultAsync(faq => faq.Id == int.Parse(id));
                if (oldFaq is null)
                {
                    await command.RespondAsync($"Couldn't find the faq with ID:{id}.\n", ephemeral: true);
                }
                switch (subCommand.Name)
                {
                    case "name":
                        string name = subCommand.Options.FirstOrDefault(op => op.Name == "name")?.Value.ToString();
                        var oldName = oldFaq.Name;
                        oldFaq.Name = name;
                        await _db.SaveChangesAsync();
                        await command.RespondAsync($"Edited description of FAQ #{id} from {oldName} to {name}.", ephemeral: IsCommandEphemeral(subCommand.Options));
                        break;
                    case "description":
                        string desc = subCommand.Options.FirstOrDefault(op => op.Name == "description")?.Value.ToString();
                        var oldDesc = SplitSanitizedString(oldFaq.Description);
                        oldFaq.Description = SplitSanitizedString(desc);
                        var from = new EmbedBuilder()
                        {
                            Title = $"Edited description of FAQ #{id} from",
                            Description = oldDesc
                        };
                        var to = new EmbedBuilder()
                        {
                            Title = $"to",
                            Description = SplitSanitizedString(desc)
                        };
                        await _db.SaveChangesAsync();
                        await command.RespondAsync(embeds: new Embed[] { from.Build(), to.Build() }, ephemeral: IsCommandEphemeral(subCommand.Options));
                        break;
                    case "link":
                        string link = subCommand.Options.FirstOrDefault(op => op.Name == "link")?.Value.ToString();
                        var oldLink = oldFaq.Link;
                        oldFaq.Link = link;
                        await _db.SaveChangesAsync();
                        await command.RespondAsync($"Edited link of FAQ #{id} from {oldLink} to {link}.", ephemeral: IsCommandEphemeral(subCommand.Options));
                        break;
                    case "image":
                        string image = subCommand.Options.FirstOrDefault(op => op.Name == "image")?.Value.ToString();
                        var oldImage = oldFaq.Image;
                        oldFaq.Image = image;
                        await _db.SaveChangesAsync();
                        await command.RespondAsync($"Edited Image of FAQ #{id} from {oldImage} to {image}.", ephemeral: IsCommandEphemeral(subCommand.Options));
                        break;
                    case "tags":
                        string tags = subCommand.Options.FirstOrDefault(op => op.Name == "tags")?.Value.ToString();
                        List<FAQTag> FAQTags = new();
                        foreach (string tag in tags.Split(','))
                        {
                            FAQTags.Add(new() { Tag = tag.Trim() });
                        }
                        oldFaq.Tags = FAQTags;
                        await _db.SaveChangesAsync();
                        await command.RespondAsync($"Removed old tags and added new tags of FAQ #{id}.", ephemeral: IsCommandEphemeral(command.Data.Options));
                        break;
                }
            }
            catch
            {
                await command.RespondAsync($"Tried to edit FAQ but failed.\n", ephemeral: true);
                Console.WriteLine($"Tried to edit FAQ but failed.\n");
            }
        }

        private async Task FAQAddCommand(SocketSlashCommand command)
        {
            if(await IsTruePirateAsync(command) is false)
                return;
            try
            {
                string name = command.Data.Options.FirstOrDefault(op => op.Name == "name")?.Value.ToString();
                string desc = command.Data.Options.FirstOrDefault(op => op.Name == "description")?.Value.ToString();
                string image = command.Data.Options.FirstOrDefault(op => op.Name == "image")?.Value.ToString();
                string link = command.Data.Options.FirstOrDefault(op => op.Name == "link")?.Value.ToString();
                string tags = command.Data.Options.FirstOrDefault(op => op.Name == "tags")?.Value.ToString();
                List<FAQTag> FAQTags = new();
                foreach(string tag in tags.Split(','))
                {
                    FAQTags.Add(new() { Tag = tag.Trim() });
                }
                var faq = new FAQEntry(name, link, image, SplitSanitizedString(desc), FAQTags);
                await _db.AddAsync(faq);
                await _db.SaveChangesAsync();
                var embed = new EmbedBuilder
                {
                    Title = $"Added FAQ Entry #{faq.Id}: **[{faq.Name}]({faq.Link})**",
                    Description = faq.Description,
                    ImageUrl = faq.Image,
                };
                await command.RespondAsync(embed: embed.Build(), ephemeral: IsCommandEphemeral(command.Data.Options));
                _faq.Add(faq);
            }
            catch
            {
                await command.RespondAsync($"Tried to Add FAQ but failed.\n", ephemeral: true);
                Console.WriteLine($"Tried to Add FAQ but failed.\n");
            }
        }

        private async Task FAQListCommand(SocketSlashCommand command)
        {
            var embed = new EmbedBuilder
            {
                Title = $"Available FAQ entries.",
            };
            foreach (FAQEntry faq in _faq)
            {
                StringBuilder tags = new();
                foreach (FAQTag tag in faq.Tags)
                {
                    tags.AppendLine($"{tag.Id}: {tag.Tag}");
                }
                embed.AddField($"{faq.Id}: {faq.Name}", $"Tags ({faq.Tags.Count})\n{tags}", true);

            }
            await command.RespondAsync(embed: embed.Build(), ephemeral: IsCommandEphemeral(command.Data.Options));
        }

        private static string SplitSanitizedString(string description)
        {
            var test = description.Split("\\n");
            StringBuilder sb = new();
            foreach(string s in test)
            {
                sb.AppendLine(s);
            }
            return sb.ToString();
        }

        private async Task FAQCommand(SocketSlashCommand command)
        {
            var data = command.Data.Options.FirstOrDefault(op => op.Name == "data")?.Value;
            if (data is null)
            {
                return;
            }
            var faq = _faq.FirstOrDefault(faq => faq.Tags.Any(tag => tag.Tag.ToLower().Contains(data.ToString()!.ToLower())));
            if (faq is not null)
            {
                var embed = new EmbedBuilder
                {
                    Title = $"**{faq.Name}**",
                    Description = $"{faq.Description}",
                    ImageUrl = faq.Image,
                    Color = Color.Blue,
                    Url = faq.Link
                };
                await command.RespondAsync(embed: embed.Build(), ephemeral: IsCommandEphemeral(command.Data.Options));
            }
            else
            {
                await command.RespondAsync($"FAQ entry not found!", ephemeral: true);
            }
        }

        private async Task InitiateSlashCommands()
        {
            var piratesGuild = _client.GetGuild(940071768679395369);
            var globalUser = _client.GetUser(_client.CurrentUser.Id);
            var user = piratesGuild.GetUser(_client.CurrentUser.Id);
            await user.ModifyAsync(x => {
                x.Nickname = "Botswain";
            });
            var commands = await _client.GetGlobalApplicationCommandsAsync();
            foreach(var command in commands)
            {
                await command.DeleteAsync();
            }
            var guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("botswainhelp");
            guildCommand.WithDescription("What is the Botswain?");
            guildCommand.AddOption("hidden", ApplicationCommandOptionType.Boolean, "Set False to show publicly. True by default.", false);
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());

            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("lackeyjoin");
            guildCommand.WithDescription("Apply to be a pirate Lackey.");
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());

            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("lackeylist");
            guildCommand.WithDescription("List of lackey applications.");
            guildCommand.AddOption("old", ApplicationCommandOptionType.Boolean, "Show old applications", false);
            guildCommand.AddOption("hidden", ApplicationCommandOptionType.Boolean, "Set False to show publicly. True by default.", false);
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());

            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("lackeyapprove");
            guildCommand.WithDescription("(TRUE PIRATE) Approve a lackey applicant.");
            guildCommand.AddOption("id", ApplicationCommandOptionType.String, $"The id of the application to approve. Use /lackeylist to find the id.", true);
            guildCommand.AddOption("hidden", ApplicationCommandOptionType.Boolean, "Set False to show publicly. True by default.", false);
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());

            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("faqlist");
            guildCommand.WithDescription("List all FAQs.");
            guildCommand.AddOption("hidden", ApplicationCommandOptionType.Boolean, "Set False to show publicly. True by default.", false);
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());

            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("faq");
            guildCommand.WithDescription("Search the FAQ with a tag.");
            guildCommand.AddOption("data", ApplicationCommandOptionType.String, "The data to look for in the FAQ database.", true);
            guildCommand.AddOption("hidden", ApplicationCommandOptionType.Boolean, "Set False to show publicly. True by default.", false);
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());

            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("faqadd");
            guildCommand.WithDescription("(TRUE PIRATE) Add a new FAQ entry.");
            guildCommand.AddOptions(new SlashCommandOptionBuilder[]
            {
                new()
                {
                    Name = "name",
                    Description = "The name of the FAQ Entry.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                },
                new()
                {
                    Name = "description",
                    Description = "The Description of the FAQ Entry.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                },
                new()
                {
                    Name = "link",
                    Description = "A link to elaborate on the FAQ Entry.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = false,
                },
                new()
                {
                    Name = "image",
                    Description = "A URI to an image for the FAQ.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = false,
                },
                new()
                {
                    Name = "tags",
                    Description = "Comma separated tags for example: \"Hero, Heroes, Class\"",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                },
                new()
                {
                    Name = "hidden",
                    Description = "Set False to show publicly. True by default.",
                    Type = ApplicationCommandOptionType.Boolean,
                    IsRequired = false,
                }
            });
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());

            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("faqaddtag");
            guildCommand.WithDescription("(TRUE PIRATE) Add a FAQ Tag.");
            guildCommand.AddOptions(new SlashCommandOptionBuilder[]
            {
                new()
                {
                    Name = "id",
                    Description = "Id of the FAQ to add a TAG to.",
                    Type = ApplicationCommandOptionType.Integer,
                    IsRequired = true,
                },
                new()
                {
                    Name = "tag",
                    Description = "The tag to add.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                },
                new()
                {
                    Name = "hidden",
                    Description = "Set False to show publicly. True by default.",
                    Type = ApplicationCommandOptionType.Boolean,
                    IsRequired = false,
                }
            });
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());

            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("faqedittag");
            guildCommand.WithDescription("(TRUE PIRATE) Edit a FAQ Tag.");
            guildCommand.AddOptions(new SlashCommandOptionBuilder[]
            {
                new()
                {
                    Name = "id",
                    Description = "Id of the FAQ to edit.",
                    Type = ApplicationCommandOptionType.Integer,
                    IsRequired = true,
                },
                new()
                {
                    Name = "tag",
                    Description = "The new tag.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                },
                new()
                {
                    Name = "hidden",
                    Description = "Set False to show publicly. True by default.",
                    Type = ApplicationCommandOptionType.Boolean,
                    IsRequired = false,
                }
            });
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());

            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("faqedit");
            guildCommand.WithDescription("(TRUE PIRATE) Edit a FAQ.");
            guildCommand.AddOptions(new SlashCommandOptionBuilder[]
            {
                new()
                {
                    Name = "name",
                    Description = "Change the name of the FAQ Entry.",
                    Type = ApplicationCommandOptionType.SubCommand,
                    Options = new List<SlashCommandOptionBuilder>
                    {
                        new()
                        {
                            Name = "id",
                            Description = "Id of the FAQ to edit.",
                            Type = ApplicationCommandOptionType.Integer,
                            IsRequired = true,
                        },
                        new()
                        {
                            Name = "name",
                            Description = "The name to change to.",
                            Type = ApplicationCommandOptionType.String,
                            IsRequired = true
                        },
                        new()
                        {
                            Name = "hidden",
                            Description = "Set False to show publicly. True by default.",
                            Type = ApplicationCommandOptionType.Boolean,
                            IsRequired = false,
                        }
                    }
                },
                new()
                {
                    Name = "description",
                    Description = "Change the Description of the FAQ Entry.",
                    Type = ApplicationCommandOptionType.SubCommand,
                    Options = new List<SlashCommandOptionBuilder>
                    {
                        new()
                        {
                            Name = "id",
                            Description = "Id of the FAQ to edit.",
                            Type = ApplicationCommandOptionType.Integer,
                            IsRequired = true,
                        },
                        new()
                        {
                            Name = "description",
                            Description = "The Description to change to.",
                            Type = ApplicationCommandOptionType.String,
                            IsRequired = true
                        },
                        new()
                        {
                            Name = "hidden",
                            Description = "Set False to show publicly. True by default.",
                            Type = ApplicationCommandOptionType.Boolean,
                            IsRequired = false,
                        }
                    }
                },
                new()
                {
                    Name = "link",
                    Description = "Change the link of the FAQ Entry.",
                    Type = ApplicationCommandOptionType.SubCommand,
                    Options = new List<SlashCommandOptionBuilder>
                    {
                        new()
                        {
                            Name = "id",
                            Description = "Id of the FAQ to edit.",
                            Type = ApplicationCommandOptionType.Integer,
                            IsRequired = true,
                        },
                        new()
                        {
                            Name = "link",
                            Description = "The Description to change to.",
                            Type = ApplicationCommandOptionType.String,
                            IsRequired = true
                        },
                        new()
                        {
                            Name = "hidden",
                            Description = "Set False to show publicly. True by default.",
                            Type = ApplicationCommandOptionType.Boolean,
                            IsRequired = false,
                        }
                    }
                },
                new()
                {
                    Name = "image",
                    Description = "Change the URI to the FAQ Image.",
                    Type = ApplicationCommandOptionType.SubCommand,
                    Options = new List<SlashCommandOptionBuilder>
                    {
                        new()
                        {
                            Name = "id",
                            Description = "Id of the FAQ to edit.",
                            Type = ApplicationCommandOptionType.Integer,
                            IsRequired = true,
                        },
                        new()
                        {
                            Name = "image",
                            Description = "The image URI to change to.",
                            Type = ApplicationCommandOptionType.String,
                            IsRequired = true
                        },
                        new()
                        {
                            Name = "hidden",
                            Description = "Set False to show publicly. True by default.",
                            Type = ApplicationCommandOptionType.Boolean,
                            IsRequired = false,
                        }
                    }
                },
                new()
                {
                    Name = "tags",
                    Description = "Change the tags for a FAQ.",
                    Type = ApplicationCommandOptionType.SubCommand,
                    Options = new List<SlashCommandOptionBuilder>
                    {
                        new()
                        {
                            Name = "id",
                            Description = "Id of the FAQ to edit.",
                            Type = ApplicationCommandOptionType.Integer,
                            IsRequired = true,
                        },
                        new()
                        {
                            Name = "tags",
                            Description = "The tags to replace the FAQs tags with.",
                            Type = ApplicationCommandOptionType.String,
                            IsRequired = true
                        },
                        new()
                        {
                            Name = "hidden",
                            Description = "Set False to show publicly. True by default.",
                            Type = ApplicationCommandOptionType.Boolean,
                            IsRequired = false,
                        }
                    }
                }
            });
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());
        }

        private Task Log(LogMessage message)
        {
            if (message.Exception is CommandException cmdException)
            {
                Console.WriteLine($"[Command/{message.Severity}] {cmdException.Command.Aliases.First()}"
                    + $" failed to execute in {cmdException.Context.Channel}.");
                Console.WriteLine(cmdException);
            }
            else
                Console.WriteLine($"[General/{message.Severity}] {message}");
            return Task.CompletedTask;
        }
    }
}
