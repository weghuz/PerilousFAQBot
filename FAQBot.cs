using Discord.Commands;
using Discord.WebSocket;
using Discord;
using FAQBot.Models;
using Microsoft.Extensions.Configuration;
using System.Text;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace FAQBot
{
    internal class FAQBot
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandHandler _commands;
        private readonly FAQDB _db;
        private readonly IConfiguration _config;
        private const ulong ROLE_TRUE_PIRATE_ID = 968880274517655612;
        private const ulong PIRATES_GUILD_ID = 940071768679395369;
        private List<FAQEntry> _faq;

        public FAQBot(IConfiguration config, FAQDB db)
        {
            _db = db;
            _config = config;
            _faq = new();
            _client = new DiscordSocketClient(new DiscordSocketConfig { MessageCacheSize = 100 });
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
            await Task.Delay(-1);
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
                await msg.ReplyAsync("This is a FAQ Bot! \n to know more execute the /faqhelp command");
            }
        }

        private async Task SlashCommandExecuted(SocketSlashCommand command)
        {
            try
            {
                switch (command.CommandName)
                {
                    case "faqhelp":
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
                Title = $"Perilous Pirates FAQ Bot",
                Description = $"The FAQ bot lists FAQ entries related to DeFi Kingdoms in a database.\n" +
                $"FAQ entries get served to users through the /faq command.\n" +
                $"Depending on what you enter with the command you get served a relevant FAQ entry to hopefully answer your questions."
            };
            embed.AddField($"1", $"faq", true);
            embed.AddField($"2", $"faqhelp", true);
            embed.AddField($"3", $"faqlist", true);
            embed.AddField($"4 (TRUE PIRATE)", $"faqadd", true);
            embed.AddField($"5 (TRUE PIRATE)", $"faqedit", true);
            embed.AddField($"6 (TRUE PIRATE)", $"faqaddtag", true);

            await command.RespondAsync(embed: embed.Build(), ephemeral: IsCommandEphemeral(command.Data.Options));
        }

        private bool IsCommandEphemeral(IReadOnlyCollection<SocketSlashCommandDataOption> options)
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
                        var oldDesc = oldFaq.Description;
                        oldFaq.Description = desc;
                        await _db.SaveChangesAsync();
                        await command.RespondAsync($"Edited description of FAQ #{id} from {oldDesc} to {desc}.", ephemeral: IsCommandEphemeral(subCommand.Options));
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
                var faq = new FAQEntry(name, link, image, desc, FAQTags);
                await _db.AddAsync(faq);
                await _db.SaveChangesAsync();
                await command.RespondAsync($"Added FAQ Entry #{faq.Id}:\n**[{faq.Name}]({faq.Link})**:\n{faq.Description}\n[Image]({faq.Image})", ephemeral: IsCommandEphemeral(command.Data.Options));
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
                await command.RespondAsync($"**[{faq.Name}]({faq.Link})**:\n{faq.Description}\n[Image]({faq.Image})", ephemeral: IsCommandEphemeral(command.Data.Options));
            }
            else
            {
                await command.RespondAsync($"FAQ entry not found!", ephemeral: true);
            }
        }

        private async Task InitiateSlashCommands()
        {
            var piratesGuild = _client.GetGuild((ulong)940071768679395369);
            var commands = await _client.GetGlobalApplicationCommandsAsync();
            foreach(var command in commands)
            {
                await command.DeleteAsync();
            }
            var guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("faqhelp");
            guildCommand.WithDescription("What is the FAQ Bot?");
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
