using System;
using System.IO;
using CMS.Base;
using CMS.Membership;
using System.Data;
using CMS.DocumentEngine;
using CMS.MacroEngine;
using CMS.Helpers;
using System.Linq;
using System.Net;
using System.Xml;
using HtmlAgilityPack;

namespace XperienceContentExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the Kentico Xperience Portal Engine Content Extractor!");

            //TODO - Move into CLI parameters
            var nodeOrder = 1;
            var dataRootDirectoryName = "Generated";
            var pageTypeNameIdentifier = "CMS.BlogPost";
            var pageTypeColumns = new string []{"NodeAliasPath", "BlogPostTitle", "BlogPostDate", "BlogPostSummary", "BlogPostBody", "BlogPostTeaser", "BlogPostThumb", "DocumentTags", "KenticoRocksFile"};
            var orderByString = "[BlogPostDate] ASC";
            var topN = 500;
            var contentPathToStart = "/blog";
            

            if(!Directory.Exists(dataRootDirectoryName))
            {
                Directory.CreateDirectory(dataRootDirectoryName);
            }

            if(!Directory.Exists($@"{dataRootDirectoryName}\{pageTypeNameIdentifier}"))
            {
                Directory.CreateDirectory($@"{dataRootDirectoryName}\{pageTypeNameIdentifier}");
            }
            
            //Doc to save page alias redirects
            var xmlRedirects = new XmlDocument();
            var parentRedirectElem = xmlRedirects.CreateNode("element", "rules", "");

            //Doc to save the actual content as xml
            var xmlDocument = new XmlDocument();
            var parentElem = xmlDocument.CreateNode("element", pageTypeNameIdentifier.Replace(".","-")+"s", "");

            //Fire up our Kentico Xperience instance
             CMS.DataEngine.CMSApplication.Init();

             var absoluteSiteName = "https://www.mcbeev.com/"; //TODO - can figure out easily later based on Site

            // Gets an object representing a specific Kentico user
            UserInfo user = UserInfoProvider.GetUserInfo("Administrator");           

            // Sets the context of the user
            using (new CMSActionContext(user))
            {
                var wc = new WebClient();

                //Get me all published pages of the specified page type on the site
                var posts = DocumentHelper.GetDocuments(pageTypeNameIdentifier)
                                    .Columns(pageTypeColumns.Join(","))
                                    .Path(contentPathToStart, PathTypeEnum.Children)
                                    .PublishedVersion()
                                    .Published()
                                    .TopN(topN)
                                    .OrderBy(orderByString);

                foreach (TreeNode post in posts)
                {  
                    //Create a new AgilityPack doc for working with the page
                    HtmlDocument doc = new HtmlDocument();
                    doc.OptionOutputAsXml = false; //if true adds a weird span tag

                    doc.DocumentNode.AppendChild(HtmlNode.CreateNode("<?xml version=\"1.0\" encoding=\"UTF-8\"?>"));
                    doc.DocumentNode.AppendChild(HtmlNode.CreateNode("\r\n"));

                    string nodeAliasPath = post.NodeAliasPath;
                    
                    nodeAliasPath = nodeAliasPath.Split('/').Last(); //clean it up to just the slug
                                        
                    //Start creating the 301 redirect rule needed for this page
                    var ruleNode = xmlRedirects.CreateNode(XmlNodeType.Element, "rule", "");
                    var attr = xmlRedirects.CreateAttribute("name");
                    attr.Value = $"redirect alias {nodeAliasPath} rule"; //simple name
                    ruleNode.Attributes.Append(attr);

                    var matchNode = xmlRedirects.CreateNode(XmlNodeType.Element, "match", "");
                    attr = xmlRedirects.CreateAttribute("url");
                    attr.Value = $"^{post.NodeAliasPath}"; //fromAlias
                    matchNode.Attributes.Append(attr);
                    ruleNode.AppendChild(matchNode);

                    var actionNode = xmlRedirects.CreateNode(XmlNodeType.Element, "action", "");
                    attr = xmlRedirects.CreateAttribute("type");
                    attr.Value = "Redirect";
                    actionNode.Attributes.Append(attr);
                    attr = xmlRedirects.CreateAttribute("url");
                    attr.Value = $"/{nodeAliasPath}"; //toAlias
                    actionNode.Attributes.Append(attr);
                    attr = xmlRedirects.CreateAttribute("redirectType");
                    attr.Value = "Permanent";
                    actionNode.Attributes.Append(attr);
                    attr = xmlRedirects.CreateAttribute("appendQueryString");
                    attr.Value = "true";
                    actionNode.Attributes.Append(attr);                    
                    ruleNode.AppendChild(actionNode);

                    //add the single redirect rule to the allRules parent 
                    parentRedirectElem.AppendChild(ruleNode);

                    //Get the main Node Title / Document Name field (always the second field name specified in app config params )
                    string nodeTitle = post.GetValue<string>(pageTypeColumns[1], "");

                    //Create a parent folder path in the data directory to store all xml about this node in a structured way on the file system
                    string nodeDirPath = $@"{dataRootDirectoryName}\{pageTypeNameIdentifier}\{nodeAliasPath.Replace("/","-")}";
                    if(!Directory.Exists(nodeDirPath))
                    {
                        Directory.CreateDirectory(nodeDirPath);
                    }

                    //Create root xml Element of the pageTypeName ex. <cms-blogpost>
                    // and start saving fields as attributes and child elements in this xml structure
                    var rootNode = HtmlNode.CreateNode($"<{pageTypeNameIdentifier.Replace(".","-")} />");
                    rootNode.Attributes.Append("originalAliasPath");
                    rootNode.SetAttributeValue("originalAliasPath", post.NodeAliasPath);
                    rootNode.AppendChild(HtmlNode.CreateNode("\r\n"));

                    var aliasNode = HtmlNode.CreateNode($"<newaliaspath/>");
                    aliasNode.InnerHtml = nodeAliasPath;
                    rootNode.AppendChild(aliasNode);

                    var orderNode = HtmlNode.CreateNode($"<newnodeorder/>");
                    orderNode.InnerHtml = nodeOrder.ToString();
                    rootNode.AppendChild(orderNode);
                    nodeOrder++;

                    //Kentico dynamic properties or "With the Coupled Columns of the page type"
                    //Iterate through all of the fields of a document in order to serialize them to xml
                    foreach(var dprop in post.Properties)
                    {
                        if(Array.IndexOf(pageTypeColumns, dprop) > 0)
                        {
                            //Create xml Element that represents each field ex. <blogpostsummary>data</blogpostsummary>
                            var node = HtmlNode.CreateNode($"<{dprop} />");
                            var data = post.GetValue<string>(dprop, "");

                            //Special stuff for Mcbeev Blog Post Summary (can be ignored for others)
                            if(dprop.ToLower().Contains("summary"))
                            {
                                //Remove wrapped p tag on Mcbeev Blog Post Summary
                                HtmlDocument docSummary = new HtmlDocument();
                                docSummary.LoadHtml(data);
                                var newSummaryNode = HtmlNode.CreateNode($"<newblogpostsummary/>");
                                try {

                                    if(docSummary.DocumentNode.SelectSingleNode("//p") != null){
                                        newSummaryNode.InnerHtml = docSummary.DocumentNode.SelectSingleNode("//p").InnerText;
                                    }
                                    else {
                                        newSummaryNode.InnerHtml = docSummary.DocumentNode.InnerText;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    //todo
                                    Console.WriteLine(ex.Message);
                                }
                                rootNode.AppendChild(newSummaryNode); 
                                rootNode.AppendChild(HtmlNode.CreateNode("\r\n"));  
                            }                

                            //This is WYSIWYG content field most likely so try to resolve things as Kentico would
                            if(dprop.ToLower().Contains("title") || dprop.ToLower().Contains("body") || dprop.ToLower().Contains("content") || dprop.ToLower().Contains("summary"))
                            {
                                //Macro resolve inside of the content (handle K# macros in the text)
                                data = MacroResolver.Resolve(data);

                                //Url resolve inside of the content (handle relative urls into absolute urls) important!
                                data = HTMLHelper.ResolveUrls(data, absoluteSiteName, true);

                                //Widget identifier (try to save a list of all Portal Inline Widgets that no longer work in MVC)
                                if(data.Contains("CMSInlineControl"))
                                {
                                    Console.WriteLine("Found a widget in this content.");
                                    // TODO Make this a real log
                                    // //Render the value as html to HP Node
                                    // HtmlDocument docWidget = new HtmlDocument();
                                    // docWidget.LoadHtml(data);
                                    // var widgets = docWidget.DocumentNode.SelectNodes("//object[contains(@type, 'Widget')]");
                                    
                                    // foreach (var w in widgets)
                                    // {          
                                    //     Console.WriteLine(w.OuterHtml);
                                    // }   
                                }

                                //Media library identifier
                                if(data.Contains("<img "))
                                {
                                    HtmlDocument docImg = new HtmlDocument();
                                    docImg.LoadHtml(data);
                                    var ImageURLs = docImg.DocumentNode.Descendants("img")
										.Select(e => e.GetAttributeValue("src", null))
										.Where(s => !String.IsNullOrEmpty(s));

                                    foreach(var oldImageURL in ImageURLs)
                                    {
                                        //Skip mp3s for now
                                        if(oldImageURL.EndsWith(".mp3") || oldImageURL.Contains(".mp3?"))
                                        {
                                            continue;
                                        }

                                        Console.WriteLine($"trying to save {oldImageURL} ...");
                                        var safeName = oldImageURL.Split(new char[]{'?'})[0];                                        
                                        safeName = safeName.Replace(".aspx", "");

                                        //Save the images locally if we can download from the "real" running site
                                        try
                                        {
                                            wc.DownloadFile(oldImageURL, Path.Combine(nodeDirPath, Path.GetFileName(safeName)));
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine(ex.Message);
                                        }  

                                        //rewrite the image path back in the data node
                                        // TODO - make this a config parameter and not hard coded to Mcbeev.com                                 
                                        data = data.Replace(oldImageURL, "/MBV/media/blog/" + nodeAliasPath.Replace("/","-") + "/" + oldImageURL.Split(new char[]{'/'}).Last());

                                        //replace the odd .png.aspx thing
                                        data = data.Replace(".png.aspx", ".png");
                                    }
                                }

                                //Use HTMLAgility Pack to fix non closed image tags
                                HtmlNode.ElementsFlags["img"] = HtmlElementFlag.Closed;
                                HtmlDocument docContent = new HtmlDocument();
                                docContent.LoadHtml(data);

                                data = $"<![CDATA[{docContent.DocumentNode.OuterHtml}]]>";
                            }

                            //Most likely image only field and not a WYSIWYG field
                            // TODO - is there a better way to check for this?
                            if((data.Length > 2) && (data.StartsWith("~/")))
                            {
                                data = data.Replace("~/", absoluteSiteName);

                                Console.WriteLine($"trying to save {data} ...");
                                var safeName = data.Split(new char[]{'?'})[0];
                                safeName = safeName.Replace(".aspx", "");
                                wc.DownloadFile(data, Path.Combine(nodeDirPath, Path.GetFileName(safeName)));

                                //rewrite the image path back in the data node                                      
                                safeName = "/MBV/media/blog/" + nodeAliasPath.Replace("/","-") + "/" + safeName.Split(new char[]{'/'}).Last();

                                data = $"<![CDATA[{safeName}]]>";
                            }

                            node.AppendChild(doc.CreateTextNode(data));
                            rootNode.AppendChild(node);
                            rootNode.AppendChild(HtmlNode.CreateNode("\r\n"));
                        }
                    }
                    doc.DocumentNode.AppendChild(rootNode);

                    //Save the single node to its own XML document in its directory
                    doc.Save($@"{nodeDirPath}\{nodeAliasPath.Replace("/","-")}.xml");

                    //Chop off the header to prepare for bundling
                    doc.DocumentNode.ChildNodes[0].Remove();
                    
                    //add this node to all nodes in main XML doc
                    parentElem.InnerXml += doc.DocumentNode.OuterHtml;
                    xmlDocument.AppendChild(parentElem);
                }                    
                Console.WriteLine($"{posts.Count} nodes found.");

                xmlDocument.Save($@"{dataRootDirectoryName}\{pageTypeNameIdentifier}\allNodes.xml");

                xmlRedirects.AppendChild(parentRedirectElem);
                xmlRedirects.Save($@"{dataRootDirectoryName}\{pageTypeNameIdentifier}\allRedirects.xml");
            }       
        }
    }
}
