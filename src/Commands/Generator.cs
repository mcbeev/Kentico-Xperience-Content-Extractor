using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using XperienceContentXtractor.Extensions;
using XperienceContentXtractor.Models;

namespace XperienceContentXtractor.Commands
{
    [Command( "generate", "g", Description = "Generate configuration settings file for the extractor" )]
    public class Generator
    {

        [Option( ShortName = "o", Description = "Config file with the settings used to Xtract from Xperience" )]
        [Required]
        public string Output { get; set; }

        public async Task OnExecuteAsync( IConsole console )
        {
            try
            {
                var settings = GenerateConfigSettings();

                settings.Validate();

                await WriteConfigAsync( settings );
            }
            catch( ValidationException ex )
            {
                console.ForegroundColor = ConsoleColor.DarkRed;
                console.WriteLine();
                console.WriteLine( ex.ValidationResult.ErrorMessage );
                console.ResetColor();
            }
        }

        private static ExtractorSettings GenerateConfigSettings( )
        {
            var settings = new ExtractorSettings();

            settings.AbsoluteSiteName = Prompt.GetString( $"Fully qualified target website URL:", promptColor: ConsoleColor.DarkMagenta );
            settings.AllNodesFilename = Prompt.GetString( $"\"All Nodes\" Xml Filename?", settings.AllNodesFilename, ConsoleColor.DarkMagenta );
            settings.AllRedirectsFilename = Prompt.GetString( $"\"IIS Redirects\" Xml Filename:", settings.AllRedirectsFilename, ConsoleColor.DarkMagenta );
            settings.ConnectionString = Prompt.GetString( "Connection String:", promptColor: ConsoleColor.DarkMagenta );
            settings.HashStringSalt = Prompt.GetString( "Hash String Salt:", promptColor: ConsoleColor.DarkMagenta );
            settings.ObjectDirectory = Prompt.GetString( $"Object (export) directory:", settings.ObjectDirectory, ConsoleColor.DarkMagenta );
            settings.RootNodeAliasPath = Prompt.GetString( $"Root \"NodeAliasPath\":", settings.RootNodeAliasPath, ConsoleColor.DarkMagenta );

            var defaultOrderByColumns = string.Join( ", ", settings.OrderByColumns );
            settings.OrderByColumns = Prompt.GetString( $"OrderBy Columns (comma-delimited):", defaultOrderByColumns, ConsoleColor.DarkMagenta )
                .Split( ',' )
                .Select( s => s.Trim() )
                .ToArray();

            var pageType = Prompt.GetString( "PageType:", promptColor: ConsoleColor.DarkMagenta );
            settings.PageType = pageType;
            settings.PageTypeColumns = ( Prompt.GetString( "PageType Columns (comma-delimited):", promptColor: ConsoleColor.DarkMagenta ) ?? string.Empty )
                .Split( ',' )
                .Select( s => s.Trim() )
                .ToArray();

            settings.TopN = Prompt.GetInt( $"Count:", settings.TopN, ConsoleColor.DarkMagenta );
            settings.XperienceUser = Prompt.GetString( $"Xperience User name:", settings.XperienceUser, ConsoleColor.DarkMagenta );

            return settings;
        }

        private async Task WriteConfigAsync( ExtractorSettings settings )
        {
            using var file = new FileStream( Output, FileMode.Create );
            await JsonSerializer.SerializeAsync( file, settings, new JsonSerializerOptions { WriteIndented = true } );
        }
    }
}
