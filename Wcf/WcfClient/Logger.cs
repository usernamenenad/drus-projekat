using System;
using System.Collections.Concurrent;

namespace WcfClient
{
    internal class Logger
    {
        public static readonly ConcurrentBag<ConsoleColor> ConsoleColors = new ConcurrentBag<ConsoleColor>()
        {
            ConsoleColor.Blue,
            ConsoleColor.Cyan,
            ConsoleColor.Green,
            ConsoleColor.Magenta,
            ConsoleColor.Red,
            ConsoleColor.Yellow,
            ConsoleColor.DarkBlue,
            ConsoleColor.DarkGreen,
            ConsoleColor.DarkCyan,
            ConsoleColor.DarkMagenta
        };

        private readonly ConsoleColor _consoleColor;

        public Logger()
        {
            if(ConsoleColors.TryTake(out var consoleColor))
            {
                _consoleColor = consoleColor;
            }
            else
            {
                _consoleColor = ConsoleColor.White;
            }
        }

        public void Log(string message)
        {
            Console.ForegroundColor = _consoleColor;
            Console.WriteLine(message);
        }
    }
}
