using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using CMS.Base;
using CMS.DataEngine;
using CMS.DocumentEngine;
using CMS.Helpers;
using CMS.MacroEngine;
using CMS.Membership;
using HtmlAgilityPack;
using McMaster.Extensions.CommandLineUtils;
using XperienceContentXtractor.Models;

namespace XperienceContentXtractor.Commands
{

    [Command( "extract", "e", Description = "Extract page data using the specified config" )]
    public class Extractor
    {

        [Argument(0, Description = "Config file with the settings used to Xtract from Xperience" )]
        [Required]
        public string Config { get; set; }

        [Option( Description = "Clean out the output directory before extraction" )]
        public bool Clean { get; set; }

        [Option( Description = "Test the extraction without saving and output" )]
        public bool DryRun { get; set; }

        public async Task OnExecuteAsync( IConsole console )
        {
            if( string.IsNullOrWhiteSpace( Config ) )
            {
                throw new ArgumentNullException( nameof( Config ) );
            }

            if( !File.Exists( Config ) )
            {
                throw new FileNotFoundException( Config );
            }

            using var file = new FileStream( Config, FileMode.Open );
            var settings = await JsonSerializer.DeserializeAsync<ExtractorSettings>( file );
            EnsureValidSettings( settings );

            try
            {
                Extract( console, settings );
            } 
            catch (CMS.DataEngine.ApplicationInitException ex)
            {
                console.ForegroundColor = ConsoleColor.DarkRed;
                console.WriteLine();
                console.WriteLine( ex.Message );
                console.WriteLine( "Please verify your connection string and hash string salt" );
                console.ResetColor();
            }
        }

        private void Extract( IConsole console, ExtractorSettings settings )
        {
            var redirects = new List<(string, string)>();

            // Create the output directories if needed
            if (!DryRun)
            {
                PrepareObjectDirectory( settings );
            }

            //Doc to save the actual content as xml
            var xmlDocument = new XmlDocument();
            var parentElem = xmlDocument.CreateNode( "element", settings.PageType.Replace( ".", "-" ) + "s", "" );

            //Fire up our Kentico Xperience instance
            ConnectionHelper.ConnectionString = settings.ConnectionString;
            ValidationHelper.HashStringSalt = settings.HashStringSalt;
            CMSApplication.Init();

            // Gets an object representing a specific Kentico user
            UserInfo user = UserInfoProvider.GetUserInfo( settings.XperienceUser );

            // Sets the context of the user
            using( new CMSActionContext( user ) )
            {
                var wc = new WebClient();

                //Get me all published pages of the specified page type on the site
                var posts = DocumentHelper.GetDocuments( settings.PageType )
                    .Columns( settings.PageTypeColumns )
                    .Path( settings.RootNodeAliasPath, PathTypeEnum.Children )
                    .PublishedVersion()
                    .Published()
                    .TopN( settings.TopN )
                    .OrderBy( settings.OrderByColumns );

                console.WriteLine( $"{posts.Count} nodes found." );
                console.WriteLine( "Exporting node data..." );

                int nodeOrder = 1;
                foreach( TreeNode post in posts )
                {
                    //Create a new AgilityPack doc for working with the page
                    HtmlDocument doc = new HtmlDocument();
                    doc.OptionOutputAsXml = false; //if true adds a weird span tag

                    doc.DocumentNode.AppendChild( HtmlNode.CreateNode( "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" ) );
                    doc.DocumentNode.AppendChild( HtmlNode.CreateNode( Environment.NewLine ) );

                    string nodeAliasPath = post.NodeAliasPath;

                    nodeAliasPath = nodeAliasPath.Split( '/' ).Last(); //clean it up to just the slug

                    // Add redirect to the list
                    redirects.Add( (post.NodeAliasPath, nodeAliasPath) );

                    // TODO: use FormInfo to determine "DocumentName" field
                    string nodeTitle = post.DocumentName;

                    //Create a parent folder path in the data directory to store all xml about this node in a structured way on the file system
                    string nodeDirPath = Path.Combine( settings.ObjectDirectory, settings.PageType, nodeAliasPath.Replace( "/", "-" ) );
                    if( !Directory.Exists( nodeDirPath ) )
                    {
                        Directory.CreateDirectory( nodeDirPath );
                    }

                    //Create root xml Element of the pageTypeName ex. <cms-blogpost>
                    // and start saving fields as attributes and child elements in this xml structure
                    var rootNode = HtmlNode.CreateNode( $"<{settings.PageType.Replace( ".", "-" )} />" );
                    rootNode.Attributes.Append( "originalAliasPath" );
                    rootNode.SetAttributeValue( "originalAliasPath", post.NodeAliasPath );
                    rootNode.AppendChild( HtmlNode.CreateNode( Environment.NewLine ) );

                    var aliasNode = HtmlNode.CreateNode( $"<newaliaspath/>" );
                    aliasNode.InnerHtml = nodeAliasPath;
                    rootNode.AppendChild( aliasNode );

                    var orderNode = HtmlNode.CreateNode( $"<newnodeorder/>" );
                    orderNode.InnerHtml = nodeOrder.ToString();
                    rootNode.AppendChild( orderNode );
                    nodeOrder++;

                    //Kentico dynamic properties or "With the Coupled Columns of the page type"
                    //Iterate through all of the fields of a document in order to serialize them to xml
                    foreach( var dprop in post.Properties )
                    {
                        if( Array.IndexOf( settings.PageTypeColumns, dprop ) > 0 )
                        {
                            //Create xml Element that represents each field ex. <blogpostsummary>data</blogpostsummary>
                            var node = HtmlNode.CreateNode( $"<{dprop} />" );
                            var data = post.GetValue( dprop, "" );

                            //Special stuff for Mcbeev Blog Post Summary (can be ignored for others)
                            if( dprop.ToLower().Contains( "summary" ) )
                            {
                                //Remove wrapped p tag on Mcbeev Blog Post Summary
                                HtmlDocument docSummary = new HtmlDocument();
                                docSummary.LoadHtml( data );
                                var newSummaryNode = HtmlNode.CreateNode( $"<newblogpostsummary/>" );
                                try
                                {

                                    if( docSummary.DocumentNode.SelectSingleNode( "//p" ) != null )
                                    {
                                        newSummaryNode.InnerHtml = docSummary.DocumentNode.SelectSingleNode( "//p" ).InnerText;
                                    }
                                    else
                                    {
                                        newSummaryNode.InnerHtml = docSummary.DocumentNode.InnerText;
                                    }
                                }
                                catch( Exception ex )
                                {
                                    //todo
                                    console.WriteLine( ex.Message );
                                }

                                rootNode.AppendChild( newSummaryNode );
                                rootNode.AppendChild( HtmlNode.CreateNode( Environment.NewLine ) );
                            }


                            //This is WYSIWYG content field most likely so try to resolve things as Kentico would
                            if( dprop.ToLower().Contains( "title" ) || dprop.ToLower().Contains( "body" ) || dprop.ToLower().Contains( "content" ) || dprop.ToLower().Contains( "summary" ) )
                            {
                                //Macro resolve inside of the content (handle K# macros in the text)
                                data = MacroResolver.Resolve( data );

                                //Url resolve inside of the content (handle relative urls into absolute urls) important!

                                data = HTMLHelper.ResolveUrls( data, settings.AbsoluteSiteName, true );

                                //Widget identifier (try to save a list of all Portal Inline Widgets that no longer work in MVC)
                                if( data.Contains( "CMSInlineControl" ) )
                                {
                                    console.WriteLine( "Found a widget in this content." );
                                    // TODO Make this a real log
                                    // //Render the value as html to HP NodedataRootDirectoryName
                                    // HtmlDocument docWidget = new HtmlDocument();
                                    // docWidget.LoadHtml(data);
                                    // var widgets = docWidget.DocumentNode.SelectNodes("//object[contains(@type, 'Widget')]");

                                    // foreach (var w in widgets)
                                    // {          
                                    //     console.WriteLine(w.OuterHtml);
                                    // }   
                                }

                                //Media library identifier
                                if( data.Contains( "<img " ) )
                                {
                                    HtmlDocument docImg = new HtmlDocument();
                                    docImg.LoadHtml( data );
                                    var ImageURLs = docImg.DocumentNode.Descendants( "img" )
                                        .Select( e => e.GetAttributeValue( "src", null ) )
                                        .Where( s => !String.IsNullOrEmpty( s ) );

                                    foreach( var oldImageURL in ImageURLs )
                                    {
                                        //Skip mp3s for now
                                        if( oldImageURL.EndsWith( ".mp3" ) || oldImageURL.Contains( ".mp3?" ) )
                                        {
                                            continue;
                                        }

                                        console.WriteLine( $"trying to save {oldImageURL} ..." );
                                        var safeName = oldImageURL.Split( new char[] { '?' } )[ 0 ];
                                        safeName = safeName.Replace( ".aspx", "" );

                                        //Save the images locally if we can download from the "real" running site
                                        try
                                        {
                                            wc.DownloadFile( oldImageURL, Path.Combine( nodeDirPath, Path.GetFileName( safeName ) ) );
                                        }
                                        catch( Exception ex )
                                        {
                                            console.WriteLine( ex.Message );
                                        }

                                        //rewrite the image path back in the data node
                                        // TODO - make this a config parameter and not hard coded to Mcbeev.com                                 
                                        data = data.Replace( oldImageURL, "/MBV/media/blog/" + nodeAliasPath.Replace( "/", "-" ) + "/" + oldImageURL.Split( new char[] { '/' } ).Last() );

                                        //replace the odd .png.aspx thing
                                        data = data.Replace( ".png.aspx", ".png" );
                                    }
                                }

                                //Use HTMLAgility Pack to fix non closed image tags
                                HtmlNode.ElementsFlags[ "img" ] = HtmlElementFlag.Closed;
                                HtmlDocument docContent = new HtmlDocument();
                                docContent.LoadHtml( data );

                                data = $"<![CDATA[{docContent.DocumentNode.OuterHtml}]]>";
                            }

                            //Most likely image only field and not a WYSIWYG field
                            // TODO - is there a better way to check for this?
                            if( ( data.Length > 2 ) && ( data.StartsWith( "~/" ) ) )
                            {
                                data = data.Replace( "~/", settings.AbsoluteSiteName );

                                console.WriteLine( $"trying to save {data} ..." );
                                var safeName = data.Split( new char[] { '?' } )[ 0 ];
                                safeName = safeName.Replace( ".aspx", "" );
                                wc.DownloadFile( data, Path.Combine( nodeDirPath, Path.GetFileName( safeName ) ) );

                                //rewrite the image path back in the data node                                      
                                safeName = "/MBV/media/blog/" + nodeAliasPath.Replace( "/", "-" ) + "/" + safeName.Split( new char[] { '/' } ).Last();

                                data = $"<![CDATA[{safeName}]]>";
                            }

                            node.AppendChild( doc.CreateTextNode( data ) );
                            rootNode.AppendChild( node );
                            rootNode.AppendChild( HtmlNode.CreateNode( "\r\n" ) );
                        }
                    }
                    doc.DocumentNode.AppendChild( rootNode );

                    //Save the single node to its own XML document in its directory
                    doc.Save( $@"{nodeDirPath}\{nodeAliasPath.Replace( "/", "-" )}.xml" );

                    //Chop off the header to prepare for bundling
                    doc.DocumentNode.ChildNodes[ 0 ].Remove();

                    //add this node to all nodes in main XML doc
                    parentElem.InnerXml += doc.DocumentNode.OuterHtml;
                    xmlDocument.AppendChild( parentElem );
                }

                if (!DryRun)
                {
                    WriteRedirects( redirects, settings );
                    xmlDocument.Save( Path.Combine( settings.ObjectDirectory, settings.PageType, settings.AllNodesFilename ) );

                    console.WriteLine( $"Extraction to {settings.ObjectDirectory} succeeded" );
                }
                else
                {
                    console.WriteLine( "Dry run succeeded" );
                }
            }
        }

        private void PrepareObjectDirectory( ExtractorSettings settings )
        {
            if( Clean && Directory.Exists(settings.ObjectDirectory))
            {
                Directory.Delete( settings.ObjectDirectory );
            }

            if( !Directory.Exists( settings.ObjectDirectory ) )
            {
                Directory.CreateDirectory( settings.ObjectDirectory );
            }

            var pageTypePath = Path.Combine( settings.ObjectDirectory, settings.PageType );
            if( !Directory.Exists( pageTypePath ) )
            {
                Directory.CreateDirectory( pageTypePath );
            }
        }

        private static void WriteRedirects( IEnumerable<(string, string)> redirects, ExtractorSettings settings )
        {
            //Doc to save page alias redirects
            var xmlRedirects = new XmlDocument();
            var parentRedirectElem = xmlRedirects.CreateNode( "element", "rules", "" );

            //Start creating the 301 redirect rule needed for this page
            redirects.ToList().ForEach( r =>
            {
                var ruleNode = xmlRedirects.CreateNode( XmlNodeType.Element, "rule", "" );
                var attr = xmlRedirects.CreateAttribute( "name" );
                attr.Value = $"redirect alias {r.Item2} rule"; //simple name
                ruleNode.Attributes.Append( attr );

                var matchNode = xmlRedirects.CreateNode( XmlNodeType.Element, "match", "" );
                attr = xmlRedirects.CreateAttribute( "url" );
                attr.Value = $"^{r.Item1}"; //fromAlias
                matchNode.Attributes.Append( attr );
                ruleNode.AppendChild( matchNode );

                var actionNode = xmlRedirects.CreateNode( XmlNodeType.Element, "action", "" );
                attr = xmlRedirects.CreateAttribute( "type" );
                attr.Value = "Redirect";
                actionNode.Attributes.Append( attr );
                attr = xmlRedirects.CreateAttribute( "url" );
                attr.Value = $"/{r.Item2}"; //toAlias
                actionNode.Attributes.Append( attr );
                attr = xmlRedirects.CreateAttribute( "redirectType" );
                attr.Value = "Permanent";
                actionNode.Attributes.Append( attr );
                attr = xmlRedirects.CreateAttribute( "appendQueryString" );
                attr.Value = "true";
                actionNode.Attributes.Append( attr );
                ruleNode.AppendChild( actionNode );

                parentRedirectElem.AppendChild( ruleNode );
            } );

            //add the single redirect rule to the allRules parent }";e
            xmlRedirects.AppendChild( parentRedirectElem );
            xmlRedirects.Save( Path.Combine( settings.ObjectDirectory, settings.PageType, settings.AllRedirectsFilename ) );
        }

        private static void EnsureValidSettings( ExtractorSettings settings )
        {
            if( string.IsNullOrWhiteSpace( settings.AllNodesFilename ) )
            {
                throw new ValidationException( $"{nameof( settings.AllNodesFilename )} cannot be blank." );
            }

            if( string.IsNullOrWhiteSpace( settings.AllRedirectsFilename ) )
            {
                throw new ValidationException( $"{nameof( settings.AllRedirectsFilename )} cannot be blank." );
            }

            if( string.IsNullOrWhiteSpace( settings.PageType ) )
            {
                throw new ValidationException( $"{nameof( settings.PageType )} cannot be blank." );
            }
        }
    }
}
