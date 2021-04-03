using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using XperienceContentXtractor.Models;

namespace XperienceContentXtractor.Extensions
{

    public static class ExtractorSettingsExtensions
    {

        public static void Validate( this ExtractorSettings settings )
        {
            var context = new ValidationContext( settings, serviceProvider: null, items: null );
            Validator.ValidateObject( settings, context, true );
        }

    }

}
