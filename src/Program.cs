using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace XperienceContentXtractor
{
    [Command( Description = "A tool for extracting page data from K12." )]
    [Subcommand( typeof( Commands.Extractor ) )]
    [Subcommand( typeof( Commands.Generator ) )]
    public class Program
    {
        static async Task Main( string[] args )
        {
            Console.WriteLine( "Welcome to the Kentico Xperience Portal Engine Content Extractor!" );
            Console.WriteLine();

            await CommandLineApplication.ExecuteAsync<Program>( args );
        }

        private int OnExecute( CommandLineApplication app, IConsole console )
        {
            console.WriteLine( "You must specify at a subcommand." );
            app.ShowHelp();
            
            return 1;
        }

    }

}
