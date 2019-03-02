﻿using Intersect.Logging;
using Intersect.Server.Core.CommandParsing;
using Intersect.Server.Core.CommandParsing.Errors;
using Intersect.Server.Core.Commands;
using Intersect.Server.Localization;
using Intersect.Threading;
using JetBrains.Annotations;
using System.Linq;
using Intersect.Server.Core.CommandParsing.Commands;

namespace Intersect.Server.Core
{
    internal sealed class ServerConsole : Threaded
    {
        [NotNull]
        public CommandParser Parser { get; }

        public ServerConsole()
        {
            Console.WaitPrefix = "> ";

            Parser = new CommandParser(
                new ParserSettings(
                    localization: Strings.Commands.Parsing
                )
            );

            Parser.Register<AnnouncementCommand>();
            Parser.Register<ApiCommand>();
            Parser.Register<BanCommand>();
            Parser.Register<CpsCommand>();
            Parser.Register<ExitCommand>();
            Parser.Register<HelpCommand>(Parser.Settings);
            Parser.Register<KickCommand>();
            Parser.Register<KillCommand>();
            Parser.Register<MakePrivateCommand>();
            Parser.Register<MakePublicCommand>();
            Parser.Register<MigrateCommand>();
            Parser.Register<MuteCommand>();
            Parser.Register<NetDebugCommand>();
            Parser.Register<OnlineListCommand>();
            Parser.Register<PowerAccountCommand>();
            Parser.Register<PowerCommand>();
            Parser.Register<UnbanCommand>();
            Parser.Register<UnmuteCommand>();
        }

        protected override void ThreadStart()
        {
            Console.WriteLine(Strings.Intro.consoleactive);

            while (ServerContext.Instance.IsRunning)
            {
#if !CONSOLE_EXTENSIONS
                Console.Write(Console.WaitPrefix);
#endif
                var line = Console.ReadLine()?.Trim();

                if (line == null)
                {
                    ServerContext.Instance.RequestShutdown();
                    break;
                }

                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                var result = Parser.Parse(line);
                var shouldHelp = result.Command is IHelpableCommand helpable && result.Find(helpable.Help);
                if (result.Missing.IsEmpty)
                {
                    var fatalError = false;
                    result.Errors.ForEach(error =>
                    {
                        if (error == null)
                        {
                            return;
                        }

                        fatalError = error.IsFatal;
                        if (error is MissingArgumentError)
                        {
                            return;
                        }

                        if (!error.IsFatal || error is MissingCommandError || error is UnhandledArgumentError)
                        {
                            Console.WriteLine(error.Message);
                        }
                        else
                        {
                            Log.Warn(error.Exception, error.Message);
                        }
                    });

                    if (!fatalError)
                    {
                        if (!shouldHelp)
                        {
                            result.Command?.Handle(ServerContext.Instance, result);
                        }
                    }
                }
                else
                {
                    Console.WriteLine(
                        Strings.Commands.Parsing.Errors.MissingArguments.ToString(
                            string.Join(
                                Strings.Commands.Parsing.Errors.MissingArgumentsDelimeter,
                                result.Missing.Select(argument =>
                                    {
                                        var typeName = argument?.ValueType.Name ?? Strings.Commands.Parsing.TypeUnknown;
                                        if (Strings.Commands.Parsing.TypeNames.TryGetValue(typeName,
                                            out var localizedType))
                                        {
                                            typeName = localizedType;
                                        }

                                        return argument?.Name +
                                               Strings.Commands.Parsing.Formatting.Type.ToString(typeName);
                                    }
                                )
                            )
                        )
                    );
                }

                if (result.Command == null)
                {
                    continue;
                }

                var command = result.Command;
                Console.WriteLine(command.FormatUsage(Parser.Settings, result.AsContext(true), true));

                if (!shouldHelp)
                {
                    continue;
                }

                Console.WriteLine($@"    {command.Description}");
                Console.WriteLine();

                var requiredBuffer = command.Arguments.Count == 1
                    ? ""
                    : new string(' ', Strings.Commands.RequiredInfo.ToString().Length);
                command.UnsortedArguments.ForEach(argument =>
                {
                    if (argument == null)
                    {
                        return;
                    }

                    var shortName = argument.HasShortName ? argument.ShortName.ToString() : null;
                    var name = argument.Name;

                    var typeName = argument.ValueType.Name;
                    if (argument.IsFlag)
                    {
                        typeName = Strings.Commands.FlagInfo;
                    }
                    else if (Strings.Commands.Parsing.TypeNames.TryGetValue(typeName, out var localizedType))
                    {
                        typeName = localizedType;
                    }

                    if (!argument.IsPositional)
                    {
                        shortName = Parser.Settings.PrefixShort + shortName;
                        name = Parser.Settings.PrefixLong + name;
                    }

                    var names = string.Join(
                        ", ", new[] {shortName, name}.Where(nameString => !string.IsNullOrWhiteSpace(nameString))
                    );

                    var required = argument.IsRequiredByDefault
                        ? Strings.Commands.RequiredInfo.ToString()
                        : requiredBuffer;

                    var descriptionSegment =
                        string.IsNullOrEmpty(argument.Description) ? "" : $@" - {argument.Description}";

                    Console.WriteLine($@"    {names,-16} {typeName,-12} {required}{descriptionSegment}");
                });

                Console.WriteLine();
            }
        }
    }
}