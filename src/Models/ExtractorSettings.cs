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

        public string XperienceUser { get; set; } = "administrator";

    }

}
