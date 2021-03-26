using System.ComponentModel.DataAnnotations;
using CMS.DocumentEngine;

namespace XperienceContentXtractor.Models
{

    public class ExtractorSettings
    {
        [Required]
        public string AbsoluteSiteName { get; set; }

        [Required]
        public string AllNodesFilename { get; set; } = "allNodes.xml";

        [Required]
        public string AllRedirectsFilename { get; set; } = "allRedirects.xml";

        [Required]
        public string ConnectionString { get; set; }

        [Required]
        public string HashStringSalt { get; set; }

        public string ObjectDirectory { get; set; } = ".xp-extractor";

        public string[] OrderByColumns { get; set; } = new[] { nameof( TreeNode.NodeAliasPath ) };

        [Required]
        public string PageType { get; set; }

        public string[] PageTypeColumns { get; set; }

        public string RootNodeAliasPath { get; set; } = "/";

        [Range( 1, int.MaxValue )]
        public int TopN { get; set; } = int.MaxValue;

        public string XperienceUser { get; set; } = "Administrator";

        //  //TODO - Move into CLI parameters
        //  var nodeOrder = 1;
        //  var dataRootDirectoryName = "Generated";
        //  var pageTypeNameIdentifier = "CMS.BlogPost";
        //  var pageTypeColumns = new string []{"NodeAliasPath", "BlogPostTitle", "BlogPostDate", "BlogPostSummary", "BlogPostBody", "BlogPostTeaser", "BlogPostThumb", "DocumentTags", "KenticoRocksFile"};
        //  var orderByString = "[BlogPostDate] ASC";
        //  var topN = 500;
        //  var contentPathToStart = "/blog";

    }

}
